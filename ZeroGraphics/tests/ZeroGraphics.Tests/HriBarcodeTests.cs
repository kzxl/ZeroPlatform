using System;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class HriBarcodeTests
    {
        [Fact]
        public void Code128Encoder_GeneratesValidModules_AndRoundTripsWithDecoder()
        {
            string payload = "ZERO-128-TEST";
            bool[] modules = Code128Encoder.Encode(payload);
            Assert.NotEmpty(modules);

            // Construct an image buffer from the modules to test optical decoding
            int moduleWidth = 2;
            int height = 50;
            int quietZone = 10;
            bool[,] grid = Code128Encoder.EncodeSymbol(payload, isGs1: false, quietZoneModules: quietZone, height: height);

            using (var img = ImageBuffer.CreateGray8(grid.GetLength(1) * moduleWidth, height))
            {
                // Fill white background
                unsafe
                {
                    byte* scan = img.Scan0;
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = scan + (y * img.Stride);
                        for (int x = 0; x < img.Width; x++) row[x] = 255;
                    }

                    // Draw bars
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = scan + (y * img.Stride);
                        for (int gx = 0; gx < grid.GetLength(1); gx++)
                        {
                            if (grid[y, gx])
                            {
                                for (int mx = 0; mx < moduleWidth; mx++)
                                    row[(gx * moduleWidth) + mx] = 0;
                            }
                        }
                    }
                }

                var decoded = BarcodeReader1D.Decode(img, BarcodeSymbology.Code128);
                Assert.NotNull(decoded);
                Assert.Equal(payload, decoded.Text);
                Assert.Equal(BarcodeSymbology.Code128, decoded.Symbology);
            }
        }

        [Fact]
        public void Code128Encoder_AutoSwitchToSetC_ForDigits()
        {
            string digits = "12345678";
            var symbols = Code128Encoder.GetSymbolValues(digits);

            // Start C should be chosen
            Assert.Equal(Code128Encoder.CodeStartC, symbols[0]);
            Assert.Equal(12, symbols[1]);
            Assert.Equal(34, symbols[2]);
            Assert.Equal(56, symbols[3]);
            Assert.Equal(78, symbols[4]);
            Assert.Equal(Code128Encoder.CodeStop, symbols[symbols.Count - 1]);
        }

        [Fact]
        public void Gs1HriFormatter_ParsesAndFormats_Correctly()
        {
            string input = "(01)08801234567891(17)260921(10)LOT789";
            var elements = Gs1HriFormatter.Parse(input);

            Assert.Equal(3, elements.Count);
            Assert.Equal("01", elements[0].Ai);
            Assert.Equal("08801234567891", elements[0].Data);
            Assert.Equal("GTIN", elements[0].Title);

            Assert.Equal("17", elements[1].Ai);
            Assert.Equal("260921", elements[1].Data);

            Assert.Equal("10", elements[2].Ai);
            Assert.Equal("LOT789", elements[2].Data);

            string formatted = Gs1HriFormatter.FormatHri(input);
            Assert.Equal("(01) 08801234567891 (17) 260921 (10) LOT789", formatted);

            string payload = Gs1HriFormatter.ToBarcodePayload(input);
            Assert.StartsWith("01088012345678911726092110LOT789", payload);
        }

        [Fact]
        public void Gs1HriFormatter_ParsesRawUnbracketedString()
        {
            string raw = "01088012345678911726092110LOT123";
            string formatted = Gs1HriFormatter.FormatHri(raw);

            Assert.Contains("(01) 08801234567891", formatted);
            Assert.Contains("(17) 260921", formatted);
            Assert.Contains("(10) LOT123", formatted);
        }

        [Fact]
        public void HriLayoutEngine_ComputesEanGrouped_AndGeneralLayout()
        {
            var options = new HriOptions
            {
                Alignment = HriAlignment.EanGrouped,
                ModuleWidth = 2,
                BarHeight = 50,
                QuietZoneModules = 10
            };

            var layout = HriLayoutEngine.ComputeLayout(BarcodeSymbology.Ean13, 95, "8801234567893", options);

            Assert.True(layout.TotalWidth > 95 * 2);
            Assert.True(layout.TotalHeight > 50);
            Assert.Equal(3, layout.TextSpans.Count);

            // Span 0: Lead digit "8"
            Assert.Equal("8", layout.TextSpans[0].Text);
            // Span 1: Left 6 digits "801234"
            Assert.Equal("801234", layout.TextSpans[1].Text);
            // Span 2: Right 6 digits "567893"
            Assert.Equal("567893", layout.TextSpans[2].Text);
        }

        [Fact]
        public void BarcodeCompositeRenderer_Ean13_RendersAndDecodesCorrectly()
        {
            string digits12 = "880123456789";
            using (var composite = BarcodeCompositeRenderer.RenderEan13(digits12))
            {
                Assert.NotNull(composite);
                Assert.True(composite.Width > 100);
                Assert.True(composite.Height > 60);

                // Optical verification through BarcodeReader1D
                var result = BarcodeReader1D.Decode(composite, BarcodeSymbology.Ean13);
                Assert.NotNull(result);
                Assert.Equal("8801234567893", result.Text);
                Assert.Equal(BarcodeSymbology.Ean13, result.Symbology);
            }
        }

        [Fact]
        public void BarcodeCompositeRenderer_Code128_RendersAndDecodesCorrectly()
        {
            string code = "INV-2026";
            using (var composite = BarcodeCompositeRenderer.RenderCode128(code))
            {
                Assert.NotNull(composite);
                Assert.True(composite.Width > 100);
                Assert.True(composite.Height > 60);

                var result = BarcodeReader1D.Decode(composite, BarcodeSymbology.Code128);
                Assert.NotNull(result);
                Assert.Equal(code, result.Text);
                Assert.Equal(BarcodeSymbology.Code128, result.Symbology);
            }
        }

        [Fact]
        public void BarcodeCompositeRenderer_Gs1_RendersValidImage()
        {
            string hri = "(01)08801234567891(10)LOT123";
            using (var composite = BarcodeCompositeRenderer.RenderGs1(hri))
            {
                Assert.NotNull(composite);
                Assert.True(composite.Width > 150);
                Assert.True(composite.Height > 60);
            }
        }
    }
}
