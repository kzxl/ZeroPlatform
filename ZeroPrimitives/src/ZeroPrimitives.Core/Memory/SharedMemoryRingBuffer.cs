#pragma warning disable CA1416

using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Represents an ultra-low-latency, zero-copy Inter-Process Communication (IPC) ring buffer
    /// built directly on top of an unmanaged Memory-Mapped File (MMF).
    /// <para>
    /// Enables sub-microsecond data exchanges (camera frames, LiDAR point clouds, AI inference tensors)
    /// between C# processes or between C# and Python AI processes with ZERO socket overhead and ZERO GC pause.
    /// </para>
    /// </summary>
    public sealed unsafe class SharedMemoryRingBuffer : IDisposable
    {
        private const uint MagicNumber = 0x5A45524F; // "ZERO"
        private const uint ProtocolVersion = 1;
        private const int HeaderSize = 64; // 64-byte cache-line aligned header

        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly byte* _basePtr;
        private readonly byte* _payloadPtr;
        private readonly int _capacity;
        private readonly int _capacityMask;
        private readonly bool _ownsFile;
        private int _disposed;

        // Header memory layout structure:
        // [0..3]   : uint Magic (0x5A45524F)
        // [4..7]   : uint Version (1)
        // [8..11]  : int  Capacity
        // [12..15] : int  Reserved
        // [16..23] : long WriteHead (Monotonically increasing atomic sequence)
        // [24..31] : long ReadTail  (Monotonically increasing atomic sequence)
        // [32..63] : 32 bytes padding for 64-byte cache line isolation

        private uint* MagicPtr => (uint*)_basePtr;
        private uint* VersionPtr => (uint*)(_basePtr + 4);
        private int* CapacityPtr => (int*)(_basePtr + 8);
        private long* WriteHeadPtr => (long*)(_basePtr + 16);
        private long* ReadTailPtr => (long*)(_basePtr + 24);

        /// <summary>
        /// Gets the total usable payload capacity in bytes of the ring buffer.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the current number of readable bytes waiting in the shared ring buffer.
        /// </summary>
        public int AvailableReadBytes
        {
            get
            {
                ThrowIfDisposed();
                long write = Volatile.Read(ref *WriteHeadPtr);
                long read = Volatile.Read(ref *ReadTailPtr);
                long diff = write - read;
                return diff < 0 ? 0 : (diff > _capacity ? _capacity : (int)diff);
            }
        }

        /// <summary>
        /// Gets the remaining writable space in bytes before the buffer is full.
        /// </summary>
        public int AvailableWriteBytes
        {
            get
            {
                ThrowIfDisposed();
                long write = Volatile.Read(ref *WriteHeadPtr);
                long read = Volatile.Read(ref *ReadTailPtr);
                long used = write - read;
                long free = _capacity - used;
                return free < 0 ? 0 : (int)free;
            }
        }

        /// <summary>
        /// Gets whether this ring buffer has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private SharedMemoryRingBuffer(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, byte* basePtr, int capacity, bool ownsFile)
        {
            _mmf = mmf;
            _accessor = accessor;
            _basePtr = basePtr;
            _payloadPtr = basePtr + HeaderSize;
            _capacity = capacity;
            _capacityMask = capacity - 1;
            _ownsFile = ownsFile;
        }

        /// <summary>
        /// Creates or opens a named shared memory ring buffer for cross-process communication.
        /// </summary>
        /// <param name="mapName">Unique system-wide name identifying the memory-mapped file.</param>
        /// <param name="payloadCapacity">Payload buffer size in bytes (automatically rounded up to the nearest power of two).</param>
        public static SharedMemoryRingBuffer CreateOrOpen(string mapName, int payloadCapacity = 4 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                throw new ArgumentNullException(nameof(mapName));
            if (payloadCapacity < 1024)
                throw new ArgumentOutOfRangeException(nameof(payloadCapacity), "Capacity must be at least 1,024 bytes.");

            int powerOfTwoCapacity = RoundUpToPowerOfTwo(payloadCapacity);
            long totalSize = HeaderSize + (long)powerOfTwoCapacity;

            MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen(mapName, totalSize, MemoryMappedFileAccess.ReadWrite);
            MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

            byte* basePtr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);

            if (basePtr == null)
            {
                accessor.Dispose();
                mmf.Dispose();
                throw new InvalidOperationException("Failed to acquire raw memory pointer from MemoryMappedViewAccessor.");
            }

            // Initialize header if newly created
            uint currentMagic = *(uint*)basePtr;
            if (currentMagic != MagicNumber)
            {
                *(uint*)basePtr = MagicNumber;
                *(uint*)(basePtr + 4) = ProtocolVersion;
                *(int*)(basePtr + 8) = powerOfTwoCapacity;
                *(int*)(basePtr + 12) = 0; // Reserved
                *(long*)(basePtr + 16) = 0; // WriteHead
                *(long*)(basePtr + 24) = 0; // ReadTail
            }

            int actualCapacity = *(int*)(basePtr + 8);
            return new SharedMemoryRingBuffer(mmf, accessor, basePtr, actualCapacity, true);
        }

        /// <summary>
        /// Attempts to write a discrete length-prefixed message frame into the shared buffer.
        /// Zero-allocation, thread-safe for a single writer process/thread.
        /// </summary>
        /// <param name="message">The raw message bytes to transmit.</param>
        /// <returns><c>true</c> if written successfully; <c>false</c> if insufficient buffer space.</returns>
        public bool TryWriteMessage(ReadOnlySpan<byte> message)
        {
            ThrowIfDisposed();
            int msgLength = message.Length;
            int totalBytesNeeded = msgLength + sizeof(int);

            long currentWrite = Volatile.Read(ref *WriteHeadPtr);
            long currentRead = Volatile.Read(ref *ReadTailPtr);

            if ((_capacity - (currentWrite - currentRead)) < totalBytesNeeded)
                return false;

            // 1. Write 4-byte length prefix
            WriteCircular((byte*)&msgLength, sizeof(int), currentWrite);
            currentWrite += sizeof(int);

            // 2. Write payload
            if (msgLength > 0)
            {
                fixed (byte* src = message)
                {
                    WriteCircular(src, msgLength, currentWrite);
                }
                currentWrite += msgLength;
            }

            // 3. Commit write head atomically
            Volatile.Write(ref *WriteHeadPtr, currentWrite);
            return true;
        }

        /// <summary>
        /// Attempts to read the next discrete length-prefixed message frame from the shared buffer.
        /// Zero-allocation, thread-safe for a single reader process/thread.
        /// </summary>
        /// <param name="destination">The destination buffer to receive message payload.</param>
        /// <param name="bytesRead">The actual number of bytes copied into destination.</param>
        /// <returns><c>true</c> if a message was successfully read; <c>false</c> if empty or destination too small.</returns>
        public bool TryReadMessage(Span<byte> destination, out int bytesRead)
        {
            ThrowIfDisposed();
            bytesRead = 0;

            long currentWrite = Volatile.Read(ref *WriteHeadPtr);
            long currentRead = Volatile.Read(ref *ReadTailPtr);

            long available = currentWrite - currentRead;
            if (available < sizeof(int))
                return false;

            // 1. Peek 4-byte length prefix
            int msgLength = 0;
            ReadCircular((byte*)&msgLength, sizeof(int), currentRead);

            if (msgLength < 0 || msgLength > _capacity)
            {
                // Corrupted frame header, advance read to write to recover
                Volatile.Write(ref *ReadTailPtr, currentWrite);
                return false;
            }

            if (available < (sizeof(int) + msgLength))
                return false; // Message payload is still being written

            if (destination.Length < msgLength)
                return false; // Destination buffer too small

            currentRead += sizeof(int);

            // 2. Read payload
            if (msgLength > 0)
            {
                fixed (byte* dst = destination)
                {
                    ReadCircular(dst, msgLength, currentRead);
                }
                currentRead += msgLength;
            }

            // 3. Commit read tail atomically
            Volatile.Write(ref *ReadTailPtr, currentRead);
            bytesRead = msgLength;
            return true;
        }

        /// <summary>
        /// Resets the read and write pointers back to zero.
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();
            Volatile.Write(ref *WriteHeadPtr, 0);
            Volatile.Write(ref *ReadTailPtr, 0);
        }

        /// <summary>
        /// Gets the raw unmanaged memory pointer to the payload ring buffer for custom SIMD/zero-copy operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePayloadPointer()
        {
            ThrowIfDisposed();
            return _payloadPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCircular(byte* source, int length, long absoluteOffset)
        {
            int index = (int)(absoluteOffset & _capacityMask);
            int firstChunk = _capacity - index;

            if (length <= firstChunk)
            {
                Buffer.MemoryCopy(source, _payloadPtr + index, firstChunk, length);
            }
            else
            {
                // Wrap-around: split into two writes
                Buffer.MemoryCopy(source, _payloadPtr + index, firstChunk, firstChunk);
                Buffer.MemoryCopy(source + firstChunk, _payloadPtr, _capacity, length - firstChunk);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadCircular(byte* destination, int length, long absoluteOffset)
        {
            int index = (int)(absoluteOffset & _capacityMask);
            int firstChunk = _capacity - index;

            if (length <= firstChunk)
            {
                Buffer.MemoryCopy(_payloadPtr + index, destination, length, length);
            }
            else
            {
                // Wrap-around: split into two reads
                Buffer.MemoryCopy(_payloadPtr + index, destination, length, firstChunk);
                Buffer.MemoryCopy(_payloadPtr, destination + firstChunk, length - firstChunk, length - firstChunk);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundUpToPowerOfTwo(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(SharedMemoryRingBuffer));
        }

        /// <summary>
        /// Releases all handles and mapped view accessors.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
            catch
            {
                // Suppress if already released
            }

            _accessor.Dispose();
            if (_ownsFile)
            {
                _mmf.Dispose();
            }
        }
    }
}
