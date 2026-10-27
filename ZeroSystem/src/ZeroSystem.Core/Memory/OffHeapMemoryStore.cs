using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroSystem.Memory;

/// <summary>
/// Sovereign high-performance Off-Heap GC-Free Memory Store.
/// Allocates contiguous unmanaged virtual memory directly from the operating system kernel,
/// completely isolated from .NET Garbage Collector heap compaction and Gen-2 pauses.
/// Optionally locks virtual memory into physical RAM via VirtualLock to eliminate page faults.
/// </summary>
public sealed unsafe class OffHeapMemoryStore : IDisposable
{
    private readonly int _slotSize;
    private readonly int _capacity;
    private readonly long _totalBytes;
    private readonly bool _pinToPhysicalRam;

    private byte* _memoryPtr;
    private readonly uint[] _slotVersions;
    private readonly int[] _freeSlots;
    private int _freeHead;
    private int _freeLock;
    private int _allocatedCount;
    private int _disposed;

    public int SlotSize => _slotSize;
    public int Capacity => _capacity;
    public long TotalBytes => _totalBytes;
    public int AllocatedCount => Volatile.Read(ref _allocatedCount);
    public int AvailableCount => _capacity - AllocatedCount;
    public bool IsPinnedToPhysicalRam => _pinToPhysicalRam;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of <see cref="OffHeapMemoryStore"/>.
    /// </summary>
    /// <param name="slotSize">Fixed size of each memory block in bytes (must be aligned, e.g. 64, 256, 4096).</param>
    /// <param name="capacity">Total number of slots available.</param>
    /// <param name="pinToPhysicalRam">Whether to lock the unmanaged memory range in physical RAM via OS VirtualLock.</param>
    public OffHeapMemoryStore(int slotSize, int capacity, bool pinToPhysicalRam = false)
    {
        if (slotSize <= 0) throw new ArgumentOutOfRangeException(nameof(slotSize), "Slot size must be positive.");
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _slotSize = slotSize;
        _capacity = capacity;
        _totalBytes = (long)slotSize * capacity;
        _pinToPhysicalRam = pinToPhysicalRam;

        // Allocate contiguous zeroed unmanaged memory
        _memoryPtr = (byte*)NativeMemory.AllocZeroed((nuint)_totalBytes);

        if (_pinToPhysicalRam)
        {
            MemoryLockKernel.TryLockMemory((IntPtr)_memoryPtr, (nuint)_totalBytes);
        }

        _slotVersions = new uint[capacity];
        _freeSlots = new int[capacity];

        for (int i = 0; i < capacity; i++)
        {
            _slotVersions[i] = 1; // Generation 1
            _freeSlots[i] = capacity - 1 - i;
        }
        _freeHead = capacity;
    }

    /// <summary>
    /// Allocates an off-heap block slot and returns its unique versioned handle.
    /// </summary>
    /// <returns>A valid <see cref="OffHeapHandle"/>, or <see cref="OffHeapHandle.Invalid"/> if capacity is exhausted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OffHeapHandle Allocate()
    {
        ThrowIfDisposed();

        if (TryPopFreeSlot(out int index))
        {
            Interlocked.Increment(ref _allocatedCount);
            uint version = Volatile.Read(ref _slotVersions[index]);
            return new OffHeapHandle(index, version);
        }

        return OffHeapHandle.Invalid;
    }

