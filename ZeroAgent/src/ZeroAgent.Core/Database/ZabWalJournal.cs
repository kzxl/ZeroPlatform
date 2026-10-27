using System;
using System.IO;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// High-performance, crash-resilient Write-Ahead Log (WAL) journal manager.
    /// Employs append-only framing with hardware CRC32C verification to prevent database corruption
    /// during continuous concurrent read/write workloads and sudden power failures.
    /// </summary>
    public sealed class ZabWalJournal : IDisposable
    {
        private readonly string _walFilePath;
        private readonly object _lock = new object();
        private int _pendingFrames;
        private bool _disposed;

        public string WalFilePath => _walFilePath;
        public int PendingFrames => _pendingFrames;
        public int AutoCheckpointThreshold { get; set; } = 500;
        public long MaxWalSizeBytes { get; set; } = 16 * 1024 * 1024; // 16 MB default threshold

        public ZabWalJournal(string baselineFilePath, int autoCheckpointThreshold = 500, long maxWalSizeBytes = 16 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(baselineFilePath)) throw new ArgumentNullException(nameof(baselineFilePath));
            _walFilePath = baselineFilePath + "-wal";
            AutoCheckpointThreshold = autoCheckpointThreshold;
            MaxWalSizeBytes = maxWalSizeBytes;
        }

        /// <summary>
        /// Appends an atomic transaction frame to the WAL and flushes directly to persistent storage.
        /// </summary>
        public void AppendFrame(ZabWalOpCode opCode, byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            lock (_lock)
            {
                string? dir = Path.GetDirectoryName(_walFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }

                var frame = new ZabWalFrame(opCode, payload);

                using (var fs = new FileStream(_walFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    frame.WriteTo(fs);
                    fs.Flush(flushToDisk: true); // Hardware durability guarantee
                }

                _pendingFrames++;
            }
        }

        /// <summary>
        /// Recovers and replays uncommitted mutations from the WAL file.
        /// Automatically detects and truncates any torn/partial frames resulting from power failures.
        /// </summary>
        public int RecoverAndReplay(Action<ZabWalOpCode, byte[]> applyMutation)
        {
            if (applyMutation == null) throw new ArgumentNullException(nameof(applyMutation));

            lock (_lock)
            {
                if (!File.Exists(_walFilePath)) return 0;

                var fileInfo = new FileInfo(_walFilePath);
                if (fileInfo.Length == 0) return 0;

                int replayedCount = 0;
                long validBytesOffset = 0;

                using (var fs = new FileStream(_walFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    while (fs.Position < fs.Length)
                    {
                        var frame = ZabWalFrame.TryRead(fs, out long frameStart, out bool isTorn);
                        if (isTorn || frame == null)
                        {
                            // Torn/corrupted frame detected at EOF -> truncate safely
                            fs.SetLength(frameStart);
                            break;
                        }

                        applyMutation(frame.OpCode, frame.Payload);
                        replayedCount++;
                        validBytesOffset = fs.Position;
                    }
                }

                _pendingFrames = replayedCount;
                return replayedCount;
            }
        }

        /// <summary>
        /// Executes a WAL Checkpoint:
        /// Invokes the provided baseline snapshot flusher, then resets the WAL log file to 0 bytes.
        /// </summary>
        public void Checkpoint(Action flushBaselineSnapshot)
        {
            if (flushBaselineSnapshot == null) throw new ArgumentNullException(nameof(flushBaselineSnapshot));

            lock (_lock)
            {
                flushBaselineSnapshot();

                if (File.Exists(_walFilePath))
                {
                    using (var fs = new FileStream(_walFilePath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength(0);
                        fs.Flush(flushToDisk: true);
                    }
                }

                _pendingFrames = 0;
            }
        }

        /// <summary>
        /// Checks whether the pending frame count or WAL file size has crossed checkpoint thresholds.
        /// </summary>
        public bool ShouldCheckpoint()
        {
            if (_pendingFrames >= AutoCheckpointThreshold) return true;
            try
            {
                if (File.Exists(_walFilePath) && new FileInfo(_walFilePath).Length >= MaxWalSizeBytes)
                {
                    return true;
                }
            }
            catch
            {
                // Fallback gracefully if file access temporarily contended
            }
            return false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
