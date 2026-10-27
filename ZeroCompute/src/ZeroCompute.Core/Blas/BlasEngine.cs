using System;
using System.Numerics;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using ZeroCompute.Core.Context;
using ZeroCompute.Core.Cpu;
using ZeroTensor.Core;

namespace ZeroCompute.Core.Blas
{
    /// <summary>
    /// High-performance CPU multi-threaded BLAS engine with cache-blocked GEMM and vectorized element-wise kernels.
    /// Pure C# with zero third-party dependencies.
    /// </summary>
    public static class BlasEngine
    {
        private const int BlockSize = 64;

        /// <summary>
        /// Cache-blocked multi-threaded Matrix Multiplication: C = alpha * (A x B) + beta * C.
        /// A is [M, K], B is [K, N], C is [M, N].
        /// </summary>
        public static unsafe void Gemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float> C,
            float alpha = 1.0f,
            float beta = 0.0f)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (C == null) throw new ArgumentNullException(nameof(C));

            if (A.Rank != 2 || B.Rank != 2 || C.Rank != 2)
                throw new ArgumentException("Tensors must be 2D matrices.");

            int M = A.Shape[0];
            int K = A.Shape[1];
            int N = B.Shape[1];

            if (B.Shape[0] != K)
                throw new ArgumentException($"Inner dimensions must match: A is [{M},{K}], B is [{B.Shape[0]},{N}].");
            if (C.Shape[0] != M || C.Shape[1] != N)
                throw new ArgumentException($"Output matrix C shape must be [{M},{N}], but got [{C.Shape[0]},{C.Shape[1]}].");

            var aContig = A.ToContiguous();
            var bContig = B.ToContiguous();
            var cContig = C.ToContiguous();

