using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Data;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Autonomous end-to-end telemetry historian pipeline.
    /// Ingests streaming batches from <see cref="ZeroTelemetryBus"/>, maintains in-memory ring buffers,
    /// coordinates periodic WAL flushes, and executes LTTB downsampling for UI charts.
    /// </summary>
    public sealed class ZeroHistorianPipeline : IDisposable
    {
        private static readonly Lazy<ZeroHistorianPipeline> _shared =
            new Lazy<ZeroHistorianPipeline>(() => new ZeroHistorianPipeline("Shared", ZeroTelemetryBus.Shared));

        public static ZeroHistorianPipeline Shared => _shared.Value;

        private readonly string _name;
        private readonly ZeroTelemetryBus _bus;
        private readonly IHistorianEngine? _engine;
        private readonly ZeroTagStore? _tagStore;

        // In-memory ring buffer per tag for sub-second zero-disk live queries
        private readonly ConcurrentDictionary<int, TagRingBuffer> _recentBuffers =
            new ConcurrentDictionary<int, TagRingBuffer>();

        private IDisposable? _busSubscription;
        private long _totalIngestedSamples;
        private long _totalBatchesIngested;
        private bool _disposed = false;

        public string Name => _name;
        public long TotalIngestedSamples => Volatile.Read(ref _totalIngestedSamples);
        public long TotalBatchesIngested => Volatile.Read(ref _totalBatchesIngested);

        public ZeroHistorianPipeline(
            string name = "HistorianPipeline",
            ZeroTelemetryBus? bus = null,
            IHistorianEngine? engine = null,
            ZeroTagStore? tagStore = null)
        {
            _name = name;
            _bus = bus ?? ZeroTelemetryBus.Shared;
            _engine = engine;
            _tagStore = tagStore;

            // Auto-subscribe to telemetry stream
            _busSubscription = _bus.SubscribeUpdates(IngestBatch);
        }

        public void ConfigureTagRingBuffer(int tagId, int capacity = 1000)
        {
            _recentBuffers[tagId] = new TagRingBuffer(capacity);
        }

        public void IngestBatch(ReadOnlySpan<TagUpdate> batch)
        {
            if (batch.IsEmpty) return;

            Interlocked.Add(ref _totalIngestedSamples, batch.Length);
            Interlocked.Increment(ref _totalBatchesIngested);

            for (int i = 0; i < batch.Length; i++)
            {
                ref readonly var update = ref batch[i];

                // 1. Ingest into fast in-memory ring buffer
                if (_recentBuffers.TryGetValue(update.TagId, out var ring))
                {
                    ring.Add(update.TimestampUtcMs, update.Value.AsDouble());
                }

                // 2. Feed to disk historian engine if configured
                if (_engine != null)
                {
                    string path = _tagStore?.GetMetadata(update.TagId)?.Path ?? $"Tag_{update.TagId}";
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(update.TimestampUtcMs).UtcDateTime;
                    _engine.LogSample(path, update.Value.AsDouble(), update.Value.Quality, dt);
                }
            }
        }

        public IReadOnlyList<TimePoint> QueryRecentInMemory(int tagId)
        {
            if (_recentBuffers.TryGetValue(tagId, out var ring))
            {
                return ring.Snapshot();
            }
            return Array.Empty<TimePoint>();
        }

        public async Task<IReadOnlyList<TimePoint>> QueryHistoricalAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints = 1000,
            CancellationToken cancellationToken = default)
        {
            if (_engine != null)
            {
                return await _engine.QueryDecimatedAsync(tagPath, startTime, endTime, targetPoints, cancellationToken).ConfigureAwait(false);
            }

            return Array.Empty<TimePoint>();
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_engine != null)
            {
                await _engine.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _busSubscription?.Dispose();
            _busSubscription = null;
            _recentBuffers.Clear();
        }

        /// <summary>
        /// High-performance circular ring buffer storing recent telemetry points without heap allocations.
        /// </summary>
        private sealed class TagRingBuffer
        {
            private readonly object _lock = new object();
            private readonly TimePoint[] _buffer;
            private int _head = 0;
            private int _count = 0;
            private readonly int _capacity;

            public TagRingBuffer(int capacity)
            {
                _capacity = Math.Max(10, capacity);
                _buffer = new TimePoint[_capacity];
            }

            public void Add(long timestampMs, double value)
            {
                lock (_lock)
                {
                    _buffer[_head] = new TimePoint(timestampMs, value);
                    _head = (_head + 1) % _capacity;
                    if (_count < _capacity) _count++;
                }
            }

            public IReadOnlyList<TimePoint> Snapshot()
            {
                lock (_lock)
                {
                    if (_count == 0) return Array.Empty<TimePoint>();

                    var result = new TimePoint[_count];
                    int start = (_head - _count + _capacity) % _capacity;

                    for (int i = 0; i < _count; i++)
                    {
                        result[i] = _buffer[(start + i) % _capacity];
                    }

                    return result;
                }
            }
        }
    }
}
