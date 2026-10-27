using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorShapeTests
    {
        [Fact]
        public void Shape_BasicProperties_AreCorrect()
        {
            var shape = new TensorShape(2, 3, 4);
            Assert.Equal(3, shape.Rank);
            Assert.Equal(24, shape.TotalElements);
            Assert.Equal(2, shape[0]);
            Assert.Equal(3, shape[1]);
            Assert.Equal(4, shape[2]);
            Assert.Equal(4, shape[-1]); // Negative indexing
            Assert.Equal("(2, 3, 4)", shape.ToString());
        }

        [Fact]
        public void Shape_BroadcastingRules_MatchNumPy()
        {
            // Case 1: (3, 4) and (4,) -> (3, 4)
            var a = new TensorShape(3, 4);
            var b = new TensorShape(4);
            Assert.True(a.IsCompatibleForBroadcasting(b, out var outShape1));
            Assert.Equal(new TensorShape(3, 4), outShape1);

            // Case 2: (3, 1) and (1, 4) -> (3, 4)
            var c = new TensorShape(3, 1);
            var d = new TensorShape(1, 4);
            Assert.True(c.IsCompatibleForBroadcasting(d, out var outShape2));
            Assert.Equal(new TensorShape(3, 4), outShape2);

            // Case 3: (2, 3, 1) and (3, 5) -> (2, 3, 5)
            var e = new TensorShape(2, 3, 1);
            var f = new TensorShape(3, 5);
            Assert.True(e.IsCompatibleForBroadcasting(f, out var outShape3));
            Assert.Equal(new TensorShape(2, 3, 5), outShape3);

            // Case 4: Incompatible shapes (3, 4) and (3, 5) -> False
            var g = new TensorShape(3, 4);
            var h = new TensorShape(3, 5);
            Assert.False(g.IsCompatibleForBroadcasting(h, out _));
        }

        [Fact]
        public void Shape_SqueezeAndUnsqueeze_WorkCorrectly()
        {
            var shape = new TensorShape(1, 3, 1, 4);

            // Squeeze all size-1 dims -> (3, 4)
            var squeezed = shape.Squeeze();
            Assert.Equal(new TensorShape(3, 4), squeezed);

            // Squeeze specific axis 0 -> (3, 1, 4)
            var squeezed0 = shape.Squeeze(0);
            Assert.Equal(new TensorShape(3, 1, 4), squeezed0);

            // Unsqueeze at axis 1 on (3, 4) -> (3, 1, 4)
            var unsqueezed = squeezed.Unsqueeze(1);
            Assert.Equal(new TensorShape(3, 1, 4), unsqueezed);
        }

        [Fact]
        public void Shape_Permute_ReordersDimensions()
        {
            var shape = new TensorShape(2, 3, 4);
            var permuted = shape.Permute(2, 0, 1);
            Assert.Equal(new TensorShape(4, 2, 3), permuted);
        }
    }
}
