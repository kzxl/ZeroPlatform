using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Rendering;
using ZeroGraphics.Vector.Tessellation;

namespace ZeroGraphics.Tests
{
    public class VectorGraphicsEngineTests
    {
        // =========================================================================
        // 1. Path2D & Geometry Tests
        // =========================================================================

        [Fact]
        public void Path2D_VerbsAndPoints_RecordsAndBoundsCorrectly()
        {
            var path = new Path2D();
            path.MoveTo(10, 20)
                .LineTo(50, 60)
                .QuadTo(80, 20, 100, 60)
                .CubicTo(120, 80, 140, 40, 160, 100)
                .Close();

            Assert.Equal(5, path.VerbCount);
            Assert.Equal(1 + 1 + 2 + 3, path.PointCount);

            path.GetBounds(out float minX, out float minY, out float maxX, out float maxY);
            Assert.Equal(10.0f, minX, precision: 3);
            Assert.Equal(20.0f, minY, precision: 3);
            Assert.Equal(160.0f, maxX, precision: 3);
            Assert.Equal(100.0f, maxY, precision: 3);

            string svgPath = path.ToSvgPathData();
            Assert.Contains("M 10 20", svgPath);
            Assert.Contains("L 50 60", svgPath);
            Assert.Contains("Q 80 20, 100 60", svgPath);
            Assert.Contains("C 120 80, 140 40, 160 100", svgPath);
            Assert.EndsWith("Z", svgPath);
        }

        [Fact]
        public void Path2D_AddRectAndAddCircle_ConstructsExpectedVerbs()
        {
            var rectPath = new Path2D();
            rectPath.AddRect(0, 0, 200, 100);

            // MoveTo + 3 LineTos + Close = 5 verbs
            Assert.Equal(5, rectPath.VerbCount);
            rectPath.GetBounds(out float rx0, out float ry0, out float rx1, out float ry1);
            Assert.Equal(0.0f, rx0, precision: 3);
            Assert.Equal(0.0f, ry0, precision: 3);
            Assert.Equal(200.0f, rx1, precision: 3);
            Assert.Equal(100.0f, ry1, precision: 3);

            var circlePath = new Path2D();
            circlePath.AddCircle(100, 100, 50);
            // MoveTo + 4 CubicTos + Close = 6 verbs
            Assert.Equal(6, circlePath.VerbCount);
            circlePath.GetBounds(out float cx0, out float cy0, out float cx1, out float cy1);
            Assert.InRange(cx0, 49.0f, 51.0f);
            Assert.InRange(cy0, 49.0f, 51.0f);
            Assert.InRange(cx1, 149.0f, 151.0f);
            Assert.InRange(cy1, 149.0f, 151.0f);
        }

        // =========================================================================
        // 2. Adaptive Flattening Tests
        // =========================================================================

        [Fact]
        public void AdaptiveFlattening_FlattensCubicAndQuadratic_WithSubPixelAccuracy()
        {
            var path = new Path2D();
            path.MoveTo(0, 0)
                .QuadTo(50, 100, 100, 0)
                .CubicTo(150, -100, 200, 100, 250, 0);

            var subpaths = AdaptiveFlattening.Flatten(path, tolerance: 0.25f);

            Assert.Single(subpaths);
            var polyline = subpaths[0];

            // Should have multiple subdivided vertices along the curves
            Assert.True(polyline.Count >= 10);
            Assert.Equal(0.0f, polyline[0].X, precision: 3);
            Assert.Equal(0.0f, polyline[0].Y, precision: 3);
            Assert.Equal(250.0f, polyline[polyline.Count - 1].X, precision: 3);
            Assert.Equal(0.0f, polyline[polyline.Count - 1].Y, precision: 3);
        }

        // =========================================================================
        // 3. PathStroker Tests
        // =========================================================================

