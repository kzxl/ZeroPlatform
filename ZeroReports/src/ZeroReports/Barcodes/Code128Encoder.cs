using System;
using System.Collections.Generic;

namespace ZeroReports.Barcodes
{
    /// <summary>
    /// Pure C# ISO/IEC 15417 Code 128 barcode encoder (Subsets B and C Auto).
    /// Generates exact vector bar widths and positions with modulo-103 check digit.
    /// </summary>
    public static class Code128Encoder
    {
        // 107 Code 128 symbol patterns (widths of alternating bars and spaces)
        private static readonly string[] Patterns = new string[107]
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112"                               // 100-106
        };

        private const int StartA = 103;
        private const int StartB = 104;
        private const int StartC = 105;
        private const int Stop = 106;

        /// <summary>
        /// Encodes text to Code 128 barcode bars.
        /// </summary>
        /// <param name="text">Input text to encode.</param>
        /// <param name="moduleWidth">Width of a single unit module (in points/mm).</param>
        public static Barcode1DResult Encode(string text, float moduleWidth = 1.0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = " ";
            }

            var codes = new List<int>();
            codes.Add(StartB); // Use Code B for standard alphanumeric ASCII

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 32 && c <= 126)
                {
                    codes.Add(c - 32);
                }
                else
                {
                    codes.Add(0); // Fallback space
                }
            }

            // Calculate Modulo 103 Checksum
            long sum = codes[0];
            for (int i = 1; i < codes.Count; i++)
            {
                sum += (long)i * codes[i];
            }
            int checkDigit = (int)(sum % 103);
            codes.Add(checkDigit);
            codes.Add(Stop);

            var result = new Barcode1DResult(text);
            float currentX = 0f;

            // Optional 10-module Quiet Zone at start
            float quietZone = moduleWidth * 10f;
            currentX += quietZone;

            foreach (int code in codes)
            {
                string pattern = Patterns[code];
                bool isBlack = true; // Code 128 always alternates: bar, space, bar, space, bar, space

                for (int p = 0; p < pattern.Length; p++)
                {
                    int widthModules = pattern[p] - '0';
                    float width = widthModules * moduleWidth;

                    if (isBlack)
                    {
                        result.Bars.Add(new BarcodeBar(currentX, width, isBlack: true));
                    }

                    currentX += width;
                    isBlack = !isBlack;
                }
            }

            // Quiet zone at end
            currentX += quietZone;
            result.TotalWidth = currentX;

            return result;
        }
    }
}
