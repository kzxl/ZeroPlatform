using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Optical Axial and Lateral Chromatic Aberration Defringe Engine.
    /// Selectively detects and neutralizes unsightly purple and green color fringing along high-contrast edges
    /// (e.g. backlit tree branches, specular metallic highlights, architectural silhouettes)
    /// without desaturating flat colored subjects like flowers or clothing.
    /// </summary>
    public static class DefringeFilter
    {
        /// <summary>
        /// Applies edge-guided defringe to a Bgra32 ImageBuffer.
        /// </summary>
        public static unsafe void Apply(
            ImageBuffer src,
            ImageBuffer dst,
            float purpleAmount = 0.5f,
            float greenAmount = 0.5f,
            float edgeThreshold = 0.12f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("Defringe filter requires Bgra32 format buffers.");

            if (purpleAmount < 1e-4f && greenAmount < 1e-4f)
            {
                src.CopyTo(dst);
                return;
            }

            int w = src.Width;
            int h = src.Height;
            if (w < 3 || h < 3)
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

            ApplyRgbaFloat(pixels, w, h, purpleAmount, greenAmount, edgeThreshold);

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
        /// High-performance edge-guided defringing on interleaved RGBA float buffer [0..1].
        /// When edgeThreshold > 0, restricts defringing to high-contrast edges.
        /// When edgeThreshold <= 0, applies global defringing across all saturated purple/green pixels.
        /// Modifies pixels in-place.
        /// </summary>
        public static void ApplyRgbaFloat(
            float[] pixels,
            int w,
            int h,
            float purpleAmount = 0.5f,
            float greenAmount = 0.5f,
            float edgeThreshold = 0.0f)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (w <= 0 || h <= 0) throw new ArgumentException("Dimensions must be positive.");
            if (purpleAmount < 1e-4f && greenAmount < 1e-4f) return;

            float pAmt = Clamp(purpleAmount, 0f, 1f);
            float gAmt = Clamp(greenAmount, 0f, 1f);
            bool useEdgeGuidance = edgeThreshold > 0.001f;

            // 1. Compute perceptual luminance plane
            float[] luma = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    // Rec.709 luminance
                    luma[row + x] = 0.2126f * pixels[p] + 0.7152f * pixels[p + 1] + 0.0722f * pixels[p + 2];
                }
            });

            // 2. Compute Sobel luminance edge magnitude if edge guidance is requested
            float[]? expandedEdge = null;
            if (useEdgeGuidance && w >= 3 && h >= 3)
            {
                float[] edgeMag = new float[w * h];
                Parallel.For(1, h - 1, y =>
                {
                    int row = y * w;
                    int rowPrev = (y - 1) * w;
                    int rowNext = (y + 1) * w;

                    for (int x = 1; x < w - 1; x++)
                    {
                        float tl = luma[rowPrev + x - 1], tc = luma[rowPrev + x], tr = luma[rowPrev + x + 1];
                        float ml = luma[row + x - 1],                            mr = luma[row + x + 1];
                        float bl = luma[rowNext + x - 1], bc = luma[rowNext + x], br = luma[rowNext + x + 1];

                        float gx = (tr + 2f * mr + br) - (tl + 2f * ml + bl);
                        float gy = (bl + 2f * bc + br) - (tl + 2f * tc + tr);
                        float mag = (float)Math.Sqrt(gx * gx + gy * gy);
                        edgeMag[row + x] = mag;
                    }
                });

                // Expand edge mask slightly (1-pixel dilation) to catch fringe pixels slightly offset from the boundary
                expandedEdge = FastDilate3x3(edgeMag, w, h);
            }

            // 3. Process defringing per pixel
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    float edgeWeight = 1.0f;

                    if (useEdgeGuidance && expandedEdge != null)
                    {
                        float edge = expandedEdge[idx];
                        if (edge < edgeThreshold) continue; // Skip non-edge flat areas
                        edgeWeight = Clamp((edge - edgeThreshold) / edgeThreshold, 0f, 1f);
                    }

                    int p = idx * 4;
                    float r = pixels[p];
                    float g = pixels[p + 1];
                    float b = pixels[p + 2];

                    // Convert to HSV / Hue
                    float max = Math.Max(r, Math.Max(g, b));
                    float min = Math.Min(r, Math.Min(g, b));
                    float delta = max - min;
                    if (delta < 0.05f || max < 1e-4f) continue; // Neutral gray, no fringe

                    float sat = delta / max;
                    if (sat < 0.08f) continue;

                    float hue;
                    if (max == r)
                    {
                        hue = 60f * (((g - b) / delta) % 6f);
                    }
                    else if (max == g)
                    {
                        hue = 60f * (((b - r) / delta) + 2f);
                    }
                    else
                    {
                        hue = 60f * (((r - g) / delta) + 4f);
                    }
                    if (hue < 0f) hue += 360f;

                    // Evaluate fringe confidence
                    float fringeFactor = 0f;

                    // Purple fringe: ~240° to 340° (peak at 285°)
                    if (pAmt > 0f && hue >= 240f && hue <= 340f)
                    {
                        float dist = Math.Abs(hue - 285f) / 55f;
                        float weight = 1f - dist * dist;
                        if (weight > 0f) fringeFactor = pAmt * weight;
                    }
                    // Green fringe: ~75° to 165° (peak at 120°)
                    else if (gAmt > 0f && hue >= 75f && hue <= 165f)
                    {
                        float dist = Math.Abs(hue - 120f) / 45f;
                        float weight = 1f - dist * dist;
                        if (weight > 0f) fringeFactor = gAmt * weight;
                    }

                    if (fringeFactor <= 0f) continue;

                    float totalAtt = fringeFactor * edgeWeight;
                    if (totalAtt <= 1e-4f) continue;

                    // Neutral luminance target
                    float Y = luma[idx];

                    // Desaturate fringe towards edge luminance
                    pixels[p] = r + (Y - r) * totalAtt;
                    pixels[p + 1] = g + (Y - g) * totalAtt;
                    pixels[p + 2] = b + (Y - b) * totalAtt;
                }
            });
        }

        private static float[] FastDilate3x3(float[] src, int w, int h)
        {
            float[] dst = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float maxVal = src[row + x];
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        int nRow = ny * w;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            float v = src[nRow + nx];
                            if (v > maxVal) maxVal = v;
                        }
                    }
                    dst[row + x] = maxVal;
                }
            });
            return dst;
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
