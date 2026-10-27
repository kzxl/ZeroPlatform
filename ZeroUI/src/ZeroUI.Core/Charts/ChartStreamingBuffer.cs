using System;
using System.Collections.Generic;
using ZeroPlatform.Concurrency;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// High-throughput, thread-safe streaming buffer for real-time telemetry and oscilloscope charts.
    /// Built on sovereign Tier-0 <see cref="ZeroRingBuffer{T}"/> for lock-free, zero-allocation data ingestion.
    /// </summary>
    public class ChartStreamingBuffer
    {
        private readonly ZeroRingBuffer<ChartPoint> _ringBuffer;
        private readonly object _syncLock = new object();
        private readonly int _capacity;

        /// <summary>
        /// Maximum number of points held in the streaming window.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Current number of points in the buffer.
        /// </summary>
        public int Count => _ringBuffer.Count;

        /// <summary>
        /// Creates a new streaming buffer with the specified capacity (rounded up to power of 2 if needed).
        /// </summary>
        public ChartStreamingBuffer(int capacity = 1024)
        {
            if (capacity < 16) capacity = 16;
            // Round up to power of two for optimal bitmask performance
            _capacity = RoundUpPowerOfTwo(capacity);
            _ringBuffer = new ZeroRingBuffer<ChartPoint>(_capacity);
        }

        /// <summary>
        /// Enqueues a single point. If full, drops the oldest point to maintain a rolling sliding window.
        /// </summary>
        public void Push(double value, string label = "", uint? colorRgba = null)
        {
            Push(new ChartPoint(label, value, colorRgba));
        }

        /// <summary>
        /// Enqueues a pre-constructed ChartPoint with rolling window semantics.
        /// </summary>
        public void Push(ChartPoint point)
        {
            lock (_syncLock)
            {
                if (_ringBuffer.IsFull)
                {
                    // Evict oldest point to slide the window forward
                    _ringBuffer.TryDequeue(out _);
                }
                _ringBuffer.TryEnqueue(point);
            }
        }

        /// <summary>
        /// Enqueues a batch of points atomically.
        /// </summary>
        public void PushBatch(ReadOnlySpan<ChartPoint> points)
        {
            lock (_syncLock)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    if (_ringBuffer.IsFull)
                    {
                        _ringBuffer.TryDequeue(out _);
                    }
                    _ringBuffer.TryEnqueue(points[i]);
                }
            }
        }

        /// <summary>
        /// Copies all current points into a target list with zero allocation if capacity permits.
        /// </summary>
        public void CopyTo(List<ChartPoint> target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.Clear();

            lock (_syncLock)
            {
                int count = _ringBuffer.Count;
                if (count == 0) return;

                var rented = System.Buffers.ArrayPool<ChartPoint>.Shared.Rent(count);
                try
                {
                    int dequeued = _ringBuffer.TryDequeueBatch(rented.AsSpan(0, count));
                    for (int i = 0; i < dequeued; i++)
                    {
                        target.Add(rented[i]);
                        _ringBuffer.TryEnqueue(rented[i]);
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<ChartPoint>.Shared.Return(rented, clearArray: true);
                }
            }
        }

        /// <summary>
        /// Snapshots current points into an array.
        /// </summary>
        public ChartPoint[] Snapshot()
        {
            lock (_syncLock)
            {
                int count = _ringBuffer.Count;
                if (count == 0) return Array.Empty<ChartPoint>();

                var result = new ChartPoint[count];
                int dequeued = _ringBuffer.TryDequeueBatch(result);
                for (int i = 0; i < dequeued; i++)
                {
                    _ringBuffer.TryEnqueue(result[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// Snapshots only the numerical values into an array, ideal for DSP filtering or FFT analysis.
        /// </summary>
        public double[] SnapshotValues()
        {
            lock (_syncLock)
            {
                int count = _ringBuffer.Count;
                if (count == 0) return Array.Empty<double>();

                var pts = new ChartPoint[count];
                int dequeued = _ringBuffer.TryDequeueBatch(pts);
                var values = new double[dequeued];
                for (int i = 0; i < dequeued; i++)
                {
                    values[i] = pts[i].Value;
                    _ringBuffer.TryEnqueue(pts[i]);
                }
                return values;
            }
        }

        /// <summary>
        /// Clears all points in the buffer.
        /// </summary>
        public void Clear()
        {
            lock (_syncLock)
            {
                _ringBuffer.Clear();
            }
        }

        private static int RoundUpPowerOfTwo(int x)
        {
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }
    }
}