            fixed (float* pA = &aContig[0, 0])
            fixed (float* pB = &bContig[0, 0])
            fixed (float* pC = &cContig[0, 0])
            {
                ExecuteGemmCore(pA, pB, pC, M, K, N, alpha, beta);
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        /// <summary>
        /// Cache-blocked multi-threaded Matrix Multiplication on contiguous memory spans: C = alpha * (A x B) + beta * C.
        /// A is [M, K], B is [K, N], C is [M, N].
        /// </summary>
        public static unsafe void Gemm(
            ReadOnlySpan<float> A,
            ReadOnlySpan<float> B,
            Span<float> C,
            int M, int K, int N,
            float alpha = 1.0f,
            float beta = 0.0f)
        {
            if (M <= 0 || K <= 0 || N <= 0)
                throw new ArgumentException("Matrix dimensions must be positive integers.");
            if (A.Length < M * K)
                throw new ArgumentException($"Span A length {A.Length} is smaller than required {M * K}.");
            if (B.Length < K * N)
                throw new ArgumentException($"Span B length {B.Length} is smaller than required {K * N}.");
            if (C.Length < M * N)
                throw new ArgumentException($"Span C length {C.Length} is smaller than required {M * N}.");

            fixed (float* pA = A)
            fixed (float* pB = B)
            fixed (float* pC = C)
            {
                ExecuteGemmCore(pA, pB, pC, M, K, N, alpha, beta);
            }
        }

        private static unsafe void ExecuteGemmCore(
            float* pA,
            float* pB,
            float* pC,
            int M,
            int K,
            int N,
            float alpha,
            float beta)
        {
            IntPtr ptrA = (IntPtr)pA;
            IntPtr ptrB = (IntPtr)pB;
            IntPtr ptrC = (IntPtr)pC;

            int totalElements = M * N;

            // Handle beta scaling first if needed
            if (beta == 0.0f)
            {
                Compute.For((totalElements + 1023) / 1024, chunk =>
                {
                    float* localC = (float*)ptrC;
                    int start = chunk * 1024;
                    int end = Math.Min(start + 1024, totalElements);
                    for (int idx = start; idx < end; idx++)
                    {
                        localC[idx] = 0.0f;
                    }
                });
            }
            else if (beta != 1.0f)
            {
                Compute.For((totalElements + 1023) / 1024, chunk =>
                {
                    float* localC = (float*)ptrC;
                    int start = chunk * 1024;
                    int end = Math.Min(start + 1024, totalElements);
                    for (int idx = start; idx < end; idx++)
                    {
                        localC[idx] *= beta;
                    }
                });
            }

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported && Fma.IsSupported)
            {
                ExecuteGemmAvx2Fma(ptrA, ptrB, ptrC, M, K, N, alpha);
                return;
            }
#endif

            // Tiled blocked GEMM with SIMD FMA unrolling
            int numBlocksM = (M + BlockSize - 1) / BlockSize;
            int vStep = Vector<float>.Count;
            int unrollV = vStep * 2;

            Compute.For(numBlocksM, bi =>
            {
                float* localA = (float*)ptrA;
                float* localB = (float*)ptrB;
                float* localC = (float*)ptrC;

                int iStart = bi * BlockSize;
                int iEnd = Math.Min(iStart + BlockSize, M);

                for (int bk = 0; bk < K; bk += BlockSize)
                {
                    int kEnd = Math.Min(bk + BlockSize, K);

                    for (int bj = 0; bj < N; bj += BlockSize)
                    {
                        int jEnd = Math.Min(bj + BlockSize, N);
                        int numCols = jEnd - bj;

                        // Inner kernel
                        for (int i = iStart; i < iEnd; i++)
                        {
                            float* pRowC = localC + i * N;
                            for (int k = bk; k < kEnd; k++)
                            {
                                float aVal = alpha * localA[i * K + k];
                                float* pRowB = localB + k * N;

                                int j = bj;
                                if (Vector.IsHardwareAccelerated && numCols >= unrollV)
                                {
                                    var vAlpha = new Vector<float>(aVal);
                                    int vLimit = bj + (numCols / unrollV) * unrollV;
                                    for (; j < vLimit; j += unrollV)
                                    {
                                        var vB0 = *(Vector<float>*)(pRowB + j);
                                        var vC0 = *(Vector<float>*)(pRowC + j);
                                        var vB1 = *(Vector<float>*)(pRowB + j + vStep);
                                        var vC1 = *(Vector<float>*)(pRowC + j + vStep);

                                        *(Vector<float>*)(pRowC + j) = vC0 + vAlpha * vB0;
                                        *(Vector<float>*)(pRowC + j + vStep) = vC1 + vAlpha * vB1;
                                    }
                                }

                                if (Vector.IsHardwareAccelerated && j <= jEnd - vStep)
                                {
                                    var vAlpha = new Vector<float>(aVal);
                                    int singleLimit = bj + (numCols / vStep) * vStep;
                                    for (; j < singleLimit; j += vStep)
                                    {
                                        var vB = *(Vector<float>*)(pRowB + j);
                                        var vC = *(Vector<float>*)(pRowC + j);
                                        *(Vector<float>*)(pRowC + j) = vC + vAlpha * vB;
                                    }
                                }

                                for (; j < jEnd; j++)
                                {
                                    pRowC[j] += aVal * pRowB[j];
                                }
                            }
                        }
                    }
                }
            }, CpuWorkloadType.ComputeBound);
        }

#if NET8_0_OR_GREATER
        private static unsafe void ExecuteGemmAvx2Fma(
            IntPtr ptrA, IntPtr ptrB, IntPtr ptrC,
            int M, int K, int N,
            float alpha)
        {
            int numBlocksM = (M + BlockSize - 1) / BlockSize;

            Compute.For(numBlocksM, bi =>
            {
                float* localA = (float*)ptrA;
                float* localB = (float*)ptrB;
                float* localC = (float*)ptrC;

                int iStart = bi * BlockSize;
                int iEnd = Math.Min(iStart + BlockSize, M);

                for (int bk = 0; bk < K; bk += BlockSize)
                {
                    int kEnd = Math.Min(bk + BlockSize, K);

                    for (int bj = 0; bj < N; bj += BlockSize)
                    {
                        int jEnd = Math.Min(bj + BlockSize, N);

                        // 4x8 microkernel: 4 rows x 8 cols (1 Vector256 per row)
                        int i = iStart;
                        for (; i <= iEnd - 4; i += 4)
                        {
                            float* pC0 = localC + (i + 0) * N;
                            float* pC1 = localC + (i + 1) * N;
                            float* pC2 = localC + (i + 2) * N;
                            float* pC3 = localC + (i + 3) * N;

                            int j = bj;
                            for (; j <= jEnd - 8; j += 8)
                            {
                                var c0 = Avx.LoadVector256(pC0 + j);
                                var c1 = Avx.LoadVector256(pC1 + j);
                                var c2 = Avx.LoadVector256(pC2 + j);
                                var c3 = Avx.LoadVector256(pC3 + j);

                                for (int k = bk; k < kEnd; k++)
                                {
                                    var vb = Avx.LoadVector256(localB + k * N + j);

                                    var va0 = Vector256.Create(alpha * localA[(i + 0) * K + k]);
                                    var va1 = Vector256.Create(alpha * localA[(i + 1) * K + k]);
                                    var va2 = Vector256.Create(alpha * localA[(i + 2) * K + k]);
                                    var va3 = Vector256.Create(alpha * localA[(i + 3) * K + k]);

                                    c0 = Fma.MultiplyAdd(va0, vb, c0);
                                    c1 = Fma.MultiplyAdd(va1, vb, c1);
                                    c2 = Fma.MultiplyAdd(va2, vb, c2);
                                    c3 = Fma.MultiplyAdd(va3, vb, c3);
                                }

                                Avx.Store(pC0 + j, c0);
                                Avx.Store(pC1 + j, c1);
                                Avx.Store(pC2 + j, c2);
                                Avx.Store(pC3 + j, c3);
                            }

                            // Cleanup remaining columns for these 4 rows
                            for (int k = bk; k < kEnd; k++)
                            {
                                float a0 = alpha * localA[(i + 0) * K + k];
                                float a1 = alpha * localA[(i + 1) * K + k];
                                float a2 = alpha * localA[(i + 2) * K + k];
                                float a3 = alpha * localA[(i + 3) * K + k];
                                float* pB = localB + k * N;

                                for (int cj = j; cj < jEnd; cj++)
                                {
                                    float bVal = pB[cj];
                                    pC0[cj] += a0 * bVal;
                                    pC1[cj] += a1 * bVal;
                                    pC2[cj] += a2 * bVal;
                                    pC3[cj] += a3 * bVal;
                                }
                            }
                        }

                        // Cleanup remaining rows
                        for (; i < iEnd; i++)
                        {
                            float* pRowC = localC + i * N;
                            for (int k = bk; k < kEnd; k++)
                            {
                                float aVal = alpha * localA[i * K + k];
                                float* pRowB = localB + k * N;
                                var va = Vector256.Create(aVal);

                                int j = bj;
                                for (; j <= jEnd - 8; j += 8)
                                {
                                    var vc = Avx.LoadVector256(pRowC + j);
                                    var vb = Avx.LoadVector256(pRowB + j);
                                    Avx.Store(pRowC + j, Fma.MultiplyAdd(va, vb, vc));
                                }
                                for (; j < jEnd; j++)
                                {
                                    pRowC[j] += aVal * pRowB[j];
                                }
                            }
                        }
                    }
                }
            }, CpuWorkloadType.ComputeBound);
        }
#endif

        public static void Add(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            Compute.Vector.Add(A, B, C);
        }

        public static unsafe void Add(ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C)
        {
            int count = Math.Min(A.Length, Math.Min(B.Length, C.Length));
            fixed (float* pA = A)
            fixed (float* pB = B)
            fixed (float* pC = C)
            {
                float* ptrA = pA, ptrB = pB, ptrC = pC;
                Compute.For(count, (start, end) =>
                {
                    ComputeVectorOps.Add(ptrA, ptrB, ptrC, start, end);
                }, CpuWorkloadType.MemoryBound);
            }
        }

        public static void Multiply(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            Compute.Vector.Multiply(A, B, C);
        }

        public static unsafe void Multiply(ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C)
        {
            int count = Math.Min(A.Length, Math.Min(B.Length, C.Length));
            fixed (float* pA = A)
            fixed (float* pB = B)
            fixed (float* pC = C)
            {
                float* ptrA = pA, ptrB = pB, ptrC = pC;
                Compute.For(count, (start, end) =>
                {
                    ComputeVectorOps.Multiply(ptrA, ptrB, ptrC, start, end);
                }, CpuWorkloadType.MemoryBound);
            }
        }

