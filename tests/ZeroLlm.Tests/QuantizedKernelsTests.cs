using System;
using Xunit;
using ZeroLlm.Core.Quantization;

namespace ZeroLlm.Tests
{
    public class QuantizedKernelsTests
    {
        [Fact]
        public void HalfHelper_ConvertsFloatsAndHalvesAccurately()
        {
            float[] testValues = { 0.0f, 1.0f, -1.0f, 0.5f, 2.5f, -10.25f, 100.0f };
            foreach (var val in testValues)
            {
                ushort half = HalfHelper.FloatToHalf(val);
                float roundtrip = HalfHelper.HalfToFloat(half);
                Assert.Equal(val, roundtrip, precision: 2);
            }
        }

        [Fact]
        public void BlockQ8_0_QuantizesAndDequantizesCorrectly()
        {
            float[] original = new float[32];
            for (int i = 0; i < 32; i++)
            {
                original[i] = (i - 16) * 1.5f;
            }

            var block = BlockQ8_0.Quantize(original);
            float[] reconstructed = new float[32];
            block.Dequantize(reconstructed);

            for (int i = 0; i < 32; i++)
            {
                // Q8_0 quantization error is very low (< 0.2)
                Assert.InRange(Math.Abs(original[i] - reconstructed[i]), 0.0f, 0.2f);
            }
        }

        [Fact]
        public void BlockQ4_0_QuantizesAndDequantizesCorrectly()
        {
            float[] original = new float[32];
            for (int i = 0; i < 32; i++)
            {
                original[i] = (i - 16) * 0.5f;
            }

            var block = BlockQ4_0.Quantize(original);
            float[] reconstructed = new float[32];
            block.Dequantize(reconstructed);

            for (int i = 0; i < 32; i++)
            {
                // Q4_0 quantization error is within 4-bit discrete quantization interval
                Assert.InRange(Math.Abs(original[i] - reconstructed[i]), 0.0f, 0.75f);
            }
        }

        [Fact]
        public void QuantizedKernels_DotProductQ8_0_MatchesScalar()
        {
            int elements = 64; // 2 blocks
            float[] x = new float[elements];
            float[] wRaw = new float[elements];

            for (int i = 0; i < elements; i++)
            {
                x[i] = (i % 5) * 0.2f;
                wRaw[i] = ((i % 7) - 3) * 0.5f;
            }

            var blocks = new BlockQ8_0[2];
            blocks[0] = BlockQ8_0.Quantize(wRaw.AsSpan(0, 32));
            blocks[1] = BlockQ8_0.Quantize(wRaw.AsSpan(32, 32));

            float expectedDot = 0.0f;
            float[] dequantized = new float[elements];
            blocks[0].Dequantize(dequantized.AsSpan(0, 32));
            blocks[1].Dequantize(dequantized.AsSpan(32, 32));

            for (int i = 0; i < elements; i++)
            {
                expectedDot += x[i] * dequantized[i];
            }

            float fusedDot = QuantizedKernels.DotProductQ8_0(x, blocks);
            Assert.Equal(expectedDot, fusedDot, precision: 3);
        }

        [Fact]
        public void QuantizedKernels_DotProductQ4_0_MatchesScalar()
        {
            int elements = 64; // 2 blocks
            float[] x = new float[elements];
            float[] wRaw = new float[elements];

            for (int i = 0; i < elements; i++)
            {
                x[i] = (i % 3) * 0.4f;
                wRaw[i] = ((i % 5) - 2) * 0.8f;
            }

            var blocks = new BlockQ4_0[2];
            blocks[0] = BlockQ4_0.Quantize(wRaw.AsSpan(0, 32));
            blocks[1] = BlockQ4_0.Quantize(wRaw.AsSpan(32, 32));

            float expectedDot = 0.0f;
            float[] dequantized = new float[elements];
            blocks[0].Dequantize(dequantized.AsSpan(0, 32));
            blocks[1].Dequantize(dequantized.AsSpan(32, 32));

            for (int i = 0; i < elements; i++)
            {
                expectedDot += x[i] * dequantized[i];
            }

            float fusedDot = QuantizedKernels.DotProductQ4_0(x, blocks);
            Assert.Equal(expectedDot, fusedDot, precision: 3);
        }

        [Fact]
        public void QuantizedKernels_MatVecMulQ8_0_CalculatesCorrectOutputs()
        {
            int rows = 3;
            int cols = 32; // 1 block per row

            var weights = new BlockQ8_0[rows];
            for (int r = 0; r < rows; r++)
            {
                float[] rowFloats = new float[cols];
                for (int c = 0; c < cols; c++) rowFloats[c] = (r + 1) * 0.1f;
                weights[r] = BlockQ8_0.Quantize(rowFloats);
            }

            float[] x = new float[cols];
            for (int c = 0; c < cols; c++) x[c] = 1.0f;

            float[] y = new float[rows];
            QuantizedKernels.MatVecMulQ8_0(weights, x, y, rows, cols);

            // Row 0: 32 * (1.0 * 0.1) = ~3.2
            Assert.InRange(y[0], 3.1f, 3.3f);
            // Row 1: 32 * (1.0 * 0.2) = ~6.4
            Assert.InRange(y[1], 6.2f, 6.6f);
            // Row 2: 32 * (1.0 * 0.3) = ~9.6
            Assert.InRange(y[2], 9.4f, 9.8f);
        }

        [Fact]
        public void QuantizedKernels_MultiBlockAvx2_DotProductQ8_And_Q4_MatchDequantized()
        {
            int blocksCount = 8;
            int elements = blocksCount * 32;
            float[] x = new float[elements];
            float[] wRaw = new float[elements];

            for (int i = 0; i < elements; i++)
            {
                x[i] = ((i % 13) - 6) * 0.15f;
                wRaw[i] = ((i % 11) - 5) * 0.35f;
            }

            var q8Blocks = new BlockQ8_0[blocksCount];
            var q4Blocks = new BlockQ4_0[blocksCount];
            float expectedQ8 = 0f;
            float expectedQ4 = 0f;
            float[] deq8 = new float[32];
            float[] deq4 = new float[32];

            for (int b = 0; b < blocksCount; b++)
            {
                var slice = wRaw.AsSpan(b * 32, 32);
                q8Blocks[b] = BlockQ8_0.Quantize(slice);
                q4Blocks[b] = BlockQ4_0.Quantize(slice);

                q8Blocks[b].Dequantize(deq8);
                q4Blocks[b].Dequantize(deq4);

                for (int j = 0; j < 32; j++)
                {
                    expectedQ8 += x[b * 32 + j] * deq8[j];
                    expectedQ4 += x[b * 32 + j] * deq4[j];
                }
            }

            Assert.Equal(expectedQ8, QuantizedKernels.DotProductQ8_0(x, q8Blocks), precision: 2);
            Assert.Equal(expectedQ4, QuantizedKernels.DotProductQ4_0(x, q4Blocks), precision: 2);
        }
    }
}
