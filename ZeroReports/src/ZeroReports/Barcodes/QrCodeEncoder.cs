using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroReports.Barcodes
{
    /// <summary>
    /// Pure C# ISO/IEC 18004 QR Code matrix generator with Reed-Solomon Error Correction.
    /// Produces a boolean 2D matrix suitable for sharp vector rectangle rendering.
    /// </summary>
    public static class QrCodeEncoder
    {
        // Galois Field GF(2^8) log and antilog tables for Reed-Solomon polynomial math
        private static readonly byte[] ExpTable = new byte[256];
        private static readonly byte[] LogTable = new byte[256];

        static QrCodeEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                ExpTable[i] = (byte)x;
                LogTable[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0)
                {
                    x ^= 0x11D; // x^8 + x^4 + x^3 + x^2 + 1
                }
            }
            ExpTable[255] = ExpTable[0];
        }

        private static byte GfMultiply(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return ExpTable[(LogTable[a] + LogTable[b]) % 255];
        }

        /// <summary>
        /// Generates Reed-Solomon error correction codewords.
        /// </summary>
        private static byte[] GenerateEcCodewords(byte[] data, int ecCount)
        {
            // Build generator polynomial
            byte[] gen = new byte[] { 1 };
            for (int i = 0; i < ecCount; i++)
            {
                byte[] next = new byte[gen.Length + 1];
                for (int j = 0; j < gen.Length; j++)
                {
                    next[j] ^= GfMultiply(gen[j], ExpTable[i]);
                    next[j + 1] ^= gen[j];
                }
                gen = next;
            }

            // Polynomial division
            byte[] remainder = new byte[ecCount];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ remainder[0]);
                for (int i = 0; i < ecCount - 1; i++)
                {
                    remainder[i] = (byte)(remainder[i + 1] ^ GfMultiply(gen[i + 1], factor));
                }
                remainder[ecCount - 1] = GfMultiply(gen[ecCount], factor);
            }

            return remainder;
        }

        /// <summary>
        /// Encodes text into a QR Code 2D matrix (Version 1-3 with Error Correction Level M).
        /// </summary>
        public static Barcode2DResult Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) text = " ";

            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            // Determine minimal QR version (Level M)
            // Version 1: 16 data bytes, 10 EC bytes -> 21x21
            // Version 2: 28 data bytes, 16 EC bytes -> 25x25
            // Version 3: 44 data bytes, 26 EC bytes -> 29x29
            int version;
            int totalDataBytes;
            int ecBytes;

            if (textBytes.Length <= 14)
            {
                version = 1;
                totalDataBytes = 16;
                ecBytes = 10;
            }
            else if (textBytes.Length <= 26)
            {
                version = 2;
                totalDataBytes = 28;
                ecBytes = 16;
            }
            else
            {
                version = 3;
                totalDataBytes = 44;
                ecBytes = 26;
            }

            int size = 17 + 4 * version;
            var result = new Barcode2DResult(size);
            var isReserved = new bool[size, size];

            // 1. Pack data into 8-bit byte mode bitstream
            var bits = new List<bool>();
            // Mode Indicator: 0100 (Byte mode)
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

            // Character count indicator (8 bits for Ver 1-9)
            int charCount = textBytes.Length;
            for (int i = 7; i >= 0; i--)
            {
                bits.Add(((charCount >> i) & 1) == 1);
            }

            // Data bytes
            foreach (byte b in textBytes)
            {
                for (int i = 7; i >= 0; i--)
                {
                    bits.Add(((b >> i) & 1) == 1);
                }
            }

            // Terminator: up to 4 zero bits
            int maxDataBits = totalDataBytes * 8;
            int termLen = Math.Min(4, maxDataBits - bits.Count);
            for (int i = 0; i < termLen; i++) bits.Add(false);

            // Pad to 8-bit boundary
            while (bits.Count % 8 != 0) bits.Add(false);

            // Pad with alternating 0xEC and 0x11
            byte[] padBytes = new byte[] { 0xEC, 0x11 };
            int padIdx = 0;
            while (bits.Count < maxDataBits)
            {
                byte pad = padBytes[padIdx % 2];
                padIdx++;
                for (int i = 7; i >= 0; i--)
                {
                    bits.Add(((pad >> i) & 1) == 1);
                }
            }

            // Convert bitstream to data bytes
            byte[] dataCodewords = new byte[totalDataBytes];
            for (int i = 0; i < totalDataBytes; i++)
            {
                byte val = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (bits[i * 8 + b]) val |= (byte)(1 << (7 - b));
                }
                dataCodewords[i] = val;
            }

            // Generate Error Correction codewords
            byte[] ecCodewords = GenerateEcCodewords(dataCodewords, ecBytes);

            // Combine Data + EC codewords into final bit sequence
            var finalBits = new List<bool>();
            foreach (byte b in dataCodewords)
            {
                for (int i = 7; i >= 0; i--) finalBits.Add(((b >> i) & 1) == 1);
            }
            foreach (byte b in ecCodewords)
            {
                for (int i = 7; i >= 0; i--) finalBits.Add(((b >> i) & 1) == 1);
            }

            // 2. Place Function Patterns into Matrix

            // Finder patterns (Top-Left, Top-Right, Bottom-Left)
            PlaceFinderPattern(result, isReserved, 0, 0);
            PlaceFinderPattern(result, isReserved, size - 7, 0);
            PlaceFinderPattern(result, isReserved, 0, size - 7);

            // Timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                bool val = (i % 2 == 0);
                if (!isReserved[i, 6]) { result[i, 6] = val; isReserved[i, 6] = true; }
                if (!isReserved[6, i]) { result[6, i] = val; isReserved[6, i] = true; }
            }

            // Alignment pattern for Version >= 2
            if (version >= 2)
            {
                int alignPos = size - 7;
                PlaceAlignmentPattern(result, isReserved, alignPos, alignPos);
            }

            // Reserve Format Info areas around Top-Left finder
            for (int i = 0; i < 9; i++)
            {
                isReserved[i, 8] = true;
                isReserved[8, i] = true;
            }
            for (int i = 0; i < 8; i++)
            {
                isReserved[size - 1 - i, 8] = true;
                isReserved[8, size - 1 - i] = true;
            }
            // Dark module at (8, 4*version + 9)
            result[8, 4 * version + 9] = true;
            isReserved[8, 4 * version + 9] = true;

            // 3. Place Data Bits in 2-column zigzag fashion
            int bitIdx = 0;
            int right = size - 1;
            while (right > 0)
            {
                if (right == 6) right--; // Skip vertical timing pattern

                for (int vert = 0; vert < size; vert++)
                {
                    int y = ((right + 1) / 2 % 2 == 1) ? (size - 1 - vert) : vert;

                    for (int xCol = 0; xCol < 2; xCol++)
                    {
                        int x = right - xCol;
                        if (!isReserved[x, y])
                        {
                            bool bit = (bitIdx < finalBits.Count) ? finalBits[bitIdx++] : false;
                            // Apply Mask Pattern 0: (x + y) % 2 == 0
                            if ((x + y) % 2 == 0)
                            {
                                bit = !bit;
                            }
                            result[x, y] = bit;
                        }
                    }
                }
                right -= 2;
            }

            // 4. Format Information (Level M = 00, Mask 0 = 000 -> 15-bit sequence: 101010000010010)
            // Standard precomputed 15-bit format for Level M / Mask 0 (BCH(15, 5) masked with 0x5412)
            int formatBits = 0b101010000010010;

            // Place format bits on matrix
            int[] formatCoordsX = new int[] { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
            int[] formatCoordsY = new int[] { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };

            for (int i = 0; i < 15; i++)
            {
                bool b = ((formatBits >> i) & 1) == 1;
                result[formatCoordsX[i], formatCoordsY[i]] = b;

                // Copy to secondary format location
                if (i < 7)
                {
                    result[8, size - 1 - i] = b;
                }
                else
                {
                    result[size - 15 + i, 8] = b;
                }
            }

            return result;
        }

        private static void PlaceFinderPattern(Barcode2DResult matrix, bool[,] isReserved, int startX, int startY)
        {
            for (int dy = -1; dy <= 7; dy++)
            {
                for (int dx = -1; dx <= 7; dx++)
                {
                    int x = startX + dx;
                    int y = startY + dy;
                    if (x < 0 || x >= matrix.Size || y < 0 || y >= matrix.Size) continue;

                    isReserved[x, y] = true;

                    // 7x7 outer square, 5x5 inner white, 3x3 center black
                    if (dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6)
                    {
                        if (dx == 0 || dx == 6 || dy == 0 || dy == 6 ||
                            (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4))
                        {
                            matrix[x, y] = true;
                        }
                        else
                        {
                            matrix[x, y] = false;
                        }
                    }
                    else
                    {
                        matrix[x, y] = false; // Separator
                    }
                }
            }
        }

        private static void PlaceAlignmentPattern(Barcode2DResult matrix, bool[,] isReserved, int centerX, int centerY)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x < 0 || x >= matrix.Size || y < 0 || y >= matrix.Size) continue;

                    isReserved[x, y] = true;
                    if (Math.Abs(dx) == 2 || Math.Abs(dy) == 2 || (dx == 0 && dy == 0))
                    {
                        matrix[x, y] = true;
                    }
                    else
                    {
                        matrix[x, y] = false;
                    }
                }
            }
        }
    }
}
