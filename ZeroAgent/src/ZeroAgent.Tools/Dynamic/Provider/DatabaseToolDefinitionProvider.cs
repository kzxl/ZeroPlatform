using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Tools.Dynamic.Model;

namespace ZeroAgent.Tools.Dynamic.Provider
{
    /// <summary>
    /// Loads tool definitions directly from enterprise database tables (Sys_AgentTools, Sys_AgentToolParameters).
    /// Supports automatic polling and manual invalidation for live updates.
    /// </summary>
    public sealed class DatabaseToolDefinitionProvider : IToolDefinitionProvider
    {
        private readonly Func<IDbConnection> _connectionFactory;
        private readonly string _toolsTableName;
        private readonly string _paramsTableName;
        private readonly Timer? _pollTimer;
        private int _lastMaxVersion = 0;
        private bool _disposed;

        public string ProviderSource { get; }

        public event EventHandler? DefinitionsChanged;

        public DatabaseToolDefinitionProvider(
            Func<IDbConnection> connectionFactory,
            string providerSource = "Database: ERP_MDS",
            string toolsTableName = "Sys_AgentTools",
            string paramsTableName = "Sys_AgentToolParameters",
            TimeSpan? pollingInterval = null)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            ProviderSource = providerSource;
            _toolsTableName = toolsTableName;
            _paramsTableName = paramsTableName;

            if (pollingInterval.HasValue && pollingInterval.Value > TimeSpan.Zero)
            {
                _pollTimer = new Timer(OnPollElapsed, null, pollingInterval.Value, pollingInterval.Value);
            }
        }

        public async Task<IReadOnlyList<ToolDefinitionRecord>> LoadDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<ToolDefinitionRecord>();

            await Task.Run(() =>
            {
                using var conn = _connectionFactory();
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                // Query Tools
                string toolSql = $"SELECT ToolId, ToolName, Description, Category, ExecutionType, ExecutionPayload, RequiresApproval, AllowedRolesCsv, Version " +
                                 $"FROM {_toolsTableName} WHERE IsActive = 1 ORDER BY ToolId ASC";

                var toolRows = new List<(int ToolId, ToolDefinitionRecord Record)>();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = toolSql;
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string name = reader.GetString(1);
                        string desc = reader.GetString(2);
                        string cat = reader.IsDBNull(3) ? "General" : reader.GetString(3);
                        string execTypeStr = reader.IsDBNull(4) ? "RestApi" : reader.GetString(4);
                        string execPayload = reader.IsDBNull(5) ? "{}" : reader.GetString(5);
                        bool reqApproval = !reader.IsDBNull(6) && Convert.ToBoolean(reader.GetValue(6));
                        string rolesCsv = reader.IsDBNull(7) ? "All" : reader.GetString(7);
                        int ver = reader.IsDBNull(8) ? 1 : reader.GetInt32(8);

                        if (ver > _lastMaxVersion) _lastMaxVersion = ver;

                        ToolExecutionType execType = ToolExecutionType.RestApi;
                        if (Enum.TryParse<ToolExecutionType>(execTypeStr, true, out var parsedType))
                        {
                            execType = parsedType;
                        }

                        ToolExecutionConfig execConfig = new ToolExecutionConfig();
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(execPayload))
                            {
                                if (execPayload.TrimStart().StartsWith("{"))
                                {
                                    execConfig = JsonSerializer.Deserialize<ToolExecutionConfig>(execPayload) ?? new ToolExecutionConfig();
                                }
                                else if (execType == ToolExecutionType.ParameterizedSql)
                                {
                                    execConfig.SqlQuery = execPayload;
                                }
                                else if (execType == ToolExecutionType.RestApi)
                                {
                                    execConfig.UrlTemplate = execPayload;
                                }
                            }
                        }
                        catch
                        {
                            // fallback default config
                        }

                        var allowedRoles = new List<string>();
                        if (!string.IsNullOrWhiteSpace(rolesCsv))
                        {
                            foreach (var r in rolesCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                allowedRoles.Add(r.Trim());
                            }
                        }

                        var rec = new ToolDefinitionRecord
                        {
                            Name = name,
                            Description = desc,
                            Category = cat,
                            ExecutionType = execType,
                            RequiresApproval = reqApproval,
                            AllowedRoles = allowedRoles,
                            Execution = execConfig,
                            IsActive = true,
                            Version = ver
                        };

                        toolRows.Add((id, rec));
                    }
                }

                // Query Parameters for all loaded tools
                if (toolRows.Count > 0)
                {
                    string paramSql = $"SELECT ToolId, ParamName, DataType, Description, IsRequired, DefaultValue, EnumValuesCsv " +
                                      $"FROM {_paramsTableName} ORDER BY ToolId ASC, SortOrder ASC";

                    var toolMap = new Dictionary<int, ToolDefinitionRecord>();
                    foreach (var tr in toolRows)
                    {
                        toolMap[tr.ToolId] = tr.Record;
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = paramSql;
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            int tId = reader.GetInt32(0);
                            if (toolMap.TryGetValue(tId, out var targetTool))
                            {
                                string pName = reader.GetString(1);
                                string pType = reader.IsDBNull(2) ? "string" : reader.GetString(2);
                                string pDesc = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                                bool req = !reader.IsDBNull(4) && Convert.ToBoolean(reader.GetValue(4));
                                string? defVal = reader.IsDBNull(5) ? null : reader.GetString(5);
                                string? enumCsv = reader.IsDBNull(6) ? null : reader.GetString(6);

                                List<string>? enumList = null;
                                if (!string.IsNullOrWhiteSpace(enumCsv))
                                {
                                    enumList = new List<string>(enumCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                                }

                                targetTool.Parameters.Add(new ToolParameterDefinition
                                {
                                    Name = pName,
                                    Type = pType,
                                    Description = pDesc,
                                    IsRequired = req,
                                    DefaultValue = defVal,
                                    EnumValues = enumList
                                });
                            }
                        }
                    }
                }

                foreach (var tr in toolRows)
                {
                    results.Add(tr.Record);
                }
            }, cancellationToken);

            return results;
        }

        public void TriggerReload()
        {
            DefinitionsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnPollElapsed(object? state)
        {
            try
            {
                using var conn = _connectionFactory();
                if (conn.State != ConnectionState.Open) conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COALESCE(MAX(Version), 0) FROM {_toolsTableName} WHERE IsActive = 1";
                object? val = cmd.ExecuteScalar();
                if (val != null && int.TryParse(val.ToString(), out int currentMax))
                {
                    if (currentMax > _lastMaxVersion)
                    {
                        _lastMaxVersion = currentMax;
                        DefinitionsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch
            {
                // Silently swallow poll failure to avoid crashing background timer
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer?.Dispose();
        }
    }
}
