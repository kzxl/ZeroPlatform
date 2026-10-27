using System;
using System.Runtime.InteropServices;

namespace ZeroLlm.Core.Quantization
{
    /// <summary>
    /// GGML Q4_0 4-bit quantized block of 32 elements.
    /// Memory footprint: 18 bytes (2-byte FP16 scale + 16 bytes packing 32 4-bit nibbles).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct BlockQ4_0
    {
        public const int BlockSize = 32;

        public ushort Scale;      // FP16 scale factor
        public fixed byte Qs[16]; // 16 bytes packing 32 4-bit nibbles (low nibble 0..15, high nibble 16..31)

        public float GetScale() => HalfHelper.HalfToFloat(Scale);

        public void SetScale(float scale) => Scale = HalfHelper.FloatToHalf(scale);

        /// <summary>
        /// Dequantizes the 32 elements into the target float span: dst[i] = (q[i] - 8) * Scale.
        /// </summary>
        public void Dequantize(Span<float> destination)
        {
            if (destination.Length < BlockSize)
                throw new ArgumentException($"Destination span must be at least {BlockSize} elements.");

            float d = GetScale();
            fixed (byte* q = Qs)
            {
                for (int i = 0; i < 16; i++)
                {
                    byte b = q[i];
                    int q0 = (b & 0x0F) - 8;
                    int q1 = (b >> 4) - 8;
                    destination[i] = q0 * d;
                    destination[i + 16] = q1 * d;
                }
            }
        }

        /// <summary>
        /// Quantizes 32 float values into a Q4_0 block.
        /// </summary>
        public static BlockQ4_0 Quantize(ReadOnlySpan<float> source)
        {
            if (source.Length < BlockSize)
                throw new ArgumentException($"Source span must be at least {BlockSize} elements.");

            float amax = 0.0f;
            for (int i = 0; i < BlockSize; i++)
            {
                float v = Math.Abs(source[i]);
                if (v > amax) amax = v;
            }

            float d = amax / 8.0f;
            float id = d != 0.0f ? 1.0f / d : 0.0f;

            var block = new BlockQ4_0();
            block.SetScale(d);

            for (int i = 0; i < 16; i++)
            {
                float x0 = source[i] * id;
                float x1 = source[i + 16] * id;

                int q0 = (int)Math.Min(15, Math.Max(0, Math.Round(x0) + 8));
                int q1 = (int)Math.Min(15, Math.Max(0, Math.Round(x1) + 8));

                block.Qs[i] = (byte)((q0 & 0x0F) | ((q1 & 0x0F) << 4));
            }

            return block;
        }
    }
}
