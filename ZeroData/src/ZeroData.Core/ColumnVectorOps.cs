using System;
using System.Numerics;

namespace ZeroData.Core
{
    /// <summary>
    /// Hardware-accelerated SIMD vector operations (AVX2 / AVX-512) for DataColumns.
    /// Provides zero-copy contiguous columnar arithmetic and aggregations with Apache Arrow null validity propagation.
    /// </summary>
    public static class ColumnVectorOps
    {
        #region Null Validity Helpers

        private static (byte[]? bitmap, bool hasNulls) CombineNullBitmaps<T1, T2>(DataColumn<T1> left, DataColumn<T2> right, int length)
        {
            bool leftNulls = left.HasNulls;
            bool rightNulls = right.HasNulls;
            if (!leftNulls && !rightNulls) return (null, false);

            int numBytes = (length + 7) >> 3;
            byte[] result = new byte[numBytes];

            if (leftNulls && rightNulls)
            {
                var lMap = left.RawNullBitmap!;
                var rMap = right.RawNullBitmap!;
                for (int i = 0; i < numBytes; i++)
                {
                    result[i] = (byte)(lMap[i] & rMap[i]);
                }
            }
            else if (leftNulls)
            {
                Array.Copy(left.RawNullBitmap!, result, numBytes);
            }
            else
            {
                Array.Copy(right.RawNullBitmap!, result, numBytes);
            }

            return (result, true);
        }

        private static (byte[]? bitmap, bool hasNulls) CloneNullBitmap<T>(DataColumn<T> col, int length)
        {
            if (!col.HasNulls || col.RawNullBitmap == null) return (null, false);
            int numBytes = (length + 7) >> 3;
            byte[] result = new byte[numBytes];
            Array.Copy(col.RawNullBitmap, result, numBytes);
            return (result, true);
        }

        #endregion

        #region Double Vector Operations

