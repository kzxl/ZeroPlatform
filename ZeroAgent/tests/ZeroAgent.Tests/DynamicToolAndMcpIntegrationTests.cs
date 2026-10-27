using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Dynamic;
using ZeroAgent.Tools.Dynamic.Dispatchers;
using ZeroAgent.Tools.Dynamic.Model;
using ZeroAgent.Tools.Dynamic.Provider;
using ZeroAgent.Tools.Erp;
using ZeroAgent.Tools.Mcp;

namespace ZeroAgent.Tests
{
    public sealed class DynamicToolAndMcpIntegrationTests
    {
        [Fact]
        public async Task JsonFileToolDefinitionProvider_LoadsManifestAndExecutes()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "zero_agent_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string jsonFile = Path.Combine(tempDir, "tools.manifest.json");

            try
            {
                var manifest = new ToolsManifestFile
                {
                    Version = "1.0.0",
                    Tools = new List<ToolDefinitionRecord>
                    {
                        new ToolDefinitionRecord
                        {
                            Name = "erp_lot_balance_dynamic",
                            Description = "Tra cứu số lượng tồn kho theo lô.",
                            Category = "Inventory",
                            ExecutionType = ToolExecutionType.CustomDelegate,
                            Parameters = new List<ToolParameterDefinition>
                            {
                                new ToolParameterDefinition { Name = "lot_no", Type = "string", IsRequired = true },
                                new ToolParameterDefinition { Name = "warehouse_code", Type = "string", IsRequired = true }
                            }
                        }
                    }
                };

                File.WriteAllText(jsonFile, JsonSerializer.Serialize(manifest));

                using var provider = new JsonFileToolDefinitionProvider(jsonFile, enableHotReload: false);
                var definitions = await provider.LoadDefinitionsAsync();

                Assert.Single(definitions);
                Assert.Equal("erp_lot_balance_dynamic", definitions[0].Name);

                var factory = new DynamicToolFactory();
                factory.RegisterDelegate("erp_lot_balance_dynamic", arg => "Tồn kho: 5,000 kg, Khả dụng: 4,000 kg");

                var registry = new AgentToolRegistry();
                var tool = factory.CreateTool(definitions[0]);
                registry.Register(tool);

                Assert.True(registry.Contains("erp_lot_balance_dynamic"));
                string result = await registry.ExecuteAsync("erp_lot_balance_dynamic", "{\"lot_no\": \"LOT-01\"}");
                Assert.Contains("5,000 kg", result);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task McpServerEngine_HandlesInitialize_ToolsList_ToolsCall()
        {
            var registry = new AgentToolRegistry();
            registry.Register(new AgentTool(
                "erp_inventory_lot_balance_query",
                "Tra cứu số lượng tồn kho theo lô.",
                "lot_no: string, warehouse_code: string",
                arg => Task.FromResult("Lô LOT-2026-PP43: Tồn 5,330 kg, Khả dụng 4,014 kg")));

            var mcpServer = new McpServerEngine(registry, "ERP.McpServer", "2.0.0");

            // 1. Initialize Handshake
            string initReq = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}";
            string initRes = await mcpServer.ProcessMessageAsync(initReq);
            using (var doc = JsonDocument.Parse(initRes))
            {
                Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
                Assert.Equal(1, doc.RootElement.GetProperty("id").GetInt32());
                var result = doc.RootElement.GetProperty("result");
                Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
                Assert.Equal("ERP.McpServer", result.GetProperty("serverInfo").GetProperty("name").GetString());
            }

            // 2. Tools/List
            string listReq = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}";
            string listRes = await mcpServer.ProcessMessageAsync(listReq);
            using (var doc = JsonDocument.Parse(listRes))
            {
                Assert.Equal(2, doc.RootElement.GetProperty("id").GetInt32());
                var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
                Assert.True(tools.GetArrayLength() >= 1);
                Assert.Equal("erp_inventory_lot_balance_query", tools[0].GetProperty("name").GetString());
            }

