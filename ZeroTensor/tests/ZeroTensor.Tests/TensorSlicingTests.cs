using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorSlicingTests
    {
        [Fact]
        public void Tensor_CreationAndIndexing_AreAccurate()
        {
            var t = new Tensor<float>(2, 3);
            Assert.Equal(6, t.Length);
            Assert.True(t.IsContiguous);

            t[0, 0] = 10f;
            t[0, 1] = 20f;
            t[1, 2] = 30f;

            Assert.Equal(10f, t[0, 0]);
            Assert.Equal(20f, t[0, 1]);
            Assert.Equal(30f, t[1, 2]);
        }

        [Fact]
        public void Tensor_Slice_IsZeroCopyAndMutatesParent()
        {
            var parent = Tensor.Arange(0f, 12f).Reshape(3, 4); // 3 rows, 4 cols
            Assert.Equal(0f, parent[0, 0]);
            Assert.Equal(5f, parent[1, 1]);

            // Slice rows 1..2 (length 2)
            var sliceRows = parent.Slice(0, 1, 2);
            Assert.Equal(new TensorShape(2, 4), sliceRows.Shape);
            Assert.Equal(4f, sliceRows[0, 0]); // was parent[1, 0]

            // Mutate via slice
            sliceRows[0, 0] = 999f;
            Assert.Equal(999f, parent[1, 0]); // Parent modified without copy!
        }

        [Fact]
        public void Tensor_SubTensor_DropsAxisZero()
        {
            var matrix = Tensor.Arange(0f, 6f).Reshape(2, 3);
            var row1 = matrix.SubTensor(1); // 1D vector of length 3

            Assert.Equal(1, row1.Rank);
            Assert.Equal(3, row1.Length);
            Assert.Equal(3f, row1[0]);
            Assert.Equal(4f, row1[1]);
            Assert.Equal(5f, row1[2]);
        }

        [Fact]
        public void Tensor_Transpose_SwapsAxesWithoutCopyingBuffer()
        {
            var t = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            var transposed = t.Transpose(0, 1);
            Assert.Equal(new TensorShape(3, 2), transposed.Shape);
            Assert.Equal(1f, transposed[0, 0]);
            Assert.Equal(4f, transposed[0, 1]);
            Assert.Equal(2f, transposed[1, 0]);
            Assert.Equal(5f, transposed[1, 1]);
            Assert.Equal(3f, transposed[2, 0]);
            Assert.Equal(6f, transposed[2, 1]);

            // Non-contiguous check
            Assert.False(transposed.IsContiguous);

            // ToContiguous creates packed copy
            var contig = transposed.ToContiguous();
            Assert.True(contig.IsContiguous);
            Assert.Equal(new float[] { 1, 4, 2, 5, 3, 6 }, contig.ToArray());
        }

        [Fact]
        public void Tensor_Reshape_ContiguousIsZeroCopy()
        {
            var t = Tensor.Arange(0f, 8f).Reshape(2, 4);
            var reshaped = t.Reshape(4, 2);

            Assert.Equal(new TensorShape(4, 2), reshaped.Shape);
            Assert.Equal(t.Buffer, reshaped.Buffer); // Same buffer
            Assert.Equal(0f, reshaped[0, 0]);
            Assert.Equal(7f, reshaped[3, 1]);
        }
    }
}
