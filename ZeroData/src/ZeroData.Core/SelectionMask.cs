using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace ZeroData.Core
{
    /// <summary>
    /// Compact bitmask representation for row selections conforming to Apache Arrow null/validity bitmask conventions.
    /// Enables zero-copy bitwise query composition (AND, OR, NOT) across columnar filters without intermediate index allocations.
    /// </summary>
    public sealed class SelectionMask
    {
        private readonly byte[] _bitmap;
        private readonly int _length;
        private int _selectedCount = -1;

        public int Length => _length;
        public byte[] RawBitmap => _bitmap;

        public SelectionMask(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            _length = length;
            int bytes = (length + 7) >> 3;
            _bitmap = new byte[bytes];
        }

        public SelectionMask(int length, byte[] bitmap, bool copy = false)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            _length = length;
            int requiredBytes = (length + 7) >> 3;
            if (bitmap.Length < requiredBytes)
            {
                throw new ArgumentException($"Bitmap length {bitmap.Length} is insufficient for {length} bits.", nameof(bitmap));
            }

            if (copy)
            {
                _bitmap = new byte[requiredBytes];
                Array.Copy(bitmap, _bitmap, requiredBytes);
            }
            else
            {
                _bitmap = bitmap;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSelected(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            return (_bitmap[index >> 3] & (1 << (index & 7))) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSelected(int index, bool selected = true)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            if (selected)
            {
                _bitmap[index >> 3] |= (byte)(1 << (index & 7));
            }
            else
            {
                _bitmap[index >> 3] &= (byte)~(1 << (index & 7));
            }
            _selectedCount = -1;
        }

        public int SelectedCount
        {
            get
            {
                if (_selectedCount >= 0) return _selectedCount;

                int count = 0;
                int fullBytes = _length >> 3;

                for (int i = 0; i < fullBytes; i++)
                {
                    count += PopCount(_bitmap[i]);
                }

                int rem = _length & 7;
                if (rem > 0)
                {
                    byte lastByte = (byte)(_bitmap[fullBytes] & ((1 << rem) - 1));
                    count += PopCount(lastByte);
                }

                _selectedCount = count;
                return count;
            }
        }

        public static SelectionMask CreateAllSelected(int length)
        {
            var mask = new SelectionMask(length);
            int bytes = (length + 7) >> 3;
            for (int i = 0; i < bytes; i++) mask._bitmap[i] = 0xFF;
            mask._selectedCount = length;
            return mask;
        }

        public static SelectionMask CreateEmpty(int length)
        {
            var mask = new SelectionMask(length);
            mask._selectedCount = 0;
            return mask;
        }

        public static SelectionMask FromIndices(int length, ReadOnlySpan<int> indices)
        {
            var mask = new SelectionMask(length);
            for (int i = 0; i < indices.Length; i++)
            {
                mask.SetSelected(indices[i], true);
            }
            mask._selectedCount = indices.Length;
            return mask;
        }

        /// <summary>
        /// Computes bitwise AND with another SelectionMask (SIMD unrolled).
        /// </summary>
        public SelectionMask And(SelectionMask other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other._length != _length) throw new ArgumentException("Mask lengths must match.", nameof(other));

            int bytes = (_length + 7) >> 3;
            byte[] res = new byte[bytes];
            var b1 = _bitmap;
            var b2 = other._bitmap;

            int i = 0;
            int limit = bytes - 8;
            unsafe
            {
                fixed (byte* p1 = b1)
                fixed (byte* p2 = b2)
                fixed (byte* pR = res)
                {
                    for (; i <= limit; i += 8)
                    {
                        *(ulong*)(pR + i) = *(ulong*)(p1 + i) & *(ulong*)(p2 + i);
                    }
                }
            }

            for (; i < bytes; i++)
            {
                res[i] = (byte)(b1[i] & b2[i]);
            }

            return new SelectionMask(_length, res);
        }

        /// <summary>
        /// Computes bitwise OR with another SelectionMask (SIMD unrolled).
        /// </summary>
        public SelectionMask Or(SelectionMask other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other._length != _length) throw new ArgumentException("Mask lengths must match.", nameof(other));

            int bytes = (_length + 7) >> 3;
            byte[] res = new byte[bytes];
            var b1 = _bitmap;
            var b2 = other._bitmap;

            int i = 0;
            int limit = bytes - 8;
            unsafe
            {
                fixed (byte* p1 = b1)
                fixed (byte* p2 = b2)
                fixed (byte* pR = res)
                {
                    for (; i <= limit; i += 8)
                    {
                        *(ulong*)(pR + i) = *(ulong*)(p1 + i) | *(ulong*)(p2 + i);
                    }
                }
            }

            for (; i < bytes; i++)
            {
                res[i] = (byte)(b1[i] | b2[i]);
            }

            return new SelectionMask(_length, res);
        }

        /// <summary>
        /// Inverts all selection bits.
        /// </summary>
        public SelectionMask Not()
        {
            int bytes = (_length + 7) >> 3;
            byte[] res = new byte[bytes];
            var b1 = _bitmap;

            int i = 0;
            int limit = bytes - 8;
            unsafe
            {
                fixed (byte* p1 = b1)
                fixed (byte* pR = res)
                {
                    for (; i <= limit; i += 8)
                    {
                        *(ulong*)(pR + i) = ~*(ulong*)(p1 + i);
                    }
                }
            }

            for (; i < bytes; i++)
            {
                res[i] = (byte)~b1[i];
            }

            return new SelectionMask(_length, res);
        }

        /// <summary>
        /// Converts active selection bits into a contiguous integer array of row indices.
        /// </summary>
        public int[] ToIndices()
        {
            int count = SelectedCount;
            if (count == 0) return Array.Empty<int>();

            int[] indices = new int[count];
            int written = 0;
            int bytes = (_length + 7) >> 3;

            for (int byteIdx = 0; byteIdx < bytes; byteIdx++)
            {
                byte b = _bitmap[byteIdx];
                if (b == 0) continue;

                int baseIdx = byteIdx << 3;
                for (int bit = 0; bit < 8; bit++)
                {
                    int row = baseIdx + bit;
                    if (row >= _length) break;

                    if ((b & (1 << bit)) != 0)
                    {
                        indices[written++] = row;
                        if (written == count) break;
                    }
                }
                if (written == count) break;
            }

            return indices;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(byte value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            int v = value;
            v = v - ((v >> 1) & 0x55);
            v = (v & 0x33) + ((v >> 2) & 0x33);
            return (v + (v >> 4)) & 0x0F;
#endif
        }
    }
}
