using System;
using Xunit;
using ZeroPrompt.Core.Grammar;

namespace ZeroPrompt.Tests
{
    public class JsonGrammarTests
    {
        [Fact]
        public void JsonGrammarState_Should_Accept_Valid_Json()
        {
            var grammar = new JsonGrammarState();
            string json = "{\"tool\": \"read_plc\", \"register\": 102}";

            for (int i = 0; i < json.Length; i++)
            {
                bool accepted = grammar.TryConsume(json[i]);
                Assert.True(accepted, $"Failed to consume char '{json[i]}' at index {i}");
            }

            Assert.True(grammar.IsCompleted);
        }

        [Fact]
        public void JsonGrammarState_Should_Reject_Malformed_Json()
        {
            var grammar = new JsonGrammarState();
            string malformed = "{unquoted_key: 123}";

            bool reachedError = false;
            for (int i = 0; i < malformed.Length; i++)
            {
                if (!grammar.TryConsume(malformed[i]))
                {
                    reachedError = true;
                    break;
                }
            }

            Assert.True(reachedError || grammar.IsInError);
        }

        [Fact]
        public void JsonGrammarState_Lookahead_Should_Predict_Acceptance()
        {
            var grammar = new JsonGrammarState();
            grammar.TryConsume('{');

            Assert.True(grammar.CanAcceptNext("\"name\"".AsSpan()));
            Assert.False(grammar.CanAcceptNext("123".AsSpan()));
        }
    }
}
