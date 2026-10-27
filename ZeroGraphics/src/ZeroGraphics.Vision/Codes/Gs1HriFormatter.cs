using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Represents an individual parsed GS1 Application Identifier element.
    /// </summary>
    public sealed class Gs1Element
    {
        public string Ai { get; }
        public string Data { get; }
        public string Title { get; }

        public Gs1Element(string ai, string data, string title = "")
        {
            Ai = ai;
            Data = data;
            Title = title;
        }

        public string HriString => $"({Ai}) {Data}";
        public override string ToString() => HriString;
    }

    internal readonly struct AiInfo
    {
        public readonly int Length;
        public readonly bool IsFixed;
        public readonly string Title;

        public AiInfo(int length, bool isFixed, string title)
        {
            Length = length;
            IsFixed = isFixed;
            Title = title;
        }
    }

    /// <summary>
    /// Pure C# industrial GS1 Application Identifier (AI) parser and Human Readable Interpretation (HRI) formatter.
    /// Converts between raw barcode bitstream payloads and human-readable bracketed GS1 strings.
    /// </summary>
    public static class Gs1HriFormatter
    {
        private static readonly Dictionary<string, AiInfo> KnownAis =
            new Dictionary<string, AiInfo>(StringComparer.Ordinal)
            {
                { "00", new AiInfo(18, true, "SSCC") },
                { "01", new AiInfo(14, true, "GTIN") },
                { "02", new AiInfo(14, true, "CONTENT") },
                { "10", new AiInfo(20, false, "BATCH/LOT") },
                { "11", new AiInfo(6, true, "PROD DATE") },
                { "12", new AiInfo(6, true, "DUE DATE") },
                { "13", new AiInfo(6, true, "PACK DATE") },
                { "15", new AiInfo(6, true, "BEST BEFORE") },
                { "17", new AiInfo(6, true, "USE BY OR EXPIRY") },
                { "20", new AiInfo(2, true, "VARIANT") },
                { "21", new AiInfo(20, false, "SERIAL") },
                { "30", new AiInfo(8, false, "VAR. COUNT") },
                { "37", new AiInfo(8, false, "COUNT") },
                { "400", new AiInfo(30, false, "ORDER NUMBER") },
                { "410", new AiInfo(13, true, "SHIP TO LOC") },
                { "420", new AiInfo(20, false, "SHIP TO POST") }
            };

        /// <summary>
        /// Parses a GS1 string (with or without AI parentheses) into structured GS1Elements.
        /// </summary>
        public static List<Gs1Element> Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<Gs1Element>();

            string trimmed = input.Trim();
            if (trimmed.Contains("(") && trimmed.Contains(")"))
            {
                return ParseBracketed(trimmed);
            }

            return ParseRaw(trimmed);
        }

        /// <summary>
        /// Formats any GS1 data string into canonical Human Readable Interpretation (HRI) with parentheses:
        /// Example: "(01) 08801234567891 (17) 260921 (10) LOT123"
        /// </summary>
        public static string FormatHri(string input)
        {
            var elements = Parse(input);
            if (elements.Count == 0)
                return input;

            var sb = new StringBuilder();
            for (int i = 0; i < elements.Count; i++)
            {
                if (i > 0) sb.Append(" ");
                sb.Append(elements[i].HriString);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts human-readable bracketed text into a raw barcode payload suitable for Code128 / GS1-128.
        /// Strips AI parentheses and injects FNC1 ('\u001D' / GS) delimiters between variable-length fields.
        /// </summary>
        public static string ToBarcodePayload(string hriInput)
        {
            var elements = Parse(hriInput);
            if (elements.Count == 0)
                return hriInput;

            var sb = new StringBuilder();
            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                sb.Append(el.Ai);
                sb.Append(el.Data);

                // If not last element and current AI is variable length, append FNC1 separator
                if (i < elements.Count - 1 && IsVariableLength(el.Ai))
                {
                    sb.Append('\u001D'); // Group Separator / FNC1
                }
            }
            return sb.ToString();
        }

        private static List<Gs1Element> ParseBracketed(string text)
        {
            var list = new List<Gs1Element>();
            var matches = Regex.Matches(text, @"\((?<ai>\d{2,4})\)\s*(?<data>[^\(\)]+)");
            foreach (Match m in matches)
            {
                string ai = m.Groups["ai"].Value;
                string data = m.Groups["data"].Value.Trim();
                string title = GetTitle(ai);
                list.Add(new Gs1Element(ai, data, title));
            }
            return list;
        }

        private static List<Gs1Element> ParseRaw(string raw)
        {
            var list = new List<Gs1Element>();
            int cursor = 0;
            int length = raw.Length;

            while (cursor < length)
            {
                // Skip any leading GS / FNC1 characters
                if (raw[cursor] == '\u001D' || raw[cursor] == 29)
                {
                    cursor++;
                    continue;
                }

                // Try 2-digit, 3-digit, or 4-digit AI
                string? matchedAi = null;
                int aiLen = 0;

                for (int candidateLen = 2; candidateLen <= 4; candidateLen++)
                {
                    if (cursor + candidateLen <= length)
                    {
                        string candidate = raw.Substring(cursor, candidateLen);
                        if (KnownAis.ContainsKey(candidate) || (candidateLen == 2 && char.IsDigit(candidate[0]) && char.IsDigit(candidate[1])))
                        {
                            matchedAi = candidate;
                            aiLen = candidateLen;
                            break;
                        }
                    }
                }

                if (matchedAi == null)
                {
                    // Fallback: take remaining as raw
                    list.Add(new Gs1Element("99", raw.Substring(cursor)));
                    break;
                }

                cursor += aiLen;

                // Extract data
                if (KnownAis.TryGetValue(matchedAi, out var info))
                {
                    if (info.IsFixed)
                    {
                        int take = Math.Min(info.Length, length - cursor);
                        string data = raw.Substring(cursor, take);
                        cursor += take;
                        list.Add(new Gs1Element(matchedAi, data, info.Title));
                    }
                    else
                    {
                        // Variable length up to FNC1 delimiter or max length
                        int end = raw.IndexOf('\u001D', cursor);
                        if (end < 0) end = raw.IndexOf((char)29, cursor);
                        if (end < 0) end = Math.Min(cursor + info.Length, length);
                        else end = Math.Min(end, cursor + info.Length);

                        string data = raw.Substring(cursor, end - cursor);
                        cursor = end;
                        list.Add(new Gs1Element(matchedAi, data, info.Title));
                    }
                }
                else
                {
                    // Generic 2-digit AI assumed variable up to next FNC1 or 20 chars
                    int end = raw.IndexOf('\u001D', cursor);
                    if (end < 0) end = raw.IndexOf((char)29, cursor);
                    if (end < 0) end = Math.Min(cursor + 20, length);

                    string data = raw.Substring(cursor, end - cursor);
                    cursor = end;
                    list.Add(new Gs1Element(matchedAi, data, "INTERNAL"));
                }
            }

            return list;
        }

        private static bool IsVariableLength(string ai)
        {
            if (KnownAis.TryGetValue(ai, out var info))
                return !info.IsFixed;
            return true;
        }

        private static string GetTitle(string ai)
        {
            if (KnownAis.TryGetValue(ai, out var info))
                return info.Title;
            return "DATA";
        }
    }
}
