using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Core.Engine
{
    /// <summary>
    /// Configuration options for the external REST API LLM provider.
    /// Compatible with OpenAI, Azure OpenAI, Groq, Ollama, vLLM, DeepSeek, and custom gateways.
    /// </summary>
    public sealed class ExternalApiLlmOptions
    {
        /// <summary>
        /// Target endpoint URL (e.g. "https://api.openai.com/v1/chat/completions" or "http://localhost:11434/v1/chat/completions").
        /// </summary>
        public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// API key / Bearer token. Optional for local unauthenticated instances like Ollama / vLLM.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Model identifier (e.g. "gpt-4o-mini", "deepseek-chat", "qwen2.5-coder", "llama3.1").
        /// </summary>
        public string Model { get; set; } = "gpt-4o-mini";

        /// <summary>
        /// Sampling temperature in [0.0, 2.0]. Lower values yield more deterministic extractions.
        /// </summary>
        public float Temperature { get; set; } = 0.2f;

        /// <summary>
        /// Optional token generation budget.
        /// </summary>
        public int? MaxTokens { get; set; } = 512;

        /// <summary>
        /// Optional default system prompt to include in chat requests.
        /// </summary>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Request timeout in seconds. Defaults to 60 seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Optional custom HTTP headers (e.g. organization, routing tags, or gateway auth).
        /// </summary>
        public IDictionary<string, string>? CustomHeaders { get; set; }
    }

    /// <summary>
    /// Pure C# external REST API LLM client implementing <see cref="ILlmClient"/>.
    /// Supports OpenAI-compatible endpoints with zero external third-party dependencies.
    /// </summary>
    public sealed class ExternalApiLlmClient : ILlmClient, IDisposable
    {
        private readonly ExternalApiLlmOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;

        public ExternalApiLlmOptions Options => _options;

        public ExternalApiLlmClient(ExternalApiLlmOptions options, HttpClient? httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }
            else
            {
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds))
                };
                _disposeClient = true;
            }
        }

        public ExternalApiLlmClient(
            string endpoint,
            string model,
            string? apiKey = null,
            HttpClient? httpClient = null)
            : this(new ExternalApiLlmOptions
            {
                Endpoint = endpoint,
                Model = model,
                ApiKey = apiKey
            }, httpClient)
        {
        }

        /// <summary>
        /// Executes completion inference against the external LLM endpoint.
        /// </summary>
        public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var messages = new List<Dictionary<string, string>>();
            if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
            {
                messages.Add(new Dictionary<string, string>
                {
                    ["role"] = "system",
                    ["content"] = _options.SystemPrompt
                });
            }

            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "user",
                ["content"] = prompt
            });

            var payload = new Dictionary<string, object>
            {
                ["model"] = _options.Model,
                ["messages"] = messages,
                ["temperature"] = _options.Temperature
            };

            if (_options.MaxTokens.HasValue)
            {
                payload["max_tokens"] = _options.MaxTokens.Value;
            }

            string requestJson = JsonSerializer.Serialize(payload);

            using (var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint))
            {
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey!);
                }

                if (_options.CustomHeaders != null)
                {
                    foreach (var kvp in _options.CustomHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                    }
                }

                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            $"External LLM API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
                    }

                    return ExtractResponseContent(responseBody);
                }
            }
        }

        private static string ExtractResponseContent(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            using (var doc = JsonDocument.Parse(responseBody))
            {
                var root = doc.RootElement;

                // 1. OpenAI-compatible schema: choices[0].message.content
                if (root.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var content))
                        {
                            string contentStr = content.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(contentStr)) return contentStr;
                        }

                        // Support reasoning models (Gemma-4-E4B, DeepSeek-R1, QwQ)
                        if (message.TryGetProperty("reasoning_content", out var reasoning))
                        {
                            string reasoningStr = reasoning.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(reasoningStr)) return reasoningStr;
                        }
                    }

                    if (firstChoice.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }

                // 2. Ollama /api/generate schema: response
                if (root.TryGetProperty("response", out var ollamaResponse))
                {
                    return ollamaResponse.GetString() ?? string.Empty;
                }

                // 3. Fallback text property
                if (root.TryGetProperty("text", out var fallbackText))
                {
                    return fallbackText.GetString() ?? string.Empty;
                }

                throw new InvalidOperationException($"Unable to extract completion content from response: {responseBody}");
            }
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
