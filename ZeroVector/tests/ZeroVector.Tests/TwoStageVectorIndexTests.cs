using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Quantization;

namespace ZeroVector.Tests
{
    public class TwoStageVectorIndexTests
    {
        [Fact]
        public void Quantizer1Bit_CorrectPackingAndHammingDistance()
        {
            float[] a = new float[] { 1.5f, -0.5f, 2.0f, -1.0f };
            float[] b = new float[] { 1.0f, 0.5f, -2.0f, -1.0f };

            ulong[] bqA = new ulong[1];
            ulong[] bqB = new ulong[1];

            BinaryQuantizer.Quantize1Bit(a, bqA);
            BinaryQuantizer.Quantize1Bit(b, bqB);

            // a: [1, 0, 1, 0] = bit0=1, bit1=0, bit2=1, bit3=0 => 0b0101 = 5
            // b: [1, 1, 0, 0] = bit0=1, bit1=1, bit2=0, bit3=0 => 0b0011 = 3
            // xor: bit1 and bit2 differ => dist = 2
            int dist = BinaryQuantizer.ComputeHammingDistance(bqA, bqB);
            Assert.Equal(2, dist);

            float sim = BinaryQuantizer.ComputeHammingSimilarity(bqA, bqB, 4);
            Assert.Equal(0.5f, sim, precision: 3);
        }

        [Fact]
        public void SQ8_QuantizeAndDequantize_LowReconstructionError()
        {
            int dim = 128;
            var rand = new Random(1234);
            float[] original = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                original[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
            }
            VectorMetrics.NormalizeL2(original);

            sbyte[] sq8 = new sbyte[dim];
            BinaryQuantizer.QuantizeSQ8(original, sq8, out float scale, out float offset);

            float[] reconstructed = new float[dim];
            BinaryQuantizer.DequantizeSQ8(sq8, reconstructed, scale, offset);

            // Compute cosine similarity between original and reconstructed
            float cosSim = VectorMetrics.CosineSimilarity(original, reconstructed);
            Assert.True(cosSim > 0.99f, $"Expected reconstruction cosine similarity > 0.99, got {cosSim}");
        }

