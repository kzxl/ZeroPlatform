using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Swarm;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class HierarchicalSwarmTests
    {
        [Fact]
        public void AgentBlackboard_StoresVariablesAndLogs_GeneratesPromptSummary()
        {
            var blackboard = new AgentBlackboard();
            blackboard.Set("pressure_threshold", 45.0);
            blackboard.Set("alert_status", "YELLOW_WARNING");

            blackboard.PostFinding("SensorAgent", "Pressure spiked to 48.2 PSI at 14:02.");
            blackboard.PostFinding("ValveAgent", "Adjusted bypass valve to 30%.");

            Assert.Equal(2, blackboard.StateCount);
            Assert.Equal(2, blackboard.LogCount);

            Assert.True(blackboard.TryGet<double>("pressure_threshold", out var threshold));
            Assert.Equal(45.0, threshold);

            string summary = blackboard.ToPromptSummary();
            Assert.Contains("=== Shared Swarm Blackboard Context ===", summary);
            Assert.Contains("pressure_threshold: 45", summary);
            Assert.Contains("alert_status: YELLOW_WARNING", summary);
            Assert.Contains("SensorAgent", summary);
            Assert.Contains("ValveAgent", summary);

            blackboard.Clear();
            Assert.Equal(0, blackboard.StateCount);
            Assert.Equal(0, blackboard.LogCount);
        }

        [Fact]
        public async Task AgentSwarm_Handoff_TransfersContextViaBlackboard()
        {
            var swarm = new AgentSwarm();
            var regA = new AgentToolRegistry();
            var regB = new AgentToolRegistry();

            var llmB = new MockSwarmLlmClient(prompt => "Final Answer: Đã nhận dữ liệu bàn giao và reset relay thành công.");

            var agentA = new ReActAgent("AgentA", "Analyzer", regA, new MockSwarmLlmClient(_ => "Final Answer: ok"));
            var agentB = new ReActAgent("AgentB", "Remediator", regB, llmB);

            swarm.RegisterAgent(agentA);
            swarm.RegisterAgent(agentB);

            var handoffResult = await swarm.HandoffAsync(
                "AgentA",
                "AgentB",
                "Khắc phục sự cố relay số 4",
                transferData: "Relay 4 bị nhảy áp lúc 10:15");

            Assert.True(handoffResult.Success);
            Assert.Contains("reset relay thành công", handoffResult.Output);

            // Blackboard should contain handoff logs
            var logs = swarm.Blackboard.GetLogs();
            Assert.NotEmpty(logs);
            Assert.Contains(logs, l => l.Action.Contains("Handoff initiated"));
        }

        [Fact]
        public async Task SupervisorAgent_DecomposesAndSynthesizes_MultiAgentMission()
        {
            var swarm = new AgentSwarm();

            // 1. Diagnostics Specialist
            var diagTools = new AgentToolRegistry();
            diagTools.Register("check_vibration", "Check bearing vibration.", (string _) => "Vibration: 7.8 mm/s (Warning)");
            var diagLlm = new MockSwarmLlmClient(prompt =>
            {
                if (prompt.Contains("Action:")) return "Final Answer: Bạc đạn máy CNC-01 bị rung lắc 7.8 mm/s, cần thay thế vòng bi.";
                return "Action: check_vibration(CNC-01)";
            });
            var diagAgent = new ReActAgent("DiagnosticsSpecialist", "Vibration Analyst", diagTools, diagLlm);
            swarm.RegisterAgent(diagAgent);

            // 2. Financial Specialist
            var finTools = new AgentToolRegistry();
            finTools.Register("get_part_cost", "Fetch spare part catalog price.", (string _) => "Cost: 15,000,000 VND");
            var finLlm = new MockSwarmLlmClient(prompt =>
            {
                if (prompt.Contains("Action:")) return "Final Answer: Giá bạc đạn chính hãng ISO VG 68 là 15,000,000 VND.";
                return "Action: get_part_cost(bearing-cnc-01)";
            });
            var finAgent = new ReActAgent("FinancialSpecialist", "Cost Estimator", finTools, finLlm);
            swarm.RegisterAgent(finAgent);

            // 3. Supervisor LLM
            var supervisorLlm = new MockSupervisorLlmClient(
                planOutput: "Delegate: [DiagnosticsSpecialist] | Task: Kiểm tra rung động bạc đạn CNC-01\n" +
                            "Delegate: [FinancialSpecialist] | Task: Dự toán chi phí thay bạc đạn CNC-01",
                synthesisOutput: "Báo cáo Tổng Hợp Giám Sát: Thiết bị CNC-01 có dấu hiệu rung lắc bất thường 7.8 mm/s. Khuyến nghị thay thế vòng bi với tổng chi phí ước tính là 15,000,000 VND.");

            var supervisor = new SupervisorAgent("PlantSupervisor", "Technical Lead", swarm, supervisorLlm);

            var result = await supervisor.ExecuteHierarchicalTaskAsync("Kiểm tra bạc đạn máy CNC-01 và tính toán chi phí linh kiện");

            Assert.True(result.Success);
            Assert.Contains("Báo cáo Tổng Hợp Giám Sát", result.Output);
            Assert.Contains("7.8 mm/s", result.Output);
            Assert.Contains("15,000,000 VND", result.Output);

            // Blackboard should record all findings
            var logs = swarm.Blackboard.GetLogs();
            Assert.Contains(logs, l => l.AgentName == "DiagnosticsSpecialist");
            Assert.Contains(logs, l => l.AgentName == "FinancialSpecialist");
        }

        private sealed class MockSwarmLlmClient : ILlmClient
        {
            private readonly Func<string, string> _func;

            public MockSwarmLlmClient(Func<string, string> func)
            {
                _func = func;
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_func(prompt));
            }
        }

        private sealed class MockSupervisorLlmClient : ILlmClient
        {
            private readonly string _planOutput;
            private readonly string _synthesisOutput;

            public MockSupervisorLlmClient(string planOutput, string synthesisOutput)
            {
                _planOutput = planOutput;
                _synthesisOutput = synthesisOutput;
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                if (prompt.Contains("Decompose the following complex objective"))
                {
                    return Task.FromResult(_planOutput);
                }
                return Task.FromResult(_synthesisOutput);
            }
        }
    }
}
