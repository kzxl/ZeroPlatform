using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class PromptRecordingLlmClient : ILlmClient
    {
        public List<string> RecordedPrompts { get; } = new List<string>();
        private readonly Queue<string> _responses = new Queue<string>();

        public void Enqueue(string response) => _responses.Enqueue(response);

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            RecordedPrompts.Add(prompt);
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }
            return Task.FromResult("Final Answer: Done.");
        }
    }

    public class ChainOfDraftTests
    {
        [Fact]
        public async Task ChainOfDraft_InjectsTelegraphicInstructionIntoPrompt()
        {
            var registry = new AgentToolRegistry();
            registry.Register("GetMetrics", "Fetches metrics.", (string arg) => "42");

            var llm = new PromptRecordingLlmClient();
            llm.Enqueue("Final Answer: 42");

            var agent = new ReActAgent("DraftAgent", "Speed Optimizer", registry, llm)
                .WithReasoningStyle(ReasoningStyle.ChainOfDraft);

            var context = new AgentContext("Get quick metrics", maxSteps: 3);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.NotEmpty(llm.RecordedPrompts);
            string prompt = llm.RecordedPrompts[0];

            Assert.Contains("[REASONING STYLE: CHAIN-OF-DRAFT]", prompt);
            Assert.Contains("under 30 words", prompt);
            Assert.EndsWith("Thought: ", prompt);
        }

        [Fact]
        public async Task SilentAction_ConfiguresDirectActionPrompt()
        {
            var registry = new AgentToolRegistry();
            registry.Register("GetMetrics", "Fetches metrics.", (string arg) => "42");

            var llm = new PromptRecordingLlmClient();
            llm.Enqueue("Final Answer: 42");

            var agent = new ReActAgent("DirectAgent", "Direct Operator", registry, llm)
                .WithReasoningStyle(ReasoningStyle.SilentAction);

            var context = new AgentContext("Execute fast", maxSteps: 3);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.NotEmpty(llm.RecordedPrompts);
            string prompt = llm.RecordedPrompts[0];

            Assert.Contains("[REASONING STYLE: DIRECT ACTION]", prompt);
            Assert.EndsWith("Action: ", prompt);
        }

        [Fact]
        public async Task DetailedCoT_PreservesStandardThoughtPrompt()
        {
            var registry = new AgentToolRegistry();
            var llm = new PromptRecordingLlmClient();
            llm.Enqueue("Final Answer: Standard");

            var agent = new ReActAgent("StandardAgent", "Standard Assistant", registry, llm);
            Assert.Equal(ReasoningStyle.DetailedCoT, agent.ReasoningStyle);

            var context = new AgentContext("Standard test", maxSteps: 3);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.NotEmpty(llm.RecordedPrompts);
            string prompt = llm.RecordedPrompts[0];

            Assert.DoesNotContain("[REASONING STYLE: CHAIN-OF-DRAFT]", prompt);
            Assert.DoesNotContain("[REASONING STYLE: DIRECT ACTION]", prompt);
            Assert.Contains("Thought: you should always think about what to do", prompt);
            Assert.EndsWith("Thought: ", prompt);
        }
    }
}
