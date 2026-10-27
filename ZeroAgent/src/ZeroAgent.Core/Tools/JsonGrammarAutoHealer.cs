using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Deterministic JSON Grammar & Schema Auto-Healer:
    /// Automatically repairs common LLM structural malformations (unquoted keys, single quotes,
    /// trailing commas, missing braces/brackets, markdown wrappers, and invalid literals)
    /// without requiring re-prompting or external dependencies.
    /// </summary>
    public static class JsonGrammarAutoHealer
    {
        private static readonly Regex TrailingCommaRegex = new Regex(@",\s*([\]}])", RegexOptions.Compiled);
        private static readonly Regex UnquotedKeyRegex = new Regex(@"(?<=[{,\s])(?<key>[a-zA-Z_][a-zA-Z0-9_]*)\s*:", RegexOptions.Compiled);

        /// <summary>
        /// Heals malformed JSON text into strictly compliant JSON syntax.
        /// </summary>
        public static string Heal(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return "{}";

            string text = rawText.Trim();

            // 1. Strip Markdown code fences
            text = StripMarkdownFences(text);

            // 2. Extract JSON payload between outermost braces or brackets
            int firstBrace = text.IndexOf('{');
            int firstBracket = text.IndexOf('[');

            int startIdx = -1;

            if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
            {
                startIdx = firstBrace;
            }
            else if (firstBracket >= 0)
            {
                startIdx = firstBracket;
            }

            if (startIdx >= 0)
            {
                text = text.Substring(startIdx).Trim();
            }

            // 3. Fix unquoted keys: { key: "value" } -> { "key": "value" }
            text = UnquotedKeyRegex.Replace(text, "\"${key}\":");

            // 4. Convert single-quoted string values: 'value' -> "value"
            text = ReplaceSingleQuotes(text);

            // 5. Remove trailing commas: { "a": 1, } -> { "a": 1 }
            text = TrailingCommaRegex.Replace(text, "$1");

            // 6. Replace JavaScript-specific non-standard tokens
            text = text.Replace(": undefined", ": null")
                       .Replace(": NaN", ": null");

            // 7. Repair truncated/unbalanced closures
            text = BalanceClosures(text);

            // 8. Remove any trailing commas exposed after adding closing braces/brackets
            text = TrailingCommaRegex.Replace(text, "$1");

            return text;
        }

        /// <summary>
        /// Attempts to deserialize raw LLM text into type <typeparamref name="T"/>,
        /// automatically healing syntax errors if initial parse fails.
        /// </summary>
        public static bool TryHealAndDeserialize<T>(string rawText, out T? result, out string healedJson)
        {
            healedJson = string.Empty;
            result = default;

            if (string.IsNullOrWhiteSpace(rawText)) return false;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            // Attempt 1: Direct parse
            try
            {
                result = JsonSerializer.Deserialize<T>(rawText, options);
                healedJson = rawText;
                return result != null;
            }
            catch
            {
                // Proceed to healing
            }

            // Attempt 2: Healed parse
            try
            {
                healedJson = Heal(rawText);
                result = JsonSerializer.Deserialize<T>(healedJson, options);
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Heals and deserializes raw LLM text into type <typeparamref name="T"/>, returning null if unparseable.
        /// </summary>
        public static T? HealAndDeserialize<T>(string rawText)
        {
            if (TryHealAndDeserialize<T>(rawText, out var result, out _))
            {
                return result;
            }
            return default;
        }

        private static string StripMarkdownFences(string text)
        {
            int fenceStart = text.IndexOf("```", StringComparison.Ordinal);
            if (fenceStart >= 0)
            {
                int contentStart = text.IndexOf('\n', fenceStart);
                if (contentStart > fenceStart)
                {
                    int fenceEnd = text.IndexOf("```", contentStart, StringComparison.Ordinal);
                    if (fenceEnd > contentStart)
                    {
                        return text.Substring(contentStart + 1, fenceEnd - contentStart - 1).Trim();
                    }
                    return text.Substring(contentStart + 1).Trim();
                }
            }
            return text;
        }

        private static string ReplaceSingleQuotes(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inDoubleQuote = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inDoubleQuote = !inDoubleQuote;
                    sb.Append(c);
                }
                else if (c == '\'' && !inDoubleQuote)
                {
                    sb.Append('"');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string BalanceClosures(string text)
        {
            int openBraces = 0;
            int openBrackets = 0;
            bool inString = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == '{') openBraces++;
                    else if (c == '}') openBraces = Math.Max(0, openBraces - 1);
                    else if (c == '[') openBrackets++;
                    else if (c == ']') openBrackets = Math.Max(0, openBrackets - 1);
                }
            }

            if (openBraces == 0 && openBrackets == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text);
            // Close any unclosed string
            if (inString)
            {
                sb.Append('"');
            }

            while (openBrackets > 0)
            {
                sb.Append(']');
                openBrackets--;
            }

            while (openBraces > 0)
            {
                sb.Append('}');
                openBraces--;
            }

            return sb.ToString();
        }
    }
}
