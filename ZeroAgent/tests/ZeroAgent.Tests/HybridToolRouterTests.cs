using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Embedding;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class HybridToolRouterTests
    {
        [Fact]
        public void ToolSemanticRouter_HybridVectorRouting_SelectsBestTools()
        {
            var registry = new AgentToolRegistry();
            registry.Register("financial_report_generator", "Generates monthly financial balance sheets and quarterly revenue statements.", (string _) => "report");
            registry.Register("camera_rtsp_stream", "Connects to security CCTV RTSP video streams and camera feeds.", (string _) => "stream");
            registry.Register("motor_vibration_analyzer", "Analyzes rotational vibration sensor frequencies and bearing health.", (string _) => "vibration");
            registry.Register("database_sql_executor", "Executes raw SQL query on relational PostgreSQL or SQL Server databases.", (string _) => "sql");

            var router = new ToolSemanticRouter(128);
            // Register tools with multilingual natural language triggers
            router.RegisterTool(registry.Get("financial_report_generator")!, new[] { "báo cáo", "doanh thu", "tài chính", "balance sheet", "revenue" });
            router.RegisterTool(registry.Get("camera_rtsp_stream")!, new[] { "cctv", "camera", "video stream", "giám sát" });
            router.RegisterTool(registry.Get("motor_vibration_analyzer")!, new[] { "động cơ", "rung động", "vibration", "bearing" });
            router.RegisterTool(registry.Get("database_sql_executor")!, new[] { "cơ sở dữ liệu", "sql", "truy vấn", "database query" });

            Assert.Equal(4, router.Count);
            Assert.Equal(128, router.Embedder.Dimension);

            // Test 1: Query about camera surveillance
            var cameraMatches = router.Route("Show me the cctv video feed", topK: 1);
            Assert.NotEmpty(cameraMatches);
            Assert.Equal("camera_rtsp_stream", cameraMatches[0].Tool.Name);
            Assert.True(cameraMatches[0].Score > 0.3f);

            // Test 2: Query about finance & revenue in Vietnamese
            var finMatches = router.Route("Kiểm tra báo cáo doanh thu tài chính quý", topK: 1);
            Assert.NotEmpty(finMatches);
            Assert.Equal("financial_report_generator", finMatches[0].Tool.Name);

            // Test 3: Query about SQL query execution
            var sqlMatches = router.Route("Run database SQL queries", topK: 1);
            Assert.NotEmpty(sqlMatches);
            Assert.Equal("database_sql_executor", sqlMatches[0].Tool.Name);
        }

        [Fact]
        public void ToolSemanticRouter_SupportsCustomEmbedder()
        {
            var customEmbedder = new FastTextEmbedder(64);
            var router = new ToolSemanticRouter(customEmbedder);

            Assert.Equal(64, router.Embedder.Dimension);

            var tool = new AgentTool("diagnostics_tool", "Hardware diagnostic scan.", "string arg", arg => Task.FromResult("ok"));
            router.RegisterTool(tool, new[] { "scan", "check" });

            var matches = router.Route("scan hardware", topK: 1);
            Assert.NotEmpty(matches);
            Assert.Equal("diagnostics_tool", matches[0].Tool.Name);
        }

        [Fact]
        public async Task ReActAgent_WithToolRouter_PrunesToolDefinitionsInPrompt()
        {
            var registry = new AgentToolRegistry();
            // Register 10 distinct tools
            registry.Register("finance_tool", "Finance and revenue balance reporting.", (string _) => "fin");
            registry.Register("accounting_tool", "General ledger and tax calculations.", (string _) => "acc");
            registry.Register("cctv_tool", "Camera surveillance stream.", (string _) => "cctv");
            registry.Register("robot_tool", "Robotic arm forward kinematics.", (string _) => "robot");
            registry.Register("hydraulic_tool", "Hydraulic fluid pressure telemetry.", (string _) => "hydraulic");
            registry.Register("conveyor_tool", "Conveyor belt velocity control.", (string _) => "conveyor");
            registry.Register("welding_tool", "Laser welding arc temperature.", (string _) => "welding");
            registry.Register("hvac_tool", "Cleanroom climate and airflow control.", (string _) => "hvac");
            registry.Register("agv_tool", "Autonomous guided vehicle dispatch.", (string _) => "agv");
            registry.Register("printer_tool", "Industrial barcode label printer.", (string _) => "printer");

            var router = new ToolSemanticRouter(128);
            // Register with domain triggers
            router.RegisterTool(registry.Get("finance_tool")!, new[] { "tài chính", "doanh thu", "số dư", "finance" });
            router.RegisterTool(registry.Get("accounting_tool")!, new[] { "kế toán", "sổ cái", "thuế", "accounting", "ledger" });
            router.RegisterTool(registry.Get("cctv_tool")!, new[] { "camera", "cctv" });
            router.RegisterTool(registry.Get("robot_tool")!, new[] { "robot", "cánh tay" });
            router.RegisterTool(registry.Get("hydraulic_tool")!, new[] { "thủy lực", "áp suất" });
            router.RegisterTool(registry.Get("conveyor_tool")!, new[] { "băng tải", "conveyor" });
            router.RegisterTool(registry.Get("welding_tool")!, new[] { "hàn laser", "welding" });
            router.RegisterTool(registry.Get("hvac_tool")!, new[] { "điều hòa", "hvac" });
            router.RegisterTool(registry.Get("agv_tool")!, new[] { "xe tự hành", "agv" });
            router.RegisterTool(registry.Get("printer_tool")!, new[] { "máy in", "printer" });

            string capturedPrompt = string.Empty;
            var mockLlm = new CapturingLlmClient(prompt =>
            {
                capturedPrompt = prompt;
                return "Final Answer: Pruned successfully";
            });

            var agent = new ReActAgent("FinanceSpecialist", "Accountant", registry, mockLlm)
            {
                ToolRouter = router,
                MaxPromptTools = 2 // Prune 10 tools down to only the 2 most relevant tools
            };

            var context = new AgentContext("Báo cáo số dư tài chính và sổ cái kế toán", maxSteps: 3);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.NotEmpty(capturedPrompt);

            // Verified: Only finance and accounting tools should be injected in prompt
            Assert.Contains("finance_tool", capturedPrompt);
            Assert.Contains("accounting_tool", capturedPrompt);

            // Non-relevant tools must NOT be injected into the prompt
            Assert.DoesNotContain("cctv_tool", capturedPrompt);
            Assert.DoesNotContain("robot_tool", capturedPrompt);
            Assert.DoesNotContain("hydraulic_tool", capturedPrompt);
            Assert.DoesNotContain("welding_tool", capturedPrompt);
        }

        private sealed class CapturingLlmClient : ILlmClient
        {
            private readonly Func<string, string> _handler;

            public CapturingLlmClient(Func<string, string> handler)
            {
                _handler = handler;
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_handler(prompt));
            }
        }
    }
}
