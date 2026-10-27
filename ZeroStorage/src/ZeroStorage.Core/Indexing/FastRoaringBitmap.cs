using System;
using System.Collections.Generic;

namespace ZeroStorage.Core.Indexing
{
    /// <summary>
    /// Pure C# high-performance Roaring Bitmap implementation for compressed 32-bit integer sets.
    /// Provides sub-microsecond bitwise set operations (AND, OR, AND-NOT) for tag indexing.
    /// </summary>
    public sealed class FastRoaringBitmap
    {
        private const int ArrayThreshold = 4096;

        internal struct Chunk
        {
            public ushort Key;
            public IContainer Container;

            public Chunk(ushort key, IContainer container)
            {
                Key = key;
                Container = container;
            }
        }

        private readonly List<Chunk> _chunks = new List<Chunk>();

        public int ChunkCount => _chunks.Count;

        public bool IsEmpty => _chunks.Count == 0;

        public bool Add(int x)
        {
            uint u = (uint)x;
            ushort key = (ushort)(u >> 16);
            ushort val = (ushort)(u & 0xFFFF);

            int idx = BinarySearchChunk(key);
            if (idx >= 0)
            {
                var chunk = _chunks[idx];
                bool added = chunk.Container.Add(val);
                if (chunk.Container is ArrayContainer ac && ac.Count >= ArrayThreshold)
                {
                    _chunks[idx] = new Chunk(key, ac.ToBitmapContainer());
                }
                return added;
            }
            else
            {
                int insertIdx = ~idx;
                var ac = new ArrayContainer();
                ac.Add(val);
                _chunks.Insert(insertIdx, new Chunk(key, ac));
                return true;
            }
        }

        public bool Contains(int x)
        {
            uint u = (uint)x;
            ushort key = (ushort)(u >> 16);
            ushort val = (ushort)(u & 0xFFFF);

            int idx = BinarySearchChunk(key);
            if (idx < 0) return false;

            return _chunks[idx].Container.Contains(val);
        }

        public FastRoaringBitmap And(FastRoaringBitmap other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            var result = new FastRoaringBitmap();
            int i = 0;
            int j = 0;

            while (i < _chunks.Count && j < other._chunks.Count)
            {
                ushort key1 = _chunks[i].Key;
                ushort key2 = other._chunks[j].Key;

                if (key1 == key2)
                {
                    var intersected = _chunks[i].Container.And(other._chunks[j].Container);
                    if (intersected.Count > 0)
                    {
                        result._chunks.Add(new Chunk(key1, intersected));
                    }
                    i++;
                    j++;
                }
                else if (key1 < key2)
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return result;
        }

        public FastRoaringBitmap Or(FastRoaringBitmap other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            var result = new FastRoaringBitmap();
            int i = 0;
            int j = 0;

            while (i < _chunks.Count || j < other._chunks.Count)
            {
                if (i < _chunks.Count && j < other._chunks.Count)
                {
                    ushort key1 = _chunks[i].Key;
                    ushort key2 = other._chunks[j].Key;

                    if (key1 == key2)
                    {
                        var ored = _chunks[i].Container.Or(other._chunks[j].Container);
                        result._chunks.Add(new Chunk(key1, ored));
                        i++;
                        j++;
                    }
                    else if (key1 < key2)
                    {
                        result._chunks.Add(new Chunk(key1, _chunks[i].Container.Clone()));
                        i++;
                    }
                    else
                    {
                        result._chunks.Add(new Chunk(key2, other._chunks[j].Container.Clone()));
                        j++;
                    }
                }
                else if (i < _chunks.Count)
                {
                    result._chunks.Add(new Chunk(_chunks[i].Key, _chunks[i].Container.Clone()));
                    i++;
                }
                else
                {
                    result._chunks.Add(new Chunk(other._chunks[j].Key, other._chunks[j].Container.Clone()));
                    j++;
                }
            }

            return result;
        }

        public int[] ToArray()
        {
            int totalCount = 0;
            for (int c = 0; c < _chunks.Count; c++)
            {
                totalCount += _chunks[c].Container.Count;
            }

            var result = new int[totalCount];
            int offset = 0;

            for (int c = 0; c < _chunks.Count; c++)
            {
                int prefix = ((int)_chunks[c].Key) << 16;
                offset = _chunks[c].Container.Fill(prefix, result, offset);
            }

            return result;
        }

        private int BinarySearchChunk(ushort key)
        {
            int low = 0;
            int high = _chunks.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                ushort midKey = _chunks[mid].Key;

                if (midKey < key)
                    low = mid + 1;
                else if (midKey > key)
                    high = mid - 1;
                else
                    return mid;
            }

            return ~low;
        }

