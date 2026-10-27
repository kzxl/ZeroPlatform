using System;
using System.Text.RegularExpressions;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# EAN-13 and UPC-A barcode encoder.
    /// Generates standard 95-module binary patterns with automatic Modulo-10 checksum calculation.
    /// </summary>
    public static class Ean13Encoder
    {
        // 7-bit module encodings for digits 0..9 in L, G, and R sets
        private static readonly string[] L_Patterns = new[]
        {
            "0001101", // 0
            "0011001", // 1
            "0010011", // 2
            "0111101", // 3
            "0100011", // 4
            "0110001", // 5
            "0101111", // 6
            "0111011", // 7
            "0110111", // 8
            "0001011"  // 9
        };

        private static readonly string[] G_Patterns = new[]
        {
            "0100111", // 0
            "0110011", // 1
            "0011011", // 2
            "0100001", // 3
            "0011101", // 4
            "0111001", // 5
            "0000101", // 6
            "0010001", // 7
            "0001001", // 8
            "0010111"  // 9
        };

        private static readonly string[] R_Patterns = new[]
        {
            "1110010", // 0
            "1100110", // 1
            "1101100", // 2
            "1000010", // 3
            "1011100", // 4
            "1001110", // 5
            "1010000", // 6
            "1000100", // 7
            "1001000", // 8
            "1110100"  // 9
        };

        // First digit parity map for digits 2..7 (L = true, G = false)
        private static readonly string[] FirstDigitParities = new[]
        {
            "LLLLLL", // 0 (UPC-A)
            "LLGLGG", // 1
            "LLGGLG", // 2
            "LLGGGL", // 3
            "LGLLGG", // 4
            "LGGLLG", // 5
            "LGGGLL", // 6
            "LGLGLG", // 7
            "LGLGGL", // 8
            "LGGLGL"  // 9
        };

        /// <summary>
        /// Calculates the GS1 Modulo-10 checksum for an 11-digit (UPC-A) or 12-digit (EAN-13) string.
        /// </summary>
        public static int CalculateChecksum(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
                throw new ArgumentNullException(nameof(digits));

            string clean = Regex.Replace(digits, @"\s+", "");

            if (clean.Length == 11) // UPC-A: 11 data digits
            {
                int sum = 0;
                for (int i = 0; i < 11; i++)
                {
                    int val = clean[i] - '0';
                    sum += (i % 2 == 0) ? (val * 3) : val;
                }
                int mod = sum % 10;
                return (mod == 0) ? 0 : (10 - mod);
            }
            else if (clean.Length >= 12) // EAN-13: 12 data digits
            {
                int sum = 0;
                for (int i = 0; i < 12; i++)
                {
                    int val = clean[i] - '0';
                    sum += (i % 2 == 0) ? val : (val * 3);
                }
                int mod = sum % 10;
                return (mod == 0) ? 0 : (10 - mod);
            }
            else
            {
                throw new ArgumentException("Requires 11 digits (UPC-A) or 12 digits (EAN-13).", nameof(digits));
            }
        }

        /// <summary>
        /// Encodes an 11, 12, or 13-digit string into a 95-bit standard EAN-13 / UPC-A barcode module pattern.
        /// If 11 or 12 digits are passed, the Modulo-10 checksum digit is automatically computed and appended.
        /// </summary>
        public static bool[] Encode(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
                throw new ArgumentNullException(nameof(digits));

            string clean = Regex.Replace(digits, @"\s+", "");
            if (clean.Length == 11)
            {
                clean = "0" + clean + CalculateChecksum(clean).ToString();
            }
            else if (clean.Length == 12)
            {
                // Check if it's already a complete 12-digit UPC-A code (11 data + 1 check digit)
                int upcCheck = CalculateChecksum(clean.Substring(0, 11));
                if (upcCheck == (clean[11] - '0'))
                {
                    clean = "0" + clean;
                }
                else
                {
                    clean += CalculateChecksum(clean).ToString();
                }
            }
            else if (clean.Length == 13)
            {
                int expectedChecksum = CalculateChecksum(clean.Substring(0, 12));
                int actualChecksum = clean[12] - '0';
                if (expectedChecksum != actualChecksum)
                    throw new ArgumentException($"Invalid EAN-13 checksum: expected {expectedChecksum}, got {actualChecksum}.");
            }
            else
            {
                throw new ArgumentException("Barcode requires 11 (UPC-A), 12, or 13 digits.", nameof(digits));
            }

            bool[] modules = new bool[95];
            int cursor = 0;

            // 1. Start Guard: 101
            modules[cursor++] = true;
            modules[cursor++] = false;
            modules[cursor++] = true;

            int firstDigit = clean[0] - '0';
            string parity = FirstDigitParities[firstDigit];

            // 2. Left 6 Digits (clean[1..6])
            for (int i = 0; i < 6; i++)
            {
                int digit = clean[i + 1] - '0';
                string pat = (parity[i] == 'L') ? L_Patterns[digit] : G_Patterns[digit];
                for (int b = 0; b < 7; b++)
                {
                    modules[cursor++] = (pat[b] == '1');
                }
            }

            // 3. Center Guard: 01010
            modules[cursor++] = false;
            modules[cursor++] = true;
            modules[cursor++] = false;
            modules[cursor++] = true;
            modules[cursor++] = false;

            // 4. Right 6 Digits (clean[7..12])
            for (int i = 0; i < 6; i++)
            {
                int digit = clean[i + 7] - '0';
                string pat = R_Patterns[digit];
                for (int b = 0; b < 7; b++)
                {
                    modules[cursor++] = (pat[b] == '1');
                }
            }

            // 5. End Guard: 101
            modules[cursor++] = true;
            modules[cursor++] = false;
            modules[cursor++] = true;

            return modules;
        }

        /// <summary>
        /// Generates a 2D symbol bitmap grid with quiet zones and vertical height.
        /// </summary>
        public static bool[,] EncodeSymbol(string digits, int quietZone = 9, int height = 50)
        {
            bool[] modules = Encode(digits);
            int totalWidth = modules.Length + (quietZone * 2);
            bool[,] grid = new bool[height, totalWidth];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < modules.Length; x++)
                {
                    grid[y, quietZone + x] = modules[x];
                }
            }

            return grid;
        }
    }
}
