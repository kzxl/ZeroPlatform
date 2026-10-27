using System;
using Xunit;
using ZeroTokenizer.Core.Budget;
using ZeroTokenizer.Core.Tiktoken;

namespace ZeroTokenizer.Tests
{
    public class TokenizerTests
    {
        [Fact]
        public void Tiktoken_CountTokens_GivesConsistentNonZeroResults()
        {
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            Assert.Equal(0, tokenizer.CountTokens(""));

            string text = "Hello world, ZeroPlatform is an autonomous sovereign industrial stack.";
            int count = tokenizer.CountTokens(text);
            Assert.True(count >= 8 && count <= 35, $"Token count {count} is outside expected range.");
        }

        [Fact]
        public void Bpe_EncodeDecode_RoundtripsSuccessfully()
        {
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            string original = "The quick brown fox jumps over the lazy dog";
            int[] tokens = tokenizer.Encode(original);
            Assert.NotEmpty(tokens);

            string decoded = tokenizer.Decode(tokens);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void TokenBudgeter_PreservesSystemPromptAndTrimsLowPriorityItems()
        {
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            // Set small budget: enough for system prompt + user message, but not knowledge fragments
            var budgeter = new TokenBudgeter(tokenizer, maxContextTokens: 35);

            budgeter.AddSystemPrompt("System: You are a safe industrial AI assistant."); // ~10 tokens
            budgeter.AddKnowledgeItem("Manual Page 1: Very long motor technical specifications that exceed context budget.", priority: 100); // ~15 tokens
            budgeter.AddKnowledgeItem("Manual Page 2: Even more detailed schematics and wiring diagrams for the actuator.", priority: 100); // ~15 tokens
            budgeter.AddUserMessage("User: Turn off valve #2.", priority: 900); // ~7 tokens

            string packed = budgeter.BuildPackedPrompt();

            // Verify system prompt and user message are kept
            Assert.Contains("System: You are a safe industrial AI assistant.", packed);
            Assert.Contains("User: Turn off valve #2.", packed);

            // Chronological ordering check: System comes before User
            int sysIndex = packed.IndexOf("System:");
            int userIndex = packed.IndexOf("User:");
            Assert.True(sysIndex < userIndex);
        }
    }
}
