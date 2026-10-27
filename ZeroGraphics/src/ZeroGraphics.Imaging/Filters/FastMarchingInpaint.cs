using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Telea Fast Marching Image Inpainting (Alexandru Telea, 2004).
    /// Restores missing, damaged, or masked regions by propagating boundary information inward along isophotes
    /// (lines of equal luminance) guided by the Fast Marching Method Eikonal distance field.
    /// </summary>
    public static class FastMarchingInpaint
    {
        private const byte FlagKnown = 0;
        private const byte FlagBand = 1;
        private const byte FlagInside = 2;

        /// <summary>
        /// Inpaints missing regions defined by a boolean mask on a Bgra32 ImageBuffer.
        /// </summary>
        public static unsafe void Inpaint(
            ImageBuffer src,
            ImageBuffer dst,
            bool[] mask,
            int radius = 3)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("FastMarchingInpaint requires Bgra32 buffers.");

            int w = src.Width;
            int h = src.Height;
            if (mask.Length != w * h)
                throw new ArgumentException("Mask length must match image pixel count.");

            float[] pixels = new float[w * h * 4];
            Parallel.For(0, h, y =>
            {
                byte* sRow = src.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    pixels[fx] = sRow[bx + 2] * (1f / 255f);     // R
                    pixels[fx + 1] = sRow[bx + 1] * (1f / 255f); // G
                    pixels[fx + 2] = sRow[bx] * (1f / 255f);     // B
                    pixels[fx + 3] = sRow[bx + 3] * (1f / 255f); // A
                }
            });

            InpaintRgbaFloat(pixels, w, h, mask, radius);

            Parallel.For(0, h, y =>
            {
                byte* dRow = dst.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = ClampByte((int)(pixels[fx] * 255f + 0.5f));     // R
                    dRow[bx + 1] = ClampByte((int)(pixels[fx + 1] * 255f + 0.5f)); // G
                    dRow[bx] = ClampByte((int)(pixels[fx + 2] * 255f + 0.5f));     // B
                    dRow[bx + 3] = ClampByte((int)(pixels[fx + 3] * 255f + 0.5f)); // A
                }
            });
        }

        /// <summary>
        /// Performs Telea Fast Marching Inpainting on an interleaved RGBA float buffer [0..1].
        /// Modifies masked pixels in-place.
        /// </summary>
        public static void InpaintRgbaFloat(
            float[] pixels,
            int w,
            int h,
            bool[] mask,
            int radius = 3)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (w <= 0 || h <= 0) throw new ArgumentException("Dimensions must be positive.");
            if (mask.Length != w * h) throw new ArgumentException("Mask length must match image pixel count.");

            int n = w * h;
            radius = MathCompat.Clamp(radius, 1, 8);
            float radiusSq = radius * radius;

            byte[] flags = new byte[n];
            float[] dist = new float[n];
            var band = new PriorityQueue<int, float>();

            // 1. Initialize distance and states
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    if (mask[idx])
                    {
                        // Check if boundary pixel (has at least 1 known neighbor)
                        bool isBorder = false;
                        if (x > 0 && !mask[idx - 1]) isBorder = true;
                        else if (x < w - 1 && !mask[idx + 1]) isBorder = true;
                        else if (y > 0 && !mask[idx - w]) isBorder = true;
                        else if (y < h - 1 && !mask[idx + w]) isBorder = true;

                        if (isBorder)
                        {
                            flags[idx] = FlagBand;
                            dist[idx] = 1.0f;
                            band.Enqueue(idx, 1.0f);
                        }
                        else
                        {
                            flags[idx] = FlagInside;
                            dist[idx] = 1e9f;
                        }
                    }
                    else
                    {
                        flags[idx] = FlagKnown;
                        dist[idx] = 0.0f;
                    }
                }
            }

            // If no hole pixels, return
            if (band.Count == 0) return;

            // 2. Fast Marching Inpainting propagation loop
            while (band.Count > 0)
            {
                int idx = band.Dequeue();
                if (flags[idx] == FlagKnown) continue;

                flags[idx] = FlagKnown;
                int x = idx % w;
                int y = idx / w;

                // A. Compute gradient of distance function nabla T
                float gradX = 0f;
                if (x > 0 && x < w - 1)
                {
                    if (flags[idx - 1] == FlagKnown && flags[idx + 1] == FlagKnown)
                        gradX = (dist[idx + 1] - dist[idx - 1]) * 0.5f;
                    else if (flags[idx - 1] == FlagKnown)
                        gradX = dist[idx] - dist[idx - 1];
                    else if (flags[idx + 1] == FlagKnown)
                        gradX = dist[idx + 1] - dist[idx];
                }
                else if (x > 0 && flags[idx - 1] == FlagKnown)
                {
                    gradX = dist[idx] - dist[idx - 1];
                }
                else if (x < w - 1 && flags[idx + 1] == FlagKnown)
                {
                    gradX = dist[idx + 1] - dist[idx];
                }

                float gradY = 0f;
                if (y > 0 && y < h - 1)
                {
                    if (flags[idx - w] == FlagKnown && flags[idx + w] == FlagKnown)
                        gradY = (dist[idx + w] - dist[idx - w]) * 0.5f;
                    else if (flags[idx - w] == FlagKnown)
                        gradY = dist[idx] - dist[idx - w];
                    else if (flags[idx + w] == FlagKnown)
                        gradY = dist[idx + w] - dist[idx];
                }
                else if (y > 0 && flags[idx - w] == FlagKnown)
                {
                    gradY = dist[idx] - dist[idx - w];
                }
                else if (y < h - 1 && flags[idx + w] == FlagKnown)
                {
                    gradY = dist[idx + w] - dist[idx];
                }

                // Normalize distance gradient
                float gradNorm = MathF.Sqrt(gradX * gradX + gradY * gradY);
                if (gradNorm > 1e-6f)
                {
                    gradX /= gradNorm;
                    gradY /= gradNorm;
                }

                // B. Accumulate weighted colors from known neighbors within radius
                float sumR = 0f, sumG = 0f, sumB = 0f;
                float sumW = 0f;

                int yMin = Math.Max(0, y - radius);
                int yMax = Math.Min(h - 1, y + radius);
                int xMin = Math.Max(0, x - radius);
                int xMax = Math.Min(w - 1, x + radius);

                for (int qy = yMin; qy <= yMax; qy++)
                {
                    int dy = qy - y;
                    int qRow = qy * w;
                    for (int qx = xMin; qx <= xMax; qx++)
                    {
                        int dx = qx - x;
                        float dSq = dx * dx + dy * dy;
                        if (dSq > radiusSq || dSq < 0.5f) continue;

                        int qIdx = qRow + qx;
                        if (flags[qIdx] != FlagKnown) continue;

                        float dLen = MathF.Sqrt(dSq);

                        // Direction factor: alignment with isophote gradient
                        float dir = MathF.Abs(dx * gradX + dy * gradY) / dLen;
                        if (dir < 0.05f) dir = 0.05f;

                        // Geometric distance factor: 1 / d^2
                        float dstFactor = 1f / dSq;

                        // Level set factor: 1 / (1 + |T(p) - T(q)|)
                        float levFactor = 1f / (1f + MathF.Abs(dist[idx] - dist[qIdx]));

                        float weight = dir * dstFactor * levFactor;

                        int qp = qIdx * 4;
                        sumR += pixels[qp] * weight;
                        sumG += pixels[qp + 1] * weight;
                        sumB += pixels[qp + 2] * weight;
                        sumW += weight;
                    }
                }

                if (sumW > 1e-9f)
                {
                    float invW = 1f / sumW;
                    int p = idx * 4;
                    pixels[p] = sumR * invW;
                    pixels[p + 1] = sumG * invW;
                    pixels[p + 2] = sumB * invW;
                }

                // C. Update 4 neighbors and push unvisited INSIDE into BAND
                UpdateNeighbor(x + 1, y, w, h, flags, dist, band);
                UpdateNeighbor(x - 1, y, w, h, flags, dist, band);
                UpdateNeighbor(x, y + 1, w, h, flags, dist, band);
                UpdateNeighbor(x, y - 1, w, h, flags, dist, band);
            }
        }

        private static void UpdateNeighbor(
            int nx,
            int ny,
            int w,
            int h,
            byte[] flags,
            float[] dist,
            PriorityQueue<int, float> band)
        {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) return;
            int nIdx = ny * w + nx;

            if (flags[nIdx] == FlagInside)
            {
                flags[nIdx] = FlagBand;

                // Approximate Eikonal distance from known neighbors
                float dMin = 1e9f;
                if (nx > 0 && flags[nIdx - 1] == FlagKnown) dMin = MathF.Min(dMin, dist[nIdx - 1]);
                if (nx < w - 1 && flags[nIdx + 1] == FlagKnown) dMin = MathF.Min(dMin, dist[nIdx + 1]);
                if (ny > 0 && flags[nIdx - w] == FlagKnown) dMin = MathF.Min(dMin, dist[nIdx - w]);
                if (ny < h - 1 && flags[nIdx + w] == FlagKnown) dMin = MathF.Min(dMin, dist[nIdx + w]);

                float newD = (dMin < 1e8f) ? dMin + 1.0f : 1.0f;
                dist[nIdx] = newD;
                band.Enqueue(nIdx, newD);
            }
        }

        private static byte ClampByte(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }
    }

