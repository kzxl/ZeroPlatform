using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorRangeSlicingTests
    {
        [Fact]
        public void Range_1D_Slicing_CreatesCorrectView()
        {
            var t = Tensor.FromArray(new float[] { 10, 20, 30, 40, 50 });

            // t[1..4] -> [20, 30, 40]
            var slice = t[1..4];
            Assert.Equal(new TensorShape(3), slice.Shape);
            Assert.Equal(20f, slice[0]);
            Assert.Equal(30f, slice[1]);
            Assert.Equal(40f, slice[2]);

            // t[..^1] -> [10, 20, 30, 40]
            var prefix = t[..^1];
            Assert.Equal(new TensorShape(4), prefix.Shape);
            Assert.Equal(40f, prefix[3]);
        }

        [Fact]
        public void Range_2D_Slicing_And_SubTensorViews()
        {
            var m = Tensor.FromArray(new float[]
            {
                1,  2,  3,  4,
                5,  6,  7,  8,
                9, 10, 11, 12
            }, 3, 4);

            // Sub-matrix [0..2, 1..3] -> 2x2
            // [[2, 3], [6, 7]]
            var sub = m[0..2, 1..3];
            Assert.Equal(new TensorShape(2, 2), sub.Shape);
            Assert.Equal(2f, sub[0, 0]);
            Assert.Equal(3f, sub[0, 1]);
            Assert.Equal(6f, sub[1, 0]);
            Assert.Equal(7f, sub[1, 1]);

            // Single row across range of cols: m[1, 1..3] -> 1D vector [6, 7]
            var rowSlice = m[1, 1..3];
            Assert.Equal(new TensorShape(2), rowSlice.Shape);
            Assert.Equal(6f, rowSlice[0]);
            Assert.Equal(7f, rowSlice[1]);

            // Single col across range of rows: m[0..2, 2] -> 1D vector [3, 7]
            var colSlice = m[0..2, 2];
            Assert.Equal(new TensorShape(2), colSlice.Shape);
            Assert.Equal(3f, colSlice[0]);
            Assert.Equal(7f, colSlice[1]);
        }

        [Fact]
        public void Range_Slicing_IsZeroCopy_MutatingViewAffectsOriginal()
        {
            var t = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5 });
            var slice = t[1..4];

            // Mutate slice
            slice[0] = 99f; // index 1 in original

            Assert.Equal(99f, t[1]);
        }
    }
}
