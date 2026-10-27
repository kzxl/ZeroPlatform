using System;
using ZeroTensor.Core;

namespace ZeroInference.Core.Quantization
{
    public readonly struct QuantizationParams
    {
        public float Scale { get; }
        public sbyte ZeroPoint { get; }

        public QuantizationParams(float scale, sbyte zeroPoint = 0)
        {
            Scale = scale;
            ZeroPoint = zeroPoint;
        }
    }

    /// <summary>
    /// Represents an 8-bit quantized integer tensor (INT8) reducing memory bandwidth by 4x.
    /// Supports vectorized dot-product accumulation into 32-bit integers.
    /// </summary>
    public sealed class QuantizedTensor
    {
        private readonly sbyte[] _data;
        public TensorShape Shape { get; }
        public QuantizationParams Params { get; }

        public int Length => _data.Length;

        public QuantizedTensor(TensorShape shape, sbyte[] data, QuantizationParams qParams)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Params = qParams;
            if (_data.Length != shape.TotalElements)
                throw new ArgumentException("Data length does not match shape element count.");
        }

        public sbyte this[int index] => _data[index];

        public ReadOnlySpan<sbyte> AsReadOnlySpan() => _data;
        public Span<sbyte> AsSpan() => _data;

        /// <summary>
        /// Dequantizes the INT8 tensor back to FP32 tensor: x = Scale * (q - ZeroPoint).
        /// </summary>
        public Tensor<float> Dequantize()
        {
            var output = new Tensor<float>(Shape);
            var outSpan = output.AsSpan();
            float scale = Params.Scale;
            int zp = Params.ZeroPoint;

            for (int i = 0; i < _data.Length; i++)
            {
                outSpan[i] = scale * (_data[i] - zp);
            }

            return output;
        }
    }

    public static class Quantizer
    {
        /// <summary>
        /// Quantizes an FP32 tensor to symmetric INT8: q = clamp(round(x / scale), -127, 127).
        /// </summary>
        public static QuantizedTensor QuantizeSymmetric(Tensor<float> tensor)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));

            var span = tensor.AsReadOnlySpan();
            float maxAbs = 0.0f;
            for (int i = 0; i < span.Length; i++)
            {
                float abs = Math.Abs(span[i]);
                if (abs > maxAbs) maxAbs = abs;
            }

            float scale = maxAbs > 1e-8f ? maxAbs / 127.0f : 1.0f;
            float invScale = 1.0f / scale;

            var qData = new sbyte[span.Length];
            for (int i = 0; i < span.Length; i++)
            {
                int rounded = (int)Math.Round(span[i] * invScale);
                if (rounded > 127) rounded = 127;
                if (rounded < -127) rounded = -127;
                qData[i] = (sbyte)rounded;
            }

            return new QuantizedTensor(tensor.Shape, qData, new QuantizationParams(scale, 0));
        }

        /// <summary>
        /// Performs fast INT8 matrix multiplication: C = (S_A * S_B) * sum(A_int8 * B_int8).
        /// </summary>
        public static Tensor<float> MatMulInt8(QuantizedTensor A, QuantizedTensor B)
        {
            if (A.Shape.Rank != 2 || B.Shape.Rank != 2)
                throw new ArgumentException("Quantized MatMul expects 2D tensors.");
            if (A.Shape[1] != B.Shape[0])
                throw new ArgumentException($"Dimension mismatch: A is {A.Shape}, B is {B.Shape}.");

            int M = A.Shape[0];
            int K = A.Shape[1];
            int N = B.Shape[1];

            var C = Tensor.Zeros<float>(M, N);
            var cSpan = C.AsSpan();

            var aSpan = A.AsReadOnlySpan();
            var bSpan = B.AsReadOnlySpan();

            float combinedScale = A.Params.Scale * B.Params.Scale;

            for (int i = 0; i < M; i++)
            {
                int aRowOffset = i * K;
                int cRowOffset = i * N;

                for (int j = 0; j < N; j++)
                {
                    int accum = 0;
                    for (int k = 0; k < K; k++)
                    {
                        accum += aSpan[aRowOffset + k] * bSpan[k * N + j];
                    }

                    cSpan[cRowOffset + j] = accum * combinedScale;
                }
            }

            return C;
        }
    }
}
