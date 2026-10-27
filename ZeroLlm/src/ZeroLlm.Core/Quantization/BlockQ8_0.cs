using System;
using System.Runtime.InteropServices;

namespace ZeroLlm.Core.Quantization
{
    /// <summary>
    /// GGML Q8_0 8-bit quantized block of 32 elements.
    /// Memory footprint: 34 bytes (2-byte FP16 scale + 32 1-byte signed ints).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct BlockQ8_0
    {
        public const int BlockSize = 32;

        public ushort Scale;       // FP16 scale factor
        public fixed sbyte Qs[32]; // 32 quantized signed 8-bit integers

        public float GetScale() => HalfHelper.HalfToFloat(Scale);

        public void SetScale(float scale) => Scale = HalfHelper.FloatToHalf(scale);

        /// <summary>
        /// Dequantizes the 32 elements into the target float span: dst[i] = Qs[i] * Scale.
        /// </summary>
        public void Dequantize(Span<float> destination)
        {
            if (destination.Length < BlockSize)
                throw new ArgumentException($"Destination span must be at least {BlockSize} elements.");

            float d = GetScale();
            fixed (sbyte* q = Qs)
            {
                for (int i = 0; i < BlockSize; i++)
                {
                    destination[i] = q[i] * d;
                }
            }
        }

        /// <summary>
        /// Quantizes 32 float values into a Q8_0 block.
        /// </summary>
        public static BlockQ8_0 Quantize(ReadOnlySpan<float> source)
        {
            if (source.Length < BlockSize)
                throw new ArgumentException($"Source span must be at least {BlockSize} elements.");

            float amax = 0.0f;
            for (int i = 0; i < BlockSize; i++)
            {
                float v = Math.Abs(source[i]);
                if (v > amax) amax = v;
            }

            float d = amax / 127.0f;
            float id = d > 1e-15f ? 1.0f / d : 0.0f;

            var block = new BlockQ8_0();
            block.SetScale(d);

            for (int i = 0; i < BlockSize; i++)
            {
                float v0 = source[i] * id;
                int q = (int)Math.Round(v0);
                if (q > 127) q = 127;
                if (q < -127) q = -127;
                block.Qs[i] = (sbyte)q;
            }

            return block;
        }
    }
}
