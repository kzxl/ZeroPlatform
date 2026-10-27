using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroData.Core;
using ZeroData.Sql.Dialects;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Partial implementation of DynamicDatabaseQueryTool handling analytical NL-to-SQL query building,
    /// safe execution over live connections or DataFrames, and LLM schema prompt generation.
    /// </summary>
    public static partial class DynamicDatabaseQueryTool
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string ExecuteBuildQuery(string arg)
        {
            try
            {
                var spec = JsonSerializer.Deserialize<AnalyticalQuerySpec>(arg, JsonOptions);
                if (spec == null) return JsonSerializer.Serialize(new { success = false, error = "Invalid query payload." }, JsonOptions);

                var dialect = ResolveDialectForTable(spec.Table);
                var result = DynamicSqlBuilder.Build(spec, _catalog, dialect);

                return JsonSerializer.Serialize(new
                {
                    success = result.IsValid,
                    dialect = dialect.ProviderName,
                    sql = result.Sql,
                    parameters = result.Parameters,
                    error = result.ErrorMessage
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
            }
        }

        public static async Task<string> ExecuteAnalyticsAsync(string arg)
        {
            try
            {
                var spec = JsonSerializer.Deserialize<AnalyticalQuerySpec>(arg, JsonOptions);
                if (spec == null || string.IsNullOrWhiteSpace(spec.Table))
                    return JsonSerializer.Serialize(new { success = false, error = "Target table must be specified." }, JsonOptions);

                // 1. Live SQL Pushdown
                if (_tableToConnStr.TryGetValue(spec.Table, out var connStr))
                {
                    return await ExecuteLiveSqlAnalyticsAsync(connStr, spec);
                }

                // 2. In-Memory DataFrame Analytical Evaluation
                if (_dataFrames.TryGetValue(spec.Table, out var df))
                {
                    var data = DataFrameAnalyticsEngine.Execute(df, spec);
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        source = "DATAFRAME_IN_MEMORY",
                        table = spec.Table,
                        totalMatched = data.Count,
                        data
                    }, JsonOptions);
                }

                return JsonSerializer.Serialize(new { success = false, error = $"Table '{spec.Table}' was not found in catalog." }, JsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Analytics execution failed: {ex.Message}" }, JsonOptions);
            }
        }

        public static string GenerateSchemaContext(string? filterTable = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Database Schema Catalog");

            var tables = string.IsNullOrWhiteSpace(filterTable)
                ? _catalog.Values
                : _catalog.Values.Where(t => t.TableName.Equals(filterTable, StringComparison.OrdinalIgnoreCase));

            foreach (var table in tables)
            {
                sb.AppendLine($"- Table: `{table.TableName}` ({table.Description})");
                sb.AppendLine("  Columns:");
                foreach (var c in table.Columns)
                {
                    string pk = c.IsPrimaryKey ? " [PK]" : "";
                    string vec = c.IsVector ? $" [Vector {c.VectorDimension}d]" : "";
                    sb.AppendLine($"    * `{c.Name}`: {c.DataType}{pk}{vec}");
                }
                if (table.ForeignKeys.Count > 0)
                {
                    sb.AppendLine("  Foreign Keys:");
                    foreach (var fk in table.ForeignKeys)
                    {
                        sb.AppendLine($"    * `{fk.FromColumn}` -> `{fk.ToTable}`.`{fk.ToColumn}`");
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static ISqlDialect ResolveDialectForTable(string table)
        {
            if (_tableToConnStr.TryGetValue(table, out var connStr) && !string.IsNullOrWhiteSpace(connStr))
            {
                try
                {
                    using var conn = CreateConnection(connStr);
                    return SqlDialectFactory.GetDialect(conn);
                }
                catch { }
            }
            return new SqlServerDialect();
        }

        private static Task<string> ExecuteLiveSqlAnalyticsAsync(string connStr, AnalyticalQuerySpec spec)
        {
            var dialect = ResolveDialectForTable(spec.Table);
            var buildResult = DynamicSqlBuilder.Build(spec, _catalog, dialect);
            if (!buildResult.IsValid)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = buildResult.ErrorMessage }));
            }

            using var conn = CreateConnection(connStr);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = buildResult.Sql;

            foreach (var p in buildResult.Parameters)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = p.Key;
                param.Value = p.Value ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            using var reader = cmd.ExecuteReader();
            int limit = spec.Limit ?? 500;
            var df = DataFrame.FromDataReader(reader, limit);

            var rows = new List<Dictionary<string, object?>>(df.RowCount);
            for (int r = 0; r < df.RowCount; r++)
            {
                var rowDict = new Dictionary<string, object?>(df.ColumnCount);
                foreach (var colName in df.ColumnNames)
                {
                    rowDict[colName] = df[colName].GetValue(r);
                }
                rows.Add(rowDict);
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                source = "SQL_PUSHDOWN_LIVE",
                table = spec.Table,
                sql = buildResult.Sql,
                totalMatched = df.RowCount,
                data = rows
            }));
        }
    }
}
