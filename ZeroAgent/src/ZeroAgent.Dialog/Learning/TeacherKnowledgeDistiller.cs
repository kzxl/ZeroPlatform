using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Dialog.Learning
{
    public sealed class DistilledAlias
    {
        public string Alias { get; set; } = string.Empty;
        public string TargetEntityCode { get; set; } = string.Empty;

        public DistilledAlias() { }
        public DistilledAlias(string alias, string targetEntityCode)
        {
            Alias = alias;
            TargetEntityCode = targetEntityCode;
        }
    }

    public sealed class DistilledRule
    {
        public string RuleKey { get; set; } = string.Empty;
        public string ProposedValue { get; set; } = string.Empty;

        public DistilledRule() { }
        public DistilledRule(string ruleKey, string proposedValue)
        {
            RuleKey = ruleKey;
            ProposedValue = proposedValue;
        }
    }

    public sealed class DistilledFaq
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public DistilledFaq() { }
        public DistilledFaq(string title, string content)
        {
            Title = title;
            Content = content;
        }
    }

    public sealed class DistilledIncident
    {
        public string Issue { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;

        public DistilledIncident() { }
        public DistilledIncident(string issue, string resolution)
        {
            Issue = issue;
            Resolution = resolution;
        }
    }

    public sealed class TeacherDistillationResult
    {
        public bool Success { get; }
        public string? ErrorMessage { get; }
        public IReadOnlyList<DistilledAlias> Aliases { get; }
        public IReadOnlyList<DistilledRule> Rules { get; }
        public IReadOnlyList<DistilledFaq> FaqItems { get; }
        public IReadOnlyList<DistilledIncident> IncidentEpisodes { get; }
        public int TotalInjectedCount { get; }
        public string RawTeacherResponse { get; }

        public TeacherDistillationResult(
            bool success,
            string? errorMessage,
            IReadOnlyList<DistilledAlias> aliases,
            IReadOnlyList<DistilledRule> rules,
            IReadOnlyList<DistilledFaq> faqItems,
            IReadOnlyList<DistilledIncident> incidentEpisodes,
            int totalInjectedCount,
            string rawTeacherResponse)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Aliases = aliases ?? Array.Empty<DistilledAlias>();
            Rules = rules ?? Array.Empty<DistilledRule>();
            FaqItems = faqItems ?? Array.Empty<DistilledFaq>();
            IncidentEpisodes = incidentEpisodes ?? Array.Empty<DistilledIncident>();
            TotalInjectedCount = totalInjectedCount;
            RawTeacherResponse = rawTeacherResponse ?? string.Empty;
        }

        public static TeacherDistillationResult Failed(string error, string rawResponse = "") =>
            new TeacherDistillationResult(false, error, Array.Empty<DistilledAlias>(), Array.Empty<DistilledRule>(), Array.Empty<DistilledFaq>(), Array.Empty<DistilledIncident>(), 0, rawResponse);
    }

    public sealed class TrajectoryAuditResult
    {
        public bool Success { get; }
        public float Score { get; }
        public bool IsApproved { get; }
        public string Critique { get; }
        public string? DistilledLesson { get; }
        public bool LessonIngestedToMemory { get; }
        public string RawTeacherResponse { get; }

        public TrajectoryAuditResult(
            bool success,
            float score,
            bool isApproved,
            string critique,
            string? distilledLesson,
            bool lessonIngestedToMemory,
            string rawTeacherResponse)
        {
            Success = success;
            Score = score;
            IsApproved = isApproved;
            Critique = critique ?? string.Empty;
            DistilledLesson = distilledLesson;
            LessonIngestedToMemory = lessonIngestedToMemory;
            RawTeacherResponse = rawTeacherResponse ?? string.Empty;
        }

        public static TrajectoryAuditResult Failed(string error, string rawResponse = "") =>
            new TrajectoryAuditResult(false, 0.0f, false, error, null, false, rawResponse);
    }

    public sealed class SyntheticKnowledgeResult
    {
        public bool Success { get; }
        public string Topic { get; }
        public int GeneratedCount { get; }
        public IReadOnlyList<DistilledAlias> Aliases { get; }
        public IReadOnlyList<DistilledFaq> FaqItems { get; }
        public IReadOnlyList<DistilledIncident> Incidents { get; }
        public string? ErrorMessage { get; }

        public SyntheticKnowledgeResult(
            bool success,
            string topic,
            int generatedCount,
            IReadOnlyList<DistilledAlias> aliases,
            IReadOnlyList<DistilledFaq> faqItems,
            IReadOnlyList<DistilledIncident> incidents,
            string? errorMessage = null)
        {
            Success = success;
            Topic = topic ?? string.Empty;
            GeneratedCount = generatedCount;
            Aliases = aliases ?? Array.Empty<DistilledAlias>();
            FaqItems = faqItems ?? Array.Empty<DistilledFaq>();
            Incidents = incidents ?? Array.Empty<DistilledIncident>();
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Master Teacher Knowledge Distiller:
    /// Leverages an advanced Teacher Model (e.g. OpenAI GPT-4o, DeepSeek, Claude, vLLM)
    /// to rapidly ingest, distill, and verify enterprise domain knowledge into ZeroPlatform's
    /// deterministic memory stores (AdminKnowledgeManager, SemanticMemory, EpisodicMemory).
    /// </summary>
    public sealed class TeacherKnowledgeDistiller
    {
        private readonly ILlmClient _teacherClient;
        private readonly IAdminKnowledgeManager? _knowledgeManager;
        private readonly AgenticMemoryEngine? _memoryEngine;

        public TeacherKnowledgeDistiller(
            ILlmClient teacherClient,
            IAdminKnowledgeManager? knowledgeManager = null,
            AgenticMemoryEngine? memoryEngine = null)
        {
            _teacherClient = teacherClient ?? throw new ArgumentNullException(nameof(teacherClient));
            _knowledgeManager = knowledgeManager;
            _memoryEngine = memoryEngine;
        }

        /// <summary>
        /// Distills unstructured SOPs, technical manuals, or operational documentation
        /// into structured domain aliases, rules, FAQs, and incident resolutions,
        /// automatically seeding them into AdminKnowledgeManager and SemanticMemory.
        /// </summary>
        public async Task<TeacherDistillationResult> DistillDocumentAsync(
            string documentText,
            string category = "General",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(documentText))
            {
                return TeacherDistillationResult.Failed("Document text cannot be null or empty.");
            }

            string prompt = 
@"You are an Expert AI Domain Teacher and Knowledge Engineer.
Extract high-value structured operational knowledge from the following technical document.
Your response MUST be ONLY a single valid JSON object strictly matching this schema:
{
  ""aliases"": [
    { ""alias"": ""colloquial name or synonym"", ""targetEntityCode"": ""canonical code or SKU"" }
  ],
  ""rules"": [
    { ""ruleKey"": ""domain rule key"", ""proposedValue"": ""constraint or operational value"" }
  ],
  ""faqItems"": [
    { ""title"": ""scenario or query title"", ""content"": ""verified procedure or answer"" }
  ],
  ""incidentEpisodes"": [
    { ""issue"": ""symptom or error condition"", ""resolution"": ""step-by-step resolution"" }
  ]
}

Document:
" + documentText;

            string teacherResponse;
            try
            {
                teacherResponse = await _teacherClient.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return TeacherDistillationResult.Failed($"Teacher LLM inference failed: {ex.Message}");
            }

            try
            {
                string jsonPayload = ExtractJsonPayload(teacherResponse);
                using (var doc = JsonDocument.Parse(jsonPayload))
                {
                    var root = doc.RootElement;
                    var aliases = new List<DistilledAlias>();
                    var rules = new List<DistilledRule>();
                    var faqs = new List<DistilledFaq>();
                    var incidents = new List<DistilledIncident>();
                    int injectedCount = 0;

                    // 1. Process Aliases
                    if (root.TryGetProperty("aliases", out var aliasesEl) && aliasesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in aliasesEl.EnumerateArray())
                        {
                            string alias = el.TryGetProperty("alias", out var aProp) ? aProp.GetString() ?? "" : "";
                            string target = el.TryGetProperty("targetEntityCode", out var tProp) ? tProp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(target))
                            {
                                aliases.Add(new DistilledAlias(alias, target));
                                if (_knowledgeManager != null)
                                {
                                    _knowledgeManager.TeachAlias(alias, target, category, "TeacherLLM", "Distilled from document");
                                    injectedCount++;
                                }
                            }
                        }
                    }

                    // 2. Process Rules
                    if (root.TryGetProperty("rules", out var rulesEl) && rulesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in rulesEl.EnumerateArray())
                        {
                            string key = el.TryGetProperty("ruleKey", out var kProp) ? kProp.GetString() ?? "" : "";
                            string val = el.TryGetProperty("proposedValue", out var vProp) ? vProp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                            {
                                rules.Add(new DistilledRule(key, val));
                                if (_knowledgeManager != null)
                                {
                                    _knowledgeManager.TeachRule(key, val, category, "TeacherLLM", "Distilled from document");
                                    injectedCount++;
                                }
                            }
                        }
                    }

                    // 3. Process FAQs -> Semantic Memory
                    if (root.TryGetProperty("faqItems", out var faqEl) && faqEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in faqEl.EnumerateArray())
                        {
                            string title = el.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                            string content = el.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(content))
                            {
                                faqs.Add(new DistilledFaq(title, content));
                                if (_memoryEngine != null)
                                {
                                    var emb = _memoryEngine.Embedder.Embed(title + " " + content);
                                    _memoryEngine.Semantic.Add(title, content, emb.AsSpan(), category);
                                    injectedCount++;
                                }
                            }
                        }
                    }

                    // 4. Process Incident Episodes -> Episodic Memory
                    if (root.TryGetProperty("incidentEpisodes", out var incEl) && incEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in incEl.EnumerateArray())
                        {
                            string issue = el.TryGetProperty("issue", out var iProp) ? iProp.GetString() ?? "" : "";
                            string res = el.TryGetProperty("resolution", out var rProp) ? rProp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(issue) && !string.IsNullOrWhiteSpace(res))
                            {
                                incidents.Add(new DistilledIncident(issue, res));
                                if (_memoryEngine != null)
                                {
                                    var emb = _memoryEngine.Embedder.Embed(issue + " " + res);
                                    _memoryEngine.Episodic.Record(issue, res, emb.AsSpan(), success: true);
                                    injectedCount++;
                                }
                            }
                        }
                    }

                    return new TeacherDistillationResult(
                        true,
                        null,
                        aliases,
                        rules,
                        faqs,
                        incidents,
                        injectedCount,
                        teacherResponse);
                }
            }
            catch (Exception ex)
            {
                return TeacherDistillationResult.Failed($"Failed to parse teacher knowledge output: {ex.Message}", teacherResponse);
            }
        }

        /// <summary>
        /// Teacher-as-a-Judge: Audits an agent execution trajectory (ReAct / ToT trace).
        /// If the trajectory achieves high quality, automatically distills an operational lesson
        /// and records it into EpisodicMemory for long-term continuous learning.
        /// </summary>
        public Task<TrajectoryAuditResult> AuditTrajectoryAsync(
            AgentContext context,
            AgentResponse response,
            float minApprovalScore = 0.7f,
            CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (response == null) throw new ArgumentNullException(nameof(response));

            return AuditTrajectoryAsync(context.Goal, response.ExecutionTrace, response.Output, minApprovalScore, cancellationToken);
        }

        /// <summary>
        /// Audits a goal, execution trace, and final output using the Teacher LLM.
        /// </summary>
        public async Task<TrajectoryAuditResult> AuditTrajectoryAsync(
            string goal,
            IReadOnlyList<AgentMessage> executionTrace,
            string finalAnswer,
            float minApprovalScore = 0.7f,
            CancellationToken cancellationToken = default)
        {
            var traceBuilder = new System.Text.StringBuilder();
            if (executionTrace != null)
            {
                for (int i = 0; i < executionTrace.Count; i++)
                {
                    var msg = executionTrace[i];
                    string sender = !string.IsNullOrWhiteSpace(msg.Name) ? $"{msg.Role} ({msg.Name})" : msg.Role.ToString();
                    traceBuilder.AppendLine($"[Step {i + 1}] {sender}: {msg.Content}");
                }
            }

            string prompt = 
@"You are an Expert AI Supervisor and Judge.
Audit the following AI agent reasoning trace and execution output:
Goal: " + (goal ?? string.Empty) + @"
Execution Trace:
" + traceBuilder.ToString() + @"
Final Answer:
" + (finalAnswer ?? string.Empty) + @"

Evaluate if the agent solved the problem accurately, cleanly, and safely.
Output ONLY a single valid JSON object strictly matching this schema:
{
  ""score"": 0.85,
  ""isApproved"": true,
  ""critique"": ""Detailed critique on reasoning and tool efficiency"",
  ""distilledLesson"": ""A concise operational guideline to remember for similar future situations""
}";

            string teacherResponse;
            try
            {
                teacherResponse = await _teacherClient.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return TrajectoryAuditResult.Failed($"Teacher LLM inference failed: {ex.Message}");
            }

            try
            {
                string jsonPayload = ExtractJsonPayload(teacherResponse);
                using (var doc = JsonDocument.Parse(jsonPayload))
                {
                    var root = doc.RootElement;
                    float score = 0.0f;
                    if (root.TryGetProperty("score", out var sProp) && sProp.TryGetSingle(out float sVal))
                    {
                        score = sVal;
                    }

                    bool isApproved = root.TryGetProperty("isApproved", out var aProp) && aProp.GetBoolean();
                    string critique = root.TryGetProperty("critique", out var cProp) ? cProp.GetString() ?? "" : "";
                    string? distilledLesson = root.TryGetProperty("distilledLesson", out var lProp) ? lProp.GetString() : null;

                    bool ingested = false;
                    if (isApproved && score >= minApprovalScore && !string.IsNullOrWhiteSpace(distilledLesson) && _memoryEngine != null)
                    {
                        var emb = _memoryEngine.Embedder.Embed((goal ?? "") + " " + distilledLesson);
                        _memoryEngine.Episodic.Record(goal ?? "Audited Task", distilledLesson!, emb.AsSpan(), success: true);
                        ingested = true;
                    }

                    return new TrajectoryAuditResult(
                        true,
                        score,
                        isApproved,
                        critique,
                        distilledLesson,
                        ingested,
                        teacherResponse);
                }
            }
            catch (Exception ex)
            {
                return TrajectoryAuditResult.Failed($"Failed to parse teacher audit response: {ex.Message}", teacherResponse);
            }
        }

        /// <summary>
        /// Synthesizes cold-start domain knowledge (aliases, FAQs, incidents) for bootstrapping new enterprise projects.
        /// </summary>
        public async Task<SyntheticKnowledgeResult> GenerateSyntheticKnowledgeAsync(
            string domainTopic,
            int count = 3,
            string category = "General",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domainTopic))
            {
                return new SyntheticKnowledgeResult(false, domainTopic, 0, null!, null!, null!, "Topic cannot be empty.");
            }

            string prompt = 
