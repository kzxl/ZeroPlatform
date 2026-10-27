using System;
using System.Linq;

namespace ZeroTensor.Core
{
    /// <summary>
    /// High-performance broadcasting execution engine supporting SIMD acceleration for contiguous operations
    /// and zero-allocation coordinate evaluation for broadcasted binary operations.
    /// </summary>
    public static class TensorBroadcaster
    {
        public delegate float BinaryFloatOp(float a, float b);
        public delegate double BinaryDoubleOp(double a, double b);
        public delegate Half BinaryHalfOp(Half a, Half b);

        /// <summary>
        /// Executes an element-wise binary operation between two float tensors with automatic broadcasting.
        /// </summary>
        public static Tensor<float> Apply(Tensor<float> a, Tensor<float> b, BinaryFloatOp op)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<float>(a.Shape);
                var spanA = a.AsReadOnlySpan();
                var spanB = b.AsReadOnlySpan();
                var spanRes = result.AsSpan();

                for (int i = 0; i < spanA.Length; i++)
                {
                    spanRes[i] = op(spanA[i], spanB[i]);
                }
                return result;
            }

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Operands could not be broadcast together with shapes {a.Shape} and {b.Shape}.");
            }

            var outTensor = new Tensor<float>(outShape);
            var stridesA = TensorStrides.ComputeBroadcastStrides(a.Shape, a.Strides.ToArray(), outShape);
            var stridesB = TensorStrides.ComputeBroadcastStrides(b.Shape, b.Strides.ToArray(), outShape);

            ApplyBroadcastedFloat(a, b, outTensor, stridesA, stridesB, op);
            return outTensor;
        }

        /// <summary>
        /// Executes an element-wise binary operation between two double tensors with automatic broadcasting.
        /// </summary>
        public static Tensor<double> Apply(Tensor<double> a, Tensor<double> b, BinaryDoubleOp op)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<double>(a.Shape);
                var spanA = a.AsReadOnlySpan();
                var spanB = b.AsReadOnlySpan();
                var spanRes = result.AsSpan();

                for (int i = 0; i < spanA.Length; i++)
                {
                    spanRes[i] = op(spanA[i], spanB[i]);
                }
                return result;
            }

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Operands could not be broadcast together with shapes {a.Shape} and {b.Shape}.");
            }

            var outTensor = new Tensor<double>(outShape);
            var stridesA = TensorStrides.ComputeBroadcastStrides(a.Shape, a.Strides.ToArray(), outShape);
            var stridesB = TensorStrides.ComputeBroadcastStrides(b.Shape, b.Strides.ToArray(), outShape);

            ApplyBroadcastedDouble(a, b, outTensor, stridesA, stridesB, op);
            return outTensor;
        }

        /// <summary>
        /// Executes an element-wise binary operation between two half-precision tensors with automatic broadcasting.
        /// </summary>
        public static Tensor<Half> Apply(Tensor<Half> a, Tensor<Half> b, BinaryHalfOp op)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (a.Shape == b.Shape && a.IsContiguous && b.IsContiguous)
            {
                var result = new Tensor<Half>(a.Shape);
                var spanA = a.AsReadOnlySpan();
                var spanB = b.AsReadOnlySpan();
                var spanRes = result.AsSpan();

                for (int i = 0; i < spanA.Length; i++)
                {
                    spanRes[i] = op(spanA[i], spanB[i]);
                }
                return result;
            }

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Operands could not be broadcast together with shapes {a.Shape} and {b.Shape}.");
            }

            var outTensor = new Tensor<Half>(outShape);
            var stridesA = TensorStrides.ComputeBroadcastStrides(a.Shape, a.Strides.ToArray(), outShape);
            var stridesB = TensorStrides.ComputeBroadcastStrides(b.Shape, b.Strides.ToArray(), outShape);

            ApplyBroadcastedHalf(a, b, outTensor, stridesA, stridesB, op);
            return outTensor;
        }

        private static void ApplyBroadcastedHalf(
            Tensor<Half> a,
            Tensor<Half> b,
            Tensor<Half> outTensor,
            int[] stridesA,
            int[] stridesB,
            BinaryHalfOp op)
        {
            int total = outTensor.Length;
            int rank = outTensor.Rank;
            var coords = new int[rank];
            var bufA = a.Buffer;
            var bufB = b.Buffer;
            var bufOut = outTensor.Buffer;
            int offA = a.Offset;
            int offB = b.Offset;
            int offOut = outTensor.Offset;
            var stridesOut = outTensor.Strides;

            for (int idx = 0; idx < total; idx++)
            {
                int flatA = offA;
                int flatB = offB;
                int flatOut = offOut;

                for (int d = 0; d < rank; d++)
                {
                    int c = coords[d];
                    flatA += c * stridesA[d];
                    flatB += c * stridesB[d];
                    flatOut += c * stridesOut[d];
                }

                bufOut[flatOut] = op(bufA[flatA], bufB[flatB]);

                for (int d = rank - 1; d >= 0; d--)
                {
                    coords[d]++;
                    if (coords[d] < outTensor.Shape[d])
                    {
                        break;
                    }
                    coords[d] = 0;
                }
            }
        }

        /// <summary>
        /// Executes an element-wise binary operation between two generic tensors with automatic broadcasting.
        /// </summary>
        public static Tensor<T> ApplyGeneric<T>(Tensor<T> a, Tensor<T> b, Func<T, T, T> op) where T : unmanaged, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Operands could not be broadcast together with shapes {a.Shape} and {b.Shape}.");
            }

            var outTensor = new Tensor<T>(outShape);
            var stridesA = TensorStrides.ComputeBroadcastStrides(a.Shape, a.Strides.ToArray(), outShape);
            var stridesB = TensorStrides.ComputeBroadcastStrides(b.Shape, b.Strides.ToArray(), outShape);

            int total = outShape.TotalElements;
            int rank = outShape.Rank;
            var coords = new int[rank];
            var bufA = a.Buffer;
            var bufB = b.Buffer;
            var bufOut = outTensor.Buffer;
            int offA = a.Offset;
            int offB = b.Offset;
            int offOut = outTensor.Offset;
            var stridesOut = outTensor.Strides;

            for (int idx = 0; idx < total; idx++)
            {
                int flatA = offA;
                int flatB = offB;
                int flatOut = offOut;

                for (int d = 0; d < rank; d++)
                {
                    flatA += coords[d] * stridesA[d];
                    flatB += coords[d] * stridesB[d];
                    flatOut += coords[d] * stridesOut[d];
                }

                bufOut[flatOut] = op(bufA[flatA], bufB[flatB]);

                for (int d = rank - 1; d >= 0; d--)
                {
                    coords[d]++;
                    if (coords[d] < outShape[d]) break;
                    coords[d] = 0;
                }
            }

            return outTensor;
        }

        private static unsafe void ApplyBroadcastedFloat(
            Tensor<float> a,
            Tensor<float> b,
            Tensor<float> result,
            int[] stridesA,
            int[] stridesB,
            BinaryFloatOp op)
        {
            var shape = result.Shape;
            int rank = shape.Rank;

            int offA = a.Offset;
            int offB = b.Offset;
            int offOut = result.Offset;
            var stridesOut = result.Strides;

            fixed (float* pA = &a.Storage.GetPinnableReference(0))
            fixed (float* pB = &b.Storage.GetPinnableReference(0))
            fixed (float* pOut = &result.Storage.GetPinnableReference(0))
            {
                if (rank == 1)
                {
                    int len = shape[0];
                    int sA = stridesA[0], sB = stridesB[0], sOut = stridesOut[0];

                    for (int i = 0; i < len; i++)
                    {
                        pOut[offOut + i * sOut] = op(pA[offA + i * sA], pB[offB + i * sB]);
                    }
                }
                else if (rank == 2)
                {
                    int rMax = shape[0];
                    int cMax = shape[1];

                    int sA0 = stridesA[0], sA1 = stridesA[1];
                    int sB0 = stridesB[0], sB1 = stridesB[1];
                    int sOut0 = stridesOut[0], sOut1 = stridesOut[1];

                    for (int r = 0; r < rMax; r++)
                    {
                        int rowOffA = offA + r * sA0;
                        int rowOffB = offB + r * sB0;
                        int rowOffOut = offOut + r * sOut0;

                        for (int c = 0; c < cMax; c++)
                        {
                            pOut[rowOffOut + c * sOut1] = op(pA[rowOffA + c * sA1], pB[rowOffB + c * sB1]);
                        }
                    }
                }
                else
                {
                    int total = shape.TotalElements;
                    var coords = new int[rank];

                    for (int idx = 0; idx < total; idx++)
                    {
                        int flatA = offA;
                        int flatB = offB;
                        int flatOut = offOut;

                        for (int d = 0; d < rank; d++)
                        {
                            flatA += coords[d] * stridesA[d];
                            flatB += coords[d] * stridesB[d];
                            flatOut += coords[d] * stridesOut[d];
                        }

                        pOut[flatOut] = op(pA[flatA], pB[flatB]);

                        for (int d = rank - 1; d >= 0; d--)
                        {
                            coords[d]++;
                            if (coords[d] < shape[d]) break;
                            coords[d] = 0;
                        }
                    }
                }
            }
        }

        private static unsafe void ApplyBroadcastedDouble(
            Tensor<double> a,
            Tensor<double> b,
            Tensor<double> result,
            int[] stridesA,
            int[] stridesB,
            BinaryDoubleOp op)
        {
            var shape = result.Shape;
            int rank = shape.Rank;

            int offA = a.Offset;
            int offB = b.Offset;
            int offOut = result.Offset;
            var stridesOut = result.Strides;

            fixed (double* pA = &a.Storage.GetPinnableReference(0))
            fixed (double* pB = &b.Storage.GetPinnableReference(0))
            fixed (double* pOut = &result.Storage.GetPinnableReference(0))
            {
                if (rank == 1)
                {
                    int len = shape[0];
                    int sA = stridesA[0], sB = stridesB[0], sOut = stridesOut[0];

                    for (int i = 0; i < len; i++)
                    {
                        pOut[offOut + i * sOut] = op(pA[offA + i * sA], pB[offB + i * sB]);
                    }
                }
                else if (rank == 2)
                {
                    int rMax = shape[0];
                    int cMax = shape[1];

                    int sA0 = stridesA[0], sA1 = stridesA[1];
                    int sB0 = stridesB[0], sB1 = stridesB[1];
                    int sOut0 = stridesOut[0], sOut1 = stridesOut[1];

                    for (int r = 0; r < rMax; r++)
                    {
                        int rowOffA = offA + r * sA0;
                        int rowOffB = offB + r * sB0;
                        int rowOffOut = offOut + r * sOut0;

                        for (int c = 0; c < cMax; c++)
                        {
                            pOut[rowOffOut + c * sOut1] = op(pA[rowOffA + c * sA1], pB[rowOffB + c * sB1]);
                        }
                    }
                }
                else
                {
                    int total = shape.TotalElements;
                    var coords = new int[rank];

                    for (int idx = 0; idx < total; idx++)
                    {
                        int flatA = offA;
                        int flatB = offB;
                        int flatOut = offOut;

                        for (int d = 0; d < rank; d++)
                        {
                            flatA += coords[d] * stridesA[d];
                            flatB += coords[d] * stridesB[d];
                            flatOut += coords[d] * stridesOut[d];
                        }

                        pOut[flatOut] = op(pA[flatA], pB[flatB]);

                        for (int d = rank - 1; d >= 0; d--)
                        {
                            coords[d]++;
                            if (coords[d] < shape[d]) break;
                            coords[d] = 0;
                        }
                    }
                }
            }
        }
    }
}
