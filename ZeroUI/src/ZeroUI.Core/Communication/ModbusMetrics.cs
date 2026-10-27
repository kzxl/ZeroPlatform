using System;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Detailed fine-grained telemetry observability metrics for industrial Modbus TCP communication.
    /// Separates pure network RTT from decode, tag update, and total poll cycle duration.
    /// </summary>
    public sealed class ModbusMetrics
    {
        /// <summary>
        /// Pure network round-trip time (socket send to socket receive completion) for the last poll cycle.
        /// </summary>
        public TimeSpan NetworkRtt { get; internal set; }

        /// <summary>
        /// Time spent decoding raw register byte slices into strongly typed engineering units.
        /// </summary>
        public TimeSpan DecodeTime { get; internal set; }

        /// <summary>
        /// Time spent dispatching decoded values into the Tag Engine / Tag Storage.
        /// </summary>
        public TimeSpan TagUpdateTime { get; internal set; }

        /// <summary>
        /// Total end-to-end elapsed time of the most recent PollOnceAsync execution.
        /// </summary>
        public TimeSpan TotalCycleTime { get; internal set; }

        /// <summary>
        /// Total number of poll cycles executed.
        /// </summary>
        public long TotalPollCycles { get; internal set; }

        /// <summary>
        /// Total number of network requests sent.
        /// </summary>
        public long TotalRequestsSent { get; internal set; }

        /// <summary>
        /// Total number of valid network responses received.
        /// </summary>
        public long TotalResponsesReceived { get; internal set; }

        /// <summary>
        /// Total payload bytes transmitted over the socket.
        /// </summary>
        public long TotalBytesSent { get; internal set; }

        /// <summary>
        /// Total payload bytes received over the socket.
        /// </summary>
        public long TotalBytesReceived { get; internal set; }

        /// <summary>
        /// Total number of communication faults, timeouts, or decoding errors encountered.
        /// </summary>
        public long TotalErrors { get; internal set; }

        /// <summary>
        /// Number of compiled register blocks polled per cycle.
        /// </summary>
        public int PlannedBlockCount { get; internal set; }

        // Formatted helper properties for UI telemetry dashboards & diagnostics
        public double NetworkRttMs => NetworkRtt.TotalMilliseconds;
        public double DecodeMs => DecodeTime.TotalMilliseconds;
        public double TagUpdateMs => TagUpdateTime.TotalMilliseconds;
        public double TotalCycleMs => TotalCycleTime.TotalMilliseconds;

        public override string ToString() =>
            $"[Modbus] RTT: {NetworkRttMs:F2}ms | Decode: {DecodeMs:F3}ms | TagUpdate: {TagUpdateMs:F3}ms | Cycle: {TotalCycleMs:F2}ms | Blocks: {PlannedBlockCount}";
    }
}
