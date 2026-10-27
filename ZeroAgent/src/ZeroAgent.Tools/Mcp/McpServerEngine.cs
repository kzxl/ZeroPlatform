using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tools.Mcp
{
    /// <summary>
    /// Model Context Protocol (MCP) Server Engine.
    /// Exposes tools registered in ZeroAgent's AgentToolRegistry over standard JSON-RPC 2.0
    /// (compatible with Claude Desktop, Cursor, Antigravity, and MCP stdio/SSE clients).
    /// </summary>
    public sealed class McpServerEngine
    {
        public const string ProtocolVersion = "2024-11-05";

        private readonly AgentToolRegistry _registry;
        private readonly string _serverName;
        private readonly string _serverVersion;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpServerEngine(
            AgentToolRegistry registry, 
            string serverName = "ZeroPlatform.McpServer", 
            string serverVersion = "1.0.0")
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _serverName = serverName;
            _serverVersion = serverVersion;
            _jsonOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Processes an incoming JSON-RPC request from an MCP client and produces a JSON-RPC response.
        /// </summary>
        public async Task<string> ProcessMessageAsync(string jsonRpcText)
        {
            if (string.IsNullOrWhiteSpace(jsonRpcText))
            {
                return SerializeResponse(new McpJsonRpcResponse
                {
                    Error = new McpJsonRpcError { Code = -32700, Message = "Parse error: empty payload" }
                });
            }

            McpJsonRpcRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<McpJsonRpcRequest>(jsonRpcText, _jsonOptions);
            }
            catch (Exception ex)
            {
                return SerializeResponse(new McpJsonRpcResponse
                {
                    Error = new McpJsonRpcError { Code = -32700, Message = $"Parse error: {ex.Message}" }
                });
            }

            if (req == null || string.IsNullOrEmpty(req.Method))
            {
                return SerializeResponse(new McpJsonRpcResponse
                {
                    Id = req?.Id,
                    Error = new McpJsonRpcError { Code = -32600, Message = "Invalid Request: missing method" }
                });
            }

            switch (req.Method)
            {
                case "initialize":
                    return HandleInitialize(req);

                case "ping":
                    return SerializeResponse(new McpJsonRpcResponse
                    {
                        Id = req.Id,
                        Result = new { }
                    });

                case "tools/list":
                    return HandleToolsList(req);

                case "tools/call":
                    return await HandleToolsCallAsync(req).ConfigureAwait(false);

                default:
                    return SerializeResponse(new McpJsonRpcResponse
                    {
                        Id = req.Id,
                        Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method not found: '{req.Method}'"
                        }
                    });
            }
        }

        private string HandleInitialize(McpJsonRpcRequest req)
        {
            var initResult = new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new
                {
                    tools = new
                    {
                        listChanged = true
                    }
                },
                serverInfo = new
                {
                    name = _serverName,
                    version = _serverVersion
                }
            };

            return SerializeResponse(new McpJsonRpcResponse
            {
                Id = req.Id,
                Result = initResult
            });
        }

        private string HandleToolsList(McpJsonRpcRequest req)
        {
            var descriptors = McpToolExporter.ExportTools(_registry);
            return SerializeResponse(new McpJsonRpcResponse
            {
                Id = req.Id,
                Result = new { tools = descriptors }
            });
        }

        private async Task<string> HandleToolsCallAsync(McpJsonRpcRequest req)
        {
            if (!req.Params.HasValue || req.Params.Value.ValueKind != JsonValueKind.Object)
            {
                return SerializeResponse(new McpJsonRpcResponse
                {
                    Id = req.Id,
                    Error = new McpJsonRpcError { Code = -32602, Message = "Invalid params: object required" }
                });
            }

            var pElem = req.Params.Value;
            if (!pElem.TryGetProperty("name", out var nameProp))
            {
                return SerializeResponse(new McpJsonRpcResponse
                {
                    Id = req.Id,
                    Error = new McpJsonRpcError { Code = -32602, Message = "Missing required parameter 'name'" }
                });
            }

            string toolName = nameProp.GetString() ?? string.Empty;
            string argsJson = "{}";
            if (pElem.TryGetProperty("arguments", out var argsProp))
            {
                argsJson = argsProp.GetRawText();
            }

            string executionResult;
            bool isError = false;

            try
            {
                executionResult = await _registry.ExecuteAsync(toolName, argsJson).ConfigureAwait(false);
                if (executionResult.StartsWith("Error:") || executionResult.StartsWith("Safety Policy Violation"))
                {
                    isError = true;
                }
            }
            catch (Exception ex)
            {
                executionResult = $"Tool execution error: {ex.Message}";
                isError = true;
            }

            var callResult = new McpToolCallResult
            {
                Content = new List<McpContentItem>
                {
                    new McpContentItem { Type = "text", Text = executionResult }
                },
                IsError = isError
            };

            return SerializeResponse(new McpJsonRpcResponse
            {
                Id = req.Id,
                Result = callResult
            });
        }

        /// <summary>
        /// Generates a standard JSON-RPC 2.0 notification when registered tools change.
        /// </summary>
        public string CreateToolsListChangedNotification()
        {
            var notification = new
            {
                jsonrpc = "2.0",
                method = "notifications/tools/list_changed",
                @params = new { }
            };
            return JsonSerializer.Serialize(notification, _jsonOptions);
        }

        private string SerializeResponse(McpJsonRpcResponse response)
        {
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
    }
}
