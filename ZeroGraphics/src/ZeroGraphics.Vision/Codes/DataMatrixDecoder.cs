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
    /// Pure C# ISO/IEC 16022 DataMatrix ECC200 reader and detector.
    /// Supports quadrilateral contour rectification via Homography2D, multi-orientation L-finder matching,
    /// Reed-Solomon in-place error correction, and multi-mode payload decoding.
    /// </summary>
    public sealed class DataMatrixDecoder
    {
        private readonly ReedSolomonDecoder _rsDecoder;

        public DataMatrixDecoder()
        {
            _rsDecoder = new ReedSolomonDecoder(GenericGF.DataMatrix256);
        }

        /// <summary>
        /// Decodes a 2D boolean module grid representing a full DataMatrix ECC200 symbol (including L-finder and timing borders).
        /// </summary>
        public BarcodeResult? DecodeSymbolGrid(bool[,] fullSymbolGrid, DataMatrixVersion? hintVersion = null)
        {
            if (fullSymbolGrid == null)
                return null;

            int height = fullSymbolGrid.GetLength(0);
            int width = fullSymbolGrid.GetLength(1);

            DataMatrixVersion? version = hintVersion ?? DataMatrixVersion.FindVersion(width, height);
            if (version == null)
                return null;

            // Test 4 orientations (0, 90, 180, 270 degrees)
            for (int rot = 0; rot < 4; rot++)
            {
                bool[,] orientedGrid = RotateGrid(fullSymbolGrid, rot);

                // Check L-finder pattern (solid left column & solid bottom row)
                if (CheckFinderPattern(orientedGrid, version.SymbolWidth, version.SymbolHeight))
                {
                    // Extract inner data region
                    bool[,] dataRegion = ExtractDataRegion(orientedGrid, version);

                    var result = DecodeDataGrid(dataRegion, version);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            // Fallback: try decoding directly in case borders are slightly degraded
            for (int rot = 0; rot < 4; rot++)
            {
                bool[,] orientedGrid = RotateGrid(fullSymbolGrid, rot);
                bool[,] dataRegion = ExtractDataRegion(orientedGrid, version);
                var result = DecodeDataGrid(dataRegion, version);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Decodes an isolated inner data region module grid (DataRows x DataColumns).
        /// </summary>
        public BarcodeResult? DecodeDataGrid(bool[,] dataGrid, DataMatrixVersion version)
        {
            if (dataGrid == null || version == null)
                return null;

            if (dataGrid.GetLength(0) != version.DataRows || dataGrid.GetLength(1) != version.DataColumns)
                return null;

            try
            {
                // Read codewords using ISO 16022 Utah diagonal sweep
                byte[] rawCodewords = DataMatrixBitParser.ReadCodewords(dataGrid, version.TotalCodewords);

                // Convert to int array for Reed-Solomon decoding
                int[] codewords = new int[rawCodewords.Length];
                for (int i = 0; i < rawCodewords.Length; i++)
                {
                    codewords[i] = rawCodewords[i] & 0xFF;
                }

                // Perform Reed-Solomon error correction in-place
                bool rsSuccess = _rsDecoder.Decode(codewords, version.ErrorCodewords);
                if (!rsSuccess)
                {
                    return null;
                }

                // Copy corrected bytes
                byte[] correctedBytes = new byte[rawCodewords.Length];
                for (int i = 0; i < rawCodewords.Length; i++)
                {
                    correctedBytes[i] = (byte)codewords[i];
                }

                // Decode payload
                string payload = DataMatrixPayloadDecoder.Decode(correctedBytes, version.DataCodewords);
                if (string.IsNullOrEmpty(payload))
                {
                    return null;
                }

                var corners = new PointF[]
                {
                    new PointF(0, 0),
                    new PointF(version.SymbolWidth, 0),
                    new PointF(version.SymbolWidth, version.SymbolHeight),
                    new PointF(0, version.SymbolHeight)
                };

                return new BarcodeResult(
                    text: payload,
                    symbology: BarcodeSymbology.DataMatrix,
                    rawBytes: correctedBytes,
                    cornerPoints: corners,
                    confidence: 1.0);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Detects and decodes a DataMatrix ECC200 symbol from an ImageBuffer.
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
                // Compute Otsu threshold
                int threshold = ComputeOtsuThreshold(grayImage);

                // Create binary image for contour tracing
                using (var binary = ImageBuffer.CreateGray8(grayImage.Width, grayImage.Height))
                {
                    byte* gPtr = grayImage.Scan0;
                    byte* bPtr = binary.Scan0;
                    int totalPixels = grayImage.Width * grayImage.Height;

                    for (int i = 0; i < totalPixels; i++)
                    {
                        // DataMatrix foreground (black modules) is below or equal to threshold
                        bPtr[i] = gPtr[i] <= threshold ? (byte)255 : (byte)0;
                    }

                    // 1. Try finding quadrilateral contours
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

                    // 2. Direct sampling fallback: test standard square grid across image bounds
                    var boundingBox = ContourFeatures.ComputeBoundingBox(GetForegroundPoints(binary));
                    if (boundingBox.Width >= 10 && boundingBox.Height >= 10)
                    {
                        var result = TryDirectSampling(grayImage, boundingBox, threshold);
                        if (result != null)
                            return result;
                    }

                    // 3. Try whole image direct sampling
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

        private unsafe BarcodeResult? TryDecodeQuad(ImageBuffer gray, List<Point> quad, int threshold)
        {
            // Order quad vertices: Top-Left, Top-Right, Bottom-Right, Bottom-Left
            var ordered = OrderQuadPoints(quad);

            foreach (var version in DataMatrixVersion.AllVersions)
            {
                int s = version.SymbolWidth;
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
                    // Map from canonical grid to image points
                    var homography = Homography2D.Estimate(dstPoints, srcPoints);
                    bool[,] grid = new bool[s, s];

                    byte* gPtr = gray.Scan0;
                    int w = gray.Width;
                    int h = gray.Height;
                    int stride = gray.Stride;

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
                            symbology: BarcodeSymbology.DataMatrix,
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

            foreach (var version in DataMatrixVersion.AllVersions)
            {
                int s = version.SymbolWidth;
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
                        symbology: BarcodeSymbology.DataMatrix,
                        rawBytes: res.RawBytes,
                        cornerPoints: corners,
                        confidence: 1.0);
                }
            }

            return null;
        }

        private static List<Point> OrderQuadPoints(List<Point> pts)
        {
            // Center of mass
            float cx = 0, cy = 0;
            for (int i = 0; i < pts.Count; i++) { cx += pts[i].X; cy += pts[i].Y; }
            cx /= pts.Count; cy /= pts.Count;

            // Sort by angle around center
            var sorted = new List<Point>(pts);
            sorted.Sort((p1, p2) =>
            {
                double a1 = Math.Atan2(p1.Y - cy, p1.X - cx);
                double a2 = Math.Atan2(p2.Y - cy, p2.X - cx);
                return a1.CompareTo(a2);
            });

            // Find top-left (min X+Y)
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

        private static bool CheckFinderPattern(bool[,] grid, int width, int height)
        {
            // Left column must be solid (mostly 1s)
            int leftOnes = 0;
            for (int r = 0; r < height; r++)
            {
                if (grid[r, 0]) leftOnes++;
            }

            // Bottom row must be solid (mostly 1s)
            int bottomOnes = 0;
            for (int c = 0; c < width; c++)
            {
                if (grid[height - 1, c]) bottomOnes++;
            }

            // Allow up to 1 error per border
            return leftOnes >= height - 1 && bottomOnes >= width - 1;
        }

        private static bool[,] ExtractDataRegion(bool[,] fullSymbol, DataMatrixVersion version)
        {
            bool[,] dataRegion = new bool[version.DataRows, version.DataColumns];
            for (int r = 0; r < version.DataRows; r++)
            {
                for (int c = 0; c < version.DataColumns; c++)
                {
                    // Skip top row (border 0) and left column (border 0)
                    dataRegion[r, c] = fullSymbol[r + 1, c + 1];
                }
            }
            return dataRegion;
        }

        private static bool[,] RotateGrid(bool[,] grid, int rotationSteps)
        {
            int steps = (rotationSteps % 4 + 4) % 4;
            if (steps == 0) return grid;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            switch (steps)
            {
                case 1: // 90 deg clockwise
                {
                    bool[,] res = new bool[w, h];
                    for (int r = 0; r < h; r++)
                        for (int c = 0; c < w; c++)
                            res[c, h - 1 - r] = grid[r, c];
                    return res;
                }
                case 2: // 180 deg
                {
                    bool[,] res = new bool[h, w];
                    for (int r = 0; r < h; r++)
                        for (int c = 0; c < w; c++)
                            res[h - 1 - r, w - 1 - c] = grid[r, c];
                    return res;
                }
                case 3: // 270 deg clockwise
                {
                    bool[,] res = new bool[w, h];
                    for (int r = 0; r < h; r++)
                        for (int c = 0; c < w; c++)
                            res[w - 1 - c, r] = grid[r, c];
                    return res;
                }
                default:
                    return grid;
            }
        }
    }
}
