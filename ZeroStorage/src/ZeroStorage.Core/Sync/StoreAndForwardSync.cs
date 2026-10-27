using System;
using System.Collections.Generic;
using System.IO;
using ZeroPrimitives.Cryptography;

namespace ZeroStorage.Core.Sync
{
    /// <summary>
    /// Represents the current synchronization state of an edge ZeroStorage instance.
    /// </summary>
    public sealed class SyncStatus
    {
        public int PendingSegmentCount { get; }
        public int SyncedSegmentCount { get; }
        public long PendingWalBytes { get; }
        public long LastWalAckOffset { get; }
        public DateTime? LastSyncTimeUtc { get; }

        public SyncStatus(int pendingSegments, int syncedSegments, long pendingWalBytes, long lastWalAckOffset, DateTime? lastSyncTimeUtc)
        {
            PendingSegmentCount = pendingSegments;
            SyncedSegmentCount = syncedSegments;
            PendingWalBytes = pendingWalBytes;
            LastWalAckOffset = lastWalAckOffset;
            LastSyncTimeUtc = lastSyncTimeUtc;
        }

        public override string ToString()
        {
            return $"PendingSegments={PendingSegmentCount}, SyncedSegments={SyncedSegmentCount}, PendingWalBytes={PendingWalBytes}, LastAckOffset={LastWalAckOffset}, LastSyncUtc={LastSyncTimeUtc}";
        }
    }

    /// <summary>
    /// Store-and-Forward Edge Synchronization Manager.
    /// Enables industrial edge devices and IPCs to operate fully offline during network blackouts,
    /// reliably buffering telemetry on local disk and orchestrating resilient delta replication
    /// and immutable segment transfer once connectivity is restored.
    /// </summary>
    public sealed class StoreAndForwardSync
    {
        private const uint StateMagic = 0x53594E43; // "SYNC"
        private const ushort StateVersion = 1;

        private readonly object _syncRoot = new object();
        private readonly string _dataDirectory;
        private readonly string _syncStateFilePath;
        private readonly HashSet<string> _syncedSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private long _lastWalAckOffset;
        private DateTime? _lastSyncTimeUtc;

        public long LastWalAckOffset
        {
            get { lock (_syncRoot) return _lastWalAckOffset; }
        }

        public DateTime? LastSyncTimeUtc
        {
            get { lock (_syncRoot) return _lastSyncTimeUtc; }
        }

        public StoreAndForwardSync(string dataDirectory)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

            string syncDir = Path.Combine(_dataDirectory, "sync");
            Directory.CreateDirectory(syncDir);
            _syncStateFilePath = Path.Combine(syncDir, "sync_state.dat");

            LoadState();
        }

        /// <summary>
        /// Returns all immutable segment files (.zts) that have not yet been acknowledged by the central server.
        /// Results are returned chronologically sorted by segment sequence.
        /// </summary>
        public List<string> GetPendingSegments()
        {
            lock (_syncRoot)
            {
                string segDir = Path.Combine(_dataDirectory, "segments");
                if (!Directory.Exists(segDir)) return new List<string>();

                var files = Directory.GetFiles(segDir, "*.zts");
                Array.Sort(files);

                var pending = new List<string>(files.Length);
                for (int i = 0; i < files.Length; i++)
                {
                    string fileName = Path.GetFileName(files[i]);
                    if (!_syncedSegments.Contains(fileName))
                    {
                        pending.Add(files[i]);
                    }
                }

                return pending;
            }
        }

        /// <summary>
        /// Marks an immutable segment as acknowledged by upstream and atomically persists state to disk.
        /// </summary>
        public void AcknowledgeSegment(string segmentFilePathOrName)
        {
            if (string.IsNullOrEmpty(segmentFilePathOrName))
                throw new ArgumentException("Segment name cannot be null or empty.", nameof(segmentFilePathOrName));

            string fileName = Path.GetFileName(segmentFilePathOrName);

            lock (_syncRoot)
            {
                if (_syncedSegments.Add(fileName))
                {
                    _lastSyncTimeUtc = DateTime.UtcNow;
                    SaveStateInternal();
                }
            }
        }

