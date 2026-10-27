using System;
using System.Buffers;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif
using ZeroGraphics.Core.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Industrial Normalized Cross-Correlation (NCC) Template Matcher.
    /// Provides linear brightness/contrast invariant pattern search with sub-pixel peak interpolation.
    /// Supports zero-copy native AVX2 dispatch via ZeroGraphicsNativeBridge and 0-LOH pooled C# fallback.
    /// </summary>
    public static class NccTemplateMatcher
    {
        /// <summary>
        /// Matches a template within a search image using Normalized Cross Correlation (NCC).
        /// </summary>
        /// <param name="searchImage">Source inspection image to search within.</param>
        /// <param name="templateImage">Target pattern to locate.</param>
        /// <param name="minScore">Minimum correlation threshold (default 0.75).</param>
        /// <param name="subPixelRefinement">Whether to compute sub-pixel parabolic peak refinement.</param>
        /// <returns>Matching result with sub-pixel center coordinates and confidence score.</returns>
        public static unsafe TemplateMatchResult Match(
            ImageBuffer searchImage,
            ImageBuffer templateImage,
            double minScore = 0.75,
            bool subPixelRefinement = true)
        {
            if (searchImage == null) throw new ArgumentNullException(nameof(searchImage));
            if (templateImage == null) throw new ArgumentNullException(nameof(templateImage));

            int sw = searchImage.Width;
            int sh = searchImage.Height;
            int tw = templateImage.Width;
            int th = templateImage.Height;

            if (tw > sw || th > sh || tw <= 0 || th <= 0)
                return TemplateMatchResult.NotFound;

            // 1. Ensure Grayscale representation for both images
            ImageBuffer? graySearchOwned = null;
            ImageBuffer? grayTemplateOwned = null;

            ImageBuffer graySearch = searchImage;
            if (searchImage.Format != ImageFormatMode.Gray8)
            {
                graySearchOwned = ImageBuffer.CreateGray8(sw, sh);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(searchImage, graySearchOwned);
                graySearch = graySearchOwned;
            }

            ImageBuffer grayTemplate = templateImage;
            if (templateImage.Format != ImageFormatMode.Gray8)
            {
                grayTemplateOwned = ImageBuffer.CreateGray8(tw, th);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(templateImage, grayTemplateOwned);
                grayTemplate = grayTemplateOwned;
            }

            double[]? rentedTPixels = null;
            double[]? rentedIi = null;
            double[]? rentedIi2 = null;
            double[]? rentedScoreMap = null;

            try
            {
                // Fast-path: Zero-copy native SIMD dispatch directly on unmanaged ImageBuffer pointers
                if (ZeroGraphicsNativeBridge.ShouldDispatchImage(sw, sh, minPixels: 1024) &&
                    ZeroGraphicsNativeBridge.TryNccMatch(
                        graySearch.Scan0, graySearch.Stride, sw, sh,
                        grayTemplate.Scan0, grayTemplate.Stride, tw, th,
                        minScore, subPixelRefinement,
                        out double nativeX, out double nativeY, out double nativeScore))
                {
                    if (nativeScore < minScore || nativeX < 0.0 || nativeY < 0.0)
                        return TemplateMatchResult.NotFound;

                    return new TemplateMatchResult(nativeX, nativeY, tw, th, nativeScore);
                }

                int n = tw * th;

                // 2. Precompute Template Mean and Standard Deviation using pooled array
                double tSum = 0.0;
                rentedTPixels = ArrayPool<double>.Shared.Rent(n);
                double[] tPixels = rentedTPixels;
                int tIdx = 0;

                for (int ty = 0; ty < th; ty++)
                {
                    byte* pRow = grayTemplate.GetRowPointer(ty);
                    for (int tx = 0; tx < tw; tx++)
                    {
                        double val = pRow[tx];
                        tPixels[tIdx++] = val;
                        tSum += val;
                    }
                }

                double tMean = tSum / n;
                double tVarianceSum = 0.0;
                for (int i = 0; i < n; i++)
                {
                    tPixels[i] -= tMean; // Zero-mean template
                    tVarianceSum += tPixels[i] * tPixels[i];
                }

                double tStdDev = Math.Sqrt(tVarianceSum);
                if (tStdDev < 1e-6)
                {
                    // Template has uniform flat intensity (no contrast features)
                    return TemplateMatchResult.NotFound;
                }

                // 3. Compute Integral Images of Search Image for O(1) Window Mean/Variance
                // II: sum of I, II2: sum of I^2 (Pooled 0 LOH allocations)
                int iiW = sw + 1;
                int iiH = sh + 1;
                int iiTotal = iiW * iiH;
                rentedIi = ArrayPool<double>.Shared.Rent(iiTotal);
                rentedIi2 = ArrayPool<double>.Shared.Rent(iiTotal);
                Array.Clear(rentedIi, 0, iiTotal);
                Array.Clear(rentedIi2, 0, iiTotal);

                double[] ii = rentedIi;
                double[] ii2 = rentedIi2;

                for (int y = 0; y < sh; y++)
                {
                    byte* pRow = graySearch.GetRowPointer(y);
                    double rowSum = 0.0;
                    double rowSum2 = 0.0;

                    int iiRowIdx = (y + 1) * iiW;
                    int iiPrevRowIdx = y * iiW;

                    for (int x = 0; x < sw; x++)
                    {
                        double val = pRow[x];
                        rowSum += val;
                        rowSum2 += val * val;

                        ii[iiRowIdx + (x + 1)] = ii[iiPrevRowIdx + (x + 1)] + rowSum;
                        ii2[iiRowIdx + (x + 1)] = ii2[iiPrevRowIdx + (x + 1)] + rowSum2;
                    }
                }

                // Helper local functions for O(1) rectangle queries
                double GetWindowSum(double[] table, int x, int y, int w, int h)
                {
                    int x2 = x + w;
                    int y2 = y + h;
                    return table[y2 * iiW + x2] - table[y * iiW + x2] - table[y2 * iiW + x] + table[y * iiW + x];
                }

                // 4. Slide window across search image
                int outW = sw - tw + 1;
                int outH = sh - th + 1;

                double bestScore = -1.0;
                int bestX = -1;
                int bestY = -1;

                // Cache correlation surface for sub-pixel interpolation using pooled array
                if (subPixelRefinement)
                {
                    int scoreTotal = outW * outH;
                    rentedScoreMap = ArrayPool<double>.Shared.Rent(scoreTotal);
                }
                double[]? scoreMap = rentedScoreMap;

                fixed (double* pT = tPixels)
                {
                    for (int y = 0; y < outH; y++)
                    {
                        for (int x = 0; x < outW; x++)
                        {
                            // O(1) Window variance computation
                            double iSum = GetWindowSum(ii, x, y, tw, th);
                            double iSum2 = GetWindowSum(ii2, x, y, tw, th);
                            double iVar = iSum2 - (iSum * iSum) / n;

                            if (iVar <= 1e-6)
                            {
                                if (scoreMap != null) scoreMap[y * outW + x] = 0.0;
                                continue;
                            }

                            double iStdDev = Math.Sqrt(iVar);

                            // Cross-product sum: sum( (T - tMean) * I )
                            double crossSum = 0.0;

#if NET8_0_OR_GREATER
                            if (Vector256.IsHardwareAccelerated && tw >= 4)
                            {
                                Vector256<double> vAcc = Vector256<double>.Zero;
                                int simdLimit = tw - 4;
                                for (int ty = 0; ty < th; ty++)
                                {
                                    double* pTRow = pT + ty * tw;
                                    byte* pSearchRow = graySearch.GetRowPointer(y + ty) + x;
                                    int tx = 0;
                                    for (; tx <= simdLimit; tx += 4)
                                    {
                                        var vT = Vector256.Load(pTRow + tx);
                                        var vI = Vector256.Create(
                                            (double)pSearchRow[tx],
                                            (double)pSearchRow[tx + 1],
                                            (double)pSearchRow[tx + 2],
                                            (double)pSearchRow[tx + 3]);
                                        vAcc += vT * vI;
                                    }
                                    for (; tx < tw; tx++)
                                    {
                                        crossSum += pTRow[tx] * pSearchRow[tx];
                                    }
                                }
                                crossSum += Vector256.Sum(vAcc);
                            }
                            else
#endif
                            {
                                int unrollLimit = tw - 4;
                                for (int ty = 0; ty < th; ty++)
                                {
                                    double* pTRow = pT + ty * tw;
                                    byte* pSearchRow = graySearch.GetRowPointer(y + ty) + x;
                                    int tx = 0;
                                    for (; tx <= unrollLimit; tx += 4)
                                    {
                                        crossSum +=
                                            pTRow[tx]     * pSearchRow[tx] +
                                            pTRow[tx + 1] * pSearchRow[tx + 1] +
                                            pTRow[tx + 2] * pSearchRow[tx + 2] +
                                            pTRow[tx + 3] * pSearchRow[tx + 3];
                                    }
                                    for (; tx < tw; tx++)
                                    {
                                        crossSum += pTRow[tx] * pSearchRow[tx];
                                    }
                                }
                            }

                            double score = crossSum / (tStdDev * iStdDev);
                            if (scoreMap != null) scoreMap[y * outW + x] = score;

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestX = x;
                                bestY = y;
                            }
                        }
                    }
                }

                if (bestScore < minScore || bestX < 0)
                {
                    return TemplateMatchResult.NotFound;
                }

                // 5. 2D Parabolic Sub-Pixel Peak Interpolation
                double finalX = bestX;
                double finalY = bestY;

                if (subPixelRefinement && scoreMap != null)
                {
                    if (bestX > 0 && bestX < outW - 1)
                    {
                        double l = scoreMap[bestY * outW + (bestX - 1)];
                        double c = scoreMap[bestY * outW + bestX];
                        double r = scoreMap[bestY * outW + (bestX + 1)];
                        double denom = 2.0 * (l - 2.0 * c + r);
                        if (Math.Abs(denom) > 1e-8)
                        {
                            double deltaX = (l - r) / denom;
                            if (Math.Abs(deltaX) <= 1.0) finalX += deltaX;
                        }
                    }

                    if (bestY > 0 && bestY < outH - 1)
                    {
                        double t = scoreMap[(bestY - 1) * outW + bestX];
                        double c = scoreMap[bestY * outW + bestX];
                        double b = scoreMap[(bestY + 1) * outW + bestX];
                        double denom = 2.0 * (t - 2.0 * c + b);
                        if (Math.Abs(denom) > 1e-8)
                        {
                            double deltaY = (t - b) / denom;
                            if (Math.Abs(deltaY) <= 1.0) finalY += deltaY;
                        }
                    }
                }

                return new TemplateMatchResult(finalX, finalY, tw, th, bestScore);
            }
            finally
            {
                if (rentedTPixels != null) ArrayPool<double>.Shared.Return(rentedTPixels);
                if (rentedIi != null) ArrayPool<double>.Shared.Return(rentedIi);
                if (rentedIi2 != null) ArrayPool<double>.Shared.Return(rentedIi2);
                if (rentedScoreMap != null) ArrayPool<double>.Shared.Return(rentedScoreMap);

                graySearchOwned?.Dispose();
                grayTemplateOwned?.Dispose();
            }
        }
    }
}
