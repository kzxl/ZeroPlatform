using System;
using System.Threading.Tasks;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Dialog.Engine
{
    public sealed partial class ZeroDialogEngine
    {
        /// <summary>
        /// Evaluates analytical inquiry triggers and escalates to Tier-2 Generative Deliberation (ReAct Agent)
        /// when deep multi-step deduction, root-cause analysis, or cross-tool synthesis is required.
        /// </summary>
        private async Task<DialogResponse?> TryEscalateAnalyticalQueryAsync(
            DialogueSession session,
            WorkingMemory workingMemory,
            UserProfile userProfile,
            string resolvedMessage,
            string originalUserMessage)
        {
            if (CognitiveEscalationHandler == null) return null;

            bool isAnalytical =
                resolvedMessage.IndexOf("phân tích", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("tại sao", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("nguyên nhân", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("đối chiếu", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("tổng hợp", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("analyze", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("explain", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isAnalytical) return null;

            var escalated = await CognitiveEscalationHandler(session, workingMemory, userProfile, resolvedMessage).ConfigureAwait(false);
            if (escalated != null)
            {
                workingMemory.AddTurn(originalUserMessage, escalated.Text, escalated.IntentName ?? "COGNITIVE_DELIBERATION_REACT");
                return escalated;
            }

            return null;
        }

        /// <summary>
        /// Dynamic Arbitration Matrix: Resolves potential overlaps and conflicts between Agent Memory (Semantic & Episodic)
        /// and Transactional Intents.
        /// 
        /// Collision Analysis:
        /// 1. Lexical Substring Collision: Query "Quy trình quá nhiệt Lò nung F-01" or "Sự cố quá nhiệt CNC-01" contains "nhiệt",
        ///    which can falsely trigger CHECK_TEMPERATURE instead of SOP manual or Historical Incident memory.
        ///    Arbitration: Explicit inquiry modifiers ("quy trình", "hướng dẫn", "sự cố", "lịch sử") route to Knowledge/Episodic
        ///    retrieval unless a dedicated document intent is explicitly registered.
        /// 2. Conversational Interruption: User in middle of slot collection asks an unrelated SOP question.
        ///    Arbitration: Knowledge query responds immediately without corrupting pending slot state.
        /// 3. Ephemeral / Guest Safety: Confidential internal actions prompt for login when user is Guest.
        /// </summary>
        private DialogResponse? TryArbitrateKnowledgeOrHistory(
            DialogueSession session,
            WorkingMemory workingMemory,
            UserProfile userProfile,
            string resolvedMessage,
            string originalUserMessage,
            ReadOnlySpan<float> queryEmbedding,
            DialogueIntent? detectedIntent,
            float intentScore)
        {
            // Check Semantic Memory (Knowledge / SOP Manuals / FAQ)
            bool isDocQuery =
                resolvedMessage.IndexOf("quy trình", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("hướng dẫn", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("sop", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("tài liệu", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isDocQuery)
            {
                // Only allow intent to override doc query if intent specifically targets document workflows
                bool isSpecializedDocIntent = detectedIntent != null &&
                    (detectedIntent.Name.IndexOf("SOP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     detectedIntent.Name.IndexOf("DOC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     detectedIntent.Name.IndexOf("MANUAL", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isSpecializedDocIntent)
                {
                    var faqMatches = Memory.Semantic.Query(queryEmbedding, topK: 1, minScore: 0.20f);
                    if (faqMatches.Count > 0)
                    {
                        var doc = faqMatches[0].Item;
                        string faqAnswer = $"📖 [Tài liệu {doc.Category} - {doc.Title}]:\n{doc.Content}";
                        workingMemory.AddTurn(originalUserMessage, faqAnswer, "KNOWLEDGE_RETRIEVAL");
                        return new DialogResponse(faqAnswer, SessionState.Idle, intentName: "KNOWLEDGE_RETRIEVAL", confidence: faqMatches[0].Similarity);
                    }
                }
            }

            // Check Episodic Memory (Historical Incidents / Case Memories)
            bool isHistoryQuery =
                resolvedMessage.IndexOf("trước", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("lần trước", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("lịch sử", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedMessage.IndexOf("sự cố", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isHistoryQuery)
            {
                bool isSpecializedHistoryIntent = detectedIntent != null &&
                    (detectedIntent.Name.IndexOf("INCIDENT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     detectedIntent.Name.IndexOf("HISTORY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     detectedIntent.Name.IndexOf("LOG", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isSpecializedHistoryIntent)
                {
                    var pastIncidents = Memory.Episodic.Recall(queryEmbedding, topK: 1, minScore: 0.20f);
                    if (pastIncidents.Count > 0)
                    {
                        var ep = pastIncidents[0].Episode;
                        string epAnswer = $"📜 [Ghi nhận sự cố trước đây]:\n- Vấn đề: {ep.Issue}\n- Xử lý: {ep.Resolution}\n- Kết quả: {(ep.Success ? "Thành công" : "Chưa hoàn tất")}";
                        workingMemory.AddTurn(originalUserMessage, epAnswer, "HISTORICAL_EPISODE");
                        return new DialogResponse(epAnswer, SessionState.Idle, intentName: "HISTORICAL_EPISODE", confidence: pastIncidents[0].Similarity);
                    }
                }
            }

            return null;
        }
    }
}
