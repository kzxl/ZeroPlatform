using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Professional Dark Channel Prior (DCP) Dehaze and Atmospheric Depth Recovery Engine.
    /// Recovers contrast, saturation, and dynamic range obscured by haze, mist, fog, or industrial smoke
    /// based on the physical atmospheric scattering model (He, Sun, Tang).
    /// </summary>
    public static class DehazeFilter
    {
        /// <summary>
        /// Applies Dark Channel Prior dehazing to a Bgra32 ImageBuffer.
        /// </summary>
        /// <param name="src">Source Bgra32 buffer.</param>
        /// <param name="dst">Destination Bgra32 buffer.</param>
        /// <param name="amount">Effect intensity [-1..1]. Positive values remove haze, negative values add atmospheric mist.</param>
        /// <param name="omega">Haze removal parameter (0.8..0.98, default 0.95) controlling natural aerial depth preservation.</param>
        /// <param name="patchRadius">Neighborhood radius for dark channel estimation (default 5).</param>
        /// <param name="t0">Lower transmission floor (default 0.1f) preventing high-frequency noise blowout in deep haze.</param>
        public static unsafe void Apply(
            ImageBuffer src,
            ImageBuffer dst,
            float amount = 0.5f,
            float omega = 0.95f,
            int patchRadius = 5,
            float t0 = 0.1f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("Dehaze filter requires Bgra32 format buffers.");
            if (Math.Abs(amount) < 1e-4f)
            {
                src.CopyTo(dst);
                return;
            }

            int w = src.Width;
            int h = src.Height;
            if (w < 4 || h < 4)
            {
                src.CopyTo(dst);
                return;
            }

            // Convert Bgra32 to normalized float RGBA
            float[] pixels = new float[w * h * 4];
            Parallel.For(0, h, y =>
            {
                byte* sRow = src.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    pixels[fx] = sRow[bx + 2] / 255.0f;     // R
                    pixels[fx + 1] = sRow[bx + 1] / 255.0f; // G
                    pixels[fx + 2] = sRow[bx] / 255.0f;     // B
                    pixels[fx + 3] = sRow[bx + 3] / 255.0f; // A
                }
            });

            // Process float buffer in-place
            ApplyRgbaFloat(pixels, w, h, amount, omega, patchRadius, t0);

            // Convert back to Bgra32
            Parallel.For(0, h, y =>
            {
                byte* dRow = dst.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = ClampByte((int)(pixels[fx] * 255.0f + 0.5f));     // R
                    dRow[bx + 1] = ClampByte((int)(pixels[fx + 1] * 255.0f + 0.5f)); // G
                    dRow[bx] = ClampByte((int)(pixels[fx + 2] * 255.0f + 0.5f));     // B
                    dRow[bx + 3] = ClampByte((int)(pixels[fx + 3] * 255.0f + 0.5f)); // A
                }
            });
        }

        /// <summary>
        /// High-performance Dark Channel Prior dehazing on interleaved RGBA float buffer [0..1].
        /// Modifies pixels in-place.
        /// </summary>
        public static void ApplyRgbaFloat(
            float[] pixels,
            int w,
            int h,
            float amount = 0.5f,
            float omega = 0.95f,
            int patchRadius = 5,
            float t0 = 0.1f)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (w <= 0 || h <= 0) throw new ArgumentException("Dimensions must be positive.");
            if (Math.Abs(amount) < 1e-4f) return;

            int n = w * h;
            if (patchRadius < 1) patchRadius = 1;
            if (t0 < 0.01f) t0 = 0.01f;
            omega = Clamp(omega, 0.5f, 1.0f);

            // 1. Compute raw dark channel: dark(x) = min(R, G, B)
            float[] darkRaw = new float[n];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    float r = pixels[p];
                    float g = pixels[p + 1];
                    float b = pixels[p + 2];
                    float min = r < g ? (r < b ? r : b) : (g < b ? g : b);
                    darkRaw[row + x] = min;
                }
            });

            // 2. Compute patch min dark channel: darkPatch(x) = min in neighborhood
            float[] darkPatch = FastBoxMin(darkRaw, w, h, patchRadius);

            // 3. Estimate Atmospheric Light (A): top 0.1% brightest pixels in darkPatch
            int topCount = Math.Max(1, n / 1000);
            float[] sampleIndices = SelectTopIndices(darkPatch, topCount);

            float airR = 0f, airG = 0f, airB = 0f;
            for (int i = 0; i < sampleIndices.Length; i++)
            {
                int idx = (int)sampleIndices[i];
                int p = idx * 4;
                airR += pixels[p];
                airG += pixels[p + 1];
                airB += pixels[p + 2];
            }
            float invTop = 1.0f / sampleIndices.Length;
            airR = Clamp(airR * invTop, 0.05f, 1.0f);
            airG = Clamp(airG * invTop, 0.05f, 1.0f);
            airB = Clamp(airB * invTop, 0.05f, 1.0f);

            // 4. Estimate transmission map: t_raw = 1 - omega * min(R/airR, G/airG, B/airB)
            float[] transmission = new float[n];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    float normR = pixels[p] / airR;
                    float normG = pixels[p + 1] / airG;
                    float normB = pixels[p + 2] / airB;
                    float normMin = normR < normG ? (normR < normB ? normR : normB) : (normG < normB ? normG : normB);
                    transmission[row + x] = normMin;
                }
            });

            // Patch-min on normalized channels
            float[] minNorm = FastBoxMin(transmission, w, h, patchRadius);
            Parallel.For(0, n, i =>
            {
                float t = 1.0f - omega * minNorm[i];
                transmission[i] = Clamp(t, t0, 1.0f);
            });

            // 5. Refine transmission map with edge-preserving box smoothing
            float[] tSmooth = FastBoxBlur(transmission, w, h, Math.Max(2, patchRadius / 2));

            // 6. Scene Radiance Recovery: J = (I - A) / max(t, t0) + A
            float amt = Clamp(amount, -1f, 1f);
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    int p = idx * 4;
                    float t = Math.Max(tSmooth[idx], t0);

                    float r = pixels[p];
                    float g = pixels[p + 1];
                    float b = pixels[p + 2];

                    if (amt >= 0f)
                    {
                        // Dehaze
                        float jr = (r - airR) / t + airR;
                        float jg = (g - airG) / t + airG;
                        float jb = (b - airB) / t + airB;

                        pixels[p] = Math.Max(0f, r + (jr - r) * amt);
                        pixels[p + 1] = Math.Max(0f, g + (jg - g) * amt);
                        pixels[p + 2] = Math.Max(0f, b + (jb - b) * amt);
                    }
                    else
                    {
                        // Add atmospheric mist
                        float mist = -amt * (1.0f - t);
                        pixels[p] = r + (airR - r) * mist;
                        pixels[p + 1] = g + (airG - g) * mist;
                        pixels[p + 2] = b + (airB - b) * mist;
                    }
                }
            });
        }

        private static float[] FastBoxMin(float[] src, int w, int h, int radius)
        {
            float[] temp = new float[w * h];
            float[] dst = new float[w * h];

            // Horizontal min pass
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - radius);
                    int x1 = Math.Min(w - 1, x + radius);
                    float minVal = src[row + x0];
                    for (int k = x0 + 1; k <= x1; k++)
                    {
                        float v = src[row + k];
                        if (v < minVal) minVal = v;
                    }
                    temp[row + x] = minVal;
                }
            });

            // Vertical min pass
            Parallel.For(0, w, x =>
            {
                for (int y = 0; y < h; y++)
                {
                    int y0 = Math.Max(0, y - radius);
                    int y1 = Math.Min(h - 1, y + radius);
                    float minVal = temp[y0 * w + x];
                    for (int k = y0 + 1; k <= y1; k++)
                    {
                        float v = temp[k * w + x];
                        if (v < minVal) minVal = v;
                    }
                    dst[y * w + x] = minVal;
                }
            });

            return dst;
        }

        private static float[] FastBoxBlur(float[] src, int w, int h, int radius)
        {
            float[] temp = new float[w * h];
            float[] dst = new float[w * h];

            // Horizontal average pass
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                float sum = 0f;
                int count = 0;

                for (int x = 0; x <= Math.Min(radius, w - 1); x++)
                {
                    sum += src[row + x];
                    count++;
                }

                for (int x = 0; x < w; x++)
                {
                    temp[row + x] = sum / count;

                    int addX = x + radius + 1;
                    if (addX < w)
                    {
                        sum += src[row + addX];
                        count++;
                    }

                    int subX = x - radius;
                    if (subX >= 0)
                    {
                        sum -= src[row + subX];
                        count--;
                    }
                }
            });

            // Vertical average pass
            Parallel.For(0, w, x =>
            {
                float sum = 0f;
                int count = 0;

                for (int y = 0; y <= Math.Min(radius, h - 1); y++)
                {
                    sum += temp[y * w + x];
                    count++;
                }

                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum / count;

                    int addY = y + radius + 1;
                    if (addY < h)
                    {
                        sum += temp[addY * w + x];
                        count++;
                    }

                    int subY = y - radius;
                    if (subY >= 0)
                    {
                        sum -= temp[subY * w + x];
                        count--;
                    }
                }
            });

            return dst;
        }

        private static float[] SelectTopIndices(float[] dark, int topCount)
        {
            int n = dark.Length;
            if (topCount >= n)
            {
                float[] all = new float[n];
                for (int i = 0; i < n; i++) all[i] = i;
                return all;
            }

            // Quick histogram binning for top fraction
            int[] bins = new int[256];
            for (int i = 0; i < n; i++)
            {
                int b = (int)(Clamp(dark[i], 0f, 1f) * 255.0f);
                bins[b]++;
            }

            int accum = 0;
            int thresholdBin = 255;
            for (int b = 255; b >= 0; b--)
            {
                accum += bins[b];
                if (accum >= topCount)
                {
                    thresholdBin = b;
                    break;
                }
            }

            float thr = thresholdBin / 255.0f;
            float[] result = new float[Math.Min(topCount, accum)];
            int collected = 0;

            for (int i = 0; i < n && collected < result.Length; i++)
            {
                if (dark[i] >= thr)
                {
                    result[collected++] = i;
                }
            }

            if (collected < result.Length)
            {
                Array.Resize(ref result, collected);
            }

            return result;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static byte ClampByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }
    }
}
