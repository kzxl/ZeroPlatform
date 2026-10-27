using System;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.Verification;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class StepVerifierTests
    {
        [Fact]
        public async Task StepVerifier_InterceptsUnknownTool_WithoutExecutingTool()
        {
            var registry = new AgentToolRegistry();
            registry.Register("ValidTool", "A valid tool.", (string arg) => "ValidResult");

            var mockLlm = new MockLlmClient();
            // Step 1: LLM hallucinates an unknown tool
            mockLlm.Enqueue("Thought: Let me call a hallucinated tool.\nAction: FakeHallucinatedTool(123)");
            // Step 2: LLM receives critic feedback and falls back to ValidTool
            mockLlm.Enqueue("Thought: The tool did not exist, let me use ValidTool.\nAction: ValidTool(123)");
            // Step 3: Final answer
            mockLlm.Enqueue("Thought: Done.\nFinal Answer: Task succeeded with ValidTool.");

            var agent = new ReActAgent("CriticAgent", "Verifier tester", registry, mockLlm);
            var context = new AgentContext("Test goal", maxSteps: 5);

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Equal("Task succeeded with ValidTool.", response.Output);

            // Verify that context history contains the critic rejection message
            var criticMsg = context.History.Find(m => m.Content.Contains("[VERIFICATION CRITIC REJECTION]"));
            Assert.NotNull(criticMsg);
            Assert.Contains("FakeHallucinatedTool", criticMsg.Content);
            Assert.Contains("does not exist in the active registry", criticMsg.Content);
        }

        [Fact]
        public async Task StepVerifier_InterceptsMalformedJsonArguments()
        {
            var registry = new AgentToolRegistry();
            registry.Register("JsonTool", "Takes JSON.", (string arg) => "Processed");

            var mockLlm = new MockLlmClient();
            // Step 1: Malformed JSON argument
            mockLlm.Enqueue("Thought: Call tool with broken JSON.\nAction: JsonTool({\"bad\": broken})");
            // Step 2: Correct JSON
            mockLlm.Enqueue("Thought: Fix JSON.\nAction: JsonTool({\"good\": 1})");
            // Step 3: Final Answer
            mockLlm.Enqueue("Thought: Done.\nFinal Answer: Fixed JSON successfully.");

            var agent = new ReActAgent("JsonVerifierAgent", "JSON tester", registry, mockLlm);
            var context = new AgentContext("Test JSON goal", maxSteps: 5);

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Equal("Fixed JSON successfully.", response.Output);

            var criticMsg = context.History.Find(m => m.Content.Contains("[VERIFICATION CRITIC REJECTION]"));
            Assert.NotNull(criticMsg);
            Assert.Contains("Malformed JSON arguments", criticMsg.Content);
        }

        [Fact]
        public async Task StepVerifier_CustomRule_EnforcesDomainPreconditions()
        {
            var registry = new AgentToolRegistry();
            registry.Register("DeleteRecord", "Deletes record.", (string id) => $"Deleted {id}");

            var mockLlm = new MockLlmClient();
            // Step 1: Attempt to delete record 999
            mockLlm.Enqueue("Thought: Delete record.\nAction: DeleteRecord(999)");
            // Step 2: LLM receives safety policy rejection and backs off
            mockLlm.Enqueue("Thought: Deletion not permitted.\nFinal Answer: Record 999 cannot be deleted due to policy.");

            var agent = new ReActAgent("PolicyAgent", "Policy tester", registry, mockLlm);

            // Register custom rule that forbids deleting record 999
            var verifier = new DefaultStepVerifier();
            verifier.AddRule((ctx, call, tools) =>
            {
                if (call.ToolName == "DeleteRecord" && call.ArgumentsJson.Contains("999"))
                {
                    return StepVerificationResult.Revise("Security policy forbids deleting system record 999.", "ForbiddenRecord");
                }
                return null;
            });
            agent.StepVerifier = verifier;

            var context = new AgentContext("Delete 999", maxSteps: 5);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("Record 999 cannot be deleted", response.Output);

            var criticMsg = context.History.Find(m => m.Content.Contains("[VERIFICATION CRITIC REJECTION]"));
            Assert.NotNull(criticMsg);
            Assert.Contains("Security policy forbids deleting system record 999", criticMsg.Content);
        }

        [Fact]
        public async Task StepVerifier_WhenDisabled_AllowsExecutionWithoutInterception()
        {
            var registry = new AgentToolRegistry();
            registry.Register("NormalTool", "Normal.", (string arg) => "OK");

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue("Action: NormalTool(hello)");
            mockLlm.Enqueue("Final Answer: Finished.");

            var agent = new ReActAgent("NoCriticAgent", "Tester", registry, mockLlm)
            {
                StepVerifier = null // Explicitly disabled
            };

            var context = new AgentContext("Run normal", maxSteps: 5);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Equal("Finished.", response.Output);
            Assert.DoesNotContain(context.History, m => m.Content.Contains("[VERIFICATION CRITIC REJECTION]"));
        }
    }
}
