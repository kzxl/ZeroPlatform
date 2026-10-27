using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroData.Core.Text;

/// <summary>
/// Parsed representation of a cURL HTTP command.
/// </summary>
public class ParsedCurlRequest
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
    public bool AllowInsecureSsl { get; set; }
    public string? BasicAuth { get; set; }
}

/// <summary>
/// Zero-dependency parser that converts raw cURL commands from browsers, Postman, or terminal into structured requests.
/// </summary>
public static class CurlCommandParser
{
    public static ParsedCurlRequest Parse(string curlCommand)
    {
        var result = new ParsedCurlRequest();
        if (string.IsNullOrWhiteSpace(curlCommand)) return result;

        var tokens = Tokenize(curlCommand);
        if (tokens.Count == 0) return result;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.Equals("curl", StringComparison.OrdinalIgnoreCase)) continue;

            if (token == "-X" || token == "--request")
            {
                if (i + 1 < tokens.Count)
                {
                    result.Method = tokens[++i].ToUpperInvariant();
                }
            }
            else if (token == "-H" || token == "--header")
            {
                if (i + 1 < tokens.Count)
                {
                    var headerLine = tokens[++i];
                    int sep = headerLine.IndexOf(':');
                    if (sep > 0)
                    {
                        var key = headerLine.Substring(0, sep).Trim();
                        var val = headerLine.Substring(sep + 1).Trim();
                        result.Headers[key] = val;
                    }
                }
            }
            else if (token == "-d" || token == "--data" || token == "--data-raw" || token == "--data-binary")
            {
                if (i + 1 < tokens.Count)
                {
                    result.Body = tokens[++i];
                    if (result.Method == "GET")
                    {
                        result.Method = "POST";
                    }
                }
            }
            else if (token == "-k" || token == "--insecure")
            {
                result.AllowInsecureSsl = true;
            }
            else if (token == "-u" || token == "--user")
            {
                if (i + 1 < tokens.Count)
                {
                    result.BasicAuth = tokens[++i];
                }
            }
            else if (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result.Url = token;
            }
            else if (token == "--location" || token == "-L" || token == "-s" || token == "-S")
            {
                // Informational flags skipped
            }
        }

        return result;
    }

    private static List<string> Tokenize(string command)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool escape = false;

        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];

            if (escape)
            {
                sb.Append(c);
                escape = false;
                continue;
            }

            if (c == '\\' && !inSingleQuote)
            {
                if (i + 1 < command.Length && (command[i + 1] == '\r' || command[i + 1] == '\n'))
                {
                    // Line continuation: skip newline
                    i++;
                    if (i + 1 < command.Length && command[i] == '\r' && command[i + 1] == '\n') i++;
                    continue;
                }
                escape = true;
                continue;
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingleQuote && !inDoubleQuote)
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }
}
