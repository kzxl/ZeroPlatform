using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Http;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Constants and framing helpers for ASP.NET Core SignalR JSON Hub Protocol (v1).
    /// </summary>
    internal static class SignalRProtocol
    {
        public const char RecordSeparator = '\u001e';
        public const string HandshakeRequest = "{\"protocol\":\"json\",\"version\":1}\u001e";
        public const string PingMessage = "{\"type\":6}\u001e";

        public static string FormatMessage(string json) => json + RecordSeparator;

        public static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static async Task<string> SerializeArgumentAsync(object? arg, IZeroJsonSerializer serializer, CancellationToken cancellationToken)
        {
            if (arg == null) return "null";
            if (arg is string s) return "\"" + EscapeJsonString(s) + "\"";
            if (arg is bool b) return b ? "true" : "false";
            if (arg is sbyte || arg is byte || arg is short || arg is ushort || arg is int || arg is uint || arg is long || arg is ulong || arg is float || arg is double || arg is decimal)
            {
                return Convert.ToString(arg, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            }

            using (var ms = new MemoryStream())
            {
                await serializer.SerializeAsync(ms, arg, cancellationToken).ConfigureAwait(false);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static async Task<string> BuildInvocationMessageAsync(string? invocationId, string target, object?[]? args, IZeroJsonSerializer serializer, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":1,");

            if (!string.IsNullOrEmpty(invocationId))
            {
                sb.Append("\"invocationId\":\"").Append(EscapeJsonString(invocationId!)).Append("\",");
            }

            sb.Append("\"target\":\"").Append(EscapeJsonString(target)).Append("\",\"arguments\":[");

            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    string argJson = await SerializeArgumentAsync(args[i], serializer, cancellationToken).ConfigureAwait(false);
                    sb.Append(argJson);
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Parses a raw SignalR Hub JSON message without external dependencies.
        /// </summary>
        public static SignalRHubMessage? ParseHubMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return null;

            var msg = new SignalRHubMessage();
            int i = 1;
            int length = json.Length;

            while (i < length - 1)
            {
                SkipWhitespace(json, ref i);
                if (i >= length - 1) break;

                if (json[i] == ',')
                {
                    i++;
                    SkipWhitespace(json, ref i);
                }

                if (i >= length - 1 || json[i] != '"') break;

                string propName = ReadString(json, ref i);
                SkipWhitespace(json, ref i);

                if (i >= length - 1 || json[i] != ':') break;
                i++; // skip ':'
                SkipWhitespace(json, ref i);

                if (string.Equals(propName, "type", StringComparison.OrdinalIgnoreCase))
                {
                    msg.Type = ReadInt(json, ref i);
                }
                else if (string.Equals(propName, "target", StringComparison.OrdinalIgnoreCase))
                {
                    msg.Target = ReadString(json, ref i);
                }
                else if (string.Equals(propName, "invocationId", StringComparison.OrdinalIgnoreCase))
                {
                    msg.InvocationId = ReadStringOrNull(json, ref i);
                }
                else if (string.Equals(propName, "error", StringComparison.OrdinalIgnoreCase))
                {
                    msg.Error = ReadStringOrNull(json, ref i);
                }
                else if (string.Equals(propName, "result", StringComparison.OrdinalIgnoreCase))
                {
                    msg.RawResult = ReadRawJsonValue(json, ref i);
                }
                else if (string.Equals(propName, "arguments", StringComparison.OrdinalIgnoreCase))
                {
                    ParseArguments(json, ref i, msg.RawArguments);
                }
                else
                {
                    // Skip unknown property value
                    _ = ReadRawJsonValue(json, ref i);
                }
            }

            return msg;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return "";
            i++; // skip opening quote

            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
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
                            if (i + 4 <= s.Length)
                            {
                                string hex = s.Substring(i, 4);
                                i += 4;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
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

        private static string? ReadStringOrNull(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == 'n')
            {
                // check null
                if (i + 4 <= s.Length && s.Substring(i, 4) == "null")
                {
                    i += 4;
                    return null;
                }
            }
            return ReadString(s, ref i);
        }

        private static int ReadInt(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-')) i++;
            string sub = s.Substring(start, i - start);
            int.TryParse(sub, out int val);
            return val;
        }

        private static string ReadRawJsonValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return "";

            char startChar = s[i];
            if (startChar == '"')
            {
                int start = i;
                _ = ReadString(s, ref i);
                return s.Substring(start, i - start);
            }

            if (startChar == '{' || startChar == '[')
            {
                int depth = 0;
                int start = i;
                bool inStr = false;

                while (i < s.Length)
                {
                    char c = s[i++];
                    if (inStr)
                    {
                        if (c == '\\') { if (i < s.Length) i++; }
                        else if (c == '"') inStr = false;
                    }
                    else
                    {
                        if (c == '"') inStr = true;
                        else if (c == '{' || c == '[') depth++;
                        else if (c == '}' || c == ']')
                        {
                            depth--;
                            if (depth == 0) break;
                        }
                    }
                }
                return s.Substring(start, i - start);
            }

            // Primitive value: number, true, false, null
            int startPrim = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i]))
            {
                i++;
            }
            return s.Substring(startPrim, i - startPrim);
        }

        private static void ParseArguments(string s, ref int i, List<string> args)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length || s[i] != '[') return;
            i++; // skip '['

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']')
                {
                    i++;
                    break;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                string argJson = ReadRawJsonValue(s, ref i);
                args.Add(argJson);
            }
        }
    }

    /// <summary>
    /// Represents a parsed ASP.NET Core SignalR Hub message.
    /// </summary>
    internal class SignalRHubMessage
    {
        public int Type { get; set; }
        public string? Target { get; set; }
        public string? InvocationId { get; set; }
        public string? Error { get; set; }
        public string? RawResult { get; set; }
        public List<string> RawArguments { get; } = new List<string>();
    }
}
