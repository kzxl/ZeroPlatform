using Xunit;
using ZeroUI.Core.Barcode;

namespace ZeroUI.Core.Tests
{
    public class BarcodeEngineTests
    {
        [Fact]
        public void Encode1D_Code128_GeneratesValidBitPattern()
        {
            string code = "LOT-2026-09";
            bool[] bits = BarcodeEngine.Encode1D(code, BarcodeSymbology.Code128);

            Assert.NotNull(bits);
            Assert.True(bits.Length > 50);

            // Leading and trailing quiet zones (10 false bits)
            for (int i = 0; i < 10; i++)
            {
                Assert.False(bits[i]);
                Assert.False(bits[bits.Length - 1 - i]);
            }

            // Immediately after quiet zone is the Start B symbol, which begins with a black bar
            Assert.True(bits[10]);
        }

        [Fact]
        public void Encode1D_Code39_GeneratesValidBitPattern()
        {
            string code = "ABC-123";
            bool[] bits = BarcodeEngine.Encode1D(code, BarcodeSymbology.Code39);

            Assert.NotNull(bits);
            Assert.True(bits.Length > 50);

            // Leading and trailing quiet zones (10 false bits)
            for (int i = 0; i < 10; i++)
            {
                Assert.False(bits[i]);
                Assert.False(bits[bits.Length - 1 - i]);
            }

            // Starts with '*' character bar
            Assert.True(bits[10]);
        }

        [Fact]
        public void EncodeQr_GeneratesSquareMatrixWithFinderPatterns()
        {
            string url = "https://zeroui.net";
            bool[,] matrix = BarcodeEngine.EncodeQr(url);

            Assert.NotNull(matrix);
            int size = matrix.GetLength(0);
            Assert.Equal(size, matrix.GetLength(1));
            Assert.True(size >= 21); // Version 1 is 21x21

            // Verify Top-Left 7x7 Finder Pattern Outer Box
            for (int i = 0; i < 7; i++)
            {
                Assert.True(matrix[0, i]); // Top bar
                Assert.True(matrix[6, i]); // Bottom bar of finder
                Assert.True(matrix[i, 0]); // Left bar
                Assert.True(matrix[i, 6]); // Right bar of finder
            }

            // Verify Top-Left Finder Center 3x3 Dark Module
            for (int y = 2; y <= 4; y++)
            {
                for (int x = 2; x <= 4; x++)
                {
                    Assert.True(matrix[y, x]);
                }
            }

            // Verify Top-Right 7x7 Finder Pattern
            for (int i = 0; i < 7; i++)
            {
                Assert.True(matrix[0, size - 7 + i]);
                Assert.True(matrix[6, size - 7 + i]);
            }

            // Verify Bottom-Left 7x7 Finder Pattern
            for (int i = 0; i < 7; i++)
            {
                Assert.True(matrix[size - 7 + i, 0]);
                Assert.True(matrix[size - 7 + i, 6]);
            }
        }
    }
}
