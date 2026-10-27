using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# ITF-14 (Interleaved 2 of 5) barcode decoder.
    /// Extracts interleaved digit pairs, detects start/stop guards, and verifies GS1 Modulo-10 checksums.
    /// </summary>
    public static class Itf14Decoder
    {
        // 5-element weights: 2 wide (1), 3 narrow (0)
        private static readonly int[][] Patterns2of5 = new int[][]
        {
            new int[] { 0, 0, 1, 1, 0 }, // 0
            new int[] { 1, 0, 0, 0, 1 }, // 1
            new int[] { 0, 1, 0, 0, 1 }, // 2
            new int[] { 1, 1, 0, 0, 0 }, // 3
            new int[] { 0, 0, 1, 0, 1 }, // 4
            new int[] { 1, 0, 1, 0, 0 }, // 5
            new int[] { 0, 1, 1, 0, 0 }, // 6
            new int[] { 0, 0, 0, 1, 1 }, // 7
            new int[] { 1, 0, 0, 1, 0 }, // 8
            new int[] { 0, 1, 0, 1, 0 }  // 9
        };

        /// <summary>
        /// Attempts to decode an ITF-14 barcode from alternating black/white run lengths.
        /// </summary>
        public static bool TryDecode(
            List<int> runs,
            bool firstIsBlack,
            out string text,
            out double confidence)
        {
            text = string.Empty;
            confidence = 0.0;

            // ITF-14 requires 4 (start) + 70 (data: 7 pairs * 10) + 3 (stop) = 77 runs
            if (runs == null || runs.Count < 77)
                return false;

            for (int i = 0; i <= runs.Count - 77; i++)
            {
                bool isBlack = (i % 2 == 0) ? firstIsBlack : !firstIsBlack;
                if (!isBlack) continue; // Start guard begins with Black Bar

                // 1. Check Start Pattern: N, N, N, N (Bar, Space, Bar, Space)
                int s1 = runs[i];
                int s2 = runs[i + 1];
                int s3 = runs[i + 2];
                int s4 = runs[i + 3];

                double narrowUnit = (s1 + s2 + s3 + s4) / 4.0;
                if (narrowUnit < 0.5) continue;

                if (!IsSimilar(s1, narrowUnit, 0.6) ||
                    !IsSimilar(s2, narrowUnit, 0.6) ||
                    !IsSimilar(s3, narrowUnit, 0.6) ||
                    !IsSimilar(s4, narrowUnit, 0.6))
                    continue;

                // 2. Check Stop Pattern at offset i + 4 + 70 = i + 74: W bar, N space, N bar
                int stopWideBar = runs[i + 74];
                int stopNarrowSpace = runs[i + 75];
                int stopNarrowBar = runs[i + 76];

                if (stopWideBar <= narrowUnit * 1.4 ||
                    !IsSimilar(stopNarrowSpace, narrowUnit, 0.7) ||
                    !IsSimilar(stopNarrowBar, narrowUnit, 0.7))
                    continue;

                // 3. Decode 7 Interleaved Digit Pairs (14 digits)
                char[] digits = new char[14];
                bool dataOk = true;

                for (int p = 0; p < 7; p++)
                {
                    int baseIdx = i + 4 + (p * 10);

                    // 5 Bars (dBar)
                    int[] barRuns = new int[5]
                    {
                        runs[baseIdx],
                        runs[baseIdx + 2],
                        runs[baseIdx + 4],
                        runs[baseIdx + 6],
                        runs[baseIdx + 8]
                    };

                    // 5 Spaces (dSpace)
                    int[] spaceRuns = new int[5]
                    {
                        runs[baseIdx + 1],
                        runs[baseIdx + 3],
                        runs[baseIdx + 5],
                        runs[baseIdx + 7],
                        runs[baseIdx + 9]
                    };

                    if (!DecodeDigit5(barRuns, out int digit1) || !DecodeDigit5(spaceRuns, out int digit2))
                    {
                        dataOk = false;
                        break;
                    }

                    digits[p * 2] = (char)('0' + digit1);
                    digits[p * 2 + 1] = (char)('0' + digit2);
                }

                if (!dataOk) continue;

                string candidate = new string(digits);

                // 4. Validate Modulo-10 Checksum
                int expectedChecksum = Itf14Encoder.CalculateChecksum(candidate.Substring(0, 13));
                int actualChecksum = candidate[13] - '0';

                if (expectedChecksum == actualChecksum)
                {
                    text = candidate;
                    confidence = 0.98;
                    return true;
                }
            }

            return false;
        }

        private static bool DecodeDigit5(int[] fiveRuns, out int digit)
        {
            digit = -1;

            // In 2 of 5, exactly 2 elements are Wide and 3 are Narrow
            // Find threshold by sorting runs
            int[] sorted = (int[])fiveRuns.Clone();
            Array.Sort(sorted);

            // sorted[0], sorted[1], sorted[2] are narrow
            // sorted[3], sorted[4] are wide
            // Threshold is midpoint between sorted[2] and sorted[3]
            double threshold = (sorted[2] + sorted[3]) / 2.0;
            if (sorted[3] <= sorted[2] * 1.2) // Wide must be clearly wider than Narrow
                return false;

            int[] bits = new int[5];
            int wideCount = 0;
            for (int i = 0; i < 5; i++)
            {
                if (fiveRuns[i] > threshold)
                {
                    bits[i] = 1;
                    wideCount++;
                }
                else
                {
                    bits[i] = 0;
                }
            }

            if (wideCount != 2) return false;

            for (int d = 0; d < 10; d++)
            {
                int[] pat = Patterns2of5[d];
                if (bits[0] == pat[0] &&
                    bits[1] == pat[1] &&
                    bits[2] == pat[2] &&
                    bits[3] == pat[3] &&
                    bits[4] == pat[4])
                {
                    digit = d;
                    return true;
                }
            }

            return false;
        }

        private static bool IsSimilar(double val, double target, double toleranceFraction)
        {
            return Math.Abs(val - target) <= (target * toleranceFraction);
        }
    }
}
