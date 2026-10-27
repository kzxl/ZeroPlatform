using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable CA1416 // Named MemoryMappedFile is Windows-specific in .NET BCL

namespace ZeroConcurrency.Ipc
{
    /// <summary>
    /// Pure C# ultra-low-latency, zero-copy lock-free Single-Producer Single-Consumer (SPSC) ring buffer
    /// backed by an OS-level <see cref="MemoryMappedFile"/>.
    /// Enables sub-microsecond IPC frame/tensor transfers between independent processes (e.g. C# and Python AI/Vision)
    /// without TCP sockets, named pipes, or intermediate kernel copies.
    /// </summary>
    public sealed unsafe class ZeroMmfRingBuffer : IDisposable
    {
        private const long MagicConstant = 0x5A45524F49504331L; // "ZEROIPC1"
        private const int HeaderSize = 64;

        [StructLayout(LayoutKind.Explicit, Size = HeaderSize)]
        private struct MmfHeader
        {
            [FieldOffset(0)] public long Magic;
            [FieldOffset(8)] public int Capacity;
            [FieldOffset(12)] public int SlotSize;
            [FieldOffset(16)] public long Tail;
            [FieldOffset(24)] public long Head;
        }

        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private byte* _basePointer;
        private MmfHeader* _header;
        private readonly bool _ownsMmf;
        private int _disposed;

        /// <summary>
        /// Gets the total slot capacity of the ring buffer.
        /// </summary>
        public int Capacity
        {
            get
            {
                ThrowIfDisposed();
                return _header->Capacity;
            }
        }

        /// <summary>
        /// Gets the maximum byte payload size supported per slot.
        /// </summary>
        public int MaxPayloadSize
        {
            get
            {
                ThrowIfDisposed();
                return _header->SlotSize - sizeof(int);
            }
        }

