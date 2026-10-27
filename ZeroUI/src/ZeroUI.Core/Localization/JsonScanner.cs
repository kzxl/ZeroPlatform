using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Ultra-fast, zero-allocation streaming state-machine scanner for flat JSON localization files.
    /// Operates without third-party dependencies, compatible across .NET Framework 4.6.2, .NET Standard 2.0, and .NET 8+.
    /// Supports standard JSON escapes (\", \\, \n, \r, \t, \uXXXX) and single/multi-line comments.
    /// </summary>
    public static class JsonScanner
    {
        /// <summary>
        /// Parses a flat JSON string into a destination dictionary with zero AST allocations.
        /// </summary>
        public static void Parse(string? jsonText, IDictionary<string, string> destination)
        {
            if (string.IsNullOrWhiteSpace(jsonText) || destination == null) return;

            ReadOnlySpan<char> span = jsonText.AsSpan();
            int length = span.Length;
            int i = 0;

            StringBuilder? valueBuilder = null;

            while (i < length)
            {
                // Skip whitespace and comments
                SkipWhitespaceAndComments(span, ref i);
                if (i >= length) break;

                char c = span[i];
                if (c == '{' || c == ',' || c == '}')
                {
                    i++;
                    continue;
                }

                // 1. Read Key (must start with '"')
                if (c != '"')
                {
                    i++;
                    continue;
                }

                i++; // Skip opening quote
                int keyStart = i;
                while (i < length)
                {
                    char kc = span[i];
                    if (kc == '\\')
                    {
                        i += 2;
                        continue;
                    }
                    if (kc == '"') break;
                    i++;
                }

                if (i >= length) break;
                string key = span.Slice(keyStart, i - keyStart).ToString();
                i++; // Skip closing quote

                // 2. Expect Colon ':'
                SkipWhitespaceAndComments(span, ref i);
                if (i >= length || span[i] != ':') break;
                i++; // Skip ':'

                // 3. Expect Value (string literal starting with '"')
                SkipWhitespaceAndComments(span, ref i);
                if (i >= length) break;

                if (span[i] == '"')
                {
                    i++; // Skip opening quote
                    int valStart = i;
                    bool hasEscapes = false;

                    while (i < length)
                    {
                        char vc = span[i];
                        if (vc == '\\')
                        {
                            hasEscapes = true;
                            i += 2;
                            continue;
                        }
                        if (vc == '"') break;
                        i++;
                    }

                    if (i >= length) break;

                    string value;
                    if (!hasEscapes)
                    {
                        value = span.Slice(valStart, i - valStart).ToString();
                    }
                    else
                    {
                        value = UnescapeSpan(span.Slice(valStart, i - valStart), ref valueBuilder);
                    }

                    destination[key] = value;
                    i++; // Skip closing quote
                }
                else
                {
                    // Non-string primitive value (e.g. number, boolean, null)
                    int valStart = i;
                    while (i < length && span[i] != ',' && span[i] != '}' && !char.IsWhiteSpace(span[i]))
                    {
                        i++;
                    }
                    string value = span.Slice(valStart, i - valStart).ToString();
                    destination[key] = value;
                }
            }
        }

        private static void SkipWhitespaceAndComments(ReadOnlySpan<char> span, ref int i)
        {
            int len = span.Length;
            while (i < len)
            {
                char c = span[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                }
                else if (c == '/' && i + 1 < len)
                {
                    char next = span[i + 1];
                    if (next == '/')
                    {
                        // Single line comment
                        i += 2;
                        while (i < len && span[i] != '\n' && span[i] != '\r') i++;
                    }
                    else if (next == '*')
                    {
                        // Multi-line comment
                        i += 2;
                        while (i + 1 < len && !(span[i] == '*' && span[i + 1] == '/')) i++;
                        i += 2;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static string UnescapeSpan(ReadOnlySpan<char> span, ref StringBuilder? sb)
        {
            if (sb == null) sb = new StringBuilder(span.Length);
            else sb.Clear();

            int len = span.Length;
            for (int i = 0; i < len; i++)
            {
                char c = span[i];
                if (c == '\\' && i + 1 < len)
                {
                    i++;
                    char esc = span[i];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < len)
                            {
                                if (int.TryParse(span.Slice(i + 1, 4).ToString(), System.Globalization.NumberStyles.HexNumber, null, out int hex))
                                {
                                    sb.Append((char)hex);
                                    i += 4;
                                }
                                else
                                {
                                    sb.Append("\\u");
                                }
                            }
                            else
                            {
                                sb.Append("\\u");
                            }
                            break;
                        default:
                            sb.Append('\\').Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="JsonScanner"/> to preserve backward compatibility.
    /// </summary>
    [Obsolete("Use JsonScanner instead.")]
    public static class ZeroJsonScanner
    {
        /// <summary>
        /// Parses a flat JSON string into a destination dictionary with zero AST allocations.
        /// </summary>
        public static void Parse(string? jsonText, IDictionary<string, string> destination)
            => JsonScanner.Parse(jsonText, destination);
    }
}
