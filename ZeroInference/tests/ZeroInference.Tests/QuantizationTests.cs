using System;
using Xunit;
using ZeroInference.Core.Quantization;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class QuantizationTests
    {
        [Fact]
        public void QuantizeAndDequantize_PreservesAccuracy()
        {
            var raw = Tensor.FromArray(new float[] { -4.5f, -2.0f, 0.0f, 1.25f, 3.8f, 5.0f }, 2, 3);

            var quantized = Quantizer.QuantizeSymmetric(raw);
            Assert.Equal(raw.Length, quantized.Length);

            var dequantized = quantized.Dequantize();

            var rawSpan = raw.AsReadOnlySpan();
            var deqSpan = dequantized.AsReadOnlySpan();

            for (int i = 0; i < rawSpan.Length; i++)
            {
                // Quantization error for 8-bit scale should be <= 5.0 / 127 ≈ 0.04
                Assert.InRange(deqSpan[i], rawSpan[i] - 0.05f, rawSpan[i] + 0.05f);
            }
        }

        [Fact]
        public void MatMulInt8_CalculatesCorrectProduct()
        {
            // A: [2, 2], B: [2, 2]
            var A = Tensor.FromArray(new float[] { 1.0f, 2.0f, 3.0f, 4.0f }, 2, 2);
            var B = Tensor.FromArray(new float[] { 0.5f, -1.0f, 2.0f, 1.5f }, 2, 2);

            var qA = Quantizer.QuantizeSymmetric(A);
            var qB = Quantizer.QuantizeSymmetric(B);

            var C = Quantizer.MatMulInt8(qA, qB);

            // True C[0,0] = 1*0.5 + 2*2.0 = 4.5
            // True C[0,1] = 1*(-1) + 2*1.5 = 2.0
            // True C[1,0] = 3*0.5 + 4*2.0 = 9.5
            // True C[1,1] = 3*(-1) + 4*1.5 = 3.0
            Assert.InRange(C[0, 0], 4.4f, 4.6f);
            Assert.InRange(C[0, 1], 1.9f, 2.1f);
            Assert.InRange(C[1, 0], 9.4f, 9.6f);
            Assert.InRange(C[1, 1], 2.9f, 3.1f);
        }
    }
}
