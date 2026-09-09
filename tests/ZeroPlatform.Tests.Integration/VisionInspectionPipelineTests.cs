using System;
using System.Collections.Generic;
using Xunit;
using ZeroCompute.Core.Blas;
using ZeroCompute.Core.Context;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Polygons;
using ZeroGeometry.Core.Spatial;
using ZeroInference.Core.Vision;
using ZeroTensor.Core;

namespace ZeroPlatform.Tests.Integration
{
    public class VisionInspectionPipelineTests
    {
        [Fact]
        public void VisionInspection_ComputeToGeometry_EndToEnd()
        {
            // =========================================================================
            // 1. Multidimensional Tensor Representation (ZeroTensor)
            // =========================================================================
            // Simulate camera feature map (M = 16 instances, K = 32 features)
            int M = 16;
            int K = 32;
            int N = 8; // Project down to 8 spatial feature outputs

            var inputFeatures = new float[M * K];
            for (int i = 0; i < inputFeatures.Length; i++)
            {
                inputFeatures[i] = (float)Math.Sin(i * 0.1);
            }
            var tensorA = Tensor.FromArray(inputFeatures, M, K);

            var weights = new float[K * N];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 0.05f * (i % 5);
            }
            var tensorB = Tensor.FromArray(weights, K, N);
            var tensorC = Tensor.Zeros<float>(M, N);

            // =========================================================================
            // 2. Hardware Compute Acceleration (ZeroCompute - Tiled GEMM & BLAS)
            // =========================================================================
            // Compute C = A * B using Cache-Blocked Tiled GEMM
            BlasEngine.Gemm(tensorA, tensorB, tensorC);

            // Apply vectorized ReLU activation
            BlasEngine.Activation(tensorC, tensorC, ComputeActivationType.ReLU);

            for (int r = 0; r < M; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    Assert.True(tensorC[r, c] >= 0.0f, "ReLU must eliminate negative outputs");
                }
            }

            // =========================================================================
            // 3. Edge AI Vision Post-Processing (ZeroInference - FastNMS)
            // =========================================================================
            // Generate bounding boxes from feature detections
            var rawDetections = new List<BoundingBox>
            {
                new BoundingBox(10, 10, 50, 50, 0.95f, 1),
                new BoundingBox(12, 11, 49, 51, 0.88f, 1), // Overlapping redundant box
                new BoundingBox(150, 150, 60, 60, 0.91f, 1),
                new BoundingBox(152, 149, 59, 61, 0.75f, 1)  // Overlapping redundant box
            };

            var finalBoxes = NonMaximumSuppression.Filter(rawDetections, confidenceThreshold: 0.8f, iouThreshold: 0.5f);
            Assert.Equal(2, finalBoxes.Count); // 2 distinct physical parts identified

            // =========================================================================
            // 4. 3D Spatial Partitioning & Registration (ZeroGeometry)
            // =========================================================================
            // Part 1 inspection: 3D point cloud alignment via Arun SVD ICP
            var modelPoints = new List<Point3D>
            {
                new Point3D(0, 0, 0),
                new Point3D(10, 0, 0),
                new Point3D(10, 10, 0),
                new Point3D(0, 10, 0),
                new Point3D(5, 5, 10)
            };

            // Part positioned with translation (x+2, y+3, z+1)
            var observedPoints = new List<Point3D>();
            foreach (var pt in modelPoints)
            {
                observedPoints.Add(new Point3D(pt.X + 2.0, pt.Y + 3.0, pt.Z + 1.0));
            }

            // Build k-d tree to verify spatial indexing
            var kdTree = new KdTree3D(observedPoints);
            bool found = kdTree.NearestNeighbor(new Point3D(2.0, 3.0, 1.0), out var nearest, out _, out _);
            Assert.True(found);
            Assert.Equal(2.0, nearest.X, 3);
            Assert.Equal(3.0, nearest.Y, 3);
            Assert.Equal(1.0, nearest.Z, 3);

            // Execute ICP Registration
            var sourceCloud = new PointCloud3D(observedPoints);
            var targetCloud = new PointCloud3D(modelPoints);
            var icpResult = IcpRegistration.Align(sourceCloud, targetCloud, maxIterations: 30, tolerance: 1e-4);
            Assert.True(icpResult.FitnessRms < 1e-3, "ICP must achieve high registration convergence");
            // Translation should correspond to approximately (-2, -3, -1) to bring observed back to model
            Assert.InRange(icpResult.Translation.X, -2.1, -1.9);
            Assert.InRange(icpResult.Translation.Y, -3.1, -2.9);
            Assert.InRange(icpResult.Translation.Z, -1.1, -0.9);

            // =========================================================================
            // 5. 2D Metrology Polygon Inspection & Toolpath Offset (ZeroGeometry)
            // =========================================================================
            // Part outline polygon (100x100 square)
            var subjectPoly = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 100),
                new Point2D(0, 100)
            });

            // Camera Field-Of-View clip boundary
            var fovClipPoly = new Polygon2D(new[]
            {
                new Point2D(20, 20),
                new Point2D(120, 20),
                new Point2D(120, 80),
                new Point2D(20, 80)
            });

            // Sutherland-Hodgman convex polygon clipping
            var clipped = PolygonClipper.Intersect(subjectPoly, fovClipPoly);
            Assert.Equal(4, clipped.VertexCount);
            // Clipped area: [20..100] x [20..80] = 80 x 60 = 4800
            Assert.Equal(4800.0, clipped.Area(), 1);

            // Apply inward tolerance erosion offset (-2mm)
            var eroded = PolygonOffsetter.Offset(clipped, delta: -2.0);
            Assert.True(eroded.Area() < clipped.Area(), "Inward offset must reduce area");
            // Expected area: (80 - 4) * (60 - 4) = 76 * 56 = 4256
            Assert.InRange(eroded.Area(), 4200.0, 4300.0);
        }
    }
}
