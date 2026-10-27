using System;
using System.Collections.Generic;
using ZeroPrimitives;

namespace ZeroData.Core
{
    /// <summary>
    /// Contiguous in-memory column storing elements of type T without per-row object allocations.
    /// Supports Apache Arrow-compliant null validity bitmasks and zero-boxing aggregations.
    /// </summary>
    /// <typeparam name="T">Element type (e.g. double, float, int, long, decimal, DateTime, string, bool, Guid).</typeparam>
    public class DataColumn<T> : IDataColumn
    {
        private readonly string _name;
        private T[] _data;
        private int _length;
        private byte[]? _nullBitmap;
        private bool _hasNulls;

        public string Name => _name;
        public Type DataType => typeof(T);
        public int Length => _length;
        public bool HasNulls => _hasNulls;
        public ReadOnlySpan<byte> NullBitmap => _nullBitmap != null ? new ReadOnlySpan<byte>(_nullBitmap, 0, (_length + 7) >> 3) : ReadOnlySpan<byte>.Empty;
        internal T[] RawData => _data;
        internal byte[]? RawNullBitmap => _nullBitmap;

        public DataColumn(string name, int initialCapacity = 0)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _data = new T[initialCapacity];
            _length = initialCapacity;
            _hasNulls = false;
        }

