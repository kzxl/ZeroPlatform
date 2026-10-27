using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Neural;

namespace ZeroAgent.Tests
{
    public class NeuralDialogTests
    {
        [Fact]
        public void NeuralIntentClassifier_TrainsAndConverges_WithCrossEntropyLoss()
        {
            var embedder = new LexicalSemanticEmbedder();
            var classifier = new NeuralIntentClassifier(embedder);

            var intent1 = new DialogueIntent("CHECK_TEMP", "Check temperature")
                .AddSamples("kiểm tra nhiệt độ", "xem nhiệt độ", "nhiệt độ máy", "check temp");

            var intent2 = new DialogueIntent("STOP_MACHINE", "Stop machine")
                .AddSamples("dừng máy", "ngắt nguồn", "dừng khẩn cấp", "tắt thiết bị");

            var intents = new[] { intent1, intent2 };

            // Train MLP using Adam + CrossEntropyLoss
            classifier.Train(intents, epochs: 60, learningRate: 0.05f);

            Assert.True(classifier.IsTrained);
            Assert.Equal(2, classifier.ClassCount);

            // Test predictions on known utterances
            var (pred1, conf1, _) = classifier.Predict("kiểm tra nhiệt độ");
            Assert.NotNull(pred1);
            Assert.Equal("CHECK_TEMP", pred1!.Id);
            Assert.True(conf1 > 0.80f, $"Confidence was {conf1}");

            var (pred2, conf2, _) = classifier.Predict("dừng máy ngay");
            Assert.NotNull(pred2);
            Assert.Equal("STOP_MACHINE", pred2!.Id);
            Assert.True(conf2 > 0.80f, $"Confidence was {conf2}");
        }

        [Fact]
        public void NeuralIntentClassifier_GeneralizesTo_ParaphrasedUtterances()
        {
            var embedder = new LexicalSemanticEmbedder();
            var classifier = new NeuralIntentClassifier(embedder);

            var intent1 = new DialogueIntent("CHECK_TEMP", "Check temperature")
                .AddSamples("kiểm tra nhiệt độ", "xem nhiệt độ máy", "nhiệt độ thiết bị", "đo nhiệt độ");

            var intent2 = new DialogueIntent("STOP_MACHINE", "Stop machine")
                .AddSamples("dừng máy khẩn cấp", "ngắt điện nguồn", "tắt khẩn cấp máy", "ngắt nguồn");

            classifier.Train(new[] { intent1, intent2 }, epochs: 80, learningRate: 0.05f);

            // Paraphrased query not verbatim in dataset
            var (pred, conf, _) = classifier.Predict("làm ơn tắt khẩn cấp nguồn điện");
            Assert.NotNull(pred);
            Assert.Equal("STOP_MACHINE", pred!.Id);
            Assert.True(conf > 0.70f, $"Confidence was {conf}");
        }

        [Fact]
        public void NeuralDenseProjector_ProjectsToDenseLatentSpace_AndNormalizes()
        {
            var projector = new NeuralDenseProjector(inputDim: 128, outputDim: 64, seed: 42);
            var embedder = new LexicalSemanticEmbedder();

            var raw128 = embedder.Embed("hướng dẫn xử lý sự cố quá nhiệt động cơ");
            var dense64 = projector.Project(raw128);

            Assert.Equal(64, dense64.Length);

            // Verify L2 normalization: sum(x_i^2) ~= 1.0
            float sumSq = 0f;
            for (int i = 0; i < dense64.Length; i++)
            {
                sumSq += dense64[i] * dense64[i];
            }

            Assert.InRange(sumSq, 0.99f, 1.01f);
        }

        [Fact]
        public async Task IndustrialBot_WithNeuralClassifier_EndToEndExecution()
        {
            // Create bot with Neural Classifier enabled and auto-trained
            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);
            string sessionId = "neural_session_01";

            // Turn 1: Ask temperature with machine
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-01");
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Contains("CNC-01", r1.Text);
            Assert.Contains("73.5°C", r1.Text);

            // Turn 2: Follow-up using pronoun with neural routing
            var r2 = await bot.ChatAsync(sessionId, "Nhiệt độ nó giờ sao?");
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("CNC-01", r2.Text);
        }

        [Fact]
        public void Benchmark_NeuralInferenceLatency_UnderOneMillisecond()
        {
            var embedder = new LexicalSemanticEmbedder();
            var classifier = new NeuralIntentClassifier(embedder);

            var intent1 = new DialogueIntent("CHECK_TEMP", "Check temperature")
                .AddSamples("kiểm tra nhiệt độ", "xem nhiệt độ");
            var intent2 = new DialogueIntent("STOP_MACHINE", "Stop machine")
                .AddSamples("dừng máy", "ngắt nguồn");

            classifier.Train(new[] { intent1, intent2 }, epochs: 30, learningRate: 0.05f);

            var vec = embedder.Embed("kiểm tra nhiệt độ");

            // Warm-up
            for (int i = 0; i < 5; i++)
            {
                classifier.Predict(vec);
            }

            // Benchmark 50 inference iterations
            var sw = Stopwatch.StartNew();
            int iterations = 50;
            for (int i = 0; i < iterations; i++)
            {
                var (pred, conf, _) = classifier.Predict(vec);
                Assert.NotNull(pred);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            // Must be sub-millisecond (typically < 0.05ms)
            Assert.True(avgMs < 1.0, $"Average inference latency was {avgMs:F4} ms (expected < 1.0 ms)");
        }
    }
}
