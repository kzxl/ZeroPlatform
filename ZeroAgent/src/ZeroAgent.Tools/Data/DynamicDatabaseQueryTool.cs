using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroData.Core;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Dynamic Database & Columnar DataFrame query tool for ZeroAgent.
    /// Enables self-describing schema introspection, safe parameterized filtering,
    /// NL-to-SQL dynamic analytical building, and hybrid vector similarity retrieval.
    /// </summary>
    public static partial class DynamicDatabaseQueryTool
    {
        private static readonly ConcurrentDictionary<string, TableMetadata> _catalog = new ConcurrentDictionary<string, TableMetadata>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DataFrame> _dataFrames = new ConcurrentDictionary<string, DataFrame>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> _tableToConnStr = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Func<string, global::System.Data.IDbConnection>? _connectionFactory;

        static DynamicDatabaseQueryTool()
        {
            SeedDefaultIndustrialDataset();
        }

        public static void SetConnectionFactory(Func<string, global::System.Data.IDbConnection> factory)
        {
            _connectionFactory = factory;
        }

        public static global::System.Data.IDbConnection CreateConnection(string connectionString)
        {
            if (_connectionFactory != null) return _connectionFactory(connectionString);

            var type = Type.GetType("Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient")
                    ?? Type.GetType("System.Data.SqlClient.SqlConnection, System.Data.SqlClient")
                    ?? Type.GetType("System.Data.SqlClient.SqlConnection, System.Data");

            if (type != null) return (global::System.Data.IDbConnection)Activator.CreateInstance(type, connectionString)!;

            throw new InvalidOperationException("No IDbConnection provider available. Call DynamicDatabaseQueryTool.SetConnectionFactory(...) with your connection provider.");
        }

        public static void RegisterTable(string tableName, DataFrame dataFrame, string description = "")
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));

            _dataFrames[tableName] = dataFrame;

            var meta = new TableMetadata(tableName, description, dataFrame.RowCount);
            foreach (var colName in dataFrame.ColumnNames)
            {
                var col = dataFrame[colName];
                bool isVec = col is VectorColumn;
                int dim = isVec ? ((VectorColumn)col).Dimension : 0;
                meta.Columns.Add(new ColumnMetadata(colName, col.DataType.Name, isPrimaryKey: colName.Equals("id", StringComparison.OrdinalIgnoreCase), isVector: isVec, vectorDimension: dim));
            }
            _catalog[tableName] = meta;
        }

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry.Register(new AgentTool("db_list_tables", "Lists all registered tables in catalog.", "void", _ => Task.FromResult(ExecuteListTables())));

            registry.Register(new AgentTool("db_describe_table", "Returns schema metadata for a table.", "tableName: string", ExecuteDescribeTableAsync,
                schema: new JsonSchemaConstraint("DescribeTable").AddProperty("tableName", SchemaPropertyType.String, required: true)));

            registry.Register(new AgentTool("db_query_table", "Queries records using column filters.", "tableName: string, whereColumn?: string, whereValue?: string, limit?: int",
                ExecuteQueryTableAsync,
                schema: new JsonSchemaConstraint("QueryTable")
                    .AddProperty("tableName", SchemaPropertyType.String, required: true)
                    .AddProperty("whereColumn", SchemaPropertyType.String, required: false)
                    .AddProperty("whereValue", SchemaPropertyType.String, required: false)
                    .AddProperty("limit", SchemaPropertyType.Number, required: false)));

            registry.Register(new AgentTool("db_vector_search", "Executes hybrid vector similarity search across a table's VectorColumn.",
                "tableName: string, columnName: string, queryVector: float[], topK?: int", ExecuteVectorSearchTableAsync,
                schema: new JsonSchemaConstraint("VectorSearchTable")
                    .AddProperty("tableName", SchemaPropertyType.String, required: true)
                    .AddProperty("columnName", SchemaPropertyType.String, required: true)
                    .AddProperty("topK", SchemaPropertyType.Number, required: false)));

            registry.Register(new AgentTool("db_build_query", "Generates safe, parameterized SQL with dialect awareness from an AnalyticalQuerySpec.",
                "specJson: string", arg => Task.FromResult(ExecuteBuildQuery(arg))));

            registry.Register(new AgentTool("db_execute_analytics", "Executes complex analytical queries (aggregations, GROUP BY, HAVING, ORDER BY) on Live SQL or DataFrame.",
                "specJson: string", ExecuteAnalyticsAsync));

            registry.Register(new AgentTool("db_get_schema_prompt", "Returns database catalog schema formatted as LLM system prompt context.",
                "filterTable?: string", arg => Task.FromResult(GenerateSchemaContext(string.IsNullOrWhiteSpace(arg) ? null : arg.Trim()))));
        }

        private static string ExecuteListTables()
        {
            var summary = _catalog.Values.Select(t => new
            {
                table = t.TableName,
                description = t.Description,
                rowCount = t.EstimatedRowCount,
                columnCount = t.Columns.Count,
                columns = t.Columns.Select(c => c.Name).ToArray()
            }).ToList();

            return JsonSerializer.Serialize(new { totalTables = summary.Count, tables = summary });
        }

        private static Task<string> ExecuteDescribeTableAsync(string arg)
        {
            string tableName = arg.Trim();
            if (arg.TrimStart().StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(arg);
                    if (doc.RootElement.TryGetProperty("tableName", out var prop)) tableName = prop.GetString() ?? tableName;
                }
                catch { }
            }

            if (!_catalog.TryGetValue(tableName, out var meta)) return Task.FromResult($"Table '{tableName}' not found in catalog.");
            return Task.FromResult(JsonSerializer.Serialize(meta));
        }
    }
}
