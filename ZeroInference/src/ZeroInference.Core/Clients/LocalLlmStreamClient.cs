using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace ZeroInference.Core.Clients
{
    /// <summary>
    /// Type of local LLM server protocol.
    /// </summary>
    public enum LlmServerProtocol
    {
        /// <summary>
        /// Native Ollama API (/api/generate or /api/chat).
        /// </summary>
        Ollama,

        /// <summary>
        /// OpenAI-compatible Server-Sent Events API (LM Studio, llama.cpp server, vLLM, LocalAI).
        /// </summary>
        OpenAiCompatible
    }

    /// <summary>
    /// Zero-dependency client for local private LLMs (Ollama, LM Studio, llama.cpp, LocalAI).
    /// Supports both synchronous/buffered generation and real-time Server-Sent Events (SSE) streaming.
    /// </summary>
    public sealed class LocalLlmStreamClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;

        public Uri Endpoint { get; set; }
        public string ModelName { get; set; }
        public LlmServerProtocol Protocol { get; set; }
        public float Temperature { get; set; } = 0.7f;

        public LocalLlmStreamClient(
            string endpoint = "http://localhost:11434/api/generate",
            string modelName = "llama3:latest",
            LlmServerProtocol protocol = LlmServerProtocol.Ollama,
            HttpClient? httpClient = null)
        {
            Endpoint = new Uri(endpoint);
            ModelName = modelName;
            Protocol = protocol;

            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }
            else
            {
                _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                _disposeClient = true;
            }
        }

        /// <summary>
        /// Performs non-streaming text generation and returns the complete text response.
        /// </summary>
        public async Task<string> GenerateAsync(string prompt, string? systemPrompt = null, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            await StreamGenerateAsync(prompt, token => sb.Append(token), systemPrompt, ct).ConfigureAwait(false);
            return sb.ToString();
        }

        /// <summary>
        /// Streams tokens in real-time invoking the callback as each token chunk is received.
        /// </summary>
        public async Task StreamGenerateAsync(
            string prompt,
            Action<string> onTokenReceived,
            string? systemPrompt = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return;

            string jsonPayload = BuildRequestPayload(prompt, systemPrompt, stream: true);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content })
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string? token = ExtractTokenFromStreamLine(line, Protocol);
                        if (!string.IsNullOrEmpty(token))
                        {
                            onTokenReceived(token!);
                        }
                    }
                }
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Streams tokens asynchronously as an IAsyncEnumerable sequence.
        /// </summary>
        public async IAsyncEnumerable<string> StreamGenerateAsync(
            string prompt,
            string? systemPrompt = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) yield break;

            string jsonPayload = BuildRequestPayload(prompt, systemPrompt, stream: true);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? token = ExtractTokenFromStreamLine(line, Protocol);
                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }
        }
#endif

        /// <summary>
        /// Builds JSON request payload appropriate for the configured protocol.
        /// </summary>
        internal string BuildRequestPayload(string prompt, string? systemPrompt, bool stream)
        {
            if (Protocol == LlmServerProtocol.Ollama)
            {
                var payload = new
                {
                    model = ModelName,
                    prompt = prompt,
                    system = systemPrompt,
                    stream = stream,
                    options = new { temperature = Temperature }
                };
                return JsonSerializer.Serialize(payload);
            }
            else
            {
                // OpenAI-compatible chat format
                object[] messages;
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    };
                }
                else
                {
                    messages = new object[]
                    {
                        new { role = "user", content = prompt }
                    };
                }

                var payload = new
                {
                    model = ModelName,
                    messages = messages,
                    stream = stream,
                    temperature = Temperature
                };
                return JsonSerializer.Serialize(payload);
            }
        }

        /// <summary>
        /// Parses a single SSE or line-delimited JSON line into token text.
        /// </summary>
        public static string? ExtractTokenFromStreamLine(string rawLine, LlmServerProtocol protocol)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) return null;

            string line = rawLine.Trim();

            // Handle SSE prefix (data: ...)
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                line = line.Substring(5).Trim();
                if (line == "[DONE]") return null;
            }

            if (line.Length < 2 || line[0] != '{') return null;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // 1. Check Ollama native: "response": "text"
                if (root.TryGetProperty("response", out var respProp))
                {
                    return respProp.GetString();
                }

                // 2. Check Ollama chat: "message": { "content": "text" }
                if (root.TryGetProperty("message", out var msgProp) && msgProp.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString();
                }

                // 3. Check OpenAI streaming: "choices": [ { "delta": { "content": "text" } } ]
                if (root.TryGetProperty("choices", out var choicesProp) && choicesProp.GetArrayLength() > 0)
                {
                    var first = choicesProp[0];
                    if (first.TryGetProperty("delta", out var deltaProp) && deltaProp.TryGetProperty("content", out var deltaContent))
                    {
                        return deltaContent.GetString();
                    }
                    if (first.TryGetProperty("text", out var textProp))
                    {
                        return textProp.GetString();
                    }
                }
            }
            catch
            {
                // Invalid JSON chunk in stream
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposeClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
