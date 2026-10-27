using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# high-speed EAN-13 and UPC-A barcode decoder.
    /// Performs robust guard-pattern candidate search, sub-module normalization,
    /// L/G parity decomposition, and GS1 Modulo-10 checksum validation.
    /// </summary>
    public static class Ean13Decoder
    {
        // 4-element run patterns for L-set (Space, Bar, Space, Bar) summing to 7 modules
        private static readonly int[][] L_Runs = new int[][]
        {
            new int[] { 3, 2, 1, 1 }, // 0
            new int[] { 2, 2, 2, 1 }, // 1
            new int[] { 2, 1, 2, 2 }, // 2
            new int[] { 1, 4, 1, 1 }, // 3
            new int[] { 1, 1, 3, 2 }, // 4
            new int[] { 1, 2, 3, 1 }, // 5
            new int[] { 1, 1, 1, 4 }, // 6
            new int[] { 1, 3, 1, 2 }, // 7
            new int[] { 1, 2, 1, 3 }, // 8
            new int[] { 3, 1, 1, 2 }  // 9
        };

        // 4-element run patterns for G-set (Space, Bar, Space, Bar) summing to 7 modules
        private static readonly int[][] G_Runs = new int[][]
        {
            new int[] { 1, 1, 2, 3 }, // 0
            new int[] { 1, 2, 2, 2 }, // 1
            new int[] { 2, 2, 1, 2 }, // 2
            new int[] { 1, 1, 4, 1 }, // 3
            new int[] { 2, 3, 1, 1 }, // 4
            new int[] { 1, 3, 2, 1 }, // 5
            new int[] { 4, 1, 1, 1 }, // 6
            new int[] { 2, 1, 3, 1 }, // 7
            new int[] { 3, 1, 2, 1 }, // 8
            new int[] { 2, 1, 1, 3 }  // 9
        };

        // First digit parity lookup table (index 0..9)
        private static readonly string[] FirstDigitParities = new[]
        {
            "LLLLLL", // 0
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
        /// Attempts to decode an EAN-13 or UPC-A barcode from a list of alternating black/white run lengths.
        /// </summary>
        public static bool TryDecode(
            List<int> runs,
            bool firstIsBlack,
            out string text,
            out double confidence)
        {
            text = string.Empty;
            confidence = 0.0;

            if (runs == null || runs.Count < 59)
                return false;

            // Search for Start Guard candidate: Bar (1), Space (1), Bar (1) -> 3 runs
            for (int i = 0; i <= runs.Count - 59; i++)
            {
                bool isBlack = (i % 2 == 0) ? firstIsBlack : !firstIsBlack;
                if (!isBlack) continue; // Start guard must begin with a Black bar

                int g1 = runs[i];
                int g2 = runs[i + 1];
                int g3 = runs[i + 2];

                double moduleSize = (g1 + g2 + g3) / 3.0;
                if (moduleSize < 0.5) continue;

                // Validate start guard uniformity
                if (!IsSimilar(g1, moduleSize, 0.7) ||
                    !IsSimilar(g2, moduleSize, 0.7) ||
                    !IsSimilar(g3, moduleSize, 0.7))
                    continue;

                // Check center guard at offset i + 3 + 24 = i + 27 (Space, Bar, Space, Bar, Space)
                int cIdx = i + 27;
                if (!IsSimilar(runs[cIdx], moduleSize, 0.8) ||
                    !IsSimilar(runs[cIdx + 1], moduleSize, 0.8) ||
                    !IsSimilar(runs[cIdx + 2], moduleSize, 0.8) ||
                    !IsSimilar(runs[cIdx + 3], moduleSize, 0.8) ||
                    !IsSimilar(runs[cIdx + 4], moduleSize, 0.8))
                    continue;

                // Check end guard at offset i + 27 + 5 + 24 = i + 56 (Bar, Space, Bar)
                int eIdx = i + 56;
                if (!IsSimilar(runs[eIdx], moduleSize, 0.8) ||
                    !IsSimilar(runs[eIdx + 1], moduleSize, 0.8) ||
                    !IsSimilar(runs[eIdx + 2], moduleSize, 0.8))
                    continue;

                // Guard patterns validated! Decode 6 left digits
                char[] leftParity = new char[6];
                int[] leftDigits = new int[6];
                bool leftOk = true;

                for (int d = 0; d < 6; d++)
                {
                    int rIdx = i + 3 + (d * 4);
                    if (!DecodeLeftDigit(runs, rIdx, out int digit, out char parity))
                    {
                        leftOk = false;
                        break;
                    }
                    leftDigits[d] = digit;
                    leftParity[d] = parity;
                }

                if (!leftOk) continue;

                // Determine first digit from parity sequence
                string parityStr = new string(leftParity);
                int firstDigit = -1;
                for (int p = 0; p < 10; p++)
                {
                    if (FirstDigitParities[p] == parityStr)
                    {
                        firstDigit = p;
                        break;
                    }
                }

                if (firstDigit == -1) continue;

                // Decode 6 right digits (offset i + 32)
                int[] rightDigits = new int[6];
                bool rightOk = true;

                for (int d = 0; d < 6; d++)
                {
                    int rIdx = i + 32 + (d * 4);
                    if (!DecodeRightDigit(runs, rIdx, out int digit))
                    {
                        rightOk = false;
                        break;
                    }
                    rightDigits[d] = digit;
                }

                if (!rightOk) continue;

                // Assemble 13 digits
                var sb = new StringBuilder(13);
                sb.Append(firstDigit);
                for (int d = 0; d < 6; d++) sb.Append(leftDigits[d]);
                for (int d = 0; d < 6; d++) sb.Append(rightDigits[d]);

                string candidateText = sb.ToString();

                // Validate Modulo-10 checksum
                int expectedCheck = Ean13Encoder.CalculateChecksum(candidateText.Substring(0, 12));
                int actualCheck = candidateText[12] - '0';

                if (expectedCheck == actualCheck)
                {
                    text = candidateText;
                    confidence = 0.98;
                    return true;
                }
            }

            return false;
        }

        private static bool DecodeLeftDigit(List<int> runs, int startIndex, out int digit, out char parity)
        {
            digit = -1;
            parity = ' ';

            int r0 = runs[startIndex];
            int r1 = runs[startIndex + 1];
            int r2 = runs[startIndex + 2];
            int r3 = runs[startIndex + 3];

            double total = r0 + r1 + r2 + r3;
            if (total <= 0) return false;

            double bestDist = double.MaxValue;

            // Test against L-runs
            for (int d = 0; d < 10; d++)
            {
                double dist = ComputeRunDistance(r0, r1, r2, r3, total, L_Runs[d]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    digit = d;
                    parity = 'L';
                }
            }

            // Test against G-runs
            for (int d = 0; d < 10; d++)
            {
                double dist = ComputeRunDistance(r0, r1, r2, r3, total, G_Runs[d]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    digit = d;
                    parity = 'G';
                }
            }

            return bestDist < 1.4;
        }

        private static bool DecodeRightDigit(List<int> runs, int startIndex, out int digit)
        {
            digit = -1;

            int r0 = runs[startIndex];
            int r1 = runs[startIndex + 1];
            int r2 = runs[startIndex + 2];
            int r3 = runs[startIndex + 3];

            double total = r0 + r1 + r2 + r3;
            if (total <= 0) return false;

            // Right digits follow the same run lengths as L-patterns (just inverted polarity Bar-Space-Bar-Space)
            double bestDist = double.MaxValue;
            for (int d = 0; d < 10; d++)
            {
                double dist = ComputeRunDistance(r0, r1, r2, r3, total, L_Runs[d]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    digit = d;
                }
            }

            return bestDist < 1.4;
        }

        private static double ComputeRunDistance(int r0, int r1, int r2, int r3, double total, int[] expected7)
        {
            double n0 = (r0 * 7.0) / total;
            double n1 = (r1 * 7.0) / total;
            double n2 = (r2 * 7.0) / total;
            double n3 = (r3 * 7.0) / total;

            return Math.Abs(n0 - expected7[0]) +
                   Math.Abs(n1 - expected7[1]) +
                   Math.Abs(n2 - expected7[2]) +
                   Math.Abs(n3 - expected7[3]);
        }

        private static bool IsSimilar(double val, double target, double toleranceFraction)
        {
            return Math.Abs(val - target) <= (target * toleranceFraction);
        }
    }
}