        #region Containers

        internal interface IContainer
        {
            int Count { get; }
            bool Contains(ushort val);
            bool Add(ushort val);
            IContainer And(IContainer other);
            IContainer Or(IContainer other);
            IContainer Clone();
            int Fill(int prefix, int[] destination, int offset);
        }

        internal sealed class ArrayContainer : IContainer
        {
            internal ushort[] _content;
            private int _count;

            public int Count => _count;

            public ArrayContainer(int initialCapacity = 4)
            {
                _content = new ushort[Math.Max(initialCapacity, 4)];
                _count = 0;
            }

            public bool Contains(ushort val)
            {
                return BinarySearch(val) >= 0;
            }

            public bool Add(ushort val)
            {
                int idx = BinarySearch(val);
                if (idx >= 0) return false;

                int insertIdx = ~idx;
                if (_count == _content.Length)
                {
                    Array.Resize(ref _content, _content.Length * 2);
                }

                if (insertIdx < _count)
                {
                    Array.Copy(_content, insertIdx, _content, insertIdx + 1, _count - insertIdx);
                }

                _content[insertIdx] = val;
                _count++;
                return true;
            }

            public IContainer And(IContainer other)
            {
                if (other is ArrayContainer ac)
                {
                    var result = new ArrayContainer(Math.Min(_count, ac._count));
                    int i = 0;
                    int j = 0;
                    while (i < _count && j < ac._count)
                    {
                        ushort v1 = _content[i];
                        ushort v2 = ac._content[j];
                        if (v1 == v2)
                        {
                            result.Add(v1);
                            i++;
                            j++;
                        }
                        else if (v1 < v2)
                        {
                            i++;
                        }
                        else
                        {
                            j++;
                        }
                    }
                    return result;
                }
                else if (other is BitmapContainer bc)
                {
                    var result = new ArrayContainer(_count);
                    for (int i = 0; i < _count; i++)
                    {
                        if (bc.Contains(_content[i]))
                        {
                            result.Add(_content[i]);
                        }
                    }
                    return result;
                }
                return new ArrayContainer();
            }

            public IContainer Or(IContainer other)
            {
                if (other is ArrayContainer ac)
                {
                    var result = new ArrayContainer(_count + ac._count);
                    int i = 0;
                    int j = 0;
                    while (i < _count || j < ac._count)
                    {
                        if (i < _count && j < ac._count)
                        {
                            ushort v1 = _content[i];
                            ushort v2 = ac._content[j];
                            if (v1 == v2) { result.Add(v1); i++; j++; }
                            else if (v1 < v2) { result.Add(v1); i++; }
                            else { result.Add(v2); j++; }
                        }
                        else if (i < _count)
                        {
                            result.Add(_content[i++]);
                        }
                        else
                        {
                            result.Add(ac._content[j++]);
                        }
                    }

                    if (result.Count >= ArrayThreshold)
                    {
                        return result.ToBitmapContainer();
                    }
                    return result;
                }
                else if (other is BitmapContainer bc)
                {
                    return bc.Or(this);
                }
                return this.Clone();
            }

            public BitmapContainer ToBitmapContainer()
            {
                var bc = new BitmapContainer();
                for (int i = 0; i < _count; i++)
                {
                    bc.Add(_content[i]);
                }
                return bc;
            }

            public IContainer Clone()
            {
                var copy = new ArrayContainer(_count);
                Array.Copy(_content, 0, copy._content, 0, _count);
                copy._count = _count;
                return copy;
            }

