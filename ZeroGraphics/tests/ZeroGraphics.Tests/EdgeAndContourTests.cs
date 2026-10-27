using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Contours;
using ZeroGraphics.Vision.Edge;

namespace ZeroGraphics.Tests
{
    public class EdgeAndContourTests
    {
        [Fact]
        public unsafe void TestCannyEdgeDetector_SyntheticRectangle()
        {
            int w = 100;
            int h = 100;
            using (var img = ImageBuffer.CreateGray8(w, h))
            {
                img.Clear(0);

                // Draw solid rectangle [20..79, 20..79]
                for (int y = 20; y < 80; y++)
                {
                    byte* row = img.GetRowPointer(y);
                    for (int x = 20; x < 80; x++)
                    {
                        row[x] = 200;
                    }
                }

                using (var edges = CannyEdgeDetector.Detect(img, lowThreshold: 30, highThreshold: 80, applyGaussian: false))
                {
                    Assert.Equal(w, edges.Width);
                    Assert.Equal(h, edges.Height);

                    // Top edge should have edge pixels near y = 20
                    bool topEdgeFound = false;
                    for (int y = 19; y <= 21; y++)
                    {
                        byte* row = edges.GetRowPointer(y);
                        if (row[50] == 255) topEdgeFound = true;
                    }
                    Assert.True(topEdgeFound, "Top horizontal edge must be detected.");

                    // Center should be empty (0)
                    byte* centerRow = edges.GetRowPointer(50);
                    Assert.Equal(0, centerRow[50]);
                }
            }
        }

        [Fact]
        public unsafe void TestZernikeEdgeDetector_SubPixelAccuracy()
        {
            int w = 50;
            int h = 50;
            using (var img = ImageBuffer.CreateGray8(w, h))
            {
                img.Clear(50);

                // Ideal vertical edge at x = 25.3
                double trueEdgeX = 25.3;
                for (int y = 0; y < h; y++)
                {
                    byte* row = img.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (x < 25)
                            row[x] = 40;
                        else if (x == 25)
                            row[x] = (byte)(40 + (220 - 40) * (trueEdgeX - 25.0)); // anti-aliased partial pixel
                        else
                            row[x] = 220;
                    }
                }

                // Refine at (25, 25)
                bool found = ZernikeEdgeDetector.RefineEdge(img, 25, 25, out var edge, minContrast: 50.0);
                Assert.True(found);
                Assert.True(edge.Contrast >= 50.0);
                Assert.InRange(edge.X, 25.1, 25.5); // Sub-pixel precision within 0.2 pixels
            }
        }

        [Fact]
        public unsafe void TestContourTracer_OuterAndHole()
        {
            int w = 60;
            int h = 60;
            using (var img = ImageBuffer.CreateGray8(w, h))
            {
                img.Clear(0);

                // Outer square [10..49, 10..49] with an inner hole [25..34, 25..34]
                for (int y = 10; y < 50; y++)
                {
                    byte* row = img.GetRowPointer(y);
                    for (int x = 10; x < 50; x++)
                    {
                        if (x >= 25 && x < 35 && y >= 25 && y < 35)
                            row[x] = 0; // hole
                        else
                            row[x] = 255; // solid
                    }
                }

                var contours = ContourTracer.FindContours(img, minPoints: 8);
                Assert.True(contours.Count >= 2, "Must detect at least 1 outer contour and 1 hole contour.");

                bool hasOuter = false;
                bool hasHole = false;
                foreach (var c in contours)
                {
                    if (!c.IsHole) hasOuter = true;
                    if (c.IsHole) hasHole = true;
                }

                Assert.True(hasOuter, "Must contain outer boundary.");
                Assert.True(hasHole, "Must contain hole boundary.");
            }
        }

        [Fact]
        public void TestContourFeatures_Calculations()
        {
            // 20x20 Square: (10, 10) to (30, 30)
            var square = new List<Point>
            {
                new Point(10, 10),
                new Point(30, 10),
                new Point(30, 30),
                new Point(10, 30)
            };

            // Area
            double area = ContourFeatures.ComputeArea(square);
            Assert.Equal(400.0, area, precision: 2);

            // Perimeter
            double perimeter = ContourFeatures.ComputePerimeter(square);
            Assert.Equal(80.0, perimeter, precision: 2);

            // Centroid
            var centroid = ContourFeatures.ComputeCentroid(square);
            Assert.Equal(20.0f, centroid.X, precision: 2);
            Assert.Equal(20.0f, centroid.Y, precision: 2);

            // Bounding Box
            var bbox = ContourFeatures.ComputeBoundingBox(square);
            Assert.Equal(10, bbox.X);
            Assert.Equal(10, bbox.Y);
            Assert.Equal(21, bbox.Width);
            Assert.Equal(21, bbox.Height);

            // Point In Polygon
            Assert.True(ContourFeatures.ContainsPoint(square, 20, 20));
            Assert.False(ContourFeatures.ContainsPoint(square, 5, 20));
            Assert.False(ContourFeatures.ContainsPoint(square, 35, 20));

            // RDP Polygon Approximation
            // Add colinear points along top and bottom edges
            var denseSquare = new List<Point>
            {
                new Point(10, 10),
                new Point(15, 10),
                new Point(20, 10),
                new Point(25, 10),
                new Point(30, 10),
                new Point(30, 30),
                new Point(10, 30)
            };

            var simplified = ContourFeatures.ApproximatePolygon(denseSquare, epsilon: 1.0);
            // Colinear points along (10,10)-(30,10) should be simplified away
            Assert.True(simplified.Count < denseSquare.Count);
        }
    }
}
