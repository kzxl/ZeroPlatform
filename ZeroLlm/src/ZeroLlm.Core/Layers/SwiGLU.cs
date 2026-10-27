using System;
using System.Runtime.CompilerServices;

namespace ZeroLlm.Core.Layers
{
    /// <summary>
    /// Pure C# Swish-Gated Linear Unit (SwiGLU) activation kernel: Swish(gate) * up.
    /// Hardware-optimized with pinned pointer arithmetic and unrolled pipeline.
    /// </summary>
    public static unsafe class SwiGLU
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forward(ReadOnlySpan<float> gate, ReadOnlySpan<float> up, Span<float> output)
        {
            if (gate.Length != up.Length || gate.Length != output.Length)
            {
                throw new ArgumentException("Gate, up, and output spans must have identical lengths.");
            }

            int len = gate.Length;
            fixed (float* pGate = gate)
            fixed (float* pUp = up)
            fixed (float* pOut = output)
            {
                int limit4 = len - (len % 4);
                for (int i = 0; i < limit4; i += 4)
                {
                    float g0 = pGate[i];
                    float g1 = pGate[i + 1];
                    float g2 = pGate[i + 2];
                    float g3 = pGate[i + 3];

                    float s0 = 1.0f / (1.0f + (float)Math.Exp(-g0));
                    float s1 = 1.0f / (1.0f + (float)Math.Exp(-g1));
                    float s2 = 1.0f / (1.0f + (float)Math.Exp(-g2));
                    float s3 = 1.0f / (1.0f + (float)Math.Exp(-g3));

                    pOut[i] = (g0 * s0) * pUp[i];
                    pOut[i + 1] = (g1 * s1) * pUp[i + 1];
                    pOut[i + 2] = (g2 * s2) * pUp[i + 2];
                    pOut[i + 3] = (g3 * s3) * pUp[i + 3];
                }

                for (int i = limit4; i < len; i++)
                {
                    float g = pGate[i];
                    float s = 1.0f / (1.0f + (float)Math.Exp(-g));
                    pOut[i] = (g * s) * pUp[i];
                }
            }
        }
    }
}
