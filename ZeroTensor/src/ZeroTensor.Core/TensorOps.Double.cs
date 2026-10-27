using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZeroTensor.Core
{
    public static partial class TensorOps
    {
        #region Double SIMD Implementations

        internal static unsafe Tensor<double> AddDouble(Tensor<double> a, Tensor<double> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<double>(a.Shape);
                int length = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pRes + i, va + vb);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] + pB[i];
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x + y);
        }

        internal static unsafe Tensor<double> SubtractDouble(Tensor<double> a, Tensor<double> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<double>(a.Shape);
                int length = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pRes + i, va - vb);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] - pB[i];
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x - y);
        }

        internal static unsafe Tensor<double> MultiplyDouble(Tensor<double> a, Tensor<double> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<double>(a.Shape);
                int length = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pRes + i, va * vb);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] * pB[i];
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x * y);
        }

        internal static unsafe Tensor<double> DivideDouble(Tensor<double> a, Tensor<double> b)
        {
            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<double>(a.Shape);
                int length = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pRes + i, va / vb);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] / pB[i];
                    }
                }

                return result;
            }

            return TensorBroadcaster.Apply(a, b, (x, y) => x / y);
        }

        internal static unsafe Tensor<double> NegateDouble(Tensor<double> a)
        {
            var result = new Tensor<double>(a.Shape);
            if (a.IsContiguous)
            {
                int length = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pRes + i, -va);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = -pA[i];
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = -a[coords]);
            return result;
        }

        internal static unsafe Tensor<double> AddScalarDouble(Tensor<double> a, double scalar)
        {
            var result = new Tensor<double>(a.Shape);
            if (a.IsContiguous)
            {
                int length = a.Length;
                var vScalar = new Vector<double>(scalar);
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pRes + i, va + vScalar);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] + scalar;
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] + scalar);
            return result;
        }

        internal static unsafe Tensor<double> SubtractScalarDouble(Tensor<double> a, double scalar)
        {
            var result = new Tensor<double>(a.Shape);
            if (a.IsContiguous)
            {
                int length = a.Length;
                var vScalar = new Vector<double>(scalar);
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pRes + i, va - vScalar);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] - scalar;
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] - scalar);
            return result;
        }

        internal static unsafe Tensor<double> MultiplyScalarDouble(Tensor<double> a, double scalar)
        {
            var result = new Tensor<double>(a.Shape);
            if (a.IsContiguous)
            {
                int length = a.Length;
                var vScalar = new Vector<double>(scalar);
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pRes = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pRes + i, va * vScalar);
                    }
                    for (; i < length; i++)
                    {
                        pRes[i] = pA[i] * scalar;
                    }
                }
                return result;
            }

            a.ForEachCoordinate(coords => result[coords] = a[coords] * scalar);
            return result;
        }

        internal static Tensor<double> DivideScalarDouble(Tensor<double> a, double scalar)
        {
            if (scalar == 0.0) throw new DivideByZeroException();
            double invScalar = 1.0 / scalar;
            return MultiplyScalarDouble(a, invScalar);
        }

        #endregion

        #region Double Vectorized Element-Wise Functions

        public static Tensor<double> Exp(Tensor<double> t) => ApplyUnaryDouble(t, Math.Exp);
        public static Tensor<double> Log(Tensor<double> t) => ApplyUnaryDouble(t, Math.Log);
        public static Tensor<double> Sqrt(Tensor<double> t) => ApplyUnaryDouble(t, Math.Sqrt);
        public static Tensor<double> Abs(Tensor<double> t) => ApplyUnaryDouble(t, Math.Abs);
        public static Tensor<double> Sin(Tensor<double> t) => ApplyUnaryDouble(t, Math.Sin);
        public static Tensor<double> Cos(Tensor<double> t) => ApplyUnaryDouble(t, Math.Cos);
        public static Tensor<double> Tanh(Tensor<double> t) => ApplyUnaryDouble(t, Math.Tanh);
        public static Tensor<double> Sigmoid(Tensor<double> t) => ApplyUnaryDouble(t, x => 1.0 / (1.0 + Math.Exp(-x)));

        public static unsafe Tensor<double> ReLU(Tensor<double> t)
        {
            var result = new Tensor<double>(t.Shape);
            if (t.IsContiguous)
            {
                int length = t.Length;
                var zeroVec = Vector<double>.Zero;
                int vecSize = Vector<double>.Count;
                fixed (double* pSrc = &t.Storage.GetPinnableReference(t.Offset))
                fixed (double* pDst = &result.Storage.GetPinnableReference(result.Offset))
                {
                    int i = 0;
                    for (; i <= length - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<double>>(pSrc + i);
                        Unsafe.WriteUnaligned(pDst + i, Vector.Max(v, zeroVec));
                    }
                    for (; i < length; i++)
                    {
                        pDst[i] = Math.Max(0.0, pSrc[i]);
                    }
                }
                return result;
            }

            return ApplyUnaryDouble(t, x => Math.Max(0.0, x));
        }

        public static Tensor<double> GELU(Tensor<double> t)
        {
            const double sqrt2OverPi = 0.7978845608028654;
            return ApplyUnaryDouble(t, x => 0.5 * x * (1.0 + Math.Tanh(sqrt2OverPi * (x + 0.044715 * x * x * x))));
        }

        public static Tensor<double> Clamp(Tensor<double> t, double min, double max)
        {
            return ApplyUnaryDouble(t, x => x < min ? min : (x > max ? max : x));
        }

        public static Tensor<double> Pow(Tensor<double> t, double power)
        {
            return ApplyUnaryDouble(t, x => Math.Pow(x, power));
        }

        private static unsafe Tensor<double> ApplyUnaryDouble(Tensor<double> t, Func<double, double> func)
        {
            var result = new Tensor<double>(t.Shape);
            if (t.IsContiguous)
            {
                int length = t.Length;
                fixed (double* pSrc = &t.Storage.GetPinnableReference(t.Offset))
                fixed (double* pDst = &result.Storage.GetPinnableReference(result.Offset))
                {
                    for (int i = 0; i < length; i++)
                    {
                        pDst[i] = func(pSrc[i]);
                    }
                }
                return result;
            }

            t.ForEachCoordinate(coords => result[coords] = func(t[coords]));
            return result;
        }

        #endregion

        #region Double Reductions

        public static unsafe Tensor<double> Sum(Tensor<double> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                double sum = 0.0;
                if (t.IsContiguous)
                {
                    int length = t.Length;
                    int vecSize = Vector<double>.Count;
                    var acc = Vector<double>.Zero;
                    fixed (double* p = &t.Storage.GetPinnableReference(t.Offset))
                    {
                        int i = 0;
                        for (; i <= length - vecSize; i += vecSize)
                        {
                            acc += Unsafe.ReadUnaligned<Vector<double>>(p + i);
                        }
                        for (int v = 0; v < vecSize; v++) sum += acc[v];
                        for (; i < length; i++) sum += p[i];
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

            return ReduceAxisDouble(t, axis, keepDims, 0.0, (acc, val) => acc + val);
        }

        public static Tensor<double> Mean(Tensor<double> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                var sum = Sum(t, -1, keepDims);
                return sum / (double)t.Length;
            }

            int ax = axis < 0 ? axis + t.Rank : axis;
            int count = t.Shape[ax];
            var sumTensor = Sum(t, axis, keepDims);
            return sumTensor / (double)count;
        }

        public static Tensor<double> Max(Tensor<double> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                double maxVal = double.NegativeInfinity;
                t.ForEachElement(v => { if (v > maxVal) maxVal = v; });
                return Tensor.FromArray(new[] { maxVal });
            }

            return ReduceAxisDouble(t, axis, keepDims, double.NegativeInfinity, Math.Max);
        }

        public static Tensor<double> Min(Tensor<double> t, int axis = -1, bool keepDims = false)
        {
            if (axis == -1)
            {
                double minVal = double.PositiveInfinity;
                t.ForEachElement(v => { if (v < minVal) minVal = v; });
                return Tensor.FromArray(new[] { minVal });
            }

            return ReduceAxisDouble(t, axis, keepDims, double.PositiveInfinity, Math.Min);
        }

        public static Tensor<double> Variance(Tensor<double> t, int axis = -1, bool unbiased = true)
        {
            var mean = Mean(t, axis, keepDims: true);
            var diff = t - mean;
            var sqDiff = diff * diff;

            int count = axis == -1 ? t.Length : t.Shape[axis < 0 ? axis + t.Rank : axis];
            int denom = unbiased ? Math.Max(1, count - 1) : count;

            return Sum(sqDiff, axis, keepDims: false) / (double)denom;
        }

        public static Tensor<double> Std(Tensor<double> t, int axis = -1, bool unbiased = true)
        {
            return Sqrt(Variance(t, axis, unbiased));
        }

        public static Tensor<double> Softmax(Tensor<double> t, int axis = -1)
        {
            var maxVal = Max(t, axis, keepDims: true);
            var exp = Exp(t - maxVal);
            var sumExp = Sum(exp, axis, keepDims: true);
            return exp / sumExp;
        }

        private static Tensor<double> ReduceAxisDouble(
            Tensor<double> t,
            int axis,
            bool keepDims,
            double initialValue,
            Func<double, double, double> reducer)
        {
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, keepDims);
            var result = new Tensor<double>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = keepDims ? (int[])coords.Clone() : ExpandCoordsForAxis(coords, ax, 0);
                double acc = initialValue;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    acc = reducer(acc, t[srcCoords]);
                }

                result[coords] = acc;
            });

            return result;
        }

        #endregion
    }
}
