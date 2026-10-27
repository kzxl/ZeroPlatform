using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Embedding;
using ZeroAgent.Core.Memory;
using ZeroAgent.Core.Reasoning;
using ZeroAgent.Core.Reasoning.Verification;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Caching;

namespace ZeroAgent.Core.Engine
{
    /// <summary>
    /// Autonomous cognitive agent implementing the Reasoning-Action (ReAct) paradigm.
    /// Interleaves thinking, external tool execution, observation analysis, and answer formulation.
    /// </summary>
    public sealed class ReActAgent
    {
        public string Name { get; }
        public string Role { get; }
        public AgentToolRegistry Tools { get; }
        public ILlmClient Llm { get; }
        public ContextBudgetManager BudgetManager { get; set; } = new ContextBudgetManager();
        public int MaxContextTokens { get; set; } = 4096;
        public Func<ReActLoopGuard> CreateLoopGuard { get; set; } = () => new ReActLoopGuard();
        public ToolSemanticRouter? ToolRouter { get; set; }
        public int MaxPromptTools { get; set; } = 0;
        public AgentEpisodicMemory? EpisodicMemory { get; set; }
        public ITextEmbedder? MemoryEmbedder { get; set; }
        public bool AutoRecordEpisodes { get; set; } = true;
        public float EpisodicRecallMinSimilarity { get; set; } = 0.50f;
        public IStepCritic? StepVerifier { get; set; } = new DefaultStepVerifier();
        public ReasoningStyle ReasoningStyle { get; set; } = ReasoningStyle.DetailedCoT;

        public ReActAgent WithReasoningStyle(ReasoningStyle style)
        {
            ReasoningStyle = style;
            return this;
        }

        public ReActAgent(string name, string role, AgentToolRegistry tools, ILlmClient llm)
        {
            Name = name ?? "ReActAgent";
            Role = role ?? "General Autonomous Assistant";
            Tools = tools ?? new AgentToolRegistry();
            Llm = llm ?? throw new ArgumentNullException(nameof(llm));
        }

        public Task<AgentResponse> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(context, onEvent: null, cancellationToken);
        }