            // 3. Tools/Call
            string callReq = "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"erp_inventory_lot_balance_query\",\"arguments\":{\"lot_no\":\"LOT-2026-PP43\"}}}";
            string callRes = await mcpServer.ProcessMessageAsync(callReq);
            using (var doc = JsonDocument.Parse(callRes))
            {
                Assert.Equal(3, doc.RootElement.GetProperty("id").GetInt32());
                var resElem = doc.RootElement.GetProperty("result");
                Assert.False(resElem.GetProperty("isError").GetBoolean());
                var contentArr = resElem.GetProperty("content");
                Assert.Equal("text", contentArr[0].GetProperty("type").GetString());
                Assert.Contains("5,330 kg", contentArr[0].GetProperty("text").GetString());
            }

            // 4. Live Notification Test
            string notif = mcpServer.CreateToolsListChangedNotification();
            Assert.Contains("notifications/tools/list_changed", notif);
        }

        [Fact]
        public async Task McpRemoteToolBridge_WrapsDescriptorIntoAgentTool()
        {
            using var schemaDoc = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"order_code\":{\"type\":\"string\"}},\"required\":[\"order_code\"]}");
            var descriptor = new McpToolDescriptor
            {
                Name = "external_crm_order_query",
                Description = "Tra cứu đơn hàng CRM ngoài hệ thống.",
                InputSchema = schemaDoc.RootElement.Clone()
            };

            bool wasInvoked = false;
            var bridge = new McpRemoteToolBridge(descriptor, (name, arg) =>
            {
                wasInvoked = true;
                return Task.FromResult($"Đơn hàng {arg}: Đang giao");
            });

            var registry = new AgentToolRegistry();
            registry.Register(bridge);

            Assert.True(registry.Contains("external_crm_order_query"));
            string result = await registry.ExecuteAsync("external_crm_order_query", "ORD-999");

            Assert.True(wasInvoked);
            Assert.Contains("Đang giao", result);
        }

        [Fact]
        public async Task DynamicSqlTool_SafetyGate_BlocksDestructiveStatements()
        {
            var def = new ToolDefinitionRecord
            {
                Name = "unsafe_sql_tool",
                Description = "Cố gắng chạy lệnh sửa đổi dữ liệu.",
                ExecutionType = ToolExecutionType.ParameterizedSql,
                Execution = new ToolExecutionConfig
                {
                    SqlQuery = "DROP TABLE Sys_Users"
                }
            };

            var sqlTool = new DynamicSqlTool(def, () => throw new InvalidOperationException("Should not open connection"));
            string result = await sqlTool.ExecuteAsync("{}");

            Assert.Contains("Safety Policy Violation", result);
            Assert.Contains("strictly forbids data modification", result);
        }

        [Fact]
        public async Task EnterpriseErpToolkit_QueriesLotBalanceAndOperations()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            // 1. Query Lot Balance for LOT-2026-PP01
            string lotRes = await registry.ExecuteAsync("erp_inventory_lot_balance_query", "{\"lot_no\": \"LOT-2026-PP01\", \"warehouse_code\": \"WH-MAT-01\"}");
            Assert.Contains("LOT-2026-PP01", lotRes);
            Assert.Contains("3300", lotRes);
            Assert.Contains("WH-MAT-01", lotRes);

            // 2. Query Production Plan
            string planRes = await registry.ExecuteAsync("erp_production_plan_query", "{\"process_stage\": \"Extrusion\", \"line_code\": \"EXT-01\"}");
            Assert.Contains("Extrusion", planRes);
            Assert.Contains("WO-2026-0412", planRes);
            Assert.Contains("9600", planRes);

            // 3. Query Sales Order
            string orderRes = await registry.ExecuteAsync("erp_sales_order_query", "{\"order_code\": \"SO-2026-0881\", \"agency\": \"HQ\"}");
            Assert.Contains("SO-2026-0881", orderRes);
            Assert.Contains("Global Packaging Solutions Ltd.", orderRes);
        }
    }
}