        public static unsafe void Activation(ReadOnlySpan<float> input, Span<float> output, ComputeActivationType type)
        {
            if (type == ComputeActivationType.Softmax)
                throw new NotSupportedException("Softmax requires tensor multidimensional shape specification. Use Activation(Tensor, Tensor, ...) or provide explicit row dimensions.");

            int total = Math.Min(input.Length, output.Length);
            fixed (float* pIn = input)
            fixed (float* pOut = output)
            {
                ExecuteElementwiseActivation(pIn, pOut, total, type);
            }
        }

        public static unsafe void Activation(Tensor<float> input, Tensor<float> output, ComputeActivationType type)
        {
            int total = (int)input.Length;
            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();

            var inFlat = inContig.Flatten();
            var outFlat = outContig.Flatten();

            fixed (float* pIn = &inFlat[0])
            fixed (float* pOut = &outFlat[0])
            {
                if (type == ComputeActivationType.Softmax)
                {
                    int rows = (int)(input.Length / input.Shape[input.Rank - 1]);
                    int cols = input.Shape[input.Rank - 1];
                    float* localIn = pIn;
                    float* localOut = pOut;

                    Compute.For(rows, r =>
                    {
                        float* rowIn = localIn + r * cols;
                        float* rowOut = localOut + r * cols;

                        float max = float.MinValue;
                        for (int c = 0; c < cols; c++)
                            if (rowIn[c] > max) max = rowIn[c];

                        float sum = 0.0f;
                        for (int c = 0; c < cols; c++)
                        {
                            float exp = (float)Math.Exp(rowIn[c] - max);
                            rowOut[c] = exp;
                            sum += exp;
                        }

                        float invSum = 1.0f / sum;
                        for (int c = 0; c < cols; c++)
                        {
                            rowOut[c] *= invSum;
                        }
                    });
                }
                else
                {
                    ExecuteElementwiseActivation(pIn, pOut, total, type);
                }
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        private static unsafe void ExecuteElementwiseActivation(float* ptrIn, float* ptrOut, int total, ComputeActivationType type)
        {
            switch (type)
            {
                case ComputeActivationType.ReLU:
                    Compute.For((total + 2047) / 2048, chunk =>
                    {
                        float* localIn = ptrIn;
                        float* localOut = ptrOut;
                        int start = chunk * 2048;
                        int end = Math.Min(start + 2048, total);
                        for (int i = start; i < end; i++)
                        {
                            float val = localIn[i];
                            localOut[i] = val > 0f ? val : 0f;
                        }
                    });
                    break;

                case ComputeActivationType.LeakyReLU:
                    Compute.For((total + 2047) / 2048, chunk =>
                    {
                        float* localIn = ptrIn;
                        float* localOut = ptrOut;
                        int start = chunk * 2048;
                        int end = Math.Min(start + 2048, total);
                        for (int i = start; i < end; i++)
                        {
                            float val = localIn[i];
                            localOut[i] = val >= 0f ? val : 0.01f * val;
                        }
                    });
                    break;

                case ComputeActivationType.Sigmoid:
                    Compute.For((total + 2047) / 2048, chunk =>
                    {
                        float* localIn = ptrIn;
                        float* localOut = ptrOut;
                        int start = chunk * 2048;
                        int end = Math.Min(start + 2048, total);
                        for (int i = start; i < end; i++)
                        {
                            localOut[i] = 1.0f / (1.0f + (float)Math.Exp(-localIn[i]));
                        }
                    });
                    break;

                case ComputeActivationType.Tanh:
                    Compute.For((total + 2047) / 2048, chunk =>
                    {
                        float* localIn = ptrIn;
                        float* localOut = ptrOut;
                        int start = chunk * 2048;
                        int end = Math.Min(start + 2048, total);
                        for (int i = start; i < end; i++)
                        {
                            localOut[i] = (float)Math.Tanh(localIn[i]);
                        }
                    });
                    break;

                case ComputeActivationType.GELU:
                    Compute.For((total + 2047) / 2048, chunk =>
                    {
                        float* localIn = ptrIn;
                        float* localOut = ptrOut;
                        int start = chunk * 2048;
                        int end = Math.Min(start + 2048, total);
                        ComputeVectorOps.Gelu(localIn, localOut, start, end);
                    }, CpuWorkloadType.ComputeBound);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported activation type: {type}");
            }
        }

        public static unsafe Tensor<float> ReduceSum(Tensor<float> input, int axis)
        {
            if (axis < 0) axis += input.Rank;
            if (axis < 0 || axis >= input.Rank)
                throw new ArgumentOutOfRangeException(nameof(axis));

            int[] newShape = new int[input.Rank - 1];
            int idx = 0;
            for (int i = 0; i < input.Rank; i++)
            {
                if (i != axis) newShape[idx++] = input.Shape[i];
            }

            var output = Tensor.Zeros<float>(newShape.Length == 0 ? new[] { 1 } : newShape);

            int outer = 1;
            for (int i = 0; i < axis; i++) outer *= input.Shape[i];
            int reduceDim = input.Shape[axis];
            int inner = 1;
            for (int i = axis + 1; i < input.Rank; i++) inner *= input.Shape[i];

            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();

            var inFlat = inContig.Flatten();
            var outFlat = outContig.Flatten();

            fixed (float* pIn = &inFlat[0])
            fixed (float* pOut = &outFlat[0])
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrOut = (IntPtr)pOut;

                Compute.For(outer, o =>
                {
                    float* localIn = (float*)ptrIn;
                    float* localOut = (float*)ptrOut;

                    for (int inr = 0; inr < inner; inr++)
                    {
                        float sum = 0f;
                        for (int k = 0; k < reduceDim; k++)
                        {
                            sum += localIn[(o * reduceDim + k) * inner + inr];
                        }
                        localOut[o * inner + inr] = sum;
                    }
                });
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }

            return output;
        }

        public static unsafe Tensor<float> ReduceMax(Tensor<float> input, int axis)
        {
            if (axis < 0) axis += input.Rank;
            if (axis < 0 || axis >= input.Rank)
                throw new ArgumentOutOfRangeException(nameof(axis));

            int[] newShape = new int[input.Rank - 1];
            int idx = 0;
            for (int i = 0; i < input.Rank; i++)
            {
                if (i != axis) newShape[idx++] = input.Shape[i];
            }

            var output = Tensor.Zeros<float>(newShape.Length == 0 ? new[] { 1 } : newShape);

            int outer = 1;
            for (int i = 0; i < axis; i++) outer *= input.Shape[i];
            int reduceDim = input.Shape[axis];
            int inner = 1;
            for (int i = axis + 1; i < input.Rank; i++) inner *= input.Shape[i];

            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();

            var inFlat = inContig.Flatten();
            var outFlat = outContig.Flatten();

            fixed (float* pIn = &inFlat[0])
            fixed (float* pOut = &outFlat[0])
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrOut = (IntPtr)pOut;

                Compute.For(outer, o =>
                {
                    float* localIn = (float*)ptrIn;
                    float* localOut = (float*)ptrOut;

                    for (int inr = 0; inr < inner; inr++)
                    {
                        float max = float.MinValue;
                        for (int k = 0; k < reduceDim; k++)
                        {
                            float v = localIn[(o * reduceDim + k) * inner + inr];
                            if (v > max) max = v;
                        }
                        localOut[o * inner + inr] = max;
                    }
                });
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }

            return output;
        }

