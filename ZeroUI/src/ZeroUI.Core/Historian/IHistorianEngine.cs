using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Data;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Contract for industrial time-series historian engines.
    /// Handles asynchronous batch ingestion, downsampled range queries,
    /// Store-and-Forward synchronization, and rolling partition maintenance.
    /// </summary>
    public interface IHistorianEngine : IDisposable
    {
        /// <summary>
        /// Enqueues a single telemetry sample into the ingestion buffer (non-blocking).
        /// </summary>
        void LogSample(string tagPath, double value, ScadaQuality quality = ScadaQuality.Good, DateTime? timestamp = null);

        /// <summary>
        /// Flushes any pending buffered records immediately to persistent storage.
        /// </summary>
        Task FlushAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries historical data for a specific tag within a time window,
        /// automatically decimated using LTTB down to targetPoints for instantaneous chart rendering.
        /// </summary>
        Task<IReadOnlyList<TimePoint>> QueryDecimatedAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints = 1000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries historical data for a specific tag within a time window,
        /// automatically routed to the optimal multi-resolution tier (or explicit override)
        /// and decimated using LTTB down to targetPoints.
        /// </summary>
        Task<IReadOnlyList<TimePoint>> QueryDecimatedAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints,
            TelemetryResolution? resolution,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Purges database partitions older than the specified retention window in days.
        /// </summary>
        Task<int> PurgeExpiredPartitionsAsync(int retentionDays, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a batch of unsynchronized records for Store-and-Forward forwarding.
        /// </summary>
        Task<IReadOnlyList<HistorianRecord>> ReadUnsyncedBatchAsync(int batchSize = 1000, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks records up to the specified ID as successfully synchronized to the central server.
        /// </summary>
        Task MarkSyncedAsync(long upToId, DateTime partitionDate, CancellationToken cancellationToken = default);
    }
}
