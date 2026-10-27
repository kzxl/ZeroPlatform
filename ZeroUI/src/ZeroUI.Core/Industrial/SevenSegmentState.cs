using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Industrial
{
    /// <summary>
    /// Preset color palettes for industrial 7-segment digital displays.
    /// </summary>
    public enum SevenSegmentColorPreset
    {
        Custom = 0,
        NeonEmerald = 1,
        NeonCyan = 2,
        NeonAmber = 3,
        NeonRed = 4,
        CrispWhite = 5,
        UltraViolet = 6
    }

    /// <summary>
    /// Display mode for leading zeros in numeric display values.
    /// </summary>
    public enum LeadingZeroDisplayMode
    {
        Blank = 0,
        DimmedGhost = 1,
        LitZero = 2
    }

    /// <summary>
    /// Frame and bezel styling for the LED acrylic enclosure.
    /// </summary>
    public enum SevenSegmentFrameStyle
    {
        RecessedBezel = 0,
        AcrylicGlass = 1,
        Borderless = 2
    }

    /// <summary>
    /// Item parsed from input text to display on a digital 7-segment module.
    /// </summary>
    public struct ParsedDisplayItem
    {
        public char Character;
        public bool HasDecimal;
        public bool IsColon;
        public bool IsDimmed;
    }

    /// <summary>
    /// Platform-neutral core state, bitmask pattern decoder, and color presets for 7-Segment Displays.
    /// </summary>
    public static class SevenSegmentState
    {
        private static readonly byte[] CharPatterns = new byte[128];

        static SevenSegmentState()
        {
            // Digits 0-9
            CharPatterns['0'] = 0x3F; // A B C D E F
            CharPatterns['1'] = 0x06; // B C
            CharPatterns['2'] = 0x5B; // A B D E G
            CharPatterns['3'] = 0x4F; // A B C D G
            CharPatterns['4'] = 0x66; // B C F G
            CharPatterns['5'] = 0x6D; // A C D F G
            CharPatterns['6'] = 0x7D; // A C D E F G
            CharPatterns['7'] = 0x07; // A B C
            CharPatterns['8'] = 0x7F; // A B C D E F G
            CharPatterns['9'] = 0x6F; // A B C D F G

            // Common Industrial Symbols
            CharPatterns['-'] = 0x40; // G
            CharPatterns['_'] = 0x08; // D
            CharPatterns['='] = 0x48; // D G
            CharPatterns[' '] = 0x00;
            CharPatterns['['] = 0x39; // A D E F
            CharPatterns[']'] = 0x0F; // A B C D

            // Letters for SCADA/MES telemetry status (STOP, RUN, PASS, FAIL, COOL, HEAT, ALARM, Err, OFF, ON)
            CharPatterns['A'] = 0x77; CharPatterns['a'] = 0x77;
            CharPatterns['B'] = 0x7C; CharPatterns['b'] = 0x7C; // b
            CharPatterns['C'] = 0x39; CharPatterns['c'] = 0x58; // C / c
            CharPatterns['D'] = 0x5E; CharPatterns['d'] = 0x5E; // d
            CharPatterns['E'] = 0x79; CharPatterns['e'] = 0x79;
            CharPatterns['F'] = 0x71; CharPatterns['f'] = 0x71;
            CharPatterns['G'] = 0x3D; CharPatterns['g'] = 0x6F;
            CharPatterns['H'] = 0x76; CharPatterns['h'] = 0x74; // H / h
            CharPatterns['I'] = 0x06; CharPatterns['i'] = 0x04;
            CharPatterns['J'] = 0x1E; CharPatterns['j'] = 0x1E;
            CharPatterns['K'] = 0x75; CharPatterns['k'] = 0x74;
            CharPatterns['L'] = 0x38; CharPatterns['l'] = 0x30;
            CharPatterns['M'] = 0x54; CharPatterns['m'] = 0x54; // Industrial standard displays as n
            CharPatterns['N'] = 0x54; CharPatterns['n'] = 0x54; // n
            CharPatterns['O'] = 0x3F; CharPatterns['o'] = 0x5C; // O / o
            CharPatterns['P'] = 0x73; CharPatterns['p'] = 0x73;
            CharPatterns['Q'] = 0x67; CharPatterns['q'] = 0x67;
            CharPatterns['R'] = 0x50; CharPatterns['r'] = 0x50; // r
            CharPatterns['S'] = 0x6D; CharPatterns['s'] = 0x6D; // S (5)
            CharPatterns['T'] = 0x78; CharPatterns['t'] = 0x78; // t
            CharPatterns['U'] = 0x3E; CharPatterns['u'] = 0x1C; // U / u
            CharPatterns['V'] = 0x1C; CharPatterns['v'] = 0x1C;
            CharPatterns['W'] = 0x2A; CharPatterns['w'] = 0x2A;
            CharPatterns['X'] = 0x76; CharPatterns['x'] = 0x76;
            CharPatterns['Y'] = 0x6E; CharPatterns['y'] = 0x6E; // y
            CharPatterns['Z'] = 0x5B; CharPatterns['z'] = 0x5B;
        }

        /// <summary>
        /// Translates a character into its standard 7-segment bitmask (A=0x01, B=0x02, C=0x04, D=0x08, E=0x10, F=0x20, G=0x40).
        /// </summary>
        public static byte GetPattern(char c)
        {
            if (c < 128)
            {
                return CharPatterns[c];
            }
            if (c == '°') return 0x63;
            return 0x00;
        }

        /// <summary>
        /// Resolves ARGB 32-bit color values (primary segment and unlit ghost) for industrial presets.
        /// </summary>
        public static (uint segmentArgb, uint dimArgb) GetPresetColors(SevenSegmentColorPreset preset)
        {
            switch (preset)
            {
                case SevenSegmentColorPreset.NeonEmerald:
                    return (0xFF34D399, 0xFF142D23); // Emerald
                case SevenSegmentColorPreset.NeonCyan:
                    return (0xFF38BDF8, 0xFF102636); // Cyan
                case SevenSegmentColorPreset.NeonAmber:
                    return (0xFFF59E0B, 0xFF30200E); // Amber
                case SevenSegmentColorPreset.NeonRed:
                    return (0xFFEF4444, 0xFF301212); // Red
                case SevenSegmentColorPreset.CrispWhite:
                    return (0xFFF8FAFC, 0xFF232830); // White
                case SevenSegmentColorPreset.UltraViolet:
                    return (0xFFC084FC, 0xFF261834); // Violet
                case SevenSegmentColorPreset.Custom:
                default:
                    return (0xFF34D399, 0xFF142D23);
            }
        }

        /// <summary>
        /// Derives an unlit ghost segment ARGB color from an arbitrary primary segment ARGB color.
        /// </summary>
        public static uint DeriveDimColor(uint primaryArgb)
        {
            byte r = (byte)((primaryArgb >> 16) & 0xFF);
            byte g = (byte)((primaryArgb >> 8) & 0xFF);
            byte b = (byte)(primaryArgb & 0xFF);

            byte dimR = (byte)Math.Max(8, r / 5);
            byte dimG = (byte)Math.Max(8, g / 5);
            byte dimB = (byte)Math.Max(8, b / 5);

            return 0xFF000000 | ((uint)dimR << 16) | ((uint)dimG << 8) | dimB;
        }

        /// <summary>
        /// Parses the input string into display items, merging colons and decimal points into previous items.
        /// </summary>
        public static List<ParsedDisplayItem> ParseValue(string rawValue, int digitCount, LeadingZeroDisplayMode leadingZeroMode)
        {
            var rawItems = new List<ParsedDisplayItem>(16);
            string text = rawValue ?? "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ':')
                {
                    rawItems.Add(new ParsedDisplayItem { IsColon = true });
                }
                else if (c == '.' || c == ',')
                {
                    if (rawItems.Count > 0 && !rawItems[rawItems.Count - 1].IsColon && !rawItems[rawItems.Count - 1].HasDecimal)
                    {
                        var last = rawItems[rawItems.Count - 1];
                        last.HasDecimal = true;
                        rawItems[rawItems.Count - 1] = last;
                    }
                    else
                    {
                        rawItems.Add(new ParsedDisplayItem { Character = ' ', HasDecimal = true });
                    }
                }
                else
                {
                    rawItems.Add(new ParsedDisplayItem { Character = c });
                }
            }

            int digitSlotCount = 0;
            for (int i = 0; i < rawItems.Count; i++)
            {
                if (!rawItems[i].IsColon) digitSlotCount++;
            }

            int paddingNeeded = Math.Max(0, digitCount - digitSlotCount);
            if (paddingNeeded == 0) return rawItems;

            var result = new List<ParsedDisplayItem>(rawItems.Count + paddingNeeded);
            for (int i = 0; i < paddingNeeded; i++)
            {
                switch (leadingZeroMode)
                {
                    case LeadingZeroDisplayMode.LitZero:
                        result.Add(new ParsedDisplayItem { Character = '0' });
                        break;
                    case LeadingZeroDisplayMode.DimmedGhost:
                        result.Add(new ParsedDisplayItem { Character = '0', IsDimmed = true });
                        break;
                    case LeadingZeroDisplayMode.Blank:
                    default:
                        result.Add(new ParsedDisplayItem { Character = ' ' });
                        break;
                }
            }

            result.AddRange(rawItems);
            return result;
        }
    }
}
