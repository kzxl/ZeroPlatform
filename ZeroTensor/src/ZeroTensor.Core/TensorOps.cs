using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Vectorized mathematical operations, element-wise functions, broadcasting arithmetic, and reductions for tensors.
    /// </summary>
    public static partial class TensorOps
    {
        #region Binary Arithmetic with Broadcasting

        public static Tensor<T> Add<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                var fa = (Tensor<float>)(object)a;
                var fb = (Tensor<float>)(object)b;
                var fres = AddFloat(fa, fb);
                return (Tensor<T>)(object)fres;
            }
            if (typeof(T) == typeof(double))
            {
                var da = (Tensor<double>)(object)a;
                var db = (Tensor<double>)(object)b;
                var dres = AddDouble(da, db);
                return (Tensor<T>)(object)dres;
            }
            if (typeof(T) == typeof(Half))
            {
                var ha = (Tensor<Half>)(object)a;
                var hb = (Tensor<Half>)(object)b;
                var hres = AddHalf(ha, hb);
                return (Tensor<T>)(object)hres;
            }
            if (typeof(T) == typeof(BFloat16))
            {
                var bfa = (Tensor<BFloat16>)(object)a;
                var bfb = (Tensor<BFloat16>)(object)b;
                var bfres = AddBFloat16(bfa, bfb);
                return (Tensor<T>)(object)bfres;
            }

            throw new NotSupportedException($"Add not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> Subtract<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                var fa = (Tensor<float>)(object)a;
                var fb = (Tensor<float>)(object)b;
                var fres = SubtractFloat(fa, fb);
                return (Tensor<T>)(object)fres;
            }
            if (typeof(T) == typeof(double))
            {
                var da = (Tensor<double>)(object)a;
                var db = (Tensor<double>)(object)b;
                var dres = SubtractDouble(da, db);
                return (Tensor<T>)(object)dres;
            }
            if (typeof(T) == typeof(Half))
            {
                var ha = (Tensor<Half>)(object)a;
                var hb = (Tensor<Half>)(object)b;
                var hres = SubtractHalf(ha, hb);
                return (Tensor<T>)(object)hres;
            }
            if (typeof(T) == typeof(BFloat16))
            {
                var bfa = (Tensor<BFloat16>)(object)a;
                var bfb = (Tensor<BFloat16>)(object)b;
                var bfres = SubtractBFloat16(bfa, bfb);
                return (Tensor<T>)(object)bfres;
            }

            throw new NotSupportedException($"Subtract not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> Multiply<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                var fa = (Tensor<float>)(object)a;
                var fb = (Tensor<float>)(object)b;
                var fres = MultiplyFloat(fa, fb);
                return (Tensor<T>)(object)fres;
            }
            if (typeof(T) == typeof(double))
            {
                var da = (Tensor<double>)(object)a;
                var db = (Tensor<double>)(object)b;
                var dres = MultiplyDouble(da, db);
                return (Tensor<T>)(object)dres;
            }
            if (typeof(T) == typeof(Half))
            {
                var ha = (Tensor<Half>)(object)a;
                var hb = (Tensor<Half>)(object)b;
                var hres = MultiplyHalf(ha, hb);
                return (Tensor<T>)(object)hres;
            }
            if (typeof(T) == typeof(BFloat16))
            {
                var bfa = (Tensor<BFloat16>)(object)a;
                var bfb = (Tensor<BFloat16>)(object)b;
                var bfres = MultiplyBFloat16(bfa, bfb);
                return (Tensor<T>)(object)bfres;
            }

            throw new NotSupportedException($"Multiply not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> Divide<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                var fa = (Tensor<float>)(object)a;
                var fb = (Tensor<float>)(object)b;
                var fres = DivideFloat(fa, fb);
                return (Tensor<T>)(object)fres;
            }
            if (typeof(T) == typeof(double))
            {
                var da = (Tensor<double>)(object)a;
                var db = (Tensor<double>)(object)b;
                var dres = DivideDouble(da, db);
                return (Tensor<T>)(object)dres;
            }
            if (typeof(T) == typeof(Half))
            {
                var ha = (Tensor<Half>)(object)a;
                var hb = (Tensor<Half>)(object)b;
                var hres = DivideHalf(ha, hb);
                return (Tensor<T>)(object)hres;
            }
            if (typeof(T) == typeof(BFloat16))
            {
                var bfa = (Tensor<BFloat16>)(object)a;
                var bfb = (Tensor<BFloat16>)(object)b;
                var bfres = DivideBFloat16(bfa, bfb);
                return (Tensor<T>)(object)bfres;
            }

            throw new NotSupportedException($"Divide not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> Negate<T>(Tensor<T> a) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                var fa = (Tensor<float>)(object)a;
                return (Tensor<T>)(object)NegateFloat(fa);
            }
            if (typeof(T) == typeof(double))
            {
                var da = (Tensor<double>)(object)a;
                return (Tensor<T>)(object)NegateDouble(da);
            }
            if (typeof(T) == typeof(Half))
            {
                var ha = (Tensor<Half>)(object)a;
                return (Tensor<T>)(object)NegateHalf(ha);
            }
            if (typeof(T) == typeof(BFloat16))
            {
                var bfa = (Tensor<BFloat16>)(object)a;
                return (Tensor<T>)(object)NegateBFloat16(bfa);
            }

            throw new NotSupportedException($"Negate not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> AddScalar<T>(Tensor<T> a, T scalar) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                return (Tensor<T>)(object)AddScalarFloat((Tensor<float>)(object)a, (float)(object)scalar);
            }
            if (typeof(T) == typeof(double))
            {
                return (Tensor<T>)(object)AddScalarDouble((Tensor<double>)(object)a, (double)(object)scalar);
            }
            if (typeof(T) == typeof(Half))
            {
                return (Tensor<T>)(object)AddScalarHalf((Tensor<Half>)(object)a, (Half)(object)scalar);
            }

            throw new NotSupportedException($"AddScalar not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> SubtractScalar<T>(Tensor<T> a, T scalar) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                return (Tensor<T>)(object)SubtractScalarFloat((Tensor<float>)(object)a, (float)(object)scalar);
            }
            if (typeof(T) == typeof(double))
            {
                return (Tensor<T>)(object)SubtractScalarDouble((Tensor<double>)(object)a, (double)(object)scalar);
            }
            if (typeof(T) == typeof(Half))
            {
                return (Tensor<T>)(object)SubtractScalarHalf((Tensor<Half>)(object)a, (Half)(object)scalar);
            }

            throw new NotSupportedException($"SubtractScalar not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> MultiplyScalar<T>(Tensor<T> a, T scalar) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                return (Tensor<T>)(object)MultiplyScalarFloat((Tensor<float>)(object)a, (float)(object)scalar);
            }
            if (typeof(T) == typeof(double))
            {
                return (Tensor<T>)(object)MultiplyScalarDouble((Tensor<double>)(object)a, (double)(object)scalar);
            }
            if (typeof(T) == typeof(Half))
            {
                return (Tensor<T>)(object)MultiplyScalarHalf((Tensor<Half>)(object)a, (Half)(object)scalar);
            }

            throw new NotSupportedException($"MultiplyScalar not implemented for type {typeof(T)}.");
        }

        public static Tensor<T> DivideScalar<T>(Tensor<T> a, T scalar) where T : unmanaged, IEquatable<T>
        {
            if (typeof(T) == typeof(float))
            {
                return (Tensor<T>)(object)DivideScalarFloat((Tensor<float>)(object)a, (float)(object)scalar);
            }
            if (typeof(T) == typeof(double))
            {
                return (Tensor<T>)(object)DivideScalarDouble((Tensor<double>)(object)a, (double)(object)scalar);
            }
            if (typeof(T) == typeof(Half))
            {
                return (Tensor<T>)(object)DivideScalarHalf((Tensor<Half>)(object)a, (Half)(object)scalar);
            }

            throw new NotSupportedException($"DivideScalar not implemented for type {typeof(T)}.");
        }

        #endregion

        #region Float SIMD Implementations

        private static Tensor<float> AddFloat(Tensor<float> a, Tensor<float> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<float>(a.Shape);
                int offA = a.Offset;
                int offB = b.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pB = &b.Storage.GetPinnableReference(offB))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                            Unsafe.WriteUnaligned(pRes + i, va + vb);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] + pB[i];
                        }
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x + y);
        }

        private static Tensor<float> SubtractFloat(Tensor<float> a, Tensor<float> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<float>(a.Shape);
                int offA = a.Offset;
                int offB = b.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pB = &b.Storage.GetPinnableReference(offB))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                            Unsafe.WriteUnaligned(pRes + i, va - vb);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] - pB[i];
                        }
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x - y);
        }

        private static Tensor<float> MultiplyFloat(Tensor<float> a, Tensor<float> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<float>(a.Shape);
                int offA = a.Offset;
                int offB = b.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pB = &b.Storage.GetPinnableReference(offB))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                            Unsafe.WriteUnaligned(pRes + i, va * vb);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] * pB[i];
                        }
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x * y);
        }

        private static Tensor<float> DivideFloat(Tensor<float> a, Tensor<float> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<float>(a.Shape);
                int offA = a.Offset;
                int offB = b.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pB = &b.Storage.GetPinnableReference(offB))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                            Unsafe.WriteUnaligned(pRes + i, va / vb);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] / pB[i];
                        }
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x / y);
        }

        private static Tensor<float> NegateFloat(Tensor<float> a)
        {
            var result = new Tensor<float>(a.Shape);
            if (a.IsContiguous)
            {
                int offA = a.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            Unsafe.WriteUnaligned(pRes + i, -va);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = -pA[i];
                        }
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = -a[coords]);
            return result;
        }

        private static Tensor<float> AddScalarFloat(Tensor<float> a, float scalar)
        {
            var result = new Tensor<float>(a.Shape);
            if (a.IsContiguous)
            {
                int offA = a.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                var vScalar = new Vector<float>(scalar);
                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            Unsafe.WriteUnaligned(pRes + i, va + vScalar);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] + scalar;
                        }
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] + scalar);
            return result;
        }

        private static Tensor<float> SubtractScalarFloat(Tensor<float> a, float scalar)
        {
            var result = new Tensor<float>(a.Shape);
            if (a.IsContiguous)
            {
                int offA = a.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                var vScalar = new Vector<float>(scalar);
                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            Unsafe.WriteUnaligned(pRes + i, va - vScalar);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] - scalar;
                        }
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] - scalar);
            return result;
        }

        private static Tensor<float> MultiplyScalarFloat(Tensor<float> a, float scalar)
        {
            var result = new Tensor<float>(a.Shape);
            if (a.IsContiguous)
            {
                int offA = a.Offset;
                int offRes = result.Offset;
                int length = a.Length;

                var vScalar = new Vector<float>(scalar);
                int vecSize = Vector<float>.Count;
                unsafe
                {
                    fixed (float* pA = &a.Storage.GetPinnableReference(offA))
                    fixed (float* pRes = &result.Storage.GetPinnableReference(offRes))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                            Unsafe.WriteUnaligned(pRes + i, va * vScalar);
                        }
                        for (; i < length; i++)
                        {
                            pRes[i] = pA[i] * scalar;
                        }
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] * scalar);
            return result;
        }

        private static Tensor<float> DivideScalarFloat(Tensor<float> a, float scalar)
        {
            if (scalar == 0f) throw new DivideByZeroException();
            float invScalar = 1f / scalar;
            return MultiplyScalarFloat(a, invScalar);
        }

        #endregion

        #region Vectorized Element-Wise Functions

        public static Tensor<float> Exp(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Exp(x));

        public static Tensor<float> Log(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Log(x));

        public static Tensor<float> Sqrt(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Sqrt(x));

        public static Tensor<float> Abs(Tensor<float> t) => ApplyUnary(t, Math.Abs);

        public static Tensor<float> Sin(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Sin(x));

        public static Tensor<float> Cos(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Cos(x));

        public static Tensor<float> Tanh(Tensor<float> t) => ApplyUnary(t, x => (float)Math.Tanh(x));

        public static Tensor<float> Sigmoid(Tensor<float> t) => ApplyUnary(t, x => 1.0f / (1.0f + (float)Math.Exp(-x)));

        public static Tensor<float> ReLU(Tensor<float> t)
        {
            var result = new Tensor<float>(t.Shape);
            if (t.IsContiguous)
            {
                int length = t.Length;
                var zeroVec = Vector<float>.Zero;
                int vecSize = Vector<float>.Count;

                unsafe
                {
                    fixed (float* pSrc = &t.Storage.GetPinnableReference(t.Offset))
                    fixed (float* pDst = &result.Storage.GetPinnableReference(result.Offset))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            var v = Unsafe.ReadUnaligned<Vector<float>>(pSrc + i);
                            Unsafe.WriteUnaligned(pDst + i, Vector.Max(v, zeroVec));
                        }
                        for (; i < length; i++)
                        {
                            pDst[i] = Math.Max(0f, pSrc[i]);
                        }
                    }
                }
                return result;
            }

            return ApplyUnary(t, x => Math.Max(0f, x));
        }

        public static Tensor<float> GELU(Tensor<float> t)
        {
            var contig = t.IsContiguous ? t : t.ToContiguous();
            var result = new Tensor<float>(contig.Shape);
            int len = contig.Length;
            const float sqrt2OverPi = 0.7978845608f;
            const float geluCoeff = 0.044715f;

            System.Threading.Tasks.Parallel.For(0, (len + 1023) / 1024, chunkIdx =>
            {
                int start = chunkIdx * 1024;
                int count = Math.Min(1024, len - start);
                ref float pSrc = ref contig.Storage.GetPinnableReference(contig.Offset + start);
                ref float pDst = ref result.Storage.GetPinnableReference(result.Offset + start);

                for (int i = 0; i < count; i++)
                {
                    float val = System.Runtime.CompilerServices.Unsafe.Add(ref pSrc, i);
                    float inner = sqrt2OverPi * (val + geluCoeff * val * val * val);
                    System.Runtime.CompilerServices.Unsafe.Add(ref pDst, i) = 0.5f * val * (1.0f + (float)Math.Tanh(inner));
                }
            });

            return result;
        }

        public static Tensor<float> Clamp(Tensor<float> t, float min, float max)
        {
            return ApplyUnary(t, x => x < min ? min : (x > max ? max : x));
        }

        public static Tensor<float> Pow(Tensor<float> t, float power)
        {
            return ApplyUnary(t, x => (float)Math.Pow(x, power));
        }

        private static Tensor<float> ApplyUnary(Tensor<float> t, Func<float, float> func)
        {
            var result = new Tensor<float>(t.Shape);
            if (t.IsContiguous)
            {
                var srcSpan = t.AsReadOnlySpan();
                var dstSpan = result.AsSpan();
                for (int i = 0; i < srcSpan.Length; i++)
                {
                    dstSpan[i] = func(srcSpan[i]);
                }
                return result;
            }

            t.ForEachCoordinate(coords => result[coords] = func(t[coords]));
            return result;
        }

        #endregion

        #region Axis Reductions

        /// <summary>
        /// Computes the sum of elements along the specified axis (or all elements if axis is -1).
        /// </summary>
        public static Tensor<float> Sum(Tensor<float> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                float sum = 0f;
                if (t.IsContiguous)
                {
                    int length = t.Length;
                    int vecSize = Vector<float>.Count;
                    unsafe
                    {
                        fixed (float* pSrc = &t.Storage.GetPinnableReference(t.Offset))
                        {
                            var acc = Vector<float>.Zero;
                            int i = 0;
                            for (; i <= length - vecSize; i += vecSize)
                            {
                                acc += Unsafe.ReadUnaligned<Vector<float>>(pSrc + i);
                            }
                            for (int v = 0; v < vecSize; v++) sum += acc[v];
                            for (; i < length; i++) sum += pSrc[i];
                        }
                    }
                }
                else
                {
                    t.ForEachElement(v => sum += v);
                }

                if (keepDims)
                {
                    var keepShape = new int[t.Rank];
                    for (int k = 0; k < t.Rank; k++) keepShape[k] = 1;
                    return Tensor.FromArray(new[] { sum }, keepShape);
                }

                return Tensor.FromArray(new[] { sum });
            }

            return ReduceAxis(t, axis, keepDims, 0f, (acc, val) => acc + val);
        }

        /// <summary>
        /// Computes the arithmetic mean of elements along the specified axis (or all elements if axis is -1).
        /// </summary>
        public static Tensor<float> Mean(Tensor<float> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                var sum = Sum(t, -1, keepDims);
                return sum / (float)t.Length;
            }

            int ax = axis < 0 ? axis + t.Rank : axis;
            int count = t.Shape[ax];
            var sumTensor = Sum(t, axis, keepDims);
            return sumTensor / (float)count;
        }

        /// <summary>
        /// Computes the maximum of elements along the specified axis.
        /// </summary>
        public static Tensor<float> Max(Tensor<float> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                float maxVal = float.NegativeInfinity;
                t.ForEachElement(v => { if (v > maxVal) maxVal = v; });
                return Tensor.FromArray(new[] { maxVal });
            }

            return ReduceAxis(t, axis, keepDims, float.NegativeInfinity, Math.Max);
        }

        /// <summary>
        /// Computes the minimum of elements along the specified axis.
        /// </summary>
        public static Tensor<float> Min(Tensor<float> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                float minVal = float.PositiveInfinity;
                t.ForEachElement(v => { if (v < minVal) minVal = v; });
                return Tensor.FromArray(new[] { minVal });
            }

            return ReduceAxis(t, axis, keepDims, float.PositiveInfinity, Math.Min);
        }

        /// <summary>
        /// Returns the indices of the maximum values along an axis.
        /// </summary>
        public static Tensor<int> ArgMax(Tensor<float> t, int axis = -1)
        {
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, false);
            var result = new Tensor<int>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = ExpandCoordsForAxis(coords, ax, 0);
                float maxVal = float.NegativeInfinity;
                int bestIdx = 0;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    float val = t[srcCoords];
                    if (val > maxVal)
                    {
                        maxVal = val;
                        bestIdx = i;
                    }
                }

                result[coords] = bestIdx;
            });

            return result;
        }

        /// <summary>
        /// Returns the indices of the minimum values along an axis.
        /// </summary>
        public static Tensor<int> ArgMin(Tensor<float> t, int axis = -1)
        {
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, false);
            var result = new Tensor<int>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = ExpandCoordsForAxis(coords, ax, 0);
                float minVal = float.PositiveInfinity;
                int bestIdx = 0;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    float val = t[srcCoords];
                    if (val < minVal)
                    {
                        minVal = val;
                        bestIdx = i;
                    }
                }

                result[coords] = bestIdx;
            });

            return result;
        }

        /// <summary>
        /// Computes the sample or population variance along the specified axis.
        /// </summary>
        public static Tensor<float> Variance(Tensor<float> t, int axis = -1, bool unbiased = true)
        {
            var mean = Mean(t, axis, keepDims: true);
            var diff = t - mean;
            var sqDiff = diff * diff;

            int count = axis == -1 ? t.Length : t.Shape[axis < 0 ? axis + t.Rank : axis];
            int denom = unbiased ? Math.Max(1, count - 1) : count;

            return Sum(sqDiff, axis, keepDims: false) / (float)denom;
        }

        /// <summary>
        /// Computes the standard deviation along the specified axis.
        /// </summary>
        public static Tensor<float> Std(Tensor<float> t, int axis = -1, bool unbiased = true)
        {
            return Sqrt(Variance(t, axis, unbiased));
        }

        /// <summary>
        /// Computes numerically stable softmax along the specified axis.
        /// Uses high-speed fused single-pass SIMD kernel along the last axis.
        /// </summary>
        public static Tensor<float> Softmax(Tensor<float> t, int axis = -1) => SoftmaxFast(t, axis);

        internal static Tensor<float> SoftmaxReduce(Tensor<float> t, int axis)
        {
            var maxVal = Max(t, axis, keepDims: true);
            var exp = Exp(t - maxVal);
            var sumExp = Sum(exp, axis, keepDims: true);
            return exp / sumExp;
        }

        private static Tensor<float> ReduceAxis(
            Tensor<float> t,
            int axis,
            bool keepDims,
            float initialValue,
            Func<float, float, float> reducer)
        {
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, keepDims);
            var result = new Tensor<float>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = keepDims ? (int[])coords.Clone() : ExpandCoordsForAxis(coords, ax, 0);
                float acc = initialValue;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    acc = reducer(acc, t[srcCoords]);
                }

                result[coords] = acc;
            });

            return result;
        }

        private static int[] GetReducedDimensions(TensorShape shape, int axis, bool keepDims)
        {
            if (keepDims)
            {
                var dims = shape.ToArray();
                dims[axis] = 1;
                return dims;
            }

            int newRank = shape.Rank - 1;
            if (newRank == 0) return Array.Empty<int>();

            var res = new int[newRank];
            for (int i = 0, j = 0; i < shape.Rank; i++)
            {
                if (i != axis)
                {
                    res[j++] = shape[i];
                }
            }

            return res;
        }

        private static int[] ExpandCoordsForAxis(int[] coords, int targetAxis, int defaultVal)
        {
            var res = new int[coords.Length + 1];
            for (int i = 0, j = 0; i < res.Length; i++)
            {
                if (i == targetAxis)
                {
                    res[i] = defaultVal;
                }
                else
                {
                    res[i] = coords[j++];
                }
            }
            return res;
        }

        #endregion
    }
}
