using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// High-speed, zero-allocation open-addressing hash table utilizing Robin Hood hashing.
    /// Provides cacheline-friendly flat array storage with zero per-node heap allocations,
    /// outperforming standard BCL Dictionary in lookup latency and memory footprint.
    /// </summary>
    /// <typeparam name="TKey">Key type (must implement IEquatable or override Equals/GetHashCode).</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    public sealed class ZeroRobinHoodMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
    {
        private const float MaxLoadFactor = 0.85f;
        private const int DefaultCapacity = 16;

        private struct Entry
        {
            public int HashCode;
            public int Distance; // -1 indicates an empty slot
            public TKey Key;
            public TValue Value;
        }

        private Entry[] _entries;
        private int _mask;
        private int _count;
        private int _threshold;
        private readonly IEqualityComparer<TKey> _comparer;

        /// <summary>
        /// Gets the current number of elements in the map.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the total slot capacity.
        /// </summary>
        public int Capacity => _entries.Length;

        /// <summary>
        /// Initializes a new instance with the specified initial capacity and optional comparer.
        /// </summary>
        public ZeroRobinHoodMap(int initialCapacity = DefaultCapacity, IEqualityComparer<TKey>? comparer = null)
        {
            int capacity = GetNextPowerOfTwo(Math.Max(initialCapacity, DefaultCapacity));
            _entries = new Entry[capacity];
            for (int i = 0; i < _entries.Length; i++) _entries[i].Distance = -1;

            _mask = capacity - 1;
            _threshold = (int)(capacity * MaxLoadFactor);
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            int initialSlot = hashCode & _mask;
            int dist = 0;
            int slot = initialSlot;

            while (true)
            {
                ref readonly Entry entry = ref _entries[slot];
                if (entry.Distance == -1 || dist > entry.Distance)
                {
                    value = default!;
                    return false;
                }

                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                {
                    value = entry.Value;
                    return true;
                }

                slot = (slot + 1) & _mask;
                dist++;
            }
        }

        /// <summary>
        /// Sets or inserts a key-value pair into the map.
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (_count >= _threshold)
            {
                Resize(_entries.Length * 2);
            }

            int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            InsertOrUpdate(key, value, hashCode);
        }

        private void InsertOrUpdate(TKey key, TValue value, int hashCode)
        {
            int initialSlot = hashCode & _mask;
            int slot = initialSlot;

            TKey currentKey = key;
            TValue currentValue = value;
            int currentHash = hashCode;
            int currentDist = 0;

            while (true)
            {
                ref Entry entry = ref _entries[slot];

                if (entry.Distance == -1)
                {
                    // Empty slot found, insert here
                    entry.HashCode = currentHash;
                    entry.Distance = currentDist;
                    entry.Key = currentKey;
                    entry.Value = currentValue;
                    _count++;
                    return;
                }

                if (entry.HashCode == currentHash && _comparer.Equals(entry.Key, currentKey))
                {
                    // Key already exists, update in-place
                    entry.Value = currentValue;
                    return;
                }

                // Robin Hood principle: rich steals from poor
                if (currentDist > entry.Distance)
                {
                    // Swap current item with entry and keep looking for a slot for the displaced entry
                    int tempHash = entry.HashCode;
                    int tempDist = entry.Distance;
                    TKey tempKey = entry.Key;
                    TValue tempVal = entry.Value;

                    entry.HashCode = currentHash;
                    entry.Distance = currentDist;
                    entry.Key = currentKey;
                    entry.Value = currentValue;

                    currentHash = tempHash;
                    currentDist = tempDist;
                    currentKey = tempKey;
                    currentValue = tempVal;
                }

                slot = (slot + 1) & _mask;
                currentDist++;
            }
        }

        /// <summary>
        /// Removes the element with the specified key.
        /// </summary>
        public bool Remove(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
            int initialSlot = hashCode & _mask;
            int dist = 0;
            int slot = initialSlot;

            while (true)
            {
                ref Entry entry = ref _entries[slot];
                if (entry.Distance == -1 || dist > entry.Distance)
                {
                    return false;
                }

                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                {
                    // Backward shift deletion to preserve Robin Hood invariant
                    int curr = slot;
                    while (true)
                    {
                        int next = (curr + 1) & _mask;
                        ref Entry nextEntry = ref _entries[next];

                        if (nextEntry.Distance <= 0)
                        {
                            _entries[curr].Distance = -1;
                            _entries[curr].Key = default!;
                            _entries[curr].Value = default!;
                            break;
                        }

                        _entries[curr] = nextEntry;
                        _entries[curr].Distance--;
                        curr = next;
                    }

                    _count--;
                    return true;
                }

                slot = (slot + 1) & _mask;
                dist++;
            }
        }

        /// <summary>
        /// Clears all entries from the map.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i].Distance = -1;
                _entries[i].Key = default!;
                _entries[i].Value = default!;
            }
            _count = 0;
        }

        private void Resize(int newCapacity)
        {
            var oldEntries = _entries;
            _entries = new Entry[newCapacity];
            for (int i = 0; i < _entries.Length; i++) _entries[i].Distance = -1;

            _mask = newCapacity - 1;
            _threshold = (int)(newCapacity * MaxLoadFactor);
            _count = 0;

            for (int i = 0; i < oldEntries.Length; i++)
            {
                if (oldEntries[i].Distance != -1)
                {
                    InsertOrUpdate(oldEntries[i].Key, oldEntries[i].Value, oldEntries[i].HashCode);
                }
            }
        }

        private static int GetNextPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value) power <<= 1;
            return power;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Distance != -1)
                {
                    yield return new KeyValuePair<TKey, TValue>(_entries[i].Key, _entries[i].Value);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
