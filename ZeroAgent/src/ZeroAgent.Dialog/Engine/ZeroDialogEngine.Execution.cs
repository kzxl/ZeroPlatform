using System;
using System.Threading.Tasks;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Dialog.Engine
{
    public sealed partial class ZeroDialogEngine
    {
        /// <summary>
        /// Executes the ready intent, validates RBAC permissions, caches idempotent outputs,
        /// and formats the final personalized template response.
        /// </summary>
        private async Task<DialogResponse> ExecuteIntentAsync(
            DialogueSession session,
            WorkingMemory workingMemory,
            UserProfile userProfile,
            string userMessage,
            string resolvedMessage,
            float[] queryEmbedding,
            float score)
        {
            var intent = session.CurrentIntent!;

            // RBAC Permission Gate check
            if (!string.IsNullOrEmpty(intent.RequiredPermission) && !userProfile.CanExecute(intent.RequiredPermission))
            {
                session.State = SessionState.ActionBlockedByPermission;
                string deniedMsg = userProfile.IsGuest
                    ? Generator.FormatGuestLoginRequired(intent.RequiredPermission, intent.Name)
                    : Generator.FormatPermissionDenied(intent.RequiredPermission);
                workingMemory.AddTurn(userMessage, deniedMsg, intent.Name);
                return new DialogResponse(deniedMsg, SessionState.ActionBlockedByPermission, intent.Name, session.Slots, false, score);
            }

            // Execute action
            string actionOutput;
            if (intent.ActionHandler != null)
            {
                actionOutput = await intent.ActionHandler(session).ConfigureAwait(false);
            }
            else
            {
                actionOutput = $"Tác vụ '{intent.Name}' đã được xác nhận thực thi.";
            }

            // Update Working Memory active entities
            foreach (var kvp in session.Slots)
            {
                workingMemory.SetSlot(kvp.Key, kvp.Value);
            }

            // Record successful action into slots for template rendering
            session.SetSlot("output", actionOutput);

            string personaDescription = userProfile.Persona.Tone switch
            {
                CommunicationTone.Formal => "trang trọng, chính xác, lịch sự",
                CommunicationTone.Respectful => "kính trọng, chu đáo, lễ phép",
                CommunicationTone.Friendly => "thân thiện, cởi mở, hỗ trợ nhiệt tình",
                CommunicationTone.Casual => "gần gũi, tự nhiên, dứt khoát",
                CommunicationTone.Direct => "ngắn gọn, trực tiếp, tập trung vào số liệu",
                _ => "kỹ sư chuyên nghiệp, lịch sự, chính xác"
            };

            var nlgContext = new ZeroAgent.Dialog.Generator.NlgContext(
                userMessage,
                intent.Name,
                session.Slots,
                intent.ResponseTemplates,
                actionOutput)
            {
                PersonaStyle = personaDescription,
                Domain = intent.Domain ?? session.ActiveDomain
            };

            string finalResponse = await NlgSynthesizer.SynthesizeAsync(nlgContext).ConfigureAwait(false);
            workingMemory.AddTurn(userMessage, finalResponse, intent.Name);

            // Populate semantic response cache safely:
            // 1. Never cache state-mutating actions (SET, WRITE, STOP, EMERGENCY)
            // 2. Volatile real-time telemetry (TEMPERATURE, SENSOR, TSDB) gets ultra-short TTL (5s) to avoid stale safety risks
            // 3. Static informational intents get standard TTL (10m)
            if (intent.Name != null
                && !intent.Name.StartsWith("SET_", StringComparison.OrdinalIgnoreCase)
                && !intent.Name.StartsWith("WRITE_", StringComparison.OrdinalIgnoreCase)
                && !intent.Name.StartsWith("STOP_", StringComparison.OrdinalIgnoreCase)
                && !intent.Name.Contains("EMERGENCY"))
            {
                TimeSpan ttl = IsVolatileIntent(intent.Name) ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(10);
                Memory.ResponseCache.Store(queryEmbedding, resolvedMessage, finalResponse, intent.Name, ttl);
            }

            userProfile.Persona.RecordInteraction(intent.Name, session.Slots);
            session.State = SessionState.Completed;
            return new DialogResponse(finalResponse, SessionState.Completed, intent.Name, session.Slots, true, score);
        }

        private static bool IsVolatileIntent(string? intentName)
        {
            if (string.IsNullOrEmpty(intentName)) return false;
            return intentName!.IndexOf("TEMPERATURE", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("PRESSURE", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("TELEMETRY", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("SENSOR", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("TSDB", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("LIVE", StringComparison.OrdinalIgnoreCase) >= 0
                || intentName.IndexOf("METRIC", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