        public DataColumn(string name, T[] data)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _length = data.Length;
            _hasNulls = false;
        }

        public DataColumn(string name, IEnumerable<T> values)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            var list = new List<T>(values);
            _data = list.ToArray();
            _length = _data.Length;
            _hasNulls = false;
        }

        internal DataColumn(string name, T[] data, byte[]? nullBitmap, bool hasNulls)
        {
            _name = name;
            _data = data;
            _length = data.Length;
            _nullBitmap = nullBitmap;
            _hasNulls = hasNulls;
        }

        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _data[index];
            }
        }

        public Span<T> AsSpan() => new Span<T>(_data, 0, _length);

        public ReadOnlySpan<T> AsReadOnlySpan() => new ReadOnlySpan<T>(_data, 0, _length);

        public bool IsNull(int index)
        {
            if (!_hasNulls || _nullBitmap == null) return false;
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            return (_nullBitmap[index >> 3] & (1 << (index & 7))) == 0;
        }

        public void SetNull(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureNullBitmap();
            _nullBitmap![index >> 3] &= (byte)~(1 << (index & 7));
            _data[index] = default!;
            _hasNulls = true;
        }

        public void SetValid(int index, T value)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            _data[index] = value;
            if (_nullBitmap != null)
            {
                _nullBitmap[index >> 3] |= (byte)(1 << (index & 7));
            }
        }

        private void EnsureNullBitmap()
        {
            if (_nullBitmap == null)
            {
                int bytes = (_data.Length + 7) >> 3;
                _nullBitmap = new byte[bytes];
                for (int i = 0; i < bytes; i++) _nullBitmap[i] = 0xFF; // 1 = valid
            }
        }

        public object? GetValue(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (IsNull(index)) return null;
            return _data[index];
        }

        public void SetValue(int index, object? value)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (value == null || value == DBNull.Value)
            {
                SetNull(index);
            }
            else
            {
                SetValid(index, FastConvert.To<T>(value));
            }
        }

        public IDataColumn Slice(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "Invalid slice range.");
            }

            var sliceData = new T[length];
            Array.Copy(_data, start, sliceData, 0, length);
            var sliceCol = new DataColumn<T>(_name, sliceData);

            if (_hasNulls)
            {
                for (int i = 0; i < length; i++)
                {
                    if (IsNull(start + i))
                    {
                        sliceCol.SetNull(i);
                    }
                }
            }

            return sliceCol;
        }

        public IDataColumn Clone()
        {
            var cloneData = new T[_length];
            Array.Copy(_data, cloneData, _length);

            byte[]? cloneBitmap = null;
            if (_nullBitmap != null)
            {
                cloneBitmap = new byte[_nullBitmap.Length];
                Buffer.BlockCopy(_nullBitmap, 0, cloneBitmap, 0, _nullBitmap.Length);
            }

            return new DataColumn<T>(_name, cloneData, cloneBitmap, _hasNulls);
        }

        public IDataColumn Filter(int[] indices)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            var filtered = new T[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                filtered[i] = _data[indices[i]];
            }

            var filterCol = new DataColumn<T>(_name, filtered);
            if (_hasNulls)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    if (IsNull(indices[i]))
                    {
                        filterCol.SetNull(i);
                    }
                }
            }

            return filterCol;
        }

        public T[] ToArray()
        {
            var copy = new T[_length];
            Array.Copy(_data, copy, _length);
            return copy;
        }

        #region Numeric Aggregations (Double)

        public double SumAsDouble()
        {
            double sum = 0.0;
            if (typeof(T) == typeof(double))
            {
                var arr = (double[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(float))
            {
                var arr = (float[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += (double)arr[i];
                }
            }
            else
            {
                throw new NotSupportedException($"Sum is not supported for type {typeof(T)}.");
            }

            return sum;
        }

        public double MeanAsDouble()
        {
            int validCount = ValidCount();
            if (validCount == 0) return 0.0;
            return SumAsDouble() / validCount;
        }

        public double MinAsDouble()
        {
            if (_length == 0) return double.NaN;
            double min = double.PositiveInfinity;
            bool found = false;

            if (typeof(T) == typeof(double))
            {
                var arr = (double[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(float))
            {
                var arr = (float[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    int v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    long v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = (double)arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else
            {
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double val = FastConvert.ToDouble(_data[i]);
                    if (val < min) min = val;
                    found = true;
                }
            }

            return found ? min : double.NaN;
        }

        public double MaxAsDouble()
        {
            if (_length == 0) return double.NaN;
            double max = double.NegativeInfinity;
            bool found = false;

            if (typeof(T) == typeof(double))
            {
                var arr = (double[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(float))
            {
                var arr = (float[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    int v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    long v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double v = (double)arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else
            {
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    double val = FastConvert.ToDouble(_data[i]);
                    if (val > max) max = val;
                    found = true;
                }
            }

            return found ? max : double.NaN;
        }

        private int ValidCount()
        {
            if (!_hasNulls) return _length;
            int count = 0;
            for (int i = 0; i < _length; i++)
            {
                if (!IsNull(i)) count++;
            }
            return count;
        }

        #endregion

        #region Numeric Aggregations (Decimal for ERP & Finance)

        public decimal SumAsDecimal()
        {
            decimal sum = 0m;
            if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += arr[i];
                }
            }
            else if (typeof(T) == typeof(double))
            {
                var arr = (double[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += (decimal)arr[i];
                }
            }
            else if (typeof(T) == typeof(float))
            {
                var arr = (float[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    sum += (decimal)arr[i];
                }
            }
            else
            {
                throw new NotSupportedException($"SumAsDecimal is not supported for type {typeof(T)}.");
            }

            return sum;
        }

        public decimal AverageAsDecimal()
        {
            int count = ValidCount();
            if (count == 0) return 0m;
            return SumAsDecimal() / count;
        }

        public decimal MinAsDecimal()
        {
            if (_length == 0) return 0m;
            decimal min = decimal.MaxValue;
            bool found = false;

            if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    decimal v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    int v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    long v = arr[i];
                    if (v < min) min = v;
                    found = true;
                }
            }
            else
            {
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    decimal v = FastConvert.ToDecimal(_data[i]);
                    if (v < min) min = v;
                    found = true;
                }
            }

            return found ? min : 0m;
        }

        public decimal MaxAsDecimal()
        {
            if (_length == 0) return 0m;
            decimal max = decimal.MinValue;
            bool found = false;

            if (typeof(T) == typeof(decimal))
            {
                var arr = (decimal[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    decimal v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                var arr = (int[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    int v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                var arr = (long[])(object)_data;
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    long v = arr[i];
                    if (v > max) max = v;
                    found = true;
                }
            }
            else
            {
                for (int i = 0; i < _length; i++)
                {
                    if (_hasNulls && IsNull(i)) continue;
                    decimal v = FastConvert.ToDecimal(_data[i]);
                    if (v > max) max = v;
                    found = true;
                }
            }

            return found ? max : 0m;
        }

        #endregion
    }
}
