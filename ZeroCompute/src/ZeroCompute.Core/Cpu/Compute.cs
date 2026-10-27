using System;
using ZeroTensor.Core;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// Represents a high-performance element-wise operator capable of being inlined by the JIT into vectorized compute loops.
    /// </summary>
    public interface IElementwiseOp<T>
    {
        /// <summary>
        /// Applies the operator transformation to a single element.
        /// </summary>
        T Apply(T value);
    }

    /// <summary>
    /// Sovereign CPU Parallel Compute Runtime API.
    /// Provides CUDA-like parallel abstractions (For, Tile2D, Map, FusedMap, Reduce, Vector)
    /// running exclusively on modern CPUs with automatic core pinning, cache tiling, SIMD unrolling,
    /// and deterministic execution planning.
    /// </summary>
    public static class Compute
    {
        #region 1D Parallel For

        /// <summary>
        /// Executes a parallel for loop over an iteration range [0, count).
        /// Automatically selects sequential or multi-core execution based on deterministic heuristics.
        /// </summary>
        /// <param name="count">Total number of iterations.</param>
        /// <param name="body">Delegate executed for each index.</param>
        /// <param name="workload">Workload classification guiding core allocation.</param>
        public static void For(int count, Action<int> body, CpuWorkloadType workload = CpuWorkloadType.Auto)
        {
            if (count <= 0) return;
            if (body == null) throw new ArgumentNullException(nameof(body));

            var plan = ExecutionPlanner.Plan1D(count, workload);
            if (!plan.IsParallel)
            {
                for (int i = 0; i < count; i++)
                {
                    body(i);
                }
                return;
            }

            ComputeWorkerPool.Shared.DispatchRange(count, plan.WorkerCount, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    body(i);
                }
            });
        }

        /// <summary>
        /// Executes a chunked parallel loop over range [0, count) with range-based callbacks (start, end).
        /// Eliminates delegate invocation overhead per item and enables batch vectorization.
        /// </summary>
        /// <param name="count">Total number of iterations.</param>
        /// <param name="rangeBody">Delegate executed with chunk boundaries (startIndex, endIndex).</param>
        /// <param name="workload">Workload classification guiding core allocation.</param>
        public static void For(int count, Action<int, int> rangeBody, CpuWorkloadType workload = CpuWorkloadType.Auto)
        {
            if (count <= 0) return;
            if (rangeBody == null) throw new ArgumentNullException(nameof(rangeBody));

            var plan = ExecutionPlanner.Plan1D(count, workload);
            if (!plan.IsParallel)
            {
                rangeBody(0, count);
                return;
            }

            ComputeWorkerPool.Shared.DispatchRange(count, plan.WorkerCount, rangeBody);
        }

        /// <summary>
        /// Executes a range-based parallel loop with explicit worker count override.
        /// </summary>
        public static void For(int count, int maxWorkers, Action<int, int> rangeBody)
        {
            if (count <= 0) return;
            if (rangeBody == null) throw new ArgumentNullException(nameof(rangeBody));

            var plan = ExecutionPlanner.Plan1D(count, CpuWorkloadType.Auto, maxWorkers);
            if (!plan.IsParallel)
            {
                rangeBody(0, count);
                return;
            }

            ComputeWorkerPool.Shared.DispatchRange(count, plan.WorkerCount, rangeBody);
        }

        #endregion

        #region 2D Cache Tiling

        /// <summary>
        /// Executes a 2D computation partitioned into cache-friendly rectangular tiles.
        /// Ensures data within each tile remains resident in L1/L2 cache during processing.
        /// </summary>
        /// <param name="width">2D domain width (X dimension).</param>
        /// <param name="height">2D domain height (Y dimension).</param>
        /// <param name="tileW">Tile width in elements (0 for auto default 64).</param>
        /// <param name="tileH">Tile height in elements (0 for auto default 64).</param>
        /// <param name="tileAction">Callback receiving tile bounds: (xStart, yStart, xEnd, yEnd).</param>
        /// <param name="workload">Workload classification.</param>
        public static void Tile2D(
            int width,
            int height,
            int tileW,
            int tileH,
            Action<int, int, int, int> tileAction,
            CpuWorkloadType workload = CpuWorkloadType.ComputeBound)
        {
            if (width <= 0 || height <= 0) return;
            if (tileAction == null) throw new ArgumentNullException(nameof(tileAction));

            ExecutionPlanner.Plan2D(width, height, ref tileW, ref tileH);

            int tilesY = (height + tileH - 1) / tileH;

            long totalElements = (long)width * height;
            if (totalElements < ExecutionPlanner.DefaultSequentialThreshold)
            {
                for (int ty = 0; ty < tilesY; ty++)
                {
                    int y0 = ty * tileH;
                    int y1 = Math.Min(y0 + tileH, height);

                    for (int x0 = 0; x0 < width; x0 += tileW)
                    {
                        int x1 = Math.Min(x0 + tileW, width);
                        tileAction(x0, y0, x1, y1);
                    }
                }
                return;
            }

            // Partition tile rows across worker threads to maximize contiguous row spatial locality
            For(tilesY, ComputeWorkerPool.Shared.WorkerCount, (tyStart, tyEnd) =>
            {
                for (int ty = tyStart; ty < tyEnd; ty++)
                {
                    int y0 = ty * tileH;
                    int y1 = Math.Min(y0 + tileH, height);

                    for (int x0 = 0; x0 < width; x0 += tileW)
                    {
                        int x1 = Math.Min(x0 + tileW, width);
                        tileAction(x0, y0, x1, y1);
                    }
                }
            });
        }

        #endregion

        #region Kernel Fusion & Element-wise Map

        /// <summary>
        /// Maps an input array to an output array using the specified transformation.
        /// Automatically parallelizes across cores when count exceeds the sequential threshold.
        /// </summary>
        public static void Map<TIn, TOut>(TIn[] source, TOut[] destination, Func<TIn, TOut> mapFunc)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (mapFunc == null) throw new ArgumentNullException(nameof(mapFunc));
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination length is smaller than source length.", nameof(destination));

            int count = source.Length;
            For(count, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    destination[i] = mapFunc(source[i]);
                }
            }, CpuWorkloadType.Auto);
        }

        /// <summary>
        /// Executes a fused element-wise kernel on arrays in a single cache pass,
        /// avoiding multiple round-trips to DRAM.
        /// </summary>
        public static void FusedMap<T>(T[] source, T[] destination, Func<T, T> fusedKernel)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (fusedKernel == null) throw new ArgumentNullException(nameof(fusedKernel));
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination length is smaller than source length.", nameof(destination));

            int count = source.Length;
            For(count, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    destination[i] = fusedKernel(source[i]);
                }
            }, CpuWorkloadType.ComputeBound);
        }

        /// <summary>
        /// Executes an ultra-high performance fused element-wise kernel on arrays in a single cache pass,
        /// using a specialized struct operator that eliminates delegate invocation overhead and enables JIT inlining.
        /// </summary>
        public static void FusedMap<T, TOp>(T[] source, T[] destination, TOp op)
            where TOp : struct, IElementwiseOp<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination length is smaller than source length.", nameof(destination));

            int count = source.Length;
            For(count, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    destination[i] = op.Apply(source[i]);
                }
            }, CpuWorkloadType.ComputeBound);
        }

        /// <summary>
        /// Executes an ultra-high performance fused element-wise kernel on Spans in a single cache pass,
        /// using a specialized struct operator that eliminates delegate invocation overhead and enables JIT inlining.
        /// </summary>
        public static unsafe void FusedMap<T, TOp>(ReadOnlySpan<T> source, Span<T> destination, TOp op)
            where T : unmanaged
            where TOp : struct, IElementwiseOp<T>
        {
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination length is smaller than source length.", nameof(destination));

            int count = source.Length;
            fixed (T* pSrc = source)
            fixed (T* pDst = destination)
            {
                T* ptrSrc = pSrc;
                T* ptrDst = pDst;
                For(count, (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        ptrDst[i] = op.Apply(ptrSrc[i]);
                    }
                }, CpuWorkloadType.ComputeBound);
            }
        }

        /// <summary>
        /// Maps a source Tensor to a destination Tensor.
        /// </summary>
        public static void Map<TIn, TOut>(Tensor<TIn> source, Tensor<TOut> destination, Func<TIn, TOut> mapFunc)
            where TIn : unmanaged, IEquatable<TIn>
            where TOut : unmanaged, IEquatable<TOut>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (mapFunc == null) throw new ArgumentNullException(nameof(mapFunc));
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination tensor length is smaller than source tensor length.", nameof(destination));

            var sFlat = source.ToContiguous().Flatten();
            var dFlat = destination.ToContiguous().Flatten();

            int count = sFlat.Length;
            For(count, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    dFlat[i] = mapFunc(sFlat[i]);
                }
            }, CpuWorkloadType.Auto);

            if (!ReferenceEquals(dFlat, destination))
            {
                dFlat.CopyTo(destination);
            }
        }

        #endregion

        #region Parallel Reduction

        /// <summary>
        /// Computes the sum of all elements in an array using multi-core SIMD reduction.
        /// </summary>
        public static unsafe float ReduceSum(float[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int count = data.Length;
            if (count == 0) return 0f;

            var plan = ExecutionPlanner.Plan1D(count, CpuWorkloadType.MemoryBound);
            if (!plan.IsParallel)
            {
                fixed (float* pData = data)
                {
                    return ComputeReduceOps.Sum(pData, 0, count);
                }
            }

            int workers = plan.WorkerCount;
            float[] partialSums = new float[workers];

            fixed (float* pData = data)
            {
                float* ptr = pData;
                ComputeWorkerPool.Shared.DispatchRangeIndexed(count, workers, (start, end, workerIdx) =>
                {
                    partialSums[workerIdx] = ComputeReduceOps.Sum(ptr, start, end);
                });
            }

            float total = 0f;
            for (int w = 0; w < workers; w++)
            {
                total += partialSums[w];
            }
            return total;
        }

        /// <summary>
        /// Computes the maximum value in an array using multi-core SIMD reduction.
        /// </summary>
        public static unsafe float ReduceMax(float[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int count = data.Length;
            if (count == 0) return float.MinValue;

            var plan = ExecutionPlanner.Plan1D(count, CpuWorkloadType.MemoryBound);
            if (!plan.IsParallel)
            {
                fixed (float* pData = data)
                {
                    return ComputeReduceOps.Max(pData, 0, count);
                }
            }

            int workers = plan.WorkerCount;
            float[] partialMax = new float[workers];

            fixed (float* pData = data)
            {
                float* ptr = pData;
                ComputeWorkerPool.Shared.DispatchRangeIndexed(count, workers, (start, end, workerIdx) =>
                {
                    partialMax[workerIdx] = ComputeReduceOps.Max(ptr, start, end);
                });
            }

            float max = float.MinValue;
            for (int w = 0; w < workers; w++)
            {
                if (partialMax[w] > max) max = partialMax[w];
            }
            return max;
        }

        /// <summary>
        /// Computes the minimum value in an array using multi-core SIMD reduction.
        /// </summary>
        public static unsafe float ReduceMin(float[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int count = data.Length;
            if (count == 0) return float.MaxValue;

            var plan = ExecutionPlanner.Plan1D(count, CpuWorkloadType.MemoryBound);
            if (!plan.IsParallel)
            {
                fixed (float* pData = data)
                {
                    return ComputeReduceOps.Min(pData, 0, count);
                }
            }

            int workers = plan.WorkerCount;
            float[] partialMin = new float[workers];

            fixed (float* pData = data)
            {
                float* ptr = pData;
                ComputeWorkerPool.Shared.DispatchRangeIndexed(count, workers, (start, end, workerIdx) =>
                {
                    partialMin[workerIdx] = ComputeReduceOps.Min(ptr, start, end);
                });
            }

            float min = float.MaxValue;
            for (int w = 0; w < workers; w++)
            {
                if (partialMin[w] < min) min = partialMin[w];
            }
            return min;
        }

        /// <summary>
        /// General-purpose multi-core associative reduction.
        /// </summary>
        public static T Reduce<T>(T[] data, Func<T, T, T> reducer, T initialValue)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (reducer == null) throw new ArgumentNullException(nameof(reducer));
            int count = data.Length;
            if (count == 0) return initialValue;

            var plan = ExecutionPlanner.Plan1D(count, CpuWorkloadType.Auto);
            if (!plan.IsParallel)
            {
                T acc = initialValue;
                for (int i = 0; i < count; i++)
                {
                    acc = reducer(acc, data[i]);
                }
                return acc;
            }

            int workers = plan.WorkerCount;
            T[] partialAcc = new T[workers];

            ComputeWorkerPool.Shared.DispatchRangeIndexed(count, workers, (start, end, workerIdx) =>
            {
                T localAcc = initialValue;
                for (int i = start; i < end; i++)
                {
                    localAcc = reducer(localAcc, data[i]);
                }
                partialAcc[workerIdx] = localAcc;
            });

            T finalAcc = partialAcc[0];
            for (int w = 1; w < workers; w++)
            {
                finalAcc = reducer(finalAcc, partialAcc[w]);
            }
            return finalAcc;
        }

        #endregion

        #region Nested Vector Subsystem

        /// <summary>
        /// Unified SIMD Vector operations subsystem.
        /// Automatically handles multi-core batching combined with inner-loop SIMD unrolling.
        /// </summary>
        public static class Vector
        {
            /// <summary>
            /// Vectorized parallel addition: result[i] = a[i] + b[i].
            /// </summary>
            public static unsafe void Add(float[] a, float[] b, float[] result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (b.Length < a.Length || result.Length < a.Length)
                    throw new ArgumentException("Lengths must match or exceed source array length.");

                int count = a.Length;
                fixed (float* pA = a)
                fixed (float* pB = b)
                fixed (float* pR = result)
                {
                    float* ptrA = pA;
                    float* ptrB = pB;
                    float* ptrR = pR;

                    For(count, (start, end) =>
                    {
                        ComputeVectorOps.Add(ptrA, ptrB, ptrR, start, end);
                    }, CpuWorkloadType.MemoryBound);
                }
            }

            /// <summary>
            /// Vectorized parallel multiplication: result[i] = a[i] * b[i].
            /// </summary>
            public static unsafe void Multiply(float[] a, float[] b, float[] result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (b.Length < a.Length || result.Length < a.Length)
                    throw new ArgumentException("Lengths must match or exceed source array length.");

                int count = a.Length;
                fixed (float* pA = a)
                fixed (float* pB = b)
                fixed (float* pR = result)
                {
                    float* ptrA = pA;
                    float* ptrB = pB;
                    float* ptrR = pR;

                    For(count, (start, end) =>
                    {
                        ComputeVectorOps.Multiply(ptrA, ptrB, ptrR, start, end);
                    }, CpuWorkloadType.MemoryBound);
                }
            }

            /// <summary>
            /// Vectorized parallel scale: result[i] = a[i] * scalar.
            /// </summary>
            public static unsafe void Scale(float[] a, float scalar, float[] result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (result.Length < a.Length)
                    throw new ArgumentException("Result array length must match or exceed source array length.");

                int count = a.Length;
                fixed (float* pA = a)
                fixed (float* pR = result)
                {
                    float* ptrA = pA;
                    float* ptrR = pR;

                    For(count, (start, end) =>
                    {
                        ComputeVectorOps.Scale(ptrA, scalar, ptrR, start, end);
                    }, CpuWorkloadType.MemoryBound);
                }
            }

            /// <summary>
            /// Vectorized parallel Fused Multiply-Add: result[i] = a[i] * b[i] + c[i].
            /// </summary>
            public static unsafe void Fma(float[] a, float[] b, float[] c, float[] result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (c == null) throw new ArgumentNullException(nameof(c));
                if (result == null) throw new ArgumentNullException(nameof(result));

                int count = a.Length;
                fixed (float* pA = a)
                fixed (float* pB = b)
                fixed (float* pC = c)
                fixed (float* pR = result)
                {
                    float* ptrA = pA;
                    float* ptrB = pB;
                    float* ptrC = pC;
                    float* ptrR = pR;

                    For(count, (start, end) =>
                    {
                        ComputeVectorOps.Fma(ptrA, ptrB, ptrC, ptrR, start, end);
                    }, CpuWorkloadType.ComputeBound);
                }
            }

            /// <summary>
            /// Vectorized parallel addition for Tensors.
            /// </summary>
            public static unsafe void Add(Tensor<float> a, Tensor<float> b, Tensor<float> result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (result == null) throw new ArgumentNullException(nameof(result));

                var aFlat = a.ToContiguous().Flatten();
                var bFlat = b.ToContiguous().Flatten();
                var rFlat = result.ToContiguous().Flatten();

                fixed (float* pA = &aFlat[0])
                fixed (float* pB = &bFlat[0])
                fixed (float* pR = &rFlat[0])
                {
                    float* ptrA = pA;
                    float* ptrB = pB;
                    float* ptrR = pR;

                    For(aFlat.Length, (start, end) =>
                    {
                        ComputeVectorOps.Add(ptrA, ptrB, ptrR, start, end);
                    }, CpuWorkloadType.MemoryBound);
                }

                if (!ReferenceEquals(rFlat, result))
                {
                    rFlat.CopyTo(result);
                }
            }

            /// <summary>
            /// Vectorized parallel multiplication for Tensors.
            /// </summary>
            public static unsafe void Multiply(Tensor<float> a, Tensor<float> b, Tensor<float> result)
            {
                if (a == null) throw new ArgumentNullException(nameof(a));
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (result == null) throw new ArgumentNullException(nameof(result));

                var aFlat = a.ToContiguous().Flatten();
                var bFlat = b.ToContiguous().Flatten();
                var rFlat = result.ToContiguous().Flatten();

                fixed (float* pA = &aFlat[0])
                fixed (float* pB = &bFlat[0])
                fixed (float* pR = &rFlat[0])
                {
                    float* ptrA = pA;
                    float* ptrB = pB;
                    float* ptrR = pR;

                    For(aFlat.Length, (start, end) =>
                    {
                        ComputeVectorOps.Multiply(ptrA, ptrB, ptrR, start, end);
                    }, CpuWorkloadType.MemoryBound);
                }

                if (!ReferenceEquals(rFlat, result))
                {
                    rFlat.CopyTo(result);
                }
            }

            /// <summary>
            /// Vectorized high-precision GELU activation using AVX2 SIMD rational approximation.
            /// </summary>
            public static unsafe void Gelu(float[] input, float[] output)
            {
                if (input == null) throw new ArgumentNullException(nameof(input));
                if (output == null) throw new ArgumentNullException(nameof(output));
                if (output.Length < input.Length)
                    throw new ArgumentException("Output length must match or exceed input length.");

                int count = input.Length;
                fixed (float* pIn = input)
                fixed (float* pOut = output)
                {
                    float* ptrIn = pIn;
                    float* ptrOut = pOut;

                    For(count, (start, end) =>
                    {
                        ComputeVectorOps.Gelu(ptrIn, ptrOut, start, end);
                    }, CpuWorkloadType.ComputeBound);
                }
            }

            /// <summary>
            /// Vectorized high-precision GELU activation for Tensors.
            /// </summary>
            public static unsafe void Gelu(Tensor<float> input, Tensor<float> output)
            {
                if (input == null) throw new ArgumentNullException(nameof(input));
                if (output == null) throw new ArgumentNullException(nameof(output));

                var inFlat = input.ToContiguous().Flatten();
                var outFlat = output.ToContiguous().Flatten();

                fixed (float* pIn = &inFlat[0])
                fixed (float* pOut = &outFlat[0])
                {
                    float* ptrIn = pIn;
                    float* ptrOut = pOut;

                    For(inFlat.Length, (start, end) =>
                    {
                        ComputeVectorOps.Gelu(ptrIn, ptrOut, start, end);
                    }, CpuWorkloadType.ComputeBound);
                }

                if (!ReferenceEquals(outFlat, output))
                {
                    outFlat.CopyTo(output);
                }
            }
        }

        #endregion
    }
}
