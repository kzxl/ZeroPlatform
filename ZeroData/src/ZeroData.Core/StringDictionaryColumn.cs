using System;
using System.Collections.Generic;
using ZeroPrimitives;

namespace ZeroData.Core
{
    /// <summary>
    /// Highly compressed, dictionary-encoded in-memory columnar vector for string data.
    /// Stores unique string categories in a compact table and rows as dense integer indices,
    /// eliminating up to 95% of heap memory allocations and speeding up filter operations via integer equality.
    /// </summary>
    public class StringDictionaryColumn : IDataColumn
    {
        private readonly string _name;
        private int[] _indices;
        private readonly List<string> _dictionary;
        private readonly Dictionary<string, int> _lookup;
        private int _length;
        private byte[]? _nullBitmap;
        private bool _hasNulls;

        public string Name => _name;
        public Type DataType => typeof(string);
        public int Length => _length;
        public bool HasNulls => _hasNulls;
        public int CategoryCount => _dictionary.Count;
        public IReadOnlyList<string> Categories => _dictionary;
        internal int[] RawIndices => _indices;
        internal byte[]? RawNullBitmap => _nullBitmap;

        public StringDictionaryColumn(string name, int initialCapacity = 0)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _indices = new int[initialCapacity];
            _dictionary = new List<string>();
            _lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            _length = initialCapacity;
            _hasNulls = false;
        }

        public StringDictionaryColumn(string name, IEnumerable<string?> values)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dictionary = new List<string>();
            _lookup = new Dictionary<string, int>(StringComparer.Ordinal);

            var tempIndices = new List<int>();
            _hasNulls = false;

            int rowIdx = 0;
            foreach (var val in values)
            {
                if (val == null)
                {
                    EnsureNullBitmap(rowIdx + 1);
                    _nullBitmap![rowIdx >> 3] &= (byte)~(1 << (rowIdx & 7));
                    _hasNulls = true;
                    tempIndices.Add(-1);
                }
                else
                {
                    int catIdx = GetOrAddCategory(val);
                    tempIndices.Add(catIdx);
                    if (_nullBitmap != null)
                    {
                        EnsureNullBitmap(rowIdx + 1);
                        _nullBitmap[rowIdx >> 3] |= (byte)(1 << (rowIdx & 7));
                    }
                }
                rowIdx++;
            }

