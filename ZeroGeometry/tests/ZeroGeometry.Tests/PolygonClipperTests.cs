using System;
using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.Polygons;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Tests
{
    public class PolygonClipperTests
    {
        [Fact]
        public void Polygon_AreaCentroidAndPointInPolygon_WorkCorrectly()
        {
            // Unit square: (0,0), (10,0), (10,10), (0,10)
            var square = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10)
            });

            Assert.Equal(100.0, square.Area());
            Assert.False(square.IsClockwise()); // CCW orientation
            Assert.Equal(40.0, square.Perimeter());

            var c = square.Centroid();
            Assert.InRange(c.X, 4.99, 5.01);
            Assert.InRange(c.Y, 4.99, 5.01);

            Assert.True(square.ContainsPoint(new Point2D(5, 5)));
            Assert.True(square.ContainsPoint(new Point2D(1, 1)));
            Assert.False(square.ContainsPoint(new Point2D(15, 5)));
            Assert.False(square.ContainsPoint(new Point2D(-2, 5)));
        }

        [Fact]
        public void PolygonClipper_Intersect_ComputesExactOverlap()
        {
            // Subject square: [0, 10] x [0, 10]
            var subject = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10)
            });

            // Clip square: [4, 14] x [4, 14]
            var clip = new Polygon2D(new[]
            {
                new Point2D(4, 4),
                new Point2D(14, 4),
                new Point2D(14, 14),
                new Point2D(4, 14)
            });

            var intersection = PolygonClipper.Intersect(subject, clip);

            // Overlap is square [4, 10] x [4, 10], Width=6, Height=6, Area = 36
            Assert.Equal(4, intersection.VertexCount);
            Assert.InRange(intersection.Area(), 35.99, 36.01);

            var c = intersection.Centroid();
            Assert.InRange(c.X, 6.99, 7.01);
            Assert.InRange(c.Y, 6.99, 7.01);
        }

        [Fact]
        public void PolygonOffsetter_DilatesPolygonCorrectly()
        {
            // Square: [0, 10] x [0, 10], Area = 100
            var square = new Polygon2D(new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10)
            });

            // Dilate outward by 2.0 -> becomes [-2, 12] x [-2, 12], size 14x14, Area = 196
            var dilated = PolygonOffsetter.Offset(square, delta: 2.0);

            Assert.Equal(4, dilated.VertexCount);
            Assert.InRange(dilated.Area(), 195.9, 196.1);
        }
    }
}
