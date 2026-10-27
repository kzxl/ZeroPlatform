using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// High-performance, zero-allocation bitset operating directly over arbitrary Span of bytes.
    /// Provides hardware-accelerated bit manipulation, counting, and scanning without heap overhead.
    /// </summary>
    public readonly ref struct BitSpan
    {
        private readonly Span<byte> _bytes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitSpan(Span<byte> bytes)
        {
            _bytes = bytes;
        }

        public readonly int LengthBits
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _bytes.Length * 8;
        }

        public readonly int LengthBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _bytes.Length;
        }

        public readonly Span<byte> Bytes => _bytes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int bitIndex)
        {
            if ((uint)bitIndex >= (uint)LengthBits)
                ThrowOutOfRange();

            int byteIndex = bitIndex >> 3;
            int bitOffset = bitIndex & 7;
            return (_bytes[byteIndex] & (1 << bitOffset)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int bitIndex)
        {
            if ((uint)bitIndex >= (uint)LengthBits)
                ThrowOutOfRange();

            int byteIndex = bitIndex >> 3;
            int bitOffset = bitIndex & 7;
            _bytes[byteIndex] |= (byte)(1 << bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int bitIndex)
        {
            if ((uint)bitIndex >= (uint)LengthBits)
                ThrowOutOfRange();

            int byteIndex = bitIndex >> 3;
            int bitOffset = bitIndex & 7;
            _bytes[byteIndex] &= (byte)~(1 << bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Toggle(int bitIndex)
        {
            if ((uint)bitIndex >= (uint)LengthBits)
                ThrowOutOfRange();

            int byteIndex = bitIndex >> 3;
            int bitOffset = bitIndex & 7;
            _bytes[byteIndex] ^= (byte)(1 << bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAll()
        {
            _bytes.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAll()
        {
            _bytes.Fill(0xFF);
        }

        /// <summary>
        /// Counts the total number of set bits (1s) across the entire span using hardware bit operations.
        /// </summary>
        public int CountSetBits()
        {
            int total = 0;
            for (int i = 0; i < _bytes.Length; i++)
            {
                total += BitOps.PopCount(_bytes[i]);
            }
            return total;
        }

        /// <summary>
        /// Finds the index of the next set bit starting from the specified bit index.
        /// Returns -1 if no set bit is found.
        /// </summary>
        public int FindNextSetBit(int startBit = 0)
        {
            int totalBits = LengthBits;
            if (startBit < 0 || startBit >= totalBits) return -1;

            int startByte = startBit >> 3;
            int firstBitOffset = startBit & 7;

            // Handle first partial byte
            byte b = (byte)(_bytes[startByte] >> firstBitOffset);
            if (b != 0)
            {
                return startBit + BitOps.TrailingZeroCount(b);
            }

            // Scan subsequent full bytes
            for (int i = startByte + 1; i < _bytes.Length; i++)
            {
                byte val = _bytes[i];
                if (val != 0)
                {
                    return (i << 3) + BitOps.TrailingZeroCount(val);
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOutOfRange()
        {
            throw new IndexOutOfRangeException("Bit index was outside the bounds of the BitSpan.");
        }
    }
}