    /// <summary>
    /// Releases an off-heap block slot back to the free pool, invalidating the handle against use-after-free.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Free(OffHeapHandle handle)
    {
        ThrowIfDisposed();

        if (!handle.IsValid || (uint)handle.Index >= (uint)_capacity)
            return false;

        int index = handle.Index;
        uint expectedVersion = handle.Version;
        if (Volatile.Read(ref _slotVersions[index]) != expectedVersion)
            return false; // Stale handle / already freed

        uint nextVersion = unchecked(expectedVersion + 1);
        if (nextVersion == 0) nextVersion = 1; // avoid 0

        if (Interlocked.CompareExchange(
                ref Unsafe.As<uint, int>(ref _slotVersions[index]),
                (int)nextVersion,
                (int)expectedVersion) != (int)expectedVersion)
        {
            return false;
        }

        byte* slotPtr = GetSlotPointer(index);
        NativeMemory.Clear(slotPtr, (nuint)_slotSize);

        Interlocked.Decrement(ref _allocatedCount);
        PushFreeSlot(index);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryPopFreeSlot(out int index)
    {
        if (Volatile.Read(ref _freeHead) == 0)
        {
            index = -1;
            return false;
        }

        if (Interlocked.CompareExchange(ref _freeLock, 1, 0) != 0)
        {
            EnterLockSlow();
        }

        int head = _freeHead - 1;
        if (head >= 0)
        {
            index = _freeSlots[head];
            _freeHead = head;
            Volatile.Write(ref _freeLock, 0);
            return true;
        }

        Volatile.Write(ref _freeLock, 0);
        index = -1;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFreeSlot(int index)
    {
        if (Interlocked.CompareExchange(ref _freeLock, 1, 0) != 0)
        {
            EnterLockSlow();
        }

        int head = _freeHead;
        if ((uint)head < (uint)_freeSlots.Length)
        {
            _freeSlots[head] = index;
            _freeHead = head + 1;
        }

        Volatile.Write(ref _freeLock, 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnterLockSlow()
    {
        var spinner = new SpinWait();
        do
        {
            spinner.SpinOnce();
        }
        while (Interlocked.CompareExchange(ref _freeLock, 1, 0) != 0);
    }

    /// <summary>
    /// Gets a zero-copy unmanaged <see cref="Span{T}"/> directly spanning the memory block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(OffHeapHandle handle)
    {
        ValidateHandle(handle);
        return new Span<byte>(GetSlotPointer(handle.Index), _slotSize);
    }

    /// <summary>
    /// Gets a direct unmanaged reference to the struct stored at the off-heap block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(OffHeapHandle handle) where T : unmanaged
    {
        ValidateHandle(handle);
        if (sizeof(T) > _slotSize)
        {
            throw new ArgumentException($"Type {typeof(T).Name} size ({sizeof(T)} bytes) exceeds slot size ({_slotSize} bytes).");
        }

        return ref *(T*)GetSlotPointer(handle.Index);
    }

    /// <summary>
    /// Writes raw payload bytes into the specified off-heap block.
    /// </summary>
    public void Write(OffHeapHandle handle, ReadOnlySpan<byte> source)
    {
        var target = GetSpan(handle);
        int bytesToCopy = Math.Min(source.Length, _slotSize);
        source.Slice(0, bytesToCopy).CopyTo(target);

        // Zero out remainder if source is shorter than slot
        if (bytesToCopy < _slotSize)
        {
            target.Slice(bytesToCopy).Clear();
        }
    }

    /// <summary>
    /// Reads payload bytes from the specified off-heap block into a destination buffer.
    /// </summary>
    public int Read(OffHeapHandle handle, Span<byte> destination)
    {
        var source = GetSpan(handle);
        int bytesToCopy = Math.Min(destination.Length, _slotSize);
        source.Slice(0, bytesToCopy).CopyTo(destination);
        return bytesToCopy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte* GetSlotPointer(int index) => _memoryPtr + ((long)index * _slotSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateHandle(OffHeapHandle handle)
    {
        ThrowIfDisposed();
        if (!handle.IsValid || (uint)handle.Index >= (uint)_capacity)
            throw new ArgumentException("Invalid off-heap handle.", nameof(handle));

        if (_slotVersions[handle.Index] != handle.Version)
            throw new InvalidOperationException($"Use-after-free detected: Handle generation {handle.Version} does not match slot version {_slotVersions[handle.Index]}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(OffHeapMemoryStore));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_memoryPtr != null)
        {
            if (_pinToPhysicalRam)
            {
                MemoryLockKernel.TryUnlockMemory((IntPtr)_memoryPtr, (nuint)_totalBytes);
            }

            NativeMemory.Free(_memoryPtr);
            _memoryPtr = null;
        }
    }
}
