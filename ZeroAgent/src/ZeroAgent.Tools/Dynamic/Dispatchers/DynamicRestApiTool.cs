using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Dynamic.Model;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Dynamic.Dispatchers
{
    /// <summary>
    /// Dynamic agent tool that executes HTTP REST APIs based on declarative JSON/DB definitions.
    /// </summary>
    public sealed class DynamicRestApiTool : IAgentTool
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ToolDefinitionRecord _def;
        private readonly HttpClient _httpClient;

        public string Name => _def.Name;
        public string Description => _def.Description;
        public string ParameterSignature { get; }
        public JsonSchemaConstraint? Schema { get; }
        public bool RequiresApproval => _def.RequiresApproval;
        public ToolDefinitionRecord Definition => _def;

        public DynamicRestApiTool(ToolDefinitionRecord def, HttpClient? httpClient = null)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _httpClient = httpClient ?? SharedHttpClient;

            // Build signature and schema
            var sigSb = new StringBuilder();
            Schema = new JsonSchemaConstraint(def.Name);

            for (int i = 0; i < def.Parameters.Count; i++)
            {
                var p = def.Parameters[i];
                if (i > 0) sigSb.Append(", ");
                sigSb.Append($"{p.Name}: {p.Type}");
                if (!p.IsRequired) sigSb.Append("?");

                Schema.AddProperty(p.Name, MapSchemaPropertyType(p.Type), p.IsRequired);
            }

            ParameterSignature = sigSb.ToString();
        }

        public async Task<string> ExecuteAsync(string argument)
        {
            try
            {
                var argsMap = ParseArguments(argument);
                string url = InterpolateString(_def.Execution.UrlTemplate, argsMap);
                string methodStr = _def.Execution.Method?.ToUpperInvariant() ?? "POST";
                HttpMethod method = new HttpMethod(methodStr);

                using var request = new HttpRequestMessage(method, url);

                // Add configured headers
                if (_def.Execution.Headers != null)
                {
                    foreach (var h in _def.Execution.Headers)
                    {
                        string headerVal = InterpolateString(h.Value, argsMap);
                        request.Headers.TryAddWithoutValidation(h.Key, headerVal);
                    }
                }

                // Add body if applicable
                if (method != HttpMethod.Get && method != HttpMethod.Head)
                {
                    string body;
                    if (!string.IsNullOrWhiteSpace(_def.Execution.BodyTemplate))
                    {
                        body = InterpolateString(_def.Execution.BodyTemplate, argsMap);
                    }
                    else
                    {
                        body = JsonSerializer.Serialize(argsMap);
                    }
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Tool execution failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {content}";
                }

                return content;
            }
            catch (Exception ex)
            {
                return $"Tool execution failed with error: {ex.Message}";
            }
        }

        public async Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request)
        {
            var sw = Stopwatch.StartNew();
            string output = await ExecuteAsync(request.ArgumentsJson).ConfigureAwait(false);
            sw.Stop();

            if (output.StartsWith("Tool execution failed with"))
            {
                return ToolCallResponse.CreateFailure(request.CallId, Name, output, sw.Elapsed);
            }

            return ToolCallResponse.CreateSuccess(request.CallId, Name, output, sw.Elapsed);
        }

        private static Dictionary<string, string> ParseArguments(string argument)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(argument)) return result;

            try
            {
                using var doc = JsonDocument.Parse(argument);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value.ToString();
                    }
                    return result;
                }
            }
            catch
            {
                // Raw string fallback
            }

            result["arg"] = argument;
            return result;
        }

        private static string InterpolateString(string template, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            var sb = new StringBuilder(template);
            foreach (var kvp in values)
            {
                sb.Replace("{" + kvp.Key + "}", kvp.Value);
            }
            return sb.ToString();
        }

        private static SchemaPropertyType MapSchemaPropertyType(string type)
        {
            switch (type?.ToLowerInvariant())
            {
                case "int":
                case "integer":
                case "float":
                case "double":
                case "number":
                    return SchemaPropertyType.Number;
                case "bool":
                case "boolean":
                    return SchemaPropertyType.Boolean;
                case "array":
                    return SchemaPropertyType.Array;
                case "object":
                    return SchemaPropertyType.Object;
                default:
                    return SchemaPropertyType.String;
            }
        }
    }
}
