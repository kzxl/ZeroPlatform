using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable CA1416 // Named MemoryMappedFile is Windows-specific in .NET BCL

namespace ZeroConcurrency.Ipc
{
    /// <summary>
    /// 192-byte (3 CPU cache lines) Shared Memory Ring Buffer Header matching ZeroRust's binary layout.
    /// Isolates metadata (offset 0), <see cref="WriteHead"/> (offset 64), and <see cref="ReadTail"/> (offset 128)
    /// onto dedicated 64-byte cache lines to eliminate false sharing across producer and consumer cores.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = SizeInBytes)]
    public struct ShmHeader
    {
        public const uint ExpectedMagic = 0x5A45524F; // "ZERO"
        public const uint ExpectedVersion = 1;
        public const int SizeInBytes = 192;
        public const int WriteHeadByteOffset = 64;
        public const int ReadTailByteOffset = 128;
        public const int DataRegionByteOffset = 192;

        // Cache Line 0 (Offsets 0..63): Static Ring Metadata
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Capacity;
        [FieldOffset(12)] public uint SlotSize;
        [FieldOffset(16)] public uint Flags;

        // Cache Line 1 (Offsets 64..127): Producer Monotonic Write Sequence
        [FieldOffset(64)] public ulong WriteHead;

        // Cache Line 2 (Offsets 128..191): Consumer Monotonic Read Sequence
        [FieldOffset(128)] public ulong ReadTail;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(uint capacity, uint slotSize)
        {
            Unsafe.InitBlockUnaligned(ref Unsafe.As<ShmHeader, byte>(ref this), 0, SizeInBytes);
            Magic = ExpectedMagic;
            Version = ExpectedVersion;
            Capacity = capacity;
            SlotSize = slotSize;
            Flags = 1; // Initialized
            Volatile.Write(ref WriteHead, 0UL);
            Volatile.Write(ref ReadTail, 0UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValid()
        {
            return Magic == ExpectedMagic
                && Version == ExpectedVersion
                && Capacity > 0
                && SlotSize > 0;
        }
    }

    /// <summary>
    /// Commanded motion setpoint payload (64 bytes) sent from ZeroPlatform (.NET) to ZeroRust.
    /// Binary-compatible with <c>zero_ipc::payloads::MotionCommandPayload</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
    public unsafe struct MotionCommandPayload : IEquatable<MotionCommandPayload>
    {
        public ulong TimestampNs;
        public uint CommandType;
        public fixed int TargetPositions[6];
        public fixed int TargetVelocities[6];
        public ushort Controlword;
        public ushort Reserved;

        public bool Equals(MotionCommandPayload other)
        {
            if (TimestampNs != other.TimestampNs ||
                CommandType != other.CommandType ||
                Controlword != other.Controlword ||
                Reserved != other.Reserved)
            {
                return false;
            }

            for (int i = 0; i < 6; i++)
            {
                if (TargetPositions[i] != other.TargetPositions[i] ||
                    TargetVelocities[i] != other.TargetVelocities[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is MotionCommandPayload other && Equals(other);

        public override int GetHashCode() => TimestampNs.GetHashCode() ^ (int)CommandType ^ Controlword;

        public static bool operator ==(MotionCommandPayload left, MotionCommandPayload right) => left.Equals(right);
        public static bool operator !=(MotionCommandPayload left, MotionCommandPayload right) => !left.Equals(right);
    }

    /// <summary>
    /// Actual machine feedback telemetry payload (80 bytes) sent from ZeroRust to ZeroPlatform (.NET).
    /// Binary-compatible with <c>zero_ipc::payloads::TelemetryPayload</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 80)]
    public unsafe struct TelemetryPayload : IEquatable<TelemetryPayload>
    {
        public ulong TimestampNs;
        public fixed int ActualPositions[6];
        public fixed int ActualVelocities[6];
        public fixed ushort Statuswords[6];
        public fixed byte DriveStates[6];
        public ushort FaultCode;
        public fixed byte Reserved[4];

        public bool Equals(TelemetryPayload other)
        {
            if (TimestampNs != other.TimestampNs || FaultCode != other.FaultCode)
                return false;

            for (int i = 0; i < 6; i++)
            {
                if (ActualPositions[i] != other.ActualPositions[i] ||
                    ActualVelocities[i] != other.ActualVelocities[i] ||
                    Statuswords[i] != other.Statuswords[i] ||
                    DriveStates[i] != other.DriveStates[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is TelemetryPayload other && Equals(other);

        public override int GetHashCode() => TimestampNs.GetHashCode() ^ FaultCode;

        public static bool operator ==(TelemetryPayload left, TelemetryPayload right) => left.Equals(right);
        public static bool operator !=(TelemetryPayload left, TelemetryPayload right) => !left.Equals(right);
    }

    /// <summary>
    /// Cross-tier lock-free Single-Producer Single-Consumer (SPSC) shared memory ring buffer
    /// matching ZeroRust's 192-byte cache-line isolated <see cref="ShmHeader"/> binary layout.
    /// Supports both <see cref="MemoryMappedFile"/> and raw unmanaged memory buffers for sub-200ns
    /// zero-GC struct exchange between .NET and Rust RTOS/edge nodes.
    /// </summary>
    /// <typeparam name="T">The unmanaged payload struct type.</typeparam>
    public sealed unsafe class ZeroRustShmRingBuffer<T> : IDisposable where T : unmanaged
    {
        public const uint MagicConstant = ShmHeader.ExpectedMagic;
        public const uint VersionConstant = ShmHeader.ExpectedVersion;
        public const int HeaderSize = ShmHeader.SizeInBytes; // 192 bytes
        public const int WriteHeadOffset = ShmHeader.WriteHeadByteOffset; // 64
        public const int ReadTailOffset = ShmHeader.ReadTailByteOffset; // 128
        public const int DataOffset = ShmHeader.DataRegionByteOffset; // 192

        private readonly MemoryMappedFile? _mmf;
        private readonly MemoryMappedViewAccessor? _accessor;
        private byte* _basePointer;
        private byte* _dataPointer;
        private ShmHeader* _header;
        private readonly ulong _capacity;
        private readonly ulong _mask;
        private readonly bool _isPowerOfTwo;
        private readonly int _slotSize;
        private readonly bool _ownsMmf;
        private readonly bool _ownsUnmanagedMemory;
        private int _disposed;

        /// <summary>
        /// Gets the total slot capacity of the ring buffer.
        /// </summary>
        public int Capacity => (int)_capacity;

        /// <summary>
        /// Gets the byte size of each slot in the ring buffer.
        /// </summary>
        public int SlotSize => _slotSize;

        /// <summary>
        /// Gets the base unmanaged pointer of the shared memory region (pointing to the 192-byte <see cref="ShmHeader"/>).
        /// </summary>
        public IntPtr BasePointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return (IntPtr)_basePointer;
            }
        }

        /// <summary>
        /// Gets a direct reference to the underlying 192-byte <see cref="ShmHeader"/>.
        /// </summary>
        public ref ShmHeader Header
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return ref *_header;
            }
        }

        /// <summary>
        /// Gets the current monotonic write head sequence.
        /// </summary>
        public ulong WriteHead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return Volatile.Read(ref _header->WriteHead);
            }
        }

        /// <summary>
        /// Gets the current monotonic read tail sequence.
        /// </summary>
        public ulong ReadTail
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return Volatile.Read(ref _header->ReadTail);
            }
        }

        /// <summary>
        /// Gets the number of unread items currently in the ring buffer.
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                ulong head = Volatile.Read(ref _header->WriteHead);
                ulong tail = Volatile.Read(ref _header->ReadTail);
                return head > tail ? (int)(head - tail) : 0;
            }
        }

