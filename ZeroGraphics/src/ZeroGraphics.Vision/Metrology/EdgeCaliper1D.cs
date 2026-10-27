using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// High-precision 1D Edge Caliper (Rake) for industrial dimensional metrology.
    /// Samples pixel intensity along a line segment and detects edge boundaries with sub-pixel resolution.
    /// </summary>
    public static class EdgeCaliper1D
    {
        /// <summary>
        /// Scans along a line segment from (x1, y1) to (x2, y2) and detects all edge transitions.
        /// </summary>
        /// <param name="image">Inspection image (Gray8 or Bgra32).</param>
        /// <param name="x1">Start X coordinate.</param>
        /// <param name="y1">Start Y coordinate.</param>
        /// <param name="x2">End X coordinate.</param>
        /// <param name="y2">End Y coordinate.</param>
        /// <param name="minMagnitude">Minimum gradient magnitude threshold (default 15.0).</param>
        /// <param name="polarity">Desired transition polarity.</param>
        /// <param name="sampleStep">Sampling interval along the line (default 0.5 pixels).</param>
        /// <returns>List of detected sub-pixel edges.</returns>
        public static unsafe List<CaliperEdge> FindEdges(
            ImageBuffer image,
            double x1, double y1,
            double x2, double y2,
            double minMagnitude = 15.0,
            EdgePolarity polarity = EdgePolarity.Any,
            double sampleStep = 0.5)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (sampleStep <= 0.0) throw new ArgumentOutOfRangeException(nameof(sampleStep));

            var results = new List<CaliperEdge>();

            double dx = x2 - x1;
            double dy = y2 - y1;
            double lineLength = Math.Sqrt(dx * dx + dy * dy);

            if (lineLength < 2.0) return results;

            int numSamples = (int)Math.Ceiling(lineLength / sampleStep) + 1;
            if (numSamples < 3) return results;

            // 1. Ensure Grayscale representation
            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;
            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(image.Width, image.Height);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            try
            {
                int imgW = grayImage.Width;
                int imgH = grayImage.Height;

                // 2. Sample intensity profile along the line segment via Bilinear Interpolation
                double[] profile = new double[numSamples];
                double[] sampleX = new double[numSamples];
                double[] sampleY = new double[numSamples];
                double[] sampleDist = new double[numSamples];

                for (int i = 0; i < numSamples; i++)
                {
                    double t = (double)i / (numSamples - 1);
                    double px = x1 + t * dx;
                    double py = y1 + t * dy;

                    sampleX[i] = px;
                    sampleY[i] = py;
                    sampleDist[i] = t * lineLength;

                    // Bilinear sample with clamping
                    if (px < 0.0 || px >= imgW - 1 || py < 0.0 || py >= imgH - 1)
                    {
                        int cx = (int)Math.Max(0, Math.Min(imgW - 1, Math.Round(px)));
                        int cy = (int)Math.Max(0, Math.Min(imgH - 1, Math.Round(py)));
                        profile[i] = *(grayImage.GetRowPointer(cy) + cx);
                    }
                    else
                    {
                        int x0 = (int)px;
                        int y0 = (int)py;
                        double fx = px - x0;
                        double fy = py - y0;

                        byte* r0 = grayImage.GetRowPointer(y0);
                        byte* r1 = grayImage.GetRowPointer(y0 + 1);

                        double p00 = r0[x0];
                        double p10 = r0[x0 + 1];
                        double p01 = r1[x0];
                        double p11 = r1[x0 + 1];

                        profile[i] = (1.0 - fx) * (1.0 - fy) * p00 +
                                     fx * (1.0 - fy) * p10 +
                                     (1.0 - fx) * fy * p01 +
                                     fx * fy * p11;
                    }
                }

                // 3. Compute 1st derivative (central difference)
                double[] deriv = new double[numSamples];
                for (int i = 1; i < numSamples - 1; i++)
                {
                    deriv[i] = (profile[i + 1] - profile[i - 1]) * 0.5;
                }

                // 4. Locate local extrema with magnitude >= minMagnitude
                for (int i = 2; i < numSamples - 2; i++)
                {
                    double dPrev = deriv[i - 1];
                    double dCur = deriv[i];
                    double dNext = deriv[i + 1];

                    double mag = Math.Abs(dCur);
                    if (mag < minMagnitude) continue;

                    bool isPeak = false;
                    EdgePolarity detectedPolarity;

                    if (dCur > 0)
                    {
                        // Positive derivative = Dark to Light
                        if (dCur >= dPrev && dCur >= dNext && (dCur > dPrev || dCur > dNext))
                        {
                            isPeak = true;
                            detectedPolarity = EdgePolarity.DarkToLight;
                        }
                        else continue;
                    }
                    else
                    {
                        // Negative derivative = Light to Dark
                        if (dCur <= dPrev && dCur <= dNext && (dCur < dPrev || dCur < dNext))
                        {
                            isPeak = true;
                            detectedPolarity = EdgePolarity.LightToDark;
                        }
                        else continue;
                    }

                    if (!isPeak) continue;

                    // Filter by polarity preference
                    if (polarity != EdgePolarity.Any && polarity != detectedPolarity)
                        continue;

                    // 5. Parabolic sub-pixel peak interpolation
                    double denom = 2.0 * (dPrev - 2.0 * dCur + dNext);
                    double deltaSample = 0.0;
                    if (Math.Abs(denom) > 1e-8)
                    {
                        deltaSample = (dPrev - dNext) / denom;
                        if (Math.Abs(deltaSample) > 1.0) deltaSample = 0.0;
                    }

                    double subIndex = i + deltaSample;
                    double subT = subIndex / (numSamples - 1);
                    double edgeX = x1 + subT * dx;
                    double edgeY = y1 + subT * dy;
                    double edgeDist = subT * lineLength;

                    // Peak magnitude interpolation
                    double peakMag = Math.Abs(dCur - 0.25 * (dPrev - dNext) * deltaSample);

                    results.Add(new CaliperEdge(edgeX, edgeY, edgeDist, peakMag, detectedPolarity));
                }

                return results;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        /// <summary>
        /// Finds the strongest single edge transition along the caliper line.
        /// </summary>
        public static CaliperEdge? FindStrongestEdge(
            ImageBuffer image,
            double x1, double y1,
            double x2, double y2,
            double minMagnitude = 15.0,
            EdgePolarity polarity = EdgePolarity.Any)
        {
            var edges = FindEdges(image, x1, y1, x2, y2, minMagnitude, polarity);
            if (edges.Count == 0) return null;

            CaliperEdge strongest = edges[0];
            for (int i = 1; i < edges.Count; i++)
            {
                if (edges[i].Magnitude > strongest.Magnitude)
                {
                    strongest = edges[i];
                }
            }
            return strongest;
        }

        /// <summary>
        /// Finds the first edge transition encountered along the caliper line from start to end.
        /// </summary>
        public static CaliperEdge? FindFirstEdge(
            ImageBuffer image,
            double x1, double y1,
            double x2, double y2,
            double minMagnitude = 15.0,
            EdgePolarity polarity = EdgePolarity.Any)
        {
            var edges = FindEdges(image, x1, y1, x2, y2, minMagnitude, polarity);
            return edges.Count > 0 ? edges[0] : (CaliperEdge?)null;
        }
    }
}
