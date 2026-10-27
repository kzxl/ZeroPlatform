using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroSystem.Memory;

/// <summary>
/// High-throughput, zero-allocation circular buffer residing completely in unmanaged off-heap memory.
/// Specially optimized for high-frequency telemetry, kernel metrics, and sensor ingestion with 0% GC overhead.
/// </summary>
/// <typeparam name="T">Unmanaged data type (e.g. SystemMemoryStatus, CpuTelemetrySnapshot, struct).</typeparam>
public sealed unsafe class OffHeapCircularTelemetryBuffer<T> : IDisposable where T : unmanaged
{
    private readonly int _capacity;
    private readonly int _mask;
    private readonly int _itemSize;
    private readonly long _totalBytes;
    private readonly bool _pinToPhysicalRam;

    private byte* _bufferPtr;
    private long _sequence;
    private int _disposed;

    public int Capacity => _capacity;
    public int ItemSize => _itemSize;
    public long TotalBytes => _totalBytes;
    public long TotalWrittenCount => Volatile.Read(ref _sequence);
    public bool IsPinnedToPhysicalRam => _pinToPhysicalRam;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of <see cref="OffHeapCircularTelemetryBuffer{T}"/>.
    /// </summary>
    /// <param name="capacity">Number of items to retain. Automatically rounded up to the nearest power of two.</param>
    /// <param name="pinToPhysicalRam">Whether to lock the virtual address range into physical RAM via OS VirtualLock.</param>
    public OffHeapCircularTelemetryBuffer(int capacity, bool pinToPhysicalRam = false)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
        _mask = _capacity - 1;
        _itemSize = sizeof(T);
        _totalBytes = (long)_itemSize * _capacity;
        _pinToPhysicalRam = pinToPhysicalRam;

        _bufferPtr = (byte*)NativeMemory.AllocZeroed((nuint)_totalBytes);

        if (_pinToPhysicalRam)
        {
            MemoryLockKernel.TryLockMemory((IntPtr)_bufferPtr, (nuint)_totalBytes);
        }
    }

    /// <summary>
    /// Appends a telemetry item into the circular buffer. Overwrites the oldest item once capacity is reached.
    /// Thread-safe and lock-free.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(in T item)
    {
        ThrowIfDisposed();

        long seq = Interlocked.Increment(ref _sequence) - 1;
        int slot = (int)(seq & _mask);
        T* pSlot = (T*)(_bufferPtr + ((long)slot * _itemSize));
        *pSlot = item;
    }

    /// <summary>
    /// Attempts to read the most recently written telemetry item.
    /// </summary>
    public bool TryReadLatest(out T item)
    {
        ThrowIfDisposed();

        long seq = Volatile.Read(ref _sequence);
        if (seq == 0)
        {
            item = default;
            return false;
        }

        int slot = (int)((seq - 1) & _mask);
        T* pSlot = (T*)(_bufferPtr + ((long)slot * _itemSize));
        item = *pSlot;
        return true;
    }

    /// <summary>
    /// Copies up to <paramref name="destination"/>.Length most recent telemetry items in chronological order.
    /// </summary>
    /// <returns>The number of items written to destination.</returns>
    public int CopyRecent(Span<T> destination)
    {
        ThrowIfDisposed();

        long seq = Volatile.Read(ref _sequence);
        if (seq == 0 || destination.Length == 0)
            return 0;

        int available = (int)Math.Min(seq, _capacity);
        int toCopy = Math.Min(destination.Length, available);

        long startSeq = seq - toCopy;
        for (int i = 0; i < toCopy; i++)
        {
            int slot = (int)((startSeq + i) & _mask);
            T* pSlot = (T*)(_bufferPtr + ((long)slot * _itemSize));
            destination[i] = *pSlot;
        }

        return toCopy;
    }

    /// <summary>
    /// Resets the write sequence.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        Volatile.Write(ref _sequence, 0);
        NativeMemory.Clear(_bufferPtr, (nuint)_totalBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(OffHeapCircularTelemetryBuffer<T>));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_bufferPtr != null)
        {
            if (_pinToPhysicalRam)
            {
                MemoryLockKernel.TryUnlockMemory((IntPtr)_bufferPtr, (nuint)_totalBytes);
            }

            NativeMemory.Free(_bufferPtr);
            _bufferPtr = null;
        }
    }
}
