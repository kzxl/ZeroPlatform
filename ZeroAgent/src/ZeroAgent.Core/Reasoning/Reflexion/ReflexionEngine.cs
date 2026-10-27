using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Core.Reasoning.Reflexion
{
    /// <summary>
    /// Reflexion Engine:
    /// Facilitates verbal reinforcement learning without parameter fine-tuning.
    /// When an agent fails a task or encounters a step rejection, the Reflexion Engine
    /// analyzes the failure trace, produces an actionable self-reflection, and stores it in memory.
    /// Subsequent runs automatically pre-fetch and inject these lessons into the system prompt.
    /// </summary>
    public sealed class ReflexionEngine
    {
        private readonly IReflexionMemory _memory;

        public IReflexionMemory Memory => _memory;

        public ReflexionEngine(IReflexionMemory? memory = null)
        {
            _memory = memory ?? new InMemoryReflexionMemory();
        }

        /// <summary>
        /// Injects relevant past failure reflections into the agent's context history.
        /// </summary>
        public int InjectReflectionsIntoContext(AgentContext context, int topK = 2)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.Goal)) return 0;

            var pastReflections = _memory.Recall(context.Goal, topK);
            if (pastReflections.Count == 0) return 0;

            var sb = new StringBuilder();
            sb.AppendLine("[REFLEXION EXPERIENCE REPLAY - LESSONS LEARNED FROM PAST ATTEMPTS]");
            foreach (var ep in pastReflections)
            {
                sb.AppendLine($"- Goal: \"{ep.Goal}\" | Pitfall: {ep.FailureReason} | Correction: {ep.SelfReflection}");
            }
            sb.AppendLine("Apply these lessons to avoid repeating previous mistakes.");

            context.AddMessage(AgentRole.System, sb.ToString(), "ReflexionEngine");
            return pastReflections.Count;
        }

        /// <summary>
        /// Reflects on a failed agent response, distills a corrective lesson, and remembers it in memory.
        /// </summary>
        public async Task<ReflexionEpisode> ReflectAndRememberAsync(
            AgentContext context,
            AgentResponse failedResponse,
            ILlmClient llm,
            CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (failedResponse == null) throw new ArgumentNullException(nameof(failedResponse));
            if (llm == null) throw new ArgumentNullException(nameof(llm));

            var traceBuilder = new StringBuilder();
            for (int i = 0; i < failedResponse.ExecutionTrace.Count; i++)
            {
                var msg = failedResponse.ExecutionTrace[i];
                traceBuilder.AppendLine($"[{msg.Role}] {msg.Content}");
            }

            string prompt = 
$@"You are an Expert AI Self-Reflection Critic.
An AI agent failed to achieve the following goal:
Goal: {context.Goal}
Failure Output: {failedResponse.Output}

Execution Trace:
{traceBuilder}

Diagnose the root cause of the failure and provide:
1. Root Cause: A single sentence describing why the agent failed (e.g. wrong tool parameter, hallucinated command, premature termination).
2. Lesson: A concise, actionable instruction for the agent on how to successfully solve this goal next time.

Output format:
Root Cause: <explanation>
Lesson: <actionable instruction>";

            string reflectionText = await llm.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

            string rootCause = ExtractField(reflectionText, "Root Cause:", failedResponse.ErrorMessage ?? "Execution failed");
            string lesson = ExtractField(reflectionText, "Lesson:", reflectionText.Trim());

            var episode = new ReflexionEpisode(context.Goal, rootCause, lesson);
            _memory.Remember(episode);
            return episode;
        }

        private static string ExtractField(string text, string header, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            int idx = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return fallback;

            int start = idx + header.Length;
            int nextNewline = text.IndexOf('\n', start);
            if (nextNewline > start)
            {
                return text.Substring(start, nextNewline - start).Trim();
            }

            return text.Substring(start).Trim();
        }
    }
}
