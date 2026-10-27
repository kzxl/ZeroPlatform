using System;
using System.Diagnostics;
using System.Linq;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class VectorColumnAndHybridSearchTests
    {
        [Fact]
        public void VectorColumn_CreationAndDirectAccess_Succeeds()
        {
            int dim = 64;
            var vecCol = new VectorColumn("embedding", dim);

            float[] v1 = new float[dim];
            float[] v2 = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                v1[i] = i * 0.1f;
                v2[i] = -i * 0.1f;
            }

            vecCol.AddVector(v1);
            vecCol.AddVector(v2);

            Assert.Equal(2, vecCol.Length);
            Assert.Equal(dim, vecCol.Dimension);

            var span0 = vecCol.GetVectorSpan(0);
            var span1 = vecCol.GetVectorSpan(1);

            Assert.Equal(0.0f, span0[0]);
            Assert.Equal(0.1f, span0[1]);
            Assert.Equal(-0.1f, span1[1]);
        }

        [Fact]
        public void VectorColumn_SearchNearest_ExactMatchRankOne()
        {
            int dim = 128;
            int count = 200;
            var rand = new Random(42);

            var vecCol = new VectorColumn("embedding", dim);
            for (int i = 0; i < count; i++)
            {
                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                Normalize(vec);
                vecCol.AddVector(vec);
            }

            // Query with row 42
            var query = vecCol.GetVector(42);
            var matches = vecCol.SearchNearest(query, topK: 5);

            Assert.NotEmpty(matches);
            Assert.Equal(42, matches[0].RowIndex);
            Assert.True(matches[0].Score > 0.999f);
        }

        [Fact]
        public void DataFrame_FusedHybridSearch_WithSelectionMask()
        {
            int count = 500;
            int dim = 128;
            var rand = new Random(100);

            var ids = new int[count];
            var categories = new string[count];
            var prices = new double[count];
            var vecCol = new VectorColumn("embedding", dim);

            for (int i = 0; i < count; i++)
            {
                ids[i] = i + 1;
                categories[i] = (i % 3 == 0) ? "electronics" : (i % 3 == 1 ? "books" : "clothing");
                prices[i] = 10.0 + (i % 100);

                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                Normalize(vec);
                vecCol.AddVector(vec);
            }

            var df = new DataFrame(
                new DataColumn<int>("id", ids),
                new DataColumn<string>("category", categories),
                new DataColumn<double>("price", prices),
                vecCol
            );

            // Filter condition: category == "electronics" AND price < 50
            var mask = new SelectionMask(count);
            for (int i = 0; i < count; i++)
            {
                if (categories[i] == "electronics" && prices[i] < 50.0)
                {
                    mask.SetSelected(i, true);
                }
            }

            // Target vector from an electronic item
            var query = vecCol.GetVector(3); // row 3 is electronics (3 % 3 == 0)

            var matches = df.SearchVector("embedding", query, topK: 5, mask);

            Assert.NotEmpty(matches);
            foreach (var match in matches)
            {
                int r = match.RowIndex;
                Assert.Equal("electronics", categories[r]);
                Assert.True(prices[r] < 50.0);
            }
        }

        [Fact]
        public void DataFrame_SearchVector_WithRowPredicate()
        {
            int count = 300;
            int dim = 64;
            var rand = new Random(2026);

            var categories = new string[count];
            var vecCol = new VectorColumn("vec", dim);

            for (int i = 0; i < count; i++)
            {
                categories[i] = (i % 2 == 0) ? "hardware" : "software";
                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                Normalize(vec);
                vecCol.AddVector(vec);
            }

            var df = new DataFrame(
                new DataColumn<string>("cat", categories),
                vecCol
            );

            var query = vecCol.GetVector(10); // hardware

            // Query only software rows
            var matches = df.SearchVector("vec", query, topK: 5, row => row.GetString("cat") == "software");

            Assert.NotEmpty(matches);
            foreach (var match in matches)
            {
                int r = match.RowIndex;
                Assert.Equal("software", categories[r]);
            }
        }

        [Fact]
        public void DataFrame_FilterByVector_MaterializesRankedDataFrame()
        {
            int count = 100;
            int dim = 64;
            var rand = new Random(333);

            var ids = new int[count];
            var vecCol = new VectorColumn("feat", dim);

            for (int i = 0; i < count; i++)
            {
                ids[i] = i * 10;
                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                Normalize(vec);
                vecCol.AddVector(vec);
            }

            var df = new DataFrame(new DataColumn<int>("id", ids), vecCol);
            var query = vecCol.GetVector(15);

            var filteredDf = df.FilterByVector("feat", query, topK: 3);

            Assert.Equal(3, filteredDf.RowCount);
            Assert.Equal(150, filteredDf.Column<int>("id")[0]); // row 15 has id 150
        }

        [Fact]
        public void VectorColumn_NullHandling_SkipsNullRowsInSearch()
        {
            int dim = 32;
            var vecCol = new VectorColumn("vec", dim);

            for (int i = 0; i < 10; i++)
            {
                float[] v = new float[dim];
                for (int d = 0; d < dim; d++) v[d] = 1.0f;
                Normalize(v);
                vecCol.AddVector(v);
            }

            // Set row 5 as null
            vecCol.SetNull(5);
            Assert.True(vecCol.IsNull(5));

            var query = vecCol.GetVector(0);
            var matches = vecCol.SearchNearest(query, topK: 10);

            Assert.DoesNotContain(matches, m => m.RowIndex == 5);
        }

        [Fact]
        public void VectorColumn_Performance_FusedSearchThroughputBenchmark()
        {
            int count = 2000;
            int dim = 128;
            var rand = new Random(777);

            var vecCol = new VectorColumn("emb", dim);
            for (int i = 0; i < count; i++)
            {
                float[] v = new float[dim];
                for (int d = 0; d < dim; d++) v[d] = (float)(rand.NextDouble() * 2.0 - 1.0);
                Normalize(v);
                vecCol.AddVector(v);
            }

            var query = vecCol.GetVector(50);

            // Warmup
            vecCol.SearchNearest(query, 10);

            int iterations = 500;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                vecCol.SearchNearest(query, 10);
            }
            sw.Stop();

            double qps = iterations / sw.Elapsed.TotalSeconds;
            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;

            Assert.True(qps > 500.0, $"Expected QPS > 500, got {qps:F1} QPS ({avgMs:F3} ms/query)");
        }

        private static void Normalize(Span<float> vec)
        {
            float sumSq = 0f;
            for (int i = 0; i < vec.Length; i++) sumSq += vec[i] * vec[i];
            if (sumSq <= 1e-12f) return;
            float inv = 1.0f / (float)Math.Sqrt(sumSq);
            for (int i = 0; i < vec.Length; i++) vec[i] *= inv;
        }
    }
}
