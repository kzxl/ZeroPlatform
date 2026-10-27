using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroData.Core;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Partial implementation of DynamicDatabaseQueryTool handling in-memory DataFrame queries,
    /// columnar mask filtering, vector similarity retrieval, and default dataset seeding.
    /// </summary>
    public static partial class DynamicDatabaseQueryTool
    {
        private static Task<string> ExecuteQueryTableAsync(string arg)
        {
            try
            {
                string tableName = string.Empty;
                string? filterCol = null;
                string? filterVal = null;
                int limit = 10;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(arg);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("tableName", out var t)) tableName = t.GetString() ?? string.Empty;
                    if (root.TryGetProperty("whereColumn", out var c)) filterCol = c.GetString();
                    if (root.TryGetProperty("whereValue", out var v)) filterVal = v.GetString();
                    if (root.TryGetProperty("limit", out var l)) limit = l.GetInt32();
                }
                else
                {
                    tableName = arg.Trim();
                }

                if (!_dataFrames.TryGetValue(tableName, out var df))
                {
                    if (_tableToConnStr.TryGetValue(tableName, out var connStr))
                    {
                        return ExecuteSqlPushdownQueryAsync(connStr, tableName, filterCol, filterVal, limit);
                    }
                    return Task.FromResult($"Error: Table '{tableName}' was not found.");
                }

                // Apply filter if specified
                DataFrame filtered = df;
                if (!string.IsNullOrWhiteSpace(filterCol) && filterVal != null)
                {
                    if (df.HasColumn(filterCol))
                    {
                        var mask = new SelectionMask(df.RowCount);
                        var targetCol = df[filterCol];
                        for (int r = 0; r < df.RowCount; r++)
                        {
                            var val = targetCol.GetValue(r)?.ToString();
                            if (string.Equals(val, filterVal, StringComparison.OrdinalIgnoreCase))
                            {
                                mask.SetSelected(r, true);
                            }
                        }
                        filtered = df.Filter(mask);
                    }
                }

                // Materialize rows up to limit
                int actualCount = Math.Min(limit, filtered.RowCount);
                var rows = new List<Dictionary<string, object?>>(actualCount);

                for (int r = 0; r < actualCount; r++)
                {
                    var rowDict = new Dictionary<string, object?>(filtered.ColumnCount);
                    foreach (var colName in filtered.ColumnNames)
                    {
                        var col = filtered[colName];
                        if (col is VectorColumn)
                        {
                            rowDict[colName] = $"[Vector {((VectorColumn)col).Dimension}d]";
                        }
                        else
                        {
                            rowDict[colName] = col.GetValue(r);
                        }
                    }
                    rows.Add(rowDict);
                }

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    table = tableName,
                    totalMatched = filtered.RowCount,
                    returned = actualCount,
                    data = rows
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to execute query: {ex.Message}");
            }
        }

        private static Task<string> ExecuteVectorSearchTableAsync(string arg)
        {
            try
            {
                string tableName = string.Empty;
                string colName = string.Empty;
                float[]? queryVec = null;
                int topK = 5;

                using (var doc = JsonDocument.Parse(arg))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("tableName", out var t)) tableName = t.GetString() ?? string.Empty;
                    if (root.TryGetProperty("columnName", out var c)) colName = c.GetString() ?? string.Empty;
                    if (root.TryGetProperty("topK", out var k)) topK = k.GetInt32();

                    if (root.TryGetProperty("queryVector", out var qv) && qv.ValueKind == JsonValueKind.Array)
                    {
                        queryVec = qv.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    }
                }

                if (!_dataFrames.TryGetValue(tableName, out var df))
                    return Task.FromResult($"Table '{tableName}' not found.");

                if (!df.HasColumn(colName) || !(df[colName] is VectorColumn vecCol))
                    return Task.FromResult($"Column '{colName}' in '{tableName}' is not a valid VectorColumn.");

                queryVec ??= new float[vecCol.Dimension]; // Fallback if empty for testing
                var matches = vecCol.SearchNearest(queryVec, topK);

                var results = new List<object>(matches.Length);
                foreach (var m in matches)
                {
                    var rowData = new Dictionary<string, object?>();
                    foreach (var cn in df.ColumnNames)
                    {
                        if (cn != colName) rowData[cn] = df[cn].GetValue(m.RowIndex);
                    }
                    results.Add(new { rowIndex = m.RowIndex, score = m.Score, row = rowData });
                }

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    table = tableName,
                    vectorColumn = colName,
                    totalResults = matches.Length,
                    results
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Vector search failed: {ex.Message}");
            }
        }

        private static void SeedDefaultIndustrialDataset()
        {
            int count = 5;
            int[] ids = new[] { 1, 2, 3, 4, 5 };
            string[] machines = new[] { "CNC-01", "CNC-02", "ROBOT-ARM-01", "PRESS-03", "CONVEYOR-04" };
            string[] status = new[] { "RUNNING", "WARNING", "RUNNING", "CRITICAL_ERROR", "IDLE" };
            double[] efficiency = new[] { 98.4, 76.1, 99.5, 41.2, 0.0 };
            int[] defects = new[] { 0, 4, 0, 18, 0 };

            var vecCol = new VectorColumn("embedding", 16);
            for (int i = 0; i < count; i++)
            {
                float[] v = new float[16];
                v[i % 16] = 1.0f;
                vecCol.AddVector(v);
            }

            var df = new DataFrame(
                new DataColumn<int>("id", ids),
                new DataColumn<string>("machine_id", machines),
                new DataColumn<string>("status", status),
                new DataColumn<double>("efficiency", efficiency),
                new DataColumn<int>("defect_count", defects),
                vecCol
            );

            RegisterTable("factory_machines", df, "Factory equipment real-time operational status, efficiency, and defect metrics.");
        }
    }
}
