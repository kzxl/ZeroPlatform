using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;

namespace ZeroAgent.Core.Reasoning.TreeOfThought
{
    /// <summary>
    /// Value function and state evaluator interface for Tree-of-Thought (ToT) exploration.
    /// Scores candidate reasoning nodes between 0.0 (dead end / catastrophic) and 1.0 (goal solved).
    /// </summary>
    public interface IToTEvaluator
    {
        Task<float> EvaluateNodeAsync(
            AgentContext context,
            ThoughtNode node,
            CancellationToken cancellationToken = default);
    }
}