        /// <summary>
        /// Gets the number of unread slots currently in the buffer.
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                long tail = Volatile.Read(ref _header->Tail);
                long head = Volatile.Read(ref _header->Head);
                long diff = tail - head;
                return diff > 0 ? (int)diff : 0;
            }
        }

        /// <summary>
        /// Gets whether this buffer has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private ZeroMmfRingBuffer(MemoryMappedFile mmf, bool ownsMmf)
        {
            _mmf = mmf;
            _ownsMmf = ownsMmf;
            _accessor = _mmf.CreateViewAccessor();

            byte* ptr = null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            _basePointer = ptr;
            _header = (MmfHeader*)ptr;
        }

        /// <summary>
        /// Creates a new shared memory ring buffer or opens an existing one.
        /// </summary>
        /// <param name="mapName">Globally unique system map name.</param>
        /// <param name="capacity">Number of slots (must be a positive power of two).</param>
        /// <param name="slotSize">Size in bytes for each slot (must accommodate payload + 4 bytes header).</param>
        public static ZeroMmfRingBuffer CreateOrOpen(string mapName, int capacity, int slotSize)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));
            if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
                throw new ArgumentException("Capacity must be a positive power of two.", nameof(capacity));
            if (slotSize <= sizeof(int))
                throw new ArgumentException($"SlotSize must be greater than {sizeof(int)} bytes.", nameof(slotSize));

            long totalBytes = HeaderSize + ((long)capacity * slotSize);
            var mmf = MemoryMappedFile.CreateOrOpen(mapName, totalBytes, MemoryMappedFileAccess.ReadWrite);

            var buffer = new ZeroMmfRingBuffer(mmf, ownsMmf: true);

            // Initialize header if newly created
            if (Interlocked.CompareExchange(ref buffer._header->Magic, MagicConstant, 0) == 0)
            {
                buffer._header->Capacity = capacity;
                buffer._header->SlotSize = slotSize;
                Volatile.Write(ref buffer._header->Tail, 0);
                Volatile.Write(ref buffer._header->Head, 0);
            }
            else if (buffer._header->Magic != MagicConstant)
            {
                buffer.Dispose();
                throw new InvalidOperationException("The existing shared memory file does not have a valid ZeroIPC header.");
            }

            return buffer;
        }

        /// <summary>
        /// Opens an existing shared memory ring buffer.
        /// </summary>
        /// <param name="mapName">Globally unique system map name.</param>
        public static ZeroMmfRingBuffer OpenExisting(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));

            var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            var buffer = new ZeroMmfRingBuffer(mmf, ownsMmf: true);

            if (buffer._header->Magic != MagicConstant)
            {
                buffer.Dispose();
                throw new InvalidOperationException("The target shared memory file does not have a valid ZeroIPC header.");
            }

            return buffer;
        }

        /// <summary>
        /// Attempts to write a payload into the shared memory ring buffer without heap allocations.
        /// </summary>
        /// <param name="payload">Payload byte span.</param>
        /// <returns><c>true</c> if written successfully; <c>false</c> if the buffer is full (backpressure).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(ReadOnlySpan<byte> payload)
        {
            ThrowIfDisposed();

            int maxPayload = MaxPayloadSize;
            if (payload.Length > maxPayload)
                throw new ArgumentOutOfRangeException(nameof(payload), $"Payload size ({payload.Length} bytes) exceeds maximum slot payload ({maxPayload} bytes).");

            long tail = Volatile.Read(ref _header->Tail);
            long head = Volatile.Read(ref _header->Head);

            if (tail - head >= _header->Capacity)
                return false; // Buffer is full

            int slotIndex = (int)(tail & (_header->Capacity - 1));
            byte* slotPtr = _basePointer + HeaderSize + ((long)slotIndex * _header->SlotSize);

            // Write length prefix
            *(int*)slotPtr = payload.Length;

            // Write payload data
            if (payload.Length > 0)
            {
                var destSpan = new Span<byte>(slotPtr + sizeof(int), payload.Length);
                payload.CopyTo(destSpan);
            }

            // Publish write monotonically
            Volatile.Write(ref _header->Tail, tail + 1);
            return true;
        }

        /// <summary>
        /// Attempts to read the next available payload from the shared memory ring buffer.
        /// </summary>
        /// <param name="destination">Destination span to receive payload data.</param>
        /// <param name="bytesRead">Number of bytes read.</param>
        /// <returns><c>true</c> if a payload was read; <c>false</c> if the buffer was empty.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(Span<byte> destination, out int bytesRead)
        {
            ThrowIfDisposed();

            long head = Volatile.Read(ref _header->Head);
            long tail = Volatile.Read(ref _header->Tail);

            if (head >= tail)
            {
                bytesRead = 0;
                return false; // Buffer is empty
            }

            int slotIndex = (int)(head & (_header->Capacity - 1));
            byte* slotPtr = _basePointer + HeaderSize + ((long)slotIndex * _header->SlotSize);

            int payloadLength = *(int*)slotPtr;
            if (payloadLength < 0 || payloadLength > destination.Length)
            {
                throw new InvalidOperationException($"Destination buffer is too small ({destination.Length} bytes) for payload ({payloadLength} bytes).");
            }

            if (payloadLength > 0)
            {
                var srcSpan = new ReadOnlySpan<byte>(slotPtr + sizeof(int), payloadLength);
                srcSpan.CopyTo(destination);
            }

            bytesRead = payloadLength;

            // Publish read monotonically
            Volatile.Write(ref _header->Head, head + 1);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ZeroMmfRingBuffer), "The shared memory ring buffer has already been disposed.");
        }

        /// <summary>
        /// Releases all unmanaged view pointers and closes the underlying memory-mapped file.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_basePointer != null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _basePointer = null;
                _header = null;
            }

            _accessor.Dispose();

            if (_ownsMmf)
            {
                _mmf.Dispose();
            }
        }
    }
}
