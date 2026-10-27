using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class Barcode1DTests
    {
        [Fact]
        public void TestCode128_SetB_AlphanumericDecoding()
        {
            string expectedPayload = "PART-98741";
            using (var img = GenerateCode128BImage(expectedPayload, moduleWidth: 2, height: 60))
            {
                var result = BarcodeReader1D.Decode(img, BarcodeSymbology.Code128);
                Assert.NotNull(result);
                Assert.Equal(BarcodeSymbology.Code128, result!.Symbology);
                Assert.Equal(expectedPayload, result.Text);
                Assert.Equal(1.0, result.Confidence);
            }
        }

        [Fact]
        public void TestCode128_SetC_NumericDecoding()
        {
            string expectedPayload = "12345678";
            using (var img = GenerateCode128CImage(expectedPayload, moduleWidth: 3, height: 50))
            {
                var result = BarcodeReader1D.Decode(img, BarcodeSymbology.Code128);
                Assert.NotNull(result);
                Assert.Equal(BarcodeSymbology.Code128, result!.Symbology);
                Assert.Equal(expectedPayload, result.Text);
            }
        }

        [Fact]
        public void TestCode39_AlphanumericDecoding()
        {
            string expectedPayload = "MOTOR-042";
            using (var img = GenerateCode39Image(expectedPayload, narrowWidth: 2, wideWidth: 5, height: 60))
            {
                var result = BarcodeReader1D.Decode(img, BarcodeSymbology.Code39);
                Assert.NotNull(result);
                Assert.Equal(BarcodeSymbology.Code39, result!.Symbology);
                Assert.Equal(expectedPayload, result.Text);
                Assert.Equal(1.0, result.Confidence);
            }
        }

        [Fact]
        public unsafe void TestBarcodeReader1D_Inverted180Degrees()
        {
            string expectedPayload = "MLG-7721";
            using (var normalImg = GenerateCode128BImage(expectedPayload, moduleWidth: 2, height: 60))
            {
                // Flip horizontally (180° inversion)
                using (var flippedImg = ImageBuffer.CreateGray8(normalImg.Width, normalImg.Height))
                {
                    for (int y = 0; y < normalImg.Height; y++)
                    {
                        byte* srcRow = normalImg.GetRowPointer(y);
                        byte* dstRow = flippedImg.GetRowPointer(y);
                        for (int x = 0; x < normalImg.Width; x++)
                        {
                            dstRow[normalImg.Width - 1 - x] = srcRow[x];
                        }
                    }

                    var result = BarcodeReader1D.Decode(flippedImg);
                    Assert.NotNull(result);
                    Assert.Equal(expectedPayload, result!.Text);
                }
            }
        }

        [Fact]
        public unsafe void TestBarcodeReader1D_VerticalOrientation90Degrees()
        {
            string expectedPayload = "PCB-ROT-90";
            using (var normalImg = GenerateCode128BImage(expectedPayload, moduleWidth: 2, height: 60))
            {
                // Transpose: width becomes height, height becomes width
                using (var verticalImg = ImageBuffer.CreateGray8(normalImg.Height, normalImg.Width))
                {
                    for (int y = 0; y < normalImg.Height; y++)
                    {
                        byte* srcRow = normalImg.GetRowPointer(y);
                        for (int x = 0; x < normalImg.Width; x++)
                        {
                            verticalImg.GetRowPointer(x)[y] = srcRow[x];
                        }
                    }

                    var result = BarcodeReader1D.Decode(verticalImg);
                    Assert.NotNull(result);
                    Assert.Equal(expectedPayload, result!.Text);
                }
            }
        }

        [Fact]
        public void TestBarcodeReader1D_BlankOrNoiseRejection()
        {
            using (var blankImg = ImageBuffer.CreateGray8(200, 60))
            {
                blankImg.Clear(200); // uniform gray
                var result = BarcodeReader1D.Decode(blankImg);
                Assert.Null(result);
            }
        }

        // =====================================================================
        // Synthetic Barcode Generators
        // =====================================================================

        private static unsafe ImageBuffer GenerateCode128BImage(string text, int moduleWidth, int height)
        {
            var symbols = new List<int> { Code128Decoder.StartB };
            int checksum = Code128Decoder.StartB;

            for (int i = 0; i < text.Length; i++)
            {
                int code = text[i] - 32;
                symbols.Add(code);
                checksum += (i + 1) * code;
            }

            symbols.Add(checksum % 103);
            symbols.Add(Code128Decoder.StopPattern);

            // Compute total module width
            int totalModules = 10 + 10; // Left and right quiet zones (10 modules each)
            foreach (var code in symbols)
            {
                byte[] pat = Code128Decoder.GetPattern(code);
                foreach (var w in pat) totalModules += w;
            }

            int imgWidth = totalModules * moduleWidth;
            var img = ImageBuffer.CreateGray8(imgWidth, height);
            img.Clear(240); // White background

            int curModule = 10; // start after quiet zone
            foreach (var code in symbols)
            {
                byte[] pat = Code128Decoder.GetPattern(code);
                bool isBlack = true;
                foreach (var w in pat)
                {
                    if (isBlack)
                    {
                        int startX = curModule * moduleWidth;
                        int endX = (curModule + w) * moduleWidth;
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = img.GetRowPointer(y);
                            for (int x = startX; x < endX; x++) row[x] = 20; // Black bar
                        }
                    }
                    curModule += w;
                    isBlack = !isBlack;
                }
            }

            return img;
        }

        private static unsafe ImageBuffer GenerateCode128CImage(string digits, int moduleWidth, int height)
        {
            if (digits.Length % 2 != 0)
                throw new ArgumentException("Digits must have an even length for Code 128 Set C.");

            var symbols = new List<int> { Code128Decoder.StartC };
            int checksum = Code128Decoder.StartC;

            int weight = 1;
            for (int i = 0; i < digits.Length; i += 2)
            {
                int code = int.Parse(digits.Substring(i, 2));
                symbols.Add(code);
                checksum += weight * code;
                weight++;
            }

            symbols.Add(checksum % 103);
            symbols.Add(Code128Decoder.StopPattern);

            int totalModules = 20;
            foreach (var code in symbols)
            {
                byte[] pat = Code128Decoder.GetPattern(code);
                foreach (var w in pat) totalModules += w;
            }

            int imgWidth = totalModules * moduleWidth;
            var img = ImageBuffer.CreateGray8(imgWidth, height);
            img.Clear(240);

            int curModule = 10;
            foreach (var code in symbols)
            {
                byte[] pat = Code128Decoder.GetPattern(code);
                bool isBlack = true;
                foreach (var w in pat)
                {
                    if (isBlack)
                    {
                        int startX = curModule * moduleWidth;
                        int endX = (curModule + w) * moduleWidth;
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = img.GetRowPointer(y);
                            for (int x = startX; x < endX; x++) row[x] = 20;
                        }
                    }
                    curModule += w;
                    isBlack = !isBlack;
                }
            }

            return img;
        }

        private static unsafe ImageBuffer GenerateCode39Image(string text, int narrowWidth, int wideWidth, int height)
        {
            string fullPayload = "*" + text.ToUpperInvariant() + "*";

            // Compute total pixel width
            int totalPixelWidth = 20 * narrowWidth; // quiet zones
            for (int i = 0; i < fullPayload.Length; i++)
            {
                if (!Code39Decoder.TryGetPattern(fullPayload[i], out int pat))
                    throw new ArgumentException($"Invalid Code 39 character: {fullPayload[i]}");

                for (int b = 8; b >= 0; b--)
                {
                    bool isWide = ((pat >> b) & 1) == 1;
                    totalPixelWidth += isWide ? wideWidth : narrowWidth;
                }

                if (i < fullPayload.Length - 1)
                {
                    totalPixelWidth += narrowWidth; // Inter-character space gap
                }
            }

            var img = ImageBuffer.CreateGray8(totalPixelWidth, height);
            img.Clear(240);

            int currentX = 10 * narrowWidth; // Start after quiet zone
            for (int i = 0; i < fullPayload.Length; i++)
            {
                Code39Decoder.TryGetPattern(fullPayload[i], out int pat);
                bool isBar = true;

                for (int b = 8; b >= 0; b--)
                {
                    bool isWide = ((pat >> b) & 1) == 1;
                    int w = isWide ? wideWidth : narrowWidth;

                    if (isBar)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = img.GetRowPointer(y);
                            for (int x = currentX; x < currentX + w; x++) row[x] = 20;
                        }
                    }

                    currentX += w;
                    isBar = !isBar;
                }

                if (i < fullPayload.Length - 1)
                {
                    currentX += narrowWidth; // Inter-character gap
                }
            }

            return img;
        }
    }
}
