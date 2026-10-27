using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.Verification;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Caching;

namespace ZeroAgent.Core.Reasoning.TreeOfThought
{
    /// <summary>
    /// Autonomous cognitive deliberator implementing the Tree-of-Thought (ToT) paradigm.
    /// Explores multiple reasoning branches, evaluates intermediate hypotheses, backtracks from unpromising paths,
    /// and prunes dead ends to solve complex Root Cause Analysis (RCA) and non-linear diagnostic missions.
    /// </summary>
    public sealed class TreeOfThoughtAgent
    {
        public string Name { get; }
        public string Role { get; }
        public AgentToolRegistry Tools { get; }
        public ILlmClient Llm { get; }
        public IToTEvaluator Evaluator { get; set; } = new HeuristicToTEvaluator();
        public int MaxTreeDepth { get; set; } = 4;
        public int BeamWidth { get; set; } = 2;
        public float PruningThreshold { get; set; } = 0.30f;
        public IStepCritic? StepVerifier { get; set; } = new DefaultStepVerifier();

        public TreeOfThoughtAgent(string name, string role, AgentToolRegistry tools, ILlmClient llm)
        {
            Name = name ?? "ToTAgent";
            Role = role ?? "Analytical Deliberator";
            Tools = tools ?? new AgentToolRegistry();
            Llm = llm ?? throw new ArgumentNullException(nameof(llm));
        }

        public async Task<AgentResponse> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var sw = Stopwatch.StartNew();
            int totalSteps = 0;
            var nodeStore = new Dictionary<string, ThoughtNode>(StringComparer.OrdinalIgnoreCase);

            // Root node
            var root = new ThoughtNode("root", null, 0, $"Begin goal: {context.Goal}") { ValueScore = 1.0f };
            nodeStore[root.Id] = root;

            var frontier = new List<ThoughtNode> { root };
            ThoughtNode? bestTerminalSolution = null;

            for (int depth = 1; depth <= MaxTreeDepth; depth++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nextCandidates = new List<ThoughtNode>();

                foreach (var parent in frontier)
                {
                    if (parent.IsTerminal)
                    {
                        if (bestTerminalSolution == null || parent.ValueScore > bestTerminalSolution.ValueScore)
                        {
                            bestTerminalSolution = parent;
                        }
                        continue;
                    }

                    int branchAttempts = 0;
                    string? lastPrunedFeedback = null;
                    const int maxAttemptsPerParent = 2;

                    while (branchAttempts < maxAttemptsPerParent)
                    {
                        branchAttempts++;
                        totalSteps++;

                        // 1. Build prompt exploring from this branch's trajectory
                        string branchTrajectory = BuildBranchTrajectory(nodeStore, parent);
                        string prompt = BuildBranchPrompt(context, branchTrajectory);

                        if (branchAttempts > 1 && !string.IsNullOrEmpty(lastPrunedFeedback))
                        {
                            prompt += $"\n[BACKTRACK DIRECTIVE] Previous hypothesis was rejected as an unpromising dead end: {lastPrunedFeedback}. Formulate an alternative hypothesis or alternate tool action.";
                        }

                        // 2. Query LLM for next thought and action
                        string completion = await Llm.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

                        string childId = $"n_{depth}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
                        var childNode = new ThoughtNode(childId, parent.Id, depth, completion);
                        parent.ChildrenIds.Add(childId);
                        nodeStore[childId] = childNode;

                        // 3. Inspect for Final Answer
                        if (ToolCallParser.TryParseFinalAnswer(completion, out string finalAnswer))
                        {
                            childNode.IsTerminal = true;
                            childNode.FinalAnswer = finalAnswer.Trim();
                            childNode.ValueScore = await Evaluator.EvaluateNodeAsync(context, childNode, cancellationToken).ConfigureAwait(false);

                            if (bestTerminalSolution == null || childNode.ValueScore > bestTerminalSolution.ValueScore)
                            {
                                bestTerminalSolution = childNode;
                            }
                            nextCandidates.Add(childNode);
                            break;
                        }

                        // 4. Inspect for Action
                        if (ToolCallParser.TryParseToolCall(completion, out var toolCall))
                        {
                            childNode.Action = toolCall;

                            // Step-Level Verification check
                            if (StepVerifier != null)
                            {
                                var verification = await StepVerifier.VerifyStepAsync(context, completion, toolCall, Tools, cancellationToken).ConfigureAwait(false);
                                if (!verification.IsApproved)
                                {
                                    childNode.Observation = $"[VERIFICATION CRITIC REJECTION] {verification.FeedbackForAgent}";
                                    childNode.ValueScore = await Evaluator.EvaluateNodeAsync(context, childNode, cancellationToken).ConfigureAwait(false);
                                    nextCandidates.Add(childNode);
                                    break;
                                }
                            }

                            // Execute Tool
                            var toolResponse = await Tools.ExecuteCallAsync(toolCall).ConfigureAwait(false);
                            childNode.Observation = toolResponse.Success
                                ? (toolResponse.Content ?? string.Empty)
                                : $"Error: {toolResponse.ErrorMessage ?? toolResponse.Content}";

                            // Evaluate cognitive value of this branch
                            childNode.ValueScore = await Evaluator.EvaluateNodeAsync(context, childNode, cancellationToken).ConfigureAwait(false);

                            // Pruning check (Backtracking from unpromising branches)
                            if (childNode.ValueScore >= PruningThreshold)
                            {
                                nextCandidates.Add(childNode);
                                break; // Viable branch found
                            }
                            else
                            {
                                // Dead end: save feedback and backtrack to try next attempt on this parent
                                lastPrunedFeedback = childNode.Observation;
                            }
                        }
                        else
                        {
                            // Direct completion treated as terminal hypothesis
                            childNode.IsTerminal = true;
                            childNode.FinalAnswer = completion.Trim();
                            childNode.ValueScore = await Evaluator.EvaluateNodeAsync(context, childNode, cancellationToken).ConfigureAwait(false);
                            if (bestTerminalSolution == null || childNode.ValueScore > bestTerminalSolution.ValueScore)
                            {
                                bestTerminalSolution = childNode;
                            }
                            break;
                        }
                    }
                }

                // If a high-confidence solution is reached, conclude early
                if (bestTerminalSolution != null && bestTerminalSolution.ValueScore >= 0.80f)
                {
                    break;
                }

                // Beam selection: retain top K promising branches
                frontier = nextCandidates
                    .Where(n => !n.IsTerminal)
                    .OrderByDescending(n => n.ValueScore)
                    .Take(Math.Max(1, BeamWidth))
                    .ToList();

                if (frontier.Count == 0 && bestTerminalSolution != null)
                {
                    break; // No more active exploration branches, but solution exists
                }
            }

