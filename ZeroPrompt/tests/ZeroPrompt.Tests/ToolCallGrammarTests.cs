using System;
using System.Collections.Generic;
using Xunit;
using ZeroPrompt.Core.Grammar;
using ZeroTokenizer.Core.Tiktoken;

namespace ZeroPrompt.Tests
{
    public class ToolCallGrammarTests
    {
        [Fact]
        public void SchemaConstrainedJsonGrammar_Should_Enforce_Property_Whitelist_And_Types()
        {
            var schema = new JsonSchemaConstraint("so_query")
                .AddProperty("order_id", SchemaPropertyType.String, required: true)
                .AddProperty("limit", SchemaPropertyType.Number, required: false);

            var grammar = new SchemaConstrainedJsonGrammar(schema);
            string validJson = "{\"order_id\": \"SO-2026-001\", \"limit\": 10}";

            for (int i = 0; i < validJson.Length; i++)
            {
                bool ok = grammar.TryConsume(validJson[i]);
                Assert.True(ok, $"Failed to consume char '{validJson[i]}' at index {i}");
            }

            Assert.True(grammar.IsCompleted);
        }

        [Fact]
        public void SchemaConstrainedJsonGrammar_Should_Reject_Unregistered_Property()
        {
            var schema = new JsonSchemaConstraint("so_query")
                .AddProperty("order_id", SchemaPropertyType.String, required: true);

            var grammar = new SchemaConstrainedJsonGrammar(schema);
            string invalidJson = "{\"unknown_prop\": \"hack\"}";

            bool reachedError = false;
            for (int i = 0; i < invalidJson.Length; i++)
            {
                if (!grammar.TryConsume(invalidJson[i]))
                {
                    reachedError = true;
                    break;
                }
            }

            Assert.True(reachedError || grammar.IsInError);
        }

        [Fact]
        public void SchemaConstrainedJsonGrammar_Should_Reject_Early_Close_When_Required_Property_Missing()
        {
            var schema = new JsonSchemaConstraint("so_query")
                .AddProperty("order_id", SchemaPropertyType.String, required: true)
                .AddProperty("customer_code", SchemaPropertyType.String, required: true);

            var grammar = new SchemaConstrainedJsonGrammar(schema);
            // Only order_id is present, customer_code is missing!
            string incompleteJson = "{\"order_id\": \"SO-01\"}";

            bool rejected = false;
            for (int i = 0; i < incompleteJson.Length; i++)
            {
                if (!grammar.TryConsume(incompleteJson[i]))
                {
                    rejected = true;
                    break;
                }
            }

            Assert.True(rejected || grammar.IsInError || !grammar.IsCompleted);
        }

        [Fact]
        public void ToolCallGrammarState_Should_Accept_Valid_Tool_Call()
        {
            var schemas = new Dictionary<string, JsonSchemaConstraint>
            {
                ["mds_db_so_query"] = new JsonSchemaConstraint("mds_db_so_query")
                    .AddProperty("order_id", SchemaPropertyType.String, required: true),
                ["mds_db_lot_balance_query"] = new JsonSchemaConstraint("mds_db_lot_balance_query")
                    .AddProperty("lot_no", SchemaPropertyType.String, required: true)
            };

            var state = new ToolCallGrammarState(schemas, requireStartTag: true);
            string toolCallStr = "<tool_call>{\"tool\": \"mds_db_so_query\", \"parameters\": {\"order_id\": \"SO-2026-99\"}}</tool_call>";

            for (int i = 0; i < toolCallStr.Length; i++)
            {
                bool ok = state.TryConsume(toolCallStr[i]);
                Assert.True(ok, $"Failed to consume char '{toolCallStr[i]}' at index {i}");
            }

            Assert.True(state.IsCompleted);
            Assert.Equal("mds_db_so_query", state.SelectedToolName);
        }

        [Fact]
        public void ToolCallGrammarState_Should_Reject_Unregistered_Tool_Name()
        {
            var schemas = new Dictionary<string, JsonSchemaConstraint>
            {
                ["mds_db_so_query"] = new JsonSchemaConstraint("mds_db_so_query")
                    .AddProperty("order_id", SchemaPropertyType.String, required: true)
            };

            var state = new ToolCallGrammarState(schemas, requireStartTag: true);
            string maliciousCall = "<tool_call>{\"tool\": \"unregistered_malicious_tool\"";

            bool reachedError = false;
            for (int i = 0; i < maliciousCall.Length; i++)
            {
                if (!state.TryConsume(maliciousCall[i]))
                {
                    reachedError = true;
                    break;
                }
            }

            Assert.True(reachedError || state.IsInError);
        }

        [Fact]
        public void ToolCallGrammarLogitProcessor_Should_Mask_Invalid_Tokens_Inside_ToolCall()
        {
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();
            var schemas = new Dictionary<string, JsonSchemaConstraint>
            {
                ["mds_db_so_query"] = new JsonSchemaConstraint("mds_db_so_query")
                    .AddProperty("order_id", SchemaPropertyType.String, required: true)
            };

            var processor = new ToolCallGrammarLogitProcessor(tokenizer, schemas);

            // Simulate prompt containing <tool_call>
            int[] pastTokens = tokenizer.Encode("<tool_call>");

            float[] logits = new float[tokenizer.VocabularySize];
            int braceToken = tokenizer.Encode("{")[0];
            int wordToken = tokenizer.Encode("Hello")[0];

            logits[braceToken] = 2.0f;
            logits[wordToken] = 5.0f; // Invalid inside tool call! Must start with '{'

            processor.Process(logits, pastTokens);

            Assert.True(processor.IsInsideToolCall);
            Assert.Equal(2.0f, logits[braceToken]);
            Assert.True(logits[wordToken] < -1e8f, "WordToken should be masked out to -Infinity inside tool call");
        }
    }
}
