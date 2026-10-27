using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using Task = global::System.Threading.Tasks.Task;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Dynamic.Dispatchers;
using ZeroAgent.Tools.Dynamic.Model;

namespace ZeroAgent.Tools.Dynamic
{
    /// <summary>
    /// Factory for creating executable IAgentTool instances from declarative ToolDefinitionRecords.
    /// </summary>
    public sealed class DynamicToolFactory
    {
        private readonly HttpClient? _httpClient;
        private readonly Func<string, IDbConnection>? _dbConnectionResolver;
        private readonly Dictionary<string, Func<string, string>> _customDelegates;

        public DynamicToolFactory(
            HttpClient? httpClient = null,
            Func<string, IDbConnection>? dbConnectionResolver = null)
        {
            _httpClient = httpClient;
            _dbConnectionResolver = dbConnectionResolver;
            _customDelegates = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        public void RegisterDelegate(string toolName, Func<string, string> handler)
        {
            _customDelegates[toolName] = handler;
        }

        public IAgentTool CreateTool(ToolDefinitionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            switch (record.ExecutionType)
            {
                case ToolExecutionType.RestApi:
                    return new DynamicRestApiTool(record, _httpClient);

                case ToolExecutionType.ParameterizedSql:
                    if (_dbConnectionResolver == null)
                    {
                        throw new InvalidOperationException(
                            $"Cannot create DynamicSqlTool for '{record.Name}': No IDbConnection resolver is configured in DynamicToolFactory.");
                    }
                    string connKey = record.Execution?.ConnectionKey ?? "Default";
                    return new DynamicSqlTool(record, () => _dbConnectionResolver(connKey));

                case ToolExecutionType.CustomDelegate:
                    if (_customDelegates.TryGetValue(record.Name, out var handler))
                    {
                        return new AgentTool(record.Name, record.Description, "string arguments", arg => Task.FromResult(handler(arg)), null, record.RequiresApproval);
                    }
                    return new AgentTool(record.Name, record.Description, "string arguments", arg => Task.FromResult($"[MOCK EXECUTION] Tool '{record.Name}' executed with args: {arg}"), null, record.RequiresApproval);

                default:
                    // Fallback to delegate or mock
                    if (_customDelegates.TryGetValue(record.Name, out var fallbackHandler))
                    {
                        return new AgentTool(record.Name, record.Description, "string arguments", arg => Task.FromResult(fallbackHandler(arg)), null, record.RequiresApproval);
                    }
                    return new DynamicRestApiTool(record, _httpClient);
            }
        }
    }
}
