using System;
using System.Collections.Generic;

namespace ZeroReports.Barcodes
{
    /// <summary>
    /// Pure C# EAN-13 barcode encoder with parity patterns and modulo-10 check digit calculation.
    /// </summary>
    public static class Ean13Encoder
    {
        // L-code patterns (odd parity)
        private static readonly string[] LPatterns = new[]
        {
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011"
        };

        // G-code patterns (even parity)
        private static readonly string[] GPatterns = new[]
        {
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111"
        };

        // R-code patterns (right hand side)
        private static readonly string[] RPatterns = new[]
        {
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100"
        };

        // Parity selection according to first digit
        private static readonly string[] FirstDigitParity = new[]
        {
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
        };

        public static int CalculateCheckDigit(string first12Digits)
        {
            if (first12Digits.Length < 12) return 0;

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int val = first12Digits[i] - '0';
                sum += (i % 2 == 0) ? val : (val * 3);
            }

            int mod = sum % 10;
            return (mod == 0) ? 0 : (10 - mod);
        }

        public static Barcode1DResult Encode(string digits, float moduleWidth = 1.0f)
        {
            // Sanitize input: extract digits only
            var sb = new System.Text.StringBuilder();
            foreach (char c in digits)
            {
                if (char.IsDigit(c)) sb.Append(c);
            }

            string clean = sb.ToString();
            if (clean.Length < 12)
            {
                clean = clean.PadLeft(12, '0');
            }

            if (clean.Length == 12)
            {
                clean += CalculateCheckDigit(clean);
            }
            else if (clean.Length > 13)
            {
                clean = clean.Substring(0, 13);
            }

            int first = clean[0] - '0';
            string parity = FirstDigitParity[first];

            // Build binary pattern string
            var bitString = new System.Text.StringBuilder();

            // Normal Quiet zone: 9 modules
            bitString.Append(new string('0', 9));

            // Start guard: 101
            bitString.Append("101");

            // Left 6 digits
            for (int i = 0; i < 6; i++)
            {
                int digit = clean[i + 1] - '0';
                char p = parity[i];
                bitString.Append(p == 'L' ? LPatterns[digit] : GPatterns[digit]);
            }

            // Center guard: 01010
            bitString.Append("01010");

            // Right 6 digits (R-codes)
            for (int i = 0; i < 6; i++)
            {
                int digit = clean[i + 7] - '0';
                bitString.Append(RPatterns[digit]);
            }

            // End guard: 101
            bitString.Append("101");

            // End Quiet zone: 9 modules
            bitString.Append(new string('0', 9));

            var result = new Barcode1DResult(clean);
            float currentX = 0f;
            string bits = bitString.ToString();

            int b = 0;
            while (b < bits.Length)
            {
                char bit = bits[b];
                int runLength = 0;
                while (b < bits.Length && bits[b] == bit)
                {
                    runLength++;
                    b++;
                }

                float width = runLength * moduleWidth;
                if (bit == '1')
                {
                    result.Bars.Add(new BarcodeBar(currentX, width, isBlack: true));
                }

                currentX += width;
            }

            result.TotalWidth = currentX;
            return result;
        }
    }
}