        /// <summary>
        /// Batched General Matrix Multiplication: C[b] = alpha * (A[b] x B[b]) + beta * C[b].
        /// Supports rank 3 tensors [B, M, K] x [B, K, N] and rank 4 tensors [B, H, M, K] x [B, H, K, N].
        /// </summary>
        public static unsafe void BatchedGemm(
            Tensor<float> A, Tensor<float> B, Tensor<float> C,
            float alpha = 1.0f, float beta = 0.0f)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (C == null) throw new ArgumentNullException(nameof(C));

            if (A.Rank < 3 || B.Rank < 3)
                throw new ArgumentException("Batched GEMM requires tensors with rank >= 3.");

            int rank = A.Rank;
            int M = A.Shape[rank - 2];
            int K = A.Shape[rank - 1];
            int N = B.Shape[rank - 1];

            if (B.Shape[rank - 2] != K)
                throw new ArgumentException($"Inner dimensions must match: A is [..., {M}, {K}], B is [..., {B.Shape[rank - 2]}, {N}].");

            int batchCount = 1;
            for (int d = 0; d < rank - 2; d++)
            {
                if (A.Shape[d] != B.Shape[d])
                    throw new ArgumentException($"Batch dimension {d} mismatch: A has {A.Shape[d]}, B has {B.Shape[d]}.");
                batchCount *= A.Shape[d];
            }

            var aContig = A.ToContiguous();
            var bContig = B.ToContiguous();
            var cContig = C.ToContiguous();

            int strideA = M * K;
            int strideB = K * N;
            int strideC = M * N;

