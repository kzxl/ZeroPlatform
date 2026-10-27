using System;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Dialog.Neural;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ZabIntegrationTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabIntegration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ZabDatabase_TwoStageVectorIndex_AcceleratesSearch()
        {
            string dbPath = Path.Combine(_tempDir, "two_stage_vectors.zab");
            int dim = 32;

            // Generate 25 synthetic vectors (exceeds threshold 16 to trigger TwoStageVectorIndex accelerator)
            float[] targetQuery = new float[dim];
            for (int d = 0; d < dim; d++) targetQuery[d] = 1.0f;
            VectorMetrics.NormalizeL2(targetQuery);

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                for (int i = 0; i < 25; i++)
                {
                    float[] vec = new float[dim];
                    for (int d = 0; d < dim; d++)
                    {
                        vec[d] = (float)Math.Sin(i * 0.5 + d * 0.1);
                    }
                    if (i == 10)
                    {
                        // Make vector 10 almost identical to target query
                        Array.Copy(targetQuery, vec, dim);
                    }
                    VectorMetrics.NormalizeL2(vec);
                    db.StoreVector($"Vector_{i}", vec);
                }

                // Execute SearchVectors -> should utilize TwoStageVectorIndex accelerator
                var matches = db.SearchVectors(targetQuery, topK: 3, minSimilarity: 0.80f);
                Assert.NotEmpty(matches);
                Assert.Equal("Vector_10", matches[0].Record.Label);
                Assert.True(matches[0].Similarity > 0.95f);

                db.Commit();
            }

            // Reopen and verify accelerator reloads from snapshot
            using (var dbReopened = ZabDatabase.Open(dbPath))
            {
                Assert.Equal(25, dbReopened.Vectors.Count);
                var matches = dbReopened.SearchVectors(targetQuery, topK: 1, minSimilarity: 0.80f);
                Assert.Single(matches);
                Assert.Equal("Vector_10", matches[0].Record.Label);
            }
        }

        [Fact]
        public void ZabMemoryStorageBridge_PersistsAndHydratesAgenticMemory()
        {
            string dbPath = Path.Combine(_tempDir, "agentic_memory.zab");

            var engine = new AgenticMemoryEngine(dimension: 128);

            // 1. Add Semantic SOP Items
            float[] sopVec1 = engine.Embedder.Embed("SOP 01: Spindle Lubrication Manual");
            float[] sopVec2 = engine.Embedder.Embed("SOP 02: Emergency Stop Procedure");
            engine.Semantic.Add("Spindle Lubrication", "Apply ISO VG 68 oil every 500 hours.", sopVec1, "Maintenance");
            engine.Semantic.Add("Emergency Stop", "Press red button and lock out power supply.", sopVec2, "Safety");

            // 2. Add Episodic Incidents (Ebbinghaus Decay)
            float[] epVec1 = engine.Embedder.Embed("Spindle overheating error code E401");
            engine.Episodic.Record("Spindle overheating E401", "Cleaned coolant nozzle and cleared chips.", epVec1, success: true);

            // 3. Add Semantic Response Cache
            float[] cacheVec = engine.Embedder.Embed("What is the oil grade for spindle?");
            engine.ResponseCache.Store(cacheVec, "What is the oil grade for spindle?", "ISO VG 68");

            // Persist to Sovereign ZabDatabase
            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                ZabMemoryStorageBridge.PersistToZab(engine, db);
                Assert.True(db.Knowledge.Count >= 3);
                Assert.Single(db.Plans);
            }

            // Hydrate into fresh empty AgenticMemoryEngine
            var newEngine = new AgenticMemoryEngine(dimension: 128);
            Assert.Equal(0, newEngine.Semantic.Count);
            Assert.Equal(0, newEngine.Episodic.Count);
            Assert.Equal(0, newEngine.ResponseCache.Count);

            using (var dbReopened = ZabDatabase.Open(dbPath))
            {
                ZabMemoryStorageBridge.HydrateFromZab(newEngine, dbReopened);

                Assert.Equal(2, newEngine.Semantic.Count);
                Assert.Equal(1, newEngine.Episodic.Count);
                Assert.Equal(1, newEngine.ResponseCache.Count);

                // Verify Semantic recall on hydrated memory
                var sopResults = newEngine.Semantic.Query(sopVec1, topK: 1);
                Assert.Single(sopResults);
                Assert.Equal("Spindle Lubrication", sopResults[0].Item.Title);

                // Verify Episodic recall with Ebbinghaus scoring
                var epResults = newEngine.Episodic.Recall(epVec1, topK: 1);
                Assert.Single(epResults);
                Assert.Contains("Cleaned coolant nozzle", epResults[0].Episode.Resolution);
            }
        }

        [Fact]
        public void NeuralIntentClassifier_ExportsToZabPolicy_AndRunsSystem1Inference()
        {
            string dbPath = Path.Combine(_tempDir, "exported_neural.zab");

            var embedder = new HybridSemanticEmbedder(dimension: 128);
            var classifier = new NeuralIntentClassifier(embedder);

            var intentTurnOn = new DialogueIntent("TurnOnSpindle", "Industrial");
            intentTurnOn.SampleUtterances.Add("Turn on spindle at 3000 RPM");
            intentTurnOn.SampleUtterances.Add("Start spindle rotation");
            intentTurnOn.SampleUtterances.Add("Activate motor spindle");

            var intentReadSensor = new DialogueIntent("ReadPressureSensor", "Sensor");
            intentReadSensor.SampleUtterances.Add("Read hydraulic pressure bar");
            intentReadSensor.SampleUtterances.Add("Check pressure sensor value");
            intentReadSensor.SampleUtterances.Add("What is current line pressure?");

            classifier.Train(new[] { intentTurnOn, intentReadSensor }, epochs: 30);
            Assert.True(classifier.IsTrained);

            // Export to lightweight INT8 ZabNeuralPolicy
            var zabPolicy = classifier.ExportToZabPolicy("FactoryIntentPolicy");
            Assert.NotNull(zabPolicy);
            Assert.Equal(128, zabPolicy.InputDim);
            Assert.Equal(2, zabPolicy.OutputClasses.Count);

            // Save to ZabDatabase and execute sub-0.05ms System 1 inference
            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(zabPolicy);
                db.Commit();

                float[] testVec = embedder.Embed("Please start spindle motor now");
                string? predictedChoice = db.PredictChoice(testVec, out float conf);

                Assert.Equal("TurnOnSpindle", predictedChoice);
                Assert.True(conf > 0.50f);
            }

            // Verify persistence of exported policy across reboots
            using (var dbReopened = ZabDatabase.Open(dbPath))
            {
                Assert.NotNull(dbReopened.NeuralPolicy);
                float[] testVec = embedder.Embed("What is current line pressure in bar?");
                string? predictedChoice = dbReopened.PredictChoice(testVec, out float conf);

                Assert.Equal("ReadPressureSensor", predictedChoice);
                Assert.True(conf > 0.50f);
            }
        }
    }
}
