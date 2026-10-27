using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZeroLlm.Core.Layers
{
    /// <summary>
    /// Pure C# Root Mean Square Layer Normalization (RMSNorm) kernel.
    /// Hardware-accelerated via SIMD (AVX2/AVX-512) Vector<float>.
    /// </summary>
    public static unsafe class RmsNorm
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Forward(ReadOnlySpan<float> input, ReadOnlySpan<float> weight, Span<float> output, float epsilon = 1e-5f)
        {
            if (input.Length != weight.Length || input.Length != output.Length)
            {
                throw new ArgumentException("Input, weight, and output spans must have identical lengths.");
            }

            int len = input.Length;
            int vecSize = Vector<float>.Count;
            int simdLimit = len - (len % vecSize);

            fixed (float* pIn = input)
            fixed (float* pW = weight)
            fixed (float* pOut = output)
            {
                Vector<float> vSumSq = Vector<float>.Zero;
                for (int i = 0; i < simdLimit; i += vecSize)
                {
                    var v = *(Vector<float>*)(pIn + i);
                    vSumSq += v * v;
                }

                float sumSquares = Vector.Dot(vSumSq, Vector<float>.One);
                for (int i = simdLimit; i < len; i++)
                {
                    float v = pIn[i];
                    sumSquares += v * v;
                }

                float meanSquare = sumSquares / len;
                float invRms = 1.0f / (float)Math.Sqrt(meanSquare + epsilon);
                var vInvRms = new Vector<float>(invRms);

                for (int i = 0; i < simdLimit; i += vecSize)
                {
                    var vIn = *(Vector<float>*)(pIn + i);
                    var vWeight = *(Vector<float>*)(pW + i);
                    *(Vector<float>*)(pOut + i) = vIn * vInvRms * vWeight;
                }

                for (int i = simdLimit; i < len; i++)
                {
                    pOut[i] = pIn[i] * invRms * pW[i];
                }
            }
        }
    }
}