            fixed (float* pA = aContig.AsSpan())
            fixed (float* pB = bContig.AsSpan())
            fixed (float* pC = cContig.AsSpan())
            {
                IntPtr ptrA = (IntPtr)pA;
                IntPtr ptrB = (IntPtr)pB;
                IntPtr ptrC = (IntPtr)pC;

                ComputeWorkerPool.Shared.DispatchRange(batchCount, Environment.ProcessorCount, (startBatch, countBatch) =>
                {
                    for (int b = startBatch; b < startBatch + countBatch; b++)
                    {
                        float* curA = (float*)ptrA + b * strideA;
                        float* curB = (float*)ptrB + b * strideB;
                        float* curC = (float*)ptrC + b * strideC;
                        Gemm(new ReadOnlySpan<float>(curA, strideA),
                             new ReadOnlySpan<float>(curB, strideB),
                             new Span<float>(curC, strideC),
                             M, K, N, alpha, beta);
                    }
                });
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        /// <summary>
        /// Root Mean Square Normalization (RMSNorm) widely utilized in modern LLM architectures (LLaMA, Mistral, Qwen).
        /// </summary>
        public static unsafe void RmsNorm(
            Tensor<float> input, Tensor<float> output,
            Tensor<float>? weight = null, float epsilon = 1e-5f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (input.Length != output.Length)
                throw new ArgumentException("Input and Output tensor lengths must match.");

            int rank = input.Rank;
            int hiddenDim = input.Shape[rank - 1];
            int rowCount = input.Length / hiddenDim;

            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();
            var wContig = weight != null ? weight.ToContiguous() : null;

            int vStep = Vector<float>.Count;

            fixed (float* pIn = inContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            fixed (float* pW = (wContig != null ? wContig.AsSpan() : ReadOnlySpan<float>.Empty))
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrOut = (IntPtr)pOut;
                IntPtr ptrW = (IntPtr)pW;
                bool hasWeight = ptrW != IntPtr.Zero;

                Compute.For(rowCount, row =>
                {
                    float* inRow = (float*)ptrIn + row * hiddenDim;
                    float* outRow = (float*)ptrOut + row * hiddenDim;
                    float* wRow = (float*)ptrW;

                    float sumSq = 0.0f;
                    int i = 0;

                    if (Vector.IsHardwareAccelerated && hiddenDim >= vStep)
                    {
                        var vSum = Vector<float>.Zero;
                        int vLimit = (hiddenDim / vStep) * vStep;
                        for (; i < vLimit; i += vStep)
                        {
                            var v = *(Vector<float>*)(inRow + i);
                            vSum += v * v;
                        }
                        for (int e = 0; e < vStep; e++) sumSq += vSum[e];
                    }

                    for (; i < hiddenDim; i++)
                    {
                        float v = inRow[i];
                        sumSq += v * v;
                    }

                    float invRms = 1.0f / (float)Math.Sqrt((sumSq / hiddenDim) + epsilon);

                    int j = 0;
                    if (Vector.IsHardwareAccelerated && hiddenDim >= vStep)
                    {
                        var vInv = new Vector<float>(invRms);
                        int vLimit = (hiddenDim / vStep) * vStep;
                        if (hasWeight)
                        {
                            for (; j < vLimit; j += vStep)
                            {
                                var vX = *(Vector<float>*)(inRow + j);
                                var vW = *(Vector<float>*)(wRow + j);
                                *(Vector<float>*)(outRow + j) = vX * vInv * vW;
                            }
                        }
                        else
                        {
                            for (; j < vLimit; j += vStep)
                            {
                                var vX = *(Vector<float>*)(inRow + j);
                                *(Vector<float>*)(outRow + j) = vX * vInv;
                            }
                        }
                    }

                    for (; j < hiddenDim; j++)
                    {
                        float w = hasWeight ? wRow[j] : 1.0f;
                        outRow[j] = inRow[j] * invRms * w;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        /// <summary>
        /// Layer Normalization (LayerNorm) standard in transformer attention and feed-forward blocks.
        /// </summary>
        public static unsafe void LayerNorm(
            Tensor<float> input, Tensor<float> output,
            Tensor<float>? weight = null, Tensor<float>? bias = null, float epsilon = 1e-5f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (input.Length != output.Length)
                throw new ArgumentException("Input and Output tensor lengths must match.");

            int rank = input.Rank;
            int hiddenDim = input.Shape[rank - 1];
            int rowCount = input.Length / hiddenDim;

            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();
            var wContig = weight != null ? weight.ToContiguous() : null;
            var bContig = bias != null ? bias.ToContiguous() : null;

            int vStep = Vector<float>.Count;

            fixed (float* pIn = inContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            fixed (float* pW = (wContig != null ? wContig.AsSpan() : ReadOnlySpan<float>.Empty))
            fixed (float* pB = (bContig != null ? bContig.AsSpan() : ReadOnlySpan<float>.Empty))
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrOut = (IntPtr)pOut;
                IntPtr ptrW = (IntPtr)pW;
                IntPtr ptrB = (IntPtr)pB;

                bool hasWeight = ptrW != IntPtr.Zero;
                bool hasBias = ptrB != IntPtr.Zero;

                Compute.For(rowCount, row =>
                {
                    float* inRow = (float*)ptrIn + row * hiddenDim;
                    float* outRow = (float*)ptrOut + row * hiddenDim;
                    float* wRow = (float*)ptrW;
                    float* bRow = (float*)ptrB;

                    // Pass 1: Mean
                    float sum = 0.0f;
                    int i = 0;
                    if (Vector.IsHardwareAccelerated && hiddenDim >= vStep)
                    {
                        var vSum = Vector<float>.Zero;
                        int vLimit = (hiddenDim / vStep) * vStep;
                        for (; i < vLimit; i += vStep)
                        {
                            vSum += *(Vector<float>*)(inRow + i);
                        }
                        for (int e = 0; e < vStep; e++) sum += vSum[e];
                    }
                    for (; i < hiddenDim; i++) sum += inRow[i];
                    float mean = sum / hiddenDim;

                    // Pass 2: Variance
                    float sumVar = 0.0f;
                    int j = 0;
                    if (Vector.IsHardwareAccelerated && hiddenDim >= vStep)
                    {
                        var vMean = new Vector<float>(mean);
                        var vSumVar = Vector<float>.Zero;
                        int vLimit = (hiddenDim / vStep) * vStep;
                        for (; j < vLimit; j += vStep)
                        {
                            var diff = *(Vector<float>*)(inRow + j) - vMean;
                            vSumVar += diff * diff;
                        }
                        for (int e = 0; e < vStep; e++) sumVar += vSumVar[e];
                    }
                    for (; j < hiddenDim; j++)
                    {
                        float diff = inRow[j] - mean;
                        sumVar += diff * diff;
                    }
                    float invStd = 1.0f / (float)Math.Sqrt((sumVar / hiddenDim) + epsilon);

                    // Pass 3: Normalize and Scale/Shift
                    int k = 0;
                    if (Vector.IsHardwareAccelerated && hiddenDim >= vStep)
                    {
                        var vMean = new Vector<float>(mean);
                        var vInvStd = new Vector<float>(invStd);
                        int vLimit = (hiddenDim / vStep) * vStep;
                        for (; k < vLimit; k += vStep)
                        {
                            var norm = (*(Vector<float>*)(inRow + k) - vMean) * vInvStd;
                            if (hasWeight) norm *= *(Vector<float>*)(wRow + k);
                            if (hasBias) norm += *(Vector<float>*)(bRow + k);
                            *(Vector<float>*)(outRow + k) = norm;
                        }
                    }
                    for (; k < hiddenDim; k++)
                    {
                        float val = (inRow[k] - mean) * invStd;
                        if (hasWeight) val *= wRow[k];
                        if (hasBias) val += bRow[k];
                        outRow[k] = val;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        /// <summary>
        /// Numerically stable row-wise Softmax over the last dimension.
        /// </summary>
        public static unsafe void Softmax(Tensor<float> input, Tensor<float> output, int axis = -1)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (input.Length != output.Length)
                throw new ArgumentException("Input and Output tensor lengths must match.");

            int rank = input.Rank;
            if (axis < 0) axis += rank;
            if (axis != rank - 1)
                throw new NotSupportedException("Optimized Softmax currently supports the last dimension (axis = -1).");

            int rowLength = input.Shape[rank - 1];
            int rowCount = input.Length / rowLength;

            var inContig = input.ToContiguous();
            var outContig = output.ToContiguous();

            fixed (float* pIn = inContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrOut = (IntPtr)pOut;

                Compute.For(rowCount, row =>
                {
                    float* inRow = (float*)ptrIn + row * rowLength;
                    float* outRow = (float*)ptrOut + row * rowLength;

                    // 1. Max
                    float maxVal = float.MinValue;
                    for (int i = 0; i < rowLength; i++)
                    {
                        if (inRow[i] > maxVal) maxVal = inRow[i];
                    }

                    // 2. Exp and sum
                    float expSum = 0.0f;
                    for (int i = 0; i < rowLength; i++)
                    {
                        float e = (float)Math.Exp(inRow[i] - maxVal);
                        outRow[i] = e;
                        expSum += e;
                    }

                    // 3. Normalize
                    float invSum = 1.0f / Math.Max(expSum, 1e-12f);
                    for (int i = 0; i < rowLength; i++)
                    {
                        outRow[i] *= invSum;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        #region Kernel Fusion & Advanced Attention Primitives

        /// <summary>
        /// Fused General Matrix Multiply + Bias Addition + Activation:
        /// C = Activation(Alpha * (A @ B) + Bias)
        /// Avoids intermediate memory allocations and DRAM write-read cycles.
        /// </summary>
        public static unsafe void FusedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float>? bias,
            Tensor<float> C,
            ComputeActivationType activation = ComputeActivationType.None,
            float alpha = 1.0f)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (C == null) throw new ArgumentNullException(nameof(C));

            Gemm(A, B, C, alpha, 0.0f);

            if (bias == null && activation == ComputeActivationType.None)
                return;

            int M = A.Shape[0];
            int N = B.Shape[1];

            var cContig = C.ToContiguous();
            var biasContig = bias?.ToContiguous();
            Span<float> biasSpan = biasContig != null ? biasContig.AsSpan() : default;

            fixed (float* pC = cContig.AsSpan())
            fixed (float* pBias = biasSpan)
            {
                IntPtr ptrC = (IntPtr)pC;
                IntPtr ptrBias = (IntPtr)pBias;

                Compute.For(M, row =>
                {
                    float* rowC = (float*)ptrC + row * N;
                    float* biasArr = (float*)ptrBias;

                    for (int j = 0; j < N; j++)
                    {
                        float val = rowC[j];
                        if (biasArr != null)
                        {
                            val += biasArr[j];
                        }

                        if (activation == ComputeActivationType.ReLU)
                        {
                            val = Math.Max(0.0f, val);
                        }
                        else if (activation == ComputeActivationType.GELU)
                        {
                            float inner = 0.79788456f * (val + 0.044715f * val * val * val);
                            val = 0.5f * val * (1.0f + (float)Math.Tanh(inner));
                        }
                        else if (activation == ComputeActivationType.SiLU)
                        {
                            val = val / (1.0f + (float)Math.Exp(-val));
                        }

                        rowC[j] = val;
                    }
                }, CpuWorkloadType.MemoryBound);
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        /// <summary>
        /// Fused Residual Addition + RMSNorm:
        /// output = RMSNorm(input + residual, weight, epsilon)
        /// Single-pass execution through memory.
        /// </summary>
        public static unsafe void FusedResidualRmsNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            float epsilon = 1e-5f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (residual == null) throw new ArgumentNullException(nameof(residual));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (input.Length != residual.Length || input.Length != output.Length)
                throw new ArgumentException("Input, residual, and output tensors must have the same total elements.");

            int rank = input.Rank;
            int hiddenDim = input.Shape[rank - 1];
            int rowCount = input.Length / hiddenDim;

            var inContig = input.ToContiguous();
            var resContig = residual.ToContiguous();
            var outContig = output.ToContiguous();
            var wContig = weight?.ToContiguous();
            Span<float> wSpan = wContig != null ? wContig.AsSpan() : default;

            fixed (float* pIn = inContig.AsSpan())
            fixed (float* pRes = resContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            fixed (float* pW = wSpan)
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrRes = (IntPtr)pRes;
                IntPtr ptrOut = (IntPtr)pOut;
                IntPtr ptrW = (IntPtr)pW;

                Compute.For(rowCount, row =>
                {
                    float* inRow = (float*)ptrIn + row * hiddenDim;
                    float* resRow = (float*)ptrRes + row * hiddenDim;
                    float* outRow = (float*)ptrOut + row * hiddenDim;
                    float* wRow = (float*)ptrW;

                    float sumSq = 0.0f;
                    for (int i = 0; i < hiddenDim; i++)
                    {
                        float sum = inRow[i] + resRow[i];
                        sumSq += sum * sum;
                    }

                    float invRms = 1.0f / (float)Math.Sqrt((sumSq / hiddenDim) + epsilon);

                    if (wRow != null)
                    {
                        for (int i = 0; i < hiddenDim; i++)
                        {
                            float sum = inRow[i] + resRow[i];
                            outRow[i] = sum * invRms * wRow[i];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < hiddenDim; i++)
                        {
                            float sum = inRow[i] + resRow[i];
                            outRow[i] = sum * invRms;
                        }
                    }
                }, CpuWorkloadType.MemoryBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        /// <summary>
        /// Fused Residual Addition + LayerNorm:
        /// output = LayerNorm(input + residual, weight, bias, epsilon)
        /// </summary>
        public static unsafe void FusedResidualLayerNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            Tensor<float>? bias = null,
            float epsilon = 1e-5f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (residual == null) throw new ArgumentNullException(nameof(residual));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (input.Length != residual.Length || input.Length != output.Length)
                throw new ArgumentException("Input, residual, and output tensors must have the same total elements.");

            int rank = input.Rank;
            int hiddenDim = input.Shape[rank - 1];
            int rowCount = input.Length / hiddenDim;

            var inContig = input.ToContiguous();
            var resContig = residual.ToContiguous();
            var outContig = output.ToContiguous();
            var wContig = weight?.ToContiguous();
            var bContig = bias?.ToContiguous();
            Span<float> wSpanLN = wContig != null ? wContig.AsSpan() : default;
            Span<float> bSpanLN = bContig != null ? bContig.AsSpan() : default;

            fixed (float* pIn = inContig.AsSpan())
            fixed (float* pRes = resContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            fixed (float* pW = wSpanLN)
            fixed (float* pB = bSpanLN)
            {
                IntPtr ptrIn = (IntPtr)pIn;
                IntPtr ptrRes = (IntPtr)pRes;
                IntPtr ptrOut = (IntPtr)pOut;
                IntPtr ptrW = (IntPtr)pW;
                IntPtr ptrB = (IntPtr)pB;

                Compute.For(rowCount, row =>
                {
                    float* inRow = (float*)ptrIn + row * hiddenDim;
                    float* resRow = (float*)ptrRes + row * hiddenDim;
                    float* outRow = (float*)ptrOut + row * hiddenDim;
                    float* wRow = (float*)ptrW;
                    float* bRow = (float*)ptrB;

                    float sum = 0.0f;
                    for (int i = 0; i < hiddenDim; i++)
                    {
                        sum += inRow[i] + resRow[i];
                    }
                    float mean = sum / hiddenDim;

                    float sumVar = 0.0f;
                    for (int i = 0; i < hiddenDim; i++)
                    {
                        float diff = (inRow[i] + resRow[i]) - mean;
                        sumVar += diff * diff;
                    }
                    float invStd = 1.0f / (float)Math.Sqrt((sumVar / hiddenDim) + epsilon);

                    for (int i = 0; i < hiddenDim; i++)
                    {
                        float norm = ((inRow[i] + resRow[i]) - mean) * invStd;
                        if (wRow != null) norm *= wRow[i];
                        if (bRow != null) norm += bRow[i];
                        outRow[i] = norm;
                    }
                }, CpuWorkloadType.MemoryBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        /// <summary>
        /// Scaled Dot-Product Attention (FlashAttention-2 Online Softmax):
        /// Output = Softmax(Q * K^T * scale) * V
        /// Pure C# cache-tiled online Softmax algorithm with zero quadratic memory allocations.
        /// </summary>
        public static unsafe void ScaledDotProductAttention(
            Tensor<float> Q,
            Tensor<float> K,
            Tensor<float> V,
            Tensor<float> output,
            float? scale = null,
            bool isCausal = false)
        {
            if (Q == null) throw new ArgumentNullException(nameof(Q));
            if (K == null) throw new ArgumentNullException(nameof(K));
            if (V == null) throw new ArgumentNullException(nameof(V));
            if (output == null) throw new ArgumentNullException(nameof(output));

            int rank = Q.Rank;
            int D = Q.Shape[rank - 1];
            int seqLenQ = Q.Shape[rank - 2];
            int seqLenK = K.Shape[rank - 2];

            int batchCount = 1;
            for (int d = 0; d < rank - 2; d++) batchCount *= Q.Shape[d];

            float s = scale ?? (1.0f / (float)Math.Sqrt(D));

            var qContig = Q.ToContiguous();
            var kContig = K.ToContiguous();
            var vContig = V.ToContiguous();
            var outContig = output.ToContiguous();

            fixed (float* pQ = qContig.AsSpan())
            fixed (float* pK = kContig.AsSpan())
            fixed (float* pV = vContig.AsSpan())
            fixed (float* pOut = outContig.AsSpan())
            {
                IntPtr ptrQ = (IntPtr)pQ;
                IntPtr ptrK = (IntPtr)pK;
                IntPtr ptrV = (IntPtr)pV;
                IntPtr ptrOut = (IntPtr)pOut;

                int totalQueries = batchCount * seqLenQ;

                Compute.For(totalQueries, qGlobal =>
                {
                    int b = qGlobal / seqLenQ;
                    int i = qGlobal % seqLenQ;

                    float* qPtr = (float*)ptrQ + (b * seqLenQ + i) * D;
                    float* kBase = (float*)ptrK + b * seqLenK * D;
                    float* vBase = (float*)ptrV + b * seqLenK * D;
                    float* outPtr = (float*)ptrOut + (b * seqLenQ + i) * D;

                    float* acc = stackalloc float[D];
                    for (int d = 0; d < D; d++) acc[d] = 0.0f;

                    float m = float.NegativeInfinity;
                    float l = 0.0f;

                    for (int j = 0; j < seqLenK; j++)
                    {
                        if (isCausal && j > i) break;

                        float* kPtr = kBase + j * D;
                        float* vPtr = vBase + j * D;

                        float dot = 0.0f;
                        for (int d = 0; d < D; d++)
                        {
                            dot += qPtr[d] * kPtr[d];
                        }
                        float score = dot * s;

                        float mNext = Math.Max(m, score);
                        float alpha = (float)Math.Exp(m - mNext);
                        float beta = (float)Math.Exp(score - mNext);
                        l = l * alpha + beta;
                        m = mNext;

                        for (int d = 0; d < D; d++)
                        {
                            acc[d] = acc[d] * alpha + beta * vPtr[d];
                        }
                    }

                    float invL = 1.0f / Math.Max(l, 1e-12f);
                    for (int d = 0; d < D; d++)
                    {
                        outPtr[d] = acc[d] * invL;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(outContig, output))
            {
                outContig.CopyTo(output);
            }
        }

        /// <summary>
        /// Multi-threaded half-precision (FP16) General Matrix Multiplication with FP32 register accumulation:
        /// C = A (M x K) @ B (K x N).
        /// </summary>
        public static unsafe void Gemm(Tensor<Half> A, Tensor<Half> B, Tensor<Half> C)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (C == null) throw new ArgumentNullException(nameof(C));

            if (A.Rank != 2 || B.Rank != 2 || C.Rank != 2)
                throw new ArgumentException("Matrices must be 2-dimensional.");

            int M = A.Shape[0];
            int K = A.Shape[1];
            int N = B.Shape[1];

            if (B.Shape[0] != K || C.Shape[0] != M || C.Shape[1] != N)
                throw new ArgumentException("Matrix dimensions do not match for multiplication.");

            var aContig = A.ToContiguous();
            var bContig = B.ToContiguous();
            var cContig = C.ToContiguous();

            fixed (Half* pA = aContig.AsSpan())
            fixed (Half* pB = bContig.AsSpan())
            fixed (Half* pC = cContig.AsSpan())
            {
                IntPtr ptrA = (IntPtr)pA;
                IntPtr ptrB = (IntPtr)pB;
                IntPtr ptrC = (IntPtr)pC;

                const int tileSize = 32;

                Compute.For(M, i =>
                {
                    Half* aRow = (Half*)ptrA + i * K;
                    Half* cRow = (Half*)ptrC + i * N;
                    Half* bBase = (Half*)ptrB;

                    for (int j0 = 0; j0 < N; j0 += tileSize)
                    {
                        int jEnd = Math.Min(j0 + tileSize, N);
                        for (int j = j0; j < jEnd; j++)
                        {
                            float sum = 0.0f;
                            for (int k = 0; k < K; k++)
                            {
                                sum += (float)aRow[k] * (float)bBase[k * N + j];
                            }
                            cRow[j] = (Half)sum;
                        }
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        #region Quantized INT8 and INT4 GEMM

        /// <summary>
        /// Computes General Matrix Multiplication with INT8 weights and FP32 activations:
        /// C[m, n] = scale[n] * sum_k( A[m, k] * (B[k, n] - zp[n]) ).
        /// </summary>
        public static unsafe void GemmInt8(
            Tensor<float> A,
            Tensor<sbyte> B,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (scales == null) throw new ArgumentNullException(nameof(scales));
            if (C == null) throw new ArgumentNullException(nameof(C));

            int M = A.Shape[0];
            int K = A.Shape[1];
            int N = B.Shape[1];

            if (B.Shape[0] != K)
                throw new ArgumentException($"Inner dimensions must match: A is [{M},{K}], B is [{B.Shape[0]},{N}].");
            if (scales.Length < N)
                throw new ArgumentException($"Scales length {scales.Length} must be at least N={N}.");

            var aContig = A.IsContiguous ? A : A.ToContiguous();
            var bContig = B.IsContiguous ? B : B.ToContiguous();
            var sContig = scales.IsContiguous ? scales : scales.ToContiguous();
            var cContig = C.IsContiguous ? C : C.ToContiguous();
            var zpContig = zeroPoints != null ? (zeroPoints.IsContiguous ? zeroPoints : zeroPoints.ToContiguous()) : null;

            fixed (float* pA = aContig.AsSpan())
            fixed (sbyte* pB = bContig.AsSpan())
            fixed (float* pScales = sContig.AsSpan())
            fixed (float* pC = cContig.AsSpan())
            fixed (float* pZp = zpContig != null ? zpContig.AsSpan() : default)
            {
                IntPtr ptrA = (IntPtr)pA;
                IntPtr ptrB = (IntPtr)pB;
                IntPtr ptrS = (IntPtr)pScales;
                IntPtr ptrC = (IntPtr)pC;
                IntPtr ptrZp = (IntPtr)pZp;

                Compute.For(M, i =>
                {
                    float* aRow = (float*)ptrA + i * K;
                    float* cRow = (float*)ptrC + i * N;
                    sbyte* bBase = (sbyte*)ptrB;
                    float* sVec = (float*)ptrS;
                    float* zpVec = (float*)ptrZp;

                    for (int j = 0; j < N; j++)
                    {
                        float scale = sVec[j];
                        float zp = zpVec != null ? zpVec[j] : 0.0f;
                        float acc = 0.0f;

                        int k = 0;
                        for (; k <= K - 4; k += 4)
                        {
                            acc += aRow[k] * ((float)bBase[k * N + j] - zp);
                            acc += aRow[k + 1] * ((float)bBase[(k + 1) * N + j] - zp);
                            acc += aRow[k + 2] * ((float)bBase[(k + 2) * N + j] - zp);
                            acc += aRow[k + 3] * ((float)bBase[(k + 3) * N + j] - zp);
                        }
                        for (; k < K; k++)
                        {
                            acc += aRow[k] * ((float)bBase[k * N + j] - zp);
                        }

                        cRow[j] = acc * scale;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        /// <summary>
        /// Computes General Matrix Multiplication with packed INT4 weights and FP32 activations:
        /// Each byte in packedWeights stores two 4-bit nibbles (low nibble = row 2*l, high nibble = row 2*l+1).
        /// C[m, n] = scale[n] * sum_k( A[m, k] * (W_dequant[k, n] - zp[n]) ).
        /// </summary>
        public static unsafe void GemmInt4(
            Tensor<float> A,
            Tensor<byte> packedWeights,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (packedWeights == null) throw new ArgumentNullException(nameof(packedWeights));
            if (scales == null) throw new ArgumentNullException(nameof(scales));
            if (C == null) throw new ArgumentNullException(nameof(C));

            int M = A.Shape[0];
            int K = A.Shape[1];
            int packedK = packedWeights.Shape[0];
            int N = packedWeights.Shape[1];

            if (packedK != (K + 1) / 2)
                throw new ArgumentException($"Inner dimension mismatch: A is [{M},{K}], but packedWeights is [{packedK},{N}] (expected {(K + 1) / 2}).");
            if (scales.Length < N)
                throw new ArgumentException($"Scales length {scales.Length} must be at least N={N}.");

            var aContig = A.IsContiguous ? A : A.ToContiguous();
            var wContig = packedWeights.IsContiguous ? packedWeights : packedWeights.ToContiguous();
            var sContig = scales.IsContiguous ? scales : scales.ToContiguous();
            var cContig = C.IsContiguous ? C : C.ToContiguous();
            var zpContig = zeroPoints != null ? (zeroPoints.IsContiguous ? zeroPoints : zeroPoints.ToContiguous()) : null;

            fixed (float* pA = aContig.AsSpan())
            fixed (byte* pW = wContig.AsSpan())
            fixed (float* pScales = sContig.AsSpan())
            fixed (float* pC = cContig.AsSpan())
            fixed (float* pZp = zpContig != null ? zpContig.AsSpan() : default)
            {
                IntPtr ptrA = (IntPtr)pA;
                IntPtr ptrW = (IntPtr)pW;
                IntPtr ptrS = (IntPtr)pScales;
                IntPtr ptrC = (IntPtr)pC;
                IntPtr ptrZp = (IntPtr)pZp;

                Compute.For(M, i =>
                {
                    float* aRow = (float*)ptrA + i * K;
                    float* cRow = (float*)ptrC + i * N;
                    byte* wBase = (byte*)ptrW;
                    float* sVec = (float*)ptrS;
                    float* zpVec = (float*)ptrZp;

                    for (int j = 0; j < N; j++)
                    {
                        float scale = sVec[j];
                        float zp = zpVec != null ? zpVec[j] : 0.0f;
                        float acc = 0.0f;

                        for (int l = 0; l < packedK; l++)
                        {
                            byte packed = wBase[l * N + j];
                            int nibble0 = packed & 0x0F;
                            int nibble1 = (packed >> 4) & 0x0F;

                            int k0 = l * 2;
                            int k1 = k0 + 1;

                            float w0 = ((float)nibble0 - zp);
                            acc += aRow[k0] * w0;

                            if (k1 < K)
                            {
                                float w1 = ((float)nibble1 - zp);
                                acc += aRow[k1] * w1;
                            }
                        }

                        cRow[j] = acc * scale;
                    }
                }, CpuWorkloadType.ComputeBound);
            }

            if (!ReferenceEquals(cContig, C))
            {
                cContig.CopyTo(C);
            }
        }

        #endregion

        #endregion
    }
}