        public async Task<AgentResponse> ExecuteAsync(
            AgentContext context, 
            Action<AgentStreamEvent>? onEvent, 
            CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var sw = Stopwatch.StartNew();
            int currentStep = 0;
            var loopGuard = CreateLoopGuard();

            onEvent?.Invoke(AgentStreamEvent.Started(context.Goal, sw.Elapsed));

            var conversation = new StringBuilder();

            while (currentStep < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentStep++;

                // 1. Budget enforcement on context history
                if (BudgetManager != null && MaxContextTokens > 0)
                {
                    BudgetManager.PruneContextToBudget(context, MaxContextTokens);
                }

                // 2. Build prompt
                string prompt = BuildPrompt(context, conversation.ToString());

                // 3. Query LLM
                string completion = await Llm.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
                context.AddMessage(AgentRole.Assistant, completion);
                onEvent?.Invoke(AgentStreamEvent.Thought(completion, currentStep, sw.Elapsed));

                // 4. Check for Final Answer
                if (ToolCallParser.TryParseFinalAnswer(completion, out string answer))
                {
                    sw.Stop();
                    if (AutoRecordEpisodes && EpisodicMemory != null && MemoryEmbedder != null && !string.IsNullOrWhiteSpace(context.Goal))
                    {
                        EpisodicMemory.Remember($"Goal: {context.Goal} => Solution: {answer.Trim()}", MemoryEmbedder);
                    }
                    onEvent?.Invoke(AgentStreamEvent.Completed(answer.Trim(), currentStep, sw.Elapsed));
                    return AgentResponse.Succeeded(answer, currentStep, sw.Elapsed, context.History);
                }

                // 5. Check for Action (supports ReAct syntax, JSON objects, and markdown codeblocks)
                if (ToolCallParser.TryParseToolCall(completion, out var toolCall))
                {
                    onEvent?.Invoke(AgentStreamEvent.ToolCall(toolCall.ToolName, toolCall.ArgumentsJson, currentStep, sw.Elapsed));

                    if (StepVerifier != null)
                    {
                        var verification = await StepVerifier.VerifyStepAsync(context, completion, toolCall, Tools, cancellationToken).ConfigureAwait(false);
                        if (!verification.IsApproved)
                        {
                            string observation = $"[VERIFICATION CRITIC REJECTION] {verification.FeedbackForAgent}";
                            context.AddMessage(AgentRole.Tool, observation, toolCall.ToolName);
                            conversation.AppendLine(completion);
                            conversation.AppendLine($"Observation: {observation}");
                            onEvent?.Invoke(AgentStreamEvent.Rejected(verification.FeedbackForAgent, currentStep, sw.Elapsed));
                            continue;
                        }
                        else
                        {
                            onEvent?.Invoke(AgentStreamEvent.Verified("Step approved by critic", currentStep, sw.Elapsed));
                        }
                    }

                    var guardStatus = loopGuard.EvaluateCall(toolCall.ToolName, toolCall.ArgumentsJson, out string guardFeedback);

                    if (guardStatus == LoopGuardStatus.CircuitBreak)
                    {
                        // Circuit breaker triggered: block duplicate execution and inject instruction
                        string observation = guardFeedback;
                        context.AddMessage(AgentRole.Tool, observation, toolCall.ToolName);
                        conversation.AppendLine(completion);
                        conversation.AppendLine($"Observation: {observation}");
                        onEvent?.Invoke(AgentStreamEvent.CircuitBreak(guardFeedback, currentStep, sw.Elapsed));
                    }
                    else
                    {
                        var response = await Tools.ExecuteCallAsync(toolCall).ConfigureAwait(false);
                        string rawObservation;
                        if (response.Success && (response.Content == null || !response.Content.StartsWith("Tool execution failed with error:")))
                        {
                            rawObservation = response.Content ?? string.Empty;
                            if (guardStatus == LoopGuardStatus.WarnAndReflect && !string.IsNullOrEmpty(guardFeedback))
                            {
                                rawObservation += $"\n{guardFeedback}";
                            }
                        }
                        else
                        {
                            string err = (!response.Success && !string.IsNullOrEmpty(response.ErrorMessage))
                                ? response.ErrorMessage
                                : response.Content;
                            rawObservation = $"Error: {err}\n[SELF-CORRECTION DIRECTIVE] The tool execution encountered an error. Reflect on why the error occurred, inspect the argument format, or select an alternate tool.";
                        }

                        string observation = ObservationCompactor.Compact(toolCall.ToolName, rawObservation);
                        context.AddMessage(AgentRole.Tool, observation, toolCall.ToolName);

                        conversation.AppendLine(completion);
                        conversation.AppendLine($"Observation: {observation}");
                        onEvent?.Invoke(AgentStreamEvent.ToolDone(toolCall.ToolName, observation, currentStep, sw.Elapsed));
                    }
                }
                else
                {
                    // No action or final answer detected: Treat completion as answer
                    sw.Stop();
                    if (AutoRecordEpisodes && EpisodicMemory != null && MemoryEmbedder != null && !string.IsNullOrWhiteSpace(context.Goal))
                    {
                        EpisodicMemory.Remember($"Goal: {context.Goal} => Solution: {completion.Trim()}", MemoryEmbedder);
                    }
                    onEvent?.Invoke(AgentStreamEvent.Completed(completion.Trim(), currentStep, sw.Elapsed));
                    return AgentResponse.Succeeded(completion.Trim(), currentStep, sw.Elapsed, context.History);
                }
            }

            sw.Stop();
            string maxStepErr = $"Agent exceeded maximum step limit ({context.MaxSteps}) without reaching a final answer.";
            onEvent?.Invoke(AgentStreamEvent.Failed(maxStepErr, currentStep, sw.Elapsed));
            return AgentResponse.Failed(maxStepErr, currentStep, sw.Elapsed, context.History);
        }

