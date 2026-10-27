using System;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 16022 DataMatrix ECC200 payload decoder.
    /// Decodes data codewords from ASCII, C40, Text, and Base256 encodation schemes.
    /// </summary>
    public static class DataMatrixPayloadDecoder
    {
        private enum Mode
        {
            Ascii,
            C40,
            Text,
            AnsiX12,
            Edifact,
            Base256
        }

        private static readonly char[] C40Shift2Chars = new char[]
        {
            '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
            ':', ';', '<', '=', '>', '?', '@',
            '[', '\\', ']', '^', '_'
        };

        /// <summary>
        /// Decodes DataMatrix data codewords into a text string.
        /// </summary>
        public static string Decode(byte[] codewords, int dataCodewordCount)
        {
            if (codewords == null || codewords.Length == 0 || dataCodewordCount <= 0)
                return string.Empty;

            int count = Math.Min(codewords.Length, dataCodewordCount);
            StringBuilder sb = new StringBuilder();
            int index = 0;
            Mode currentMode = Mode.Ascii;

            while (index < count)
            {
                switch (currentMode)
                {
                    case Mode.Ascii:
                        index = DecodeAscii(codewords, index, count, sb, ref currentMode);
                        break;
                    case Mode.C40:
                        index = DecodeC40OrText(codewords, index, count, sb, ref currentMode, isText: false);
                        break;
                    case Mode.Text:
                        index = DecodeC40OrText(codewords, index, count, sb, ref currentMode, isText: true);
                        break;
                    case Mode.Base256:
                        index = DecodeBase256(codewords, index, count, sb, ref currentMode);
                        break;
                    default:
                        // Fallback: decode remaining as raw ASCII
                        index = DecodeAscii(codewords, index, count, sb, ref currentMode);
                        break;
                }
            }

            return sb.ToString();
        }

        private static int DecodeAscii(byte[] codewords, int index, int count, StringBuilder sb, ref Mode currentMode)
        {
            int c = codewords[index++];

            if (c >= 1 && c <= 128)
            {
                // Standard ASCII
                sb.Append((char)(c - 1));
            }
            else if (c == 129)
            {
                // Pad character - marks end of data
                return count;
            }
            else if (c >= 130 && c <= 229)
            {
                // 2-digit numeric pair (c - 130)
                int val = c - 130;
                sb.Append((char)('0' + (val / 10)));
                sb.Append((char)('0' + (val % 10)));
            }
            else if (c == 230)
            {
                // Switch to C40
                currentMode = Mode.C40;
            }
            else if (c == 231)
            {
                // Switch to Base256
                currentMode = Mode.Base256;
            }
            else if (c == 232)
            {
                // FNC1 (Group Separator)
                sb.Append('\u001d');
            }
            else if (c == 235)
            {
                // Upper Shift (next byte + 128)
                if (index < count)
                {
                    int next = codewords[index++];
                    sb.Append((char)(next + 128));
                }
            }
            else if (c == 239)
            {
                // Switch to Text
                currentMode = Mode.Text;
            }

            return index;
        }

        private static int DecodeC40OrText(byte[] codewords, int index, int count, StringBuilder sb, ref Mode currentMode, bool isText)
        {
            int shift = 0;

            while (index < count)
            {
                int c1 = codewords[index++];
                if (c1 == 254)
                {
                    // Return to ASCII mode
                    currentMode = Mode.Ascii;
                    return index;
                }

                if (index >= count)
                {
                    // Truncated C40 pair
                    break;
                }

                int c2 = codewords[index++];
                int val = (c1 << 8) + c2 - 1;

                int[] cValues = new int[3];
                cValues[0] = val / 1600;
                int rem = val % 1600;
                cValues[1] = rem / 40;
                cValues[2] = rem % 40;

                for (int i = 0; i < 3; i++)
                {
                    int v = cValues[i];
                    switch (shift)
                    {
                        case 0:
                            if (v == 0) shift = 1;
                            else if (v == 1) shift = 2;
                            else if (v == 2) shift = 3;
                            else if (v == 3) sb.Append(' ');
                            else if (v <= 13) sb.Append((char)('0' + (v - 4)));
                            else if (v <= 39)
                            {
                                if (isText)
                                    sb.Append((char)('a' + (v - 14)));
                                else
                                    sb.Append((char)('A' + (v - 14)));
                            }
                            break;

                        case 1:
                            // Shift 1: ASCII 0..31
                            sb.Append((char)v);
                            shift = 0;
                            break;

                        case 2:
                            // Shift 2: Punctuation
                            if (v < C40Shift2Chars.Length)
                                sb.Append(C40Shift2Chars[v]);
                            else if (v == 27)
                                sb.Append('\u001d'); // FNC1
                            shift = 0;
                            break;

                        case 3:
                            // Shift 3
                            if (isText)
                            {
                                if (v <= 25) sb.Append((char)('A' + v));
                            }
                            else
                            {
                                if (v == 0) sb.Append('`');
                                else if (v <= 26) sb.Append((char)('a' + (v - 1)));
                                else if (v == 27) sb.Append('{');
                                else if (v == 28) sb.Append('|');
                                else if (v == 29) sb.Append('}');
                                else if (v == 30) sb.Append('~');
                            }
                            shift = 0;
                            break;
                    }
                }
            }

            return index;
        }

        private static int DecodeBase256(byte[] codewords, int index, int count, StringBuilder sb, ref Mode currentMode)
        {
            if (index >= count)
            {
                currentMode = Mode.Ascii;
                return index;
            }

            int d1 = Unrandomize255State(codewords[index++], index);
            int dataLength;
            if (d1 == 0)
            {
                dataLength = count - index;
            }
            else if (d1 < 250)
            {
                dataLength = d1;
            }
            else
            {
                if (index >= count) { currentMode = Mode.Ascii; return index; }
                int d2 = Unrandomize255State(codewords[index++], index);
                dataLength = (d1 - 249) * 250 + d2;
            }

            for (int i = 0; i < dataLength && index < count; i++)
            {
                byte b = Unrandomize255State(codewords[index++], index);
                sb.Append((char)b);
            }

            currentMode = Mode.Ascii;
            return index;
        }

        private static byte Unrandomize255State(byte val, int position)
        {
            int pseudoRandom = ((149 * position) % 255) + 1;
            int temp = val - pseudoRandom;
            return (byte)(temp >= 0 ? temp : temp + 256);
        }
    }
}
