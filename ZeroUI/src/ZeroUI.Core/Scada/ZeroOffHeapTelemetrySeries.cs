using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroPrimitives.Memory;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Blittable telemetry sample point containing microsecond-resolution timestamp and scalar value.
    /// Memory footprint: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct TelemetrySample
    {
        public readonly double TimestampUtcMs;
        public readonly double Value;

        public TelemetrySample(double timestampUtcMs, double value)
        {
            TimestampUtcMs = timestampUtcMs;
            Value = value;
        }
    }

    /// <summary>
    /// High-throughput off-heap sliding window circular buffer for industrial SCADA telemetry.
    /// Allocates unmanaged memory directly from NativeMemoryPool (0 GC allocation, 0 LOH churn).
    /// Safe for 100k+ samples/sec real-time streaming to UI charts without triggering GC pause spikes.
    /// </summary>
    public sealed unsafe class ZeroOffHeapTelemetrySeries : IDisposable
    {
        private NativeMemoryBlock? _memoryBlock;
        private TelemetrySample* _ptr;
        private readonly int _capacity;
        private int _head;
        private int _count;
        private readonly object _syncLock = new object();
        private int _disposed;

        public int Capacity => _capacity;

        public int Count
        {
            get
            {
                lock (_syncLock) return _count;
            }
        }

        public ZeroOffHeapTelemetrySeries(int capacity = 16384)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            int bytesRequired = capacity * sizeof(TelemetrySample);
            _memoryBlock = NativeMemoryPool.Shared.Rent(bytesRequired);
            _ptr = (TelemetrySample*)_memoryBlock.Pointer;
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Pushes a new telemetry data point into the off-heap sliding window buffer.
        /// Overwrites the oldest sample when capacity is reached. Zero GC allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(double timestampUtcMs, double value)
        {
            Push(new TelemetrySample(timestampUtcMs, value));
        }

        /// <summary>
        /// Pushes a sample struct into the off-heap sliding window buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(in TelemetrySample sample)
        {
            ThrowIfDisposed();
            lock (_syncLock)
            {
                _ptr[_head] = sample;
                _head = (_head + 1) % _capacity;
                if (_count < _capacity)
                {
                    _count++;
                }
            }
        }

        /// <summary>
        /// Reads the latest N samples into destination spans in chronological order (oldest to newest).
        /// Zero heap allocation.
        /// </summary>
        public int ReadLatest(Span<double> outTimestamps, Span<double> outValues)
        {
            ThrowIfDisposed();
            lock (_syncLock)
            {
                int available = _count;
                int readCount = Math.Min(available, Math.Min(outTimestamps.Length, outValues.Length));
                if (readCount <= 0) return 0;

                int startIdx = (_head - readCount + _capacity) % _capacity;

                for (int i = 0; i < readCount; i++)
                {
                    int ringIdx = (startIdx + i) % _capacity;
                    outTimestamps[i] = _ptr[ringIdx].TimestampUtcMs;
                    outValues[i] = _ptr[ringIdx].Value;
                }

                return readCount;
            }
        }

        /// <summary>
        /// Resets the sliding window without deallocating unmanaged memory.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            lock (_syncLock)
            {
                _head = 0;
                _count = 0;
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroOffHeapTelemetrySeries));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lock (_syncLock)
                {
                    _memoryBlock?.Dispose();
                    _memoryBlock = null;
                    _ptr = null;
                    _count = 0;
                }
            }
        }
    }
}
