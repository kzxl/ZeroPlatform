using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Thread-safe zero-allocation coalescing queue that throttles high-frequency telemetry updates.
    /// Deduplicates signals via latest-value semantics and delivers coalesced batches to UI subscribers
    /// via ArrayPool&lt;IScadaTag&gt;.Shared and ArraySegment&lt;IScadaTag&gt;.
    /// </summary>
    public sealed class TelemetryThrottleQueue : IDisposable
    {
        private readonly ConcurrentDictionary<string, IScadaTag> _pendingSnapshots =
            new ConcurrentDictionary<string, IScadaTag>(StringComparer.OrdinalIgnoreCase);

        private readonly Timer _flushTimer;
        private readonly Action<IReadOnlyList<IScadaTag>> _flushCallback;
        private int _isFlushing;
        private bool _disposed;

        /// <summary>
        /// Gets the flush interval in milliseconds (default: 33ms ~ 30Hz).
        /// </summary>
        public int IntervalMs { get; }

        public TelemetryThrottleQueue(Action<IReadOnlyList<IScadaTag>> flushCallback, int intervalMs = 33)
        {
            _flushCallback = flushCallback ?? throw new ArgumentNullException(nameof(flushCallback));
            IntervalMs = Math.Max(10, intervalMs);
            _flushTimer = new Timer(OnFlushTimer, null, IntervalMs, IntervalMs);
        }

        /// <summary>
        /// Enqueues a telemetry snapshot. Coalesces with newest value if tag is already pending.
        /// </summary>
        public void Enqueue(IScadaTag tag)
        {
            if (tag == null || string.IsNullOrEmpty(tag.TagPath)) return;
            _pendingSnapshots[tag.TagPath] = tag;
        }

        private void OnFlushTimer(object? state)
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
                return; // Previous flush still executing, skip frame

            try
            {
                if (_pendingSnapshots.IsEmpty) return;

                int count = _pendingSnapshots.Count;
                if (count == 0) return;

                // Rent buffer from ArrayPool (Zero heap allocation)
                var rentedArray = ArrayPool<IScadaTag>.Shared.Rent(count);
                int itemsCopied = 0;

                try
                {
                    foreach (var kvp in _pendingSnapshots)
                    {
                        if (_pendingSnapshots.TryRemove(kvp.Key, out var tag))
                        {
                            rentedArray[itemsCopied++] = tag;
                            if (itemsCopied >= count) break;
                        }
                    }

                    if (itemsCopied > 0)
                    {
                        // ArraySegment<T> implements IReadOnlyList<T> without heap allocation
                        var segment = new ArraySegment<IScadaTag>(rentedArray, 0, itemsCopied);
                        _flushCallback(segment);
                    }
                }
                finally
                {
                    ArrayPool<IScadaTag>.Shared.Return(rentedArray, clearArray: true);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flushTimer.Dispose();
            _pendingSnapshots.Clear();
        }
    }
}
