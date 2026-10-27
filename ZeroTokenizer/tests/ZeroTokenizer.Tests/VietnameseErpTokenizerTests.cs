using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroTokenizer.Core;
using ZeroTokenizer.Core.Training;
using ZeroTokenizer.Core.Vietnamese;

namespace ZeroTokenizer.Tests
{
    public class VietnameseErpTokenizerTests
    {
        [Fact]
        public void VietnameseErpTokenizer_EncodeDecode_RoundtripsSuccessfully()
        {
            var tokenizer = VietnameseErpTokenizer.CreateDefault();

            string sampleText = "Kiểm tra tồn kho mã hàng SKU-STEEL-01 tại kho tổng xem còn bao nhiêu cái";
            int[] tokens = tokenizer.Encode(sampleText);

            Assert.NotEmpty(tokens);
            string decoded = tokenizer.Decode(tokens);
            Assert.Equal(sampleText, decoded);
        }

        [Fact]
        public void VietnameseErpTokenizer_PreservesSpecialTokens()
        {
            var tokenizer = VietnameseErpTokenizer.CreateDefault();

            string input = "<thought>Đang kiểm tra dữ liệu kho</thought><response>Tồn kho hiện có 500 cái.</response>";
            int[] tokens = tokenizer.Encode(input);

            // Verify special tokens have their exact IDs in the stream
            int thoughtId = tokenizer.Vocabulary[SpecialTokens.Thought];
            int endThoughtId = tokenizer.Vocabulary[SpecialTokens.EndThought];
            int respId = tokenizer.Vocabulary[SpecialTokens.Response];
            int endRespId = tokenizer.Vocabulary[SpecialTokens.EndResponse];

            Assert.Contains(thoughtId, tokens);
            Assert.Contains(endThoughtId, tokens);
            Assert.Contains(respId, tokens);
            Assert.Contains(endRespId, tokens);

            string decoded = tokenizer.Decode(tokens);
            Assert.Equal(input, decoded);
        }

        [Fact]
        public void VietnameseErpTokenizer_HandlesLosslessByteFallbackForEmojisAndSymbols()
        {
            var tokenizer = VietnameseErpTokenizer.CreateDefault();

            string exoticText = "Báo cáo kho 🏭: Nhiệt độ 25°C, tồn kho 500 kg ✅ [OK]";
            int[] tokens = tokenizer.Encode(exoticText);

            Assert.NotEmpty(tokens);
            string decoded = tokenizer.Decode(tokens);
            Assert.Equal(exoticText, decoded);
        }

        [Fact]
        public void VietnameseErpTokenizer_SerializationRoundtrip_PreservesVocabAndRanks()
        {
            var original = VietnameseErpTokenizer.CreateDefault();

            string serialized;
            using (var sw = new StringWriter())
            {
                original.Save(sw);
                serialized = sw.ToString();
            }

            Assert.Contains("# ZERO_VIETNAMESE_ERP_BPE_V1", serialized);
            Assert.Contains("[VOCAB_COUNT]:", serialized);
            Assert.Contains("[RANKS_COUNT]:", serialized);

            VietnameseErpTokenizer restored;
            using (var sr = new StringReader(serialized))
            {
                restored = VietnameseErpTokenizer.Load(sr);
            }

            Assert.Equal(original.VocabularySize, restored.VocabularySize);
            Assert.Equal(original.SpecialTokensList.Count, restored.SpecialTokensList.Count);

            string testPhrase = "Lệnh sản xuất MO-2026-01 hoàn thành 65% kế hoạch định mức BOM";
            int[] origTokens = original.Encode(testPhrase);
            int[] restTokens = restored.Encode(testPhrase);
            Assert.Equal(origTokens, restTokens);
            Assert.Equal(testPhrase, restored.Decode(restTokens));
        }

        [Fact]
        public void VietnameseErpTokenizer_FormatAgentPrompt_ProducesValidStructuredPrompt()
        {
            string prompt = VietnameseErpTokenizer.FormatAgentPrompt(
                systemPrompt: "Bạn là chuyên viên ERP",
                userMessage: "Kiểm tra đơn hàng SO-001",
                domain: "sales",
                thought: "Truy vấn bảng sales_orders",
                toolCall: "query_so(id='SO-001')",
                toolResult: "Đã giao hàng 100%",
                response: "Đơn hàng SO-001 đã được giao hoàn tất.");

            Assert.StartsWith(SpecialTokens.Bos, prompt);
            Assert.Contains("<expert>sales</expert>", prompt);
            Assert.Contains("<thought>Truy vấn bảng sales_orders</thought>", prompt);
            Assert.Contains("<tool_call>query_so(id='SO-001')</tool_call>", prompt);
            Assert.Contains("<tool_result>Đã giao hàng 100%</tool_result>", prompt);
            Assert.Contains("<response>Đơn hàng SO-001 đã được giao hoàn tất.</response>", prompt);
        }

        [Fact]
        public void BpeTrainer_TrainsFromCorpus_GeneratesValidTokenizer()
        {
            var trainer = new BpeTrainer(targetVocabSize: 320, minFrequency: 2);
            var corpus = new[]
            {
                "tồn kho vật tư linh kiện",
                "kiểm tra tồn kho vật tư",
                "báo cáo tồn kho vật tư hàng hoá",
                "xuất kho vật tư sản xuất"
            };

            var trainedTokenizer = trainer.Train(corpus);
            Assert.True(trainedTokenizer.VocabularySize >= 280);

            // Test lossless roundtrip on corpus text
            string testText = "tồn kho vật tư";
            int[] tokens = trainedTokenizer.Encode(testText);
            Assert.NotEmpty(tokens);
            string decoded = trainedTokenizer.Decode(tokens);
            Assert.Equal(testText, decoded);
        }
    }
}