        [Theory]
        [InlineData(LineCap.Butt, LineJoin.Miter)]
        [InlineData(LineCap.Square, LineJoin.Bevel)]
        [InlineData(LineCap.Round, LineJoin.Round)]
        public void PathStroker_StrokePath_GeneratesValidTriangleMeshes(LineCap cap, LineJoin join)
        {
            var path = new Path2D();
            path.MoveTo(10, 10)
                .LineTo(100, 50)
                .LineTo(50, 100);

            var stroke = new StrokeStyle(6.0f, cap, join);
            var mesh = new VectorMesh();

            PathStroker.StrokePath(path, stroke, 0xFFFFFFFF, mesh);

            Assert.True(mesh.VertexCount >= 4);
            Assert.True(mesh.IndexCount >= 6);
            Assert.Equal(0, mesh.IndexCount % 3); // Valid triangles
        }

        // =========================================================================
        // 4. PolygonTriangulator Tests
        // =========================================================================

        [Fact]
        public void PolygonTriangulator_TriangulatesConvexAndConcavePolygons()
        {
            var mesh = new VectorMesh();

            // Convex quad (rectangle): 4 vertices -> 2 triangles = 6 indices
            var rect = new List<VectorPoint>
            {
                new VectorPoint(0, 0),
                new VectorPoint(100, 0),
                new VectorPoint(100, 50),
                new VectorPoint(0, 50)
            };
            PolygonTriangulator.TriangulatePolygon(rect, 0xFF00FF00, mesh);
            Assert.Equal(4, mesh.VertexCount);
            Assert.Equal(6, mesh.IndexCount);

            mesh.Clear();

            // Concave polygon (L-shape with 6 vertices): (N - 2) = 4 triangles = 12 indices
            var lShape = new List<VectorPoint>
            {
                new VectorPoint(0, 0),
                new VectorPoint(100, 0),
                new VectorPoint(100, 30),
                new VectorPoint(30, 30),
                new VectorPoint(30, 100),
                new VectorPoint(0, 100)
            };
            PolygonTriangulator.TriangulatePolygon(lShape, 0xFFFF0000, mesh);
            Assert.Equal(6, mesh.VertexCount);
            Assert.Equal(12, mesh.IndexCount);
        }

        // =========================================================================
        // 5. SvgExporter Tests
        // =========================================================================

        [Fact]
        public void SvgExporter_GeneratesValidSvgDocument()
        {
            var path = new Path2D();
            path.AddCircle(50, 50, 40);

            string svg = SvgExporter.ExportSinglePath(path, 100, 100, new StrokeStyle(2.0f), "#2196F3", "#E3F2FD");

            Assert.Contains("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg);
            Assert.Contains("viewBox=\"0 0 100 100\"", svg);
            Assert.Contains("stroke=\"#2196F3\"", svg);
            Assert.Contains("fill=\"#E3F2FD\"", svg);
            Assert.EndsWith("</svg>", svg.Trim());
        }

        // =========================================================================
        // 6. VectorRenderer & RHI Dispatch Tests
        // =========================================================================

        [Fact]
        public void VectorRenderer_DispatchesBatchesToNullRhiDevice()
        {
            using var nullDevice = new NullRhiDevice();
            using var renderer = new VectorRenderer(nullDevice);

            // Draw various shapes
            renderer.DrawLine(0, 0, 100, 100, new StrokeStyle(2.0f), 0xFFFFFFFF);
            renderer.FillRect(20, 20, 80, 80, 0xFF00FF00);
            renderer.DrawCircle(150, 150, 30, new StrokeStyle(1.5f), 0xFFFF0000);
            renderer.FillCircle(250, 250, 40, 0xFF0000FF, segments: 16);

            Assert.True(renderer.Mesh.VertexCount > 0);
            Assert.True(renderer.Mesh.IndexCount > 0);

            // Flush to command buffer
            using var cmd = nullDevice.CreateCommandBuffer();
            cmd.Begin();
            renderer.Flush(cmd);
            cmd.End();

            // After flush, mesh is cleared and ready for next frame
            Assert.Equal(0, renderer.Mesh.VertexCount);
            Assert.Equal(0, renderer.Mesh.IndexCount);
        }
    }
}