            sw.Stop();

            if (bestTerminalSolution != null && !string.IsNullOrWhiteSpace(bestTerminalSolution.FinalAnswer))
            {
                var history = ReconstructHistory(nodeStore, bestTerminalSolution);
                return AgentResponse.Succeeded(bestTerminalSolution.FinalAnswer!, totalSteps, sw.Elapsed, history);
            }

            return AgentResponse.Failed("Tree-of-Thought search exhausted all branches without identifying an acceptable solution.", totalSteps, sw.Elapsed, context.History);
        }

        private string BuildBranchTrajectory(Dictionary<string, ThoughtNode> nodeStore, ThoughtNode current)
        {
            var path = new List<ThoughtNode>();
            ThoughtNode? curr = current;
            while (curr != null && curr.Id != "root")
            {
                path.Add(curr);
                curr = curr.ParentId != null && nodeStore.TryGetValue(curr.ParentId, out var p) ? p : null;
            }
            path.Reverse();

            var sb = new StringBuilder();
            foreach (var node in path)
            {
                sb.AppendLine(node.Thought);
                if (!string.IsNullOrEmpty(node.Observation))
                {
                    sb.AppendLine($"Observation: {node.Observation}");
                }
            }
            return sb.ToString();
        }

        private string BuildBranchPrompt(AgentContext context, string trajectory)
        {
            var optimizer = new PromptLayoutOptimizer();

            string systemInstruction = $"You are {Name}, an analytical Tree-of-Thought agent ({Role}).\n" +
                "Evaluate the current hypothesis branch and formulate the next logical thought and action.\n" +
                "Use the format:\n" +
                "Goal: user prompt\n" +
                "Thought: your hypothesis\n" +
                "Action: tool to call (or none if ready)\n" +
                "Observation: result\n" +
                "... (or 'Final Answer: <solution>' if confident)";

            optimizer.AddSystem(systemInstruction, "tot_system_instruction");
            optimizer.AddTools(Tools.GetToolsPrompt(), "tot_tool_definitions");
            optimizer.AddUserQuery($"Goal: {context.Goal}", "tot_goal");

            if (!string.IsNullOrWhiteSpace(trajectory))
            {
                optimizer.AddHistory(trajectory, "tot_branch_trajectory");
            }

            var layout = optimizer.Optimize();
            return layout.FullPrompt + "\nThought: ";
        }

        private List<AgentMessage> ReconstructHistory(Dictionary<string, ThoughtNode> nodeStore, ThoughtNode terminalNode)
        {
            var path = new List<ThoughtNode>();
            ThoughtNode? curr = terminalNode;
            while (curr != null && curr.Id != "root")
            {
                path.Add(curr);
                curr = curr.ParentId != null && nodeStore.TryGetValue(curr.ParentId, out var p) ? p : null;
            }
            path.Reverse();

            var history = new List<AgentMessage>();
            foreach (var node in path)
            {
                history.Add(new AgentMessage(AgentRole.Assistant, node.Thought));
                if (!string.IsNullOrEmpty(node.Observation))
                {
                    history.Add(new AgentMessage(AgentRole.Tool, node.Observation!, node.Action?.ToolName));
                }
            }
            if (!string.IsNullOrEmpty(terminalNode.FinalAnswer))
            {
                history.Add(new AgentMessage(AgentRole.Assistant, $"Final Answer: {terminalNode.FinalAnswer}"));
            }
            return history;
        }
    }
}