            _indices = tempIndices.ToArray();
            _length = _indices.Length;
        }

        internal StringDictionaryColumn(
            string name,
            int[] indices,
            List<string> dictionary,
            Dictionary<string, int> lookup,
            byte[]? nullBitmap,
            bool hasNulls)
        {
            _name = name;
            _indices = indices;
            _dictionary = dictionary;
            _lookup = lookup;
            _length = indices.Length;
            _nullBitmap = nullBitmap;
            _hasNulls = hasNulls;
        }

        public string? this[int index]
        {
            get => GetString(index);
            set => SetString(index, value);
        }

        /// <summary>
        /// Gets the total number of unique strings (cardinality) in this dictionary.
        /// </summary>
        public int Cardinality => _dictionary.Count;

        /// <summary>
        /// Gets the immutable dictionary entry list.
        /// </summary>
        public IReadOnlyList<string> Dictionary => _dictionary;

        public string? GetString(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (_hasNulls && IsNull(index)) return null;

            int catIdx = _indices[index];
            if (catIdx < 0 || catIdx >= _dictionary.Count) return null;
            return _dictionary[catIdx];
        }

        public void SetString(int index, string? value)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (value == null)
            {
                SetNull(index);
            }
            else
            {
                int catIdx = GetOrAddCategory(value);
                _indices[index] = catIdx;
                if (_nullBitmap != null)
                {
                    _nullBitmap[index >> 3] |= (byte)(1 << (index & 7));
                }
            }
        }

        public int GetCategoryIndex(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (_hasNulls && IsNull(index)) return -1;
            return _indices[index];
        }

        public object? GetValue(int index) => GetString(index);

        public void SetValue(int index, object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                SetNull(index);
            }
            else
            {
                SetString(index, value.ToString());
            }
        }

        public bool IsNull(int index)
        {
            if (!_hasNulls || _nullBitmap == null) return false;
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            return (_nullBitmap[index >> 3] & (1 << (index & 7))) == 0;
        }

        public void SetNull(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureNullBitmap(_length);
            _nullBitmap![index >> 3] &= (byte)~(1 << (index & 7));
            _indices[index] = -1;
            _hasNulls = true;
        }

        public IDataColumn Slice(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "Invalid slice range.");
            }

            var sliceIndices = new int[length];
            Array.Copy(_indices, start, sliceIndices, 0, length);

            byte[]? sliceBitmap = null;
            if (_hasNulls && _nullBitmap != null)
            {
                int numBytes = (length + 7) >> 3;
                sliceBitmap = new byte[numBytes];
                for (int b = 0; b < numBytes; b++) sliceBitmap[b] = 0xFF;

                for (int i = 0; i < length; i++)
                {
                    if (IsNull(start + i))
                    {
                        sliceBitmap[i >> 3] &= (byte)~(1 << (i & 7));
                    }
                }
            }

            return new StringDictionaryColumn(_name, sliceIndices, _dictionary, _lookup, sliceBitmap, _hasNulls);
        }

        public IDataColumn Clone()
        {
            var cloneIndices = new int[_length];
            Array.Copy(_indices, cloneIndices, _length);

            var cloneDict = new List<string>(_dictionary);
            var cloneLookup = new Dictionary<string, int>(_lookup, StringComparer.Ordinal);

            byte[]? cloneBitmap = null;
            if (_nullBitmap != null)
            {
                cloneBitmap = new byte[_nullBitmap.Length];
                Buffer.BlockCopy(_nullBitmap, 0, cloneBitmap, 0, _nullBitmap.Length);
            }

            return new StringDictionaryColumn(_name, cloneIndices, cloneDict, cloneLookup, cloneBitmap, _hasNulls);
        }

        public IDataColumn Filter(int[] indices)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            var filtered = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                filtered[i] = _indices[indices[i]];
            }

            byte[]? filterBitmap = null;
            if (_hasNulls && _nullBitmap != null)
            {
                int numBytes = (indices.Length + 7) >> 3;
                filterBitmap = new byte[numBytes];
                for (int b = 0; b < numBytes; b++) filterBitmap[b] = 0xFF;

                for (int i = 0; i < indices.Length; i++)
                {
                    if (IsNull(indices[i]))
                    {
                        filterBitmap[i >> 3] &= (byte)~(1 << (i & 7));
                    }
                }
            }

            return new StringDictionaryColumn(_name, filtered, _dictionary, _lookup, filterBitmap, _hasNulls);
        }

        /// <summary>
        /// Highly optimized search that finds row indices matching the specified string category using integer comparison.
        /// Bypasses string equality checks across rows.
        /// </summary>
        public int[] FindMatchingRowIndices(string categoryValue)
        {
            if (categoryValue == null)
            {
                var nullMatches = new List<int>();
                for (int i = 0; i < _length; i++)
                {
                    if (IsNull(i)) nullMatches.Add(i);
                }
                return nullMatches.ToArray();
            }

            if (!_lookup.TryGetValue(categoryValue, out int targetCategoryIdx))
            {
                return Array.Empty<int>();
            }

            var matches = new List<int>();
            for (int i = 0; i < _length; i++)
            {
                if (_hasNulls && IsNull(i)) continue;
                if (_indices[i] == targetCategoryIdx)
                {
                    matches.Add(i);
                }
            }

            return matches.ToArray();
        }

        /// <summary>
        /// Converts an existing uncompressed DataColumn&lt;string&gt; into a dictionary-encoded StringDictionaryColumn.
        /// </summary>
        public static StringDictionaryColumn FromDataColumn(DataColumn<string> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int count = source.Length;
            var indices = new int[count];
            var dict = new List<string>();
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);

            var raw = source.RawData;
            bool hasNulls = source.HasNulls;

            for (int i = 0; i < count; i++)
            {
                if (hasNulls && source.IsNull(i))
                {
                    indices[i] = -1;
                    continue;
                }

                string val = raw[i] ?? string.Empty;
                if (!lookup.TryGetValue(val, out int catIdx))
                {
                    catIdx = dict.Count;
                    dict.Add(val);
                    lookup[val] = catIdx;
                }
                indices[i] = catIdx;
            }

            byte[]? nullBitmap = null;
            if (hasNulls && source.RawNullBitmap != null)
            {
                nullBitmap = new byte[source.RawNullBitmap.Length];
                Array.Copy(source.RawNullBitmap, nullBitmap, nullBitmap.Length);
            }

            return new StringDictionaryColumn(source.Name, indices, dict, lookup, nullBitmap, hasNulls);
        }

        /// <summary>
        /// Expands this dictionary-encoded column back to a standard DataColumn&lt;string&gt;.
        /// </summary>
        public DataColumn<string> ToStringColumn()
        {
            var strings = new string[_length];
            for (int i = 0; i < _length; i++)
            {
                if (_hasNulls && IsNull(i))
                {
                    strings[i] = default!;
                }
                else
                {
                    int catIdx = _indices[i];
                    strings[i] = (catIdx >= 0 && catIdx < _dictionary.Count) ? _dictionary[catIdx] : string.Empty;
                }
            }

            byte[]? nullBitmap = null;
            if (_nullBitmap != null)
            {
                nullBitmap = new byte[_nullBitmap.Length];
                Array.Copy(_nullBitmap, nullBitmap, nullBitmap.Length);
            }

            return new DataColumn<string>(_name, strings, nullBitmap, _hasNulls);
        }

        private int GetOrAddCategory(string value)
        {
            if (_lookup.TryGetValue(value, out int idx)) return idx;

            idx = _dictionary.Count;
            _dictionary.Add(value);
            _lookup[value] = idx;
            return idx;
        }

        private void EnsureNullBitmap(int requiredLength)
        {
            int requiredBytes = (requiredLength + 7) >> 3;
            if (_nullBitmap == null)
            {
                _nullBitmap = new byte[Math.Max(requiredBytes, (_indices.Length + 7) >> 3)];
                for (int i = 0; i < _nullBitmap.Length; i++) _nullBitmap[i] = 0xFF;
            }
            else if (requiredBytes > _nullBitmap.Length)
            {
                int newBytes = Math.Max(requiredBytes, _nullBitmap.Length * 2);
                var old = _nullBitmap;
                _nullBitmap = new byte[newBytes];
                Array.Copy(old, _nullBitmap, old.Length);
                for (int i = old.Length; i < newBytes; i++) _nullBitmap[i] = 0xFF;
            }
        }
    }
}
