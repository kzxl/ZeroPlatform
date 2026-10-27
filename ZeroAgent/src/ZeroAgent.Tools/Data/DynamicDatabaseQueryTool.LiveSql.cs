using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroData.Core;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Partial implementation of DynamicDatabaseQueryTool handling live SQL database schema introspection
    /// and SQL Pushdown query execution with parameterization.
    /// </summary>
    public static partial class DynamicDatabaseQueryTool
    {
        /// <summary>
        /// Automatically introspects and registers all tables and columns from a live SQL database into the Agent catalog.
        /// Does NOT load entire data into memory; queries will execute via SQL Pushdown.
        /// </summary>
        public static void RegisterLiveDatabase(string connectionString, string databaseName = "")
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            using (var conn = CreateConnection(connectionString))
            {
                conn.Open();

                // 1. Discover all tables
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TABLE_NAME, TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                    var tables = new List<(string Name, string Schema)>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tName = reader.GetString(0);
                            string tSchema = reader.GetString(1);
                            tables.Add((tName, tSchema));
                        }
                    }

                    // 2. Discover columns for each table
                    foreach (var (tName, tSchema) in tables)
                    {
                        using (var colCmd = conn.CreateCommand())
                        {
                            colCmd.CommandText = $"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tName}' AND TABLE_SCHEMA = '{tSchema}' ORDER BY ORDINAL_POSITION";
                            var meta = new TableMetadata(tName, $"Live database table from {databaseName} ({tSchema}.{tName})", estimatedRowCount: 0);

                            using (var colReader = colCmd.ExecuteReader())
                            {
                                while (colReader.Read())
                                {
                                    string cName = colReader.GetString(0);
                                    string cType = colReader.GetString(1);
                                    bool isPk = cName.Equals("id", StringComparison.OrdinalIgnoreCase) || cName.EndsWith("_id", StringComparison.OrdinalIgnoreCase);
                                    meta.Columns.Add(new ColumnMetadata(cName, cType, isPrimaryKey: isPk));
                                }
                            }

                            _catalog[tName] = meta;
                            _tableToConnStr[tName] = connectionString;
                        }
                    }
                }
            }
        }

        private static Task<string> ExecuteSqlPushdownQueryAsync(string connStr, string tableName, string? filterCol, string? filterVal, int limit)
        {
            try
            {
                using (var conn = CreateConnection(connStr))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        var sb = new StringBuilder();
                        int safeLimit = Math.Max(1, Math.Min(limit, 500));
                        sb.Append("SELECT TOP (").Append(safeLimit).Append(") * FROM [").Append(tableName.Replace("]", "]]")).Append("]");

                        if (!string.IsNullOrWhiteSpace(filterCol) && filterVal != null)
                        {
                            sb.Append(" WHERE [").Append(filterCol.Replace("]", "]]")).Append("] = @val");
                            var valParam = cmd.CreateParameter();
                            valParam.ParameterName = "@val";
                            valParam.Value = filterVal;
                            cmd.Parameters.Add(valParam);
                        }

                        cmd.CommandText = sb.ToString();

                        using (var reader = cmd.ExecuteReader())
                        {
                            var df = DataFrame.FromDataReader(reader, safeLimit);

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
                                table = tableName,
                                source = "SQL_PUSHDOWN_LIVE",
                                totalMatched = df.RowCount,
                                returned = df.RowCount,
                                data = rows
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to execute live SQL pushdown: {ex.Message}");
            }
        }
    }
}
