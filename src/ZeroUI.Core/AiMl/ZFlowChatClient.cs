using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.AiMl
{
    /// <summary>
    /// Generic zero-dependency HTTP &amp; SSE provider implementing <see cref="IChatFlowProvider"/>
    /// for ZFlow workflow backends (/api/workflows/run, /api/chat/stream, /api/memory/sessions).
    /// Contains no domain-specific assumptions.
    /// </summary>
    public sealed class ZFlowChatClient : IChatFlowProvider
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
        public string DefaultWorkflowId { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 30000;

        public ZFlowChatClient() { }

        public ZFlowChatClient(string baseUrl, string defaultWorkflowId = "")
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
                BaseUrl = baseUrl.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(defaultWorkflowId))
                DefaultWorkflowId = defaultWorkflowId.Trim();
        }

        public async Task<ChatFlowResult> ExecuteAsync(
            ChatFlowRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string wfId = !string.IsNullOrWhiteSpace(request.WorkflowId) ? request.WorkflowId : DefaultWorkflowId;
            string url = $"{BaseUrl.TrimEnd('/')}/api/workflows/run";
            string payload = BuildRequestJson(wfId, request.InputText, request.SessionId, request.Context);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Math.Max(1000, TimeoutMs));

            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                using var resp = await SharedHttpClient.SendAsync(
                    httpReq,
                    HttpCompletionOption.ResponseContentRead,
                    timeoutCts.Token).ConfigureAwait(false);

                string respJson = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    return new ChatFlowResult
                    {
                        Status = "failed",
                        Error = string.IsNullOrWhiteSpace(respJson) ? $"HTTP {(int)resp.StatusCode}" : respJson,
                        Output = $"⚠️ HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}"
                    };
                }

                return ParseResultJson(respJson);
            }
            catch (Exception ex)
            {
                return new ChatFlowResult
                {
                    Status = "failed",
                    Error = ex.Message,
                    Output = $"⚠️ Execution error: {ex.Message}"
                };
            }
        }

        public async Task<ChatFlowResult> StreamAsync(
            ChatFlowRequest request,
            Action<ChatStreamChunk>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string wfId = !string.IsNullOrWhiteSpace(request.WorkflowId) ? request.WorkflowId : DefaultWorkflowId;
            string url = $"{BaseUrl.TrimEnd('/')}/api/chat/stream";
            string payload = BuildRequestJson(wfId, request.InputText, request.SessionId, request.Context);
            var startTime = DateTime.UtcNow;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Math.Max(2000, TimeoutMs));

            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                httpReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var resp = await SharedHttpClient.SendAsync(
                    httpReq,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token).ConfigureAwait(false);

                resp.EnsureSuccessStatusCode();

                ChatFlowResult? finalResult = null;
                var accumulatedTokens = new StringBuilder();
                var accumulatedReasoning = new StringBuilder();

                using (var respStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(respStream, Encoding.UTF8))
                {
                    string currentEvent = string.Empty;
                    while (!reader.EndOfStream && !timeoutCts.Token.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;

                        if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentEvent = line.Substring(6).Trim();
                        }
                        else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            string dataJson = line.Substring(5).Trim();
                            if (string.IsNullOrEmpty(dataJson)) continue;

                            var chunk = ParseStreamChunk(currentEvent, dataJson);
                            if (chunk.EventType == "token" && !string.IsNullOrEmpty(chunk.Token))
                            {
                                accumulatedTokens.Append(chunk.Token);
                            }
                            else if (chunk.EventType == "reasoning" && !string.IsNullOrEmpty(chunk.Token))
                            {
                                accumulatedReasoning.Append(chunk.Token);
                            }
                            else if (chunk.EventType == "complete" && chunk.FinalResult != null)
                            {
                                finalResult = chunk.FinalResult;
                                if (finalResult.DurationMs <= 0)
                                {
                                    finalResult.DurationMs = Math.Round((DateTime.UtcNow - startTime).TotalMilliseconds, 1);
                                }
                            }

                            onChunk?.Invoke(chunk);
                        }
                    }
                }

                if (finalResult != null)
                {
                    if (string.IsNullOrWhiteSpace(finalResult.Output) && accumulatedTokens.Length > 0)
                    {
                        finalResult.Output = accumulatedTokens.ToString();
                    }
                    if (string.IsNullOrWhiteSpace(finalResult.Reasoning) && accumulatedReasoning.Length > 0)
                    {
                        finalResult.Reasoning = accumulatedReasoning.ToString();
                    }
                    return finalResult;
                }

                return new ChatFlowResult
                {
                    Status = "completed",
                    Output = accumulatedTokens.ToString(),
                    Reasoning = accumulatedReasoning.Length > 0 ? accumulatedReasoning.ToString() : null,
                    DurationMs = Math.Round((DateTime.UtcNow - startTime).TotalMilliseconds, 1)
                };
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new ChatFlowResult
                {
                    Status = "cancelled",
                    Error = ex.Message,
                    Output = string.Empty
                };
            }
        }

        public async Task<bool> ResetSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            string url = $"{BaseUrl.TrimEnd('/')}/api/memory/sessions/{Uri.EscapeDataString(sessionId)}";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(10000);

            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Delete, url);
                using var resp = await SharedHttpClient.SendAsync(httpReq, timeoutCts.Token).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Utility to encode any local file into a base64 Data URI string.
        /// </summary>
        public static string EncodeFileToDataUri(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Attachment file not found.", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".bin";
            string mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }

        #region Generic JSON Serialization & Parsing

        public static string BuildRequestJson(
            string workflowId,
            string inputText,
            string? sessionId,
            IDictionary<string, object?>? context)
        {
            var root = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_id"] = workflowId ?? string.Empty,
                ["input_text"] = inputText ?? string.Empty
            };

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                root["session_id"] = sessionId;
            }

            if (context != null && context.Count > 0)
            {
                root["context"] = context;
            }

            var sb = new StringBuilder(512);
            WriteJsonValue(sb, root);
            return sb.ToString();
        }

        public static ChatFlowResult ParseResultJson(string json)
        {
            var result = new ChatFlowResult();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var root = SimpleJsonReader.ParseObject(json);
            if (root == null) return result;

            if (root.TryGetValue("status", out var st) && st is string stStr)
                result.Status = stStr;
            if (root.TryGetValue("final_output", out var fo) && fo is string foStr)
                result.Output = foStr;
            else if (root.TryGetValue("output", out var outObj) && outObj is string outStr)
                result.Output = outStr;

            if (root.TryGetValue("reasoning", out var rs) && rs is string rsStr)
                result.Reasoning = rsStr;

            if (root.TryGetValue("error", out var err) && err is string errStr)
                result.Error = errStr;
            else if (root.TryGetValue("detail", out var det) && det is string detStr)
                result.Error = detStr;

            if (root.TryGetValue("total_duration_ms", out var durObj) && durObj != null)
            {
                if (double.TryParse(Convert.ToString(durObj, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                {
                    result.DurationMs = dur;
                }
            }

            // Parse generic context/entity updates
            foreach (var dictKey in new[] { "entity_memory", "context_updates" })
            {
                if (root.TryGetValue(dictKey, out var memObj) && memObj is Dictionary<string, object?> memDict)
                {
                    foreach (var kv in memDict)
                    {
                        if (kv.Value != null)
                        {
                            string valStr = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(valStr))
                            {
                                result.ContextUpdates[kv.Key] = valStr;
                            }
                        }
                    }
                }
            }

            // Parse generic action items
            if (root.TryGetValue("actions", out var actObj) && actObj is List<object?> actList)
            {
                foreach (var item in actList)
                {
                    if (item is Dictionary<string, object?> actDict)
                    {
                        var action = new ChatActionItem();

                        if (actDict.TryGetValue("action_type", out var at) && at is string atStr)
                            action.ActionType = atStr;
                        if (actDict.TryGetValue("feature_code", out var fc) && fc is string fcStr)
                            action.TargetKey = fcStr;
                        else if (actDict.TryGetValue("target_key", out var tk) && tk is string tkStr)
                            action.TargetKey = tkStr;

                        if (actDict.TryGetValue("label", out var lb) && lb is string lbStr)
                            action.Label = lbStr;
                        if (actDict.TryGetValue("domain", out var dm) && dm is string dmStr)
                            action.Category = dmStr;
                        if (actDict.TryGetValue("entity_code", out var ec) && ec is string ecStr)
                            action.ReferenceValue = ecStr;
                        else if (actDict.TryGetValue("reference_value", out var rv) && rv is string rvStr)
                            action.ReferenceValue = rvStr;

                        if (actDict.TryGetValue("allowed_modes", out var amObj) && amObj is List<object?> amList)
                        {
                            foreach (var m in amList)
                            {
                                if (m is string mStr && !string.IsNullOrWhiteSpace(mStr))
                                    action.AllowedModes.Add(mStr);
                            }
                        }

                        if (actDict.TryGetValue("permissions", out var permObj) && permObj is Dictionary<string, object?> permDict)
                        {
                            foreach (var pk in permDict)
                            {
                                if (pk.Value is bool bVal)
                                    action.Flags[pk.Key] = bVal;
                                else if (pk.Value != null && bool.TryParse(pk.Value.ToString(), out bool parsedB))
                                    action.Flags[pk.Key] = parsedB;
                            }
                        }

                        foreach (var kv in actDict)
                        {
                            if (kv.Value is string sVal && !action.Metadata.ContainsKey(kv.Key))
                            {
                                action.Metadata[kv.Key] = sVal;
                            }
                        }

                        result.Actions.Add(action);
                    }
                }
            }

            return result;
        }

        private static ChatStreamChunk ParseStreamChunk(string eventType, string dataJson)
        {
            if (string.Equals(eventType, "workflow_complete", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return new ChatStreamChunk
                {
                    EventType = "complete",
                    FinalResult = ParseResultJson(dataJson)
                };
            }

            var dict = SimpleJsonReader.ParseObject(dataJson);
            if (dict == null)
            {
                return new ChatStreamChunk { EventType = eventType };
            }

            if (string.Equals(eventType, "node_start", StringComparison.OrdinalIgnoreCase))
            {
                string label = dict.TryGetValue("label", out var l) && l is string ls && !string.IsNullOrWhiteSpace(ls)
                    ? ls
                    : (dict.TryGetValue("node_type", out var nt) && nt is string nts ? nts : "Node");
                return new ChatStreamChunk
                {
                    EventType = "status",
                    StatusText = label
                };
            }

            return new ChatStreamChunk
            {
                EventType = eventType,
                Token = dict.TryGetValue("token", out var tok) && tok is string ts ? ts : null,
                Error = dict.TryGetValue("error", out var err) && err is string es ? es : null
            };
        }

        private static void WriteJsonValue(StringBuilder sb, object? value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string s)
            {
                sb.Append('"').Append(EscapeJson(s)).Append('"');
                return;
            }

            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is double || value is float || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key == null || entry.Value == null) continue;
                    if (!first) sb.Append(',');
                    sb.Append('"').Append(EscapeJson(entry.Key.ToString() ?? string.Empty)).Append("\":");
                    WriteJsonValue(sb, entry.Value);
                    first = false;
                }
                sb.Append('}');
                return;
            }

            if (value is IEnumerable enumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(',');
                    WriteJsonValue(sb, item);
                    first = false;
                }
                sb.Append(']');
                return;
            }

            sb.Append('"').Append(EscapeJson(value.ToString() ?? string.Empty)).Append('"');
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Minimal Recursive JSON Reader

        internal static class SimpleJsonReader
        {
            public static Dictionary<string, object?>? ParseObject(string json)
            {
                int idx = 0;
                return ReadValue(json, ref idx) as Dictionary<string, object?>;
            }

            private static object? ReadValue(string s, ref int i)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) return null;

                char c = s[i];
                if (c == '{') return ReadObject(s, ref i);
                if (c == '[') return ReadArray(s, ref i);
                if (c == '"') return ReadString(s, ref i);
                if (c == 't' || c == 'f') return ReadBoolean(s, ref i);
                if (c == 'n') return ReadNull(s, ref i);
                return ReadNumberOrLiteral(s, ref i);
            }

            private static Dictionary<string, object?> ReadObject(string s, ref int i)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                i++;

                while (i < s.Length)
                {
                    SkipWhitespace(s, ref i);
                    if (i >= s.Length) break;
                    if (s[i] == '}')
                    {
                        i++;
                        break;
                    }
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    string key = ReadString(s, ref i);
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;

                    object? val = ReadValue(s, ref i);
                    dict[key] = val;
                }

                return dict;
            }

            private static List<object?> ReadArray(string s, ref int i)
            {
                var list = new List<object?>();
                i++;

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

                    list.Add(ReadValue(s, ref i));
                }

                return list;
            }

            private static string ReadString(string s, ref int i)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') return string.Empty;
                i++;

                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') break;
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
                                if (i + 4 <= s.Length &&
                                    int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
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

            private static bool ReadBoolean(string s, ref int i)
            {
                if (s.Length - i >= 4 && string.Compare(s, i, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    i += 4;
                    return true;
                }
                if (s.Length - i >= 5 && string.Compare(s, i, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    i += 5;
                    return false;
                }
                i++;
                return false;
            }

            private static object? ReadNull(string s, ref int i)
            {
                if (s.Length - i >= 4 && string.Compare(s, i, "null", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    i += 4;
                    return null;
                }
                i++;
                return null;
            }

            private static object ReadNumberOrLiteral(string s, ref int i)
            {
                int start = i;
                while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
                string raw = s.Substring(start, i - start);
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }
                return raw;
            }

            private static void SkipWhitespace(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
            }
        }

        #endregion
    }
}
