using System;
using Xunit;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class OcrGeometryTests
{
    [Fact]
    public void OcrPoint_PropertiesAndEquality()
    {
        var p1 = new OcrPoint(10.5f, 20.25f);
        var p2 = new OcrPoint(10.5f, 20.25f);
        var p3 = new OcrPoint(0f, 0f);

        Assert.Equal(10.5f, p1.X);
        Assert.Equal(20.25f, p1.Y);
        Assert.Equal(p1, p2);
        Assert.True(p1 == p2);
        Assert.False(p1 == p3);
        Assert.Equal(OcrPoint.Zero, p3);
        Assert.Contains("10.5", p1.ToString());
    }

    [Fact]
    public void OcrRect_ContainsAndUnion()
    {
        var r1 = new OcrRect(10, 10, 100, 50);
        var r2 = new OcrRect(50, 20, 20, 20);
        var r3 = new OcrRect(200, 200, 10, 10);

        Assert.True(r1.Contains(50, 25));
        Assert.False(r1.Contains(5, 5));
        Assert.True(r1.Contains(r2));
        Assert.False(r1.Contains(r3));
        Assert.True(r1.IntersectsWith(r2));
        Assert.False(r1.IntersectsWith(r3));

        var union = r1.Union(r3);
        Assert.Equal(10, union.Left);
        Assert.Equal(10, union.Top);
        Assert.Equal(210, union.Right);
        Assert.Equal(210, union.Bottom);
        Assert.Equal(40000, union.Area);
    }

    [Fact]
    public void OcrQuad_FromRectAndBoundingRect()
    {
        var rect = new OcrRect(20, 30, 80, 40);
        var quad = OcrQuad.FromRect(rect);

        Assert.Equal(new OcrPoint(20, 30), quad.TopLeft);
        Assert.Equal(new OcrPoint(100, 30), quad.TopRight);
        Assert.Equal(new OcrPoint(100, 70), quad.BottomRight);
        Assert.Equal(new OcrPoint(20, 70), quad.BottomLeft);

        var bounds = quad.GetBoundingRect();
        Assert.Equal(rect, bounds);
    }
}
