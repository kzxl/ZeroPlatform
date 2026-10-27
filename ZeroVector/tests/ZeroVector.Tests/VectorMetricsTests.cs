using System;
using Xunit;
using ZeroVector.Core.Metrics;

namespace ZeroVector.Tests
{
    public class VectorMetricsTests
    {
        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(384)]
        [InlineData(512)]
        [InlineData(1536)]
        public unsafe void DotProduct_SimdMatchesScalar_AcrossDimensions(int dimension)
        {
            var rand = new Random(1234);
            var a = new float[dimension];
            var b = new float[dimension];
            for (int i = 0; i < dimension; i++)
            {
                a[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                b[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
            }

            float simd = VectorMetrics.DotProduct(a, b);
            float scalar;
            fixed (float* pA = a, pB = b)
            {
                scalar = VectorMetrics.DotProductScalar(pA, pB, dimension);
            }

            Assert.Equal(scalar, simd, precision: 4);
        }

        [Fact]
        public unsafe void CosineSimilarity_SatisfiesInvariantsAndMatchesScalar()
        {
            int dim = 384;
            var rand = new Random(5678);
            var a = new float[dim];
            var b = new float[dim];
            var negA = new float[dim];
            var zeros = new float[dim];

            for (int i = 0; i < dim; i++)
            {
                a[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                b[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                negA[i] = -a[i];
            }

            // Invariants
            float selfCos = VectorMetrics.CosineSimilarity(a, a);
            Assert.True(Math.Abs(selfCos - 1.0f) < 1e-5f, $"Self-similarity should be 1.0 but got {selfCos}");

            float negCos = VectorMetrics.CosineSimilarity(a, negA);
            Assert.True(Math.Abs(negCos - (-1.0f)) < 1e-5f, $"Opposite similarity should be -1.0 but got {negCos}");

            float zeroCos = VectorMetrics.CosineSimilarity(a, zeros);
            Assert.Equal(0.0f, zeroCos);

            // Parity with scalar
            float simdCos = VectorMetrics.CosineSimilarity(a, b);
            float scalarCos;
            fixed (float* pA = a, pB = b)
            {
                scalarCos = VectorMetrics.CosineSimilarityScalar(pA, pB, dim);
            }
            Assert.Equal(scalarCos, simdCos, precision: 5);
        }

        [Fact]
        public unsafe void EuclideanDistance_MatchesScalarAndTriangleInequality()
        {
            int dim = 128;
            var rand = new Random(999);
            var a = new float[dim];
            var b = new float[dim];
            var c = new float[dim];

            for (int i = 0; i < dim; i++)
            {
                a[i] = (float)(rand.NextDouble() * 10.0);
                b[i] = (float)(rand.NextDouble() * 10.0);
                c[i] = (float)(rand.NextDouble() * 10.0);
            }

            Assert.Equal(0.0f, VectorMetrics.EuclideanDistance(a, a), precision: 5);

            float dab = VectorMetrics.EuclideanDistance(a, b);
            float dba = VectorMetrics.EuclideanDistance(b, a);
            Assert.Equal(dab, dba, precision: 5);

            // Triangle inequality: d(a, c) <= d(a, b) + d(b, c)
            float dbc = VectorMetrics.EuclideanDistance(b, c);
            float dac = VectorMetrics.EuclideanDistance(a, c);
            Assert.True(dac <= dab + dbc + 1e-4f);

            // Parity
            float simdSq = VectorMetrics.EuclideanDistanceSquared(a, b);
            float scalarSq;
            fixed (float* pA = a, pB = b)
            {
                scalarSq = VectorMetrics.EuclideanDistanceSquaredScalar(pA, pB, dim);
            }
            Assert.Equal(scalarSq, simdSq, precision: 3);
        }

        [Fact]
        public void ManhattanDistance_CalculatesL1NormAccurately()
        {
            float[] a = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };
            float[] b = new float[] { 2f, 2f, 5f, 1f, 8f, 6f, 10f, 6f, 11f };
            // abs diffs: 1 + 0 + 2 + 3 + 3 + 0 + 3 + 2 + 2 = 16

            float dist = VectorMetrics.ManhattanDistance(a, b);
            Assert.Equal(16.0f, dist, precision: 5);
        }

        [Fact]
        public void HammingDistance_CalculatesCorrectPopCount()
        {
            byte[] a = new byte[] { 0b00000000, 0b11110000, 0b10101010 };
            byte[] b = new byte[] { 0b00000001, 0b11110000, 0b01010101 };
            // diff: 1 bit + 0 bits + 8 bits = 9 bits

            int dist = VectorMetrics.HammingDistance(a, b);
            Assert.Equal(9, dist);
        }

        [Fact]
        public void NormalizeL2_YieldsUnitLengthVector()
        {
            float[] vec = new float[] { 3.0f, 4.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 12.0f };
            // norm = sqrt(9 + 16 + 144) = sqrt(169) = 13
            VectorMetrics.NormalizeL2(vec);

            Assert.Equal(3.0f / 13.0f, vec[0], precision: 5);
            Assert.Equal(4.0f / 13.0f, vec[1], precision: 5);
            Assert.Equal(12.0f / 13.0f, vec[8], precision: 5);

            float normSq = VectorMetrics.DotProduct(vec, vec);
            Assert.True(Math.Abs(normSq - 1.0f) < 1e-5f);
        }
    }
}
