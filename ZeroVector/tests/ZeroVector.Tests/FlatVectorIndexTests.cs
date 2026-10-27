using System;
using System.IO;
using Xunit;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroVector.Tests
{
    public class FlatVectorIndexTests
    {
        [Fact]
        public void AddAndSearchTopK_ReturnsExactMatchFirst()
        {
            int dim = 128;
            var index = new FlatVectorIndex(dim, initialCapacity: 16);
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

            // Fetch a known vector and query with it
            var query = new float[dim];
            bool found = index.TryGet(42, query);
            Assert.True(found);

            var results = index.SearchTopK(query, k: 5, VectorMetricType.Cosine);
            Assert.NotEmpty(results);
            Assert.Equal(42, results[0].Id);
            Assert.True(results[0].Score > 0.999f);
        }

        [Fact]
        public void MultiThreadedSearch_OverParallelThreshold_SucceedsAccurately()
        {
            int dim = 64;
            int count = 700; // Above 512 threshold
            var index = new FlatVectorIndex(dim, initialCapacity: 800);
            var rand = new Random(777);

            for (int id = 1; id <= count; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            Assert.Equal(count, index.Count);

            var query = new float[dim];
            Assert.True(index.TryGet(555, query));

            var top10 = index.SearchTopK(query, k: 10, VectorMetricType.Cosine);
            Assert.Equal(10, top10.Length);
            Assert.Equal(555, top10[0].Id);
            Assert.True(top10[0].Score > 0.999f);

            // Verify descending order
            for (int i = 0; i < top10.Length - 1; i++)
            {
                Assert.True(top10[i].Score >= top10[i + 1].Score);
            }
        }

        [Fact]
        public void SaveAndLoad_PreservesAllVectorsAndSearchResults()
        {
            int dim = 32;
            var original = new FlatVectorIndex(dim, initialCapacity: 32, VectorMetricType.Cosine);
            var rand = new Random(101);

            for (int id = 1; id <= 50; id++)
            {
                var vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                original.Add(id, vec);
            }

            using (var ms = new MemoryStream())
            {
                original.Save(ms);
                ms.Position = 0;

                var loaded = FlatVectorIndex.Load(ms);

                Assert.Equal(original.Dimension, loaded.Dimension);
                Assert.Equal(original.Count, loaded.Count);

                var query = new float[dim];
                original.TryGet(25, query);

                var originalResults = original.SearchTopK(query, 5);
                var loadedResults = loaded.SearchTopK(query, 5);

                Assert.Equal(originalResults.Length, loadedResults.Length);
                for (int i = 0; i < originalResults.Length; i++)
                {
                    Assert.Equal(originalResults[i].Id, loadedResults[i].Id);
                    Assert.Equal(originalResults[i].Score, loadedResults[i].Score, precision: 4);
                }
            }
        }
    }
}