        private string BuildPrompt(AgentContext context, string trajectory)
        {
            var optimizer = new PromptLayoutOptimizer();

            // 1. Static System Instruction
            string systemInstruction;
            if (ReasoningStyle == ReasoningStyle.ChainOfDraft)
            {
                systemInstruction = $"You are {Name}, an autonomous cognitive agent acting as a {Role}.\n" +
                    "[REASONING STYLE: CHAIN-OF-DRAFT]\n" +
                    "Keep thoughts ultra-concise, telegraphic, and under 30 words. Focus strictly on the immediate next action without verbose filler.\n" +
                    "Use the following format:\n" +
                    "Goal: the user prompt to accomplish\n" +
                    "Thought: concise telegraphic draft (< 30 words)\n" +
                    "Action: the action to take, should be one of the tools: ToolName(argument)\n" +
                    "Observation: the result of the action\n" +
                    "... (this Thought/Action/Observation can repeat)\n" +
                    "Thought: concise final justification\n" +
                    "Final Answer: concise, authoritative final answer";
            }
            else if (ReasoningStyle == ReasoningStyle.SilentAction)
            {
                systemInstruction = $"You are {Name}, an autonomous cognitive agent acting as a {Role}.\n" +
                    "[REASONING STYLE: DIRECT ACTION]\n" +
                    "Proceed directly to action formulation without conversational delay.\n" +
                    "Use the following format:\n" +
                    "Goal: the user prompt to accomplish\n" +
                    "Action: the action to take, should be one of the tools: ToolName(argument)\n" +
                    "Observation: the result of the action\n" +
                    "... (repeat as necessary)\n" +
                    "Final Answer: direct final answer";
            }
            else
            {
                systemInstruction = $"You are {Name}, an autonomous cognitive agent acting as a {Role}.\n" +
                    "Use the following format:\n" +
                    "Goal: the user prompt to accomplish\n" +
                    "Thought: you should always think about what to do\n" +
                    "Action: the action to take, should be one of the tools: ToolName(argument)\n" +
                    "Observation: the result of the action\n" +
                    "... (this Thought/Action/Observation can repeat)\n" +
                    "Thought: I now have the final answer\n" +
                    "Final Answer: the final answer to the original input question";
            }

            optimizer.AddSystem(systemInstruction, "react_system_instruction");

            // 1.1 Injected System Directives & Context Summaries
            if (context != null)
            {
                for (int i = 0; i < context.History.Count; i++)
                {
                    var msg = context.History[i];
                    if (msg.Role == AgentRole.System && !string.IsNullOrWhiteSpace(msg.Content))
                    {
                        optimizer.AddSystem(msg.Content, $"system_context_{i}");
                    }
                }
            }

            // 2. Tool Definitions (optionally routed to prune context)
            string toolsPrompt;
            if (ToolRouter != null && MaxPromptTools > 0 && Tools.Count > MaxPromptTools)
            {
                var relevantTools = ToolRouter.Route(context?.Goal ?? string.Empty, topK: MaxPromptTools);
                toolsPrompt = relevantTools.Count > 0
                    ? AgentToolRegistry.FormatToolsPrompt(relevantTools.Select(r => r.Tool))
                    : Tools.GetToolsPrompt();
            }
            else
            {
                toolsPrompt = Tools.GetToolsPrompt();
            }
            optimizer.AddTools(toolsPrompt, "react_tool_definitions");

            // 2.5 Injected Episodic Memory (Recall prior successful trajectories for similar goals)
            if (EpisodicMemory != null && MemoryEmbedder != null && !string.IsNullOrWhiteSpace(context?.Goal))
            {
                var pastExperiences = EpisodicMemory.Recall(context!.Goal, MemoryEmbedder, topK: 2);
                var relevantEpisodes = pastExperiences.FindAll(e => e.SimilarityScore >= EpisodicRecallMinSimilarity);
                if (relevantEpisodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Relevant past successful experiences for similar goals:");
                    foreach (var (memory, _) in relevantEpisodes)
                    {
                        sb.AppendLine($"- {memory}");
                    }
                    optimizer.AddFewShot(sb.ToString().TrimEnd(), "react_episodic_exemplars");
                }
            }

            // 3. Prior Dialogue History
            if (context != null)
            {
                var historySb = new StringBuilder();
                for (int i = 0; i < context.History.Count; i++)
                {
                    var msg = context.History[i];
                    if (msg.Role == AgentRole.User && msg.Content != context.Goal)
                    {
                        historySb.AppendLine($"User: {msg.Content}");
                    }
                    else if (msg.Role == AgentRole.Assistant)
                    {
                        historySb.AppendLine($"Assistant: {msg.Content}");
                    }
                }
                if (historySb.Length > 0)
                {
                    optimizer.AddHistory(historySb.ToString().TrimEnd(), "prior_dialog_history");
                }
            }

            // 4. User Goal
            optimizer.AddUserQuery($"Begin!\nGoal: {context?.Goal ?? string.Empty}", "react_goal");

            // 5. Dynamic Trajectory
            if (!string.IsNullOrWhiteSpace(trajectory))
            {
                optimizer.AddHistory(trajectory, "react_trajectory");
            }

            var layout = optimizer.Optimize();
            return ReasoningStyle == ReasoningStyle.SilentAction
                ? layout.FullPrompt + "\nAction: "
                : layout.FullPrompt + "\nThought: ";
        }
    }
}
