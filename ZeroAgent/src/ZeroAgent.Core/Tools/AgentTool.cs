using System;
using System.Threading.Tasks;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Represents an invokable tool binding available to the AI agent.
    /// Supports parameter schema constraints (ZeroPrompt PDA) and Human-in-the-Loop (HITL) safety policies.
    /// </summary>
    public sealed class AgentTool : IAgentTool
    {
        public string Name { get; }
        public string Description { get; }
        public string ParameterSignature { get; }
        public Func<string, Task<string>> Invoker { get; }
        public JsonSchemaConstraint? Schema { get; set; }
        public bool RequiresApproval { get; set; }

        public AgentTool(
            string name, 
            string description, 
            string parameterSignature, 
            Func<string, Task<string>> invoker,
            JsonSchemaConstraint? schema = null,
            bool requiresApproval = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            ParameterSignature = parameterSignature ?? string.Empty;
            Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            Schema = schema;
            RequiresApproval = requiresApproval;
        }

        /// <summary>
        /// Attaches a JSON Schema constraint to enforce valid argument generation via ZeroPrompt.
        /// </summary>
        public AgentTool WithSchema(JsonSchemaConstraint schema)
        {
            Schema = schema;
            return this;
        }

        /// <summary>
        /// Sets whether this tool requires human-in-the-loop (HITL) authorization before invocation.
        /// </summary>
        public AgentTool WithApproval(bool requiresApproval = true)
        {
            RequiresApproval = requiresApproval;
            return this;
        }

        public async Task<string> ExecuteAsync(string argument)
        {
            try
            {
                return await Invoker(argument).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return $"Tool execution failed with error: {ex.Message}";
            }
        }

        public async Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string result = await Invoker(request.ArgumentsJson).ConfigureAwait(false);
                sw.Stop();
                return ToolCallResponse.CreateSuccess(request.CallId, Name, result, sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return ToolCallResponse.CreateFailure(request.CallId, Name, ex.Message, sw.Elapsed);
            }
        }
    }
}
