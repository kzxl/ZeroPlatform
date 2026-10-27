using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Robust, zero-allocation parser for extracting tool invocations and final answers from LLM completions.
    /// Supports XML tool calling tags, standard ReAct syntax, and native JSON function-calling formats.
    /// Includes resilient auto-healing for truncated or malformed JSON payloads.
    /// </summary>
    public static class ToolCallParser
    {
        private static readonly Regex XmlToolCallRegex = new Regex(
            @"<tool_call>(.*?)(?:</tool_call>|$)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex ResponseTagRegex = new Regex(
            @"<response>(.*?)(?:</response>|$)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex ActionFuncRegex = new Regex(
            @"Action:\s*([A-Za-z0-9_]+)\s*\((.*)\)",
            RegexOptions.Compiled);

        private static readonly Regex ActionJsonRegex = new Regex(
            @"Action:\s*(\{.+\})",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex CodeblockJsonRegex = new Regex(
            @"```(?:json)?\s*(\{.+\})\s*```",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex FinalAnswerRegex = new Regex(
            @"Final Answer:\s*(.*)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Attempts to parse a tool invocation request from the model's raw completion output.
        /// </summary>
        public static bool TryParseToolCall(string? completion, out ToolCallRequest request)
        {
            request = null!;
            if (string.IsNullOrWhiteSpace(completion)) return false;

            string text = completion.Trim();

            // 1. Try XML Tool Call: <tool_call>{...}</tool_call> (supports truncated closing tag)
            var xmlMatch = XmlToolCallRegex.Match(text);
            if (xmlMatch.Success && !string.IsNullOrWhiteSpace(xmlMatch.Groups[1].Value))
            {
                if (TryParseJsonPayload(xmlMatch.Groups[1].Value.Trim(), out request))
                {
                    return true;
                }
            }

            // 2. Try Classic ReAct: Action: ToolName(arguments)
            var funcMatch = ActionFuncRegex.Match(text);
            if (funcMatch.Success)
            {
                string toolName = funcMatch.Groups[1].Value.Trim();
                string rawArgs = funcMatch.Groups[2].Value.Trim();

                // Strip outer quotes if single string
                if (rawArgs.StartsWith("\"") && rawArgs.EndsWith("\"") && rawArgs.Length >= 2)
                {
                    rawArgs = rawArgs.Substring(1, rawArgs.Length - 2);
                }

                request = new ToolCallRequest(toolName, rawArgs);
                return true;
            }

            // 3. Try ReAct with JSON block: Action: {"tool": "...", "arguments": ...}
            var actJsonMatch = ActionJsonRegex.Match(text);
            if (actJsonMatch.Success && TryParseJsonPayload(actJsonMatch.Groups[1].Value, out request))
            {
                return true;
            }

            // 4. Try Markdown Codeblock JSON: ```json { "tool": "...", ... } ```
            var codeMatch = CodeblockJsonRegex.Match(text);
            if (codeMatch.Success && TryParseJsonPayload(codeMatch.Groups[1].Value, out request))
            {
                return true;
            }

            // 5. Try Direct Raw JSON string starting with '{'
            if (text.StartsWith("{") && TryParseJsonPayload(text, out request))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse a final answer from the model's completion output.
        /// </summary>
        public static bool TryParseFinalAnswer(string? completion, out string finalAnswer)
        {
            finalAnswer = string.Empty;
            if (string.IsNullOrWhiteSpace(completion)) return false;

            // 1. Try <response>...</response>
            var respMatch = ResponseTagRegex.Match(completion);
            if (respMatch.Success && !string.IsNullOrWhiteSpace(respMatch.Groups[1].Value))
            {
                finalAnswer = respMatch.Groups[1].Value.Trim();
                return true;
            }

            // 2. Try Final Answer: ...
            var match = FinalAnswerRegex.Match(completion);
            if (match.Success)
            {
                finalAnswer = match.Groups[1].Value.Trim();
                return true;
            }

            return false;
        }

        public static bool TryParseJsonPayload(string json, out ToolCallRequest request)
        {
            request = null!;
            if (string.IsNullOrWhiteSpace(json)) return false;

            string repaired = SafeRepairJson(json);

            try
            {
                using var doc = JsonDocument.Parse(repaired);
                var root = doc.RootElement;

                string? toolName = null;
                if (root.TryGetProperty("tool", out var tProp)) toolName = tProp.GetString();
                else if (root.TryGetProperty("name", out var nProp)) toolName = nProp.GetString();
                else if (root.TryGetProperty("tool_name", out var tnProp)) toolName = tnProp.GetString();

                if (string.IsNullOrWhiteSpace(toolName)) return false;

                string argsJson = "{}";
                if (root.TryGetProperty("arguments", out var aProp))
                {
                    argsJson = aProp.ValueKind == JsonValueKind.String ? aProp.GetString()! : aProp.GetRawText();
                }
                else if (root.TryGetProperty("parameters", out var pProp))
                {
                    argsJson = pProp.ValueKind == JsonValueKind.String ? pProp.GetString()! : pProp.GetRawText();
                }

                string? callId = null;
                if (root.TryGetProperty("id", out var idProp)) callId = idProp.GetString();
                else if (root.TryGetProperty("call_id", out var cidProp)) callId = cidProp.GetString();

                request = new ToolCallRequest(toolName!, argsJson, callId);
                return true;
            }
            catch
            {
                // Fallback regex extractor for heavily damaged outputs
                var nameMatch = Regex.Match(json, @"(?:""name""|""tool""|'name'|'tool')\s*:\s*[""']([^""']+)[""']");
                if (nameMatch.Success)
                {
                    string toolName = nameMatch.Groups[1].Value;
                    var argsObjMatch = Regex.Match(json, @"(?:""arguments""|""parameters"")\s*:\s*(\{.*)", RegexOptions.Singleline);
                    string argsJson = "{}";
                    if (argsObjMatch.Success)
                    {
                        argsJson = SafeRepairJson(argsObjMatch.Groups[1].Value);
                    }
                    request = new ToolCallRequest(toolName, argsJson);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Automatically heals truncated, unclosed, or malformed JSON payloads produced by LLM sampling.
        /// </summary>
        public static string SafeRepairJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "{}";
            string text = raw.Trim();

            // Replace single quotes with double quotes if no double quotes present
            if (text.Contains("'") && !text.Contains("\""))
            {
                text = text.Replace('\'', '"');
            }

            // Remove trailing commas before closing braces/brackets
            text = Regex.Replace(text, @",\s*(\}|\])", "$1");

            // Check if string literal is unclosed
            bool inString = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inString = !inString;
                }
            }
            if (inString)
            {
                text += "\"";
            }

            // Count opening vs closing braces
            int openBraces = 0, closeBraces = 0;
            int openBrackets = 0, closeBrackets = 0;
            inString = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
                if (!inString)
                {
                    if (c == '{') openBraces++;
                    else if (c == '}') closeBraces++;
                    else if (c == '[') openBrackets++;
                    else if (c == ']') closeBrackets++;
                }
            }

            // Close missing brackets and braces
            while (closeBrackets < openBrackets)
            {
                text += "]";
                closeBrackets++;
            }
            while (closeBraces < openBraces)
            {
                text += "}";
                closeBraces++;
            }

            return text;
        }
    }
}
