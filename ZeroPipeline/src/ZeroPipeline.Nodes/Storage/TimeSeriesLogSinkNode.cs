using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Nodes.Inspection;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;

namespace ZeroPipeline.Nodes.Storage
{
    /// <summary>
    /// Pipeline terminal sink node that logs numeric inspection metrics into Gorilla-compressed TimeSeriesBlocks.
    /// Supports high-throughput industrial telemetry logging with delta-of-delta and XOR floating-point compression.
    /// </summary>
    public sealed class TimeSeriesLogSinkNode : SinkNode<InspectionResult>
    {
        private readonly List<TimeSeriesPoint> _pointBuffer;
        private readonly List<TimeSeriesBlock> _compressedBlocks;
        private readonly int _metricId;
        private readonly int _blockSize;
        private readonly object _lock = new object();

        public int MetricId => _metricId;
        public int BlockSize => _blockSize;
        public IReadOnlyList<TimeSeriesBlock> CompressedBlocks
        {
            get
            {
                lock (_lock) return _compressedBlocks.ToArray();
            }
        }

        public int TotalLoggedPoints { get; private set; }

        public TimeSeriesLogSinkNode(
            int metricId = 1,
            int blockSize = 100,
            string? name = null)
            : base(name ?? $"TSDB_Logger[Metric={metricId}]")
        {
            _metricId = metricId;
            _blockSize = blockSize > 0 ? blockSize : 100;
            _pointBuffer = new List<TimeSeriesPoint>(_blockSize);
            _compressedBlocks = new List<TimeSeriesBlock>();
        }

        protected override Task ConsumeAsync(InspectionResult input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) return Task.CompletedTask;

            long timestampMs = input.TimestampTicks / TimeSpan.TicksPerMillisecond;
            var point = new TimeSeriesPoint(timestampMs, input.MeasuredValue);

            lock (_lock)
            {
                _pointBuffer.Add(point);
                TotalLoggedPoints++;

                if (_pointBuffer.Count >= _blockSize)
                {
                    FlushBufferInternal();
                }
            }

            return Task.CompletedTask;
        }

        private void FlushBufferInternal()
        {
            if (_pointBuffer.Count > 0)
            {
                var block = TimeSeriesBlock.FromPoints(_metricId, _pointBuffer);
                _compressedBlocks.Add(block);
                _pointBuffer.Clear();
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                FlushBufferInternal();
            }
        }

        protected override Task OnResetAsync()
        {
            lock (_lock)
            {
                _pointBuffer.Clear();
                _compressedBlocks.Clear();
                TotalLoggedPoints = 0;
            }
            return base.OnResetAsync();
        }
    }
}
