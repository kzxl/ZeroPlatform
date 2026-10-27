using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroGraphics.Vision.Contours;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Tests
{
    public class VectorProcessingTests
    {
        // =========================================================================
        // 1. CubicBezier2D Tests
        // =========================================================================

        [Fact]
        public void CubicBezier2D_Evaluate_PreservesEndpointsAndCalculatesMidpoint()
        {
            var p0 = new PointF(0, 0);
            var p1 = new PointF(0, 100);
            var p2 = new PointF(100, 100);
            var p3 = new PointF(100, 0);

            var curve = new CubicBezier2D(p0, p1, p2, p3);

            // B(0) == P0
            var start = curve.Evaluate(0.0f);
            Assert.Equal(0.0f, start.X, precision: 4);
            Assert.Equal(0.0f, start.Y, precision: 4);

            // B(1) == P3
            var end = curve.Evaluate(1.0f);
            Assert.Equal(100.0f, end.X, precision: 4);
            Assert.Equal(0.0f, end.Y, precision: 4);

            // B(0.5) should match 1/8*P0 + 3/8*P1 + 3/8*P2 + 1/8*P3
            var mid = curve.Evaluate(0.5f);
            float expectedX = 0.125f * 0 + 0.375f * 0 + 0.375f * 100 + 0.125f * 100; // 50
            float expectedY = 0.125f * 0 + 0.375f * 100 + 0.375f * 100 + 0.125f * 0; // 75
            Assert.Equal(expectedX, mid.X, precision: 4);
            Assert.Equal(expectedY, mid.Y, precision: 4);
        }

        [Fact]
        public void CubicBezier2D_Derivative_MatchesAnalyticalTangents()
        {
            var p0 = new PointF(10, 20);
            var p1 = new PointF(30, 40);
            var p2 = new PointF(70, 80);
            var p3 = new PointF(100, 120);

            var curve = new CubicBezier2D(p0, p1, p2, p3);

            // B'(0) = 3 * (P1 - P0)
            var dStart = curve.Derivative(0.0f);
            Assert.Equal(3.0f * (30 - 10), dStart.X, precision: 4);
            Assert.Equal(3.0f * (40 - 20), dStart.Y, precision: 4);

            // B'(1) = 3 * (P3 - P2)
            var dEnd = curve.Derivative(1.0f);
            Assert.Equal(3.0f * (100 - 70), dEnd.X, precision: 4);
            Assert.Equal(3.0f * (120 - 80), dEnd.Y, precision: 4);
        }

        [Fact]
        public void CubicBezier2D_Split_DeCasteljau_YieldsIdenticalGeometry()
        {
            var curve = new CubicBezier2D(
                new PointF(0, 0),
                new PointF(20, 80),
                new PointF(80, 80),
                new PointF(100, 0));

            curve.Split(0.5f, out var left, out var right);

            // Left end meets right start at midpoint
            var mid = curve.Evaluate(0.5f);
            Assert.Equal(mid.X, left.P3.X, precision: 4);
            Assert.Equal(mid.Y, left.P3.Y, precision: 4);
            Assert.Equal(mid.X, right.P0.X, precision: 4);
            Assert.Equal(mid.Y, right.P0.Y, precision: 4);

            // Intermediate points match
            var ptAtQuarter = curve.Evaluate(0.25f);
            var leftMid = left.Evaluate(0.5f);
            Assert.Equal(ptAtQuarter.X, leftMid.X, precision: 3);
            Assert.Equal(ptAtQuarter.Y, leftMid.Y, precision: 3);

            var ptAtThreeQuarter = curve.Evaluate(0.75f);
            var rightMid = right.Evaluate(0.5f);
            Assert.Equal(ptAtThreeQuarter.X, rightMid.X, precision: 3);
            Assert.Equal(ptAtThreeQuarter.Y, rightMid.Y, precision: 3);
        }

        [Fact]
        public void CubicBezier2D_BoundingBox_And_EstimateLength_AreConsistent()
        {
            // Straight line from (0, 0) to (300, 400) -> Euclidean length = 500
            var straight = new CubicBezier2D(
                new PointF(0, 0),
                new PointF(100, 133.333f),
                new PointF(200, 266.666f),
                new PointF(300, 400));

            float length = straight.EstimateLength(20);
            Assert.InRange(length, 499.5f, 500.5f);

            var bbox = straight.GetBoundingBox();
            Assert.Equal(0.0f, bbox.Left, precision: 3);
            Assert.Equal(0.0f, bbox.Top, precision: 3);
            Assert.Equal(300.0f, bbox.Right, precision: 3);
            Assert.Equal(400.0f, bbox.Bottom, precision: 3);
        }

        // =========================================================================
        // 2. BezierCurveFitter (Raster-to-Vector) Tests
        // =========================================================================

        [Fact]
        public void BezierCurveFitter_FitStraightLine_ProducesSingleSegmentWithZeroError()
        {
            var points = new List<PointF>();
            for (int i = 0; i <= 20; i++)
            {
                points.Add(new PointF(i * 10.0f, i * 5.0f));
            }

            var path = BezierCurveFitter.Fit(points, errorTolerance: 0.1);
            Assert.NotNull(path);
            Assert.Single(path.Curves);

            var curve = path.Curves[0];
            Assert.Equal(0.0f, curve.P0.X, precision: 3);
            Assert.Equal(0.0f, curve.P0.Y, precision: 3);
            Assert.Equal(200.0f, curve.P3.X, precision: 3);
            Assert.Equal(100.0f, curve.P3.Y, precision: 3);

            string svgPath = path.ToSvgPathData();
            Assert.StartsWith("M 0 0 C", svgPath);
        }

        [Fact]
        public void BezierCurveFitter_FitCircularArc_ApproximatesWithinTolerance()
        {
            // 90-degree circular arc with radius R = 100, centered at (0, 0)
            var points = new List<PointF>();
            int numPoints = 25;
            for (int i = 0; i < numPoints; i++)
            {
                double theta = (Math.PI / 2.0) * (i / (double)(numPoints - 1));
                points.Add(new PointF((float)(100.0 * Math.Cos(theta)), (float)(100.0 * Math.Sin(theta))));
            }

            double tolerance = 1.0;
            var path = BezierCurveFitter.Fit(points, errorTolerance: tolerance);
            Assert.NotNull(path);
            Assert.True(path.Curves.Count >= 1 && path.Curves.Count <= 4);

            // Validate that every original sample point is within tolerance of the fitted path
            foreach (var pt in points)
            {
                float minDist = float.MaxValue;
                foreach (var curve in path.Curves)
                {
                    for (int s = 0; s <= 200; s++)
                    {
                        var curvePt = curve.Evaluate(s / 200.0f);
                        float dist = (float)Math.Sqrt(
                            (curvePt.X - pt.X) * (curvePt.X - pt.X) +
                            (curvePt.Y - pt.Y) * (curvePt.Y - pt.Y));
                        if (dist < minDist) minDist = dist;
                    }
                }

                Assert.True(minDist <= tolerance + 0.25f, $"Point ({pt.X}, {pt.Y}) deviated by {minDist} > tolerance {tolerance}");
            }

            string svg = path.ToSvg(200, 200);
            Assert.Contains("<svg", svg);
            Assert.Contains("<path d=", svg);
        }

        [Fact]
        public void BezierCurveFitter_ClosedContour_SetsIsClosedAndOutputsZ()
        {
            var points = new List<Point>
            {
                new Point(0, 0),
                new Point(100, 0),
                new Point(100, 100),
                new Point(0, 100),
                new Point(0, 0) // Closed
            };

            var path = BezierCurveFitter.Fit(points, errorTolerance: 1.0);
            Assert.True(path.IsClosed);

            string d = path.ToSvgPathData();
            Assert.EndsWith("Z", d.Trim());
        }

        // =========================================================================
        // 3. VectorMetrics (SIMD & Parity) Tests
        // =========================================================================

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(73)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(511)]
        [InlineData(512)]
        [InlineData(1024)]
        public unsafe void VectorMetrics_DotProduct_SimdMatchesScalar_AcrossDimensions(int dimension)
        {
            var rnd = new Random(42 + dimension);
            var a = new float[dimension];
            var b = new float[dimension];

            for (int i = 0; i < dimension; i++)
            {
                a[i] = (float)(rnd.NextDouble() * 20.0 - 10.0);
                b[i] = (float)(rnd.NextDouble() * 20.0 - 10.0);
            }

            float simd = VectorMetrics.DotProduct(a, b);
            float scalar;
            fixed (float* pA = a, pB = b)
            {
                scalar = VectorMetrics.DotProductScalar(pA, pB, dimension);
            }

            float diff = Math.Abs(simd - scalar);
            float maxVal = Math.Max(Math.Abs(simd), Math.Abs(scalar));
            float relError = maxVal > 1e-5f ? diff / maxVal : diff;

            Assert.True(relError < 1e-4f, $"DotProduct mismatch at dim {dimension}: SIMD={simd}, Scalar={scalar}, relErr={relError}");
        }

        [Fact]
        public unsafe void VectorMetrics_CosineSimilarity_SatisfiesInvariantsAndMatchesScalar()
        {
            int dim = 128;
            var rnd = new Random(1337);
            var a = new float[dim];
            var b = new float[dim];

            for (int i = 0; i < dim; i++)
            {
                a[i] = (float)(rnd.NextDouble() * 10.0 - 5.0);
                b[i] = (float)(rnd.NextDouble() * 10.0 - 5.0);
            }

            // Identical vectors -> Cosine = 1.0
            float selfCos = VectorMetrics.CosineSimilarity(a, a);
            Assert.Equal(1.0f, selfCos, precision: 5);

            // Opposite vectors -> Cosine = -1.0
            var negA = new float[dim];
            for (int i = 0; i < dim; i++) negA[i] = -a[i];
            float negCos = VectorMetrics.CosineSimilarity(a, negA);
            Assert.Equal(-1.0f, negCos, precision: 5);

            // Zero vector handled gracefully
            var zeros = new float[dim];
            float zeroCos = VectorMetrics.CosineSimilarity(a, zeros);
            Assert.Equal(0.0f, zeroCos);

            // General random vector SIMD vs Scalar parity
            float simdCos = VectorMetrics.CosineSimilarity(a, b);
            float scalarCos;
            fixed (float* pA = a, pB = b)
            {
                scalarCos = VectorMetrics.CosineSimilarityScalar(pA, pB, dim);
            }

            Assert.Equal(scalarCos, simdCos, precision: 4);
        }

        [Fact]
        public unsafe void VectorMetrics_EuclideanDistance_MatchesScalarAndTriangleInequality()
        {
            int dim = 64;
            var rnd = new Random(2026);
            var a = new float[dim];
            var b = new float[dim];
            var c = new float[dim];

            for (int i = 0; i < dim; i++)
            {
                a[i] = (float)rnd.NextDouble() * 10f;
                b[i] = (float)rnd.NextDouble() * 10f;
                c[i] = (float)rnd.NextDouble() * 10f;
            }

            // Distance to self == 0
            Assert.Equal(0.0f, VectorMetrics.EuclideanDistance(a, a), precision: 5);

            // Symmetry: d(A, B) == d(B, A)
            float dab = VectorMetrics.EuclideanDistance(a, b);
            float dba = VectorMetrics.EuclideanDistance(b, a);
            Assert.Equal(dab, dba, precision: 5);

            // Triangle Inequality: d(A, C) <= d(A, B) + d(B, C)
            float dbc = VectorMetrics.EuclideanDistance(b, c);
            float dac = VectorMetrics.EuclideanDistance(a, c);
            Assert.True(dac <= dab + dbc + 1e-4f, $"Triangle inequality failed: dac={dac}, dab+dbc={dab + dbc}");

            // Parity with scalar
            float simdSq = VectorMetrics.EuclideanDistanceSquared(a, b);
            float scalarSq;
            fixed (float* pA = a, pB = b)
            {
                scalarSq = VectorMetrics.EuclideanDistanceSquaredScalar(pA, pB, dim);
            }
            Assert.Equal(scalarSq, simdSq, precision: 3);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(15)]
        [InlineData(32)] // Standard ORB 256-bit descriptor
        [InlineData(64)] // Standard 512-bit descriptor
        [InlineData(65)]
        public void VectorMetrics_HammingDistance_CalculatesCorrectPopCount(int bytesCount)
        {
            var a = new byte[bytesCount];
            var b = new byte[bytesCount];

            // Identical -> 0
            Assert.Equal(0, VectorMetrics.HammingDistance(a, b));

            // Single bit flip in first byte
            b[0] ^= 0x01;
            Assert.Equal(1, VectorMetrics.HammingDistance(a, b));

            // Single bit flip in last byte
            b[bytesCount - 1] ^= 0x80;
            Assert.Equal(bytesCount == 1 ? 2 : 2, VectorMetrics.HammingDistance(a, b));

            // Alternating pattern 0xAA vs 0x55 (all 8 bits differ in every byte)
            for (int i = 0; i < bytesCount; i++)
            {
                a[i] = 0xAA; // 10101010
                b[i] = 0x55; // 01010101
            }

            int expected = bytesCount * 8;
            Assert.Equal(expected, VectorMetrics.HammingDistance(a, b));
        }

        [Fact]
        public void VectorMetrics_NormalizeL2_YieldsUnitLengthVector()
        {
            var vec = new float[] { 3.0f, 4.0f }; // Length = 5
            VectorMetrics.NormalizeL2(vec);

            Assert.Equal(0.6f, vec[0], precision: 5);
            Assert.Equal(0.8f, vec[1], precision: 5);

            float normSq = VectorMetrics.DotProduct(vec, vec);
            Assert.Equal(1.0f, normSq, precision: 5);
        }

        // =========================================================================
        // 4. FeatureVectorIndex Tests
        // =========================================================================

        [Fact]
        public void FeatureVectorIndex_AddAndSearchTopK_ReturnsExactMatchFirst()
        {
            int dim = 128;
            var index = new FeatureVectorIndex(dim);
            var rnd = new Random(777);

            // Insert 50 vectors
            for (int id = 1; id <= 50; id++)
            {
                var vec = new float[dim];
                for (int i = 0; i < dim; i++)
                {
                    vec[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);
                index.Add(id, vec);
            }

            Assert.Equal(50, index.Count);

            // Construct exact query matching id #28
            var query = new float[dim];
            rnd = new Random(777);
            for (int id = 1; id <= 28; id++)
            {
                for (int i = 0; i < dim; i++)
                {
                    query[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(query);
            }

            // Search Top 5 with Cosine
            var top5 = index.SearchTopK(query, 5, VectorMetricType.Cosine);
            Assert.Equal(5, top5.Length);
            Assert.Equal(28, top5[0].Id);
            Assert.Equal(1.0f, top5[0].Score, precision: 4);

            // Verify scores are descending
            for (int i = 1; i < top5.Length; i++)
            {
                Assert.True(top5[i - 1].Score >= top5[i].Score, "Scores must be in descending order.");
            }

            // Search Top 5 with Euclidean (where exact match has distance 0.0)
            var top5Euclid = index.SearchTopK(query, 5, VectorMetricType.EuclideanSquared);
            Assert.Equal(28, top5Euclid[0].Id);
            Assert.Equal(0.0f, top5Euclid[0].Score, precision: 4);

            // Verify Euclidean distances are ascending
            for (int i = 1; i < top5Euclid.Length; i++)
            {
                Assert.True(top5Euclid[i - 1].Score <= top5Euclid[i].Score, "Euclidean distances must be ascending.");
            }
        }
    }
}
