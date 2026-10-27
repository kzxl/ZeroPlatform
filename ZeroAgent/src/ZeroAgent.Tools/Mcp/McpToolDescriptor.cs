using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tools.Mcp
{
    /// <summary>
    /// Standard Model Context Protocol (MCP) Tool Descriptor model.
    /// Interoperable with modern agent ecosystems (Claude Desktop, Semantic Kernel, OpenAI, LangChain).
    /// </summary>
    public sealed class McpToolDescriptor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("inputSchema")]
        public JsonElement InputSchema { get; set; }
    }

    /// <summary>
    /// Model Context Protocol (MCP) Tool Exporter.
    /// Converts ZeroAgent's AgentToolRegistry into standard MCP tool descriptors and JSON-RPC tool lists.
    /// </summary>
    public static class McpToolExporter
    {
        public static List<McpToolDescriptor> ExportTools(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var result = new List<McpToolDescriptor>();
            foreach (var tool in registry.Tools)
            {
                var props = new Dictionary<string, object>();
                if (tool.Schema != null)
                {
                    foreach (var kvp in tool.Schema.Properties)
                    {
                        props[kvp.Key] = new { type = kvp.Value.ToString().ToLowerInvariant() };
                    }
                }
                var schemaObj = new
                {
                    type = "object",
                    properties = props,
                    required = tool.Schema?.RequiredProperties
                };
                string json = JsonSerializer.Serialize(schemaObj);
                using var doc = JsonDocument.Parse(json);
                var schemaElement = doc.RootElement.Clone();

                result.Add(new McpToolDescriptor
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = schemaElement
                });
            }

            return result;
        }

        public static string ToMcpJson(AgentToolRegistry registry, bool indented = true)
        {
            var descriptors = ExportTools(registry);
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(new { tools = descriptors }, options);
        }
    }
}
