using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Vision.Contours;
using ZeroGraphics.Vision.Stitching;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# ISO/IEC 18004 QR Code reader and detector.
    /// Supports 1:1:3:1:1 finder pattern ratio scanning, BCH (15, 5) format information decoding,
    /// Homography2D perspective rectification, Reed-Solomon in-place error correction,
    /// and multi-mode (Numeric, Alphanumeric, Byte, Kanji) payload decoding.
    /// </summary>
    public sealed class QrDecoder
    {
        private readonly ReedSolomonDecoder _rsDecoder;

        public QrDecoder()
        {
            _rsDecoder = new ReedSolomonDecoder(GenericGF.QrCode256);
        }

        /// <summary>
        /// Decodes a 2D boolean module grid representing a full QR Code symbol (Version 1 to 10).
        /// </summary>
        public BarcodeResult? DecodeSymbolGrid(bool[,] fullSymbolGrid, QrVersion? hintVersion = null)
        {
            if (fullSymbolGrid == null)
                return null;

            int dim = fullSymbolGrid.GetLength(0);
            if (dim != fullSymbolGrid.GetLength(1) || dim < 21 || (dim - 17) % 4 != 0)
                return null;

            int versionNum = (dim - 17) / 4;
            var version = hintVersion ?? QrVersion.GetByVersionNumber(versionNum);
            if (version == null)
                return null;

            // Try all 4 rotations (0, 90, 180, 270 degrees)
            for (int rot = 0; rot < 4; rot++)
            {
                bool[,] orientedGrid = RotateGrid(fullSymbolGrid, rot);

                // Coordinates for 15-bit format information
                // Position 1: Around Top-Left
                int[] r1 = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
                int[] c1 = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };

                // Position 2: Bottom-Left and Top-Right
                int[] r2 = { 8, 8, 8, 8, 8, 8, 8, 8, dim - 7, dim - 6, dim - 5, dim - 4, dim - 3, dim - 2, dim - 1 };
                int[] c2 = { dim - 1, dim - 2, dim - 3, dim - 4, dim - 5, dim - 6, dim - 7, dim - 8, 8, 8, 8, 8, 8, 8, 8 };

                int f1 = 0;
                int f2 = 0;
                for (int i = 0; i < 15; i++)
                {
                    if (orientedGrid[r1[i], c1[i]]) f1 |= (1 << i);
                    if (orientedGrid[r2[i], c2[i]]) f2 |= (1 << i);
                }

                var formatInfo = QrFormatInfo.DecodeFormatInformation(f1, f2);
                if (formatInfo == null)
                    continue;

                try
                {
                    bool[,] funcPattern = QrBitParser.BuildFunctionPattern(version);
                    bool[,] unmasked = QrBitParser.ApplyMask(orientedGrid, formatInfo.DataMask, funcPattern);
                    byte[] rawCodewords = QrBitParser.ReadCodewords(unmasked, version, funcPattern);

                    var dataBlocks = QrBitParser.Deinterleave(rawCodewords, version, formatInfo.ErrorCorrectionLevel);
                    var ecBlocks = version.GetECBlocks(formatInfo.ErrorCorrectionLevel);
                    int ecCount = ecBlocks.ECCodewordsPerBlock;

                    bool allSuccess = true;
                    var correctedBlocks = new List<byte[]>();

                    foreach (var block in dataBlocks)
                    {
                        int[] ints = new int[block.Codewords.Length];
                        for (int k = 0; k < block.Codewords.Length; k++)
                        {
                            ints[k] = block.Codewords[k] & 0xFF;
                        }

                        if (!_rsDecoder.Decode(ints, ecCount))
                        {
                            allSuccess = false;
                            break;
                        }

                        byte[] data = new byte[block.NumDataCodewords];
                        for (int k = 0; k < block.NumDataCodewords; k++)
                        {
                            data[k] = (byte)ints[k];
                        }

                        correctedBlocks.Add(data);
                    }

                    if (!allSuccess)
                        continue;

                    byte[] allData = new byte[ecBlocks.TotalDataCodewords];
                    int offset = 0;
                    foreach (var b in correctedBlocks)
                    {
                        Array.Copy(b, 0, allData, offset, b.Length);
                        offset += b.Length;
                    }

                    string text = QrPayloadDecoder.Decode(allData, version);
                    if (!string.IsNullOrEmpty(text))
                    {
                        var corners = new PointF[]
                        {
                            new PointF(0, 0),
                            new PointF(dim, 0),
                            new PointF(dim, dim),
                            new PointF(0, dim)
                        };

                        return new BarcodeResult(
                            text: text,
                            symbology: BarcodeSymbology.QrCode,
                            rawBytes: allData,
                            cornerPoints: corners,
                            confidence: 1.0);
                    }
                }
                catch
                {
                    // Continue trying next rotation
                }
            }

            return null;
        }

        /// <summary>
        /// Detects and decodes a QR Code symbol from an ImageBuffer.
        /// </summary>
        public unsafe BarcodeResult? Decode(ImageBuffer image)
        {
            if (image == null || image.Width < 10 || image.Height < 10)
                return null;

            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;

            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(image.Width, image.Height);
                ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            try
            {
                int threshold = ComputeOtsuThreshold(grayImage);
                byte* gPtr = grayImage.Scan0;
                int stride = grayImage.Stride;

                // 1. Finder Pattern Ratio Scanning (1:1:3:1:1)
                var finderResult = TryDecodeFinderPatterns(grayImage, gPtr, stride, threshold);
                if (finderResult != null)
                    return finderResult;

                // 2. Quadrilateral contour tracing
                using (var binary = ImageBuffer.CreateGray8(grayImage.Width, grayImage.Height))
                {
                    byte* bPtr = binary.Scan0;
                    int totalPixels = grayImage.Width * grayImage.Height;

                    for (int i = 0; i < totalPixels; i++)
                    {
                        bPtr[i] = gPtr[i] <= threshold ? (byte)255 : (byte)0;
                    }

                    var contours = ContourTracer.FindContours(binary, minPoints: 16);
                    foreach (var contour in contours)
                    {
                        if (contour.IsHole) continue;

                        double area = ContourFeatures.ComputeArea(contour.Points);
                        if (area < 100) continue;

                        double perimeter = ContourFeatures.ComputePerimeter(contour.Points);
                        var quad = ContourFeatures.ApproximatePolygon(contour.Points, epsilon: perimeter * 0.03, closed: true);

                        if (quad.Count == 4)
                        {
                            var result = TryDecodeQuad(grayImage, quad, threshold);
                            if (result != null)
                                return result;
                        }
                    }

                    // 3. Foreground bounding box direct sampling
                    var boundingBox = ContourFeatures.ComputeBoundingBox(GetForegroundPoints(binary));
                    if (boundingBox.Width >= 10 && boundingBox.Height >= 10)
                    {
                        var result = TryDirectSampling(grayImage, boundingBox, threshold);
                        if (result != null)
                            return result;
                    }

                    // 4. Whole image direct sampling
                    var fullBox = new Rectangle(0, 0, grayImage.Width, grayImage.Height);
                    var fullResult = TryDirectSampling(grayImage, fullBox, threshold);
                    if (fullResult != null)
                        return fullResult;
                }

                return null;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        private readonly struct RunSegment
        {
            public readonly bool IsBlack;
            public readonly int Length;
            public readonly int StartX;

            public RunSegment(bool isBlack, int length, int startX)
            {
                IsBlack = isBlack;
                Length = length;
                StartX = startX;
            }
        }

        private sealed class FinderPatternInfo
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float EstimatedModuleSize { get; set; }
            public int Count { get; set; }

            public FinderPatternInfo(float x, float y, float m)
            {
                X = x;
                Y = y;
                EstimatedModuleSize = m;
                Count = 1;
            }

            public bool AboutEquals(float x, float y, float m)
            {
                float dx = Math.Abs(x - X);
                float dy = Math.Abs(y - Y);
                if (dx <= EstimatedModuleSize * 2.0f && dy <= EstimatedModuleSize * 2.0f)
                {
                    float dm = Math.Abs(m - EstimatedModuleSize);
                    return dm <= 1.5f || dm <= EstimatedModuleSize * 0.5f;
                }
                return false;
            }

            public void Combine(float x, float y, float m)
            {
                X = (X * Count + x) / (Count + 1);
                Y = (Y * Count + y) / (Count + 1);
                EstimatedModuleSize = (EstimatedModuleSize * Count + m) / (Count + 1);
                Count++;
            }
        }

        private unsafe BarcodeResult? TryDecodeFinderPatterns(ImageBuffer gray, byte* gPtr, int stride, int threshold)
        {
            int w = gray.Width;
            int h = gray.Height;
            int stepY = Math.Max(1, h / 300);

            var candidates = new List<FinderPatternInfo>();
            var runs = new List<RunSegment>(128);

            for (int y = 0; y < h; y += stepY)
            {
                runs.Clear();
                byte* row = gPtr + y * stride;
                bool curBlack = row[0] <= threshold;
                int runStart = 0;

                for (int x = 1; x < w; x++)
                {
                    bool isBlack = row[x] <= threshold;
                    if (isBlack != curBlack)
                    {
                        runs.Add(new RunSegment(curBlack, x - runStart, runStart));
                        curBlack = isBlack;
                        runStart = x;
                    }
                }
                runs.Add(new RunSegment(curBlack, w - runStart, runStart));

                for (int i = 0; i <= runs.Count - 5; i++)
                {
                    if (runs[i].IsBlack && !runs[i + 1].IsBlack && runs[i + 2].IsBlack && !runs[i + 3].IsBlack && runs[i + 4].IsBlack)
                    {
                        int b1 = runs[i].Length;
                        int w1 = runs[i + 1].Length;
                        int b2 = runs[i + 2].Length;
                        int w2 = runs[i + 3].Length;
                        int b3 = runs[i + 4].Length;

                        int total = b1 + w1 + b2 + w2 + b3;
                        double m = total / 7.0;
                        if (m < 1.0) continue;

                        double maxVar = m * 0.75;
                        if (Math.Abs(b1 - m) <= maxVar &&
                            Math.Abs(w1 - m) <= maxVar &&
                            Math.Abs(b2 - 3 * m) <= 3 * maxVar &&
                            Math.Abs(w2 - m) <= maxVar &&
                            Math.Abs(b3 - m) <= maxVar)
                        {
                            double cx = runs[i + 2].StartX + runs[i + 2].Length / 2.0;
                            if (VerifyVerticalFinder(gPtr, w, h, stride, (int)Math.Round(cx), y, m, threshold, out double cy, out double mv))
                            {
                                AddOrMergeCandidate(candidates, (float)cx, (float)cy, (float)((m + mv) * 0.5));
                            }
                        }
                    }
                }
            }

            if (candidates.Count < 3)
                return null;

            candidates.Sort((a, b) => b.Count.CompareTo(a.Count));
            int takeCount = Math.Min(candidates.Count, 8);

            for (int i = 0; i < takeCount; i++)
            {
                for (int j = i + 1; j < takeCount; j++)
                {
                    for (int k = j + 1; k < takeCount; k++)
                    {
                        var p1 = candidates[i];
                        var p2 = candidates[j];
                        var p3 = candidates[k];

                        double d12 = Dist(p1, p2);
                        double d23 = Dist(p2, p3);
                        double d31 = Dist(p3, p1);

                        FinderPatternInfo tl, trCandidate, blCandidate;
                        double da, db, hyp;

                        if (d23 >= d12 && d23 >= d31)
                        {
                            tl = p1; trCandidate = p2; blCandidate = p3;
                            da = d12; db = d31; hyp = d23;
                        }
                        else if (d31 >= d12 && d31 >= d23)
                        {
                            tl = p2; trCandidate = p3; blCandidate = p1;
                            da = d23; db = d12; hyp = d31;
                        }
                        else
                        {
                            tl = p3; trCandidate = p1; blCandidate = p2;
                            da = d31; db = d23; hyp = d12;
                        }

                        if (Math.Abs(da - db) / Math.Max(da, db) > 0.35) continue;
                        double expHyp = Math.Sqrt(da * da + db * db);
                        if (Math.Abs(hyp - expHyp) / hyp > 0.25) continue;

                        double z = (trCandidate.X - tl.X) * (blCandidate.Y - tl.Y) - (trCandidate.Y - tl.Y) * (blCandidate.X - tl.X);
                        FinderPatternInfo tr = (z > 0) ? trCandidate : blCandidate;
                        FinderPatternInfo bl = (z > 0) ? blCandidate : trCandidate;

                        float brX = tr.X + bl.X - tl.X;
                        float brY = tr.Y + bl.Y - tl.Y;

                        double mAvg = (tl.EstimatedModuleSize + tr.EstimatedModuleSize + bl.EstimatedModuleSize) / 3.0;
                        double dAvg = (da + db) * 0.5;
                        int nEst = (int)Math.Round(dAvg / mAvg) + 7;

                        var versionsToTest = new List<QrVersion>(QrVersion.AllVersions);
                        versionsToTest.Sort((v1, v2) => Math.Abs(v1.Dimension - nEst).CompareTo(Math.Abs(v2.Dimension - nEst)));

                        foreach (var v in versionsToTest)
                        {
                            int s = v.Dimension;
                            var srcPts = new PointF[]
                            {
                                new PointF(3.5f, 3.5f),
                                new PointF(s - 3.5f, 3.5f),
                                new PointF(s - 3.5f, s - 3.5f),
                                new PointF(3.5f, s - 3.5f)
                            };

                            var dstPts = new PointF[]
                            {
                                new PointF(tl.X, tl.Y),
                                new PointF(tr.X, tr.Y),
                                new PointF(brX, brY),
                                new PointF(bl.X, bl.Y)
                            };

                            try
                            {
                                var H = Homography2D.Estimate(srcPts, dstPts);
                                bool[,] grid = new bool[s, s];

                                for (int r = 0; r < s; r++)
                                {
                                    for (int c = 0; c < s; c++)
                                    {
                                        var imgPt = H.TransformPoint(c + 0.5f, r + 0.5f);
                                        int ix = (int)Math.Round(imgPt.X);
                                        int iy = (int)Math.Round(imgPt.Y);
                                        if (ix >= 0 && ix < w && iy >= 0 && iy < h)
                                        {
                                            grid[r, c] = gPtr[iy * stride + ix] <= threshold;
                                        }
                                    }
                                }

                                var res = DecodeSymbolGrid(grid, v);
                                if (res != null)
                                {
                                    var corners = new PointF[]
                                    {
                                        H.TransformPoint(0, 0),
                                        H.TransformPoint(s, 0),
                                        H.TransformPoint(s, s),
                                        H.TransformPoint(0, s)
                                    };
                                    return new BarcodeResult(res.Text, BarcodeSymbology.QrCode, res.RawBytes, corners, 1.0);
                                }
                            }
                            catch
                            {
                                // Continue trying candidate version
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static unsafe bool VerifyVerticalFinder(
            byte* gPtr, int w, int h, int stride,
            int cx, int cy, double m, int threshold,
            out double outCy, out double outMv)
        {
            outCy = 0;
            outMv = 0;

            if (cx < 0 || cx >= w || cy < 0 || cy >= h) return false;
            if (gPtr[cy * stride + cx] > threshold) return false;

            // 1. Center black run
            int top = cy;
            while (top >= 0 && gPtr[top * stride + cx] <= threshold) top--;
            int bot = cy + 1;
            while (bot < h && gPtr[bot * stride + cx] <= threshold) bot++;

            int vb2 = bot - 1 - top;
            if (vb2 <= 0) return false;

            // 2. White run above
            int wTop = top;
            while (wTop >= 0 && gPtr[wTop * stride + cx] > threshold) wTop--;
            int vw1 = top - wTop;
            if (vw1 <= 0) return false;

            // 3. Black run above
            int bTop = wTop;
            while (bTop >= 0 && gPtr[bTop * stride + cx] <= threshold) bTop--;
            int vb1 = wTop - bTop;
            if (vb1 <= 0) return false;

            // 4. White run below
            int wBot = bot;
            while (wBot < h && gPtr[wBot * stride + cx] > threshold) wBot++;
            int vw2 = wBot - bot;
            if (vw2 <= 0) return false;

            // 5. Black run below
            int bBot = wBot;
            while (bBot < h && gPtr[bBot * stride + cx] <= threshold) bBot++;
            int vb3 = bBot - wBot;
            if (vb3 <= 0) return false;

            int totalV = vb1 + vw1 + vb2 + vw2 + vb3;
            double mv = totalV / 7.0;
            if (mv < 1.0) return false;

            double maxVarV = mv * 0.75;
            if (Math.Abs(vb1 - mv) > maxVarV ||
                Math.Abs(vw1 - mv) > maxVarV ||
                Math.Abs(vb2 - 3 * mv) > 3 * maxVarV ||
                Math.Abs(vw2 - mv) > maxVarV ||
                Math.Abs(vb3 - mv) > maxVarV)
            {
                return false;
            }

            if (Math.Abs(m - mv) > Math.Max(m, mv) * 0.6)
                return false;

            outCy = top + 1 + (vb2 - 1) / 2.0;
            outMv = mv;
            return true;
        }

        private static void AddOrMergeCandidate(List<FinderPatternInfo> candidates, float x, float y, float m)
        {
            foreach (var cand in candidates)
            {
                if (cand.AboutEquals(x, y, m))
                {
                    cand.Combine(x, y, m);
                    return;
                }
            }
            candidates.Add(new FinderPatternInfo(x, y, m));
        }

        private static double Dist(FinderPatternInfo a, FinderPatternInfo b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private unsafe BarcodeResult? TryDecodeQuad(ImageBuffer gray, List<Point> quad, int threshold)
        {
            var ordered = OrderQuadPoints(quad);
            byte* gPtr = gray.Scan0;
            int w = gray.Width;
            int h = gray.Height;
            int stride = gray.Stride;

            foreach (var version in QrVersion.AllVersions)
            {
                int s = version.Dimension;
                var dstPoints = new PointF[]
                {
                    new PointF(0, 0),
                    new PointF(s, 0),
                    new PointF(s, s),
                    new PointF(0, s)
                };

                var srcPoints = new PointF[]
                {
                    new PointF(ordered[0].X, ordered[0].Y),
                    new PointF(ordered[1].X, ordered[1].Y),
                    new PointF(ordered[2].X, ordered[2].Y),
                    new PointF(ordered[3].X, ordered[3].Y)
                };

                try
                {
                    var homography = Homography2D.Estimate(dstPoints, srcPoints);
                    bool[,] grid = new bool[s, s];

                    for (int r = 0; r < s; r++)
                    {
                        for (int c = 0; c < s; c++)
                        {
                            var imgPt = homography.TransformPoint(c + 0.5f, r + 0.5f);
                            int ix = (int)Math.Round(imgPt.X);
                            int iy = (int)Math.Round(imgPt.Y);
                            if (ix >= 0 && ix < w && iy >= 0 && iy < h)
                            {
                                grid[r, c] = gPtr[iy * stride + ix] <= threshold;
                            }
                        }
                    }

                    var decodeRes = DecodeSymbolGrid(grid, version);
                    if (decodeRes != null)
                    {
                        var corners = new PointF[4];
                        for (int i = 0; i < 4; i++)
                        {
                            corners[i] = new PointF(ordered[i].X, ordered[i].Y);
                        }

                        return new BarcodeResult(
                            text: decodeRes.Text,
                            symbology: BarcodeSymbology.QrCode,
                            rawBytes: decodeRes.RawBytes,
                            cornerPoints: corners,
                            confidence: 1.0);
                    }
                }
                catch
                {
                    // Continue trying next candidate version
                }
            }

            return null;
        }

        private unsafe BarcodeResult? TryDirectSampling(ImageBuffer gray, Rectangle box, int threshold)
        {
            byte* gPtr = gray.Scan0;
            int w = gray.Width;
            int h = gray.Height;
            int stride = gray.Stride;

            foreach (var version in QrVersion.AllVersions)
            {
                int s = version.Dimension;
                bool[,] grid = new bool[s, s];
                double stepX = (double)box.Width / s;
                double stepY = (double)box.Height / s;

                for (int r = 0; r < s; r++)
                {
                    int py = (int)(box.Y + (r + 0.5) * stepY);
                    if (py < 0 || py >= h) continue;

                    for (int c = 0; c < s; c++)
                    {
                        int px = (int)(box.X + (c + 0.5) * stepX);
                        if (px < 0 || px >= w) continue;

                        grid[r, c] = gPtr[py * stride + px] <= threshold;
                    }
                }

                var res = DecodeSymbolGrid(grid, version);
                if (res != null)
                {
                    var corners = new PointF[]
                    {
                        new PointF(box.Left, box.Top),
                        new PointF(box.Right, box.Top),
                        new PointF(box.Right, box.Bottom),
                        new PointF(box.Left, box.Bottom)
                    };

                    return new BarcodeResult(
                        text: res.Text,
                        symbology: BarcodeSymbology.QrCode,
                        rawBytes: res.RawBytes,
                        cornerPoints: corners,
                        confidence: 1.0);
                }
            }

            return null;
        }

        private static bool[,] RotateGrid(bool[,] grid, int rotation)
        {
            if (rotation == 0) return grid;
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            bool[,] result;
            if (rotation == 1) // 90 deg clockwise
            {
                result = new bool[w, h];
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        result[c, h - 1 - r] = grid[r, c];
            }
            else if (rotation == 2) // 180 deg
            {
                result = new bool[h, w];
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        result[h - 1 - r, w - 1 - c] = grid[r, c];
            }
            else // 270 deg clockwise
            {
                result = new bool[w, h];
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        result[w - 1 - c, r] = grid[r, c];
            }
            return result;
        }

        private static unsafe int ComputeOtsuThreshold(ImageBuffer gray)
        {
            byte* data = gray.Scan0;
            int w = gray.Width;
            int h = gray.Height;
            int stride = gray.Stride;

            int[] hist = new int[256];
            int totalPixels = w * h;

            for (int y = 0; y < h; y++)
            {
                byte* row = data + y * stride;
                for (int x = 0; x < w; x++)
                {
                    hist[row[x]]++;
                }
            }

            double sum = 0;
            for (int i = 0; i < 256; i++) sum += i * hist[i];

            double sumB = 0;
            int wB = 0;
            double maxVar = 0;
            int threshold = 128;

            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                int wF = totalPixels - wB;
                if (wF == 0) break;

                sumB += t * hist[t];
                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;

                double betweenVar = (double)wB * wF * (mB - mF) * (mB - mF);
                if (betweenVar > maxVar)
                {
                    maxVar = betweenVar;
                    threshold = t;
                }
            }

            return threshold;
        }

        private static List<Point> OrderQuadPoints(List<Point> pts)
        {
            float cx = 0, cy = 0;
            for (int i = 0; i < pts.Count; i++) { cx += pts[i].X; cy += pts[i].Y; }
            cx /= pts.Count; cy /= pts.Count;

            var sorted = new List<Point>(pts);
            sorted.Sort((p1, p2) =>
            {
                double a1 = Math.Atan2(p1.Y - cy, p1.X - cx);
                double a2 = Math.Atan2(p2.Y - cy, p2.X - cx);
                return a1.CompareTo(a2);
            });

            int tlIdx = 0;
            int minSum = int.MaxValue;
            for (int i = 0; i < sorted.Count; i++)
            {
                int sum = sorted[i].X + sorted[i].Y;
                if (sum < minSum)
                {
                    minSum = sum;
                    tlIdx = i;
                }
            }

            var ordered = new List<Point>(4);
            for (int i = 0; i < 4; i++)
            {
                ordered.Add(sorted[(tlIdx + i) % 4]);
            }
            return ordered;
        }

        private static unsafe List<Point> GetForegroundPoints(ImageBuffer binary)
        {
            var pts = new List<Point>();
            byte* ptr = binary.Scan0;
            int w = binary.Width;
            int h = binary.Height;
            int stride = binary.Stride;

            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + y * stride;
                for (int x = 0; x < w; x++)
                {
                    if (row[x] != 0)
                    {
                        pts.Add(new Point(x, y));
                    }
                }
            }
            return pts;
        }
    }
}
