using System;

namespace ZeroComm.Core.Buffers
{
    /// <summary>
    /// High-throughput circular ring buffer designed for streaming protocol framing and packet extraction.
    /// Eliminates heap allocations during continuous TCP socket or Serial port data ingestion.
    /// </summary>
    public sealed class CircularRingBuffer
    {
        private readonly byte[] _buffer;
        private int _head; // Read index
        private int _tail; // Write index
        private int _count;
        private readonly object _syncRoot = new object();

        public int Capacity => _buffer.Length;
        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _count;
                }
            }
        }

        public int FreeSpace => Capacity - Count;

        public CircularRingBuffer(int capacity = 65536)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

            _buffer = new byte[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Writes data into the circular buffer. Throws InvalidOperationException if buffer overflows.
        /// </summary>
        public void Write(byte[] src, int offset, int count)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (offset < 0 || count < 0 || offset + count > src.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return;

            lock (_syncRoot)
            {
                if (count > FreeSpace)
                    throw new InvalidOperationException($"Ring buffer overflow. Available space: {FreeSpace}, requested: {count}");

                int firstChunk = Math.Min(count, Capacity - _tail);
                Array.Copy(src, offset, _buffer, _tail, firstChunk);

                int secondChunk = count - firstChunk;
                if (secondChunk > 0)
                {
                    Array.Copy(src, offset + firstChunk, _buffer, 0, secondChunk);
                }

                _tail = (_tail + count) % Capacity;
                _count += count;
            }
        }

        public void Write(byte[] src) => Write(src, 0, src.Length);

        /// <summary>
        /// Peeks bytes from the buffer without advancing the read position.
        /// </summary>
        public int Peek(byte[] dst, int offset, int count)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (offset < 0 || count < 0 || offset + count > dst.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_syncRoot)
            {
                int bytesToRead = Math.Min(count, _count);
                if (bytesToRead == 0) return 0;

                int firstChunk = Math.Min(bytesToRead, Capacity - _head);
                Array.Copy(_buffer, _head, dst, offset, firstChunk);

                int secondChunk = bytesToRead - firstChunk;
                if (secondChunk > 0)
                {
                    Array.Copy(_buffer, 0, dst, offset + firstChunk, secondChunk);
                }

                return bytesToRead;
            }
        }

        /// <summary>
        /// Peeks a single byte at the specified index relative to head (0 = first unread byte).
        /// </summary>
        public byte PeekByte(int relativeIndex)
        {
            lock (_syncRoot)
            {
                if (relativeIndex < 0 || relativeIndex >= _count)
                    throw new ArgumentOutOfRangeException(nameof(relativeIndex));

                int idx = (_head + relativeIndex) % Capacity;
                return _buffer[idx];
            }
        }

        /// <summary>
        /// Reads and removes bytes from the buffer into the destination array.
        /// </summary>
        public int Read(byte[] dst, int offset, int count)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (offset < 0 || count < 0 || offset + count > dst.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_syncRoot)
            {
                int bytesToRead = Math.Min(count, _count);
                if (bytesToRead == 0) return 0;

                int firstChunk = Math.Min(bytesToRead, Capacity - _head);
                Array.Copy(_buffer, _head, dst, offset, firstChunk);

                int secondChunk = bytesToRead - firstChunk;
                if (secondChunk > 0)
                {
                    Array.Copy(_buffer, 0, dst, offset + firstChunk, secondChunk);
                }

                _head = (_head + bytesToRead) % Capacity;
                _count -= bytesToRead;
                return bytesToRead;
            }
        }

        /// <summary>
        /// Advances the read pointer by specified count, discarding the read bytes.
        /// </summary>
        public void Advance(int count)
        {
            lock (_syncRoot)
            {
                if (count < 0 || count > _count)
                    throw new ArgumentOutOfRangeException(nameof(count));

                _head = (_head + count) % Capacity;
                _count -= count;
            }
        }

        /// <summary>
        /// Clears all data in the buffer.
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _head = 0;
                _tail = 0;
                _count = 0;
            }
        }

        /// <summary>
        /// Searches for the first occurrence of a byte value. Returns relative index from head, or -1 if not found.
        /// </summary>
        public int IndexOf(byte value)
        {
            lock (_syncRoot)
            {
                for (int i = 0; i < _count; i++)
                {
                    int idx = (_head + i) % Capacity;
                    if (_buffer[idx] == value)
                        return i;
                }
                return -1;
            }
        }

        /// <summary>
        /// Searches for the first occurrence of a byte sequence. Returns relative index from head, or -1 if not found.
        /// </summary>
        public int IndexOfSequence(byte[] sequence)
        {
            if (sequence == null || sequence.Length == 0) return -1;

            lock (_syncRoot)
            {
                int seqLen = sequence.Length;
                if (_count < seqLen) return -1;

                for (int i = 0; i <= _count - seqLen; i++)
                {
                    bool match = true;
                    for (int j = 0; j < seqLen; j++)
                    {
                        int idx = (_head + i + j) % Capacity;
                        if (_buffer[idx] != sequence[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return i;
                }
                return -1;
            }
        }
    }
}
