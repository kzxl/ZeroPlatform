using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroAgent.Tools.Mcp
{
    /// <summary>
    /// Standard JSON-RPC 2.0 Request envelope for Model Context Protocol (MCP).
    /// </summary>
    public sealed class McpJsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    /// <summary>
    /// Standard JSON-RPC 2.0 Response envelope for Model Context Protocol (MCP).
    /// </summary>
    public sealed class McpJsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public McpJsonRpcError? Error { get; set; }
    }

    public sealed class McpJsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Data { get; set; }
    }

    public sealed class McpContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public sealed class McpToolCallResult
    {
        [JsonPropertyName("content")]
        public List<McpContentItem> Content { get; set; } = new List<McpContentItem>();

        [JsonPropertyName("isError")]
        public bool IsError { get; set; } = false;
    }
}
