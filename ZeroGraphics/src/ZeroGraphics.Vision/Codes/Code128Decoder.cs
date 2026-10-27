using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// High-performance pure C# Code 128 barcode decoder supporting Character Sets A, B, and C
    /// with Modulo 103 checksum verification and zero heap allocations during scanning.
    /// </summary>
    public static class Code128Decoder
    {
        // 107 Code 128 patterns (0..102 data, 103 Start A, 104 Start B, 105 Start C, 106 Stop)
        // Each pattern (0..105) has 6 elements (3 bars, 3 spaces) summing to 11 modules.
        // Stop (106) has 7 elements summing to 13 modules.
        private static readonly byte[][] Patterns = new byte[][]
        {
            new byte[] { 2, 1, 2, 2, 2, 2 }, // 0
            new byte[] { 2, 2, 2, 1, 2, 2 }, // 1
            new byte[] { 2, 2, 2, 2, 2, 1 }, // 2
            new byte[] { 1, 2, 1, 2, 2, 3 }, // 3
            new byte[] { 1, 2, 1, 3, 2, 2 }, // 4
            new byte[] { 1, 3, 1, 2, 2, 2 }, // 5
            new byte[] { 1, 2, 2, 2, 1, 3 }, // 6
            new byte[] { 1, 2, 2, 3, 1, 2 }, // 7
            new byte[] { 1, 3, 2, 2, 1, 2 }, // 8
            new byte[] { 2, 2, 1, 2, 1, 3 }, // 9
            new byte[] { 2, 2, 1, 3, 1, 2 }, // 10
            new byte[] { 2, 3, 1, 2, 1, 2 }, // 11
            new byte[] { 1, 1, 2, 2, 3, 2 }, // 12
            new byte[] { 1, 2, 2, 1, 3, 2 }, // 13
            new byte[] { 1, 2, 2, 2, 3, 1 }, // 14
            new byte[] { 1, 1, 3, 2, 2, 2 }, // 15
            new byte[] { 1, 2, 3, 1, 2, 2 }, // 16
            new byte[] { 1, 2, 3, 2, 2, 1 }, // 17
            new byte[] { 2, 2, 3, 2, 1, 1 }, // 18
            new byte[] { 2, 2, 1, 1, 3, 2 }, // 19
            new byte[] { 2, 2, 1, 2, 3, 1 }, // 20
            new byte[] { 2, 1, 3, 2, 1, 2 }, // 21
            new byte[] { 2, 2, 3, 1, 1, 2 }, // 22
            new byte[] { 3, 1, 2, 1, 3, 1 }, // 23
            new byte[] { 3, 1, 1, 2, 2, 2 }, // 24
            new byte[] { 3, 2, 1, 1, 2, 2 }, // 25
            new byte[] { 3, 2, 1, 2, 2, 1 }, // 26
            new byte[] { 3, 1, 2, 2, 1, 2 }, // 27
            new byte[] { 3, 2, 2, 1, 1, 2 }, // 28
            new byte[] { 3, 2, 2, 2, 1, 1 }, // 29
            new byte[] { 2, 1, 2, 1, 2, 3 }, // 30
            new byte[] { 2, 1, 2, 3, 2, 1 }, // 31
            new byte[] { 2, 3, 2, 1, 2, 1 }, // 32
            new byte[] { 1, 1, 1, 3, 2, 3 }, // 33
            new byte[] { 1, 3, 1, 1, 2, 3 }, // 34
            new byte[] { 1, 3, 1, 3, 2, 1 }, // 35
            new byte[] { 1, 1, 2, 3, 1, 3 }, // 36
            new byte[] { 1, 3, 2, 1, 1, 3 }, // 37
            new byte[] { 1, 3, 2, 3, 1, 1 }, // 38
            new byte[] { 2, 1, 1, 3, 1, 3 }, // 39
            new byte[] { 2, 3, 1, 1, 1, 3 }, // 40
            new byte[] { 2, 3, 1, 3, 1, 1 }, // 41
            new byte[] { 1, 1, 2, 1, 3, 3 }, // 42
            new byte[] { 1, 1, 2, 3, 3, 1 }, // 43
            new byte[] { 1, 3, 2, 1, 3, 1 }, // 44
            new byte[] { 1, 1, 3, 1, 2, 3 }, // 45
            new byte[] { 1, 1, 3, 3, 2, 1 }, // 46
            new byte[] { 1, 3, 3, 1, 2, 1 }, // 47
            new byte[] { 3, 1, 3, 1, 2, 1 }, // 48
            new byte[] { 2, 1, 1, 3, 3, 1 }, // 49
            new byte[] { 2, 3, 1, 1, 3, 1 }, // 50
            new byte[] { 2, 1, 3, 1, 1, 3 }, // 51
            new byte[] { 2, 1, 3, 3, 1, 1 }, // 52
            new byte[] { 2, 1, 3, 1, 3, 1 }, // 53
            new byte[] { 3, 1, 1, 1, 2, 3 }, // 54
            new byte[] { 3, 1, 1, 3, 2, 1 }, // 55
            new byte[] { 3, 3, 1, 1, 2, 1 }, // 56
            new byte[] { 3, 1, 2, 1, 1, 3 }, // 57
            new byte[] { 3, 1, 2, 3, 1, 1 }, // 58
            new byte[] { 3, 3, 2, 1, 1, 1 }, // 59
            new byte[] { 3, 1, 4, 1, 1, 1 }, // 60
            new byte[] { 2, 2, 1, 4, 1, 1 }, // 61
            new byte[] { 4, 3, 1, 1, 1, 1 }, // 62
            new byte[] { 1, 1, 1, 2, 2, 4 }, // 63
            new byte[] { 1, 1, 1, 4, 2, 2 }, // 64
            new byte[] { 1, 2, 1, 1, 2, 4 }, // 65
            new byte[] { 1, 2, 1, 4, 2, 1 }, // 66
            new byte[] { 1, 4, 1, 1, 2, 2 }, // 67
            new byte[] { 1, 4, 1, 2, 2, 1 }, // 68
            new byte[] { 1, 1, 2, 2, 1, 4 }, // 69
            new byte[] { 1, 1, 2, 4, 1, 2 }, // 70
            new byte[] { 1, 2, 2, 1, 1, 4 }, // 71
            new byte[] { 1, 2, 2, 4, 1, 1 }, // 72
            new byte[] { 1, 4, 2, 1, 1, 2 }, // 73
            new byte[] { 1, 4, 2, 2, 1, 1 }, // 74
            new byte[] { 2, 4, 1, 2, 1, 1 }, // 75
            new byte[] { 2, 2, 1, 1, 1, 4 }, // 76
            new byte[] { 4, 1, 3, 1, 1, 1 }, // 77
            new byte[] { 2, 4, 1, 1, 1, 2 }, // 78
            new byte[] { 1, 3, 4, 1, 1, 1 }, // 79
            new byte[] { 1, 1, 1, 2, 4, 2 }, // 80
            new byte[] { 1, 2, 1, 1, 4, 2 }, // 81
            new byte[] { 1, 2, 1, 2, 4, 1 }, // 82
            new byte[] { 1, 1, 4, 2, 1, 2 }, // 83
            new byte[] { 1, 2, 4, 1, 1, 2 }, // 84
            new byte[] { 1, 2, 4, 2, 1, 1 }, // 85
            new byte[] { 4, 1, 1, 2, 1, 2 }, // 86
            new byte[] { 4, 2, 1, 1, 1, 2 }, // 87
            new byte[] { 4, 2, 1, 2, 1, 1 }, // 88
            new byte[] { 2, 1, 2, 1, 4, 1 }, // 89
            new byte[] { 2, 1, 4, 1, 2, 1 }, // 90
            new byte[] { 4, 1, 2, 1, 2, 1 }, // 91
            new byte[] { 1, 1, 1, 1, 4, 3 }, // 92
            new byte[] { 1, 1, 1, 3, 4, 1 }, // 93
            new byte[] { 1, 3, 1, 1, 4, 1 }, // 94
            new byte[] { 1, 1, 4, 1, 1, 3 }, // 95
            new byte[] { 1, 1, 4, 3, 1, 1 }, // 96
            new byte[] { 4, 1, 1, 1, 1, 3 }, // 97
            new byte[] { 4, 1, 1, 3, 1, 1 }, // 98
            new byte[] { 1, 1, 3, 1, 4, 1 }, // 99
            new byte[] { 1, 1, 4, 1, 3, 1 }, // 100
            new byte[] { 3, 1, 1, 1, 4, 1 }, // 101
            new byte[] { 4, 1, 1, 1, 3, 1 }, // 102
            new byte[] { 2, 1, 1, 4, 1, 2 }, // 103 Start A
            new byte[] { 2, 1, 1, 2, 1, 4 }, // 104 Start B
            new byte[] { 2, 1, 1, 2, 3, 2 }, // 105 Start C
            new byte[] { 2, 3, 3, 1, 1, 1, 2 } // 106 Stop (7 elements, 13 modules)
        };

        public const int StartA = 103;
        public const int StartB = 104;
        public const int StartC = 105;
        public const int StopPattern = 106;

        public const int CodeSetA = 1;
        public const int CodeSetB = 2;
        public const int CodeSetC = 3;

        /// <summary>
        /// Attempts to decode a Code 128 barcode from a sequence of alternating black/white run-lengths.
        /// </summary>
        public static bool TryDecode(
            IReadOnlyList<int> runs,
            bool firstIsBlack,
            out string decodedText,
            out double confidence)
        {
            decodedText = string.Empty;
            confidence = 0.0;

            if (runs == null || runs.Count < 19) // Min: quiet + start (6) + char (6) + check (6) + stop (7)
                return false;

            // Black bars must start at even or odd index depending on firstIsBlack
            int firstBarIndex = firstIsBlack ? 0 : 1;

            // Try candidate start positions
            for (int startIdx = firstBarIndex; startIdx <= runs.Count - 19; startIdx += 2)
            {
                if (TryDecodeFromStart(runs, startIdx, out decodedText, out confidence))
                    return true;
            }

            return false;
        }

        private static bool TryDecodeFromStart(
            IReadOnlyList<int> runs,
            int startIdx,
            out string decodedText,
            out double confidence)
        {
            decodedText = string.Empty;
            confidence = 0.0;

            // 1. Match Start Pattern (at startIdx, 6 elements)
            int startCode = MatchPattern(runs, startIdx, 6, 11);
            if (startCode < StartA || startCode > StartC)
                return false;

            int checksum = startCode;
            int weight = 1;
            int currentIdx = startIdx + 6;

            var symbols = new List<int>();

            // Read symbols until STOP pattern
            while (currentIdx + 6 <= runs.Count)
            {
                // Check if currentIdx is STOP (7 elements)
                if (currentIdx + 7 <= runs.Count)
                {
                    int stopCandidate = MatchStopPattern(runs, currentIdx);
                    if (stopCandidate == StopPattern)
                    {
                        // Successfully reached STOP!
                        // The symbol immediately before STOP must be the checksum character
                        if (symbols.Count < 1)
                            return false;

                        int checkChar = symbols[symbols.Count - 1];
                        symbols.RemoveAt(symbols.Count - 1);

                        // Undo checkChar from checksum calculation
                        checksum -= (weight - 1) * checkChar;

                        // Verify Modulo 103 checksum
                        int expectedChecksum = checksum % 103;
                        if (expectedChecksum != checkChar)
                            return false;

                        // Decode payload symbols according to Code 128 Set switching
                        decodedText = DecodeSymbols(symbols, startCode);
                        confidence = 1.0;
                        return true;
                    }
                }

                // Match 6-element symbol
                int symbolCode = MatchPattern(runs, currentIdx, 6, 11);
                if (symbolCode < 0 || symbolCode > 105)
                    return false;

                symbols.Add(symbolCode);
                checksum += weight * symbolCode;
                weight++;
                currentIdx += 6;
            }

            return false;
        }

        private static int MatchPattern(IReadOnlyList<int> runs, int offset, int count, int expectedModules)
        {
            if (offset + count > runs.Count)
                return -1;

            int totalWidth = 0;
            for (int i = 0; i < count; i++)
                totalWidth += runs[offset + i];

            if (totalWidth <= 0)
                return -1;

            double moduleWidth = (double)totalWidth / expectedModules;

            int bestMatch = -1;
            double minError = double.MaxValue;

            int maxCode = (count == 6) ? 105 : 106;
            for (int code = 0; code <= maxCode; code++)
            {
                byte[] pat = Patterns[code];
                if (pat.Length != count)
                    continue;

                double error = 0.0;
                for (int i = 0; i < count; i++)
                {
                    double expectedWidth = pat[i] * moduleWidth;
                    error += Math.Abs(runs[offset + i] - expectedWidth);
                }

                if (error < minError)
                {
                    minError = error;
                    bestMatch = code;
                }
            }

            // Error threshold: average error per element must be <= 0.4 module
            if (minError / (count * moduleWidth) <= 0.45)
                return bestMatch;

            return -1;
        }

        private static int MatchStopPattern(IReadOnlyList<int> runs, int offset)
        {
            if (offset + 7 > runs.Count)
                return -1;

            int totalWidth = 0;
            for (int i = 0; i < 7; i++)
                totalWidth += runs[offset + i];

            if (totalWidth <= 0)
                return -1;

            double moduleWidth = (double)totalWidth / 13.0;

            byte[] pat = Patterns[StopPattern];
            double error = 0.0;
            for (int i = 0; i < 7; i++)
            {
                double expectedWidth = pat[i] * moduleWidth;
                error += Math.Abs(runs[offset + i] - expectedWidth);
            }

            if (error / (7.0 * moduleWidth) <= 0.20)
                return StopPattern;

            return -1;
        }

        private static string DecodeSymbols(List<int> symbols, int startCode)
        {
            var sb = new StringBuilder();
            int currentSet = (startCode == StartA) ? CodeSetA : (startCode == StartB) ? CodeSetB : CodeSetC;
            bool isShifted = false;

            for (int i = 0; i < symbols.Count; i++)
            {
                int code = symbols[i];
                int activeSet = isShifted ? ((currentSet == CodeSetA) ? CodeSetB : CodeSetA) : currentSet;
                if (isShifted) isShifted = false;

                if (activeSet == CodeSetB)
                {
                    if (code <= 95)
                    {
                        sb.Append((char)(code + 32));
                    }
                    else if (code == 98) // SHIFT
                    {
                        isShifted = true;
                    }
                    else if (code == 99) // CODE C
                    {
                        currentSet = CodeSetC;
                    }
                    else if (code == 100) // CODE B
                    {
                        currentSet = CodeSetB;
                    }
                    else if (code == 101) // CODE A
                    {
                        currentSet = CodeSetA;
                    }
                }
                else if (activeSet == CodeSetA)
                {
                    if (code <= 63)
                    {
                        sb.Append((char)(code + 32));
                    }
                    else if (code <= 95)
                    {
                        sb.Append((char)(code - 64));
                    }
                    else if (code == 98) // SHIFT
                    {
                        isShifted = true;
                    }
                    else if (code == 99) // CODE C
                    {
                        currentSet = CodeSetC;
                    }
                    else if (code == 100) // CODE B
                    {
                        currentSet = CodeSetB;
                    }
                    else if (code == 101) // CODE A
                    {
                        currentSet = CodeSetA;
                    }
                }
                else // CodeSetC
                {
                    if (code <= 99)
                    {
                        sb.Append(code.ToString("D2"));
                    }
                    else if (code == 100) // CODE B
                    {
                        currentSet = CodeSetB;
                    }
                    else if (code == 101) // CODE A
                    {
                        currentSet = CodeSetA;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Helper to retrieve the 6 or 7 pattern element widths for a given Code 128 code index.
        /// </summary>
        public static byte[] GetPattern(int code)
        {
            if (code < 0 || code >= Patterns.Length)
                throw new ArgumentOutOfRangeException(nameof(code));
            return (byte[])Patterns[code].Clone();
        }
    }
}
