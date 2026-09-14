using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroIoT.OpcUa;
using ZeroIoT.SparkplugB;
using ZeroStorage.Core.Gorilla;

namespace ZeroIoT.Bridges
{
    /// <summary>
    /// High-throughput streaming sink capturing IoT metrics directly into Gorilla TSDB compressed chunks.
    /// </summary>
    public class IotGorillaTsdbSink
    {
        private readonly ConcurrentDictionary<string, List<TimeSeriesPoint>> _buffers =
            new ConcurrentDictionary<string, List<TimeSeriesPoint>>();
        private readonly object _lock = new object();

        /// <summary>
        /// Ingests a raw timestamp and numeric value for a named tag.
        /// </summary>
        public void Record(string tag, long timestampMs, double value)
        {
            if (string.IsNullOrEmpty(tag)) throw new ArgumentNullException(nameof(tag));

            var list = _buffers.GetOrAdd(tag, _ => new List<TimeSeriesPoint>());
            lock (list)
            {
                list.Add(new TimeSeriesPoint(timestampMs, value));
            }
        }

        /// <summary>
        /// Ingests an OPC-UA tag value.
        /// </summary>
        public void Record(NodeId nodeId, UaDataValue dataValue)
        {
            if (dataValue == null) return;
            string tag = nodeId.ToString();
            long ts = dataValue.SourceTimestamp?.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond
                      ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double val = dataValue.Value.ToDouble();
            Record(tag, ts, val);
        }

        /// <summary>
        /// Ingests a Sparkplug B metric.
        /// </summary>
        public void Record(SparkplugMetric metric)
        {
            if (metric == null || metric.IsNull) return;
            string tag = metric.Name;
            long ts = (long)metric.Timestamp;
            double val = metric.AsDouble();
            Record(tag, ts, val);
        }

        /// <summary>
        /// Ingests all metrics in a Sparkplug B payload.
        /// </summary>
        public void Record(SparkplugPayload payload)
        {
            if (payload == null) return;
            for (int i = 0; i < payload.Metrics.Count; i++)
            {
                Record(payload.Metrics[i]);
            }
        }

        /// <summary>
        /// Gets the current uncompressed point count for a tag.
        /// </summary>
        public int GetPointCount(string tag)
        {
            if (_buffers.TryGetValue(tag, out var list))
            {
                lock (list) { return list.Count; }
            }
            return 0;
        }

        /// <summary>
        /// Flushes and compresses buffered points for a specific tag using Gorilla bit-packing.
        /// </summary>
        public byte[] Flush(string tag)
        {
            if (!_buffers.TryGetValue(tag, out var list))
                return Array.Empty<byte>();

            List<TimeSeriesPoint> snapshot;
            lock (list)
            {
                if (list.Count == 0) return Array.Empty<byte>();
                snapshot = new List<TimeSeriesPoint>(list);
                list.Clear();
            }

            return GorillaEncoder.Encode(snapshot);
        }

        /// <summary>
        /// Flushes and compresses all buffered tags, returning a dictionary of compressed byte arrays.
        /// </summary>
        public Dictionary<string, byte[]> FlushAll()
        {
            var result = new Dictionary<string, byte[]>();
            foreach (var kvp in _buffers)
            {
                byte[] compressed = Flush(kvp.Key);
                if (compressed.Length > 0)
                {
                    result[kvp.Key] = compressed;
                }
            }
            return result;
        }
    }
}