        /// <summary>
        /// Gets whether the ring buffer has no unread items.
        /// </summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == 0;
        }

        /// <summary>
        /// Gets whether this ring buffer instance has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new standalone off-heap unmanaged <see cref="ZeroRustShmRingBuffer{T}"/>.
        /// </summary>
        /// <param name="capacity">Number of slots in the ring buffer.</param>
        /// <param name="slotSize">Optional explicit slot byte size (defaults to <c>sizeof(T)</c> rounded up to 8-byte alignment).</param>
        public ZeroRustShmRingBuffer(int capacity, int slotSize = 0)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            int resolvedSlotSize = ResolveSlotSize(slotSize);
            long totalBytes = HeaderSize + ((long)capacity * resolvedSlotSize);

#if NET8_0_OR_GREATER
            byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)totalBytes);
#else
            byte* ptr = (byte*)Marshal.AllocHGlobal((IntPtr)totalBytes);
            Unsafe.InitBlockUnaligned(ptr, 0, (uint)totalBytes);
#endif
            _ownsUnmanagedMemory = true;
            _basePointer = ptr;
            _dataPointer = ptr + DataOffset;
            _header = (ShmHeader*)ptr;
            _header->Initialize((uint)capacity, (uint)resolvedSlotSize);

            _capacity = (ulong)capacity;
            _isPowerOfTwo = (capacity & (capacity - 1)) == 0;
            _mask = _capacity - 1;
            _slotSize = resolvedSlotSize;
        }

        private ZeroRustShmRingBuffer(
            MemoryMappedFile? mmf,
            MemoryMappedViewAccessor? accessor,
            byte* basePointer,
            int capacity,
            int slotSize,
            bool ownsMmf,
            bool ownsUnmanagedMemory)
        {
            _mmf = mmf;
            _accessor = accessor;
            _basePointer = basePointer;
            _dataPointer = basePointer + DataOffset;
            _header = (ShmHeader*)basePointer;
            _capacity = (ulong)capacity;
            _isPowerOfTwo = (capacity & (capacity - 1)) == 0;
            _mask = _capacity - 1;
            _slotSize = slotSize;
            _ownsMmf = ownsMmf;
            _ownsUnmanagedMemory = ownsUnmanagedMemory;
        }

        private static int ResolveSlotSize(int requestedSlotSize)
        {
            int minSize = sizeof(T);
            int actual = requestedSlotSize > 0 ? requestedSlotSize : minSize;
            if (actual < minSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedSlotSize),
                    $"Slot size ({actual} bytes) cannot be smaller than sizeof({typeof(T).Name}) ({minSize} bytes).");
            }
            return actual;
        }

        /// <summary>
        /// Initializes or attaches a <see cref="ZeroRustShmRingBuffer{T}"/> over an external unmanaged memory buffer.
        /// </summary>
        public static ZeroRustShmRingBuffer<T> CreateFromPointer(
            IntPtr basePointer,
            int bufferLength,
            int capacity,
            int slotSize = 0,
            bool initialize = true)
        {
            if (basePointer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(basePointer));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            int resolvedSlotSize = ResolveSlotSize(slotSize);
            long requiredBytes = HeaderSize + ((long)capacity * resolvedSlotSize);
            if (bufferLength < requiredBytes)
                throw new ArgumentOutOfRangeException(nameof(bufferLength), $"Buffer length ({bufferLength}) is smaller than required {requiredBytes} bytes.");

            byte* ptr = (byte*)basePointer;
            ShmHeader* header = (ShmHeader*)ptr;

            if (initialize)
            {
                header->Initialize((uint)capacity, (uint)resolvedSlotSize);
            }
            else if (!header->IsValid() || header->SlotSize < (uint)sizeof(T))
            {
                throw new InvalidOperationException("Target unmanaged memory buffer does not contain a valid ZeroRust ShmHeader.");
            }

            return new ZeroRustShmRingBuffer<T>(
                mmf: null,
                accessor: null,
                basePointer: ptr,
                capacity: (int)header->Capacity,
                slotSize: (int)header->SlotSize,
                ownsMmf: false,
                ownsUnmanagedMemory: false);
        }

        /// <summary>
        /// Attaches to an already-initialized shared memory buffer in unmanaged memory.
        /// </summary>
        public static ZeroRustShmRingBuffer<T> AttachToPointer(IntPtr basePointer, int bufferLength)
        {
            if (basePointer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(basePointer));
            if (bufferLength < HeaderSize)
                throw new ArgumentOutOfRangeException(nameof(bufferLength), $"Buffer must be at least {HeaderSize} bytes.");

            byte* ptr = (byte*)basePointer;
            ShmHeader* header = (ShmHeader*)ptr;

            if (!header->IsValid() || header->SlotSize < (uint)sizeof(T))
            {
                throw new InvalidOperationException("Target unmanaged memory buffer does not contain a valid ZeroRust ShmHeader.");
            }

            long requiredBytes = HeaderSize + ((long)header->Capacity * header->SlotSize);
            if (bufferLength < requiredBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferLength), $"Buffer length ({bufferLength}) is smaller than required {requiredBytes} bytes.");
            }

            return new ZeroRustShmRingBuffer<T>(
                mmf: null,
                accessor: null,
                basePointer: ptr,
                capacity: (int)header->Capacity,
                slotSize: (int)header->SlotSize,
                ownsMmf: false,
                ownsUnmanagedMemory: false);
        }

        /// <summary>
        /// Creates a new OS-backed <see cref="MemoryMappedFile"/> shared memory ring buffer or opens an existing one.
        /// </summary>
        public static ZeroRustShmRingBuffer<T> CreateOrOpen(string mapName, int capacity, int slotSize = 0)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            int resolvedSlotSize = ResolveSlotSize(slotSize);
            long totalBytes = HeaderSize + ((long)capacity * resolvedSlotSize);

            var mmf = MemoryMappedFile.CreateOrOpen(mapName, totalBytes, MemoryMappedFileAccess.ReadWrite);
            var accessor = mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            ShmHeader* header = (ShmHeader*)ptr;

            if (Interlocked.CompareExchange(
                    ref Unsafe.As<uint, int>(ref header->Magic),
                    (int)MagicConstant,
                    0) == 0)
            {
                header->Version = VersionConstant;
                header->Capacity = (uint)capacity;
                header->SlotSize = (uint)resolvedSlotSize;
                header->Flags = 1;
                Volatile.Write(ref header->WriteHead, 0UL);
                Volatile.Write(ref header->ReadTail, 0UL);
            }
            else if (!header->IsValid() || header->SlotSize < (uint)sizeof(T))
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                accessor.Dispose();
                mmf.Dispose();
                throw new InvalidOperationException("Existing shared memory file does not have a valid ZeroRust ShmHeader.");
            }

            return new ZeroRustShmRingBuffer<T>(
                mmf: mmf,
                accessor: accessor,
                basePointer: ptr,
                capacity: (int)header->Capacity,
                slotSize: (int)header->SlotSize,
                ownsMmf: true,
                ownsUnmanagedMemory: false);
        }

        /// <summary>
        /// Opens an existing OS-backed <see cref="MemoryMappedFile"/> shared memory ring buffer.
        /// </summary>
        public static ZeroRustShmRingBuffer<T> OpenExisting(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                throw new ArgumentNullException(nameof(mapName));

            var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            var accessor = mmf.CreateViewAccessor();

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            ShmHeader* header = (ShmHeader*)ptr;

            if (!header->IsValid() || header->SlotSize < (uint)sizeof(T))
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                accessor.Dispose();
                mmf.Dispose();
                throw new InvalidOperationException("Target shared memory file does not have a valid ZeroRust ShmHeader.");
            }

            return new ZeroRustShmRingBuffer<T>(
                mmf: mmf,
                accessor: accessor,
                basePointer: ptr,
                capacity: (int)header->Capacity,
                slotSize: (int)header->SlotSize,
                ownsMmf: true,
                ownsUnmanagedMemory: false);
        }

        /// <summary>
        /// Publishes a structured payload into the next available slot without locks or heap allocations.
        /// Matches <c>zero_ipc::ring::ShmRingBuffer::push</c> memory ordering semantics.
        /// </summary>
        /// <param name="item">The unmanaged struct payload to publish.</param>
        /// <returns><c>true</c> if published; <c>false</c> if the ring buffer is full.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPublish(in T item)
        {
            ThrowIfDisposed();

            ulong head = _header->WriteHead;
            ulong tail = Volatile.Read(ref _header->ReadTail);

            if (head - tail >= _capacity)
            {
                return false;
            }

            ulong slotIndex = _isPowerOfTwo ? (head & _mask) : (head % _capacity);
            byte* destPtr = _dataPointer + ((long)slotIndex * _slotSize);
            Unsafe.WriteUnaligned(destPtr, item);

            Volatile.Write(ref _header->WriteHead, head + 1);
            return true;
        }

        /// <summary>
        /// Alias for <see cref="TryPublish(in T)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(in T item) => TryPublish(in item);

        /// <summary>
        /// Consumes the next structured payload from the ring buffer without locks or heap allocations.
        /// Matches <c>zero_ipc::ring::ShmRingBuffer::pop</c> memory ordering semantics.
        /// </summary>
        /// <param name="item">Receives the dequeued unmanaged struct payload.</param>
        /// <returns><c>true</c> if an item was consumed; <c>false</c> if the ring buffer is empty.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsume(out T item)
        {
            ThrowIfDisposed();

            ulong tail = _header->ReadTail;
            ulong head = Volatile.Read(ref _header->WriteHead);

            if (tail >= head)
            {
                item = default;
                return false;
            }

            ulong slotIndex = _isPowerOfTwo ? (tail & _mask) : (tail % _capacity);
            byte* srcPtr = _dataPointer + ((long)slotIndex * _slotSize);
            item = Unsafe.ReadUnaligned<T>(srcPtr);

            Volatile.Write(ref _header->ReadTail, tail + 1);
            return true;
        }

        /// <summary>
        /// Alias for <see cref="TryConsume(out T)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out T item) => TryConsume(out item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ZeroRustShmRingBuffer<T>));
        }

        /// <summary>
        /// Releases unmanaged view pointers, memory-mapped handles, or owned off-heap buffers.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_accessor != null)
            {
                if (_basePointer != null)
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _basePointer = null;
                    _dataPointer = null;
                    _header = null;
                }
                _accessor.Dispose();
            }
            else if (_ownsUnmanagedMemory && _basePointer != null)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_basePointer);
#else
                Marshal.FreeHGlobal((IntPtr)_basePointer);
#endif
                _basePointer = null;
                _dataPointer = null;
                _header = null;
            }
            else
            {
                _basePointer = null;
                _dataPointer = null;
                _header = null;
            }

            if (_ownsMmf && _mmf != null)
            {
                _mmf.Dispose();
            }
        }
    }
}
