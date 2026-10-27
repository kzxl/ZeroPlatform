using System;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class ClaheAndAntiGlareTests
    {
        [Fact]
        public void ClaheFilter_ExpandsNarrowDynamicRange_IncreasesContrast()
        {
            using var src = ImageBuffer.CreateGray8(128, 128);
            using var dst = ImageBuffer.CreateGray8(128, 128);

            // Create low-contrast image (narrow dynamic range: 80 to 100)
            unsafe
            {
                for (int y = 0; y < 128; y++)
                {
                    byte* row = src.GetRowPointer(y);
                    for (int x = 0; x < 128; x++)
                    {
                        row[x] = (byte)(80 + ((x + y) % 20));
                    }
                }
            }

            ClaheFilter.Apply(src, dst, clipLimit: 3.0f, tilesX: 4, tilesY: 4);

            // Measure dynamic range expansion
            byte minVal = 255;
            byte maxVal = 0;

            unsafe
            {
                for (int y = 0; y < 128; y++)
                {
                    byte* row = dst.GetRowPointer(y);
                    for (int x = 0; x < 128; x++)
                    {
                        byte v = row[x];
                        if (v < minVal) minVal = v;
                        if (v > maxVal) maxVal = v;
                    }
                }
            }

            // Contrast should be significantly stretched compared to original narrow delta of 20
            Assert.True(minVal <= 70, $"Expected lower dynamic range <= 70, got {minVal}");
            Assert.True(maxVal >= 130, $"Expected upper dynamic range >= 130, got {maxVal}");
            Assert.True((maxVal - minVal) >= 50, $"Expected contrast spread >= 50, got {maxVal - minVal}");
        }

        [Fact]
        public void ClaheFilter_StretchContrast_ExpandsToFullDynamicRange()
        {
            using var src = ImageBuffer.CreateGray8(64, 64);
            using var dst = ImageBuffer.CreateGray8(64, 64);

            unsafe
            {
                for (int y = 0; y < 64; y++)
                {
                    byte* row = src.GetRowPointer(y);
                    for (int x = 0; x < 64; x++)
                    {
                        row[x] = (byte)(80 + (x % 20)); // values 80 to 99
                    }
                }
            }

            ClaheFilter.StretchContrast(src, dst);

            byte minVal = 255;
            byte maxVal = 0;

            unsafe
            {
                for (int y = 0; y < 64; y++)
                {
                    byte* row = dst.GetRowPointer(y);
                    for (int x = 0; x < 64; x++)
                    {
                        byte v = row[x];
                        if (v < minVal) minVal = v;
                        if (v > maxVal) maxVal = v;
                    }
                }
            }

            Assert.Equal(0, minVal);
            Assert.Equal(255, maxVal);
        }

        [Fact]
        public void SpecularGlareReducer_SuppressesSaturatedHighlights_SmoothsGlareSpots()
        {
            using var src = ImageBuffer.CreateGray8(100, 100);
            using var dst = ImageBuffer.CreateGray8(100, 100);

            // Normal metal surface background ~ 70
            src.Clear(70);

            // Blinding glare spot from LED at center (radius 4, intensity 255)
            unsafe
            {
                for (int y = 46; y <= 54; y++)
                {
                    byte* row = src.GetRowPointer(y);
                    for (int x = 46; x <= 54; x++)
                    {
                        row[x] = 255;
                    }
                }
            }

            SpecularGlareReducer.SuppressGlare(src, dst, glareThreshold: 240, searchRadius: 6);

            // Verify center pixel intensity was toned down towards background
            unsafe
            {
                byte centerVal = dst.GetRowPointer(50)[50];
                Assert.True(centerVal < 150, $"Glare spot should be suppressed, got {centerVal}");
            }
        }

        [Fact]
        public void UniversalBarcodeReader_FallbackClahe_DecodesDimBarcode()
        {
            // Encode an EAN-13 symbol
            string payload = "8935217402731";
            bool[,] grid = Ean13Encoder.EncodeSymbol(payload, quietZone: 10, height: 40);

            int h = 40;
            int w = grid.GetLength(1);

            // Create image with low contrast (bars = 110, background = 135; delta = 25)
            using var dimImage = ImageBuffer.CreateGray8(w, h);
            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = dimImage.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        row[x] = grid[y, x] ? (byte)110 : (byte)135;
                    }
                }
            }

            // Decode with CLAHE fallback enabled
            var options = new UniversalReaderOptions
            {
                ExpectedSymbology = BarcodeSymbology.Ean13,
                EnableClaheFallback = true
            };

            var result = UniversalBarcodeReader.Decode(dimImage, options);

            Assert.NotNull(result);
            Assert.Equal(payload, result.Text);
            Assert.Equal(BarcodeSymbology.Ean13, result.Symbology);
        }
    }
}
