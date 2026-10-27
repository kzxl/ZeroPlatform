using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroUI.Core.Barcode
{
    /// <summary>
    /// Supported industrial barcode symbologies.
    /// </summary>
    public enum BarcodeSymbology
    {
        Code128,
        Code39,
        QrCode
    }

    /// <summary>
    /// Pure mathematical vector barcode and QR Code encoding engine with zero external dependencies.
    /// Generates bitmask arrays for 1D bars and 2D QR matrix modules with Reed-Solomon error correction.
    /// </summary>
    public static class BarcodeEngine
    {
        #region 1D Barcode Encoding

        /// <summary>
        /// Encodes alphanumeric text into a 1D bitmask pattern (true = black bar, false = white space).
        /// </summary>
        public static bool[] Encode1D(string text, BarcodeSymbology symbology)
        {
            if (string.IsNullOrEmpty(text)) text = " ";

            switch (symbology)
            {
                case BarcodeSymbology.Code39:
                    return EncodeCode39(text);
                case BarcodeSymbology.Code128:
                default:
                    return EncodeCode128(text);
            }
        }

        #region Code 128 Implementation

        // Standard Code 128 bit patterns (107 patterns, each 11 bits: 3 bars and 3 spaces)
        private static readonly ushort[] Code128Patterns = new ushort[]
        {
            0x6CC, 0x66C, 0x666, 0x498, 0x48C, 0x44C, 0x4C8, 0x4C4, 0x464, 0x648,
            0x644, 0x624, 0x59C, 0x4DC, 0x4CE, 0x5C8, 0x4EC, 0x4E6, 0x672, 0x65C,
            0x64E, 0x6E4, 0x674, 0x76E, 0x74C, 0x72C, 0x726, 0x764, 0x734, 0x732,
            0x6D8, 0x6C6, 0x636, 0x518, 0x458, 0x446, 0x588, 0x468, 0x462, 0x688,
            0x628, 0x622, 0x5B8, 0x58E, 0x46E, 0x5D8, 0x5C6, 0x476, 0x776, 0x68E,
            0x62E, 0x6E8, 0x6E2, 0x6EE, 0x758, 0x746, 0x716, 0x768, 0x762, 0x71A,
            0x77A, 0x642, 0x78A, 0x530, 0x50C, 0x4B0, 0x486, 0x42C, 0x426, 0x590,
            0x584, 0x4D0, 0x4C2, 0x434, 0x432, 0x612, 0x650, 0x7BA, 0x614, 0x47A,
            0x53C, 0x4BC, 0x4B6, 0x5C4, 0x4E4, 0x4E2, 0x79C, 0x70E, 0x7E4, 0x79E,
            0x7CE, 0x5E4, 0x5E2, 0x6B8, 0x6B2, 0x5B2, 0x574, 0x572, 0x56E, 0x5AE,
            0x7A4, 0x7A2, 0x73A, 0x72E, 0x78E, 0x798, 0x7A8
        };

        private const int Code128StartB = 104;
        private const int Code128Stop = 106;
        private const uint Code128StopPattern = 0x18EB; // 13 bits for stop symbol

        private static bool[] EncodeCode128(string text)
        {
            var symbols = new List<int>();
            symbols.Add(Code128StartB);

            long checksum = Code128StartB;
            for (int i = 0; i < text.Length; i++)
            {
                int val = text[i] - 32;
                if (val < 0 || val > 95) val = 0; // Fallback to space
                symbols.Add(val);
                checksum += (long)val * (i + 1);
            }

            int checkSymbol = (int)(checksum % 103);
            symbols.Add(checkSymbol);
            symbols.Add(Code128Stop);

            int totalBits = (symbols.Count - 1) * 11 + 13 + 20; // 10 bits quiet zone on each side
            var bits = new bool[totalBits];
            int bitIdx = 10; // Start after quiet zone

            for (int s = 0; s < symbols.Count; s++)
            {
                int sym = symbols[s];
                if (sym == Code128Stop)
                {
                    uint pattern = Code128StopPattern;
                    for (int b = 12; b >= 0; b--)
                    {
                        bits[bitIdx++] = ((pattern >> b) & 1) == 1;
                    }
                }
                else
                {
                    ushort pattern = Code128Patterns[sym];
                    for (int b = 10; b >= 0; b--)
                    {
                        bits[bitIdx++] = ((pattern >> b) & 1) == 1;
                    }
                }
            }

            return bits;
        }

        #endregion

        #region Code 39 Implementation

        private static readonly Dictionary<char, ushort> Code39Patterns = new Dictionary<char, ushort>
        {
            {'0', 0x034}, {'1', 0x121}, {'2', 0x061}, {'3', 0x160}, {'4', 0x031},
            {'5', 0x130}, {'6', 0x070}, {'7', 0x025}, {'8', 0x124}, {'9', 0x064},
            {'A', 0x109}, {'B', 0x049}, {'C', 0x148}, {'D', 0x019}, {'E', 0x118},
            {'F', 0x058}, {'G', 0x00D}, {'H', 0x10C}, {'I', 0x04C}, {'J', 0x01C},
            {'K', 0x103}, {'L', 0x043}, {'M', 0x142}, {'N', 0x013}, {'O', 0x112},
            {'P', 0x052}, {'Q', 0x007}, {'R', 0x106}, {'S', 0x046}, {'T', 0x016},
            {'U', 0x181}, {'V', 0x0C1}, {'W', 0x1C0}, {'X', 0x091}, {'Y', 0x190},
            {'Z', 0x0D0}, {'-', 0x085}, {'.', 0x184}, {' ', 0x0C4}, {'*', 0x094},
            {'$', 0x0A8}, {'/', 0x0A2}, {'+', 0x08A}, {'%', 0x02A}
        };

        private static bool[] EncodeCode39(string rawText)
        {
            string upper = rawText.ToUpperInvariant();
            var symbols = new List<char>();
            symbols.Add('*');
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                symbols.Add(Code39Patterns.ContainsKey(c) ? c : '-');
            }
            symbols.Add('*');

            var bits = new List<bool>();
            // Quiet zone
            for (int i = 0; i < 10; i++) bits.Add(false);

            for (int s = 0; s < symbols.Count; s++)
            {
                ushort pattern = Code39Patterns[symbols[s]];
                for (int b = 8; b >= 0; b--)
                {
                    bool isWide = ((pattern >> b) & 1) == 1;
                    bool isBar = (b % 2 == 0); // 9 elements: bar, space, bar, space, bar, space, bar, space, bar
                    int width = isWide ? 3 : 1;
                    for (int w = 0; w < width; w++) bits.Add(isBar);
                }
                // Inter-character space
                bits.Add(false);
            }

            // Trailing quiet zone
            for (int i = 0; i < 10; i++) bits.Add(false);
            return bits.ToArray();
        }

        #endregion

        #endregion

        #region 2D QR Code Generation

        /// <summary>
        /// Generates a 2D QR Code boolean matrix with standard Finder Patterns, Timing Patterns,
        /// and Reed-Solomon Error Correction. True = dark module, False = light module.
        /// </summary>
        public static bool[,] EncodeQr(string content)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(content ?? string.Empty);

            // Determine version based on data length (Version 1: up to 17 bytes, Version 2: up to 32 bytes, Version 3: up to 53 bytes)
            int version = 1;
            int totalDataCodewords = 19;
            int ecCodewords = 7;

            if (dataBytes.Length > 14)
            {
                version = 2;
                totalDataCodewords = 34;
                ecCodewords = 10;
            }
            if (dataBytes.Length > 26)
            {
                version = 3;
                totalDataCodewords = 55;
                ecCodewords = 15;
            }

            int matrixSize = 21 + (version - 1) * 4;
            bool[,] matrix = new bool[matrixSize, matrixSize];
            bool[,] isFunction = new bool[matrixSize, matrixSize];

            // 1. Finder Patterns (Top-Left, Top-Right, Bottom-Left)
            PlaceFinderPattern(matrix, isFunction, 0, 0);
            PlaceFinderPattern(matrix, isFunction, matrixSize - 7, 0);
            PlaceFinderPattern(matrix, isFunction, 0, matrixSize - 7);

            // 2. Timing Patterns (Row 6 and Column 6)
            for (int i = 8; i < matrixSize - 8; i++)
            {
                bool dark = (i % 2 == 0);
                matrix[6, i] = dark;
                isFunction[6, i] = true;
                matrix[i, 6] = dark;
                isFunction[i, 6] = true;
            }

            // 3. Dark Module
            matrix[4 * version + 9, 8] = true;
            isFunction[4 * version + 9, 8] = true;

            // 4. Alignment Pattern (for Version 2 & 3)
            if (version >= 2)
            {
                int alignPos = (version == 2) ? 18 : 22;
                PlaceAlignmentPattern(matrix, isFunction, alignPos - 2, alignPos - 2);
            }

            // 5. Reserve Format Information Area
            for (int i = 0; i < 9; i++)
            {
                isFunction[8, i] = true;
                isFunction[i, 8] = true;
                isFunction[8, matrixSize - 1 - i] = true;
                isFunction[matrixSize - 1 - i, 8] = true;
            }

            // 6. Build Codewords with 8-bit Byte Mode
            byte[] codewords = BuildQrCodewords(dataBytes, totalDataCodewords, ecCodewords);

            // 7. Place Data Codewords in Zig-Zag Pattern with Mask Pattern 0 ((row + col) % 2 == 0)
            int bitIndex = 0;
            int totalBits = codewords.Length * 8;

            int right = matrixSize - 1;
            bool goingUp = true;

            while (right > 0)
            {
                if (right == 6) right--; // Skip vertical timing column

                for (int vertical = 0; vertical < matrixSize; vertical++)
                {
                    int r = goingUp ? (matrixSize - 1 - vertical) : vertical;

                    for (int colOffset = 0; colOffset < 2; colOffset++)
                    {
                        int c = right - colOffset;
                        if (!isFunction[r, c])
                        {
                            bool bit = false;
                            if (bitIndex < totalBits)
                            {
                                int byteIdx = bitIndex / 8;
                                int bitPos = 7 - (bitIndex % 8);
                                bit = ((codewords[byteIdx] >> bitPos) & 1) == 1;
                                bitIndex++;
                            }

                            // Apply standard Mask Pattern 0: (r + c) % 2 == 0
                            bool mask = ((r + c) % 2 == 0);
                            matrix[r, c] = bit ^ mask;
                        }
                    }
                }
                right -= 2;
                goingUp = !goingUp;
            }

            // 8. Write Format Information (Error Correction Level L + Mask 0 = 0x77C4)
            ushort formatBits = 0x77C4;
            WriteFormatBits(matrix, matrixSize, formatBits);

            return matrix;
        }

        private static void PlaceFinderPattern(bool[,] matrix, bool[,] isFunction, int startX, int startY)
        {
            for (int y = -1; y <= 7; y++)
            {
                for (int x = -1; x <= 7; x++)
                {
                    int px = startX + x;
                    int py = startY + y;
                    if (px >= 0 && px < matrix.GetLength(0) && py >= 0 && py < matrix.GetLength(1))
                    {
                        isFunction[py, px] = true;
                        bool isBorder = (x == -1 || x == 7 || y == -1 || y == 7);
                        if (isBorder)
                        {
                            matrix[py, px] = false; // Separator
                        }
                        else
                        {
                            bool isOuter = (x == 0 || x == 6 || y == 0 || y == 6);
                            bool isInner = (x >= 2 && x <= 4 && y >= 2 && y <= 4);
                            matrix[py, px] = isOuter || isInner;
                        }
                    }
                }
            }
        }

        private static void PlaceAlignmentPattern(bool[,] matrix, bool[,] isFunction, int startX, int startY)
        {
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int px = startX + x;
                    int py = startY + y;
                    isFunction[py, px] = true;
                    bool isOuter = (x == 0 || x == 4 || y == 0 || y == 4);
                    bool isCenter = (x == 2 && y == 2);
                    matrix[py, px] = isOuter || isCenter;
                }
            }
        }

        private static void WriteFormatBits(bool[,] matrix, int size, ushort formatBits)
        {
            // Top-Left corner format bits
            int[] r1 = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };
            int[] c1 = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };

            for (int i = 0; i < 15; i++)
            {
                bool bit = ((formatBits >> (14 - i)) & 1) == 1;
                matrix[r1[i], c1[i]] = bit;
            }

            // Split format bits around corners
            for (int i = 0; i < 7; i++)
            {
                bool bit = ((formatBits >> i) & 1) == 1;
                matrix[size - 1 - i, 8] = bit;
            }
            for (int i = 7; i < 15; i++)
            {
                bool bit = ((formatBits >> i) & 1) == 1;
                matrix[8, size - 15 + i] = bit;
            }
        }

        private static byte[] BuildQrCodewords(byte[] data, int totalDataWords, int ecWords)
        {
            var bits = new List<bool>();

            // Mode Indicator: Byte Mode = 0100 (4 bits)
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

            // Character Count Indicator (8 bits for Version 1-9)
            int count = data.Length;
            for (int b = 7; b >= 0; b--) bits.Add(((count >> b) & 1) == 1);

            // Data Bytes
            for (int i = 0; i < data.Length; i++)
            {
                byte d = data[i];
                for (int b = 7; b >= 0; b--) bits.Add(((d >> b) & 1) == 1);
            }

            // Terminator (up to 4 zeroes)
            int padZeroes = Math.Min(4, (totalDataWords * 8) - bits.Count);
            for (int i = 0; i < padZeroes; i++) bits.Add(false);

            // Pad to multiple of 8
            while (bits.Count % 8 != 0) bits.Add(false);

            // Convert to byte array
            var dataCodewords = new byte[totalDataWords];
            int byteIndex = 0;
            for (int i = 0; i < bits.Count; i += 8)
            {
                if (byteIndex >= totalDataWords) break;
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    if (bits[i + bit]) b |= (byte)(1 << (7 - bit));
                }
                dataCodewords[byteIndex++] = b;
            }

            // Pad alternating 0xEC and 0x11 until full
            byte[] padBytes = new byte[] { 0xEC, 0x11 };
            int padToggle = 0;
            while (byteIndex < totalDataWords)
            {
                dataCodewords[byteIndex++] = padBytes[padToggle];
                padToggle ^= 1;
            }

            // Reed-Solomon Error Correction Calculation
            byte[] ecCodewords = CalculateReedSolomon(dataCodewords, ecWords);

            // Interleave Data + EC
            byte[] result = new byte[totalDataWords + ecWords];
            Array.Copy(dataCodewords, 0, result, 0, totalDataWords);
            Array.Copy(ecCodewords, 0, result, totalDataWords, ecWords);
            return result;
        }

        #region Reed-Solomon Error Correction (Galois Field GF(256))

        private static readonly byte[] GfExp = new byte[512];
        private static readonly byte[] GfLog = new byte[256];

        static BarcodeEngine()
        {
            // Initialize Galois Field GF(256) tables with primitive polynomial 0x11D (285)
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)x;
                GfLog[x] = (byte)i;
                x <<= 1;
                if (x >= 256) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++)
            {
                GfExp[i] = GfExp[i - 255];
            }
        }

        private static byte GfMultiply(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        private static byte[] CalculateReedSolomon(byte[] data, int ecCount)
        {
            // Generator polynomial for ecCount
            byte[] generator = new byte[] { 1 };
            for (int i = 0; i < ecCount; i++)
            {
                byte[] term = new byte[] { 1, GfExp[i] };
                generator = PolyMultiply(generator, term);
            }

            byte[] remainder = new byte[ecCount];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ remainder[0]);
                for (int j = 0; j < ecCount - 1; j++)
                {
                    remainder[j] = (byte)(remainder[j + 1] ^ GfMultiply(factor, generator[j + 1]));
                }
                remainder[ecCount - 1] = GfMultiply(factor, generator[ecCount]);
            }

            return remainder;
        }

        private static byte[] PolyMultiply(byte[] p1, byte[] p2)
        {
            byte[] result = new byte[p1.Length + p2.Length - 1];
            for (int i = 0; i < p1.Length; i++)
            {
                for (int j = 0; j < p2.Length; j++)
                {
                    result[i + j] ^= GfMultiply(p1[i], p2[j]);
                }
            }
            return result;
        }

        #endregion

        #endregion
    }
}
