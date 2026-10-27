using System;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Layout
{
    /// <summary>
    /// Cumulative sum array allowing O(log N) binary search coordinate lookup for dynamic row/col dimensions.
    /// </summary>
    public sealed class PrefixSumArray
    {
        private int[] _prefixSums;
        private int _count;

        public PrefixSumArray(int initialCapacity = 1024)
        {
            _prefixSums = new int[initialCapacity];
            _count = 0;
        }

        public int Count => _count;

        public int TotalDimension => _count > 0 ? _prefixSums[_count - 1] : 0;

        public void InitializeUniform(int count, int uniformSize)
        {
            if (_prefixSums.Length < count)
            {
                _prefixSums = new int[Math.Max(count, _prefixSums.Length * 2)];
            }

            _count = count;
            int accum = 0;
            for (int i = 0; i < count; i++)
            {
                accum += uniformSize;
                _prefixSums[i] = accum;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindIndexAt(int coordinate)
        {
            if (_count == 0 || coordinate < 0) return 0;
            if (coordinate >= TotalDimension) return _count - 1;

            int index = Array.BinarySearch(_prefixSums, 0, _count, coordinate);
            if (index >= 0)
            {
                // Found exact boundary; next item starts here
                return Math.Min(index + 1, _count - 1);
            }
            int bitwiseNot = ~index;
            return Math.Min(bitwiseNot, _count - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffset(int index)
        {
            if (index <= 0) return 0;
            if (index >= _count) return TotalDimension;
            return _prefixSums[index - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSize(int index)
        {
            if (index < 0 || index >= _count) return 0;
            return index == 0 ? _prefixSums[0] : _prefixSums[index] - _prefixSums[index - 1];
        }
    }
}
