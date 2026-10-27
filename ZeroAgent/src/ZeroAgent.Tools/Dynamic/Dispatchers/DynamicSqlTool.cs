using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Dynamic.Model;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Dynamic.Dispatchers
{
    /// <summary>
    /// Dynamic agent tool executing safe, parameterized SQL queries defined in DB or JSON manifests.
    /// Strictly enforces read-only access (SELECT queries only) with SQL injection protection.
    /// </summary>
    public sealed class DynamicSqlTool : IAgentTool
    {
        private static readonly Regex UnsafeSqlPattern = new Regex(
            @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE|GRANT|REVOKE)\b", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ToolDefinitionRecord _def;
        private readonly Func<IDbConnection> _connectionFactory;
        private readonly int _maxRows;

        public string Name => _def.Name;
        public string Description => _def.Description;
        public string ParameterSignature { get; }
        public JsonSchemaConstraint? Schema { get; }
        public bool RequiresApproval => _def.RequiresApproval;
        public ToolDefinitionRecord Definition => _def;

        public DynamicSqlTool(ToolDefinitionRecord def, Func<IDbConnection> connectionFactory, int maxRows = 20)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _maxRows = maxRows;

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

        public Task<string> ExecuteAsync(string argument)
        {
            return Task.Run(() =>
            {
                try
                {
                    string sql = _def.Execution.SqlQuery ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        return "Tool execution failed: No SQL query configured for this tool.";
                    }

                    // 1. Safety verification: Must be strictly read-only
                    if (UnsafeSqlPattern.IsMatch(sql))
                    {
                        return "Safety Policy Violation: DynamicSqlTool strictly forbids data modification keywords (INSERT, UPDATE, DELETE, DROP, ALTER, EXEC).";
                    }

                    var argsMap = ParseArguments(argument);

                    using var conn = _connectionFactory();
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;

                    // 2. Add parameterized values
                    foreach (var p in _def.Parameters)
                    {
                        var dbParam = cmd.CreateParameter();
                        dbParam.ParameterName = p.Name.StartsWith("@") ? p.Name : "@" + p.Name;

                        if (argsMap.TryGetValue(p.Name, out var valStr))
                        {
                            dbParam.Value = ConvertValue(valStr, p.Type);
                        }
                        else if (!string.IsNullOrEmpty(p.DefaultValue))
                        {
                            dbParam.Value = ConvertValue(p.DefaultValue, p.Type);
                        }
                        else
                        {
                            dbParam.Value = DBNull.Value;
                        }

                        cmd.Parameters.Add(dbParam);
                    }

                    // 3. Execute and format result
                    using var reader = cmd.ExecuteReader();
                    var sb = new StringBuilder();

                    int colCount = reader.FieldCount;
                    var colNames = new string[colCount];
                    for (int i = 0; i < colCount; i++) colNames[i] = reader.GetName(i);

                    // Markdown table header
                    sb.Append("| ").Append(string.Join(" | ", colNames)).AppendLine(" |");
                    sb.Append("|").Append(string.Join("|", new string('-', colCount).ToCharArray())).AppendLine("|");

                    int rowCount = 0;
                    while (reader.Read())
                    {
                        rowCount++;
                        if (rowCount > _maxRows)
                        {
                            sb.AppendLine($"| ... [Truncated: showing top {_maxRows} rows] |");
                            break;
                        }

                        var rowVals = new string[colCount];
                        for (int i = 0; i < colCount; i++)
                        {
                            rowVals[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? string.Empty;
                        }
                        sb.Append("| ").Append(string.Join(" | ", rowVals)).AppendLine(" |");
                    }

                    if (rowCount == 0)
                    {
                        return "No records found matching the query criteria.";
                    }

                    return sb.ToString().TrimEnd();
                }
                catch (Exception ex)
                {
                    return $"Tool execution failed with error: {ex.Message}";
                }
            });
        }

        public async Task<ToolCallResponse> ExecuteCallAsync(ToolCallRequest request)
        {
            var sw = Stopwatch.StartNew();
            string output = await ExecuteAsync(request.ArgumentsJson).ConfigureAwait(false);
            sw.Stop();

            if (output.StartsWith("Tool execution failed with") || output.StartsWith("Safety Policy Violation"))
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
            }

            return result;
        }

        private static object ConvertValue(string? val, string type)
        {
            if (val == null) return DBNull.Value;

            switch (type?.ToLowerInvariant())
            {
                case "int":
                case "integer":
                    return int.TryParse(val, out int iVal) ? (object)iVal : 0;
                case "float":
                case "double":
                case "number":
                    return double.TryParse(val, out double dVal) ? (object)dVal : 0.0;
                case "bool":
                case "boolean":
                    return bool.TryParse(val, out bool bVal) ? (object)bVal : false;
                default:
                    return val;
            }
        }

        private static SchemaPropertyType MapSchemaPropertyType(string type)
        {
            switch (type?.ToLowerInvariant())
            {
                case "int":
                case "integer":
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
