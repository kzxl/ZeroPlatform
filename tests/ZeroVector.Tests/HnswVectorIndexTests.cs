using System;
using Xunit;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;

namespace ZeroVector.Tests
{
    public class HnswVectorIndexTests
    {
        [Fact]
        public void AddAndSearchTopK_ReturnsExactSelfMatch()
        {
            int dim = 64;
            var hnsw = new HnswVectorIndex(dim, m: 16, efConstruction: 100, efSearch: 50, metric: VectorMetricType.Cosine);
            var rand = new Random(42);

            for (int id = 1; id <= 200; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                hnsw.Add(id, vec);
            }

            Assert.Equal(200, hnsw.Count);

            var query = new float[dim];
            Assert.True(hnsw.TryGet(100, query));

            var results = hnsw.SearchTopK(query, k: 5);
            Assert.NotEmpty(results);
            Assert.Equal(100, results[0].Id);
            Assert.True(results[0].Score > 0.999f);
        }

        [Fact]
        public void Hnsw_AchievesHighRecallComparedToBruteForce()
        {
            int dim = 32;
            int count = 500;
            var flat = new FlatVectorIndex(dim, count, VectorMetricType.Cosine);
            var hnsw = new HnswVectorIndex(dim, m: 16, efConstruction: 120, efSearch: 60, metric: VectorMetricType.Cosine);
            var rand = new Random(888);

            for (int id = 1; id <= count; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                flat.Add(id, vec);
                hnsw.Add(id, vec);
            }

            int testQueries = 20;
            int topK = 5;
            int totalHits = 0;

            for (int q = 0; q < testQueries; q++)
            {
                var queryVec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    queryVec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(queryVec);

                var exactResults = flat.SearchTopK(queryVec, topK);
                var hnswResults = hnsw.SearchTopK(queryVec, topK);

                var exactSet = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < exactResults.Length; i++)
                {
                    exactSet.Add(exactResults[i].Id);
                }

                for (int i = 0; i < hnswResults.Length; i++)
                {
                    if (exactSet.Contains(hnswResults[i].Id))
                    {
                        totalHits++;
                    }
                }
            }

            double recall = (double)totalHits / (testQueries * topK);
            // HNSW with efSearch=60 should achieve > 90% recall
            Assert.True(recall >= 0.85, $"Expected Recall@5 >= 85%, but got {recall:P2}");
        }

        [Fact]
        public void Clear_ResetsCountAndIndex()
        {
            int dim = 16;
            var hnsw = new HnswVectorIndex(dim);
            hnsw.Add(1, new float[16]);
            hnsw.Add(2, new float[16]);
            Assert.Equal(2, hnsw.Count);

            hnsw.Clear();
            Assert.Equal(0, hnsw.Count);
            Assert.Empty(hnsw.SearchTopK(new float[16], 5));
        }

        [Fact]
        public void SelectNeighborsHeuristic_BridgesClustersAcrossDirections()
        {
            // Create 2D vectors with Euclidean metric:
            // Cluster A near (1, 0): 10 very tight points at distance ~1.0 from origin
            // Cluster B near (-1.5, 0): 1 point at distance 1.5 from origin (opposite direction)
            // Cluster C near (0, 1.6): 1 point at distance 1.6 from origin (orthogonal direction)
            // When origin (0, 0) is inserted with M=2 (so M0=4 on layer 0),
            // simple selection would pick 4 redundant points from Cluster A,
            // whereas Algorithm 4 heuristic picks 1 representative from Cluster A,
            // 1 from Cluster B, 1 from Cluster C, and fills the 4th slot via keepPrunedConnections!
            var hnsw = new HnswVectorIndex(dimension: 2, m: 2, efConstruction: 32, efSearch: 16, metric: VectorMetricType.Euclidean);

            for (int i = 1; i <= 10; i++)
            {
                hnsw.Add(i, new float[] { 1.0f + i * 0.001f, 0.0f });
            }
            hnsw.Add(20, new float[] { -1.5f, 0.0f });
            hnsw.Add(30, new float[] { 0.0f, 1.6f });

            // Insert hub at origin
            hnsw.Add(99, new float[] { 0.0f, 0.0f });

            int[] neighbors = hnsw.GetNodeExternalNeighbors(99, level: 0);
            Assert.Equal(4, neighbors.Length);
            Assert.Contains(20, neighbors);
            Assert.Contains(30, neighbors);

            // Search for a query near Cluster B (-2, 0) starting from any entry point should reach node 20
            var searchB = hnsw.SearchTopK(new float[] { -2.0f, 0.0f }, k: 1);
            Assert.Single(searchB);
            Assert.Equal(20, searchB[0].Id);
        }

        [Fact]
        public void SearchLayer_PooledVisitedBitSet_HandlesMultiWordNodeCounts()
        {
            // > 256 nodes spans 5+ ulong words in the pooled visitedBits bitset
            int dim = 16;
            int count = 300;
            var hnsw = new HnswVectorIndex(dim, m: 8, efConstruction: 64, efSearch: 64, metric: VectorMetricType.Euclidean);
            var rng = new Random(1234);

            for (int id = 0; id < count; id++)
            {
                var v = new float[dim];
                for (int d = 0; d < dim; d++) v[d] = (float)rng.NextDouble();
                hnsw.Add(id, v);
            }

            var target = new float[dim];
            Assert.True(hnsw.TryGet(257, target));

            var top = hnsw.SearchTopK(target, k: 3);
            Assert.NotEmpty(top);
            Assert.Equal(257, top[0].Id);
            Assert.Equal(0.0f, top[0].Score, 4);
        }
    }
}
