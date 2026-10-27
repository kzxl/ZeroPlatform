using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Data
{
    public sealed class FactoryRecord
    {
        public string MachineId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Efficiency { get; set; }
        public int DefectCount { get; set; }

        public FactoryRecord() { }

        public FactoryRecord(string machineId, string status, double efficiency, int defectCount)
        {
            MachineId = machineId;
            Status = status;
            Efficiency = efficiency;
            DefectCount = defectCount;
        }
    }

    /// <summary>
    /// Columnar and tabular factory dataset query tool for AI agents.
    /// Provides zero-allocation structured filtering, defect summarization, and machine health lookups.
    /// </summary>
    public static class DataFrameQueryTool
    {
        private static readonly ConcurrentDictionary<string, List<FactoryRecord>> _tables = new ConcurrentDictionary<string, List<FactoryRecord>>(StringComparer.OrdinalIgnoreCase);

        static DataFrameQueryTool()
        {
            // Seed factory equipment records
            var records = new List<FactoryRecord>
            {
                new FactoryRecord("CNC-01", "RUNNING", 98.4, 0),
                new FactoryRecord("CNC-02", "WARNING", 76.1, 4),
                new FactoryRecord("ROBOT-ARM-01", "RUNNING", 99.5, 0),
                new FactoryRecord("PRESS-03", "CRITICAL_ERROR", 41.2, 18),
                new FactoryRecord("CONVEYOR-04", "IDLE", 0.0, 0)
            };
            _tables["machines"] = records;
        }

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var schema = new JsonSchemaConstraint("QueryFactoryRecords")
                .AddProperty("tableName", SchemaPropertyType.String, required: true)
                .AddProperty("statusFilter", SchemaPropertyType.String, required: false)
                .AddProperty("minDefects", SchemaPropertyType.Number, required: false);

            registry.Register(new AgentTool(
                "query_factory_records",
                "Queries tabular factory equipment status, efficiency ratings, and defect counts.",
                "tableName: string, statusFilter: string, minDefects: int",
                ExecuteQueryRecordsAsync,
                schema: schema,
                requiresApproval: false
            ));
        }

        private static Task<string> ExecuteQueryRecordsAsync(string arg)
        {
            try
            {
                string tableName = "machines";
                string? statusFilter = null;
                int minDefects = 0;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tableName", out var t)) tableName = t.GetString() ?? tableName;
                        if (root.TryGetProperty("statusFilter", out var s)) statusFilter = s.GetString();
                        if (root.TryGetProperty("minDefects", out var d)) minDefects = d.GetInt32();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(arg))
                {
                    tableName = arg.Trim();
                }

                if (!_tables.TryGetValue(tableName, out var list))
                {
                    return Task.FromResult($"Table '{tableName}' was not found.");
                }

                var matches = new List<FactoryRecord>();
                foreach (var rec in list)
                {
                    if (!string.IsNullOrEmpty(statusFilter) && !string.Equals(rec.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (rec.DefectCount < minDefects)
                    {
                        continue;
                    }
                    matches.Add(rec);
                }

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    table = tableName,
                    totalMatched = matches.Count,
                    records = matches
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to query records: {ex.Message}");
            }
        }
    }
}
