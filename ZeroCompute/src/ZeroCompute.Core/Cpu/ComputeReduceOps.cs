using System;
using System.Numerics;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// High-performance SIMD-vectorized reduction kernels (Sum, Min, Max).
    /// Employs 4-way vector accumulator unrolling to saturate CPU execution pipelines.
    /// </summary>
    public static unsafe class ComputeReduceOps
    {
        private static readonly int VectorSize = Vector<float>.Count;

        /// <summary>
        /// Computes the sum of float elements in range [start, end) using SIMD vector accumulators.
        /// </summary>
        public static float Sum(float* pData, int start, int end)
        {
            int count = end - start;
            if (count <= 0) return 0f;

            int i = start;
            int vStep = VectorSize;
            int unrollStep = vStep * 4;

            float sum = 0f;

            if (Vector.IsHardwareAccelerated && count >= unrollStep)
            {
                var acc0 = Vector<float>.Zero;
                var acc1 = Vector<float>.Zero;
                var acc2 = Vector<float>.Zero;
                var acc3 = Vector<float>.Zero;

                int unrollEnd = start + (count / unrollStep) * unrollStep;
                for (; i < unrollEnd; i += unrollStep)
                {
                    acc0 += *(Vector<float>*)(pData + i);
                    acc1 += *(Vector<float>*)(pData + i + vStep);
                    acc2 += *(Vector<float>*)(pData + i + vStep * 2);
                    acc3 += *(Vector<float>*)(pData + i + vStep * 3);
                }

                var totalAcc = (acc0 + acc1) + (acc2 + acc3);
                sum += Vector.Dot(totalAcc, Vector<float>.One);
            }

            if (Vector.IsHardwareAccelerated && i <= end - vStep)
            {
                var singleAcc = Vector<float>.Zero;
                int singleEnd = start + (count / vStep) * vStep;
                for (; i < singleEnd; i += vStep)
                {
                    singleAcc += *(Vector<float>*)(pData + i);
                }
                sum += Vector.Dot(singleAcc, Vector<float>.One);
            }

            for (; i < end; i++)
            {
                sum += pData[i];
            }

            return sum;
        }

        /// <summary>
        /// Finds the maximum float element in range [start, end).
        /// </summary>
        public static float Max(float* pData, int start, int end)
        {
            int count = end - start;
            if (count <= 0) return float.MinValue;

            int i = start;
            int vStep = VectorSize;
            int unrollStep = vStep * 4;
            float max = float.MinValue;

            if (Vector.IsHardwareAccelerated && count >= unrollStep)
            {
                var m0 = new Vector<float>(pData[start]);
                var m1 = m0;
                var m2 = m0;
                var m3 = m0;

                int unrollEnd = start + (count / unrollStep) * unrollStep;
                for (; i < unrollEnd; i += unrollStep)
                {
                    m0 = Vector.Max(m0, *(Vector<float>*)(pData + i));
                    m1 = Vector.Max(m1, *(Vector<float>*)(pData + i + vStep));
                    m2 = Vector.Max(m2, *(Vector<float>*)(pData + i + vStep * 2));
                    m3 = Vector.Max(m3, *(Vector<float>*)(pData + i + vStep * 3));
                }

                var totalM = Vector.Max(Vector.Max(m0, m1), Vector.Max(m2, m3));
                for (int v = 0; v < vStep; v++)
                {
                    if (totalM[v] > max) max = totalM[v];
                }
            }

            for (; i < end; i++)
            {
                if (pData[i] > max) max = pData[i];
            }

            return max;
        }

        /// <summary>
        /// Finds the minimum float element in range [start, end).
        /// </summary>
        public static float Min(float* pData, int start, int end)
        {
            int count = end - start;
            if (count <= 0) return float.MaxValue;

            int i = start;
            int vStep = VectorSize;
            int unrollStep = vStep * 4;
            float min = float.MaxValue;

            if (Vector.IsHardwareAccelerated && count >= unrollStep)
            {
                var m0 = new Vector<float>(pData[start]);
                var m1 = m0;
                var m2 = m0;
                var m3 = m0;

                int unrollEnd = start + (count / unrollStep) * unrollStep;
                for (; i < unrollEnd; i += unrollStep)
                {
                    m0 = Vector.Min(m0, *(Vector<float>*)(pData + i));
                    m1 = Vector.Min(m1, *(Vector<float>*)(pData + i + vStep));
                    m2 = Vector.Min(m2, *(Vector<float>*)(pData + i + vStep * 2));
                    m3 = Vector.Min(m3, *(Vector<float>*)(pData + i + vStep * 3));
                }

                var totalM = Vector.Min(Vector.Min(m0, m1), Vector.Min(m2, m3));
                for (int v = 0; v < vStep; v++)
                {
                    if (totalM[v] < min) min = totalM[v];
                }
            }

            for (; i < end; i++)
            {
                if (pData[i] < min) min = pData[i];
            }

            return min;
        }
    }
}