#if !NET6_0_OR_GREATER
    internal sealed class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private struct HeapNode
        {
            public TElement Element;
            public TPriority Priority;
        }

        private readonly List<HeapNode> _heap = new List<HeapNode>();

        public int Count => _heap.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add(new HeapNode { Element = element, Priority = priority });
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heap[i].Priority.CompareTo(_heap[p].Priority) >= 0) break;
                HeapNode tmp = _heap[i];
                _heap[i] = _heap[p];
                _heap[p] = tmp;
                i = p;
            }
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (_heap.Count == 0)
            {
                element = default!;
                priority = default!;
                return false;
            }

            HeapNode root = _heap[0];
            element = root.Element;
            priority = root.Priority;

            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            if (_heap.Count > 0)
            {
                int i = 0;
                while (true)
                {
                    int left = (i << 1) + 1;
                    if (left >= _heap.Count) break;
                    int right = left + 1;
                    int best = (right < _heap.Count && _heap[right].Priority.CompareTo(_heap[left].Priority) < 0) ? right : left;
                    if (_heap[best].Priority.CompareTo(_heap[i].Priority) >= 0) break;
                    HeapNode tmp = _heap[i];
                    _heap[i] = _heap[best];
                    _heap[best] = tmp;
                    i = best;
                }
            }

            return true;
        }

        public TElement Dequeue()
        {
            if (!TryDequeue(out var element, out _))
                throw new InvalidOperationException("The priority queue is empty.");
            return element;
        }
    }
#endif
}
