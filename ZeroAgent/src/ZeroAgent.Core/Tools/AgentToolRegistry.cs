using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Registry for tools executable by AI agents.
    /// Provides strongly-typed registration, automatic schema description formatting,
    /// and Human-in-the-Loop (HITL) safety policy enforcement.
    /// </summary>
    public sealed class AgentToolRegistry
    {
        private readonly Dictionary<string, IAgentTool> _tools = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Global or context-specific Human-in-the-Loop approval callback.
        /// Invoked when a tool with <see cref="IAgentTool.RequiresApproval"/> is executed.
        /// Return true to allow execution, or false to reject.
        /// </summary>
        public Func<IAgentTool, string, Task<bool>>? ApprovalHandler { get; set; }

        public int Count => _tools.Count;
        public IEnumerable<IAgentTool> Tools => _tools.Values;
        public IEnumerable<string> GetToolNames() => _tools.Keys;
        public bool Contains(string name) => !string.IsNullOrEmpty(name) && _tools.ContainsKey(name);

        public void Register(IAgentTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
        }

        public void Register(string name, string description, Func<string, string> func)
        {
            Register(new AgentTool(name, description, "string argument", arg => Task.FromResult(func(arg))));
        }

        public void Register(string name, string description, Func<string, Task<string>> asyncFunc)
        {
            Register(new AgentTool(name, description, "string argument", asyncFunc));
        }

        public void Register(string name, string description, Func<int, string> func)
        {
            Register(new AgentTool(name, description, "int id", arg =>
            {
                int.TryParse(arg?.Trim(), out int val);
                return Task.FromResult(func(val));
            }));
        }

        public bool TryGetTool(string name, out IAgentTool tool)
        {
            return _tools.TryGetValue(name, out tool!);
        }

        public bool TryGetTool<T>(string name, out T tool) where T : class, IAgentTool
        {
            if (_tools.TryGetValue(name, out var t) && t is T typed)
            {
                tool = typed;
                return true;
            }
            tool = null!;
            return false;
        }

        public AgentTool? Get(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? (tool as AgentTool) : null;
        }

        public IAgentTool? Find(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }

        public async Task<string> ExecuteAsync(string name, string argument)
        {
            if (!_tools.TryGetValue(name, out var tool))
            {
                return $"Error: Tool '{name}' is not found in the registry.";
            }

            // HITL Safety Gate Check
            if (tool.RequiresApproval)
            {
                if (ApprovalHandler == null)
                {
                    return $"Safety Policy Violation: Tool '{name}' requires Human-in-the-Loop (HITL) approval, but no ApprovalHandler is configured.";
                }

                bool approved = await ApprovalHandler(tool, argument).ConfigureAwait(false);
                if (!approved)
                {
                    return $"Action rejected: Execution of tool '{name}' was denied by operator safety gate.";
                }
            }

            return await tool.ExecuteAsync(argument).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a structured tool call request with high-resolution duration measurement and error tracking.
        /// </summary>
        public async Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!_tools.TryGetValue(request.ToolName, out var tool))
            {
                sw.Stop();
                return ToolCallResponse.CreateFailure(request.CallId, request.ToolName, $"Tool '{request.ToolName}' is not registered.", sw.Elapsed);
            }

            if (tool.RequiresApproval)
            {
                if (ApprovalHandler == null)
                {
                    sw.Stop();
                    return ToolCallResponse.CreateFailure(request.CallId, request.ToolName, $"Tool '{request.ToolName}' requires HITL approval, but no ApprovalHandler is configured.", sw.Elapsed);
                }

                bool approved = await ApprovalHandler(tool, request.ArgumentsJson).ConfigureAwait(false);
                if (!approved)
                {
                    sw.Stop();
                    return ToolCallResponse.CreateFailure(request.CallId, request.ToolName, $"Execution denied by operator safety gate.", sw.Elapsed);
                }
            }

            return await tool.ExecuteCallAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes multiple tool calls sequentially or in parallel, preserving call IDs.
        /// </summary>
        public async Task<ToolCallResponse[]> ExecuteBatchAsync(IEnumerable<ToolCallRequest> requests)
        {
            if (requests == null) return Array.Empty<ToolCallResponse>();

            var list = new List<ToolCallRequest>(requests);
            var results = new ToolCallResponse[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                results[i] = await ExecuteCallAsync(list[i]).ConfigureAwait(false);
            }
            return results;
        }

        /// <summary>
        /// Formats a single tool definition for prompt injection.
        /// </summary>
        public static string FormatToolPrompt(IAgentTool tool)
        {
            if (tool == null) return string.Empty;
            string sensitiveFlag = tool.RequiresApproval ? " [REQUIRES OPERATOR APPROVAL]" : string.Empty;
            return $"- `{tool.Name}`({tool.ParameterSignature}): {tool.Description}{sensitiveFlag}";
        }

        /// <summary>
        /// Formats a collection of tool definitions for prompt injection.
        /// </summary>
        public static string FormatToolsPrompt(IEnumerable<IAgentTool> tools)
        {
            if (tools == null) return "No external tools available.";
            var sb = new StringBuilder();
            sb.AppendLine("You have access to the following tools:");
            int count = 0;
            foreach (var tool in tools)
            {
                sb.AppendLine(FormatToolPrompt(tool));
                count++;
            }
            return count == 0 ? "No external tools available." : sb.ToString();
        }

        /// <summary>
        /// Generates a markdown description of available tools for injection into the agent's system prompt.
        /// </summary>
        public string GetToolsPrompt() => FormatToolsPrompt(_tools.Values);

        /// <summary>
        /// Generates an OpenAI-compatible JSON Schema definition for all registered tools.
        /// </summary>
        public string GetToolsJsonSchema()
        {
            if (_tools.Count == 0) return "[]";

            var sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            foreach (var tool in _tools.Values)
            {
                if (!first) sb.Append(",");
                first = false;

                sb.Append("{\"name\":\"").Append(tool.Name).Append("\",");
                sb.Append("\"description\":\"").Append(tool.Description.Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"parameters\":{");
                sb.Append("\"type\":\"object\",");

                if (tool.Schema != null && tool.Schema.Properties.Count > 0)
                {
                    sb.Append("\"properties\":{");
                    bool firstProp = true;
                    foreach (var prop in tool.Schema.Properties)
                    {
                        if (!firstProp) sb.Append(",");
                        firstProp = false;
                        sb.Append("\"").Append(prop.Key).Append("\":{\"type\":\"")
                          .Append(prop.Value.ToString().ToLowerInvariant()).Append("\"}");
                    }
                    sb.Append("},");

                    sb.Append("\"required\":[");
                    bool firstReq = true;
                    foreach (var req in tool.Schema.RequiredProperties)
                    {
                        if (!firstReq) sb.Append(",");
                        firstReq = false;
                        sb.Append("\"").Append(req).Append("\"");
                    }
                    sb.Append("]");
                }
                else
                {
                    sb.Append("\"properties\":{},\"required\":[]");
                }

                sb.Append("}}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Creates a ToolCallGrammarLogitProcessor enforcing schema-constrained decoding for all registered tools.
        /// </summary>
        public ToolCallGrammarLogitProcessor CreateGrammarProcessor(ZeroTokenizer.Core.Abstractions.ITokenizer tokenizer)
        {
            var schemas = new Dictionary<string, JsonSchemaConstraint>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in _tools.Values)
            {
                schemas[tool.Name] = tool.Schema ?? new JsonSchemaConstraint(tool.Name);
            }
            return new ToolCallGrammarLogitProcessor(tokenizer, schemas);
        }
    }
}
