using System;
using System.Buffers;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// High-performance 2D spatial convolution filters:
    /// - Separable Gaussian Blur (2-Pass O(R) reduction)
    /// - Sobel Gradient Edge Detection (Gx, Gy, Magnitude)
    /// - Laplacian Edge Sharpening
    /// </summary>
    public static unsafe class ConvolutionFilters
    {
        /// <summary>
        /// Applies Separable Gaussian Blur to a Gray8 image buffer.
        /// Decomposes the 2D kernel into independent horizontal and vertical 1D convolutions,
        /// reducing computational complexity from O(K^2) to O(2K).
        /// </summary>
        /// <param name="src">Source Gray8 buffer.</param>
        /// <param name="dst">Destination Gray8 buffer.</param>
        /// <param name="sigma">Gaussian standard deviation (controls blur radius).</param>
        public static void GaussianBlur(ImageBuffer src, ImageBuffer dst, float sigma = 1.4f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Gaussian blur requires Gray8 buffers.");

            int width = src.Width;
            int height = src.Height;

            // 1. Generate 1D Gaussian Kernel
            if (sigma < 0.5f) sigma = 0.5f;
            int radius = (int)global::System.Math.Ceiling(3.0f * sigma);
            if (radius < 1) radius = 1;
            int kernelSize = 2 * radius + 1;

            float[] kernel = new float[kernelSize];
            float twoSigmaSq = 2.0f * sigma * sigma;
            float sum = 0.0f;

            for (int i = -radius; i <= radius; i++)
            {
                float weight = (float)global::System.Math.Exp(-(i * i) / twoSigmaSq);
                kernel[i + radius] = weight;
                sum += weight;
            }

            // Normalize
            float invSum = 1.0f / sum;
            for (int i = 0; i < kernelSize; i++)
            {
                kernel[i] *= invSum;
            }

            // 2. Intermediate buffer for pass 1 (Pooled to eliminate 33MB+ LOH allocation on 4K)
            float[] temp = ArrayPool<float>.Shared.Rent(width * height);
            try
            {
                fixed (float* pKernel = kernel, pTemp = temp)
                {
                    // Pass 1: Horizontal Convolution (src -> temp)
                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        float* tempRow = pTemp + y * width;

                        for (int x = 0; x < width; x++)
                        {
                            float acc = 0.0f;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int px = x + k;
                                // Clamp to edge
                                if (px < 0) px = 0;
                                else if (px >= width) px = width - 1;

                                acc += srcRow[px] * pKernel[k + radius];
                            }
                            tempRow[x] = acc;
                        }
                    }

                    // Pass 2: Vertical Convolution (temp -> dst)
                    for (int y = 0; y < height; y++)
                    {
                        byte* dstRow = dst.GetRowPointer(y);

                        for (int x = 0; x < width; x++)
                        {
                            float acc = 0.0f;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int py = y + k;
                                // Clamp to edge
                                if (py < 0) py = 0;
                                else if (py >= height) py = height - 1;

                                acc += pTemp[py * width + x] * pKernel[k + radius];
                            }

                            int val = (int)(acc + 0.5f);
                            if (val < 0) val = 0;
                            else if (val > 255) val = 255;
                            dstRow[x] = (byte)val;
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(temp);
            }
        }

        /// <summary>
        /// Computes the Sobel gradient magnitude for edge detection on a Gray8 image buffer.
        /// Unrolls horizontal Gx and vertical Gy convolution kernels in a single pass.
        /// </summary>
        public static void SobelEdgeDetection(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Sobel filter requires Gray8 buffers.");

            int width = src.Width;
            int height = src.Height;

            // Clear borders
            dst.Clear(0);

            for (int y = 1; y < height - 1; y++)
            {
                byte* rowPrev = src.GetRowPointer(y - 1);
                byte* rowCurr = src.GetRowPointer(y);
                byte* rowNext = src.GetRowPointer(y + 1);
                byte* dstRow = dst.GetRowPointer(y);

                for (int x = 1; x < width - 1; x++)
                {
                    // 3x3 Neighborhood:
                    // p00 p01 p02
                    // p10 p11 p12
                    // p20 p21 p22
                    int p00 = rowPrev[x - 1];
                    int p01 = rowPrev[x];
                    int p02 = rowPrev[x + 1];

                    int p10 = rowCurr[x - 1];
                    int p12 = rowCurr[x + 1];

                    int p20 = rowNext[x - 1];
                    int p21 = rowNext[x];
                    int p22 = rowNext[x + 1];

                    // Gx = (p02 + 2*p12 + p22) - (p00 + 2*p10 + p20)
                    int gx = (p02 + (p12 << 1) + p22) - (p00 + (p10 << 1) + p20);

                    // Gy = (p20 + 2*p21 + p22) - (p00 + 2*p01 + p02)
                    int gy = (p20 + (p21 << 1) + p22) - (p00 + (p01 << 1) + p02);

                    // Manhattan distance approximation: |Gx| + |Gy|
                    int mag = global::System.Math.Abs(gx) + global::System.Math.Abs(gy);
                    if (mag > 255) mag = 255;

                    dstRow[x] = (byte)mag;
                }
            }
        }

        /// <summary>
        /// Applies a 3x3 Laplacian sharpening filter to enhance micro-textures and fine edges.
        /// </summary>
        public static void Sharpen(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            int width = src.Width;
            int height = src.Height;

            // Copy edges
            for (int y = 0; y < height; y++)
            {
                byte* sRow = src.GetRowPointer(y);
                byte* dRow = dst.GetRowPointer(y);
                dRow[0] = sRow[0];
                dRow[width - 1] = sRow[width - 1];
            }

            for (int y = 1; y < height - 1; y++)
            {
                byte* rowPrev = src.GetRowPointer(y - 1);
                byte* rowCurr = src.GetRowPointer(y);
                byte* rowNext = src.GetRowPointer(y + 1);
                byte* dstRow = dst.GetRowPointer(y);

                for (int x = 1; x < width - 1; x++)
                {
                    // Laplacian Sharpening Kernel:
                    //  0 -1  0
                    // -1  5 -1
                    //  0 -1  0
                    int val = (5 * rowCurr[x]) - rowPrev[x] - rowNext[x] - rowCurr[x - 1] - rowCurr[x + 1];
                    if (val < 0) val = 0;
                    else if (val > 255) val = 255;
                    dstRow[x] = (byte)val;
                }
            }
        }
    }
}
