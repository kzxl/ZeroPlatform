using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZeroTensor.Core
{
    public static partial class TensorOps
    {
        #region Float In-Place Arithmetic

        /// <summary>
        /// Adds tensor b into tensor a in-place: a += b.
        /// </summary>
        public static unsafe Tensor<float> Add_(this Tensor<float> a, Tensor<float> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va + vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] += pB[i];
                    }
                }
                return a;
            }

            // General broadcasting in-place
            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] += bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Subtracts tensor b from tensor a in-place: a -= b.
        /// </summary>
        public static unsafe Tensor<float> Subtract_(this Tensor<float> a, Tensor<float> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va - vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] -= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] -= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Multiplies tensor a by tensor b element-wise in-place: a *= b.
        /// </summary>
        public static unsafe Tensor<float> Multiply_(this Tensor<float> a, Tensor<float> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va * vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] *= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] *= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Divides tensor a by tensor b element-wise in-place: a /= b.
        /// </summary>
        public static unsafe Tensor<float> Divide_(this Tensor<float> a, Tensor<float> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va / vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] /= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] /= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Adds a scalar to tensor a in-place: a += scalar.
        /// </summary>
        public static unsafe Tensor<float> AddScalar_(this Tensor<float> a, float scalar)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var vScalar = new Vector<float>(scalar);
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, v + vScalar);
                    }
                    for (; i < len; i++) pA[i] += scalar;
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] += scalar);
            return a;
        }

        /// <summary>
        /// Multiplies tensor a by a scalar in-place: a *= scalar.
        /// </summary>
        public static unsafe Tensor<float> MultiplyScalar_(this Tensor<float> a, float scalar)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var vScalar = new Vector<float>(scalar);
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, v * vScalar);
                    }
                    for (; i < len; i++) pA[i] *= scalar;
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] *= scalar);
            return a;
        }

        /// <summary>
        /// Applies ReLU activation in-place: a = max(0, a).
        /// </summary>
        public static unsafe Tensor<float> Relu_(this Tensor<float> a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var zeroVec = Vector<float>.Zero;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, Vector.Max(v, zeroVec));
                    }
                    for (; i < len; i++) pA[i] = Math.Max(0f, pA[i]);
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] = Math.Max(0f, a[coords]));
            return a;
        }

        /// <summary>
        /// Clamps all elements in-place: a = clamp(a, min, max).
        /// </summary>
        public static unsafe Tensor<float> Clamp_(this Tensor<float> a, float min, float max)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    for (int i = 0; i < len; i++)
                    {
                        float val = pA[i];
                        pA[i] = val < min ? min : (val > max ? max : val);
                    }
                }
                return a;
            }

            a.ForEachCoordinate(coords =>
            {
                float val = a[coords];
                a[coords] = val < min ? min : (val > max ? max : val);
            });
            return a;
        }

        #endregion

        #region Float Destination Buffer Operations

        /// <summary>
        /// Computes destination = a + b without allocating a new tensor buffer.
        /// </summary>
        public static unsafe void Add(Tensor<float> a, Tensor<float> b, Tensor<float> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (float* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va + vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] + pB[i];
                    }
                }
                return;
            }

            var res = a + b;
            res.CopyTo(destination);
        }

        /// <summary>
        /// Computes destination = a - b without allocating a new tensor buffer.
        /// </summary>
        public static unsafe void Subtract(Tensor<float> a, Tensor<float> b, Tensor<float> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (float* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va - vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] - pB[i];
                    }
                }
                return;
            }

            var res = a - b;
            res.CopyTo(destination);
        }

        /// <summary>
        /// Computes destination = a * b without allocating a new tensor buffer.
        /// </summary>
        public static unsafe void Multiply(Tensor<float> a, Tensor<float> b, Tensor<float> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<float>.Count;
                fixed (float* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (float* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (float* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<float>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<float>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va * vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] * pB[i];
                    }
                }
                return;
            }

            var res = a * b;
            res.CopyTo(destination);
        }

        #endregion

        #region Double In-Place Arithmetic

        /// <summary>
        /// Adds tensor b into tensor a in-place: a += b (double precision).
        /// </summary>
        public static unsafe Tensor<double> Add_(this Tensor<double> a, Tensor<double> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va + vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] += pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] += bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Subtracts tensor b from tensor a in-place: a -= b (double precision).
        /// </summary>
        public static unsafe Tensor<double> Subtract_(this Tensor<double> a, Tensor<double> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va - vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] -= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] -= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Multiplies tensor a by tensor b in-place: a *= b (double precision).
        /// </summary>
        public static unsafe Tensor<double> Multiply_(this Tensor<double> a, Tensor<double> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va * vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] *= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] *= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Divides tensor a by tensor b element-wise in-place: a /= b (double precision).
        /// </summary>
        public static unsafe Tensor<double> Divide_(this Tensor<double> a, Tensor<double> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pA + i, va / vb);
                    }
                    for (; i < len; i++)
                    {
                        pA[i] /= pB[i];
                    }
                }
                return a;
            }

            var bBroad = b.BroadcastTo(a.Shape);
            bBroad.ForEachCoordinate(coords => a[coords] /= bBroad[coords]);
            return a;
        }

        /// <summary>
        /// Adds a scalar to tensor a in-place: a += scalar (double precision).
        /// </summary>
        public static unsafe Tensor<double> AddScalar_(this Tensor<double> a, double scalar)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var vScalar = new Vector<double>(scalar);
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, v + vScalar);
                    }
                    for (; i < len; i++) pA[i] += scalar;
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] += scalar);
            return a;
        }

        /// <summary>
        /// Multiplies tensor a by a scalar in-place: a *= scalar (double precision).
        /// </summary>
        public static unsafe Tensor<double> MultiplyScalar_(this Tensor<double> a, double scalar)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var vScalar = new Vector<double>(scalar);
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, v * vScalar);
                    }
                    for (; i < len; i++) pA[i] *= scalar;
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] *= scalar);
            return a;
        }

        /// <summary>
        /// Applies ReLU activation in-place: a = max(0, a) (double precision).
        /// </summary>
        public static unsafe Tensor<double> Relu_(this Tensor<double> a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                var zeroVec = Vector<double>.Zero;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var v = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        Unsafe.WriteUnaligned(pA + i, Vector.Max(v, zeroVec));
                    }
                    for (; i < len; i++) pA[i] = Math.Max(0.0, pA[i]);
                }
                return a;
            }

            a.ForEachCoordinate(coords => a[coords] = Math.Max(0.0, a[coords]));
            return a;
        }

        /// <summary>
        /// Clamps all elements in-place: a = clamp(a, min, max) (double precision).
        /// </summary>
        public static unsafe Tensor<double> Clamp_(this Tensor<double> a, double min, double max)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.IsContiguous)
            {
                int len = a.Length;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                {
                    for (int i = 0; i < len; i++)
                    {
                        double val = pA[i];
                        pA[i] = val < min ? min : (val > max ? max : val);
                    }
                }
                return a;
            }

            a.ForEachCoordinate(coords =>
            {
                double val = a[coords];
                a[coords] = val < min ? min : (val > max ? max : val);
            });
            return a;
        }

        /// <summary>
        /// Computes destination = a + b without allocating a new tensor buffer (double precision).
        /// </summary>
        public static unsafe void Add(Tensor<double> a, Tensor<double> b, Tensor<double> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va + vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] + pB[i];
                    }
                }
                return;
            }

            var res = a + b;
            res.CopyTo(destination);
        }

        /// <summary>
        /// Computes destination = a - b without allocating a new tensor buffer (double precision).
        /// </summary>
        public static unsafe void Subtract(Tensor<double> a, Tensor<double> b, Tensor<double> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va - vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] - pB[i];
                    }
                }
                return;
            }

            var res = a - b;
            res.CopyTo(destination);
        }

        /// <summary>
        /// Computes destination = a * b without allocating a new tensor buffer (double precision).
        /// </summary>
        public static unsafe void Multiply(Tensor<double> a, Tensor<double> b, Tensor<double> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (a.Shape == b.Shape && a.Shape == destination.Shape && a.IsContiguous && b.IsContiguous && destination.IsContiguous)
            {
                int len = a.Length;
                int vecSize = Vector<double>.Count;
                fixed (double* pA = &a.Storage.GetPinnableReference(a.Offset))
                fixed (double* pB = &b.Storage.GetPinnableReference(b.Offset))
                fixed (double* pDst = &destination.Storage.GetPinnableReference(destination.Offset))
                {
                    int i = 0;
                    for (; i <= len - vecSize; i += vecSize)
                    {
                        var va = Unsafe.ReadUnaligned<Vector<double>>(pA + i);
                        var vb = Unsafe.ReadUnaligned<Vector<double>>(pB + i);
                        Unsafe.WriteUnaligned(pDst + i, va * vb);
                    }
                    for (; i < len; i++)
                    {
                        pDst[i] = pA[i] * pB[i];
                    }
                }
                return;
            }

            var res = a * b;
            res.CopyTo(destination);
        }

        #endregion
    }
}
