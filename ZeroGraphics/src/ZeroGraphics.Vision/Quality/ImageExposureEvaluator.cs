using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Quality
{
    /// <summary>
    /// Configuration options for exposure and clipping evaluation.
    /// </summary>
    public sealed class ExposureOptions
    {
        /// <summary>
        /// Threshold for crushed shadows [0..255]. Default 5.
        /// </summary>
        public byte ShadowClipThreshold { get; set; } = 5;

        /// <summary>
        /// Threshold for blown highlights [0..255]. Default 250.
        /// </summary>
        public byte HighlightClipThreshold { get; set; } = 250;

        /// <summary>
        /// Maximum acceptable percentage of blown highlight pixels before flagging. Default 0.015 (1.5%).
        /// </summary>
        public double MaxAllowableHighlightClipRatio { get; set; } = 0.015;

        /// <summary>
        /// Maximum acceptable percentage of crushed shadow pixels before flagging. Default 0.020 (2.0%).
        /// </summary>
        public double MaxAllowableShadowClipRatio { get; set; } = 0.020;

        /// <summary>
        /// Mean brightness below which image is severely underexposed. Default 35.0.
        /// </summary>
        public double SevereUnderexposureMean { get; set; } = 35.0;

        /// <summary>
        /// Mean brightness above which image is severely overexposed. Default 225.0.
        /// </summary>
        public double SevereOverexposureMean { get; set; } = 225.0;
    }

    /// <summary>
    /// Analysis result of image exposure, dynamic range, and highlight/shadow clipping.
    /// </summary>
    public sealed class ExposureResult
    {
        /// <summary>
        /// Average luminance of the image [0.0..255.0].
        /// </summary>
        public double MeanBrightness { get; set; }

        /// <summary>
        /// Ratio of pixels at or above HighlightClipThreshold [0.0..1.0].
        /// </summary>
        public double HighlightClipRatio { get; set; }

        /// <summary>
        /// Ratio of pixels at or below ShadowClipThreshold [0.0..1.0].
        /// </summary>
        public double ShadowClipRatio { get; set; }

        /// <summary>
        /// Difference between 99th percentile and 1st percentile luminance.
        /// </summary>
        public int DynamicRange { get; set; }

        /// <summary>
        /// Overall exposure score normalized to [0.0..100.0].
        /// </summary>
        public double ExposureScore { get; set; }

        /// <summary>
        /// True if highlight clipping exceeds the configured limit (blown highlights).
        /// </summary>
        public bool IsBlownHighlights { get; set; }

        /// <summary>
        /// True if shadow clipping exceeds the configured limit (crushed blacks).
        /// </summary>
        public bool IsCrushedShadows { get; set; }

        /// <summary>
        /// True if mean brightness is above the severe overexposure limit.
        /// </summary>
        public bool IsSevereOverexposure { get; set; }

        /// <summary>
        /// True if mean brightness is below the severe underexposure limit.
        /// </summary>
        public bool IsSevereUnderexposure { get; set; }

        /// <summary>
        /// 256-bin luminance histogram.
        /// </summary>
        public int[] Histogram { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// Evaluates exposure levels, dynamic range, and clipping artifacts from image buffers.
    /// Uses fast unsafe single-pass histogram aggregation with zero full-frame allocations.
    /// </summary>
    public static unsafe class ImageExposureEvaluator
    {
        /// <summary>
        /// Evaluates luminance histogram, clipping percentages, and exposure balance.
        /// </summary>
        /// <param name="image">Gray8 or Bgra32 image buffer.</param>
        /// <param name="options">Optional tuning parameters.</param>
        /// <returns>ExposureResult containing detailed metrics.</returns>
        public static ExposureResult Evaluate(ImageBuffer image, ExposureOptions? options = null)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.Width <= 0 || image.Height <= 0)
                throw new ArgumentException("Image has invalid dimensions.");

            options ??= new ExposureOptions();

            int w = image.Width;
            int h = image.Height;
            long totalPixels = (long)w * h;

            int[] histogram = new int[256];

            fixed (int* pHist = histogram)
            {
                if (image.Format == ImageFormatMode.Gray8)
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = image.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            pHist[row[x]]++;
                        }
                    }
                }
                else if (image.Format == ImageFormatMode.Bgra32)
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = image.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            int px = x * 4;
                            byte b = row[px];
                            byte g = row[px + 1];
                            byte r = row[px + 2];
                            int gray = (54 * r + 183 * g + 19 * b) >> 8;
                            pHist[gray]++;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"ImageFormatMode {image.Format} is not supported for exposure evaluation.");
                }
            }

            // Derive statistics from histogram
            long sumLuminance = 0;
            long shadowClipCount = 0;
            long highlightClipCount = 0;

            for (int i = 0; i <= options.ShadowClipThreshold; i++)
            {
                shadowClipCount += histogram[i];
            }

            for (int i = options.HighlightClipThreshold; i < 256; i++)
            {
                highlightClipCount += histogram[i];
            }

            for (int i = 0; i < 256; i++)
            {
                sumLuminance += (long)i * histogram[i];
            }

            double meanBrightness = totalPixels > 0 ? (double)sumLuminance / totalPixels : 0.0;
            double shadowClipRatio = totalPixels > 0 ? (double)shadowClipCount / totalPixels : 0.0;
            double highlightClipRatio = totalPixels > 0 ? (double)highlightClipCount / totalPixels : 0.0;

            // Compute 1st and 99th percentiles for dynamic range
            long p1Target = (long)(totalPixels * 0.01);
            long p99Target = (long)(totalPixels * 0.99);

            int p1 = 0;
            int p99 = 255;
            long cumulative = 0;

            for (int i = 0; i < 256; i++)
            {
                cumulative += histogram[i];
                if (cumulative >= p1Target && p1 == 0)
                {
                    p1 = i;
                }
                if (cumulative >= p99Target)
                {
                    p99 = i;
                    break;
                }
            }

            int dynamicRange = Math.Max(0, p99 - p1);

            // Compute composite Exposure Score [0..100]
            double score = 100.0;

            // Penalize mean deviation from ideal range [105..145]
            if (meanBrightness < 105.0)
            {
                score -= (105.0 - meanBrightness) * 0.45;
            }
            else if (meanBrightness > 145.0)
            {
                score -= (meanBrightness - 145.0) * 0.45;
            }

            // Penalize highlight clipping (blown highlights are visually destructive)
            if (options.MaxAllowableHighlightClipRatio > 0)
            {
                double hlRatio = highlightClipRatio / options.MaxAllowableHighlightClipRatio;
                if (hlRatio > 1.0)
                {
                    score -= Math.Min(40.0, (hlRatio - 1.0) * 25.0);
                }
            }

            // Penalize shadow clipping
            if (options.MaxAllowableShadowClipRatio > 0)
            {
                double shRatio = shadowClipRatio / options.MaxAllowableShadowClipRatio;
                if (shRatio > 1.0)
                {
                    score -= Math.Min(25.0, (shRatio - 1.0) * 15.0);
                }
            }

            // Penalize very narrow dynamic range (< 80)
            if (dynamicRange < 80)
            {
                score -= (80 - dynamicRange) * 0.25;
            }

            score = Math.Max(0.0, Math.Min(100.0, score));

            return new ExposureResult
            {
                MeanBrightness = Math.Round(meanBrightness, 2),
                HighlightClipRatio = Math.Round(highlightClipRatio, 4),
                ShadowClipRatio = Math.Round(shadowClipRatio, 4),
                DynamicRange = dynamicRange,
                ExposureScore = Math.Round(score, 2),
                IsBlownHighlights = highlightClipRatio > options.MaxAllowableHighlightClipRatio,
                IsCrushedShadows = shadowClipRatio > options.MaxAllowableShadowClipRatio,
                IsSevereOverexposure = meanBrightness > options.SevereOverexposureMean,
                IsSevereUnderexposure = meanBrightness < options.SevereUnderexposureMean,
                Histogram = histogram
            };
        }
    }
}
