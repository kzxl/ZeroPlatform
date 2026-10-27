using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable CA1416 // Named MemoryMappedFile and EventWaitHandle are platform-specific in .NET BCL

namespace ZeroConcurrency.Ipc
{
    /// <summary>
    /// Pure C# ultra-low-latency, zero-copy lock-free Multi-Producer Multi-Consumer (MPMC) queue
    /// backed by an OS-level <see cref="MemoryMappedFile"/> (either named shared memory or durable file-backed).
    /// <para>
    /// Implements Dmitry Vyukov's bounded MPMC queue algorithm mapped directly over unmanaged shared memory,
    /// enabling concurrent reads and writes across multiple threads or independent operating system processes
    /// with zero GC heap allocations and sub-microsecond latency.
    /// </para>
    /// </summary>
    public sealed unsafe class ZeroMmfMpmcQueue : IDisposable
    {
        private const long MagicInitializing = 0x5A4D504D435130L; // "ZMPMCQ0"
        private const long MagicReady = 0x5A4D504D435131L;        // "ZMPMCQ1"
        private const int CurrentVersion = 1;
        private const int HeaderSize = 256;

        [StructLayout(LayoutKind.Explicit, Size = HeaderSize)]
        private struct MpmcQueueHeader
        {
            [FieldOffset(0)]   public long Magic;
            [FieldOffset(8)]   public int  Version;
            [FieldOffset(12)]  public int  Capacity;
            [FieldOffset(16)]  public int  SlotSize;
            [FieldOffset(20)]  public int  MaxPayloadSize;
            [FieldOffset(24)]  public long TotalFileSize;

            // Offset 64: Enqueue cache-line padding (64 bytes aligned)
            [FieldOffset(64)]  public long EnqueuePos;

            // Offset 128: Dequeue cache-line padding (64 bytes aligned)
            [FieldOffset(128)] public long DequeuePos;

            // Offset 192: Reserved for future metrics or flags
            [FieldOffset(192)] public long Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct MmfCellHeader
        {
            [FieldOffset(0)]  public long   Sequence;
            [FieldOffset(8)]  public int    PayloadLength;
            [FieldOffset(12)] public ushort TypeId;
            [FieldOffset(14)] public ushort Flags;
        }

        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly FileStream? _fileStream;
        private readonly EventWaitHandle? _signalEvent;
        private readonly bool _ownsMmf;

        private byte* _basePointer;
        private MpmcQueueHeader* _header;
        private int _disposed;

        /// <summary>
        /// Gets the total slot capacity of the queue (always a positive power of two).
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
                return _header->MaxPayloadSize;
            }
        }

        /// <summary>
        /// Gets the total byte size allocated for each individual slot (including cell header).
        /// </summary>
        public int SlotSize
        {
            get
            {
                ThrowIfDisposed();
                return _header->SlotSize;
            }
        }

