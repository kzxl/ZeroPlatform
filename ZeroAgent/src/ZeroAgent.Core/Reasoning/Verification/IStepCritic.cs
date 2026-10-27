using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Core.Reasoning.Verification
{
    /// <summary>
    /// Step-level Critic and Verifier interface.
    /// Intercepts proposed agent actions before physical execution to detect hallucinated tools,
    /// malformed arguments, logic contradictions, or safety policy violations.
    /// </summary>
    public interface IStepCritic
    {
        Task<StepVerificationResult> VerifyStepAsync(
            AgentContext context,
            string thought,
            ToolCallRequest toolCall,
            AgentToolRegistry tools,
            CancellationToken cancellationToken = default);
    }
}
