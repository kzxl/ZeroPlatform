using System;
using System.Threading;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// High-speed contiguous flat array memory store for SCADA tags.
    /// Indexed directly by integer TagId for O(1) cache-friendly access, zero heap allocations,
    /// and bitset-driven dirty tracking.
    /// </summary>
    public sealed class TagStorage
    {
        private readonly object _resizeLock = new object();
        private ScadaValue[] _values;
        private long[] _timestampsUtcMs;
        private long[] _dirtyMask;
        private int _capacity;

        public int Capacity => _capacity;

        public TagStorage(int initialCapacity = 1024)
        {
            _capacity = Math.Max(64, initialCapacity);
            _values = new ScadaValue[_capacity];
            _timestampsUtcMs = new long[_capacity];
            _dirtyMask = new long[(_capacity + 63) / 64];
        }

        public void EnsureCapacity(int requiredTagId)
        {
            if (requiredTagId < _capacity) return;

            lock (_resizeLock)
            {
                if (requiredTagId < _capacity) return;

                int newCap = Math.Max(_capacity * 2, requiredTagId + 64);
                var newValues = new ScadaValue[newCap];
                var newTimestamps = new long[newCap];
                var newDirtyMask = new long[(newCap + 63) / 64];

                Array.Copy(_values, newValues, _values.Length);
                Array.Copy(_timestampsUtcMs, newTimestamps, _timestampsUtcMs.Length);
                Array.Copy(_dirtyMask, newDirtyMask, _dirtyMask.Length);

                _values = newValues;
                _timestampsUtcMs = newTimestamps;
                _dirtyMask = newDirtyMask;
                _capacity = newCap;
            }
        }

        public void Set(int tagId, in ScadaValue value, long timestampUtcMs)
        {
            if (tagId >= _capacity)
            {
                EnsureCapacity(tagId);
            }

            _values[tagId] = value;
            Interlocked.Exchange(ref _timestampsUtcMs[tagId], timestampUtcMs);
            MarkDirty(tagId);
        }

        public ScadaValue GetValue(int tagId)
        {
            if (tagId < 0 || tagId >= _capacity) return ScadaValue.Empty;
            return _values[tagId];
        }

        public long GetTimestampUtcMs(int tagId)
        {
            if (tagId < 0 || tagId >= _capacity) return 0;
            return Interlocked.Read(ref _timestampsUtcMs[tagId]);
        }

        public void MarkDirty(int tagId)
        {
            if (tagId < 0 || tagId >= _capacity) return;
            int wordIdx = tagId >> 6;
            long bit = 1L << (tagId & 63);

            ref long wordRef = ref _dirtyMask[wordIdx];
            long current;
            do
            {
                current = Volatile.Read(ref wordRef);
            } while (Interlocked.CompareExchange(ref wordRef, current | bit, current) != current);
        }

        public bool IsDirty(int tagId)
        {
            if (tagId < 0 || tagId >= _capacity) return false;
            int wordIdx = tagId >> 6;
            long bit = 1L << (tagId & 63);
            return (Volatile.Read(ref _dirtyMask[wordIdx]) & bit) != 0;
        }

        /// <summary>
        /// Atomically extracts all dirty tag IDs into the destination array and clears dirty flags.
        /// Returns the number of dirty tag IDs written into destination.
        /// </summary>
        public int DrainDirtyTags(int[] destination)
        {
            if (destination == null || destination.Length == 0) return 0;
            return DrainDirtyTags(destination.AsSpan());
        }

        /// <summary>
        /// Atomically extracts all dirty tag IDs into the destination Span and clears dirty flags.
        /// Zero heap allocation on hot cycle flushes.
        /// </summary>
        public int DrainDirtyTags(Span<int> destination)
        {
            if (destination.IsEmpty) return 0;

            int count = 0;
            int wordsCount = _dirtyMask.Length;

            for (int wordIdx = 0; wordIdx < wordsCount; wordIdx++)
            {
                ref long wordRef = ref _dirtyMask[wordIdx];
                if (Volatile.Read(ref wordRef) == 0) continue;

                // Atomically clear word and capture snapshot of dirty bits
                long currentWord = Interlocked.Exchange(ref wordRef, 0);
                if (currentWord == 0) continue;

                ulong dirtyWord = (ulong)currentWord;
                int baseTagId = wordIdx << 6;

                while (dirtyWord != 0)
                {
                    // Find lowest set bit
                    ulong lowestBit = dirtyWord & (0UL - dirtyWord);
                    int bitIndex = GetBitIndex(lowestBit);
                    int tagId = baseTagId + bitIndex;

                    if (count < destination.Length)
                    {
                        destination[count++] = tagId;
                    }

                    dirtyWord &= (dirtyWord - 1); // Clear lowest bit
                }

                if (count >= destination.Length) break;
            }

            return count;
        }

        private static int GetBitIndex(ulong singleBit)
        {
#if NETCOREAPP || NET8_0_OR_GREATER
            return System.Numerics.BitOperations.TrailingZeroCount(singleBit);
#else
            if (singleBit == 0) return 0;
            int c = 0;
            if ((singleBit & 0x00000000FFFFFFFFUL) == 0) { singleBit >>= 32; c += 32; }
            if ((singleBit & 0x000000000000FFFFUL) == 0) { singleBit >>= 16; c += 16; }
            if ((singleBit & 0x00000000000000FFUL) == 0) { singleBit >>= 8; c += 8; }
            if ((singleBit & 0x000000000000000FUL) == 0) { singleBit >>= 4; c += 4; }
            if ((singleBit & 0x0000000000000003UL) == 0) { singleBit >>= 2; c += 2; }
            if ((singleBit & 0x0000000000000001UL) == 0) { c += 1; }
            return c;
#endif
        }
    }
}
