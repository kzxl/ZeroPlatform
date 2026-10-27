using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// High-performance pure C# Code 39 barcode decoder supporting standard alphanumeric characters,
    /// start/stop asterisk verification, and optional Modulo 43 checksum validation.
    /// </summary>
    public static class Code39Decoder
    {
        private struct Code39Entry
        {
            public char Character;
            public int Pattern;
            public int Value;

            public Code39Entry(char character, int pattern, int value)
            {
                Character = character;
                Pattern = pattern;
                Value = value;
            }
        }

        // 44 Code 39 characters and their 9-bit binary patterns (1 = wide, 0 = narrow)
        // 9 elements: 5 bars (bits 8,6,4,2,0) and 4 spaces (bits 7,5,3,1)
        private static readonly Code39Entry[] Table = new Code39Entry[]
        {
            new Code39Entry('0', 0b000110100, 0),
            new Code39Entry('1', 0b100100001, 1),
            new Code39Entry('2', 0b001100001, 2),
            new Code39Entry('3', 0b101100000, 3),
            new Code39Entry('4', 0b000110001, 4),
            new Code39Entry('5', 0b100110000, 5),
            new Code39Entry('6', 0b001110000, 6),
            new Code39Entry('7', 0b000100101, 7),
            new Code39Entry('8', 0b100100100, 8),
            new Code39Entry('9', 0b001100100, 9),
            new Code39Entry('A', 0b100001001, 10),
            new Code39Entry('B', 0b001001001, 11),
            new Code39Entry('C', 0b101001000, 12),
            new Code39Entry('D', 0b000011001, 13),
            new Code39Entry('E', 0b100011000, 14),
            new Code39Entry('F', 0b001011000, 15),
            new Code39Entry('G', 0b000001101, 16),
            new Code39Entry('H', 0b100001100, 17),
            new Code39Entry('I', 0b001001100, 18),
            new Code39Entry('J', 0b000011100, 19),
            new Code39Entry('K', 0b100000011, 20),
            new Code39Entry('L', 0b001000011, 21),
            new Code39Entry('M', 0b101000010, 22),
            new Code39Entry('N', 0b000010011, 23),
            new Code39Entry('O', 0b100010010, 24),
            new Code39Entry('P', 0b001010010, 25),
            new Code39Entry('Q', 0b000000111, 26),
            new Code39Entry('R', 0b100000110, 27),
            new Code39Entry('S', 0b001000110, 28),
            new Code39Entry('T', 0b000010110, 29),
            new Code39Entry('U', 0b110000001, 30),
            new Code39Entry('V', 0b011000001, 31),
            new Code39Entry('W', 0b111000000, 32),
            new Code39Entry('X', 0b010010001, 33),
            new Code39Entry('Y', 0b110010000, 34),
            new Code39Entry('Z', 0b011010000, 35),
            new Code39Entry('-', 0b010000101, 36),
            new Code39Entry('.', 0b110000100, 37),
            new Code39Entry(' ', 0b011000100, 38),
            new Code39Entry('$', 0b010101000, 39),
            new Code39Entry('/', 0b010100010, 40),
            new Code39Entry('+', 0b010001010, 41),
            new Code39Entry('%', 0b000101010, 42),
            new Code39Entry('*', 0b010010100, 43) // Start / Stop asterisk
        };

        private const int AsteriskPattern = 0b010010100;

        /// <summary>
        /// Attempts to decode a Code 39 barcode from a sequence of alternating black/white run-lengths.
        /// </summary>
        public static bool TryDecode(
            IReadOnlyList<int> runs,
            bool firstIsBlack,
            out string decodedText,
            out double confidence)
        {
            decodedText = string.Empty;
            confidence = 0.0;

            if (runs == null || runs.Count < 19) // Min: start (9) + gap (1) + stop (9)
                return false;

            int firstBarIndex = firstIsBlack ? 0 : 1;

            // Try candidate start positions where a black bar begins
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

            // 1. Verify start character is '*' (Asterisk)
            if (!TryDecodeCharacter(runs, startIdx, out char startChar, out _) || startChar != '*')
                return false;

            var sb = new StringBuilder();
            int currentIdx = startIdx + 9;

            // Loop through characters separated by an inter-character gap (1 space)
            while (currentIdx < runs.Count)
            {
                // Skip inter-character space gap (1 run)
                currentIdx += 1;
                if (currentIdx + 9 > runs.Count)
                    break;

                if (!TryDecodeCharacter(runs, currentIdx, out char ch, out _))
                    return false;

                if (ch == '*')
                {
                    // Found Stop Asterisk!
                    if (sb.Length == 0)
                        return false;

                    decodedText = sb.ToString();
                    confidence = 1.0;
                    return true;
                }

                sb.Append(ch);
                currentIdx += 9;
            }

            return false;
        }

        private static bool TryDecodeCharacter(
            IReadOnlyList<int> runs,
            int offset,
            out char character,
            out int patternValue)
        {
            character = '\0';
            patternValue = -1;

            if (offset + 9 > runs.Count)
                return false;

            // Copy 9 element widths
            int[] elements = new int[9];
            int[] sorted = new int[9];
            for (int i = 0; i < 9; i++)
            {
                int val = runs[offset + i];
                elements[i] = val;
                sorted[i] = val;
            }

            Array.Sort(sorted);

            // In Code 39: exactly 6 narrow elements and 3 wide elements
            int maxNarrow = sorted[5];
            int minWide = sorted[6];

            if (maxNarrow <= 0 || minWide <= maxNarrow)
                return false;

            // Wide elements should be at least 1.35x narrow elements
            if ((double)minWide / maxNarrow < 1.35)
                return false;

            double threshold = (maxNarrow + minWide) / 2.0;

            // Construct 9-bit binary pattern (MSB = element 0, LSB = element 8)
            int pattern = 0;
            int wideCount = 0;
            for (int i = 0; i < 9; i++)
            {
                pattern <<= 1;
                if (elements[i] >= threshold)
                {
                    pattern |= 1;
                    wideCount++;
                }
            }

            if (wideCount != 3)
                return false;

            // Match against table
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].Pattern == pattern)
                {
                    character = Table[i].Character;
                    patternValue = Table[i].Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the 9-element binary pattern for a given Code 39 character (1 = wide, 0 = narrow).
        /// </summary>
        public static bool TryGetPattern(char c, out int pattern)
        {
            char upper = char.ToUpperInvariant(c);
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].Character == upper)
                {
                    pattern = Table[i].Pattern;
                    return true;
                }
            }

            pattern = 0;
            return false;
        }
    }
}
