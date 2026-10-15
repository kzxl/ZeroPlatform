using System;
using System.Buffers;
using ZeroGraphics.Core.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Vision.Edge
{
    /// <summary>
    /// High-performance Canny Edge Detector with Gaussian smoothing, Sobel gradients,
    /// 4-direction non-maximum suppression, and dual-threshold hysteresis tracking.
    /// Supports zero-copy native SIMD dispatch via ZeroGraphicsNativeBridge and 0-LOH pure C# fallback.
    /// </summary>
    public static class CannyEdgeDetector
    {
        private const byte NonEdge = 0;
        private const byte WeakEdge = 128;
        private const byte StrongEdge = 255;

        /// <summary>
        /// Detects edges in an image using the Canny edge detection algorithm.
        /// </summary>
        /// <param name="image">Source image (Gray8 or Bgra32).</param>
        /// <param name="lowThreshold">Hysteresis lower threshold.</param>
        /// <param name="highThreshold">Hysteresis upper threshold.</param>
        /// <param name="applyGaussian">Whether to apply a 5x5 Gaussian noise smoothing filter.</param>
        /// <returns>A binary Gray8 ImageBuffer where edge pixels are 255 and background is 0.</returns>
        public static unsafe ImageBuffer Detect(
            ImageBuffer image,
            double lowThreshold = 20.0,
            double highThreshold = 50.0,
            bool applyGaussian = true)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (lowThreshold < 0) throw new ArgumentOutOfRangeException(nameof(lowThreshold));
            if (highThreshold < lowThreshold) throw new ArgumentException("highThreshold must be >= lowThreshold.");

            int w = image.Width;
            int h = image.Height;

            // 1. Ensure Grayscale image
            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;
            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(w, h);
                ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            byte[]? smoothed = null;
            float[]? magnitude = null;
            byte[]? orientation = null;
            byte[]? nms = null;
            int[]? queueBuf = null;

            try
            {
                var result = ImageBuffer.CreateGray8(w, h);

                // Fast-path: Zero-copy native SIMD dispatch on unmanaged ImageBuffer pointers
                if (ZeroGraphicsNativeBridge.ShouldDispatchImage(w, h) &&
                    ZeroGraphicsNativeBridge.TryCannyEdge(
                        grayImage.Scan0, grayImage.Stride,
                        result.Scan0, result.Stride,
                        w, h,
                        (float)lowThreshold, (float)highThreshold,
                        applyGaussian))
                {
                    return result;
                }

                int totalPixels = w * h;
                if (totalPixels <= 0)
                {
                    return result;
                }

                // 2. Gaussian smoothing (5x5 binomial separable [1, 4, 6, 4, 1] / 16)
                smoothed = ArrayPool<byte>.Shared.Rent(totalPixels);
                if (applyGaussian && w >= 5 && h >= 5)
                {
                    ApplyGaussian5x5(grayImage, smoothed, w, h);
                }
                else
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* pRow = grayImage.GetRowPointer(y);
                        int rowOffset = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            smoothed[rowOffset + x] = pRow[x];
                        }
                    }
                }

                // 3. Sobel Gradients (Gx, Gy) & Magnitude & Trigonometry-Free Sector Orientation
                magnitude = ArrayPool<float>.Shared.Rent(totalPixels);
                orientation = ArrayPool<byte>.Shared.Rent(totalPixels); // 0: 0 deg, 1: 45 deg, 2: 90 deg, 3: 135 deg
                Array.Clear(magnitude, 0, totalPixels);

                fixed (byte* pSmooth = smoothed)
                fixed (float* pMag = magnitude)
                fixed (byte* pOri = orientation)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int yPrev = (y - 1) * w;
                        int yCur = y * w;
                        int yNext = (y + 1) * w;

                        for (int x = 1; x < w - 1; x++)
                        {
                            // Sobel X kernel:
                            // -1  0  1
                            // -2  0  2
                            // -1  0  1
                            int gx = (pSmooth[yPrev + (x + 1)] - pSmooth[yPrev + (x - 1)]) +
                                     2 * (pSmooth[yCur + (x + 1)] - pSmooth[yCur + (x - 1)]) +
                                     (pSmooth[yNext + (x + 1)] - pSmooth[yNext + (x - 1)]);

                            // Sobel Y kernel:
                            // -1 -2 -1
                            //  0  0  0
                            //  1  2  1
                            int gy = (pSmooth[yNext + (x - 1)] - pSmooth[yPrev + (x - 1)]) +
                                     2 * (pSmooth[yNext + x] - pSmooth[yPrev + x]) +
                                     (pSmooth[yNext + (x + 1)] - pSmooth[yPrev + (x + 1)]);

                            float mag = (float)Math.Sqrt(gx * gx + gy * gy);
                            int idx = yCur + x;
                            pMag[idx] = mag;

                            // Exact fixed-point integer tangent comparison (tan(22.5°) ≈ 0.4142, tan(67.5°) ≈ 2.4142)
                            long ax = Math.Abs(gx);
                            long ay = Math.Abs(gy);
                            byte ori;
                            if (ay * 10000L < ax * 4142L)
                                ori = 0;
                            else if (ay * 10000L >= ax * 24142L)
                                ori = 2;
                            else if ((gx ^ gy) >= 0)
                                ori = 1;
                            else
                                ori = 3;

                            pOri[idx] = ori;
                        }
                    }
                }

                // 4. Non-Maximum Suppression (NMS)
                nms = ArrayPool<byte>.Shared.Rent(totalPixels);
                Array.Clear(nms, 0, totalPixels);
                float lowThreshF = (float)lowThreshold;
                float highThreshF = (float)highThreshold;

                fixed (float* pMag = magnitude)
                fixed (byte* pOri = orientation)
                fixed (byte* pNms = nms)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int yCur = y * w;
                        int yPrev = (y - 1) * w;
                        int yNext = (y + 1) * w;

                        for (int x = 1; x < w - 1; x++)
                        {
                            int idx = yCur + x;
                            float m = pMag[idx];

                            if (m < lowThreshF)
                                continue;

                            float m1 = 0, m2 = 0;
                            switch (pOri[idx])
                            {
                                case 0: // Horizontal -> compare (x-1, y) and (x+1, y)
                                    m1 = pMag[yCur + (x - 1)];
                                    m2 = pMag[yCur + (x + 1)];
                                    break;
                                case 1: // 45 deg -> compare (x+1, y-1) and (x-1, y+1)
                                    m1 = pMag[yPrev + (x + 1)];
                                    m2 = pMag[yNext + (x - 1)];
                                    break;
                                case 2: // 90 deg -> compare (x, y-1) and (x, y+1)
                                    m1 = pMag[yPrev + x];
                                    m2 = pMag[yNext + x];
                                    break;
                                case 3: // 135 deg -> compare (x-1, y-1) and (x+1, y+1)
                                    m1 = pMag[yPrev + (x - 1)];
                                    m2 = pMag[yNext + (x + 1)];
                                    break;
                            }

                            if (m >= m1 && m >= m2)
                            {
                                pNms[idx] = m >= highThreshF ? StrongEdge : WeakEdge;
                            }
                        }
                    }
                }

                // 5. Hysteresis Edge Tracking (Edge Linking) using pooled BFS queue
                queueBuf = ArrayPool<int>.Shared.Rent(totalPixels);
                int qHead = 0;
                int qTail = 0;

                fixed (byte* pNms = nms)
                fixed (int* pQueue = queueBuf)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int rowOffset = y * w;
                        byte* pDstRow = result.GetRowPointer(y);
                        for (int x = 1; x < w - 1; x++)
                        {
                            int idx = rowOffset + x;
                            if (pNms[idx] == StrongEdge)
                            {
                                pQueue[qTail++] = idx;
                                pDstRow[x] = StrongEdge;
                            }
                        }
                    }

                    // 8-way connectivity expansion for weak edges
                    int* neighborOffsets = stackalloc int[8]
                    {
                        -w - 1, -w, -w + 1,
                        -1,          1,
                         w - 1,  w,  w + 1
                    };

                    while (qHead < qTail)
                    {
                        int currIdx = pQueue[qHead++];
                        int cx = currIdx % w;
                        int cy = currIdx / w;

                        if (cx <= 0 || cx >= w - 1 || cy <= 0 || cy >= h - 1)
                            continue;

                        for (int k = 0; k < 8; k++)
                        {
                            int nIdx = currIdx + neighborOffsets[k];
                            if (pNms[nIdx] == WeakEdge)
                            {
                                pNms[nIdx] = StrongEdge; // Mark visited
                                int nx = nIdx % w;
                                int ny = nIdx / w;
                                *(result.GetRowPointer(ny) + nx) = StrongEdge;
                                pQueue[qTail++] = nIdx;
                            }
                        }
                    }
                }

                return result;
            }
            finally
            {
                if (smoothed != null) ArrayPool<byte>.Shared.Return(smoothed);
                if (magnitude != null) ArrayPool<float>.Shared.Return(magnitude);
                if (orientation != null) ArrayPool<byte>.Shared.Return(orientation);
                if (nms != null) ArrayPool<byte>.Shared.Return(nms);
                if (queueBuf != null) ArrayPool<int>.Shared.Return(queueBuf);

                grayOwned?.Dispose();
            }
        }

        private static unsafe void ApplyGaussian5x5(ImageBuffer src, byte[] dst, int w, int h)
        {
            // Separable 1D Gaussian kernel: [1, 4, 6, 4, 1] / 16
            int[] temp = ArrayPool<int>.Shared.Rent(w * h);
            try
            {
                fixed (int* pTemp = temp)
                fixed (byte* pDst = dst)
                {
                    // Horizontal pass
                    for (int y = 0; y < h; y++)
                    {
                        byte* pSrcRow = src.GetRowPointer(y);
                        int rowOffset = y * w;

                        for (int x = 2; x < w - 2; x++)
                        {
                            pTemp[rowOffset + x] =
                                pSrcRow[x - 2] * 1 +
                                pSrcRow[x - 1] * 4 +
                                pSrcRow[x]     * 6 +
                                pSrcRow[x + 1] * 4 +
                                pSrcRow[x + 2] * 1;
                        }

                        // Handle borders
                        pTemp[rowOffset + 0] = pSrcRow[0] * 16;
                        pTemp[rowOffset + 1] = pSrcRow[1] * 16;
                        pTemp[rowOffset + (w - 2)] = pSrcRow[w - 2] * 16;
                        pTemp[rowOffset + (w - 1)] = pSrcRow[w - 1] * 16;
                    }

                    // Vertical pass
                    for (int y = 2; y < h - 2; y++)
                    {
                        int y0 = (y - 2) * w;
                        int y1 = (y - 1) * w;
                        int y2 = y * w;
                        int y3 = (y + 1) * w;
                        int y4 = (y + 2) * w;

                        for (int x = 0; x < w; x++)
                        {
                            int sum =
                                pTemp[y0 + x] * 1 +
                                pTemp[y1 + x] * 4 +
                                pTemp[y2 + x] * 6 +
                                pTemp[y3 + x] * 4 +
                                pTemp[y4 + x] * 1;

                            pDst[y2 + x] = (byte)(sum >> 8); // divide by 256
                        }
                    }

                    // Copy border rows directly
                    byte* r0 = src.GetRowPointer(0);
                    byte* r1 = src.GetRowPointer(1);
                    byte* rH2 = src.GetRowPointer(h - 2);
                    byte* rH1 = src.GetRowPointer(h - 1);
                    int offH2 = (h - 2) * w;
                    int offH1 = (h - 1) * w;

                    for (int x = 0; x < w; x++)
                    {
                        pDst[x] = r0[x];
                        pDst[w + x] = r1[x];
                        pDst[offH2 + x] = rH2[x];
                        pDst[offH1 + x] = rH1[x];
                    }
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(temp);
            }
        }
    }
}
