using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Core.Swarm.Debate
{
    /// <summary>
    /// Final consensus judgment issued by the Arbiter after an adversarial multi-agent debate.
    /// </summary>
    public sealed class DebateVerdict
    {
        public bool IsApproved { get; }
        public float ConsensusScore { get; }
        public string ProposerPlan { get; }
        public string ChallengerCritique { get; }
        public string ArbiterRationale { get; }
        public string FinalActionableDecision { get; }

        public DebateVerdict(
            bool isApproved,
            float consensusScore,
            string proposerPlan,
            string challengerCritique,
            string arbiterRationale,
            string finalActionableDecision)
        {
            IsApproved = isApproved;
            ConsensusScore = consensusScore;
            ProposerPlan = proposerPlan ?? string.Empty;
            ChallengerCritique = challengerCritique ?? string.Empty;
            ArbiterRationale = arbiterRationale ?? string.Empty;
            FinalActionableDecision = finalActionableDecision ?? string.Empty;
        }
    }

    /// <summary>
    /// Adversarial Multi-Agent Debate Engine:
    /// Mitigates hallucination, groupthink, and safety oversights in high-stakes actions
    /// through a structured multi-agent debate between a Proposer, a Challenger (Red Team), and an Arbiter.
    /// </summary>
    public sealed class AdversarialDebateEngine
    {
        private readonly ILlmClient _llm;

        public AdversarialDebateEngine(ILlmClient llm)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        }

        /// <summary>
        /// Conducts a structured adversarial debate on a proposed action plan.
        /// </summary>
        public async Task<DebateVerdict> DebateAsync(
            string goal,
            string proposedPlan,
            float minApprovalConsensus = 0.70f,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentNullException(nameof(goal));
            if (string.IsNullOrWhiteSpace(proposedPlan)) throw new ArgumentNullException(nameof(proposedPlan));

            // Stage 1: Challenger (Red Team) rigorously attacks the plan
            string challengerPrompt = 
$@"You are an Adversarial Safety & Risk Challenger (Red Team).
A proposer agent has formulated the following plan for the goal:
Goal: {goal}
Proposed Plan: {proposedPlan}

Your job is to identify critical flaws, safety violations, unhandled edge cases, and compliance risks.
State your critique clearly and concisely (max 3 points).";

            string challengerCritique = await _llm.CompleteAsync(challengerPrompt, cancellationToken).ConfigureAwait(false);

            // Stage 2: Proposer responds and hardens the plan
            string defensePrompt = 
$@"You are the Proposer Agent.
You must address the following safety critique from the Challenger:
Original Plan: {proposedPlan}
Critique: {challengerCritique}

Provide your revised, hardened plan that addresses or mitigates these risks.";

            string hardenedPlan = await _llm.CompleteAsync(defensePrompt, cancellationToken).ConfigureAwait(false);

            // Stage 3: Arbiter / Judge evaluates both sides and issues verdict
            string arbiterPrompt = 
$@"You are the Impartial Master Arbiter and Safety Judge.
Review the following debate:
Goal: {goal}
Original Plan: {proposedPlan}
Challenger Critique: {challengerCritique}
Hardened Plan: {hardenedPlan}

Evaluate the safety, robustness, and feasibility.
Format your response strictly as:
Consensus: <Score between 0.0 and 1.0>
Approved: <true or false>
Decision: <Final actionable decision>";

            string arbiterResponse = await _llm.CompleteAsync(arbiterPrompt, cancellationToken).ConfigureAwait(false);

            float consensusScore = 0.85f;
            bool isApproved = true;

            string scoreStr = ExtractValue(arbiterResponse, "Consensus:");
            if (float.TryParse(scoreStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedScore))
            {
                consensusScore = parsedScore;
            }

            string approvedStr = ExtractValue(arbiterResponse, "Approved:");
            if (bool.TryParse(approvedStr, out bool parsedApproved))
            {
                isApproved = parsedApproved && (consensusScore >= minApprovalConsensus);
            }
            else
            {
                isApproved = consensusScore >= minApprovalConsensus;
            }

            string finalDecision = ExtractValue(arbiterResponse, "Decision:");
            if (string.IsNullOrWhiteSpace(finalDecision))
            {
                finalDecision = hardenedPlan;
            }

            return new DebateVerdict(
                isApproved,
                consensusScore,
                hardenedPlan,
                challengerCritique.Trim(),
                arbiterResponse.Trim(),
                finalDecision.Trim());
        }

        private static string ExtractValue(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            int idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return string.Empty;

            int start = idx + key.Length;
            int nextNewline = text.IndexOf('\n', start);
            if (nextNewline > start)
            {
                return text.Substring(start, nextNewline - start).Trim();
            }
            return text.Substring(start).Trim();
        }
    }
}
