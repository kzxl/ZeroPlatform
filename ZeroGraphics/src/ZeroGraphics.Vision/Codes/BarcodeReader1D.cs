using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Multi-scanline and rotation-invariant 1D barcode scanner supporting horizontal, vertical,
    /// diagonal (45°/135°), and arbitrary angular passes for Code 128, Code 39, EAN-13, UPC-A, and ITF-14.
    /// </summary>
    public static class BarcodeReader1D
    {
        /// <summary>
        /// Attempts to decode a 1D barcode from an image buffer across multiple angles and scanlines.
        /// </summary>
        /// <param name="image">Image buffer (monochrome or color).</param>
        /// <param name="expectedSymbology">Expected symbology, or Unknown to auto-detect all supported symbologies.</param>
        /// <param name="scanlineStepFraction">Fraction of image dimensions between scanline passes.</param>
        /// <param name="enableMultiAngle">If true, scans diagonal and angular rays in addition to horizontal/vertical.</param>
        /// <returns>Decoded BarcodeResult, or null if no valid barcode was found.</returns>
        public static BarcodeResult? Decode(
            ImageBuffer image,
            BarcodeSymbology expectedSymbology = BarcodeSymbology.Unknown,
            double scanlineStepFraction = 0.12,
            bool enableMultiAngle = true)
        {
            if (image == null || image.Width < 20 || image.Height < 20)
                return null;

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
                int w = grayImage.Width;
                int h = grayImage.Height;

                int stepH = Math.Max(2, (int)(h * scanlineStepFraction));
                int stepW = Math.Max(2, (int)(w * scanlineStepFraction));

                // 1. Horizontal scanlines (0° and 180°)
                for (int y = stepH; y < h; y += stepH)
                {
                    if (BarcodeScanline.ExtractHorizontalRuns(grayImage, y, out var runs, out bool firstIsBlack))
                    {
                        var ptStart = new PointF(0, y);
                        var ptEnd = new PointF(w - 1, y);

                        var result = TryDecodeRuns(runs, firstIsBlack, expectedSymbology, ptStart, ptEnd);
                        if (result != null) return result;

                        var reversed = ReverseRuns(runs);
                        bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                        result = TryDecodeRuns(reversed, lastIsBlack, expectedSymbology, ptEnd, ptStart);
                        if (result != null) return result;
                    }
                }

                // 2. Vertical scanlines (90° and 270°)
                for (int x = stepW; x < w; x += stepW)
                {
                    if (BarcodeScanline.ExtractVerticalRuns(grayImage, x, out var runs, out bool firstIsBlack))
                    {
                        var ptStart = new PointF(x, 0);
                        var ptEnd = new PointF(x, h - 1);

                        var result = TryDecodeRuns(runs, firstIsBlack, expectedSymbology, ptStart, ptEnd);
                        if (result != null) return result;

                        var reversed = ReverseRuns(runs);
                        bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                        result = TryDecodeRuns(reversed, lastIsBlack, expectedSymbology, ptEnd, ptStart);
                        if (result != null) return result;
                    }
                }

                // 3. Multi-angle Diagonal & Angular passes (Rotation Invariance)
                if (enableMultiAngle)
                {
                    // Diagonals (Top-Left to Bottom-Right, 45°)
                    int diagStep = Math.Max(20, Math.Min(stepW, stepH));

                    for (int offset = -h + diagStep; offset < w; offset += diagStep)
                    {
                        float x0 = Math.Max(0, offset);
                        float y0 = Math.Max(0, -offset);
                        float x1 = Math.Min(w - 1, offset + h - 1);
                        float y1 = Math.Min(h - 1, -offset + (x1 - x0));

                        if (Math.Abs(x1 - x0) >= 30 && Math.Abs(y1 - y0) >= 30)
                        {
                            if (BarcodeScanline.ExtractRayRuns(grayImage, x0, y0, x1, y1, out var runs, out bool firstIsBlack))
                            {
                                var ptStart = new PointF(x0, y0);
                                var ptEnd = new PointF(x1, y1);

                                var result = TryDecodeRuns(runs, firstIsBlack, expectedSymbology, ptStart, ptEnd);
                                if (result != null) return result;

                                var reversed = ReverseRuns(runs);
                                bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                                result = TryDecodeRuns(reversed, lastIsBlack, expectedSymbology, ptEnd, ptStart);
                                if (result != null) return result;
                            }
                        }
                    }

                    // Anti-Diagonals (Bottom-Left to Top-Right, 135°)
                    for (int offset = diagStep; offset < w + h - diagStep; offset += diagStep)
                    {
                        float x0 = Math.Max(0, offset - (h - 1));
                        float y0 = Math.Min(h - 1, offset);
                        float x1 = Math.Min(w - 1, offset);
                        float y1 = Math.Max(0, offset - (w - 1));

                        if (Math.Abs(x1 - x0) >= 30 && Math.Abs(y1 - y0) >= 30)
                        {
                            if (BarcodeScanline.ExtractRayRuns(grayImage, x0, y0, x1, y1, out var runs, out bool firstIsBlack))
                            {
                                var ptStart = new PointF(x0, y0);
                                var ptEnd = new PointF(x1, y1);

                                var result = TryDecodeRuns(runs, firstIsBlack, expectedSymbology, ptStart, ptEnd);
                                if (result != null) return result;

                                var reversed = ReverseRuns(runs);
                                bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                                result = TryDecodeRuns(reversed, lastIsBlack, expectedSymbology, ptEnd, ptStart);
                                if (result != null) return result;
                            }
                        }
                    }
                }

                return null;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        private static BarcodeResult? TryDecodeRuns(
            List<int> runs,
            bool firstIsBlack,
            BarcodeSymbology expectedSymbology,
            PointF ptStart,
            PointF ptEnd)
        {
            var points = new PointF[] { ptStart, ptEnd };

            // 1. Code 128
            if (expectedSymbology == BarcodeSymbology.Unknown || expectedSymbology == BarcodeSymbology.Code128)
            {
                if (Code128Decoder.TryDecode(runs, firstIsBlack, out string text128, out double conf128))
                {
                    return new BarcodeResult(
                        text: text128,
                        symbology: BarcodeSymbology.Code128,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(text128),
                        cornerPoints: points,
                        confidence: conf128);
                }
            }

            // 2. EAN-13 & UPC-A
            if (expectedSymbology == BarcodeSymbology.Unknown ||
                expectedSymbology == BarcodeSymbology.Ean13 ||
                expectedSymbology == BarcodeSymbology.UpcA)
            {
                if (Ean13Decoder.TryDecode(runs, firstIsBlack, out string textEan, out double confEan))
                {
                    var sym = (expectedSymbology == BarcodeSymbology.UpcA || (textEan.Length == 13 && textEan[0] == '0'))
                        ? BarcodeSymbology.UpcA
                        : BarcodeSymbology.Ean13;

                    string returnText = (sym == BarcodeSymbology.UpcA && textEan.Length == 13 && textEan[0] == '0')
                        ? textEan.Substring(1)
                        : textEan;

                    return new BarcodeResult(
                        text: returnText,
                        symbology: sym,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(returnText),
                        cornerPoints: points,
                        confidence: confEan);
                }
            }

            // 3. ITF-14 (Interleaved 2 of 5)
            if (expectedSymbology == BarcodeSymbology.Unknown || expectedSymbology == BarcodeSymbology.Itf14)
            {
                if (Itf14Decoder.TryDecode(runs, firstIsBlack, out string textItf, out double confItf))
                {
                    return new BarcodeResult(
                        text: textItf,
                        symbology: BarcodeSymbology.Itf14,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(textItf),
                        cornerPoints: points,
                        confidence: confItf);
                }
            }

            // 4. Code 39
            if (expectedSymbology == BarcodeSymbology.Unknown || expectedSymbology == BarcodeSymbology.Code39)
            {
                if (Code39Decoder.TryDecode(runs, firstIsBlack, out string text39, out double conf39))
                {
                    return new BarcodeResult(
                        text: text39,
                        symbology: BarcodeSymbology.Code39,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(text39),
                        cornerPoints: points,
                        confidence: conf39);
                }
            }

            return null;
        }

        private static List<int> ReverseRuns(List<int> original)
        {
            var rev = new List<int>(original.Count);
            for (int i = original.Count - 1; i >= 0; i--)
            {
                rev.Add(original[i]);
            }
            return rev;
        }
    }
}
