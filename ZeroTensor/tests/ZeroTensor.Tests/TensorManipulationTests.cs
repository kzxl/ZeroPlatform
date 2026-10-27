using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorManipulationTests
    {
        [Fact]
        public void Concat_1D_Tensors_JoinsSequentially()
        {
            var t1 = Tensor.FromArray(new float[] { 1, 2, 3 });
            var t2 = Tensor.FromArray(new float[] { 4, 5 });
            var t3 = Tensor.FromArray(new float[] { 6 });

            var res = Tensor.Concat(new[] { t1, t2, t3 }, axis: 0);

            Assert.Equal(new TensorShape(6), res.Shape);
            Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6 }, res.ToArray());
        }

        [Fact]
        public void Concat_2D_Tensors_AlongAxis0_And_Axis1()
        {
            // t1: 2x2
            var t1 = Tensor.FromArray(new float[]
            {
                1, 2,
                3, 4
            }, 2, 2);

            // t2: 2x2
            var t2 = Tensor.FromArray(new float[]
            {
                5, 6,
                7, 8
            }, 2, 2);

            // Concat along rows (axis 0) -> 4x2
            var res0 = Tensor.Concat(new[] { t1, t2 }, axis: 0);
            Assert.Equal(new TensorShape(4, 2), res0.Shape);
            Assert.Equal(new float[]
            {
                1, 2,
                3, 4,
                5, 6,
                7, 8
            }, res0.ToArray());

            // Concat along cols (axis 1) -> 2x4
            var res1 = Tensor.Concat(new[] { t1, t2 }, axis: 1);
            Assert.Equal(new TensorShape(2, 4), res1.Shape);
            Assert.Equal(new float[]
            {
                1, 2, 5, 6,
                3, 4, 7, 8
            }, res1.ToArray());
        }

        [Fact]
        public void Stack_Tensors_CreatesNewAxis()
        {
            var t1 = Tensor.FromArray(new float[] { 1, 2, 3 });
            var t2 = Tensor.FromArray(new float[] { 4, 5, 6 });

            // Stack along axis 0 -> 2x3
            var res0 = Tensor.Stack(new[] { t1, t2 }, axis: 0);
            Assert.Equal(new TensorShape(2, 3), res0.Shape);
            Assert.Equal(1f, res0[0, 0]);
            Assert.Equal(6f, res0[1, 2]);

            // Stack along axis 1 -> 3x2
            var res1 = Tensor.Stack(new[] { t1, t2 }, axis: 1);
            Assert.Equal(new TensorShape(3, 2), res1.Shape);
            Assert.Equal(1f, res1[0, 0]);
            Assert.Equal(4f, res1[0, 1]);
        }

        [Fact]
        public void Split_And_Chunk_PartitionTensor()
        {
            var t = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5, 6 });

            // Split into 3 equal parts
            var parts = t.Split(3, axis: 0);
            Assert.Equal(3, parts.Length);
            Assert.Equal(new float[] { 1, 2 }, parts[0].ToArray());
            Assert.Equal(new float[] { 3, 4 }, parts[1].ToArray());
            Assert.Equal(new float[] { 5, 6 }, parts[2].ToArray());

            // Split with custom sizes
            var customParts = t.Split(new[] { 1, 3, 2 }, axis: 0);
            Assert.Equal(3, customParts.Length);
            Assert.Equal(new float[] { 1 }, customParts[0].ToArray());
            Assert.Equal(new float[] { 2, 3, 4 }, customParts[1].ToArray());
            Assert.Equal(new float[] { 5, 6 }, customParts[2].ToArray());

            // Chunk with size 4 -> [4 elements, 2 elements]
            var chunks = t.Chunk(4, axis: 0);
            Assert.Equal(2, chunks.Length);
            Assert.Equal(4, chunks[0].Length);
            Assert.Equal(2, chunks[1].Length);
        }

        [Fact]
        public void Pad_Constant_PadsBordersWithValue()
        {
            var t = Tensor.FromArray(new float[]
            {
                1, 2,
                3, 4
            }, 2, 2);

            // Pad 1 row before, 1 row after, 1 col before, 1 col after with 0
            var padded = t.Pad(new[] { 1, 1 }, new[] { 1, 1 }, PadMode.Constant, constantValue: 9f);

            Assert.Equal(new TensorShape(4, 4), padded.Shape);
            Assert.Equal(9f, padded[0, 0]);
            Assert.Equal(9f, padded[0, 1]);
            Assert.Equal(1f, padded[1, 1]);
            Assert.Equal(2f, padded[1, 2]);
            Assert.Equal(4f, padded[2, 2]);
            Assert.Equal(9f, padded[3, 3]);
        }

        [Fact]
        public void Tile_And_Repeat_ExpandTensor()
        {
            var t = Tensor.FromArray(new float[] { 1, 2 });

            // Tile 3 times -> [1, 2, 1, 2, 1, 2]
            var tiled = t.Tile(3);
            Assert.Equal(new TensorShape(6), tiled.Shape);
            Assert.Equal(new float[] { 1, 2, 1, 2, 1, 2 }, tiled.ToArray());

            // Repeat 3 times along axis 0 -> [1, 1, 1, 2, 2, 2]
            var repeated = t.Repeat(3, axis: 0);
            Assert.Equal(new TensorShape(6), repeated.Shape);
            Assert.Equal(new float[] { 1, 1, 1, 2, 2, 2 }, repeated.ToArray());
        }

        [Fact]
        public void Roll_And_Flip_ReorderElements()
        {
            var t = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5 });

            // Roll by +2 -> [4, 5, 1, 2, 3]
            var rolled = t.Roll(2, axis: 0);
            Assert.Equal(new float[] { 4, 5, 1, 2, 3 }, rolled.ToArray());

            // Flip along axis 0 -> [5, 4, 3, 2, 1]
            var flipped = t.Flip(axis: 0);
            Assert.Equal(new float[] { 5, 4, 3, 2, 1 }, flipped.ToArray());
        }
    }
}