        /// <summary>
        /// Computes element-wise addition of two double columns using SIMD.
        /// </summary>
        public static DataColumn<double> Add(this DataColumn<double> left, DataColumn<double> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<double>(leftData, i);
                    var v2 = new Vector<double>(rightData, i);
                    (v1 + v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] + rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<double>(resultName ?? left.Name + "_add", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise subtraction of two double columns using SIMD.
        /// </summary>
        public static DataColumn<double> Subtract(this DataColumn<double> left, DataColumn<double> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<double>(leftData, i);
                    var v2 = new Vector<double>(rightData, i);
                    (v1 - v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] - rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<double>(resultName ?? left.Name + "_sub", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise multiplication of two double columns using SIMD.
        /// </summary>
        public static DataColumn<double> Multiply(this DataColumn<double> left, DataColumn<double> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<double>(leftData, i);
                    var v2 = new Vector<double>(rightData, i);
                    (v1 * v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] * rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<double>(resultName ?? left.Name + "_mul", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise division of two double columns using SIMD.
        /// </summary>
        public static DataColumn<double> Divide(this DataColumn<double> left, DataColumn<double> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<double>(leftData, i);
                    var v2 = new Vector<double>(rightData, i);
                    (v1 / v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] / rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<double>(resultName ?? left.Name + "_div", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Multiplies a double column by a scalar using SIMD broadcast.
        /// </summary>
        public static DataColumn<double> Multiply(this DataColumn<double> col, double scalar, string? resultName = null)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            var srcData = col.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vScalar = new Vector<double>(scalar);

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v = new Vector<double>(srcData, i);
                    (v * vScalar).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = srcData[i] * scalar;
            }

            var (bitmap, hasNulls) = CloneNullBitmap(col, count);
            return new DataColumn<double>(resultName ?? col.Name + "_scaled", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Adds a scalar to a double column using SIMD broadcast.
        /// </summary>
        public static DataColumn<double> Add(this DataColumn<double> col, double scalar, string? resultName = null)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            var srcData = col.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vScalar = new Vector<double>(scalar);

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v = new Vector<double>(srcData, i);
                    (v + vScalar).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = srcData[i] + scalar;
            }

            var (bitmap, hasNulls) = CloneNullBitmap(col, count);
            return new DataColumn<double>(resultName ?? col.Name + "_offset", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes absolute values of double column elements using SIMD.
        /// </summary>
        public static DataColumn<double> SimdAbs(this DataColumn<double> col, string? resultName = null)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            var srcData = col.RawData;
            var res = new double[count];

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v = new Vector<double>(srcData, i);
                    Vector.Abs(v).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = Math.Abs(srcData[i]);
            }

            var (bitmap, hasNulls) = CloneNullBitmap(col, count);
            return new DataColumn<double>(resultName ?? col.Name + "_abs", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes the sum of double column elements using SIMD vector registers.
        /// Automatically skips null rows if null validity bitmask is present.
        /// </summary>
        public static double SimdSum(this DataColumn<double> col)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            if (count == 0) return 0.0;

            var srcData = col.RawData;

            if (col.HasNulls)
            {
                double sum = 0.0;
                for (int idx = 0; idx < count; idx++)
                {
                    if (!col.IsNull(idx)) sum += srcData[idx];
                }
                return sum;
            }

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vAcc = Vector<double>.Zero;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    vAcc += new Vector<double>(srcData, i);
                }
            }

            double total = 0.0;
            for (int v = 0; v < vecSize; v++)
            {
                total += vAcc[v];
            }

            for (; i < count; i++)
            {
                total += srcData[i];
            }

            return total;
        }

        /// <summary>
        /// Computes the dot product of two double columns using SIMD fused multiply-add accumulation.
        /// </summary>
        public static double SimdDot(this DataColumn<double> left, DataColumn<double> right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");
            if (count == 0) return 0.0;

            var leftData = left.RawData;
            var rightData = right.RawData;

            if (left.HasNulls || right.HasNulls)
            {
                double sum = 0.0;
                for (int idx = 0; idx < count; idx++)
                {
                    if (!left.IsNull(idx) && !right.IsNull(idx))
                    {
                        sum += leftData[idx] * rightData[idx];
                    }
                }
                return sum;
            }

            int vecSize = Vector<double>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vAcc = Vector<double>.Zero;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<double>(leftData, i);
                    var v2 = new Vector<double>(rightData, i);
                    vAcc += (v1 * v2);
                }
            }

            double total = 0.0;
            for (int v = 0; v < vecSize; v++)
            {
                total += vAcc[v];
            }

            for (; i < count; i++)
            {
                total += leftData[i] * rightData[i];
            }

            return total;
        }

        #endregion

        #region Float Vector Operations

        /// <summary>
        /// Computes element-wise addition of two float columns using SIMD.
        /// </summary>
        public static DataColumn<float> Add(this DataColumn<float> left, DataColumn<float> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new float[count];

            int vecSize = Vector<float>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<float>(leftData, i);
                    var v2 = new Vector<float>(rightData, i);
                    (v1 + v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] + rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<float>(resultName ?? left.Name + "_add", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise subtraction of two float columns using SIMD.
        /// </summary>
        public static DataColumn<float> Subtract(this DataColumn<float> left, DataColumn<float> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new float[count];

            int vecSize = Vector<float>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<float>(leftData, i);
                    var v2 = new Vector<float>(rightData, i);
                    (v1 - v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] - rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<float>(resultName ?? left.Name + "_sub", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise multiplication of two float columns using SIMD.
        /// </summary>
        public static DataColumn<float> Multiply(this DataColumn<float> left, DataColumn<float> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new float[count];

            int vecSize = Vector<float>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<float>(leftData, i);
                    var v2 = new Vector<float>(rightData, i);
                    (v1 * v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] * rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<float>(resultName ?? left.Name + "_mul", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Multiplies a float column by a scalar using SIMD broadcast.
        /// </summary>
        public static DataColumn<float> Multiply(this DataColumn<float> col, float scalar, string? resultName = null)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            var srcData = col.RawData;
            var res = new float[count];

            int vecSize = Vector<float>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vScalar = new Vector<float>(scalar);

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v = new Vector<float>(srcData, i);
                    (v * vScalar).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = srcData[i] * scalar;
            }

            var (bitmap, hasNulls) = CloneNullBitmap(col, count);
            return new DataColumn<float>(resultName ?? col.Name + "_scaled", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes the sum of float column elements using SIMD vector registers.
        /// </summary>
        public static float SimdSum(this DataColumn<float> col)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            if (count == 0) return 0f;

            var srcData = col.RawData;

            if (col.HasNulls)
            {
                float sum = 0f;
                for (int idx = 0; idx < count; idx++)
                {
                    if (!col.IsNull(idx)) sum += srcData[idx];
                }
                return sum;
            }

            int vecSize = Vector<float>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vAcc = Vector<float>.Zero;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    vAcc += new Vector<float>(srcData, i);
                }
            }

            float total = 0f;
            for (int v = 0; v < vecSize; v++)
            {
                total += vAcc[v];
            }

            for (; i < count; i++)
            {
                total += srcData[i];
            }

            return total;
        }

        #endregion

        #region Int32 Vector Operations

        /// <summary>
        /// Computes element-wise addition of two int columns using SIMD.
        /// </summary>
        public static DataColumn<int> Add(this DataColumn<int> left, DataColumn<int> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new int[count];

            int vecSize = Vector<int>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<int>(leftData, i);
                    var v2 = new Vector<int>(rightData, i);
                    (v1 + v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] + rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<int>(resultName ?? left.Name + "_add", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise subtraction of two int columns using SIMD.
        /// </summary>
        public static DataColumn<int> Subtract(this DataColumn<int> left, DataColumn<int> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new int[count];

            int vecSize = Vector<int>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<int>(leftData, i);
                    var v2 = new Vector<int>(rightData, i);
                    (v1 - v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] - rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<int>(resultName ?? left.Name + "_sub", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes the sum of int column elements using 64-bit precision to prevent overflow.
        /// </summary>
        public static long SimdSum(this DataColumn<int> col)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            if (count == 0) return 0L;

            var srcData = col.RawData;
            long total = 0L;

            if (col.HasNulls)
            {
                for (int idx = 0; idx < count; idx++)
                {
                    if (!col.IsNull(idx)) total += srcData[idx];
                }
                return total;
            }

            int vecSize = Vector<int>.Count;
            int i = 0;
            int limit = count - vecSize;
            var vAcc = Vector<int>.Zero;

            // Batch accumulate to prevent 32-bit int overflow
            const int batchSize = 1024;
            int processedInBatch = 0;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    vAcc += new Vector<int>(srcData, i);
                    processedInBatch += vecSize;

                    if (processedInBatch >= batchSize)
                    {
                        for (int v = 0; v < vecSize; v++) total += vAcc[v];
                        vAcc = Vector<int>.Zero;
                        processedInBatch = 0;
                    }
                }
            }

            for (int v = 0; v < vecSize; v++) total += vAcc[v];

            for (; i < count; i++)
            {
                total += srcData[i];
            }

            return total;
        }

        #endregion

        #region Int64 Vector Operations

        /// <summary>
        /// Computes element-wise addition of two long columns using SIMD.
        /// </summary>
        public static DataColumn<long> Add(this DataColumn<long> left, DataColumn<long> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new long[count];

            int vecSize = Vector<long>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<long>(leftData, i);
                    var v2 = new Vector<long>(rightData, i);
                    (v1 + v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] + rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<long>(resultName ?? left.Name + "_add", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Computes element-wise subtraction of two long columns using SIMD.
        /// </summary>
        public static DataColumn<long> Subtract(this DataColumn<long> left, DataColumn<long> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new long[count];

            int vecSize = Vector<long>.Count;
            int i = 0;
            int limit = count - vecSize;

            if (Vector.IsHardwareAccelerated && count >= vecSize)
            {
                for (; i <= limit; i += vecSize)
                {
                    var v1 = new Vector<long>(leftData, i);
                    var v2 = new Vector<long>(rightData, i);
                    (v1 - v2).CopyTo(res, i);
                }
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] - rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<long>(resultName ?? left.Name + "_sub", res, bitmap, hasNulls);
        }

        #endregion

        #region Decimal Vector Operations

        /// <summary>
        /// High-speed element-wise addition of two decimal columns with null bitmask propagation.
        /// </summary>
        public static DataColumn<decimal> Add(this DataColumn<decimal> left, DataColumn<decimal> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new decimal[count];

            // 4-way loop unrolling for high-throughput memory streaming
            int i = 0;
            int unrollLimit = count - 4;
            for (; i <= unrollLimit; i += 4)
            {
                res[i] = leftData[i] + rightData[i];
                res[i + 1] = leftData[i + 1] + rightData[i + 1];
                res[i + 2] = leftData[i + 2] + rightData[i + 2];
                res[i + 3] = leftData[i + 3] + rightData[i + 3];
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] + rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<decimal>(resultName ?? left.Name + "_add", res, bitmap, hasNulls);
        }

        /// <summary>
        /// High-speed element-wise multiplication of two decimal columns with null bitmask propagation.
        /// </summary>
        public static DataColumn<decimal> Multiply(this DataColumn<decimal> left, DataColumn<decimal> right, string? resultName = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            int count = left.Length;
            if (right.Length != count) throw new ArgumentException("Columns must have identical lengths.");

            var leftData = left.RawData;
            var rightData = right.RawData;
            var res = new decimal[count];

            int i = 0;
            int unrollLimit = count - 4;
            for (; i <= unrollLimit; i += 4)
            {
                res[i] = leftData[i] * rightData[i];
                res[i + 1] = leftData[i + 1] * rightData[i + 1];
                res[i + 2] = leftData[i + 2] * rightData[i + 2];
                res[i + 3] = leftData[i + 3] * rightData[i + 3];
            }

            for (; i < count; i++)
            {
                res[i] = leftData[i] * rightData[i];
            }

            var (bitmap, hasNulls) = CombineNullBitmaps(left, right, count);
            return new DataColumn<decimal>(resultName ?? left.Name + "_mul", res, bitmap, hasNulls);
        }

        /// <summary>
        /// Multiplies a decimal column by a scalar with 4-way unrolling.
        /// </summary>
        public static DataColumn<decimal> Multiply(this DataColumn<decimal> col, decimal scalar, string? resultName = null)
        {
            if (col == null) throw new ArgumentNullException(nameof(col));
            int count = col.Length;
            var srcData = col.RawData;
            var res = new decimal[count];

            int i = 0;
            int unrollLimit = count - 4;
            for (; i <= unrollLimit; i += 4)
            {
                res[i] = srcData[i] * scalar;
                res[i + 1] = srcData[i + 1] * scalar;
                res[i + 2] = srcData[i + 2] * scalar;
                res[i + 3] = srcData[i + 3] * scalar;
            }

            for (; i < count; i++)
            {
                res[i] = srcData[i] * scalar;
            }

            var (bitmap, hasNulls) = CloneNullBitmap(col, count);
            return new DataColumn<decimal>(resultName ?? col.Name + "_scaled", res, bitmap, hasNulls);
        }

        #endregion
    }
}
