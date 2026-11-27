using System;
using System.Buffers;
using System.Text;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Cryptography;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Thread-safe, bounded string deduplication pool supporting zero-allocation <see cref="ReadOnlySpan{T}"/> lookups.
    /// Prevents LOH/Gen2 bloat when loading repetitive ERP dimension keys (WarehouseCode, UomCode, CurrencyCode, AccountCode).
    /// </summary>
    public sealed class StringInternPool
    {
        private struct Entry
        {
            public ulong Hash;
            public string? Value;
        }

        private readonly Entry[] _entries;
        private readonly int _mask;
        private readonly int _maxEntries;
        private readonly int _maxStringLength;
        private readonly object _sync = new object();

        private int _count;
        private long _hits;
        private long _misses;

        public StringInternPool(int maxEntries = 4096, int maxStringLength = 128)
        {
            if (maxEntries <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxEntries), "MaxEntries must be positive.");
            if (maxStringLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStringLength), "MaxStringLength must be positive.");

            _maxEntries = maxEntries;
            _maxStringLength = maxStringLength;

            uint capacity = BitOps.RoundUpToPowerOfTwo((uint)Math.Max(8, maxEntries * 2));
            _entries = new Entry[capacity];
            _mask = (int)(capacity - 1);
        }

        public int MaxEntries => _maxEntries;
        public int MaxStringLength => _maxStringLength;

        public int Count
        {
            get { lock (_sync) return _count; }
        }

        public long Hits
        {
            get { lock (_sync) return _hits; }
        }

        public long Misses
        {
            get { lock (_sync) return _misses; }
        }

        /// <summary>
        /// Returns an existing pooled <see cref="string"/> matching <paramref name="value"/> with zero heap allocation,
        /// or interns and returns a newly allocated string if capacity permits.
        /// </summary>
        public unsafe string GetOrAdd(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return string.Empty;

            if (value.Length > _maxStringLength)
            {
                lock (_sync) _misses++;
                return CreateString(value);
            }

            ulong hash = FastHash.Fnv1a64(value);
            if (hash == 0) hash = 1;

            lock (_sync)
            {
                int slot = (int)(hash & (ulong)_mask);
                while (true)
                {
                    ref Entry e = ref _entries[slot];
                    if (e.Value == null)
                    {
                        _misses++;
                        string created = CreateString(value);
                        if (_count < _maxEntries)
                        {
                            e.Hash = hash;
                            e.Value = created;
                            _count++;
                        }
                        return created;
                    }

                    if (e.Hash == hash && value.SequenceEqual(e.Value.AsSpan()))
                    {
                        _hits++;
                        return e.Value;
                    }

                    slot = (slot + 1) & _mask;
                }
            }
        }

        /// <summary>
        /// Decodes a UTF-8 byte span on the stack (or pooled buffer) and returns the deduplicated <see cref="string"/> without allocating on cache hits.
        /// </summary>
        public unsafe string GetOrAddUtf8(ReadOnlySpan<byte> utf8Value)
        {
            if (utf8Value.IsEmpty) return string.Empty;

            if (utf8Value.Length > _maxStringLength * 3)
            {
                lock (_sync) _misses++;
                return DecodeString(utf8Value);
            }

            if (utf8Value.Length <= 256)
            {
                Span<char> stackChars = stackalloc char[utf8Value.Length];
                int charLen = DecodeChars(utf8Value, stackChars);
                return GetOrAdd(stackChars.Slice(0, charLen));
            }

            char[] rented = ArrayPool<char>.Shared.Rent(utf8Value.Length);
            try
            {
                int charLen = DecodeChars(utf8Value, rented.AsSpan());
                return GetOrAdd(rented.AsSpan(0, charLen));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                Array.Clear(_entries, 0, _entries.Length);
                _count = 0;
            }
        }

        private static unsafe string CreateString(ReadOnlySpan<char> chars)
        {
#if NET8_0_OR_GREATER
            return new string(chars);
#else
            fixed (char* p = chars)
            {
                return new string(p, 0, chars.Length);
            }
#endif
        }

        private static unsafe string DecodeString(ReadOnlySpan<byte> utf8)
        {
#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(utf8);
#else
            fixed (byte* p = utf8)
            {
                return Encoding.UTF8.GetString(p, utf8.Length);
            }
#endif
        }

        private static unsafe int DecodeChars(ReadOnlySpan<byte> utf8, Span<char> destination)
        {
#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetChars(utf8, destination);
#else
            fixed (byte* src = utf8)
            fixed (char* dst = destination)
            {
                return Encoding.UTF8.GetChars(src, utf8.Length, dst, destination.Length);
            }
#endif
        }
    }
}
