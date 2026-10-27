using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Mcp
{
    /// <summary>
    /// Bridges an external Model Context Protocol (MCP) server tool into ZeroAgent's IAgentTool ecosystem.
    /// Enables ZeroAgent to seamlessly invoke external MCP tools alongside native and dynamic tools.
    /// </summary>
    public sealed class McpRemoteToolBridge : IAgentTool
    {
        private readonly string _name;
        private readonly string _description;
        private readonly Func<string, string, Task<string>> _remoteInvoker;

        public string Name => _name;
        public string Description => _description;
        public string ParameterSignature { get; }
        public JsonSchemaConstraint? Schema { get; }
        public bool RequiresApproval { get; }

        public McpRemoteToolBridge(
            McpToolDescriptor descriptor,
            Func<string, string, Task<string>> remoteInvoker,
            bool requiresApproval = false)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            _name = descriptor.Name;
            _description = descriptor.Description;
            _remoteInvoker = remoteInvoker ?? throw new ArgumentNullException(nameof(remoteInvoker));
            RequiresApproval = requiresApproval;

            // Extract schema parameters
            Schema = new JsonSchemaConstraint(descriptor.Name);
            var sigParts = new List<string>();

            if (descriptor.InputSchema.ValueKind == JsonValueKind.Object)
            {
                var reqSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (descriptor.InputSchema.TryGetProperty("required", out var reqElem) && reqElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in reqElem.EnumerateArray())
                    {
                        string? reqProp = item.GetString();
                        if (!string.IsNullOrEmpty(reqProp)) reqSet.Add(reqProp);
                    }
                }

                if (descriptor.InputSchema.TryGetProperty("properties", out var propsElem) && propsElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in propsElem.EnumerateObject())
                    {
                        string pType = "string";
                        if (prop.Value.TryGetProperty("type", out var typeElem))
                        {
                            pType = typeElem.GetString() ?? "string";
                        }
                        bool isReq = reqSet.Contains(prop.Name);
                        Schema.AddProperty(prop.Name, MapSchemaPropertyType(pType), isReq);
                        sigParts.Add($"{prop.Name}: {pType}");
                    }
                }
            }

            ParameterSignature = sigParts.Count > 0 ? string.Join(", ", sigParts) : "arguments: object";
        }

        public Task<string> ExecuteAsync(string argument)
        {
            return _remoteInvoker(_name, argument);
        }

        public async Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request)
        {
            var sw = Stopwatch.StartNew();
            string output = await ExecuteAsync(request.ArgumentsJson).ConfigureAwait(false);
            sw.Stop();

            if (output.StartsWith("Error:") || output.StartsWith("Tool execution failed"))
            {
                return ToolCallResponse.CreateFailure(request.CallId, Name, output, sw.Elapsed);
            }

            return ToolCallResponse.CreateSuccess(request.CallId, Name, output, sw.Elapsed);
        }

        private static SchemaPropertyType MapSchemaPropertyType(string type)
        {
            switch (type?.ToLowerInvariant())
            {
                case "int":
                case "integer": return SchemaPropertyType.Number;
                case "float":
                case "double":
                case "number": return SchemaPropertyType.Number;
                case "bool":
                case "boolean": return SchemaPropertyType.Boolean;
                case "array": return SchemaPropertyType.Array;
                case "object": return SchemaPropertyType.Object;
                default: return SchemaPropertyType.String;
            }
        }
    }
}
