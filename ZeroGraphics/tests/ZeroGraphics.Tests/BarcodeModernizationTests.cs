using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class BarcodeModernizationTests
    {
        [Fact]
        public void Ean13_EncodeAndDecode_VietnamGS1_Roundtrip_Succeeds()
        {
            // GS1 Vietnam prefix 893 (as shown in user's industrial QR code payload: 8935217402737)
            string payload12 = "893521740273";
            int check = Ean13Encoder.CalculateChecksum(payload12);
            string payload13 = payload12 + check.ToString();
            Assert.Equal("8935217402731", payload13);

            // Generate 2D symbol bitmap
            bool[,] grid = Ean13Encoder.EncodeSymbol(payload13, quietZone: 15, height: 60);
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            using var image = ImageBuffer.CreateGray8(w, h);
            image.Clear(255); // White background

            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (grid[y, x]) row[x] = 0; // Black bar
                    }
                }
            }

            // Decode with auto-detection
            var result = BarcodeReader1D.Decode(image, BarcodeSymbology.Ean13);

            Assert.NotNull(result);
            Assert.Equal(BarcodeSymbology.Ean13, result.Symbology);
            Assert.Equal(payload13, result.Text);
            Assert.True(result.Confidence > 0.9);
        }

        [Fact]
        public void UpcA_EncodeAndDecode_Roundtrip_Succeeds()
        {
            // Standard UPC-A 12-digit code
            string upc11 = "01234567890";
            int check = Ean13Encoder.CalculateChecksum(upc11);
            string upc12 = upc11 + check.ToString();

            bool[,] grid = Ean13Encoder.EncodeSymbol(upc12, quietZone: 12, height: 50);
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            using var image = ImageBuffer.CreateGray8(w, h);
            image.Clear(255);

            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (grid[y, x]) row[x] = 0;
                    }
                }
            }

            var result = BarcodeReader1D.Decode(image, BarcodeSymbology.UpcA);

            Assert.NotNull(result);
            Assert.Equal(BarcodeSymbology.UpcA, result.Symbology);
            Assert.Equal(upc12, result.Text);
        }

        [Fact]
        public void Itf14_CartonShipping_EncodeAndDecode_Roundtrip_Succeeds()
        {
            // 14-digit master carton / shipping container code
            string itf13 = "1893521740273";
            int check = Itf14Encoder.CalculateChecksum(itf13);
            string itf14 = itf13 + check.ToString();

            bool[,] grid = Itf14Encoder.EncodeSymbol(itf14, quietZone: 25, height: 60, withBearerBars: true);
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            using var image = ImageBuffer.CreateGray8(w, h);
            image.Clear(255);

            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (grid[y, x]) row[x] = 0;
                    }
                }
            }

            var result = BarcodeReader1D.Decode(image, BarcodeSymbology.Itf14);

            Assert.NotNull(result);
            Assert.Equal(BarcodeSymbology.Itf14, result.Symbology);
            Assert.Equal(itf14, result.Text);
            Assert.True(result.Confidence > 0.9);
        }

        [Fact]
        public void MultiAngle_Diagonal_RayScan_RotatedCode_Succeeds()
        {
            // Create a diagonal barcode pattern oriented at 45 degrees
            int size = 200;
            using var image = ImageBuffer.CreateGray8(size, size);
            image.Clear(255); // White canvas

            // Draw alternating stripes along the main diagonal (slope +1)
            // A ray perpendicular (or along a scanline across the stripes) can be read
            float x0 = 10, y0 = 10;
            float x1 = 190, y1 = 190;

            unsafe
            {
                byte* scan0 = image.Scan0;
                int stride = image.Stride;

                // Draw perpendicular bars across diagonal
                for (int d = 20; d < 180; d += 8)
                {
                    // Draw a black bar of width 4 perpendicular to diagonal (x + y = const)
                    for (int w = 0; w < 4; w++)
                    {
                        int sum = (d + w) * 2;
                        for (int x = 0; x < size; x++)
                        {
                            int y = sum - x;
                            if (y >= 0 && y < size)
                            {
                                *(scan0 + y * stride + x) = 0;
                            }
                        }
                    }
                }
            }

            // Extract ray runs along diagonal
            bool ok = BarcodeScanline.ExtractRayRuns(image, x0, y0, x1, y1, out var runs, out bool firstIsBlack);

            Assert.True(ok);
            Assert.True(runs.Count >= 20, "Must detect multiple alternating bars/spaces along 45-degree ray.");
        }

        [Fact]
        public void UniversalBarcodeReader_AutoDetects_Ean13_And_QrCode()
        {
            // 1. Test EAN-13 detection through UniversalBarcodeReader
            string eanCode = "8935217402731";
            bool[,] eanGrid = Ean13Encoder.EncodeSymbol(eanCode, quietZone: 15, height: 50);
            using (var eanImg = ImageBuffer.CreateGray8(eanGrid.GetLength(1), eanGrid.GetLength(0)))
            {
                eanImg.Clear(255);
                unsafe
                {
                    for (int y = 0; y < eanImg.Height; y++)
                    {
                        byte* row = eanImg.GetRowPointer(y);
                        for (int x = 0; x < eanImg.Width; x++)
                            if (eanGrid[y, x]) row[x] = 0;
                    }
                }

                var resEan = UniversalBarcodeReader.Decode(eanImg);
                Assert.NotNull(resEan);
                Assert.Equal(BarcodeSymbology.Ean13, resEan.Symbology);
                Assert.Equal(eanCode, resEan.Text);
            }

            // 2. Test QR Code detection through UniversalBarcodeReader
            string qrPayload = "http://ndatrace.vn/02/8935217402737/02/hp9vm3mzoio";
            bool[,] qrGrid = QrEncoder.EncodeSymbol(qrPayload, QrErrorCorrectionLevel.M);
            int qrDim = qrGrid.GetLength(0);
            int mod = 6;
            int qz = 4;
            int total = (qrDim + qz * 2) * mod;

            using (var qrImg = ImageBuffer.CreateGray8(total, total))
            {
                qrImg.Clear(255);
                unsafe
                {
                    for (int r = 0; r < qrDim; r++)
                    {
                        for (int c = 0; c < qrDim; c++)
                        {
                            if (qrGrid[r, c])
                            {
                                int sx = (c + qz) * mod;
                                int sy = (r + qz) * mod;
                                for (int py = 0; py < mod; py++)
                                {
                                    byte* row = qrImg.GetRowPointer(sy + py);
                                    for (int px = 0; px < mod; px++) row[sx + px] = 0;
                                }
                            }
                        }
                    }
                }

                var resQr = UniversalBarcodeReader.Decode(qrImg);
                Assert.NotNull(resQr);
                Assert.Equal(BarcodeSymbology.QrCode, resQr.Symbology);
                Assert.Equal(qrPayload, resQr.Text);
            }
        }
    }
}
