using System;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Imaging;

/// <summary>
/// Result of projection profile skew estimation.
/// </summary>
public readonly struct SkewDetectionResult
{
    public double AngleDegrees { get; }
    public double PeakVariance { get; }
    public TimeSpan Elapsed { get; }

    public SkewDetectionResult(double angleDegrees, double peakVariance, TimeSpan elapsed)
    {
        AngleDegrees = angleDegrees;
        PeakVariance = peakVariance;
        Elapsed = elapsed;
    }

    public override string ToString() => $"Angle: {AngleDegrees:F2}°, Variance: {PeakVariance:F0} ({Elapsed.TotalMilliseconds:F1}ms)";
}

/// <summary>
/// High-speed pure C# projection profile deskewer for industrial packaging lines.
/// Maximizes variance of the Horizontal Projection Profile (HPP) to find the precise baseline angle.
/// </summary>
public static class ProjectionProfileDeskewer
{
    /// <summary>
    /// Detects skew angle by testing angles in range [minAngle, maxAngle] with coarse-to-fine refinement.
    /// Zero heap allocation in search loops using stackalloc profiles.
    /// </summary>
    public static SkewDetectionResult DetectSkewAngle(
        OcrImageBuffer binaryImage,
        double minAngleDeg = -15.0,
        double maxAngleDeg = 15.0,
        double coarseStepDeg = 1.0,
        double fineStepDeg = 0.1)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (binaryImage.Format != OcrPixelFormat.Gray8)
            throw new ArgumentException("Input must be a binarized Gray8 image (foreground = 255).", nameof(binaryImage));

        double bestAngle = 0.0;
        double maxVariance = -1.0;

        // Step 1: Coarse sweep
        for (double angle = minAngleDeg; angle <= maxAngleDeg; angle += coarseStepDeg)
        {
            double varScore = CalculateHppVariance(binaryImage, angle);
            if (varScore > maxVariance)
            {
                maxVariance = varScore;
                bestAngle = angle;
            }
        }

        // Step 2: Fine refinement around best coarse angle
        double fineMin = Math.Max(minAngleDeg, bestAngle - coarseStepDeg);
        double fineMax = Math.Min(maxAngleDeg, bestAngle + coarseStepDeg);

        for (double angle = fineMin; angle <= fineMax; angle += fineStepDeg)
        {
            double varScore = CalculateHppVariance(binaryImage, angle);
            if (varScore > maxVariance)
            {
                maxVariance = varScore;
                bestAngle = angle;
            }
        }

        sw.Stop();
        return new SkewDetectionResult(bestAngle, maxVariance, sw.Elapsed);
    }

    /// <summary>
    /// Computes Horizontal Projection Profile (HPP) variance for a given virtual rotation angle without generating rotated images.
    /// </summary>
    private static double CalculateHppVariance(OcrImageBuffer binaryImage, double angleDeg)
    {
        int width = binaryImage.Width;
        int height = binaryImage.Height;
        int stride = binaryImage.Stride;
        var span = binaryImage.Span;

        double rad = angleDeg * (Math.PI / 180.0);
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        double cx = width * 0.5;
        double cy = height * 0.5;

        // Bounded stackalloc accumulator for scanlines
        Span<int> hpp = stackalloc int[height];
        hpp.Clear();

        long totalForeground = 0;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            double dy = y - cy;

            for (int x = 0; x < width; x++)
            {
                if (span[rowOffset + x] == 0) continue;

                totalForeground++;
                double dx = x - cx;

                // Rotated Y coordinate: y' = -dx*sinA + dy*cosA + cy
                int rotY = (int)Math.Round(-dx * sinA + dy * cosA + cy);

                if ((uint)rotY < (uint)height)
                {
                    hpp[rotY]++;
                }
            }
        }

        if (totalForeground == 0) return 0.0;

        double sumSquares = 0.0;
        for (int i = 0; i < height; i++)
        {
            int val = hpp[i];
            sumSquares += (double)val * val;
        }

        double mean = (double)totalForeground / height;
        return sumSquares - (mean * totalForeground);
    }

    /// <summary>
    /// Rotates the image by -angleDeg to deskew the text horizontally using bilinear interpolation.
    /// </summary>
    public static OcrImageBuffer RotateDeskew(OcrImageBuffer image, double angleDeg)
    {
        if (Math.Abs(angleDeg) < 0.05)
            return image;

        int w = image.Width;
        int h = image.Height;
        int bpp = image.BytesPerPixel;
        int stride = image.Stride;

        double rad = -angleDeg * (Math.PI / 180.0);
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        double cx = w * 0.5;
        double cy = h * 0.5;

        int dstStride = w * bpp;
        byte[] dst = new byte[dstStride * h];
        var srcSpan = image.Span;

        for (int y = 0; y < h; y++)
        {
            int dstRow = y * dstStride;
            double dy = y - cy;

            for (int x = 0; x < w; x++)
            {
                double dx = x - cx;

                double srcX = dx * cosA - dy * sinA + cx;
                double srcY = dx * sinA + dy * cosA + cy;

                int x0 = (int)Math.Floor(srcX);
                int y0 = (int)Math.Floor(srcY);

                if (x0 >= 0 && x0 < w - 1 && y0 >= 0 && y0 < h - 1)
                {
                    double fx = srcX - x0;
                    double fy = srcY - y0;

                    for (int c = 0; c < bpp; c++)
                    {
                        byte p00 = srcSpan[y0 * stride + x0 * bpp + c];
                        byte p10 = srcSpan[y0 * stride + (x0 + 1) * bpp + c];
                        byte p01 = srcSpan[(y0 + 1) * stride + x0 * bpp + c];
                        byte p11 = srcSpan[(y0 + 1) * stride + (x0 + 1) * bpp + c];

                        double val = (1 - fx) * (1 - fy) * p00 +
                                     fx * (1 - fy) * p10 +
                                     (1 - fx) * fy * p01 +
                                     fx * fy * p11;

                        dst[dstRow + x * bpp + c] = (byte)Math.Max(0, Math.Min(255, (int)val));
                    }
                }
                else
                {
                    for (int c = 0; c < bpp; c++)
                        dst[dstRow + x * bpp + c] = 0;
                }
            }
        }

        return new OcrImageBuffer(w, h, dstStride, image.Format, dst);
    }
}
