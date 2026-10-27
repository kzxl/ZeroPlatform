using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# industrial Code 128 and GS1-128 barcode encoder.
    /// Supports automatic Code Set switching (A, B, C) to minimize physical symbol width,
    /// FNC1 delimiter support for GS1 Application Identifiers, and Modulo 103 checksum calculation.
    /// </summary>
    public static class Code128Encoder
    {
        // 107 standard Code 128 patterns (0..105: 6 elements / 11 modules, 106: 7 elements / 13 modules)
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
            new byte[] { 1, 1, 3, 1, 4, 1 }, // 99 (Code C in A/B)
            new byte[] { 1, 1, 4, 1, 3, 1 }, // 100 (Code B in A/C)
            new byte[] { 3, 1, 1, 1, 4, 1 }, // 101 (Code A in B/C)
            new byte[] { 4, 1, 1, 1, 3, 1 }, // 102 (FNC1)
            new byte[] { 2, 1, 1, 4, 1, 2 }, // 103 (Start A)
            new byte[] { 2, 1, 1, 2, 1, 4 }, // 104 (Start B)
            new byte[] { 2, 1, 1, 2, 3, 2 }, // 105 (Start C)
            new byte[] { 2, 3, 3, 1, 1, 1, 2 } // 106 (Stop: 7 elements, 13 modules)
        };

        public const int CodeSetA = 1;
        public const int CodeSetB = 2;
        public const int CodeSetC = 3;

        public const int CodeCodeC = 99;
        public const int CodeCodeB = 100;
        public const int CodeCodeA = 101;
        public const int CodeFnc1 = 102;
        public const int CodeStartA = 103;
        public const int CodeStartB = 104;
        public const int CodeStartC = 105;
        public const int CodeStop = 106;

        /// <summary>
        /// Encodes an ASCII or numeric payload into an optimized sequence of Code 128 symbol values.
        /// Automatically selects Code Sets A, B, and C to minimize physical symbol width.
        /// </summary>
        public static List<int> GetSymbolValues(string text, bool isGs1 = false)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException(nameof(text), "Barcode payload cannot be empty.");

            var symbols = new List<int>();
            int cursor = 0;
            int length = text.Length;

            // 1. Determine Initial Code Set
            int currentSet;
            if (CountConsecutiveDigits(text, 0) >= 4)
            {
                currentSet = CodeSetC;
                symbols.Add(CodeStartC);
            }
            else if (HasControlCharacters(text, 0, Math.Min(4, length)))
            {
                currentSet = CodeSetA;
                symbols.Add(CodeStartA);
            }
            else
            {
                currentSet = CodeSetB;
                symbols.Add(CodeStartB);
            }

            // GS1-128 mandatory FNC1 immediately following Start pattern
            if (isGs1)
            {
                symbols.Add(CodeFnc1);
            }

            // 2. Encode Payload with Dynamic Code Set Switching
            while (cursor < length)
            {
                if (currentSet == CodeSetC)
                {
                    int digitCount = CountConsecutiveDigits(text, cursor);
                    if (digitCount >= 2)
                    {
                        int val = (text[cursor] - '0') * 10 + (text[cursor + 1] - '0');
                        symbols.Add(val);
                        cursor += 2;
                    }
                    else
                    {
                        // Cannot continue in Code Set C with fewer than 2 digits
                        char c = text[cursor];
                        if (c < 32)
                        {
                            currentSet = CodeSetA;
                            symbols.Add(CodeCodeA);
                        }
                        else
                        {
                            currentSet = CodeSetB;
                            symbols.Add(CodeCodeB);
                        }
                    }
                }
                else if (currentSet == CodeSetB)
                {
                    // Check if we should switch to Code Set C (4 or more digits)
                    if (CountConsecutiveDigits(text, cursor) >= 4)
                    {
                        currentSet = CodeSetC;
                        symbols.Add(CodeCodeC);
                        continue;
                    }

                    char c = text[cursor];
                    if (c == 0x00F1 || c == '\u001D' || c == 29) // FNC1 separator / GS
                    {
                        symbols.Add(CodeFnc1);
                        cursor++;
                    }
                    else if (c < 32)
                    {
                        // Switch to Code Set A for ASCII control characters
                        currentSet = CodeSetA;
                        symbols.Add(CodeCodeA);
                        symbols.Add(c + 64);
                        cursor++;
                    }
                    else if (c <= 127)
                    {
                        symbols.Add(c - 32);
                        cursor++;
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported non-ASCII character '{(int)c}' at position {cursor}.");
                    }
                }
                else // CodeSetA
                {
                    // Check if we should switch to Code Set C
                    if (CountConsecutiveDigits(text, cursor) >= 4)
                    {
                        currentSet = CodeSetC;
                        symbols.Add(CodeCodeC);
                        continue;
                    }

                    char c = text[cursor];
                    if (c == 0x00F1 || c == '\u001D' || c == 29) // FNC1 separator
                    {
                        symbols.Add(CodeFnc1);
                        cursor++;
                    }
                    else if (c >= 96 && c <= 127)
                    {
                        // Lowercase characters require Code Set B
                        currentSet = CodeSetB;
                        symbols.Add(CodeCodeB);
                        symbols.Add(c - 32);
                        cursor++;
                    }
                    else if (c < 32)
                    {
                        symbols.Add(c + 64);
                        cursor++;
                    }
                    else if (c < 96)
                    {
                        symbols.Add(c - 32);
                        cursor++;
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported character '{(int)c}' at position {cursor}.");
                    }
                }
            }

            // 3. Compute Modulo 103 Checksum
            int sum = symbols[0];
            for (int i = 1; i < symbols.Count; i++)
            {
                sum += symbols[i] * i;
            }
            int checksum = sum % 103;
            symbols.Add(checksum);

            // 4. Append Stop Pattern
            symbols.Add(CodeStop);

            return symbols;
        }

        /// <summary>
        /// Encodes payload text into a 1D boolean module array (true = bar, false = space).
        /// </summary>
        public static bool[] Encode(string text, bool isGs1 = false)
        {
            List<int> symbols = GetSymbolValues(text, isGs1);

            // Calculate total module count:
            // Standard symbols (N-1): 11 modules each.
            // Stop pattern: 13 modules.
            int totalModules = ((symbols.Count - 1) * 11) + 13;
            bool[] modules = new bool[totalModules];

            int cursor = 0;
            for (int s = 0; s < symbols.Count; s++)
            {
                byte[] pattern = Patterns[symbols[s]];
                bool isBar = true;
                for (int e = 0; e < pattern.Length; e++)
                {
                    int width = pattern[e];
                    for (int w = 0; w < width; w++)
                    {
                        modules[cursor++] = isBar;
                    }
                    isBar = !isBar;
                }
            }

            return modules;
        }

        /// <summary>
        /// Convenience method to encode standard GS1-128 format.
        /// </summary>
        public static bool[] EncodeGs1(string text) => Encode(text, isGs1: true);

        /// <summary>
        /// Generates a 2D symbol bitmap grid with quiet zones and vertical height.
        /// </summary>
        public static bool[,] EncodeSymbol(string text, bool isGs1 = false, int quietZoneModules = 10, int height = 60)
        {
            bool[] modules = Encode(text, isGs1);
            int totalWidth = modules.Length + (quietZoneModules * 2);
            bool[,] grid = new bool[height, totalWidth];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < modules.Length; x++)
                {
                    grid[y, quietZoneModules + x] = modules[x];
                }
            }

            return grid;
        }

        private static int CountConsecutiveDigits(string text, int startIndex)
        {
            int count = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                    count++;
                else
                    break;
            }
            return count;
        }

        private static bool HasControlCharacters(string text, int startIndex, int length)
        {
            for (int i = 0; i < length && (startIndex + i) < text.Length; i++)
            {
                if (text[startIndex + i] < 32)
                    return true;
            }
            return false;
        }
    }
}
