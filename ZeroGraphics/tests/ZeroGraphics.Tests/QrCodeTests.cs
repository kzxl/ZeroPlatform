using System;
using System.Text;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class QrCodeTests
    {
        [Fact]
        public void TestQrGaloisField_ArithmeticAndInversion()
        {
            var field = GenericGF.QrCode256;
            Assert.Equal(256, field.Size);
            Assert.Equal(0, field.GeneratorBase);

            // Inversion: a * a^-1 = 1 for all non-zero a
            for (int a = 1; a < 256; a++)
            {
                int inv = field.Inverse(a);
                Assert.Equal(1, field.Multiply(a, inv));
            }

            // Addition / Subtraction in GF(2^8) is XOR
            Assert.Equal(0, GenericGF.AddOrSubtract(100, 100));
            Assert.Equal(100 ^ 200, GenericGF.AddOrSubtract(100, 200));
        }

        [Fact]
        public void TestQrFormatInfo_BchEncodingAndHammingCorrection()
        {
            var ecLevels = new[] { QrErrorCorrectionLevel.L, QrErrorCorrectionLevel.M, QrErrorCorrectionLevel.Q, QrErrorCorrectionLevel.H };

            foreach (var ec in ecLevels)
            {
                for (int mask = 0; mask < 8; mask++)
                {
                    int encoded = QrFormatInfo.EncodeFormatInformation(ec, mask);

                    // 1. Exact decode
                    var exact = QrFormatInfo.DoDecode(encoded);
                    Assert.NotNull(exact);
                    Assert.Equal(ec, exact.ErrorCorrectionLevel);
                    Assert.Equal(mask, exact.DataMask);

                    // 2. Corrupt 1 bit (test all 15 bit positions)
                    for (int bit = 0; bit < 15; bit++)
                    {
                        int corrupted1 = encoded ^ (1 << bit);
                        var decoded1 = QrFormatInfo.DoDecode(corrupted1);
                        Assert.NotNull(decoded1);
                        Assert.Equal(ec, decoded1.ErrorCorrectionLevel);
                        Assert.Equal(mask, decoded1.DataMask);
                    }

                    // 3. Corrupt 2 bits
                    int corrupted2 = encoded ^ (1 << 2) ^ (1 << 7);
                    var decoded2 = QrFormatInfo.DoDecode(corrupted2);
                    Assert.NotNull(decoded2);
                    Assert.Equal(ec, decoded2.ErrorCorrectionLevel);
                    Assert.Equal(mask, decoded2.DataMask);

                    // 4. Corrupt 3 bits (BCH 15,5 maximum tolerance)
                    int corrupted3 = encoded ^ (1 << 0) ^ (1 << 5) ^ (1 << 11);
                    var decoded3 = QrFormatInfo.DoDecode(corrupted3);
                    Assert.NotNull(decoded3);
                    Assert.Equal(ec, decoded3.ErrorCorrectionLevel);
                    Assert.Equal(mask, decoded3.DataMask);
                }
            }
        }

        [Fact]
        public void TestQrBitParser_FunctionPatternsAndMasking()
        {
            var v1 = QrVersion.Version1;
            bool[,] funcV1 = QrBitParser.BuildFunctionPattern(v1);

            // Finder patterns: (0,0), (0, 14), (14, 0)
            Assert.True(funcV1[0, 0]);
            Assert.True(funcV1[6, 6]);
            Assert.True(funcV1[0, 20]);
            Assert.True(funcV1[20, 0]);

            // Timing tracks
            Assert.True(funcV1[6, 8]);
            Assert.True(funcV1[6, 12]);
            Assert.True(funcV1[8, 6]);
            Assert.True(funcV1[12, 6]);

            // Format info coordinates
            Assert.True(funcV1[8, 0]);
            Assert.True(funcV1[8, 8]);
            Assert.True(funcV1[0, 8]);

            // Alignment pattern for Version 2 (at center 18, 18)
            var v2 = QrVersion.Version2;
            bool[,] funcV2 = QrBitParser.BuildFunctionPattern(v2);
            Assert.True(funcV2[18, 18]);
            Assert.True(funcV2[16, 16]); // alignment corner
            Assert.True(funcV2[20, 20]);
        }

        [Fact]
        public void TestQrCode_NumericRoundTrip()
        {
            string payload = "0123456789";
            var decoder = new QrDecoder();

            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.M);
            var result = decoder.DecodeSymbolGrid(grid);

            Assert.NotNull(result);
            Assert.Equal(payload, result.Text);
            Assert.Equal(BarcodeSymbology.QrCode, result.Symbology);
        }

        [Fact]
        public void TestQrCode_AlphanumericRoundTrip()
        {
            string payload = "ZERO-PLATFORM 2026";
            var decoder = new QrDecoder();

            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.Q);
            var result = decoder.DecodeSymbolGrid(grid);

            Assert.NotNull(result);
            Assert.Equal(payload, result.Text);
            Assert.Equal(BarcodeSymbology.QrCode, result.Symbology);
        }

        [Fact]
        public void TestQrCode_ByteModeRoundTrip()
        {
            string payload = "https://github.com/zeroplatform/traceability/LOT-9876";
            var decoder = new QrDecoder();

            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.L);
            var result = decoder.DecodeSymbolGrid(grid);

            Assert.NotNull(result);
            Assert.Equal(payload, result.Text);
            Assert.Equal(BarcodeSymbology.QrCode, result.Symbology);
        }

        [Fact]
        public void TestQrCode_MultiVersionAndEcLevels()
        {
            var decoder = new QrDecoder();

            // Version 1, Level H
            string textV1 = "TEST-V1-H";
            bool[,] gridV1 = QrEncoder.EncodeSymbol(textV1, QrErrorCorrectionLevel.H, forcedVersion: QrVersion.Version1);
            Assert.Equal(21, gridV1.GetLength(0));
            var resV1 = decoder.DecodeSymbolGrid(gridV1);
            Assert.NotNull(resV1);
            Assert.Equal(textV1, resV1.Text);

            // Version 2, Level M
            string textV2 = "ZERO-GRAPHICS-VERSION-2";
            bool[,] gridV2 = QrEncoder.EncodeSymbol(textV2, QrErrorCorrectionLevel.M, forcedVersion: QrVersion.Version2);
            Assert.Equal(25, gridV2.GetLength(0));
            var resV2 = decoder.DecodeSymbolGrid(gridV2);
            Assert.NotNull(resV2);
            Assert.Equal(textV2, resV2.Text);

            // Version 3, Level Q
            string textV3 = "BATCH-SERIAL-INSPECTION-PASS-V3";
            bool[,] gridV3 = QrEncoder.EncodeSymbol(textV3, QrErrorCorrectionLevel.Q, forcedVersion: QrVersion.Version3);
            Assert.Equal(29, gridV3.GetLength(0));
            var resV3 = decoder.DecodeSymbolGrid(gridV3);
            Assert.NotNull(resV3);
            Assert.Equal(textV3, resV3.Text);
        }

        [Fact]
        public void TestQrCode_Rotations()
        {
            string payload = "ROTATED-QR-CODE-TEST";
            var decoder = new QrDecoder();
            bool[,] baseGrid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.M);

            // Test 90, 180, 270 degree manual pre-rotations
            int s = baseGrid.GetLength(0);

            // 90 deg clockwise
            bool[,] rot90 = new bool[s, s];
            for (int r = 0; r < s; r++)
                for (int c = 0; c < s; c++)
                    rot90[c, s - 1 - r] = baseGrid[r, c];
            var res90 = decoder.DecodeSymbolGrid(rot90);
            Assert.NotNull(res90);
            Assert.Equal(payload, res90.Text);

            // 180 deg
            bool[,] rot180 = new bool[s, s];
            for (int r = 0; r < s; r++)
                for (int c = 0; c < s; c++)
                    rot180[s - 1 - r, s - 1 - c] = baseGrid[r, c];
            var res180 = decoder.DecodeSymbolGrid(rot180);
            Assert.NotNull(res180);
            Assert.Equal(payload, res180.Text);

            // 270 deg
            bool[,] rot270 = new bool[s, s];
            for (int r = 0; r < s; r++)
                for (int c = 0; c < s; c++)
                    rot270[s - 1 - c, r] = baseGrid[r, c];
            var res270 = decoder.DecodeSymbolGrid(rot270);
            Assert.NotNull(res270);
            Assert.Equal(payload, res270.Text);
        }

        [Fact]
        public void TestQrCode_ReedSolomonErrorCorrection_DamagedGrid()
        {
            string payload = "RESILIENT-TRACEABILITY-CODE";
            var decoder = new QrDecoder();

            // High error correction level (Level H recovers ~30% damaged modules)
            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.H, forcedVersion: QrVersion.Version3);
            int dim = grid.GetLength(0);

            // Intentionally corrupt a line of 6 modules in the data area (avoiding finders)
            for (int c = 10; c < 16; c++)
            {
                grid[12, c] = !grid[12, c];
            }

            var result = decoder.DecodeSymbolGrid(grid);
            Assert.NotNull(result);
            Assert.Equal(payload, result.Text);
        }

        [Fact]
        public void TestQrCode_EndToEndOpticalImageDecoding()
        {
            string payload = "TRACE-ITEM-SN-445588";
            var decoder = new QrDecoder();

            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.M);

            // Render to Gray8 ImageBuffer with 6px modules and 4-module quiet zone
            using (var image = QrEncoder.RenderToImage(grid, modulePixelSize: 6, quietZone: 4))
            {
                Assert.NotNull(image);
                Assert.Equal(ImageFormatMode.Gray8, image.Format);

                var result = decoder.Decode(image);
                Assert.NotNull(result);
                Assert.Equal(payload, result.Text);
                Assert.Equal(BarcodeSymbology.QrCode, result.Symbology);
                Assert.Equal(4, result.CornerPoints.Length);
                Assert.True(result.Confidence > 0.9);
            }
        }

        [Fact]
        public void TestQrCode_OpticalImage_DifferentModuleScales()
        {
            string payload = "SCALE-TEST-1234";
            var decoder = new QrDecoder();

            bool[,] grid = QrEncoder.EncodeSymbol(payload, QrErrorCorrectionLevel.L);

            // Test small scale (3px module)
            using (var smallImg = QrEncoder.RenderToImage(grid, modulePixelSize: 3, quietZone: 4))
            {
                var resSmall = decoder.Decode(smallImg);
                Assert.NotNull(resSmall);
                Assert.Equal(payload, resSmall.Text);
            }

            // Test large scale (8px module)
            using (var largeImg = QrEncoder.RenderToImage(grid, modulePixelSize: 8, quietZone: 4))
            {
                var resLarge = decoder.Decode(largeImg);
                Assert.NotNull(resLarge);
                Assert.Equal(payload, resLarge.Text);
            }
        }
    }
}
