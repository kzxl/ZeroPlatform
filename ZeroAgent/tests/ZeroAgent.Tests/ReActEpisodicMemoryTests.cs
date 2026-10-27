using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Embedding;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Memory;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class ReActEpisodicMemoryTests
    {
        [Fact]
        public async Task ReActAgent_AutoRecordsSuccessfulEpisodes_AndRecallsInSubsequentExecutions()
        {
            var memory = new AgentEpisodicMemory(128);
            var embedder = new FastTextEmbedder(128);
            var registry = new AgentToolRegistry();

            string capturedPromptRun2 = string.Empty;

            var mockLlm1 = new DelegateLlmClient(_ => Task.FromResult("Final Answer: Đã kích hoạt bơm tuần hoàn P-101 thành công ở áp lực 4.2 bar."));
            var mockLlm2 = new DelegateLlmClient(prompt =>
            {
                capturedPromptRun2 = prompt;
                return Task.FromResult("Final Answer: Đã tham chiếu tiền lệ và hoàn tất.");
            });

            // Run 1: Agent solves task and auto-records episode
            var agent1 = new ReActAgent("PlantEngineer", "Specialist", registry, mockLlm1)
            {
                EpisodicMemory = memory,
                MemoryEmbedder = embedder,
                AutoRecordEpisodes = true
            };

            var context1 = new AgentContext("Kích hoạt bơm tuần hoàn P-101", maxSteps: 3);
            var response1 = await agent1.ExecuteAsync(context1);

            Assert.True(response1.Success);
            Assert.Equal(1, memory.Count);

            // Run 2: Agent receives similar task, recalls past experience in prompt
            var agent2 = new ReActAgent("PlantEngineer", "Specialist", registry, mockLlm2)
            {
                EpisodicMemory = memory,
                MemoryEmbedder = embedder,
                EpisodicRecallMinSimilarity = 0.20f
            };

            var context2 = new AgentContext("Kích hoạt bơm tuần hoàn P-101 kiểm tra", maxSteps: 3);
            var response2 = await agent2.ExecuteAsync(context2);

            Assert.True(response2.Success);
            Assert.NotEmpty(capturedPromptRun2);

            // Verified: The system prompt includes the recalled past episode
            Assert.Contains("Relevant past successful experiences", capturedPromptRun2);
            Assert.Contains("Kích hoạt bơm tuần hoàn P-101", capturedPromptRun2);
            Assert.Contains("áp lực 4.2 bar", capturedPromptRun2);
        }

        [Fact]
        public async Task ReActAgent_IgnoresEpisodicMemory_WhenSimilarityIsBelowThreshold()
        {
            var memory = new AgentEpisodicMemory(128);
            var embedder = new FastTextEmbedder(128);
            var registry = new AgentToolRegistry();

            // Seed experience about pumps
            memory.Remember("Goal: Kích hoạt bơm tuần hoàn P-101 => Solution: Đã mở van và bật bơm", embedder);

            string capturedPrompt = string.Empty;
            var mockLlm = new DelegateLlmClient(prompt =>
            {
                capturedPrompt = prompt;
                return Task.FromResult("Final Answer: Done");
            });

            var agent = new ReActAgent("PlantEngineer", "Specialist", registry, mockLlm)
            {
                EpisodicMemory = memory,
                MemoryEmbedder = embedder,
                EpisodicRecallMinSimilarity = 0.85f // High threshold
            };

            // Completely unrelated task
            var context = new AgentContext("Xem báo cáo tổng kết doanh số bán lẻ quý 3", maxSteps: 3);
            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            // Must NOT contain irrelevant pump memory
            Assert.DoesNotContain("Relevant past successful experiences", capturedPrompt);
            Assert.DoesNotContain("bơm tuần hoàn", capturedPrompt);
        }

        private sealed class DelegateLlmClient : ILlmClient
        {
            private readonly Func<string, Task<string>> _func;

            public DelegateLlmClient(Func<string, Task<string>> func)
            {
                _func = func;
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                return _func(prompt);
            }
        }
    }
}
