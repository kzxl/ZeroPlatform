using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class IndustrialToolkitTests
    {
        [Fact]
        public void IndustrialToolkit_RegistersAllToolsSuccessfully()
        {
            var registry = new AgentToolRegistry();
            var safetyGate = new HitlSafetyGate();

            registry.RegisterIndustrialToolkit(safetyGate);

            Assert.True(registry.Count >= 7);
            Assert.True(registry.TryGetTool("read_plc_holding_registers", out var t1));
            Assert.False(t1.RequiresApproval);

            Assert.True(registry.TryGetTool("write_plc_holding_register", out var t2));
            Assert.True(t2.RequiresApproval);

            Assert.True(registry.TryGetTool("query_tsdb_metric", out _));
            Assert.True(registry.TryGetTool("detect_tsdb_anomalies", out _));
            Assert.True(registry.TryGetTool("get_system_telemetry", out _));
            Assert.True(registry.TryGetTool("query_factory_records", out _));
        }

        [Fact]
        public async Task PlcModbusTool_ReadsAndEnforcesHitlOnWrite()
        {
            var registry = new AgentToolRegistry();
            var safetyGate = new HitlSafetyGate();
            registry.RegisterIndustrialToolkit(safetyGate);

            // 1. Read holding register (Safe action - No HITL required)
            string readResult = await registry.ExecuteAsync("read_plc_holding_registers", "{\"address\": 40001, \"count\": 2}");
            Assert.Contains("40001", readResult);
            Assert.Contains("values", readResult);

            // 2. Write holding register (Sensitive action - Requires HITL approval)
            var writeTask = registry.ExecuteAsync("write_plc_holding_register", "{\"address\": 40001, \"value\": 99}");

            // Verify request is pending in safety gate
            await Task.Delay(50);
            var pending = safetyGate.PendingRequests;
            Assert.NotEmpty(pending);

            // Operator approves request
            foreach (var req in pending)
            {
                safetyGate.Approve(req.RequestId, "Operator-Phong");
            }

            string writeResult = await writeTask;
            Assert.Contains("Successfully wrote value 99 to holding register 40001", writeResult);

            // Check audit log
            Assert.NotEmpty(safetyGate.AuditLog);
            var lastLog = safetyGate.AuditLog[safetyGate.AuditLog.Count - 1];
            Assert.Equal("write_plc_holding_register", lastLog.ToolName);
            Assert.True(lastLog.Approved);
            Assert.Equal("Operator-Phong", lastLog.OperatorId);
        }

        [Fact]
        public async Task TsdbQueryTool_AggregatesAndDetectsAnomalies()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit();

            // 1. Query metric
            string queryResult = await registry.ExecuteAsync("query_tsdb_metric", "{\"metricName\":\"motor_temperature\",\"durationMinutes\":30}");
            Assert.Contains("motor_temperature", queryResult);
            Assert.Contains("avg", queryResult);

            // 2. Detect anomalies
            string anomalyResult = await registry.ExecuteAsync("detect_tsdb_anomalies", "{\"metricName\":\"motor_temperature\",\"sigmaThreshold\":2.5}");
            Assert.Contains("anomalyCount", anomalyResult);
            Assert.Contains("upperThreshold", anomalyResult);
        }

        [Fact]
        public async Task HostTelemetryTool_ReturnsValidSystemMetrics()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit();

            string telemetryJson = await registry.ExecuteAsync("get_system_telemetry", "");
            Assert.Contains("logicalProcessors", telemetryJson);
            Assert.Contains("workingSetMemoryMb", telemetryJson);
            Assert.Contains("gcAllocatedMemoryMb", telemetryJson);
        }

        [Fact]
        public async Task DataFrameQueryTool_FiltersFactoryRecords()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit();

            // Filter for CRITICAL_ERROR
            string json = await registry.ExecuteAsync("query_factory_records", "{\"tableName\":\"machines\",\"statusFilter\":\"CRITICAL_ERROR\"}");
            using var doc = JsonDocument.Parse(json);
            int matched = doc.RootElement.GetProperty("totalMatched").GetInt32();
            Assert.Equal(1, matched);
            Assert.Contains("PRESS-03", json);
        }

        [Fact]
        public void Registry_ExportsCompleteJsonSchemaForGrammarDecoding()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit();

            string schemaJson = registry.GetToolsJsonSchema();
            Assert.StartsWith("[", schemaJson);
            Assert.EndsWith("]", schemaJson);

            // Verify parseable JSON
            using var doc = JsonDocument.Parse(schemaJson);
            Assert.True(doc.RootElement.GetArrayLength() >= 7);
        }
    }
}
