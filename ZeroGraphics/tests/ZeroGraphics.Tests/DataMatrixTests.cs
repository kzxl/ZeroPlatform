using System;
using System.Text;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class DataMatrixTests
    {
        [Fact]
        public void TestGaloisField_ArithmeticAndInversion()
        {
            var field = GenericGF.DataMatrix256;
            Assert.Equal(256, field.Size);
            Assert.Equal(1, field.GeneratorBase);

            // Addition / Subtraction in GF(2^8) is XOR
            Assert.Equal(0, GenericGF.AddOrSubtract(42, 42));
            Assert.Equal(42 ^ 55, GenericGF.AddOrSubtract(42, 55));

            // Multiplication identity
            Assert.Equal(123, field.Multiply(123, 1));
            Assert.Equal(0, field.Multiply(123, 0));

            // Inversion: a * a^-1 = 1 for all non-zero a
            for (int a = 1; a < 256; a++)
            {
                int inv = field.Inverse(a);
                Assert.Equal(1, field.Multiply(a, inv));
            }
        }

        [Fact]
        public void TestReedSolomon_EncodingAndErrorCorrection()
        {
            var field = GenericGF.DataMatrix256;
            var decoder = new ReedSolomonDecoder(field);

            // Data: 5 codewords, 7 error correction codewords (12x12 symbol)
            int dataLength = 5;
            int ecLength = 7;
            int totalLength = dataLength + ecLength;

            int[] original = new int[] { 66, 67, 68, 69, 70, 0, 0, 0, 0, 0, 0, 0 };
            ReedSolomonEncoder.Encode(field, original, ecLength);

            // 1. Clean decode (no errors)
            int[] received = (int[])original.Clone();
            bool success = decoder.Decode(received, ecLength);
            Assert.True(success);
            for (int i = 0; i < totalLength; i++)
            {
                Assert.Equal(original[i], received[i]);
            }

            // 2. Corrupt 1 codeword in data portion
            int[] corrupted1 = (int[])original.Clone();
            corrupted1[1] ^= 0x5A;
            bool success1 = decoder.Decode(corrupted1, ecLength);
            Assert.True(success1);
            for (int i = 0; i < totalLength; i++)
            {
                Assert.Equal(original[i], corrupted1[i]);
            }

            // 3. Corrupt 2 codewords (1 data, 1 parity)
            int[] corrupted2 = (int[])original.Clone();
            corrupted2[0] ^= 0xFF;
            corrupted2[dataLength + 2] ^= 0x33;
            bool success2 = decoder.Decode(corrupted2, ecLength);
            Assert.True(success2);
            for (int i = 0; i < totalLength; i++)
            {
                Assert.Equal(original[i], corrupted2[i]);
            }

            // 4. Corrupt 3 codewords (within capacity since 2t = 7 -> t = 3)
            int[] corrupted3 = (int[])original.Clone();
            corrupted3[2] ^= 0x12;
            corrupted3[4] ^= 0x78;
            corrupted3[dataLength + 1] ^= 0x99;
            bool success3 = decoder.Decode(corrupted3, ecLength);
            Assert.True(success3);
            for (int i = 0; i < totalLength; i++)
            {
                Assert.Equal(original[i], corrupted3[i]);
            }
        }

        [Fact]
        public void TestDataMatrixBitParser_RoundTrip()
        {
            var version = DataMatrixVersion.FindVersion(12, 12)!;
            Assert.NotNull(version);
            Assert.Equal(10, version.DataRows);
            Assert.Equal(10, version.DataColumns);
            Assert.Equal(12, version.TotalCodewords);

            byte[] originalCodewords = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
            bool[,] dataGrid = new bool[version.DataRows, version.DataColumns];

            // Place codewords
            DataMatrixBitParser.PlaceCodewords(originalCodewords, dataGrid);

            // Read back codewords
            byte[] readCodewords = DataMatrixBitParser.ReadCodewords(dataGrid, version.TotalCodewords);

            Assert.Equal(originalCodewords.Length, readCodewords.Length);
            for (int i = 0; i < originalCodewords.Length; i++)
            {
                Assert.Equal(originalCodewords[i], readCodewords[i]);
            }
        }

        [Fact]
        public void TestDataMatrixPayloadDecoder_MultiMode()
        {
            // 1. ASCII Mode ("AB12")
            // 'A' (65 -> 66), 'B' (66 -> 67), "12" pair (12 + 130 = 142)
            byte[] asciiBytes = new byte[] { 66, 67, 142, 129 };
            string resAscii = DataMatrixPayloadDecoder.Decode(asciiBytes, 4);
            Assert.Equal("AB12", resAscii);

            // 2. C40 Mode ("PART")
            // Codeword 230 (Switch to C40), followed by C40 triplets
            // 'P' = 14 + 15 = 29, 'A' = 14, 'R' = 14 + 17 = 31
            // val1 = 29 * 1600 + 14 * 40 + 31 + 1 = 46400 + 560 + 32 = 47000 - 8?
            // val1 = 29 * 1600 + 14 * 40 + 31 + 1 = 46992
            // c1 = 46992 / 256 = 183, c2 = 46992 % 256 = 144
            int v = 29 * 1600 + 14 * 40 + 31 + 1;
            byte c1 = (byte)(v >> 8);
            byte c2 = (byte)(v & 0xFF);
            byte[] c40Bytes = new byte[] { 230, c1, c2, 254 };
            string resC40 = DataMatrixPayloadDecoder.Decode(c40Bytes, 4);
            Assert.Equal("PAR", resC40);
        }

        [Fact]
        public void TestDataMatrix_EndToEnd_SymbolGridDecoding()
        {
            string expectedPayload = "LOT-2026-X1";
            var version = DataMatrixVersion.FindVersion(16, 16)!;
            Assert.NotNull(version);

            // Encode symbol grid
            bool[,] symbolGrid = DataMatrixEncoder.EncodeSymbol(expectedPayload, version);
            Assert.Equal(16, symbolGrid.GetLength(0));
            Assert.Equal(16, symbolGrid.GetLength(1));

            // Decode directly
            var decoder = new DataMatrixDecoder();
            var result = decoder.DecodeSymbolGrid(symbolGrid, version);

            Assert.NotNull(result);
            Assert.Equal(BarcodeSymbology.DataMatrix, result!.Symbology);
            Assert.Equal(expectedPayload, result.Text);
            Assert.Equal(1.0, result.Confidence);
        }

        [Fact]
        public void TestDataMatrix_Rotations_90_180_270()
        {
            string expectedPayload = "ABC-7799";
            var version = DataMatrixVersion.FindVersion(14, 14)!;

            bool[,] originalGrid = DataMatrixEncoder.EncodeSymbol(expectedPayload, version);
            int h = originalGrid.GetLength(0);
            int w = originalGrid.GetLength(1);
            var decoder = new DataMatrixDecoder();

            // Test 90 deg clockwise
            bool[,] rot90 = new bool[w, h];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    rot90[c, h - 1 - r] = originalGrid[r, c];

            var res90 = decoder.DecodeSymbolGrid(rot90);
            Assert.NotNull(res90);
            Assert.Equal(expectedPayload, res90!.Text);

            // Test 180 deg
            bool[,] rot180 = new bool[h, w];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    rot180[h - 1 - r, w - 1 - c] = originalGrid[r, c];

            var res180 = decoder.DecodeSymbolGrid(rot180);
            Assert.NotNull(res180);
            Assert.Equal(expectedPayload, res180!.Text);

            // Test 270 deg
            bool[,] rot270 = new bool[w, h];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    rot270[w - 1 - c, r] = originalGrid[r, c];

            var res270 = decoder.DecodeSymbolGrid(rot270);
            Assert.NotNull(res270);
            Assert.Equal(expectedPayload, res270!.Text);
        }

        [Fact]
        public void TestDataMatrix_OpticalImageRenderingAndDecoding()
        {
            string expectedPayload = "SN-981240";
            var version = DataMatrixVersion.FindVersion(14, 14)!;

            bool[,] symbolGrid = DataMatrixEncoder.EncodeSymbol(expectedPayload, version);

            // Render into a Gray8 image buffer with module size = 8 and quiet zone = 2
            using (var image = DataMatrixEncoder.RenderToImage(symbolGrid, modulePixelSize: 8, quietZone: 2))
            {
                Assert.Equal(18 * 8, image.Width);
                Assert.Equal(18 * 8, image.Height);

                var decoder = new DataMatrixDecoder();
                var result = decoder.Decode(image);

                Assert.NotNull(result);
                Assert.Equal(BarcodeSymbology.DataMatrix, result!.Symbology);
                Assert.Equal(expectedPayload, result.Text);
                Assert.Equal(1.0, result.Confidence);
            }
        }

        [Fact]
        public void TestDataMatrix_ErrorCorrection_DamagedSymbol()
        {
            string expectedPayload = "DAMAGE-TEST";
            var version = DataMatrixVersion.FindVersion(16, 16)!;

            bool[,] symbolGrid = DataMatrixEncoder.EncodeSymbol(expectedPayload, version);

            // Intentionally corrupt/flip 2 modules in the inner data area (simulating scratch/dirt)
            symbolGrid[3, 3] = !symbolGrid[3, 3];
            symbolGrid[5, 5] = !symbolGrid[5, 5];

            // Render damaged code to optical image
            using (var image = DataMatrixEncoder.RenderToImage(symbolGrid, modulePixelSize: 6, quietZone: 3))
            {
                var decoder = new DataMatrixDecoder();
                var result = decoder.Decode(image);

                Assert.NotNull(result);
                Assert.Equal(BarcodeSymbology.DataMatrix, result!.Symbology);
                Assert.Equal(expectedPayload, result.Text);
                Assert.Equal(1.0, result.Confidence);
            }
        }
    }
}
