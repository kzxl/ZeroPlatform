using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Configuration options for the Universal Barcode & 2D Matrix reader.
    /// </summary>
    public sealed class UniversalReaderOptions
    {
        /// <summary>
        /// Expected symbology, or Unknown to scan all supported 1D and 2D codes.
        /// </summary>
        public BarcodeSymbology ExpectedSymbology { get; set; } = BarcodeSymbology.Unknown;

        public bool Scan1D { get; set; } = true;
        public bool ScanQr { get; set; } = true;
        public bool ScanDataMatrix { get; set; } = true;
        public bool EnableMultiAngle1D { get; set; } = true;
        public bool EnableClaheFallback { get; set; } = true;
        public double ScanlineStepFraction { get; set; } = 0.12;

        public static UniversalReaderOptions Default => new UniversalReaderOptions();
    }

    /// <summary>
    /// Unified, zero-external-dependency industrial barcode engine.
    /// Simultaneously detects, localizes, and decodes 1D barcodes (Code 128, Code 39, EAN-13, UPC-A, ITF-14)
    /// and 2D matrix codes (QR Code Model 2, DataMatrix ECC200) within a single unified API.
    /// </summary>
    public static class UniversalBarcodeReader
    {
        /// <summary>
        /// Scans an image buffer and returns the first valid barcode or 2D code found.
        /// </summary>
        public static BarcodeResult? Decode(ImageBuffer image, UniversalReaderOptions? options = null)
        {
            if (image == null) return null;
            options ??= UniversalReaderOptions.Default;

            var expected = options.ExpectedSymbology;

            // 1. QR Code
            if (options.ScanQr && (expected == BarcodeSymbology.Unknown || expected == BarcodeSymbology.QrCode))
            {
                var qrDecoder = new QrDecoder();
                var qrResult = qrDecoder.Decode(image);
                if (qrResult != null) return qrResult;
            }

            // 2. DataMatrix
            if (options.ScanDataMatrix && (expected == BarcodeSymbology.Unknown || expected == BarcodeSymbology.DataMatrix))
            {
                var dmDecoder = new DataMatrixDecoder();
                var dmResult = dmDecoder.Decode(image);
                if (dmResult != null) return dmResult;
            }

            // 3. 1D Barcodes (Code 128, Code 39, EAN-13, UPC-A, ITF-14)
            if (options.Scan1D && (expected == BarcodeSymbology.Unknown ||
                                  expected == BarcodeSymbology.Code128 ||
                                  expected == BarcodeSymbology.Code39 ||
                                  expected == BarcodeSymbology.Ean13 ||
                                  expected == BarcodeSymbology.UpcA ||
                                  expected == BarcodeSymbology.Itf14))
            {
                var barcodeResult = BarcodeReader1D.Decode(
                    image,
                    expected,
                    options.ScanlineStepFraction,
                    options.EnableMultiAngle1D);

                if (barcodeResult != null) return barcodeResult;
            }

            // 4. Fallback: Contrast Enhancement (Linear Stretch & CLAHE) for dim/overexposed surfaces
            if (options.EnableClaheFallback)
            {
                var noFallbackOptions = new UniversalReaderOptions
                {
                    ExpectedSymbology = options.ExpectedSymbology,
                    Scan1D = options.Scan1D,
                    ScanQr = options.ScanQr,
                    ScanDataMatrix = options.ScanDataMatrix,
                    EnableMultiAngle1D = options.EnableMultiAngle1D,
                    EnableClaheFallback = false,
                    ScanlineStepFraction = options.ScanlineStepFraction
                };

                using var gray = ImageBuffer.CreateGray8(image.Width, image.Height);

                if (image.Format == ImageFormatMode.Gray8)
                    image.CopyTo(gray);
                else
                    ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(image, gray);

                // Stage 4A: Fast Global Contrast Stretch
                ZeroGraphics.Imaging.Filters.ClaheFilter.StretchContrast(gray, gray);
                var enhancedResult = Decode(gray, noFallbackOptions);
                if (enhancedResult != null) return enhancedResult;

                // Stage 4B: Local Adaptive CLAHE
                ZeroGraphics.Imaging.Filters.ClaheFilter.Apply(gray, gray, clipLimit: 2.5f, tilesX: 4, tilesY: 4);
                enhancedResult = Decode(gray, noFallbackOptions);
                if (enhancedResult != null) return enhancedResult;
            }

            return null;
        }

        /// <summary>
        /// Scans an image buffer and returns ALL valid 1D and 2D barcodes present on the surface.
        /// Performs automated de-duplication across overlapping scan passes.
        /// </summary>
        public static List<BarcodeResult> DecodeAll(ImageBuffer image, UniversalReaderOptions? options = null)
        {
            var results = new List<BarcodeResult>();
            if (image == null) return results;

            options ??= UniversalReaderOptions.Default;
            var expected = options.ExpectedSymbology;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // 1. Scan QR Code
            if (options.ScanQr && (expected == BarcodeSymbology.Unknown || expected == BarcodeSymbology.QrCode))
            {
                var qrDecoder = new QrDecoder();
                var qrResult = qrDecoder.Decode(image);
                if (qrResult != null && seen.Add($"{qrResult.Symbology}:{qrResult.Text}"))
                {
                    results.Add(qrResult);
                }
            }

            // 2. Scan DataMatrix
            if (options.ScanDataMatrix && (expected == BarcodeSymbology.Unknown || expected == BarcodeSymbology.DataMatrix))
            {
                var dmDecoder = new DataMatrixDecoder();
                var dmResult = dmDecoder.Decode(image);
                if (dmResult != null && seen.Add($"{dmResult.Symbology}:{dmResult.Text}"))
                {
                    results.Add(dmResult);
                }
            }

            // 3. Scan 1D Barcodes
            if (options.Scan1D && (expected == BarcodeSymbology.Unknown ||
                                  expected == BarcodeSymbology.Code128 ||
                                  expected == BarcodeSymbology.Code39 ||
                                  expected == BarcodeSymbology.Ean13 ||
                                  expected == BarcodeSymbology.UpcA ||
                                  expected == BarcodeSymbology.Itf14))
            {
                var barcodeResult = BarcodeReader1D.Decode(
                    image,
                    expected,
                    options.ScanlineStepFraction,
                    options.EnableMultiAngle1D);

                if (barcodeResult != null && seen.Add($"{barcodeResult.Symbology}:{barcodeResult.Text}"))
                {
                    results.Add(barcodeResult);
                }
            }

            return results;
        }
    }
}