        /// <summary>
        /// Computes the pending un-synced byte range in the Write-Ahead Log.
        /// </summary>
        public bool TryGetPendingWalRange(long currentWalLength, out long fromOffset, out long length)
        {
            lock (_syncRoot)
            {
                if (currentWalLength > _lastWalAckOffset)
                {
                    fromOffset = _lastWalAckOffset;
                    length = currentWalLength - _lastWalAckOffset;
                    return true;
                }

                fromOffset = _lastWalAckOffset;
                length = 0;
                return false;
            }
        }

        /// <summary>
        /// Advances the acknowledged WAL byte offset and atomically persists state to disk.
        /// </summary>
        public void AcknowledgeWalOffset(long ackOffset)
        {
            lock (_syncRoot)
            {
                if (ackOffset > _lastWalAckOffset)
                {
                    _lastWalAckOffset = ackOffset;
                    _lastSyncTimeUtc = DateTime.UtcNow;
                    SaveStateInternal();
                }
            }
        }

        /// <summary>
        /// Gets the current synchronization status summary.
        /// </summary>
        public SyncStatus GetStatus(long currentWalLength)
        {
            lock (_syncRoot)
            {
                int pendingSegments = GetPendingSegments().Count;
                int syncedSegments = _syncedSegments.Count;
                long pendingWalBytes = Math.Max(0, currentWalLength - _lastWalAckOffset);

                return new SyncStatus(pendingSegments, syncedSegments, pendingWalBytes, _lastWalAckOffset, _lastSyncTimeUtc);
            }
        }

        #region Persistence

        private void SaveStateInternal()
        {
            string tempFile = _syncStateFilePath + ".tmp";

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(StateMagic);
                bw.Write(StateVersion);
                bw.Write(_lastWalAckOffset);
                bw.Write(_lastSyncTimeUtc?.Ticks ?? 0L);

                bw.Write(_syncedSegments.Count);
                foreach (var seg in _syncedSegments)
                {
                    bw.Write(seg);
                }

                byte[] payload = ms.ToArray();
                uint checksum = FastCrc.Crc32C(payload);

                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(payload);
                    writer.Write(checksum);
                    writer.Flush();
                }
            }

            if (File.Exists(_syncStateFilePath))
            {
                File.Delete(_syncStateFilePath);
            }
            File.Move(tempFile, _syncStateFilePath);
        }

        private void LoadState()
        {
            if (!File.Exists(_syncStateFilePath)) return;

            try
            {
                byte[] rawBytes = File.ReadAllBytes(_syncStateFilePath);
                if (rawBytes.Length < 4 + 2 + 8 + 8 + 4 + 4) return;

                int payloadLen = rawBytes.Length - 4;
                uint expectedChecksum = BitConverter.ToUInt32(rawBytes, payloadLen);
                uint actualChecksum = FastCrc.Crc32C(rawBytes.AsSpan(0, payloadLen));

                if (expectedChecksum != actualChecksum) return; // Corrupted, start fresh

                using (var ms = new MemoryStream(rawBytes, 0, payloadLen))
                using (var br = new BinaryReader(ms))
                {
                    uint magic = br.ReadUInt32();
                    if (magic != StateMagic) return;

                    ushort version = br.ReadUInt16();
                    if (version != StateVersion) return;

                    _lastWalAckOffset = br.ReadInt64();
                    long ticks = br.ReadInt64();
                    if (ticks > 0) _lastSyncTimeUtc = new DateTime(ticks, DateTimeKind.Utc);

                    int count = br.ReadInt32();
                    _syncedSegments.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        _syncedSegments.Add(br.ReadString());
                    }
                }
            }
            catch
            {
                // Fallback gracefully on read error
            }
        }

        #endregion
    }
}
