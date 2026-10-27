using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Optical
{
    public sealed class DustSpot
    {
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int Radius { get; set; } = 8;
        public float Intensity { get; set; }
    }

    public sealed class DustRemovalOptions
    {
        public float Sensitivity { get; set; } = 0.5f;
        public int MinRadius { get; set; } = 3;
        public int MaxRadius { get; set; } = 25;
        public float ConsistencyThreshold { get; set; } = 0.75f; // Invariant across >= 75% of frames
    }

    public interface ISensorDustDetector
    {
        unsafe IReadOnlyList<DustSpot> DetectDustSpots(IReadOnlyList<IntPtr> grayPointers, int width, int height, DustRemovalOptions? options = null);
        IReadOnlyList<DustSpot> DetectDustSpots(IReadOnlyList<ImageBuffer> frames, DustRemovalOptions? options = null);
        unsafe void InpaintDustSpots(float* ptr, int width, int height, int channels, IReadOnlyList<DustSpot> spots);
        void InpaintDustSpots(ImageBuffer buffer, IReadOnlyList<DustSpot> spots);
    }

    /// <summary>
    /// Multi-frame invariant sensor dust and dead-pixel detector and radial inpainting engine.
    /// Accumulates persistent dark drops across sequence acquisitions with zero unmanaged memory leaks.
    /// </summary>
    public sealed class SensorDustDetector : ISensorDustDetector
    {
        public unsafe IReadOnlyList<DustSpot> DetectDustSpots(IReadOnlyList<IntPtr> grayPointers, int width, int height, DustRemovalOptions? options = null)
        {
            if (grayPointers == null || grayPointers.Count < 2) return Array.Empty<DustSpot>();

            options ??= new DustRemovalOptions();
            int frameCount = grayPointers.Count;

            int step = 6;
            int gridW = width / step;
            int gridH = height / step;
            if (gridW <= 4 || gridH <= 4) return Array.Empty<DustSpot>();

            int[] hitCounts = new int[gridW * gridH];
            float darkThreshold = 0.04f + (1.0f - MathCompat.Clamp(options.Sensitivity, 0.1f, 1.0f)) * 0.06f;

            for (int f = 0; f < frameCount; f++)
            {
                float* pGray = (float*)grayPointers[f];
                if (pGray == null) continue;

                Parallel.For(2, gridH - 2, gy =>
                {
                    int py = gy * step;
                    int rowOffset = py * width;

                    for (int gx = 2; gx < gridW - 2; gx++)
                    {
                        int px = gx * step;
                        float centerVal = pGray[rowOffset + px];

                        float ringSum =
                            pGray[(py - 8) * width + px] +
                            pGray[(py + 8) * width + px] +
                            pGray[py * width + (px - 8)] +
                            pGray[py * width + (px + 8)] +
                            pGray[(py - 6) * width + (px - 6)] +
                            pGray[(py - 6) * width + (px + 6)] +
                            pGray[(py + 6) * width + (px - 6)] +
                            pGray[(py + 6) * width + (px + 6)];
                        float ringAvg = ringSum * 0.125f;

                        if (ringAvg - centerVal > darkThreshold && ringAvg > 0.05f)
                        {
                            hitCounts[gy * gridW + gx]++;
                        }
                    }
                });
            }

            int minHits = Math.Max(2, (int)(frameCount * options.ConsistencyThreshold));
            var detectedSpots = new List<DustSpot>();

            for (int gy = 2; gy < gridH - 2; gy++)
            {
                for (int gx = 2; gx < gridW - 2; gx++)
                {
                    int count = hitCounts[gy * gridW + gx];
                    if (count >= minHits)
                    {
                        int realX = gx * step;
                        int realY = gy * step;

                        bool isDuplicate = false;
                        foreach (var s in detectedSpots)
                        {
                            int dx = s.CenterX - realX;
                            int dy = s.CenterY - realY;
                            if (dx * dx + dy * dy < 144) // within 12px
                            {
                                isDuplicate = true;
                                break;
                            }
                        }

                        if (!isDuplicate)
                        {
                            detectedSpots.Add(new DustSpot
                            {
                                CenterX = realX,
                                CenterY = realY,
                                Radius = 10,
                                Intensity = (float)count / frameCount
                            });
                        }
                    }
                }
            }

            return detectedSpots;
        }

        public unsafe IReadOnlyList<DustSpot> DetectDustSpots(IReadOnlyList<ImageBuffer> frames, DustRemovalOptions? options = null)
        {
            if (frames == null || frames.Count < 2) return Array.Empty<DustSpot>();
            int w = frames[0].Width;
            int h = frames[0].Height;

            var floatFrames = new List<float[]>(frames.Count);
            var ptrs = new List<IntPtr>(frames.Count);

            for (int i = 0; i < frames.Count; i++)
            {
                var buf = frames[i];
                float[] gray = new float[w * h];
                Parallel.For(0, h, y =>
                {
                    byte* sRow = buf.GetRowPointer(y);
                    int rIdx = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (buf.Format == ImageFormatMode.Bgra32)
                        {
                            int bx = x * 4;
                            gray[rIdx + x] = (0.299f * sRow[bx + 2] + 0.587f * sRow[bx + 1] + 0.114f * sRow[bx]) / 255.0f;
                        }
                        else
                        {
                            gray[rIdx + x] = sRow[x] / 255.0f;
                        }
                    }
                });
                floatFrames.Add(gray);
            }

            var handles = new System.Runtime.InteropServices.GCHandle[frames.Count];
            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(floatFrames[i], System.Runtime.InteropServices.GCHandleType.Pinned);
                    ptrs.Add(handles[i].AddrOfPinnedObject());
                }
                return DetectDustSpots(ptrs, w, h, options);
            }
            finally
            {
                for (int i = 0; i < handles.Length; i++)
                {
                    if (handles[i].IsAllocated) handles[i].Free();
                }
            }
        }

        public unsafe void InpaintDustSpots(float* ptr, int width, int height, int channels, IReadOnlyList<DustSpot> spots)
        {
            if (ptr == null || spots == null || spots.Count == 0 || width <= 0 || height <= 0) return;

            foreach (var spot in spots)
            {
                int cx = spot.CenterX;
                int cy = spot.CenterY;
                int r = spot.Radius;
                int rSq = r * r;
                int outerR = r + 3;

                int x0 = Math.Max(0, cx - outerR);
                int y0 = Math.Max(0, cy - outerR);
                int x1 = Math.Min(width, cx + outerR + 1);
                int y1 = Math.Min(height, cy + outerR + 1);

                for (int y = y0; y < y1; y++)
                {
                    int dy = y - cy;
                    int dySq = dy * dy;
                    int rowOffset = y * width * channels;

                    for (int x = x0; x < x1; x++)
                    {
                        int dx = x - cx;
                        int distSq = dx * dx + dySq;

                        if (distSq <= rSq)
                        {
                            float weightSum = 0f;
                            float rAccum = 0f, gAccum = 0f, bAccum = 0f;

                            for (int angleDeg = 0; angleDeg < 360; angleDeg += 45)
                            {
                                double rad = angleDeg * Math.PI / 180.0;
                                int sx = MathCompat.Clamp(cx + (int)(outerR * Math.Cos(rad)), 0, width - 1);
                                int sy = MathCompat.Clamp(cy + (int)(outerR * Math.Sin(rad)), 0, height - 1);

                                int sIdx = (sy * width + sx) * channels;
                                float sDist = (float)Math.Sqrt((sx - x) * (sx - x) + (sy - y) * (sy - y)) + 0.1f;
                                float w = 1.0f / (sDist * sDist);

                                rAccum += ptr[sIdx] * w;
                                if (channels > 1) gAccum += ptr[sIdx + 1] * w;
                                if (channels > 2) bAccum += ptr[sIdx + 2] * w;
                                weightSum += w;
                            }

                            if (weightSum > 0f)
                            {
                                float invW = 1.0f / weightSum;
                                int targetIdx = rowOffset + x * channels;
                                ptr[targetIdx] = rAccum * invW;
                                if (channels > 1) ptr[targetIdx + 1] = gAccum * invW;
                                if (channels > 2) ptr[targetIdx + 2] = bAccum * invW;
                            }
                        }
                    }
                }
            }
        }

        public unsafe void InpaintDustSpots(ImageBuffer buffer, IReadOnlyList<DustSpot> spots)
        {
            if (buffer == null || spots == null || spots.Count == 0) return;
            if (buffer.Format != ImageFormatMode.Bgra32) return;

            int w = buffer.Width;
            int h = buffer.Height;

            float[] pixels = new float[w * h * 4];
            Parallel.For(0, h, y =>
            {
                byte* sRow = buffer.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    pixels[fx] = sRow[bx + 2] / 255.0f;
                    pixels[fx + 1] = sRow[bx + 1] / 255.0f;
                    pixels[fx + 2] = sRow[bx] / 255.0f;
                    pixels[fx + 3] = sRow[bx + 3] / 255.0f;
                }
            });

            fixed (float* pPixels = pixels)
            {
                InpaintDustSpots(pPixels, w, h, 4, spots);
            }

            Parallel.For(0, h, y =>
            {
                byte* dRow = buffer.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = (byte)MathCompat.Clamp((int)(pixels[fx] * 255.0f + 0.5f), 0, 255);
                    dRow[bx + 1] = (byte)MathCompat.Clamp((int)(pixels[fx + 1] * 255.0f + 0.5f), 0, 255);
                    dRow[bx] = (byte)MathCompat.Clamp((int)(pixels[fx + 2] * 255.0f + 0.5f), 0, 255);
                    dRow[bx + 3] = (byte)MathCompat.Clamp((int)(pixels[fx + 3] * 255.0f + 0.5f), 0, 255);
                }
            });
        }
    }
}
