using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Quality
{
    /// <summary>
    /// Configuration options for sharpness evaluation.
    /// </summary>
    public sealed class SharpnessOptions
    {
        /// <summary>
        /// Threshold below which an image is considered severely blurry/out of focus. Default 45.0.
        /// </summary>
        public double SevereBlurThreshold { get; set; } = 45.0;

        /// <summary>
        /// Threshold above which an image is considered sharp. Default 120.0.
        /// </summary>
        public double SharpThreshold { get; set; } = 120.0;

        /// <summary>
        /// Horizontal grid subdivisions for localized focal sharpness. Default 6.
        /// </summary>
        public int GridTilesX { get; set; } = 6;

        /// <summary>
        /// Vertical grid subdivisions for localized focal sharpness. Default 6.
        /// </summary>
        public int GridTilesY { get; set; } = 6;
    }

    /// <summary>
    /// Sharpness assessment result containing global variance and localized focal sharpness metrics.
    /// </summary>
    public sealed class SharpnessResult
    {
        /// <summary>
        /// Variance of Laplacian across the entire image frame.
        /// </summary>
        public double GlobalSharpness { get; set; }

        /// <summary>
        /// Peak sharpness detected in localized focal patches (preserves shallow DOF / bokeh portraits).
        /// </summary>
        public double FocalSharpness { get; set; }

        /// <summary>
        /// Effective sharpness used for decision making: Max(GlobalSharpness, FocalSharpness).
        /// </summary>
        public double EffectiveSharpness { get; set; }

        /// <summary>
        /// True if effective sharpness is below SevereBlurThreshold.
        /// </summary>
        public bool IsSevereBlur { get; set; }

        /// <summary>
        /// True if effective sharpness is between SevereBlurThreshold and SharpThreshold.
        /// </summary>
        public bool IsAcceptable { get; set; }

        /// <summary>
        /// True if effective sharpness meets or exceeds SharpThreshold.
        /// </summary>
        public bool IsSharp { get; set; }
    }

    /// <summary>
    /// Evaluates image focus and sharpness using discrete Laplacian variance and localized patch analysis.
    /// Distinguishes between accidental camera shake/missed focus and intentional shallow depth-of-field bokeh.
    /// </summary>
    public static unsafe class ImageSharpnessEvaluator
    {
        /// <summary>
        /// Evaluates sharpness of the provided image buffer.
        /// </summary>
        /// <param name="image">Gray8 or Bgra32 image buffer.</param>
        /// <param name="options">Optional tuning parameters.</param>
        /// <returns>SharpnessResult with global and focal metrics.</returns>
        public static SharpnessResult Evaluate(ImageBuffer image, SharpnessOptions? options = null)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.Width < 3 || image.Height < 3)
                throw new ArgumentException("Image must be at least 3x3 pixels.");

            options ??= new SharpnessOptions();

            int w = image.Width;
            int h = image.Height;
            int tilesX = Math.Max(1, Math.Min(options.GridTilesX, w / 3));
            int tilesY = Math.Max(1, Math.Min(options.GridTilesY, h / 3));

            int numTiles = tilesX * tilesY;
            double[] tileSum = new double[numTiles];
            double[] tileSumSq = new double[numTiles];
            long[] tileCount = new long[numTiles];

            double totalSum = 0.0;
            double totalSumSq = 0.0;
            long totalCount = 0;

            // Compute tile dimension bounds
            int[] xBounds = new int[tilesX + 1];
            for (int i = 0; i <= tilesX; i++) xBounds[i] = (i * (w - 2)) / tilesX + 1;

            int[] yBounds = new int[tilesY + 1];
            for (int i = 0; i <= tilesY; i++) yBounds[i] = (i * (h - 2)) / tilesY + 1;

            // Evaluate 3x3 Laplacian: [0, 1, 0; 1, -4, 1; 0, 1, 0]
            if (image.Format == ImageFormatMode.Gray8)
            {
                for (int ty = 0; ty < tilesY; ty++)
                {
                    int startY = yBounds[ty];
                    int endY = yBounds[ty + 1];

                    for (int y = startY; y < endY; y++)
                    {
                        byte* rowPrev = image.GetRowPointer(y - 1);
                        byte* rowCurr = image.GetRowPointer(y);
                        byte* rowNext = image.GetRowPointer(y + 1);

                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            int startX = xBounds[tx];
                            int endX = xBounds[tx + 1];
                            int tileIdx = ty * tilesX + tx;

                            double s = 0.0;
                            double sSq = 0.0;
                            int cnt = 0;

                            for (int x = startX; x < endX; x++)
                            {
                                int lap = rowPrev[x] + rowNext[x] + rowCurr[x - 1] + rowCurr[x + 1] - 4 * rowCurr[x];
                                s += lap;
                                sSq += (double)lap * lap;
                                cnt++;
                            }

                            tileSum[tileIdx] += s;
                            tileSumSq[tileIdx] += sSq;
                            tileCount[tileIdx] += cnt;

                            totalSum += s;
                            totalSumSq += sSq;
                            totalCount += cnt;
                        }
                    }
                }
            }
            else if (image.Format == ImageFormatMode.Bgra32)
            {
                // In-line fast ITU-R BT.709 grayscale conversion
                for (int ty = 0; ty < tilesY; ty++)
                {
                    int startY = yBounds[ty];
                    int endY = yBounds[ty + 1];

                    for (int y = startY; y < endY; y++)
                    {
                        byte* rowPrev = image.GetRowPointer(y - 1);
                        byte* rowCurr = image.GetRowPointer(y);
                        byte* rowNext = image.GetRowPointer(y + 1);

                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            int startX = xBounds[tx];
                            int endX = xBounds[tx + 1];
                            int tileIdx = ty * tilesX + tx;

                            double s = 0.0;
                            double sSq = 0.0;
                            int cnt = 0;

                            for (int x = startX; x < endX; x++)
                            {
                                int px = x * 4;
                                int pLeft = (x - 1) * 4;
                                int pRight = (x + 1) * 4;

                                int top = (54 * rowPrev[px + 2] + 183 * rowPrev[px + 1] + 19 * rowPrev[px]) >> 8;
                                int bottom = (54 * rowNext[px + 2] + 183 * rowNext[px + 1] + 19 * rowNext[px]) >> 8;
                                int left = (54 * rowCurr[pLeft + 2] + 183 * rowCurr[pLeft + 1] + 19 * rowCurr[pLeft]) >> 8;
                                int right = (54 * rowCurr[pRight + 2] + 183 * rowCurr[pRight + 1] + 19 * rowCurr[pRight]) >> 8;
                                int center = (54 * rowCurr[px + 2] + 183 * rowCurr[px + 1] + 19 * rowCurr[px]) >> 8;

                                int lap = top + bottom + left + right - 4 * center;
                                s += lap;
                                sSq += (double)lap * lap;
                                cnt++;
                            }

                            tileSum[tileIdx] += s;
                            tileSumSq[tileIdx] += sSq;
                            tileCount[tileIdx] += cnt;

                            totalSum += s;
                            totalSumSq += sSq;
                            totalCount += cnt;
                        }
                    }
                }
            }
            else
            {
                throw new NotSupportedException($"ImageFormatMode {image.Format} is not supported for sharpness evaluation.");
            }

            // Global variance: Var = E[X^2] - (E[X])^2
            double globalVariance = 0.0;
            if (totalCount > 0)
            {
                double mean = totalSum / totalCount;
                globalVariance = (totalSumSq / totalCount) - (mean * mean);
                if (globalVariance < 0.0) globalVariance = 0.0;
            }

            // Peak focal tile variance
            double peakTileVariance = 0.0;
            for (int i = 0; i < numTiles; i++)
            {
                if (tileCount[i] > 0)
                {
                    double m = tileSum[i] / tileCount[i];
                    double v = (tileSumSq[i] / tileCount[i]) - (m * m);
                    if (v > peakTileVariance)
                    {
                        peakTileVariance = v;
                    }
                }
            }

            double effectiveSharpness = Math.Max(globalVariance, peakTileVariance);

            return new SharpnessResult
            {
                GlobalSharpness = Math.Round(globalVariance, 2),
                FocalSharpness = Math.Round(peakTileVariance, 2),
                EffectiveSharpness = Math.Round(effectiveSharpness, 2),
                IsSevereBlur = effectiveSharpness < options.SevereBlurThreshold,
                IsAcceptable = effectiveSharpness >= options.SevereBlurThreshold && effectiveSharpness < options.SharpThreshold,
                IsSharp = effectiveSharpness >= options.SharpThreshold
            };
        }
    }
}
