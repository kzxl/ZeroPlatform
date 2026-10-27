using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// High-performance, zero-allocation reader over fragmented <see cref="ReadOnlySequence{Byte}"/> buffers.
    /// Eliminates intermediate array allocations and copying when processing segmented streams from network pipes and memory-mapped files.
    /// Inlines contiguous segment reads with instant scalar access, and transparently bridges discontinuous segment boundaries.
    /// </summary>
    public ref struct SequenceSpanReader
    {
        private ReadOnlySequence<byte> _sequence;
        private SequencePosition _currentPosition;
        private SequencePosition _nextPosition;
        private ReadOnlySpan<byte> _currentSpan;
        private int _currentSpanIndex;
        private long _consumed;

        /// <summary>
        /// Gets the total number of bytes consumed so far.
        /// </summary>
        public long Consumed => _consumed;

        /// <summary>
        /// Gets the remaining number of bytes unread in the sequence.
        /// </summary>
        public long Remaining => _sequence.Length - _consumed;

        /// <summary>
        /// Gets whether the reader has reached the end of the sequence.
        /// </summary>
        public bool End => _consumed >= _sequence.Length;

        /// <summary>
        /// Gets the current unread span in the active sequence segment.
        /// </summary>
        public ReadOnlySpan<byte> UnreadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentSpan.Slice(_currentSpanIndex);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SequenceSpanReader"/> over the given sequence.
        /// </summary>
        public SequenceSpanReader(in ReadOnlySequence<byte> sequence)
        {
            _sequence = sequence;
            _currentPosition = sequence.Start;
            _nextPosition = _currentPosition;
            _currentSpan = default;
            _currentSpanIndex = 0;
            _consumed = 0;

            if (sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
            {
                _currentSpan = memory.Span;
            }
        }

        /// <summary>
        /// Reads a single byte from the sequence.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (_currentSpanIndex < _currentSpan.Length)
            {
                _consumed++;
                return _currentSpan[_currentSpanIndex++];
            }

            return ReadByteSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte ReadByteSlow()
        {
            if (!AdvanceToNextSegment())
                ThrowHelper.ThrowEndOfBuffer();

            _consumed++;
            return _currentSpan[_currentSpanIndex++];
        }

        /// <summary>
        /// Attempts to read a single byte from the sequence.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadByte(out byte value)
        {
            if (_currentSpanIndex < _currentSpan.Length)
            {
                _consumed++;
                value = _currentSpan[_currentSpanIndex++];
                return true;
            }

            return TryReadByteSlow(out value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryReadByteSlow(out byte value)
        {
            if (!AdvanceToNextSegment())
            {
                value = 0;
                return false;
            }

            _consumed++;
            value = _currentSpan[_currentSpanIndex++];
            return true;
        }

        /// <summary>
        /// Reads a 16-bit integer in little-endian format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16LittleEndian()
        {
            const int size = sizeof(short);
            if (_currentSpanIndex + size <= _currentSpan.Length)
            {
                short val = BinaryPrimitives.ReadInt16LittleEndian(_currentSpan.Slice(_currentSpanIndex));
                _currentSpanIndex += size;
                _consumed += size;
                return val;
            }

            int b0 = ReadByte();
            int b1 = ReadByte();
            return (short)(b0 | (b1 << 8));
        }

        /// <summary>
        /// Reads a 32-bit integer in little-endian format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32LittleEndian()
        {
            const int size = sizeof(int);
            if (_currentSpanIndex + size <= _currentSpan.Length)
            {
                int val = BinaryPrimitives.ReadInt32LittleEndian(_currentSpan.Slice(_currentSpanIndex));
                _currentSpanIndex += size;
                _consumed += size;
                return val;
            }

            uint b0 = ReadByte();
            uint b1 = ReadByte();
            uint b2 = ReadByte();
            uint b3 = ReadByte();
            return (int)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
        }

        /// <summary>
        /// Reads a 64-bit integer in little-endian format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64LittleEndian()
        {
            const int size = sizeof(long);
            if (_currentSpanIndex + size <= _currentSpan.Length)
            {
                long val = BinaryPrimitives.ReadInt64LittleEndian(_currentSpan.Slice(_currentSpanIndex));
                _currentSpanIndex += size;
                _consumed += size;
                return val;
            }

            ulong lo = (uint)ReadInt32LittleEndian();
            ulong hi = (uint)ReadInt32LittleEndian();
            return (long)(lo | (hi << 32));
        }

        /// <summary>
        /// Reads a 32-bit integer in big-endian network order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32BigEndian()
        {
            const int size = sizeof(int);
            if (_currentSpanIndex + size <= _currentSpan.Length)
            {
                int val = BinaryPrimitives.ReadInt32BigEndian(_currentSpan.Slice(_currentSpanIndex));
                _currentSpanIndex += size;
                _consumed += size;
                return val;
            }

            uint b0 = ReadByte();
            uint b1 = ReadByte();
            uint b2 = ReadByte();
            uint b3 = ReadByte();
            return (int)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
        }

        /// <summary>
        /// Reads contiguous bytes into the destination span across segment boundaries.
        /// </summary>
        public void ReadBytes(Span<byte> destination)
        {
            int remainingToRead = destination.Length;
            int destOffset = 0;

            while (remainingToRead > 0)
            {
                int availableInSpan = _currentSpan.Length - _currentSpanIndex;
                if (availableInSpan <= 0)
                {
                    if (!AdvanceToNextSegment())
                        ThrowHelper.ThrowEndOfBuffer();
                    availableInSpan = _currentSpan.Length - _currentSpanIndex;
                }

                int toCopy = Math.Min(remainingToRead, availableInSpan);
                _currentSpan.Slice(_currentSpanIndex, toCopy).CopyTo(destination.Slice(destOffset, toCopy));
                _currentSpanIndex += toCopy;
                _consumed += toCopy;
                destOffset += toCopy;
                remainingToRead -= toCopy;
            }
        }

        /// <summary>
        /// Advances the reader by the specified number of bytes without reading into memory.
        /// </summary>
        public void Advance(long count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            long remainingToSkip = count;
            while (remainingToSkip > 0)
            {
                int availableInSpan = _currentSpan.Length - _currentSpanIndex;
                if (availableInSpan <= 0)
                {
                    if (!AdvanceToNextSegment())
                        ThrowHelper.ThrowEndOfBuffer();
                    availableInSpan = _currentSpan.Length - _currentSpanIndex;
                }

                int toSkip = (int)Math.Min((long)availableInSpan, remainingToSkip);
                _currentSpanIndex += toSkip;
                _consumed += toSkip;
                remainingToSkip -= toSkip;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool AdvanceToNextSegment()
        {
            while (_sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
            {
                _currentPosition = _nextPosition;
                if (memory.Length > 0)
                {
                    _currentSpan = memory.Span;
                    _currentSpanIndex = 0;
                    return true;
                }
            }

            _currentSpan = default;
            _currentSpanIndex = 0;
            return false;
        }
    }
}
