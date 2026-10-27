using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.Cognitive;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Generator;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Dialog.Engine
{
    /// <summary>
    /// Master Non-LLM Task-Oriented Dialogue Engine for Industrial & Operational Chatbots.
    /// Orchestrates 4-tier Agentic Memory, Dialogue State Tracking (DST), Tool Execution, and Template Generation.
    /// Operates entirely on CPU in sub-5ms with zero GPU/LLM dependencies.
    /// </summary>
    public sealed partial class ZeroDialogEngine
    {
        public IDialogSessionStore Sessions { get; }
        public AgenticMemoryEngine Memory { get; }
        public DialogueStateTracker Dst { get; }
        public AgentToolRegistry Tools { get; }
        public DialogueResponseGenerator Generator { get; } = new DialogueResponseGenerator();
        public INlgSynthesizer NlgSynthesizer { get; set; }
        public HitlSafetyGate? SafetyGate { get; }
        public ZabMixtureOfReflexes? MoEReflexSuite { get; set; }

        /// <summary>
        /// Two-Tier cognitive escalation handler.
        /// Invoked when Reflex NLU confidence is low (< 0.45) or intent is unresolved, delegating to ReAct deliberation.
        /// </summary>
        public Func<DialogueSession, WorkingMemory, UserProfile, string, Task<DialogResponse?>>? CognitiveEscalationHandler { get; set; }

        /// <summary>
        /// Minimum confidence score threshold for intent recognition (default: 0.35f).
        /// </summary>
        public float IntentConfidenceThreshold { get; set; } = 0.35f;

        /// <summary>
        /// Custom fallback message when user input does not match any intent or knowledge item.
        /// </summary>
        public string FallbackMessage { get; set; } = "Xin lỗi, câu hỏi của bạn nằm ngoài phạm vi hỗ trợ hoặc tôi chưa hiểu rõ yêu cầu. Vui lòng thử lại với các câu hỏi liên quan đến hệ thống.";

        public void SetCognitiveEscalationBridge(CognitiveEscalationBridge bridge)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            CognitiveEscalationHandler = bridge.EscalateAsync;
        }

        /// <summary>
        /// Enables generative natural language synthesis using the specified LLM client (Micro-SLM, 9Router, or External API).
        /// Automatically falls back to deterministic template rendering when the generative model is unavailable.
        /// </summary>
        public void UseGenerativeNlg(ILlmClient llmClient, string? customSystemDirective = null)
        {
            if (llmClient == null) throw new ArgumentNullException(nameof(llmClient));
            NlgSynthesizer = new GenerativeNlgSynthesizer(llmClient, new TemplateFallbackNlgSynthesizer(Generator), customSystemDirective);
        }

        public ZeroDialogEngine(
            AgentToolRegistry? tools = null, 
            HitlSafetyGate? safetyGate = null, 
            int dimension = 128,
            IDialogSessionStore? sessionStore = null)
        {
            Tools = tools ?? new AgentToolRegistry();
            SafetyGate = safetyGate;
            Memory = new AgenticMemoryEngine(dimension);
            Dst = new DialogueStateTracker(Memory.Embedder);
            Sessions = sessionStore ?? new InMemoryDialogSessionStore();
            NlgSynthesizer = new TemplateFallbackNlgSynthesizer(Generator);
        }

        /// <summary>
        /// Processes a conversational message from the user and returns an action or clarification response.
        /// </summary>
        public async Task<DialogResponse> ChatAsync(string sessionId, string userMessage, UserProfile? profile = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new DialogResponse("Tôi có thể giúp gì cho bạn trong việc giám sát thiết bị và vận hành nhà xưởng?", SessionState.Idle);
            }

            var workingMemory = Memory.GetWorkingMemory(sessionId);
            var session = GetOrCreateSession(sessionId);
            var userProfile = ResolveProfile(sessionId, profile);
            userProfile.Persona.RecordUtterance(userMessage);

            // Step 1: Anaphora / Coreference Resolution via Working Memory
            string resolvedMessage = workingMemory.ResolveAnaphora(userMessage);

            // Step 2: Vector embedding
            var queryEmbedding = Memory.Embedder.Embed(resolvedMessage);

            // Step 2.1: Mixture of Reflexes (MoR) / Modular MoE Domain & Security Gating
            if (MoEReflexSuite != null)
            {
                var reflex = MoEReflexSuite.Evaluate(queryEmbedding);
                if (reflex.IsBlockedBySecurity)
                {
                    string secMsg = "Cảnh báo an ninh: Yêu cầu của bạn bị từ chối do vi phạm quy tắc an toàn bảo mật hệ thống.";
                    workingMemory.AddTurn(userMessage, secMsg, "SECURITY_BLOCK");
                    return new DialogResponse(secMsg, SessionState.Idle, "SECURITY_BLOCK", confidence: reflex.SecurityRiskScore);
                }

                if (!string.IsNullOrEmpty(reflex.MatchedDomainId) && reflex.DomainAffinityScore >= 0.20f)
                {
                    session.ActiveDomain = reflex.MatchedDomainId;
                }
            }

            // Step 2.5: Check Semantic Response Cache (Short-circuit NLU/LLM if similarity >= 0.95 and session is idle/completed)
            if (session.State == SessionState.Idle || session.State == SessionState.Completed)
            {
                if (Memory.ResponseCache.TryGet(queryEmbedding, resolvedMessage, minSimilarity: 0.95f, out var cachedEntry) && cachedEntry != null)
                {
                    // Synchronize active slots from query so working memory stays updated on cache hits
                    Dst.AdvanceSession(session, resolvedMessage, null);
                    foreach (var kvp in session.Slots)
                    {
                        workingMemory.SetSlot(kvp.Key, kvp.Value);
                    }

                    workingMemory.AddTurn(userMessage, cachedEntry.ResponseText, cachedEntry.IntentName ?? "SEMANTIC_CACHE_HIT");
                    return new DialogResponse(
                        cachedEntry.ResponseText,
                        SessionState.Completed,
                        intentName: cachedEntry.IntentName ?? "SEMANTIC_CACHE_HIT",
                        confidence: cachedEntry.Similarity);
                }
            }

            // Step 3: Analytical Deliberation (Escalation to Tier-2 ReAct Agent)
            var analyticalResp = await TryEscalateAnalyticalQueryAsync(session, workingMemory, userProfile, resolvedMessage, userMessage).ConfigureAwait(false);
            if (analyticalResp != null)
            {
                return analyticalResp;
            }

            // Step 4: Intent Recognition
            var (detectedIntent, score) = Dst.MatchIntent(queryEmbedding, resolvedMessage);
            if (score < IntentConfidenceThreshold)
            {
                detectedIntent = null;
            }

            // Step 5: Memory vs Intent Conflict Arbitration (Knowledge / SOP vs Transactional Intents)
            var memoryResp = TryArbitrateKnowledgeOrHistory(session, workingMemory, userProfile, resolvedMessage, userMessage, queryEmbedding, detectedIntent, score);
            if (memoryResp != null)
            {
                return memoryResp;
            }

            // Step 6: Advance Dialogue State
            Dst.AdvanceSession(session, resolvedMessage, detectedIntent);

            if (session.CurrentIntent != null && !string.IsNullOrEmpty(session.CurrentIntent.Domain))
            {
                session.ActiveDomain = session.CurrentIntent.Domain;
            }

            foreach (var kvp in session.Slots)
            {
                workingMemory.SetSlot(kvp.Key, kvp.Value);
            }

            // Step 7: Handle dialogue state outcomes
            if (session.State == SessionState.CollectingSlots && !string.IsNullOrEmpty(session.PendingRequiredSlot))
            {
                string pending = session.PendingRequiredSlot!;
                string prompt = session.CurrentIntent?.SlotClarificationPrompts.TryGetValue(pending, out var p) == true
                    ? p
                    : $"Vui lòng cung cấp thông tin cho [{pending}]:";

                string clarText = Generator.FormatClarification(prompt);
                workingMemory.AddTurn(userMessage, clarText, session.CurrentIntent?.Name ?? "UNKNOWN");
                return new DialogResponse(clarText, SessionState.CollectingSlots, session.CurrentIntent?.Name, session.Slots, false, score);
            }

            if (session.State == SessionState.ReadyToExecute && session.CurrentIntent != null)
            {
                return await ExecuteIntentAsync(session, workingMemory, userProfile, userMessage, resolvedMessage, queryEmbedding, score).ConfigureAwait(false);
            }

            // Step 8: Cognitive Escalation Bridge (Two-Tier Deliberation)
            if (CognitiveEscalationHandler != null)
            {
                var escalated = await CognitiveEscalationHandler(session, workingMemory, userProfile, resolvedMessage).ConfigureAwait(false);
                if (escalated != null)
                {
                    workingMemory.AddTurn(userMessage, escalated.Text, escalated.IntentName ?? "COGNITIVE_DELIBERATION_REACT");
                    return escalated;
                }
            }

            // Step 8.5: Fallback Knowledge Retrieval from Semantic Memory for Unmatched Queries
            var fallbackFaq = Memory.Semantic.Query(queryEmbedding, topK: 1, minScore: 0.50f);
            if (fallbackFaq.Count > 0)
            {
                var doc = fallbackFaq[0].Item;
                string faqAnswer = $"📖 [Tài liệu {doc.Category} - {doc.Title}]:\n{doc.Content}";
                workingMemory.AddTurn(userMessage, faqAnswer, "KNOWLEDGE_RETRIEVAL");
                return new DialogResponse(faqAnswer, SessionState.Idle, intentName: "KNOWLEDGE_RETRIEVAL", confidence: fallbackFaq[0].Similarity);
            }

            // Step 9: Fallback (Out-of-Domain or Unmatched)
            string fallback = FallbackMessage ?? "Xin lỗi, tôi chưa hiểu rõ yêu cầu. Vui lòng đặt câu hỏi liên quan đến hệ thống.";
            workingMemory.AddTurn(userMessage, fallback, "FALLBACK");
            return new DialogResponse(fallback, SessionState.Idle, null, session.Slots, false, 0.0f);
        }
    }
}
