using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.TreeOfThought;
using ZeroAgent.Core.Reasoning.Verification;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Tests
{
    public class TreeOfThoughtTests
    {
        [Fact]
        public async Task TreeOfThoughtAgent_ExploresAndFindsSolution()
        {
            var registry = new AgentToolRegistry();
            registry.Register("CheckHydraulics", "Inspect hydraulic pressure.", (string arg) => "Pressure: 150 bar (Normal)");

            var mockLlm = new MockLlmClient();
            // Branch step 1: Tool call
            mockLlm.Enqueue("Thought: Check hydraulic system.\nAction: CheckHydraulics(LineA)");
            // Branch step 2: Final answer
            mockLlm.Enqueue("Thought: Pressure is normal.\nFinal Answer: Hydraulic pressure is stable at 150 bar.");

            var totAgent = new TreeOfThoughtAgent("RcaDoctor", "RCA Specialist", registry, mockLlm)
            {
                MaxTreeDepth = 3,
                BeamWidth = 2
            };

            var context = new AgentContext("Investigate pressure anomaly", maxSteps: 4);
            var response = await totAgent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("Hydraulic pressure is stable at 150 bar", response.Output);
        }

        [Fact]
        public async Task TreeOfThoughtAgent_BacktracksOnDeadEndBranch()
        {
            var registry = new AgentToolRegistry();
            registry.Register("ProbeA", "Probe subsystem A.", (string arg) => "Error: Subsystem A connection timed out.");
            registry.Register("ProbeB", "Probe subsystem B.", (string arg) => "Subsystem B report: Bearing degraded, 85 deg C.");

            var mockLlm = new MockLlmClient();
            // Attempt 1: Call ProbeA (fails, receives error observation -> penalized score < threshold -> pruned)
            mockLlm.Enqueue("Thought: Let me check subsystem A first.\nAction: ProbeA(1)");
            // Attempt 2: Alternative branch calls ProbeB (succeeds -> awarded high score)
            mockLlm.Enqueue("Thought: Subsystem A failed. Backtracking to explore subsystem B.\nAction: ProbeB(1)");
            // Final Answer from successful branch
            mockLlm.Enqueue("Thought: Identified the root cause in Subsystem B.\nFinal Answer: Root cause is bearing overheating at 85 C.");

            var totAgent = new TreeOfThoughtAgent("BacktrackDoctor", "RCA Doctor", registry, mockLlm)
            {
                MaxTreeDepth = 4,
                BeamWidth = 2,
                PruningThreshold = 0.35f
            };

            var context = new AgentContext("Find root cause of vibration", maxSteps: 5);
            var response = await totAgent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("Root cause is bearing overheating", response.Output);
        }

        [Fact]
        public async Task CognitiveEscalationBridge_RoutesToToTOnRootCauseInquiry()
        {
            var registry = new AgentToolRegistry();

            var reactLlm = new MockLlmClient();
            reactLlm.Enqueue("Final Answer: ReAct response.");
            var reactAgent = new ReActAgent("NormalReAct", "Assistant", registry, reactLlm);

            var totLlm = new MockLlmClient();
            totLlm.Enqueue("Final Answer: Tree-of-Thought RCA identified overheating valve.");
            var totAgent = new TreeOfThoughtAgent("ToTDoctor", "RCA Specialist", registry, totLlm);

            var bridge = new CognitiveEscalationBridge(reactAgent, totAgent);

            var session = new DialogueSession("sess_01");
            var wm = new WorkingMemory("sess_01");
            var profile = new UserProfile("operator1", "Lead Engineer", UserRole.Engineer);

            // Query containing root cause trigger -> routes to ToT
            var response = await bridge.EscalateAsync(session, wm, profile, "Phân tích nguyên nhân gốc rễ sự cố máy ép P-01");

            Assert.NotNull(response);
            Assert.Equal("COGNITIVE_DELIBERATION_TOT", response.IntentName);
            Assert.Contains("Tree-of-Thought RCA", response.Text);
        }
    }
}
