using System;
using System.IO;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Supported SQLite Write-Ahead Logging (WAL) checkpoint operations.
    /// </summary>
    public enum SqliteCheckpointMode
    {
        /// <summary>
        /// Checkpoints as many frames as possible without waiting for readers.
        /// </summary>
        Passive = 0,

        /// <summary>
        /// Blocks until all readers have completed, checkpoints all frames, but leaves WAL size intact.
        /// </summary>
        Full = 1,

        /// <summary>
        /// Similar to Full, but also resets the WAL file so subsequent writes start from frame 1.
        /// </summary>
        Restart = 2,

        /// <summary>
        /// Similar to Restart, but truncates the WAL file to zero bytes upon completion.
        /// </summary>
        Truncate = 3
    }

    /// <summary>
    /// Storage and WAL file metrics for an industrial SQLite time-series partition.
    /// </summary>
    public sealed class HistorianStorageMetrics
    {
        public DateTime PartitionDate { get; }
        public string DatabaseFilePath { get; }
        public long DatabaseSizeBytes { get; }
        public long WalSizeBytes { get; }
        public long TotalRecords { get; }
        public TimeSpan LastCheckpointDuration { get; internal set; }

        public double DatabaseSizeMb => DatabaseSizeBytes / (1024.0 * 1024.0);
        public double WalSizeMb => WalSizeBytes / (1024.0 * 1024.0);
        public double TotalSizeMb => (DatabaseSizeBytes + WalSizeBytes) / (1024.0 * 1024.0);

        public HistorianStorageMetrics(
            DateTime partitionDate,
            string databaseFilePath,
            long databaseSizeBytes,
            long walSizeBytes,
            long totalRecords,
            TimeSpan lastCheckpointDuration)
        {
            PartitionDate = partitionDate;
            DatabaseFilePath = databaseFilePath;
            DatabaseSizeBytes = databaseSizeBytes;
            WalSizeBytes = walSizeBytes;
            TotalRecords = totalRecords;
            LastCheckpointDuration = lastCheckpointDuration;
        }

        public override string ToString() =>
            $"Partition: {PartitionDate:yyyy-MM-dd} | Records: {TotalRecords:N0} | DB: {DatabaseSizeMb:F2} MB | WAL: {WalSizeMb:F2} MB | Checkpoint: {LastCheckpointDuration.TotalMilliseconds:F2} ms";
    }
}
