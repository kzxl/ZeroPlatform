using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.Verification;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Neural;
using ZeroNeural.Core.Quantization;
using ZeroTensor.Core;

namespace ZeroAgent.Tests
{
    public class AdvancedCapabilitiesEnhancementTests
    {
        // =========================================================================
        // PROPOSAL 1: SemanticCrossEncoderReranker Tests
        // =========================================================================
        [Fact]
        public void SemanticCrossEncoderReranker_ScoresAndReranksCandidatesPrecisely()
        {
            var reranker = new SemanticCrossEncoderReranker(dimension: 128, hiddenDim: 128, seed: 42);

            string query = "huong dan xu ly qua nhiet truc chinh spindle";
            var candidates = new List<string>
            {
                "Quy trinh an toan lao dong khi vao nha xuong",
                "Huong dan ve sinh bo loc nuoc lam mat spindle CNC khi qua nhiet",
                "Chinh sach tang ca va phu cap ca dem cho cong nhan",
                "SOP bao tri may cat laser cong suat cao"
            };

            var results = reranker.Rerank(
                query: query,
                candidates: candidates,
                textExtractor: doc => doc,
                topK: 2);

            Assert.Equal(2, results.Count);
            // The spindle overheat doc must be scored and ranked
            Assert.Contains("spindle", results[0].Item.ToLowerInvariant());
            Assert.True(results[0].RerankScore >= 0.0f && results[0].RerankScore <= 1.0f);
        }

        [Fact]
        public void SemanticCrossEncoderReranker_DirectEmbeddingScore_OutputsValidSigmoid()
        {
            var embedder = new LexicalSemanticEmbedder(dimension: 64);
            var reranker = new SemanticCrossEncoderReranker(dimension: 64, hiddenDim: 64, embedder: embedder, seed: 101);

            var u = embedder.Embed("kiem tra dien ap nguon 24V");
            var v = embedder.Embed("nguon adapter 24V cap dien cho PLC");

            float score = reranker.Score(u, v);
            Assert.InRange(score, 0.0f, 1.0f);
        }

        // =========================================================================
        // PROPOSAL 2: Streaming & Progressive Execution Engine Tests
        // =========================================================================
        [Fact]
        public async Task ReActAgent_ExecuteAsync_DispatchesProgressiveStreamEventsInOrder()
        {
            var tools = new AgentToolRegistry();
            tools.Register("CheckPressure", "Checks hydraulic pressure", (string arg) => "6.5 bar - Pressure Normal");

            var mockLlm = new TeacherMockLlmClient(prompt =>
            {
                if (prompt.Contains("Observation: 6.5 bar - Pressure Normal"))
                {
                    return "Final Answer: Ap suat thuy luc on dinh o muc 6.5 bar.";
                }
                return "Thought: Toi can kiem tra ap suat thuy luc truoc.\nAction: CheckPressure(\"line_1\")";
            });

            var agent = new ReActAgent("TestAgent", "SystemOperator", tools, mockLlm)
            {
                StepVerifier = new DefaultStepVerifier()
            };

            var context = new AgentContext("Kiem tra ap suat he thong");
            var events = new List<AgentStreamEvent>();

            var response = await agent.ExecuteAsync(context, onEvent: ev => events.Add(ev));

            Assert.True(response.Success);
            Assert.NotEmpty(events);

            // Verify event stream lifecycle
            Assert.Equal(AgentStreamEventType.GoalStarted, events[0].Type);
            Assert.Equal("Kiem tra ap suat he thong", events[0].Content);

            Assert.Contains(events, ev => ev.Type == AgentStreamEventType.ThoughtGenerated);
            Assert.Contains(events, ev => ev.Type == AgentStreamEventType.ToolCalling && ev.ToolName == "CheckPressure");
            Assert.Contains(events, ev => ev.Type == AgentStreamEventType.StepVerified);
            Assert.Contains(events, ev => ev.Type == AgentStreamEventType.ToolCompleted && ev.ToolResult!.Contains("6.5 bar"));
            Assert.Contains(events, ev => ev.Type == AgentStreamEventType.Completed);

            var completedEvent = events[events.Count - 1];
            Assert.Equal(AgentStreamEventType.Completed, completedEvent.Type);
            Assert.Contains("Ap suat thuy luc on dinh", completedEvent.Content);
        }

        // =========================================================================
        // PROPOSAL 3: NeuralWeightQuantizer (INT8 Symmetric Quantization) Tests
        // =========================================================================
        [Fact]
        public void NeuralWeightQuantizer_QuantizesAndDequantizes_WithMinimalError()
        {
            var origArray = new float[] { -2.5f, -1.0f, 0.0f, 0.75f, 1.85f, 2.5f };
            var tensor = Tensor.FromArray(origArray, 1, 6);

            var quantized = NeuralWeightQuantizer.Quantize(tensor);

            Assert.Equal(6, quantized.Length);
            Assert.True(quantized.Scale > 0f);

            // Verify compression: INT8 uses 1 byte per element vs 4 bytes in FP32
            Assert.Equal(6, quantized.Data.Length);

            var dequantized = quantized.Dequantize();
            float mae = NeuralWeightQuantizer.ComputeQuantizationError(tensor, quantized);

            // Symmetric quantization on [-2.5, 2.5] across 127 steps has step size ~0.02
            Assert.InRange(mae, 0.0f, 0.03f);
        }

        [Fact]
        public void QuantizedTensor_DotProduct_MatchesFloatDotProductWithinTolerance()
        {
            var aFloats = new float[] { 1.2f, -0.8f, 0.5f, 2.1f, -1.5f };
            var bFloats = new float[] { 0.4f, 1.1f, -0.9f, 0.8f, 1.2f };

            var aTensor = Tensor.FromArray(aFloats, 5);
            var bTensor = Tensor.FromArray(bFloats, 5);

            var qA = NeuralWeightQuantizer.Quantize(aTensor);
            var qB = NeuralWeightQuantizer.Quantize(bTensor);

            float floatDot = 0f;
            for (int i = 0; i < 5; i++) floatDot += aFloats[i] * bFloats[i];

            float int8Dot = QuantizedTensor.DotProduct(qA, qB);

            // Verify dot product approximation
            Assert.InRange(Math.Abs(floatDot - int8Dot), 0.0f, 0.15f);
        }

        [Fact]
        public void QuantizedTensor_SerializationAndDeserialization_PreservesState()
        {
            var floats = new float[] { -1.5f, 0.3f, 2.8f, -0.05f };
            var orig = Tensor.FromArray(floats, 2, 2);
            var quantized = NeuralWeightQuantizer.Quantize(orig);

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                quantized.Serialize(writer);
            }

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var reloaded = QuantizedTensor.Deserialize(reader);

            Assert.Equal(quantized.Scale, reloaded.Scale);
            Assert.Equal(quantized.Shape, reloaded.Shape);
            Assert.Equal(quantized.Data, reloaded.Data);
        }
    }
}
