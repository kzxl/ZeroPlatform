using System;
using System.Threading;

namespace ZeroAudio.Buffers
{
    /// <summary>
    /// High-performance, lock-free circular ring buffer for real-time audio sample streaming.
    /// Thread-safe for Single-Producer Single-Consumer (SPSC) patterns.
    /// </summary>
    public sealed class AudioRingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _mask;
        private readonly int _capacity;

        // Sequence positions (monotonic increasing, wrapped with mask on access)
        private long _writePos;
        private long _readPos;

        public int Capacity => _capacity;

        public AudioRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            // Round up to nearest power of two for fast bitwise masking
            _capacity = RoundUpToPowerOfTwo(capacity);
            _mask = _capacity - 1;
            _buffer = new float[_capacity];
        }

        /// <summary>
        /// Gets the number of samples currently available to be read from the buffer.
        /// </summary>
        public int AvailableRead
        {
            get
            {
                long write = Volatile.Read(ref _writePos);
                long read = Volatile.Read(ref _readPos);
                return (int)Math.Max(0, write - read);
            }
        }

        /// <summary>
        /// Gets the free space (in samples) remaining in the buffer for writing.
        /// </summary>
        public int AvailableWrite
        {
            get
            {
                long write = Volatile.Read(ref _writePos);
                long read = Volatile.Read(ref _readPos);
                return (int)Math.Max(0, _capacity - (write - read));
            }
        }

        /// <summary>
        /// Writes samples into the ring buffer. Returns the actual number of samples written.
        /// </summary>
        public int Write(ReadOnlySpan<float> source)
        {
            if (source.IsEmpty) return 0;

            long read = Volatile.Read(ref _readPos);
            long write = _writePos; // Producer owns _writePos

            int available = (int)(_capacity - (write - read));
            int toWrite = Math.Min(source.Length, available);
            if (toWrite <= 0) return 0;

            int writeIdx = (int)(write & _mask);
            int firstChunk = Math.Min(toWrite, _capacity - writeIdx);

            source.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(writeIdx, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                source.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            Volatile.Write(ref _writePos, write + toWrite);
            return toWrite;
        }

        /// <summary>
        /// Reads samples from the ring buffer into destination. Returns the actual number of samples read.
        /// </summary>
        public int Read(Span<float> destination)
        {
            if (destination.IsEmpty) return 0;

            long write = Volatile.Read(ref _writePos);
            long read = _readPos; // Consumer owns _readPos

            int available = (int)(write - read);
            int toRead = Math.Min(destination.Length, available);
            if (toRead <= 0) return 0;

            int readIdx = (int)(read & _mask);
            int firstChunk = Math.Min(toRead, _capacity - readIdx);

            _buffer.AsSpan(readIdx, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            Volatile.Write(ref _readPos, read + toRead);
            return toRead;
        }

        /// <summary>
        /// Peeks at available samples without advancing the read position.
        /// </summary>
        public int Peek(Span<float> destination)
        {
            if (destination.IsEmpty) return 0;

            long write = Volatile.Read(ref _writePos);
            long read = Volatile.Read(ref _readPos);

            int available = (int)(write - read);
            int toRead = Math.Min(destination.Length, available);
            if (toRead <= 0) return 0;

            int readIdx = (int)(read & _mask);
            int firstChunk = Math.Min(toRead, _capacity - readIdx);

            _buffer.AsSpan(readIdx, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            return toRead;
        }

        /// <summary>
        /// Clears all data in the ring buffer.
        /// </summary>
        public void Clear()
        {
            long write = Volatile.Read(ref _writePos);
            Volatile.Write(ref _readPos, write);
        }

        private static int RoundUpToPowerOfTwo(int value)
        {
            if (value <= 1) return 1;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}