        [Fact]
        public void TwoStageVectorIndex_SearchTopK_ReturnsExactMatchFirst()
        {
            int dim = 128;
            var index = new TwoStageVectorIndex(dim, initialCapacity: 16);
            var rand = new Random(42);

            for (int id = 1; id <= 100; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            Assert.Equal(100, index.Count);

            // Query with existing item
            var query = new float[dim];
            bool found = index.TryGet(42, query);
            Assert.True(found);

            var results = index.SearchTopK(query, k: 5);
            Assert.NotEmpty(results);
            Assert.Equal(42, results[0].Id);
            Assert.True(results[0].Score > 0.999f);
        }

        [Fact]
        public void TwoStageVectorIndex_HighRecallVsFlatIndex()
        {
            int dim = 256;
            int count = 500;
            var rand = new Random(888);

            var flatIndex = new FlatVectorIndex(dim, count);
            var twoStageIndex = new TwoStageVectorIndex(dim, count, oversampleFactor: 6);

            for (int id = 1; id <= count; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                flatIndex.Add(id, vec);
                twoStageIndex.Add(id, vec);
            }

            // Test 10 random query vectors
            int totalTop1Matches = 0;
            for (int q = 0; q < 10; q++)
            {
                var query = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    query[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(query);

                var flatTopK = flatIndex.SearchTopK(query, 5);
                var twoStageTopK = twoStageIndex.SearchTopK(query, 5);

                if (flatTopK[0].Id == twoStageTopK[0].Id)
                {
                    totalTop1Matches++;
                }
            }

            // High recall: BQ coarse filter followed by fine reranking should match Top 1 in almost all cases
            Assert.True(totalTop1Matches >= 9, $"Expected at least 9/10 top-1 matches, got {totalTop1Matches}");
        }

        [Fact]
        public void TwoStageVectorIndex_FilteredSearch_HonorsPredicate()
        {
            int dim = 64;
            var index = new TwoStageVectorIndex(dim, 50);
            var rand = new Random(101);

            for (int id = 1; id <= 50; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            var query = new float[dim];
            index.TryGet(10, query);

            // Filter out even IDs, only allow odd IDs
            var results = index.SearchTopK(query, k: 5, id => id % 2 != 0);

            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.True(r.Id % 2 != 0));
            Assert.DoesNotContain(results, r => r.Id == 10);
        }

        [Fact]
        public void TwoStageVectorIndex_Persistence_SaveAndLoadRoundtrip()
        {
            int dim = 64;
            var index = new TwoStageVectorIndex(dim, 20);
            var rand = new Random(555);

            for (int id = 1; id <= 20; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            byte[] serialized;
            using (var ms = new MemoryStream())
            {
                index.Save(ms);
                serialized = ms.ToArray();
            }

            using (var ms = new MemoryStream(serialized))
            {
                var loaded = TwoStageVectorIndex.Load(ms);
                Assert.Equal(index.Dimension, loaded.Dimension);
                Assert.Equal(index.Count, loaded.Count);

                var query = new float[dim];
                index.TryGet(5, query);

                var rOriginal = index.SearchTopK(query, 3);
                var rLoaded = loaded.SearchTopK(query, 3);

                Assert.Equal(rOriginal.Length, rLoaded.Length);
                for (int i = 0; i < rOriginal.Length; i++)
                {
                    Assert.Equal(rOriginal[i].Id, rLoaded[i].Id);
                    Assert.Equal(rOriginal[i].Score, rLoaded[i].Score, precision: 5);
                }
            }
        }

        [Fact]
        public void TwoStageVectorIndex_ScalarQuantizedSq8_OperatesCorrectly()
        {
            int dim = 128;
            var index = new TwoStageVectorIndex(dim, 50, storageMode: QuantizationStorageMode.ScalarQuantizedSq8);
            var rand = new Random(999);

            for (int id = 1; id <= 50; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            var query = new float[dim];
            index.TryGet(25, query);

            var results = index.SearchTopK(query, 5);
            Assert.NotEmpty(results);
            Assert.Equal(25, results[0].Id);
            Assert.True(results[0].Score > 0.95f);
        }

        [Fact]
        public void TwoStageVectorIndex_Performance_BenchmarkVsFlatIndex()
        {
            int dim = 384; // Standard embedding size (MiniLM / BGE)
            int count = 2000;
            var rand = new Random(4242);

            var flatIndex = new FlatVectorIndex(dim, count);
            var twoStageIndex = new TwoStageVectorIndex(dim, count, oversampleFactor: 4);

            for (int id = 1; id <= count; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                VectorMetrics.NormalizeL2(vec);
                flatIndex.Add(id, vec);
                twoStageIndex.Add(id, vec);
            }

            // Warmup
            var query = new float[dim];
            for (int d = 0; d < dim; d++) query[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
            VectorMetrics.NormalizeL2(query);

            flatIndex.SearchTopK(query, 10);
            twoStageIndex.SearchTopK(query, 10);

            // Benchmark 200 searches
            int iterations = 200;

            var swFlat = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                flatIndex.SearchTopK(query, 10);
            }
            swFlat.Stop();

            var swTwoStage = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                twoStageIndex.SearchTopK(query, 10);
            }
            swTwoStage.Stop();

            // Two-stage BQ search should be significantly faster or comparable with vastly lower cache footprint
            Assert.True(swTwoStage.ElapsedMilliseconds <= swFlat.ElapsedMilliseconds * 1.5,
                $"TwoStage took {swTwoStage.ElapsedMilliseconds}ms vs Flat {swFlat.ElapsedMilliseconds}ms");
        }
    }
}
