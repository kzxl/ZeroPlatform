using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Memory;
using ZeroAgent.Core.Swarm;
using ZeroAgent.Core.Tools;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class MockLlmClient : ILlmClient
    {
        private readonly Queue<string> _responses = new Queue<string>();

        public void Enqueue(string response) => _responses.Enqueue(response);

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }
            return Task.FromResult("Final Answer: Default mock response.");
        }
    }

    public class AgentTests
    {
        [Fact]
        public async Task AgentToolRegistry_RegistersAndExecutesToolsSuccessfully()
        {
            var registry = new AgentToolRegistry();

            registry.Register("Add", "Adds two numbers.", (string arg) =>
            {
                var parts = arg.Split(',');
                int a = int.Parse(parts[0].Trim());
                int b = int.Parse(parts[1].Trim());
                return (a + b).ToString();
            });

            Assert.Equal(1, registry.Count);
            string result = await registry.ExecuteAsync("Add", "15, 27");
            Assert.Equal("42", result);

            string missing = await registry.ExecuteAsync("UnknownTool", "");
            Assert.Contains("Error", missing);
        }

        [Fact]
        public async Task ReActAgent_ExecutesMultiStepThoughtActionLoop()
        {
            var registry = new AgentToolRegistry();
            registry.Register("ReadSensor", "Reads telemetry sensor.", (string sensorName) =>
            {
                return $"Sensor {sensorName} reading: 85.4 C";
            });

            var mockLlm = new MockLlmClient();
            // Step 1: Agent decides to call tool
            mockLlm.Enqueue("I should check the temperature sensor first.\nAction: ReadSensor(Temp1)");
            // Step 2: Agent observes reading and gives final answer
            mockLlm.Enqueue("The temperature is 85.4 C, which is elevated.\nFinal Answer: Temperature is high at 85.4 C.");

            var agent = new ReActAgent("DiagnosticsAgent", "Thermal monitoring specialist", registry, mockLlm);
            var context = new AgentContext("Check temperature on Temp1", maxSteps: 5);

            AgentResponse response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Equal("Temperature is high at 85.4 C.", response.Output);
            Assert.Equal(2, response.TotalSteps);
        }

        [Fact]
        public void AgentEpisodicMemory_StoresAndRecallsMemoriesViaZeroVector()
        {
            int dim = 16;
            var memory = new AgentEpisodicMemory(dim, useHnsw: false);

            var rand = new Random(42);
            var vecA = new float[dim];
            var vecB = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                vecA[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                vecB[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
            }
            VectorMetrics.NormalizeL2(vecA);
            VectorMetrics.NormalizeL2(vecB);

            memory.Remember("Motor #1 manual: replace grease every 5,000 hours.", vecA);
            memory.Remember("Hydraulic pump manual: replace seals at 10,000 hours.", vecB);

            Assert.Equal(2, memory.Count);

            // Query with vecA (should match Motor #1 best)
            var matches = memory.Recall(vecA, topK: 1);
            Assert.NotEmpty(matches);
            Assert.Contains("Motor #1 manual", matches[0].Memory);
            Assert.True(matches[0].SimilarityScore > 0.99f);
        }

        [Fact]
        public async Task AgentSwarm_DelegatesToSpecializedAgent()
        {
            var swarm = new AgentSwarm();
            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue("Final Answer: Motor #2 vibration is normal at 1.2 mm/s.");

            var agent = new ReActAgent("VibrationSpecialist", "Acoustic & vibration expert", new AgentToolRegistry(), mockLlm);
            swarm.RegisterAgent(agent);

            Assert.Equal(1, swarm.AgentCount);

            var context = new AgentContext("Inspect Motor #2 vibration.");
            AgentResponse response = await swarm.DelegateAsync("VibrationSpecialist", context);

            Assert.True(response.Success);
            Assert.Contains("1.2 mm/s", response.Output);
        }

        [Fact]
        public async Task AgentToolRegistry_EnforcesHitlApprovalPolicy()
        {
            var registry = new AgentToolRegistry();
            var dangerousTool = new AgentTool(
                "StopTurbine",
                "Emergency shutdown of cooling turbine.",
                "int turbineId",
                arg => Task.FromResult($"Turbine {arg} halted.")
            ).WithApproval(true);

            registry.Register(dangerousTool);

            // 1. Without ApprovalHandler -> Safety policy violation
            string result1 = await registry.ExecuteAsync("StopTurbine", "4");
            Assert.Contains("Safety Policy Violation", result1);

            // 2. With ApprovalHandler returning false -> Action rejected
            registry.ApprovalHandler = (tool, arg) => Task.FromResult(false);
            string result2 = await registry.ExecuteAsync("StopTurbine", "4");
            Assert.Contains("Action rejected", result2);

            // 3. With ApprovalHandler returning true -> Action executed
            registry.ApprovalHandler = (tool, arg) => Task.FromResult(true);
            string result3 = await registry.ExecuteAsync("StopTurbine", "4");
            Assert.Equal("Turbine 4 halted.", result3);
        }

        [Fact]
        public void AgentToolRegistry_GeneratesJsonSchemaAndToolPrompt()
        {
            var registry = new AgentToolRegistry();
            var schema = new ZeroPrompt.Core.Grammar.JsonSchemaConstraint("ReadRegister")
                .AddProperty("address", ZeroPrompt.Core.Grammar.SchemaPropertyType.Number, required: true)
                .AddProperty("count", ZeroPrompt.Core.Grammar.SchemaPropertyType.Number, required: false);

            var tool = new AgentTool(
                "ReadPlcRegister",
                "Reads 16-bit register from industrial PLC.",
                "address, count",
                arg => Task.FromResult("OK"),
                schema: schema,
                requiresApproval: true
            );

            registry.Register(tool);

            string prompt = registry.GetToolsPrompt();
            Assert.Contains("ReadPlcRegister", prompt);
            Assert.Contains("[REQUIRES OPERATOR APPROVAL]", prompt);

            string jsonSchema = registry.GetToolsJsonSchema();
            Assert.Contains("\"name\":\"ReadPlcRegister\"", jsonSchema);
            Assert.Contains("\"address\":{\"type\":\"number\"}", jsonSchema);
            Assert.Contains("\"required\":[\"address\"]", jsonSchema);
        }

        [Fact]
        public void ReActLoopGuard_DetectsConsecutiveDuplicatesAndCircuitBreaks()
        {
            var guard = new ReActLoopGuard { MaxConsecutiveDuplicates = 2, MaxTotalDuplicates = 3 };

            // Call 1: First time -> Allow
            var status1 = guard.EvaluateCall("ReadSensor", "Temp1", out string fb1);
            Assert.Equal(LoopGuardStatus.Allow, status1);
            Assert.Empty(fb1);

            // Call 2: Duplicate consecutive -> CircuitBreak (since maxConsecutive=2)
            var status2 = guard.EvaluateCall("ReadSensor", "Temp1", out string fb2);
            Assert.Equal(LoopGuardStatus.CircuitBreak, status2);
            Assert.Contains("CIRCUIT BREAKER TRIGGERED", fb2);
        }

        [Fact]
        public async Task ReActAgent_LoopGuard_PreventsInfiniteDuplicateToolExecution()
        {
            int toolExecutionCount = 0;
            var registry = new AgentToolRegistry();
            registry.Register("FetchTelemetry", "Fetches telemetry.", (string arg) =>
            {
                toolExecutionCount++;
                return "Val: 100";
            });

            var mockLlm = new MockLlmClient();
            // Step 1: Tool call
            mockLlm.Enqueue("Action: FetchTelemetry(Pressure1)");
            // Step 2: Exact same tool call (agent hallucinates/repeats)
            mockLlm.Enqueue("Action: FetchTelemetry(Pressure1)");
            // Step 3: Agent sees circuit breaker and provides answer
            mockLlm.Enqueue("I see the circuit breaker. Final Answer: Pressure is 100.");

            var agent = new ReActAgent("GuardTestAgent", "Tester", registry, mockLlm);
            var context = new AgentContext("Check pressure", maxSteps: 5);

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Equal("Pressure is 100.", response.Output);
            // Tool should only execute ONCE because the second identical call is blocked by circuit breaker
            Assert.Equal(1, toolExecutionCount);
        }

        [Fact]
        public async Task ReActAgent_InjectsSelfCorrectionDirectiveOnToolError()
        {
            var registry = new AgentToolRegistry();
            registry.Register("FaultyTool", "A tool that throws.", new Func<string, string>((string arg) =>
            {
                throw new InvalidOperationException("Sensor hardware offline");
            }));

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue("Action: FaultyTool(Pump1)");
            mockLlm.Enqueue("Final Answer: Sensor was offline so I halted.");

            var agent = new ReActAgent("ReflectAgent", "Tester", registry, mockLlm);
            var context = new AgentContext("Diagnose pump", maxSteps: 5);

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            // Verify context history contains the self-correction directive
            var toolMessage = context.History.Find(m => m.Role == AgentRole.Tool);
            Assert.NotNull(toolMessage);
            Assert.Contains("SELF-CORRECTION DIRECTIVE", toolMessage.Content);
            Assert.Contains("Sensor hardware offline", toolMessage.Content);
        }

        [Fact]
        public async Task ReActAgent_EnforcesContextBudgetPruning()
        {
            var registry = new AgentToolRegistry();
            registry.Register("QuickTool", "Sample tool.", (string arg) => "Sample Result");

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue("Action: QuickTool(1)");
            mockLlm.Enqueue("Final Answer: Done");

            var agent = new ReActAgent("BudgetAgent", "Tester", registry, mockLlm)
            {
                MaxContextTokens = 50 // Very small budget to force pruning
            };

            var context = new AgentContext("Small budget goal", maxSteps: 5);
            // Pre-seed context with large historical messages
            for (int i = 0; i < 15; i++)
            {
                context.AddMessage(AgentRole.User, $"This is a lengthy prior conversation turn {i} that consumes tokens.");
                context.AddMessage(AgentRole.Assistant, $"This is a lengthy prior assistant response {i} that also consumes tokens.");
            }

            int initialTokens = agent.BudgetManager.EstimateContextTokens(context);
            Assert.True(initialTokens > 100);

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            int finalTokens = agent.BudgetManager.EstimateContextTokens(context);
            // Tokens should have been pruned to stay within bounded budget
            Assert.True(finalTokens <= 50 || context.History.Count < 10);
        }
    }
}
