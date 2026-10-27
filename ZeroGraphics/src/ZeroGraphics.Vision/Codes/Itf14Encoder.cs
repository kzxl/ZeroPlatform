using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# ITF-14 (Interleaved 2 of 5) master carton barcode encoder.
    /// Generates standard interleaved binary patterns with bearer bars and GS1 Mod-10 checksum validation.
    /// </summary>
    public static class Itf14Encoder
    {
        // 5-element binary weights: 0 = Narrow, 1 = Wide (Wide is 2x or 3x Narrow)
        private static readonly int[][] Patterns2of5 = new int[][]
        {
            new int[] { 0, 0, 1, 1, 0 }, // 0: NNWWN
            new int[] { 1, 0, 0, 0, 1 }, // 1: WNNNW
            new int[] { 0, 1, 0, 0, 1 }, // 2: NWNNW
            new int[] { 1, 1, 0, 0, 0 }, // 3: WWNNN
            new int[] { 0, 0, 1, 0, 1 }, // 4: NNWNW
            new int[] { 1, 0, 1, 0, 0 }, // 5: WNWNN
            new int[] { 0, 1, 1, 0, 0 }, // 6: NWWNN
            new int[] { 0, 0, 0, 1, 1 }, // 7: NNNWW
            new int[] { 1, 0, 0, 1, 0 }, // 8: WNNWN
            new int[] { 0, 1, 0, 1, 0 }  // 9: NWNWN
        };

        /// <summary>
        /// Calculates GS1 Modulo-10 checksum for a 13-digit string.
        /// </summary>
        public static int CalculateChecksum(string digits13)
        {
            if (digits13 == null || digits13.Length < 13)
                throw new ArgumentException("Requires at least 13 digits.", nameof(digits13));

            int sum = 0;
            for (int i = 0; i < 13; i++)
            {
                int val = digits13[i] - '0';
                sum += (i % 2 == 0) ? (val * 3) : val;
            }

            int mod = sum % 10;
            return (mod == 0) ? 0 : (10 - mod);
        }

        /// <summary>
        /// Encodes a 13 or 14-digit string into a list of run lengths (alternating black bar and white space widths).
        /// Wide to Narrow ratio is 2.5 : 1 (represented as 2 and 5 integer modules or 1 and 2/3).
        /// </summary>
        public static List<int> EncodeRuns(string digits, int narrowWidth = 2, int wideWidth = 5)
        {
            if (string.IsNullOrWhiteSpace(digits))
                throw new ArgumentNullException(nameof(digits));

            string clean = Regex.Replace(digits, @"\s+", "");
            if (clean.Length == 13)
            {
                clean += CalculateChecksum(clean).ToString();
            }
            else if (clean.Length == 14)
            {
                int expected = CalculateChecksum(clean.Substring(0, 13));
                int actual = clean[13] - '0';
                if (expected != actual)
                    throw new ArgumentException($"Invalid ITF-14 checksum: expected {expected}, got {actual}.");
            }
            else
            {
                throw new ArgumentException("ITF-14 requires exactly 13 or 14 digits.", nameof(digits));
            }

            var runs = new List<int>(80);

            // 1. Start Pattern: N bar, N space, N bar, N space
            runs.Add(narrowWidth); // bar
            runs.Add(narrowWidth); // space
            runs.Add(narrowWidth); // bar
            runs.Add(narrowWidth); // space

            // 2. Data Pairs (7 pairs of interleaved digits)
            for (int p = 0; p < 7; p++)
            {
                int dBar = clean[p * 2] - '0';
                int dSpace = clean[p * 2 + 1] - '0';

                int[] barPat = Patterns2of5[dBar];
                int[] spacePat = Patterns2of5[dSpace];

                for (int e = 0; e < 5; e++)
                {
                    runs.Add(barPat[e] == 1 ? wideWidth : narrowWidth);   // Bar
                    runs.Add(spacePat[e] == 1 ? wideWidth : narrowWidth); // Space
                }
            }

            // 3. Stop Pattern: W bar, N space, N bar
            runs.Add(wideWidth);   // bar
            runs.Add(narrowWidth); // space
            runs.Add(narrowWidth); // bar

            return runs;
        }

        /// <summary>
        /// Generates a 2D symbol bitmap grid with quiet zones and optional bearer bars.
        /// </summary>
        public static bool[,] EncodeSymbol(string digits, int quietZone = 20, int height = 60, bool withBearerBars = true)
        {
            List<int> runs = EncodeRuns(digits);
            int codeWidth = 0;
            foreach (var r in runs) codeWidth += r;

            int totalWidth = codeWidth + (quietZone * 2);
            int bearerThickness = withBearerBars ? 4 : 0;
            int totalHeight = height + (bearerThickness * 2);

            bool[,] grid = new bool[totalHeight, totalWidth];

            // Top Bearer Bar
            if (withBearerBars)
            {
                for (int y = 0; y < bearerThickness; y++)
                    for (int x = 0; x < totalWidth; x++) grid[y, x] = true;
            }

            // Code lines
            int startY = bearerThickness;
            int endY = totalHeight - bearerThickness;

            for (int y = startY; y < endY; y++)
            {
                int curX = quietZone;
                bool isBlack = true;
                for (int r = 0; r < runs.Count; r++)
                {
                    int w = runs[r];
                    if (isBlack)
                    {
                        for (int i = 0; i < w; i++) grid[y, curX + i] = true;
                    }
                    curX += w;
                    isBlack = !isBlack;
                }
            }

            // Bottom Bearer Bar
            if (withBearerBars)
            {
                for (int y = totalHeight - bearerThickness; y < totalHeight; y++)
                    for (int x = 0; x < totalWidth; x++) grid[y, x] = true;
            }

            return grid;
        }
    }
}
