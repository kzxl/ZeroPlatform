using System;

namespace ZeroStorage.Core.BitIO
{
    /// <summary>
    /// High-performance bit-level stream reader with 64-bit reservoir buffer for variable-length decoding.
    /// </summary>
    public sealed class BitStreamReader
    {
        private readonly byte[] _buffer;
        private int _byteOffset;
        private ulong _bitBuffer;
        private int _bitsInReservoir;

        public bool HasMoreBits => _bitsInReservoir > 0 || _byteOffset < _buffer.Length;

        public BitStreamReader(byte[] buffer, int offset = 0)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _byteOffset = offset;
            _bitBuffer = 0;
            _bitsInReservoir = 0;
        }

        private void Refill()
        {
            while (_bitsInReservoir <= 56 && _byteOffset < _buffer.Length)
            {
                _bitBuffer |= ((ulong)_buffer[_byteOffset++] << (56 - _bitsInReservoir));
                _bitsInReservoir += 8;
            }
        }

        /// <summary>
        /// Reads a single bit (returns 0 or 1).
        /// </summary>
        public int ReadBit()
        {
            if (_bitsInReservoir == 0)
            {
                Refill();
                if (_bitsInReservoir == 0)
                    throw new InvalidOperationException("End of bit stream reached.");
            }

            int bit = (int)((_bitBuffer >> 63) & 1UL);
            _bitBuffer <<= 1;
            _bitsInReservoir--;
            return bit;
        }

        /// <summary>
        /// Reads 'numBits' from the stream and packs them into a 64-bit unsigned integer.
        /// </summary>
        public ulong ReadBits(int numBits)
        {
            if (numBits < 0 || numBits > 64)
                throw new ArgumentOutOfRangeException(nameof(numBits));

            if (numBits == 0) return 0;

            if (_bitsInReservoir < numBits)
            {
                Refill();
            }

            if (_bitsInReservoir >= numBits)
            {
                ulong result = (numBits == 64) ? _bitBuffer : (_bitBuffer >> (64 - numBits));
                _bitBuffer = (numBits == 64) ? 0 : (_bitBuffer << numBits);
                _bitsInReservoir -= numBits;
                return result;
            }

            int available = _bitsInReservoir;
            if (available == 0 && _byteOffset >= _buffer.Length)
                throw new InvalidOperationException("End of bit stream reached.");

            ulong high = (available > 0) ? (_bitBuffer >> (64 - available)) : 0;
            _bitBuffer = 0;
            _bitsInReservoir = 0;

            int remaining = numBits - available;
            Refill();
            if (_bitsInReservoir < remaining)
                throw new InvalidOperationException("End of bit stream reached.");

            ulong low = _bitBuffer >> (64 - remaining);
            _bitBuffer <<= remaining;
            _bitsInReservoir -= remaining;

            return (high << remaining) | low;
        }
    }
}