$@"You are an Expert AI Knowledge Engineer.
Generate {count} realistic operational knowledge items for the domain: ""{domainTopic}"".
Output ONLY a single valid JSON object strictly matching this schema:
{{
  ""aliases"": [
    {{ ""alias"": ""colloquial name"", ""targetEntityCode"": ""CODE-123"" }}
  ],
  ""faqItems"": [
    {{ ""title"": ""operational question"", ""content"": ""step-by-step procedure"" }}
  ],
  ""incidentEpisodes"": [
    {{ ""issue"": ""observed failure"", ""resolution"": ""verified resolution"" }}
  ]
}}";

            string teacherResponse;
            try
            {
                teacherResponse = await _teacherClient.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new SyntheticKnowledgeResult(false, domainTopic, 0, null!, null!, null!, ex.Message);
            }

            try
            {
                string jsonPayload = ExtractJsonPayload(teacherResponse);
                using (var doc = JsonDocument.Parse(jsonPayload))
                {
                    var root = doc.RootElement;
                    var aliases = new List<DistilledAlias>();
                    var faqs = new List<DistilledFaq>();
                    var incidents = new List<DistilledIncident>();

                    if (root.TryGetProperty("aliases", out var aEl) && aEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in aEl.EnumerateArray())
                        {
                            string a = el.TryGetProperty("alias", out var ap) ? ap.GetString() ?? "" : "";
                            string c = el.TryGetProperty("targetEntityCode", out var cp) ? cp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(c))
                            {
                                aliases.Add(new DistilledAlias(a, c));
                                _knowledgeManager?.TeachAlias(a, c, category, "TeacherLLM", "Synthetic bootstrapping");
                            }
                        }
                    }

                    if (root.TryGetProperty("faqItems", out var fEl) && fEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in fEl.EnumerateArray())
                        {
                            string t = el.TryGetProperty("title", out var tp) ? tp.GetString() ?? "" : "";
                            string c = el.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(t) && !string.IsNullOrWhiteSpace(c))
                            {
                                faqs.Add(new DistilledFaq(t, c));
                                if (_memoryEngine != null)
                                {
                                    var emb = _memoryEngine.Embedder.Embed(t + " " + c);
                                    _memoryEngine.Semantic.Add(t, c, emb.AsSpan(), category);
                                }
                            }
                        }
                    }

                    if (root.TryGetProperty("incidentEpisodes", out var iEl) && iEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in iEl.EnumerateArray())
                        {
                            string issue = el.TryGetProperty("issue", out var ip) ? ip.GetString() ?? "" : "";
                            string res = el.TryGetProperty("resolution", out var rp) ? rp.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(issue) && !string.IsNullOrWhiteSpace(res))
                            {
                                incidents.Add(new DistilledIncident(issue, res));
                                if (_memoryEngine != null)
                                {
                                    var emb = _memoryEngine.Embedder.Embed(issue + " " + res);
                                    _memoryEngine.Episodic.Record(issue, res, emb.AsSpan(), success: true);
                                }
                            }
                        }
                    }

                    int total = aliases.Count + faqs.Count + incidents.Count;
                    return new SyntheticKnowledgeResult(true, domainTopic, total, aliases, faqs, incidents);
                }
            }
            catch (Exception ex)
            {
                return new SyntheticKnowledgeResult(false, domainTopic, 0, null!, null!, null!, $"Parse failed: {ex.Message}");
            }
        }

        private static string ExtractJsonPayload(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";

            string trimmed = text.Trim();

            // Check markdown fence ```json ... ``` or ``` ... ```
            int fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
            if (fenceStart >= 0)
            {
                int contentStart = trimmed.IndexOf('\n', fenceStart);
                if (contentStart > fenceStart)
                {
                    int fenceEnd = trimmed.IndexOf("```", contentStart, StringComparison.Ordinal);
                    if (fenceEnd > contentStart)
                    {
                        return trimmed.Substring(contentStart + 1, fenceEnd - contentStart - 1).Trim();
                    }
                }
            }

            // Fallback: look for opening { and closing }
            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            return trimmed;
        }
    }
}
