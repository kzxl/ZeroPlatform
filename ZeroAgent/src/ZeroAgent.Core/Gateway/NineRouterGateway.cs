using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Core.Gateway
{
    /// <summary>
    /// Operating role for 9Router multi-tier model distribution.
    /// </summary>
    public enum NineRouterRole
    {
        /// <summary>
        /// Fast, responsive conversational chat and surface generation (e.g. qwen2.5:3b).
        /// </summary>
        Chat,

        /// <summary>
        /// Logical audit, trajectory reflection, and rule-invariant feedback (e.g. qwen2.5:7b, deepseek-chat).
        /// </summary>
        Feedback,

        /// <summary>
        /// Knowledge distillation, synthetic scenario extraction, and continuous learning (e.g. qwen2.5:7b, gpt-4o, claude).
        /// </summary>
        Teacher
    }

    /// <summary>
    /// Configuration options for connecting to the 9Router AI Gateway.
    /// Exposes unified routing for Chat, Feedback, and Teacher inference tiers.
    /// </summary>
    public sealed class NineRouterOptions
    {
        /// <summary>
        /// 9Router endpoint URL. Defaults to "http://localhost:20128/v1".
        /// Automatically normalizes paths without /v1 or /chat/completions.
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:20128/v1";

        /// <summary>
        /// Optional master authentication key configured on 9Router.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Model identifier for conversational answers and chat (Tier 1).
        /// Recommended: Fast local model (e.g. "qwen2.5:3b").
        /// </summary>
        public string ChatModel { get; set; } = "qwen2.5:3b";

        /// <summary>
        /// Model identifier for audit critique, trajectory reflexions, and rule checking (Tier 2).
        /// Recommended: Balanced analytical model (e.g. "qwen2.5:7b", "deepseek-chat").
        /// </summary>
        public string FeedbackModel { get; set; } = "qwen2.5:7b";

        /// <summary>
        /// Model identifier for deep document knowledge distillation and SOP extraction (Tier 3).
        /// Recommended: High-capability reasoning model (e.g. "qwen2.5:7b", "deepseek-chat", "gpt-4o").
        /// </summary>
        public string TeacherModel { get; set; } = "qwen2.5:7b";

        /// <summary>
        /// HTTP request timeout in seconds. Defaults to 60.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Optional custom HTTP headers sent to 9Router (e.g. routing tags, client ID).
        /// </summary>
        public IDictionary<string, string>? CustomHeaders { get; set; }

        /// <summary>
        /// Resolves the absolute chat completions endpoint from BaseUrl.
        /// </summary>
        public string GetNormalizedChatEndpoint()
        {
            string url = BaseUrl.TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return url + "/chat/completions";
            }
            return url + "/v1/chat/completions";
        }
    }

    /// <summary>
    /// Sovereign Multi-Tier Gateway Connector for 9Router (Nine Router).
    /// Provides unified OpenAI-compatible routing for:
    /// 1. Trả lời (Chat / Surface Synthesis Client)
    /// 2. Feedback (Critique / Audit / Reflexion Client)
    /// 3. Teacher (Knowledge Distillation & Continuous Learning Client)
    /// </summary>
    public sealed class NineRouterGateway : IDisposable
    {
        private readonly NineRouterOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;

        private readonly ILlmClient _chatClient;
        private readonly ILlmClient _feedbackClient;
        private readonly ILlmClient _teacherClient;

        public NineRouterOptions Options => _options;

        /// <summary>
        /// Client specialized for conversational answering and NLG surface generation (Role: Chat).
        /// </summary>
        public ILlmClient ChatClient => _chatClient;

        /// <summary>
        /// Client specialized for trajectory audit, invariant verification, and critique reflexions (Role: Feedback).
        /// </summary>
        public ILlmClient FeedbackClient => _feedbackClient;

        /// <summary>
        /// Client specialized for SOP document distillation, synthetic data generation, and teacher guidance (Role: Teacher).
        /// </summary>
        public ILlmClient TeacherClient => _teacherClient;

        public NineRouterGateway(NineRouterOptions options, HttpClient? httpClient = null)
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

            string endpoint = _options.GetNormalizedChatEndpoint();

            _chatClient = new ExternalApiLlmClient(new ExternalApiLlmOptions
            {
                Endpoint = endpoint,
                Model = _options.ChatModel,
                ApiKey = _options.ApiKey,
                Temperature = 0.5f,
                TimeoutSeconds = _options.TimeoutSeconds,
                CustomHeaders = _options.CustomHeaders
            }, _httpClient);

            _feedbackClient = new ExternalApiLlmClient(new ExternalApiLlmOptions
            {
                Endpoint = endpoint,
                Model = _options.FeedbackModel,
                ApiKey = _options.ApiKey,
                Temperature = 0.1f, // Deterministic audit
                TimeoutSeconds = _options.TimeoutSeconds,
                CustomHeaders = _options.CustomHeaders
            }, _httpClient);

            _teacherClient = new ExternalApiLlmClient(new ExternalApiLlmOptions
            {
                Endpoint = endpoint,
                Model = _options.TeacherModel,
                ApiKey = _options.ApiKey,
                Temperature = 0.2f, // Low temperature for high-fidelity extraction
                TimeoutSeconds = _options.TimeoutSeconds,
                CustomHeaders = _options.CustomHeaders
            }, _httpClient);
        }

        public NineRouterGateway(
            string baseUrl = "http://localhost:20128/v1",
            string? apiKey = null,
            string chatModel = "qwen2.5:3b",
            string feedbackModel = "qwen2.5:7b",
            string teacherModel = "qwen2.5:7b",
            HttpClient? httpClient = null)
            : this(new NineRouterOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                ChatModel = chatModel,
                FeedbackModel = feedbackModel,
                TeacherModel = teacherModel
            }, httpClient)
        {
        }

        /// <summary>
        /// Retrieves the pre-configured client for the specified 9Router role.
        /// </summary>
        public ILlmClient GetClientForRole(NineRouterRole role)
        {
            return role switch
            {
                NineRouterRole.Chat => _chatClient,
                NineRouterRole.Feedback => _feedbackClient,
                NineRouterRole.Teacher => _teacherClient,
                _ => _chatClient
            };
        }

        /// <summary>
        /// Creates an on-demand custom client for any specific model supported by 9Router.
        /// </summary>
        public ILlmClient CreateClientForModel(
            string modelName,
            string? systemPrompt = null,
            float temperature = 0.3f,
            int? maxTokens = null)
        {
            if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentNullException(nameof(modelName));

            return new ExternalApiLlmClient(new ExternalApiLlmOptions
            {
                Endpoint = _options.GetNormalizedChatEndpoint(),
                Model = modelName.Trim(),
                ApiKey = _options.ApiKey,
                SystemPrompt = systemPrompt,
                Temperature = temperature,
                MaxTokens = maxTokens,
                TimeoutSeconds = _options.TimeoutSeconds,
                CustomHeaders = _options.CustomHeaders
            }, _httpClient);
        }

        /// <summary>
        /// Pings the 9Router gateway to verify connectivity and responsiveness.
        /// </summary>
        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string healthUrl = _options.BaseUrl.TrimEnd('/');
                if (healthUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                {
                    healthUrl = healthUrl.Substring(0, healthUrl.Length - 3);
                }

                using (var req = new HttpRequestMessage(HttpMethod.Get, healthUrl))
                {
                    if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                    {
                        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");
                    }

                    using (var res = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false))
                    {
                        return res.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
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