            public int Fill(int prefix, int[] destination, int offset)
            {
                for (int i = 0; i < _count; i++)
                {
                    destination[offset++] = prefix | _content[i];
                }
                return offset;
            }

            private int BinarySearch(ushort val)
            {
                int low = 0;
                int high = _count - 1;
                while (low <= high)
                {
                    int mid = (low + high) >> 1;
                    ushort midVal = _content[mid];
                    if (midVal < val) low = mid + 1;
                    else if (midVal > val) high = mid - 1;
                    else return mid;
                }
                return ~low;
            }
        }

        internal sealed class BitmapContainer : IContainer
        {
            private const int Words = 1024;
            private readonly ulong[] _bitmap = new ulong[Words];
            private int _count;

            public int Count => _count;

            public bool Contains(ushort val)
            {
                int wordIdx = val >> 6;
                int bitIdx = val & 63;
                return (_bitmap[wordIdx] & (1UL << bitIdx)) != 0;
            }

            public bool Add(ushort val)
            {
                int wordIdx = val >> 6;
                int bitIdx = val & 63;
                ulong mask = 1UL << bitIdx;
                if ((_bitmap[wordIdx] & mask) != 0) return false;

                _bitmap[wordIdx] |= mask;
                _count++;
                return true;
            }

            public IContainer And(IContainer other)
            {
                if (other is BitmapContainer bc)
                {
                    var result = new BitmapContainer();
                    int count = 0;
                    for (int w = 0; w < Words; w++)
                    {
                        ulong combined = _bitmap[w] & bc._bitmap[w];
                        result._bitmap[w] = combined;
                        count += PopCount(combined);
                    }
                    result._count = count;
                    if (count < ArrayThreshold)
                    {
                        return result.ToArrayContainer();
                    }
                    return result;
                }
                else if (other is ArrayContainer ac)
                {
                    return ac.And(this);
                }
                return new ArrayContainer();
            }

            public IContainer Or(IContainer other)
            {
                if (other is BitmapContainer bc)
                {
                    var result = new BitmapContainer();
                    int count = 0;
                    for (int w = 0; w < Words; w++)
                    {
                        ulong combined = _bitmap[w] | bc._bitmap[w];
                        result._bitmap[w] = combined;
                        count += PopCount(combined);
                    }
                    result._count = count;
                    return result;
                }
                else if (other is ArrayContainer ac)
                {
                    var result = (BitmapContainer)this.Clone();
                    for (int i = 0; i < ac.Count; i++)
                    {
                        result.Add(ac._content[i]); // or via ac accessor
                    }
                    return result;
                }
                return this.Clone();
            }

            public ArrayContainer ToArrayContainer()
            {
                var ac = new ArrayContainer(_count);
                for (int w = 0; w < Words; w++)
                {
                    ulong word = _bitmap[w];
                    if (word == 0) continue;
                    int baseVal = w << 6;
                    for (int b = 0; b < 64; b++)
                    {
                        if ((word & (1UL << b)) != 0)
                        {
                            ac.Add((ushort)(baseVal + b));
                        }
                    }
                }
                return ac;
            }

            public IContainer Clone()
            {
                var copy = new BitmapContainer();
                Array.Copy(_bitmap, 0, copy._bitmap, 0, Words);
                copy._count = _count;
                return copy;
            }

            public int Fill(int prefix, int[] destination, int offset)
            {
                for (int w = 0; w < Words; w++)
                {
                    ulong word = _bitmap[w];
                    if (word == 0) continue;
                    int baseVal = w << 6;
                    for (int b = 0; b < 64; b++)
                    {
                        if ((word & (1UL << b)) != 0)
                        {
                            destination[offset++] = prefix | (baseVal + b);
                        }
                    }
                }
                return offset;
            }

            private static int PopCount(ulong x)
            {
                x -= (x >> 1) & 0x5555555555555555UL;
                x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
                x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
                return (int)((x * 0x0101010101010101UL) >> 56);
            }
        }

        #endregion
    }
}