        /// <summary>
        /// Gets an approximate snapshot count of unread items currently in the queue.
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                long enq = Volatile.Read(ref _header->EnqueuePos);
                long deq = Volatile.Read(ref _header->DequeuePos);
                long diff = enq - deq;
                if (diff < 0) return 0;
                int cap = _header->Capacity;
                return diff > cap ? cap : (int)diff;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the queue is currently empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                ThrowIfDisposed();
                return Volatile.Read(ref _header->DequeuePos) >= Volatile.Read(ref _header->EnqueuePos);
            }
        }

        /// <summary>
        /// Gets whether this queue instance has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private ZeroMmfMpmcQueue(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, byte* basePointer, EventWaitHandle? signalEvent, bool ownsMmf, FileStream? fileStream = null)
        {
            _mmf = mmf;
            _accessor = accessor;
            _basePointer = basePointer;
            _header = (MpmcQueueHeader*)basePointer;
            _signalEvent = signalEvent;
            _ownsMmf = ownsMmf;
            _fileStream = fileStream;
        }

        /// <summary>
        /// Creates a new named shared memory MPMC queue or opens an existing one for IPC.
        /// </summary>
        /// <param name="mapName">Globally unique system map name.</param>
        /// <param name="capacity">Number of slots (must be a positive power of two, e.g. 1024, 4096).</param>
        /// <param name="slotSize">Total size in bytes for each slot (must be greater than 16 bytes).</param>
        /// <param name="enableSignal">Whether to enable cross-process EventWaitHandle signaling for zero-CPU idling.</param>
        public static ZeroMmfMpmcQueue CreateOrOpen(string mapName, int capacity, int slotSize, bool enableSignal = true)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));
            ValidateCapacityAndSlotSize(capacity, slotSize);

            long totalBytes = HeaderSize + ((long)capacity * slotSize);
            var mmf = MemoryMappedFile.CreateOrOpen(mapName, totalBytes, MemoryMappedFileAccess.ReadWrite);
            var accessor = mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            EventWaitHandle? signalEvent = null;
            if (enableSignal)
            {
                try
                {
                    signalEvent = new EventWaitHandle(false, EventResetMode.AutoReset, mapName + "_sig");
                }
                catch
                {
                    signalEvent = new AutoResetEvent(false);
                }
            }

            var queue = new ZeroMmfMpmcQueue(mmf, accessor, ptr, signalEvent, ownsMmf: true);
            queue.InitializeOrWaitForHeader(capacity, slotSize, totalBytes);
            return queue;
        }

        /// <summary>
        /// Creates or opens a durable disk-backed MPMC queue using a file stream.
        /// Changes are persisted to the filesystem, surviving process crashes and restarts.
        /// </summary>
        /// <param name="filePath">Target file path on disk.</param>
        /// <param name="capacity">Number of slots (must be a positive power of two).</param>
        /// <param name="slotSize">Total size in bytes for each slot (must be greater than 16 bytes).</param>
        /// <param name="enableSignal">Whether to enable event signaling for zero-CPU idling.</param>
        public static ZeroMmfMpmcQueue CreateFromFile(string filePath, int capacity, int slotSize, bool enableSignal = true)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            ValidateCapacityAndSlotSize(capacity, slotSize);

            long totalBytes = HeaderSize + ((long)capacity * slotSize);
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (fileStream.Length < totalBytes)
            {
                fileStream.SetLength(totalBytes);
            }

            var mmf = MemoryMappedFile.CreateFromFile(fileStream, null, totalBytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
            var accessor = mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            EventWaitHandle? signalEvent = enableSignal ? new AutoResetEvent(false) : null;
            var queue = new ZeroMmfMpmcQueue(mmf, accessor, ptr, signalEvent, ownsMmf: true, fileStream: fileStream);
            queue.InitializeOrWaitForHeader(capacity, slotSize, totalBytes);
            return queue;
        }

        /// <summary>
        /// Opens an existing named shared memory queue for reading or writing.
        /// </summary>
        /// <param name="mapName">Globally unique system map name.</param>
        /// <param name="enableSignal">Whether to open the associated cross-process EventWaitHandle.</param>
        public static ZeroMmfMpmcQueue OpenExisting(string mapName, bool enableSignal = true)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));

            var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            var accessor = mmf.CreateViewAccessor();

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            EventWaitHandle? signalEvent = null;
            if (enableSignal)
            {
                try
                {
                    signalEvent = EventWaitHandle.OpenExisting(mapName + "_sig");
                }
                catch
                {
                    signalEvent = null;
                }
            }

            var queue = new ZeroMmfMpmcQueue(mmf, accessor, ptr, signalEvent, ownsMmf: true);
            queue.WaitForExistingHeader();
            return queue;
        }

        private void InitializeOrWaitForHeader(int capacity, int slotSize, long totalBytes)
        {
            long currentMagic = Volatile.Read(ref _header->Magic);

            if (currentMagic == 0)
            {
                // Attempt to become the initializer
                if (Interlocked.CompareExchange(ref _header->Magic, MagicInitializing, 0) == 0)
                {
                    _header->Version = CurrentVersion;
                    _header->Capacity = capacity;
                    _header->SlotSize = slotSize;
                    _header->MaxPayloadSize = slotSize - sizeof(MmfCellHeader);
                    _header->TotalFileSize = totalBytes;
                    Volatile.Write(ref _header->EnqueuePos, 0);
                    Volatile.Write(ref _header->DequeuePos, 0);

                    // Initialize all cell sequences to their initial slot index
                    for (int i = 0; i < capacity; i++)
                    {
                        byte* slotPtr = _basePointer + HeaderSize + ((long)i * slotSize);
                        MmfCellHeader* cell = (MmfCellHeader*)slotPtr;
                        cell->Sequence = i;
                        cell->PayloadLength = 0;
                        cell->TypeId = 0;
                        cell->Flags = 0;
                    }

                    // Publish ready state
                    Volatile.Write(ref _header->Magic, MagicReady);
                    return;
                }
            }

            // If another thread/process is currently initializing, spin-wait until ready
            WaitForExistingHeader();
        }

        private void WaitForExistingHeader()
        {
            var spinner = new SpinWait();
            while (true)
            {
                long magic = Volatile.Read(ref _header->Magic);
                if (magic == MagicReady)
                {
                    return;
                }
                if (magic != MagicInitializing && magic != 0)
                {
                    throw new InvalidOperationException($"Invalid or corrupt queue magic header: 0x{magic:X16}.");
                }
                spinner.SpinOnce();
            }
        }

        private static void ValidateCapacityAndSlotSize(int capacity, int slotSize)
        {
            if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
                throw new ArgumentException($"Capacity ({capacity}) must be a positive power of two.", nameof(capacity));
            if (slotSize <= sizeof(MmfCellHeader))
                throw new ArgumentException($"SlotSize ({slotSize} bytes) must be greater than cell header size ({sizeof(MmfCellHeader)} bytes).", nameof(slotSize));
        }

        /// <summary>
        /// Attempts to enqueue a payload into the lock-free shared memory queue.
        /// </summary>
        /// <param name="payload">Payload byte span.</param>
        /// <param name="typeId">Optional message type identifier.</param>
        /// <param name="flags">Optional message flags.</param>
        /// <returns><c>true</c> if successfully enqueued; <c>false</c> if the buffer is full.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(ReadOnlySpan<byte> payload, ushort typeId = 0, ushort flags = 0)
        {
            ThrowIfDisposed();

            int maxPayload = _header->MaxPayloadSize;
            if (payload.Length > maxPayload)
                throw new ArgumentOutOfRangeException(nameof(payload), $"Payload size ({payload.Length} bytes) exceeds maximum slot payload ({maxPayload} bytes).");

            int mask = _header->Capacity - 1;
            int slotSize = _header->SlotSize;

            while (true)
            {
                long pos = Volatile.Read(ref _header->EnqueuePos);
                int index = (int)(pos & mask);
                byte* slotPtr = _basePointer + HeaderSize + ((long)index * slotSize);
                MmfCellHeader* cell = (MmfCellHeader*)slotPtr;

                long seq = Volatile.Read(ref cell->Sequence);
                long dif = seq - pos;

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref _header->EnqueuePos, pos + 1, pos) == pos)
                    {
                        // Successfully claimed slot for writing
                        if (payload.Length > 0)
                        {
                            fixed (byte* pSrc = payload)
                            {
                                Buffer.MemoryCopy(pSrc, slotPtr + sizeof(MmfCellHeader), maxPayload, payload.Length);
                            }
                        }

                        cell->PayloadLength = payload.Length;
                        cell->TypeId = typeId;
                        cell->Flags = flags;

                        // Publish sequence to signal consumer readiness
                        Volatile.Write(ref cell->Sequence, pos + 1);

                        _signalEvent?.Set();
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    // Buffer is full (producer wrap-around caught up to consumer)
                    return false;
                }
                else
                {
                    // Another producer is writing, spin briefly
                    Thread.SpinWait(1);
                }
            }
        }

        /// <summary>
        /// Attempts to enqueue a payload, spinning and waiting with a timeout if full.
        /// </summary>
        public bool TryEnqueue(ReadOnlySpan<byte> payload, ushort typeId, ushort flags, int timeoutMs)
        {
            if (TryEnqueue(payload, typeId, flags)) return true;
            if (timeoutMs <= 0) return false;

            long start = GetTimestampMs();
            var spinner = new SpinWait();

            while (GetTimestampMs() - start < timeoutMs)
            {
                if (TryEnqueue(payload, typeId, flags)) return true;
                spinner.SpinOnce();
            }

            return false;
        }

        /// <summary>
        /// Attempts to dequeue an item from the lock-free shared memory queue.
        /// </summary>
        /// <param name="destination">Destination span to receive payload data.</param>
        /// <param name="bytesRead">Number of bytes written into <paramref name="destination"/>.</param>
        /// <param name="typeId">Message type identifier of the dequeued item.</param>
        /// <param name="flags">Message flags of the dequeued item.</param>
        /// <returns><c>true</c> if successfully dequeued; <c>false</c> if the queue was empty.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(Span<byte> destination, out int bytesRead, out ushort typeId, out ushort flags)
        {
            ThrowIfDisposed();

            int mask = _header->Capacity - 1;
            int slotSize = _header->SlotSize;

            while (true)
            {
                long pos = Volatile.Read(ref _header->DequeuePos);
                int index = (int)(pos & mask);
                byte* slotPtr = _basePointer + HeaderSize + ((long)index * slotSize);
                MmfCellHeader* cell = (MmfCellHeader*)slotPtr;

                long seq = Volatile.Read(ref cell->Sequence);
                long dif = seq - (pos + 1);

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref _header->DequeuePos, pos + 1, pos) == pos)
                    {
                        // Successfully claimed slot for reading
                        int len = cell->PayloadLength;
                        if (len > destination.Length)
                        {
                            throw new ArgumentException($"Destination buffer length ({destination.Length}) is too small for payload ({len} bytes).", nameof(destination));
                        }

                        if (len > 0)
                        {
                            fixed (byte* pDst = destination)
                            {
                                Buffer.MemoryCopy(slotPtr + sizeof(MmfCellHeader), pDst, destination.Length, len);
                            }
                        }

                        bytesRead = len;
                        typeId = cell->TypeId;
                        flags = cell->Flags;

                        // Advance sequence to pos + Capacity to mark slot free for the next wrap-around
                        Volatile.Write(ref cell->Sequence, pos + _header->Capacity);
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    // Buffer is empty
                    bytesRead = 0;
                    typeId = 0;
                    flags = 0;
                    return false;
                }
                else
                {
                    // Another consumer is reading, spin briefly
                    Thread.SpinWait(1);
                }
            }
        }

        /// <summary>
        /// Attempts to dequeue an item from the queue without inspecting metadata.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(Span<byte> destination, out int bytesRead)
        {
            return TryDequeue(destination, out bytesRead, out _, out _);
        }

        /// <summary>
        /// Attempts to dequeue an item, waiting with hybrid spin-then-event wait if empty.
        /// Provides sub-microsecond response on immediate messages and 0% CPU consumption during idle periods.
        /// </summary>
        public bool TryDequeue(Span<byte> destination, out int bytesRead, out ushort typeId, out ushort flags, int timeoutMs)
        {
            if (TryDequeue(destination, out bytesRead, out typeId, out flags))
                return true;

            if (timeoutMs <= 0) return false;

            // Phase 1: Brief low-latency spin wait (up to 50 iterations)
            for (int i = 0; i < 50; i++)
            {
                Thread.SpinWait(4);
                if (TryDequeue(destination, out bytesRead, out typeId, out flags))
                    return true;
            }

            // Phase 2: OS event wait if event handle is configured
            long start = GetTimestampMs();
            while (true)
            {
                long elapsed = GetTimestampMs() - start;
                int remaining = timeoutMs - (int)elapsed;
                if (remaining <= 0) break;

                if (_signalEvent != null)
                {
                    _signalEvent.WaitOne(remaining);
                }
                else
                {
                    Thread.Sleep(1);
                }

                if (TryDequeue(destination, out bytesRead, out typeId, out flags))
                    return true;
            }

            bytesRead = 0;
            typeId = 0;
            flags = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetTimestampMs()
        {
            return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        /// <summary>
        /// Flushes all pending memory-mapped buffers to the underlying storage device.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _accessor.Flush();
            _fileStream?.Flush(flushToDisk: true);
        }

        /// <summary>
        /// Clears all elements currently stored in the queue.
        /// </summary>
        public void Clear()
        {
            byte[] scratch = new byte[32];
            while (TryDequeue(scratch, out _)) { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ZeroMmfMpmcQueue), "The shared memory queue has already been disposed.");
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

            _fileStream?.Dispose();
            _signalEvent?.Dispose();
        }
    }
}
