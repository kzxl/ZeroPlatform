using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Tests.Layout;

public class SquarifiedTreeMapTests
{
    private record TestItem(string Name, double Size);

    [Fact]
    public void Layout_ValidWeights_GeneratesBoundedSquarifiedRectangles()
    {
        var items = new List<TestItem>
        {
            new("Videos", 500),
            new("Music", 250),
            new("Documents", 150),
            new("Downloads", 100)
        };

        double canvasWidth = 800;
        double canvasHeight = 600;

        var rects = SquarifiedTreeMap.Layout(items, x => x.Size, canvasWidth, canvasHeight);

        Assert.Equal(4, rects.Count);
        double totalNormWeight = 0;

        foreach (var r in rects)
        {
            Assert.True(r.X >= 0, $"Rect {r.Item.Name} X should be >= 0");
            Assert.True(r.Y >= 0, $"Rect {r.Item.Name} Y should be >= 0");
            Assert.True(r.X + r.Width <= canvasWidth + 1.0, $"Rect {r.Item.Name} exceeds canvas width");
            Assert.True(r.Y + r.Height <= canvasHeight + 1.0, $"Rect {r.Item.Name} exceeds canvas height");
            totalNormWeight += r.NormalizedWeight;
        }

        Assert.InRange(totalNormWeight, 0.999, 1.001);
    }

    [Fact]
    public void Layout_EmptyOrZero_ReturnsEmpty()
    {
        var empty = new List<TestItem>();
        Assert.Empty(SquarifiedTreeMap.Layout(empty, x => x.Size, 500, 400));
        Assert.Empty(SquarifiedTreeMap.Layout<TestItem>(null!, x => x.Size, 500, 400));


        var zeroDimension = new List<TestItem> { new("Item", 10) };
        Assert.Empty(SquarifiedTreeMap.Layout(zeroDimension, x => x.Size, 0, 0));
    }
}
