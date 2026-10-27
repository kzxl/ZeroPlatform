using System.Threading.Tasks;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Contract for tools executable by AI agents, supporting both string arguments and structured tool calls.
    /// </summary>
    public interface IAgentTool
    {
        /// <summary>
        /// Unique name of the tool, matching function call names.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human- and LLM-readable description of what the tool accomplishes.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Human-readable signature of arguments (e.g. "unitId: int, address: int").
        /// </summary>
        string ParameterSignature { get; }

        /// <summary>
        /// Optional JSON Schema constraint to enforce valid argument generation via ZeroPrompt.
        /// </summary>
        JsonSchemaConstraint? Schema { get; }

        /// <summary>
        /// Indicates if this tool modifies physical/critical state and requires operator authorization.
        /// </summary>
        bool RequiresApproval { get; }

        /// <summary>
        /// Executes the tool with a raw argument string (or JSON payload).
        /// </summary>
        Task<string> ExecuteAsync(string argument);

        /// <summary>
        /// Executes a structured tool call request with high-resolution telemetry.
        /// </summary>
        Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request);
    }
}
