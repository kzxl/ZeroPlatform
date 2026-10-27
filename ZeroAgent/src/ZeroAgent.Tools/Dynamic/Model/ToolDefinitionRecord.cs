using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZeroAgent.Tools.Dynamic.Model
{
    /// <summary>
    /// Supported dynamic execution backends for declarative agent tools.
    /// </summary>
    public enum ToolExecutionType
    {
        RestApi = 0,
        ParameterizedSql = 1,
        NativeAssembly = 2,
        McpRemote = 3,
        CustomDelegate = 4
    }

    /// <summary>
    /// Declarative parameter specification matching JSON Schema standard.
    /// </summary>
    public sealed class ToolParameterDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "string"; // "string", "int", "float", "bool", "array", "object"

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("required")]
        public bool IsRequired { get; set; } = true;

        [JsonPropertyName("defaultValue")]
        public string? DefaultValue { get; set; }

        [JsonPropertyName("enumValues")]
        public List<string>? EnumValues { get; set; }
    }

    /// <summary>
    /// Configuration payload for tool execution dispatch.
    /// </summary>
    public sealed class ToolExecutionConfig
    {
        // REST API Execution
        [JsonPropertyName("method")]
        public string Method { get; set; } = "POST"; // GET, POST, PUT, DELETE

        [JsonPropertyName("urlTemplate")]
        public string UrlTemplate { get; set; } = string.Empty;

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("bodyTemplate")]
        public string? BodyTemplate { get; set; }

        // Parameterized SQL Execution
        [JsonPropertyName("sqlQuery")]
        public string? SqlQuery { get; set; }

        [JsonPropertyName("connectionKey")]
        public string? ConnectionKey { get; set; }

        // Native Assembly Execution
        [JsonPropertyName("assemblyPath")]
        public string? AssemblyPath { get; set; }

        [JsonPropertyName("typeName")]
        public string? TypeName { get; set; }

        // MCP Remote Server Execution
        [JsonPropertyName("mcpServerUrl")]
        public string? McpServerUrl { get; set; }
    }

    /// <summary>
    /// Master record defining a dynamic, declarative AI agent tool.
    /// Storable in JSON manifest files or Database tables (Sys_AgentTools).
    /// </summary>
    public sealed class ToolDefinitionRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "General"; // Inventory, Production, Sales, Finance, RD

        [JsonPropertyName("executionType")]
        public ToolExecutionType ExecutionType { get; set; } = ToolExecutionType.RestApi;

        [JsonPropertyName("requiresApproval")]
        public bool RequiresApproval { get; set; } = false;

        [JsonPropertyName("allowedRoles")]
        public List<string> AllowedRoles { get; set; } = new List<string> { "All" };

        [JsonPropertyName("parameters")]
        public List<ToolParameterDefinition> Parameters { get; set; } = new List<ToolParameterDefinition>();

        [JsonPropertyName("execution")]
        public ToolExecutionConfig Execution { get; set; } = new ToolExecutionConfig();

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Root container for tools manifest JSON file.
    /// </summary>
    public sealed class ToolsManifestFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("tools")]
        public List<ToolDefinitionRecord> Tools { get; set; } = new List<ToolDefinitionRecord>();
    }
}
