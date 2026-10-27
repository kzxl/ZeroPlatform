using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Comprehensive end-to-end SCADA telemetry pipeline observability metrics.
    /// Breaks down the pipeline into discrete stages: Field RTT, Protocol Decode, Tag Storage Update,
    /// Queue/Buffer Delay, UI Dispatch Batching, and UI Frame Render Time.
    /// </summary>
    public sealed class TelemetryMetrics
    {
        public static readonly TelemetryMetrics Shared = new TelemetryMetrics();

        /// <summary>
        /// Pure network round-trip time between SCADA host and PLC (ms).
        /// </summary>
        public double PlcRttMs { get; set; }

        /// <summary>
        /// Protocol packet deserialization and byte decoding time (ms).
        /// </summary>
        public double DecodeMs { get; set; }

        /// <summary>
        /// Time to write decoded values into Tag Storage and evaluate alarms/deadbands (ms).
        /// </summary>
        public double TagUpdateMs { get; set; }

        /// <summary>
        /// Latency spent inside intermediate queues/buffers before processing (ms).
        /// </summary>
        public double QueueDelayMs { get; set; }

        /// <summary>
        /// Duration of the batched UI dispatch tick executing on the UI STA thread (ms).
        /// </summary>
        public double UiBatchMs { get; set; }

        /// <summary>
        /// Total UI frame render time including GDI/DIB rasterization (ms).
        /// </summary>
        public double FrameMs { get; set; }

        /// <summary>
        /// Formatted summary table for real-time SCADA diagnostic overlays and status strips.
        /// </summary>
        public override string ToString() =>
            $"PLC RTT: {PlcRttMs:F2}ms | Decode: {DecodeMs:F3}ms | Tag Update: {TagUpdateMs:F3}ms | Queue: {QueueDelayMs:F3}ms | UI Batch: {UiBatchMs:F2}ms | Frame: {FrameMs:F2}ms";
    }
}
