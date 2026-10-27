using System;
using System.IO;

namespace ZeroStorage.Core.BitIO
{
    /// <summary>
    /// High-performance bit-level stream writer with 64-bit word accumulator for variable-length encoding.
    /// </summary>
    public sealed class BitStreamWriter : IDisposable
    {
        private readonly MemoryStream _stream;
        private ulong _bitBuffer;
        private int _bitCount; // 0 to 63

        public BitStreamWriter(int initialCapacity = 4096)
        {
            _stream = new MemoryStream(initialCapacity);
            _bitBuffer = 0;
            _bitCount = 0;
        }

        /// <summary>
        /// Writes a single bit (0 or 1).
        /// </summary>
        public void WriteBit(int bit)
        {
            _bitBuffer = (_bitBuffer << 1) | (ulong)(uint)(bit & 1);
            _bitCount++;
            if (_bitCount >= 8)
            {
                _stream.WriteByte((byte)(_bitBuffer >> (_bitCount - 8)));
                _bitCount -= 8;
            }
        }

        /// <summary>
        /// Writes the lowest 'numBits' of 'value' to the stream, most significant bit first.
        /// numBits must be between 1 and 64.
        /// </summary>
        public void WriteBits(ulong value, int numBits)
        {
            if (numBits < 0 || numBits > 64)
                throw new ArgumentOutOfRangeException(nameof(numBits));

            if (numBits == 0) return;

            if (numBits > 56)
            {
                int part1 = 32;
                int part2 = numBits - 32;
                WriteBits(value >> part2, part1);
                WriteBits(value, part2);
                return;
            }

            ulong mask = (numBits == 64) ? ~0UL : ((1UL << numBits) - 1UL);
            _bitBuffer = (_bitBuffer << numBits) | (value & mask);
            _bitCount += numBits;

            while (_bitCount >= 8)
            {
                _stream.WriteByte((byte)(_bitBuffer >> (_bitCount - 8)));
                _bitCount -= 8;
            }
        }

        /// <summary>
        /// Flushes any pending bits by padding the remainder of the current byte with zeros.
        /// </summary>
        public void Flush()
        {
            if (_bitCount > 0)
            {
                byte b = (byte)((_bitBuffer & 0xFF) << (8 - _bitCount));
                _stream.WriteByte(b);
                _bitBuffer = 0;
                _bitCount = 0;
            }
        }

        /// <summary>
        /// Flushes pending bits and returns the complete byte array.
        /// </summary>
        public byte[] ToByteArray()
        {
            Flush();
            return _stream.ToArray();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
