using System;
using Xunit;
using ZeroPrompt.Core.Grammar;
using ZeroTokenizer.Core.Tiktoken;

namespace ZeroPrompt.Tests
{
    public class GrammarLogitMaskerTests
    {
        [Fact]
        public void GrammarLogitMasker_Should_Mask_Out_Invalid_Tokens()
        {
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();
            var masker = new GrammarLogitMasker(tokenizer);

            var grammar = new JsonGrammarState();
            // In initial state, valid next is '{' or '['
            int braceToken = tokenizer.Encode("{")[0];
            int wordToken = tokenizer.Encode("Hello")[0];

            float[] logits = new float[tokenizer.VocabularySize];
            logits[braceToken] = 2.0f;
            logits[wordToken] = 5.0f; // Higher initial logit, but invalid syntax!

            masker.MaskLogits(grammar, logits, new int[] { braceToken, wordToken });

            Assert.Equal(2.0f, logits[braceToken]);
            Assert.True(logits[wordToken] < -1e8f, "WordToken should be masked out to -Infinity");
        }
    }
}
