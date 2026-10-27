using System;
using System.Globalization;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// High-performance 64-bit Difference Hash (dHash) generator and Hamming distance calculator.
    /// Used for image deduplication, visual similarity matching, and template pre-filtering.
    /// </summary>
    public static unsafe class DifferenceHash
    {
        private const int TargetCols = 9;
        private const int TargetRows = 8;

        /// <summary>
        /// Computes a 64-bit dHash perceptual fingerprint from an <see cref="ImageBuffer"/>.
        /// Automatically handles Gray8 and Bgra32 formats with zero full-frame heap allocations.
        /// </summary>
        /// <param name="image">Source image buffer.</param>
        /// <returns>64-bit unsigned integer representing the dHash.</returns>
        public static ulong Compute(ImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.Width < TargetCols || image.Height < TargetRows)
                throw new ArgumentException($"Image dimensions must be at least {TargetCols}x{TargetRows}.");

            int width = image.Width;
            int height = image.Height;

            // 9 columns x 8 rows matrix of average intensities
            fixed (double* pMatrix = new double[TargetCols * TargetRows])
            {
                fixed (long* pCounts = new long[TargetCols * TargetRows])
                {
                    // Compute block bounds
                    int* xBounds = stackalloc int[TargetCols + 1];
                    for (int c = 0; c <= TargetCols; c++)
                    {
                        xBounds[c] = (c * width) / TargetCols;
                    }

                    int* yBounds = stackalloc int[TargetRows + 1];
                    for (int r = 0; r <= TargetRows; r++)
                    {
                        yBounds[r] = (r * height) / TargetRows;
                    }

                    // Map pixels into 9x8 cells using step-sampling for maximum throughput
                    int stepX = Math.Max(1, width / 90);
                    int stepY = Math.Max(1, height / 80);

                    if (image.Format == ImageFormatMode.Gray8)
                    {
                        for (int r = 0; r < TargetRows; r++)
                        {
                            int y0 = yBounds[r];
                            int y1 = yBounds[r + 1];

                            for (int y = y0; y < y1; y += stepY)
                            {
                                byte* row = image.GetRowPointer(y);

                                for (int c = 0; c < TargetCols; c++)
                                {
                                    int x0 = xBounds[c];
                                    int x1 = xBounds[c + 1];
                                    int cellIdx = r * TargetCols + c;

                                    for (int x = x0; x < x1; x += stepX)
                                    {
                                        pMatrix[cellIdx] += row[x];
                                        pCounts[cellIdx]++;
                                    }
                                }
                            }
                        }
                    }
                    else if (image.Format == ImageFormatMode.Bgra32)
                    {
                        for (int r = 0; r < TargetRows; r++)
                        {
                            int y0 = yBounds[r];
                            int y1 = yBounds[r + 1];

                            for (int y = y0; y < y1; y += stepY)
                            {
                                byte* row = image.GetRowPointer(y);

                                for (int c = 0; c < TargetCols; c++)
                                {
                                    int x0 = xBounds[c];
                                    int x1 = xBounds[c + 1];
                                    int cellIdx = r * TargetCols + c;

                                    for (int x = x0; x < x1; x += stepX)
                                    {
                                        int px = x * 4;
                                        byte b = row[px];
                                        byte g = row[px + 1];
                                        byte red = row[px + 2];
                                        // ITU-R BT.709 grayscale
                                        int gray = (54 * red + 183 * g + 19 * b) >> 8;

                                        pMatrix[cellIdx] += gray;
                                        pCounts[cellIdx]++;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"ImageFormatMode {image.Format} is not supported for DifferenceHash.");
                    }

                    // Normalize cell averages
                    for (int i = 0; i < TargetCols * TargetRows; i++)
                    {
                        if (pCounts[i] > 0)
                            pMatrix[i] /= pCounts[i];
                    }

                    // Build 64-bit hash: row by row, compare col[x] > col[x + 1]
                    ulong hash = 0UL;
                    int bitIndex = 0;

                    for (int r = 0; r < TargetRows; r++)
                    {
                        int rowOffset = r * TargetCols;
                        for (int c = 0; c < TargetCols - 1; c++)
                        {
                            if (pMatrix[rowOffset + c] > pMatrix[rowOffset + c + 1])
                            {
                                hash |= (1UL << bitIndex);
                            }
                            bitIndex++;
                        }
                    }

                    return hash;
                }
            }
        }

        /// <summary>
        /// Computes the Hamming distance between two 64-bit difference hashes.
        /// Distance &lt;= 6 typically represents burst sequences or near-duplicate shots.
        /// Distance 0 represents identical perceptual images.
        /// </summary>
        /// <param name="hash1">First 64-bit hash.</param>
        /// <param name="hash2">Second 64-bit hash.</param>
        /// <returns>Number of bit differences between 0 and 64.</returns>
        public static int HammingDistance(ulong hash1, ulong hash2)
        {
            ulong xor = hash1 ^ hash2;

#if NETCOREAPP || NET8_0_OR_GREATER
            return System.Numerics.BitOperations.PopCount(xor);
#else
            // Software popcount for net462
            xor = xor - ((xor >> 1) & 0x5555555555555555UL);
            xor = (xor & 0x3333333333333333UL) + ((xor >> 2) & 0x3333333333333333UL);
            return (int)((((xor + (xor >> 4)) & 0x0F0F0F0F0F0F0F0FUL) * 0x0101010101010101UL) >> 56);
#endif
        }

        /// <summary>
        /// Calculates perceptual similarity percentage between two hashes (0.0 to 1.0).
        /// </summary>
        public static double Similarity(ulong hash1, ulong hash2)
        {
            int dist = HammingDistance(hash1, hash2);
            return 1.0 - (dist / 64.0);
        }

        /// <summary>
        /// Formats 64-bit hash into a 16-character hexadecimal string.
        /// </summary>
        public static string ToHexString(ulong hash) => hash.ToString("x16", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses a 16-character hexadecimal string into a 64-bit hash.
        /// </summary>
        public static ulong FromHexString(string hex) => ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
