using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ZeroUI.Core.Data;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// High-performance embedded time-series historian leveraging SQLite with Write-Ahead Logging (WAL).
    /// Features:
    /// - Persistent active partition connection & prepared command caching (eliminates per-batch connection/DDL overhead).
    /// - Rolling daily database partitions and asynchronous batch commits.
    /// - Explicit WAL checkpointing (Passive, Full, Restart, Truncate) and file storage observability metrics.
    /// - Sub-millisecond LTTB decimation range queries.
    /// - Store-and-Forward synchronization for edge-to-cloud reliability.
    /// </summary>
    public sealed class SqliteHistorianEngine : IHistorianEngine
    {
        private readonly string _storageDirectory;
        private readonly int _batchSize;
        private readonly int _flushIntervalMs;
        private readonly ConcurrentQueue<HistorianRecord> _ingestionQueue = new ConcurrentQueue<HistorianRecord>();
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isDisposed;

        private static readonly TelemetryResolution[] RollupResolutions = new[]
        {
            TelemetryResolution.L1_100ms,
            TelemetryResolution.L2_1s,
            TelemetryResolution.L3_10s,
            TelemetryResolution.L4_1m,
            TelemetryResolution.L5_10m
        };

        // Cached active partition connection & prepared statement
        private SqliteConnection? _activeConnection;
        private SqliteCommand? _activeInsertCmd;
        private SqliteParameter? _pPath;
        private SqliteParameter? _pVal;
        private SqliteParameter? _pQuality;
        private SqliteParameter? _pTs;

        // Prepared rollup upsert statement
        private SqliteCommand? _activeRollupCmd;
        private SqliteParameter? _rpPath;
        private SqliteParameter? _rpRes;
        private SqliteParameter? _rpBTime;
        private SqliteParameter? _rpMin;
        private SqliteParameter? _rpMax;
        private SqliteParameter? _rpSum;
        private SqliteParameter? _rpLast;
        private SqliteParameter? _rpCnt;
        private SqliteParameter? _rpQual;

        private DateTime _activePartitionDate = DateTime.MinValue;
        private TimeSpan _lastCheckpointDuration = TimeSpan.Zero;

        /// <summary>
        /// Initializes a new instance of SqliteHistorianEngine.
        /// </summary>
        /// <param name="storageDirectory">Folder path to store daily SQLite databases.</param>
        /// <param name="batchSize">Number of records per transactional batch flush.</param>
        /// <param name="flushIntervalMs">Maximum duration in ms before flushing pending buffer.</param>
        public SqliteHistorianEngine(
            string? storageDirectory = null,
            int batchSize = 1000,
            int flushIntervalMs = 500)
        {
            _storageDirectory = storageDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Historian");
            _batchSize = Math.Max(1, batchSize);
            _flushIntervalMs = Math.Max(10, flushIntervalMs);

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            _flushTimer = new Timer(_ => _ = FlushAsync(_cts.Token), null, _flushIntervalMs, _flushIntervalMs);
        }

        /// <inheritdoc />
        public void LogSample(string tagPath, double value, ScadaQuality quality = ScadaQuality.Good, DateTime? timestamp = null)
        {
            if (string.IsNullOrWhiteSpace(tagPath) || _isDisposed) return;
            var time = timestamp ?? DateTime.UtcNow;
            _ingestionQueue.Enqueue(new HistorianRecord(tagPath, value, quality, time));

            if (_ingestionQueue.Count >= _batchSize)
            {
                _ = FlushAsync(_cts.Token);
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_ingestionQueue.IsEmpty || _isDisposed) return;

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_ingestionQueue.IsEmpty) return;

                // Group records by partition date
                var batchesByDate = new Dictionary<DateTime, List<HistorianRecord>>();
                while (_ingestionQueue.TryDequeue(out var record))
                {
                    var dateKey = record.Timestamp.Date;
                    if (!batchesByDate.TryGetValue(dateKey, out var list))
                    {
                        list = new List<HistorianRecord>(_batchSize);
                        batchesByDate[dateKey] = list;
                    }
                    list.Add(record);
                }

                foreach (var kvp in batchesByDate)
                {
                    await WriteBatchToPartitionAsync(kvp.Key, kvp.Value, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task WriteBatchToPartitionAsync(DateTime date, List<HistorianRecord> records, CancellationToken ct)
        {
            // Reuse open partition connection and prepared command to avoid per-batch DDL checks
            if (_activeConnection == null || _activePartitionDate != date || _activeInsertCmd == null || _activeRollupCmd == null)
            {
                CloseActivePartition();

                var dbPath = GetPartitionPath(date);
                var connStr = $"Data Source={dbPath};";

                var connection = new SqliteConnection(connStr);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                InitializeDatabasePragmas(connection);

                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO TagHistory (TagPath, Val, Quality, Timestamp, IsSynced)
                    VALUES ($path, $val, $quality, $ts, 0);";

                var pPath = cmd.CreateParameter();
                pPath.ParameterName = "$path";
                cmd.Parameters.Add(pPath);

                var pVal = cmd.CreateParameter();
                pVal.ParameterName = "$val";
                cmd.Parameters.Add(pVal);

                var pQuality = cmd.CreateParameter();
                pQuality.ParameterName = "$quality";
                cmd.Parameters.Add(pQuality);

                var pTs = cmd.CreateParameter();
                pTs.ParameterName = "$ts";
                cmd.Parameters.Add(pTs);

                // Prepared rollup upsert command
                var rollupCmd = connection.CreateCommand();
                rollupCmd.CommandText = @"
                    INSERT INTO TagHistory_Rollup (TagPath, Resolution, BucketTime, MinVal, MaxVal, SumVal, LastVal, Count, Quality)
                    VALUES ($r_path, $r_res, $r_btime, $r_min, $r_max, $r_sum, $r_last, $r_cnt, $r_qual)
                    ON CONFLICT(TagPath, Resolution, BucketTime) DO UPDATE SET
                        MinVal = MIN(TagHistory_Rollup.MinVal, excluded.MinVal),
                        MaxVal = MAX(TagHistory_Rollup.MaxVal, excluded.MaxVal),
                        SumVal = TagHistory_Rollup.SumVal + excluded.SumVal,
                        LastVal = excluded.LastVal,
                        Count = TagHistory_Rollup.Count + excluded.Count,
                        Quality = CASE WHEN excluded.Quality != 192 THEN excluded.Quality ELSE TagHistory_Rollup.Quality END;";

                var rpPath = rollupCmd.CreateParameter(); rpPath.ParameterName = "$r_path"; rollupCmd.Parameters.Add(rpPath);
                var rpRes = rollupCmd.CreateParameter(); rpRes.ParameterName = "$r_res"; rollupCmd.Parameters.Add(rpRes);
                var rpBTime = rollupCmd.CreateParameter(); rpBTime.ParameterName = "$r_btime"; rollupCmd.Parameters.Add(rpBTime);
                var rpMin = rollupCmd.CreateParameter(); rpMin.ParameterName = "$r_min"; rollupCmd.Parameters.Add(rpMin);
                var rpMax = rollupCmd.CreateParameter(); rpMax.ParameterName = "$r_max"; rollupCmd.Parameters.Add(rpMax);
                var rpSum = rollupCmd.CreateParameter(); rpSum.ParameterName = "$r_sum"; rollupCmd.Parameters.Add(rpSum);
                var rpLast = rollupCmd.CreateParameter(); rpLast.ParameterName = "$r_last"; rollupCmd.Parameters.Add(rpLast);
                var rpCnt = rollupCmd.CreateParameter(); rpCnt.ParameterName = "$r_cnt"; rollupCmd.Parameters.Add(rpCnt);
                var rpQual = rollupCmd.CreateParameter(); rpQual.ParameterName = "$r_qual"; rollupCmd.Parameters.Add(rpQual);

                _activeConnection = connection;
                _activeInsertCmd = cmd;
                _pPath = pPath;
                _pVal = pVal;
                _pQuality = pQuality;
                _pTs = pTs;

                _activeRollupCmd = rollupCmd;
                _rpPath = rpPath;
                _rpRes = rpRes;
                _rpBTime = rpBTime;
                _rpMin = rpMin;
                _rpMax = rpMax;
                _rpSum = rpSum;
                _rpLast = rpLast;
                _rpCnt = rpCnt;
                _rpQual = rpQual;

                _activePartitionDate = date;
            }

            using var transaction = _activeConnection.BeginTransaction();
            _activeInsertCmd.Transaction = transaction;

            var rollupBuckets = new Dictionary<RollupKey, RollupBucket>();

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                long tsMs = new DateTimeOffset(r.Timestamp).ToUnixTimeMilliseconds();

                _pPath!.Value = r.TagPath;
                _pVal!.Value = r.Value;
                _pQuality!.Value = (int)r.Quality;
                _pTs!.Value = tsMs;

                await _activeInsertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                // Continuous multi-resolution rollup aggregation
                for (int resIdx = 0; resIdx < RollupResolutions.Length; resIdx++)
                {
                    var res = RollupResolutions[resIdx];
                    long durationMs = res.GetBucketDurationMs();
                    long bTime = (tsMs / durationMs) * durationMs;
                    var key = new RollupKey(r.TagPath, res, bTime);

                    if (!rollupBuckets.TryGetValue(key, out var bucket))
                    {
                        rollupBuckets[key] = new RollupBucket(r.TagPath, res, bTime, r.Value, r.Quality);
                    }
                    else
                    {
                        bucket.AddSample(r.Value, r.Quality);
                    }
                }
            }

            if (rollupBuckets.Count > 0 && _activeRollupCmd != null)
            {
                _activeRollupCmd.Transaction = transaction;
                foreach (var bucket in rollupBuckets.Values)
                {
                    _rpPath!.Value = bucket.TagPath;
                    _rpRes!.Value = (int)(byte)bucket.Resolution;
                    _rpBTime!.Value = bucket.BucketTimeMs;
                    _rpMin!.Value = bucket.MinVal;
                    _rpMax!.Value = bucket.MaxVal;
                    _rpSum!.Value = bucket.SumVal;
                    _rpLast!.Value = bucket.LastVal;
                    _rpCnt!.Value = bucket.Count;
                    _rpQual!.Value = (int)bucket.Quality;

                    await _activeRollupCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }

            transaction.Commit();
        }

        /// <summary>
        /// Executes an explicit WAL checkpoint on the target partition, measuring and recording latency.
        /// </summary>
        public async Task<TimeSpan> CheckpointAsync(
            DateTime? partitionDate = null,
            SqliteCheckpointMode mode = SqliteCheckpointMode.Truncate,
            CancellationToken ct = default)
        {
            var date = partitionDate ?? DateTime.UtcNow.Date;
            var sw = Stopwatch.StartNew();

            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var dbPath = GetPartitionPath(date);
                if (!File.Exists(dbPath)) return TimeSpan.Zero;

                if (_activeConnection != null && _activePartitionDate == date)
                {
                    using var cmd = _activeConnection.CreateCommand();
                    cmd.CommandText = $"PRAGMA wal_checkpoint({mode.ToString().ToUpperInvariant()});";
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    var connStr = $"Data Source={dbPath};";
                    using var conn = new SqliteConnection(connStr);
                    await conn.OpenAsync(ct).ConfigureAwait(false);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"PRAGMA wal_checkpoint({mode.ToString().ToUpperInvariant()});";
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                _writeLock.Release();
            }

            sw.Stop();
            _lastCheckpointDuration = sw.Elapsed;
            return sw.Elapsed;
        }

        /// <summary>
        /// Queries disk storage, WAL file size, record count, and checkpoint metrics for observability.
        /// </summary>
        public HistorianStorageMetrics GetStorageMetrics(DateTime? partitionDate = null)
        {
            var date = partitionDate ?? DateTime.UtcNow.Date;
            var dbPath = GetPartitionPath(date);
            var walPath = dbPath + "-wal";

            long dbSize = File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
            long walSize = File.Exists(walPath) ? new FileInfo(walPath).Length : 0;
            long totalRecords = 0;

            if (File.Exists(dbPath))
            {
                try
                {
                    var connStr = $"Data Source={dbPath};Mode=ReadOnly;";
                    using var conn = new SqliteConnection(connStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM TagHistory;";
                    var res = cmd.ExecuteScalar();
                    if (res != null && long.TryParse(res.ToString(), out long count))
                    {
                        totalRecords = count;
                    }
                }
                catch
                {
                    // Fallback if locked
                }
            }

            return new HistorianStorageMetrics(date, dbPath, dbSize, walSize, totalRecords, _lastCheckpointDuration);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<TimePoint>> QueryDecimatedAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints = 1000,
            CancellationToken cancellationToken = default)
        {
            return QueryDecimatedAsync(tagPath, startTime, endTime, targetPoints, null, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TimePoint>> QueryDecimatedAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints,
            TelemetryResolution? resolution,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tagPath) || startTime >= endTime)
                return Array.Empty<TimePoint>();

            var selectedTier = resolution ?? TelemetryResolutionExtensions.SelectOptimalResolution(endTime - startTime);

            // Tier 0 (Raw): Scan raw table directly
            if (selectedTier == TelemetryResolution.Raw)
            {
                return await QueryRawDecimatedInternalAsync(tagPath, startTime, endTime, targetPoints, cancellationToken).ConfigureAwait(false);
            }

            // Tier 1..5: Query from pre-aggregated rollups
            var points = new List<TimePoint>();
            long startMs = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
            long endMs = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();
            long durationMs = selectedTier.GetBucketDurationMs();

            for (var curDate = startTime.Date; curDate <= endTime.Date; curDate = curDate.AddDays(1))
            {
                var dbPath = GetPartitionPath(curDate);
                if (!File.Exists(dbPath)) continue;

                var connStr = $"Data Source={dbPath};Mode=ReadOnly;";
                using var connection = new SqliteConnection(connStr);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT BucketTime, MinVal, MaxVal, LastVal, Count
                    FROM TagHistory_Rollup
                    WHERE TagPath = $path AND Resolution = $res AND BucketTime >= $start AND BucketTime <= $end
                    ORDER BY BucketTime ASC;";

                cmd.Parameters.AddWithValue("$path", tagPath);
                cmd.Parameters.AddWithValue("$res", (int)(byte)selectedTier);
                cmd.Parameters.AddWithValue("$start", startMs);
                cmd.Parameters.AddWithValue("$end", endMs);

                try
                {
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        long bTime = reader.GetInt64(0);
                        double min = reader.GetDouble(1);
                        double max = reader.GetDouble(2);
                        double last = reader.GetDouble(3);
                        int count = reader.GetInt32(4);

                        if (count <= 1 || Math.Abs(max - min) < 1e-9)
                        {
                            points.Add(new TimePoint(bTime, last));
                        }
                        else
                        {
                            // Preserve waveform peak envelopes (Min followed by Max within bucket)
                            points.Add(new TimePoint(bTime, min));
                            points.Add(new TimePoint(bTime + durationMs / 2, max));
                        }
                    }
                }
                catch (SqliteException)
                {
                    // Table might not exist in older partitions; fallback handled below
                }
            }

            // Fallback to raw if rollups are empty (e.g. legacy partitions before rollups were activated)
            if (points.Count == 0)
            {
                return await QueryRawDecimatedInternalAsync(tagPath, startTime, endTime, targetPoints, cancellationToken).ConfigureAwait(false);
            }

            if (points.Count <= targetPoints)
            {
                return points;
            }

            var decimated = new TimePoint[targetPoints];
            int written = LttbDecimation.Downsample(points, decimated, targetPoints);

            var result = new TimePoint[written];
            Array.Copy(decimated, result, written);
            return result;
        }

        private async Task<IReadOnlyList<TimePoint>> QueryRawDecimatedInternalAsync(
            string tagPath,
            DateTime startTime,
            DateTime endTime,
            int targetPoints,
            CancellationToken cancellationToken)
        {
            var rawPoints = new List<TimePoint>();
            long startMs = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
            long endMs = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();

            for (var curDate = startTime.Date; curDate <= endTime.Date; curDate = curDate.AddDays(1))
            {
                var dbPath = GetPartitionPath(curDate);
                if (!File.Exists(dbPath)) continue;

                var connStr = $"Data Source={dbPath};Mode=ReadOnly;";
                using var connection = new SqliteConnection(connStr);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Timestamp, Val
                    FROM TagHistory
                    WHERE TagPath = $path AND Timestamp >= $start AND Timestamp <= $end
                    ORDER BY Timestamp ASC;";

                cmd.Parameters.AddWithValue("$path", tagPath);
                cmd.Parameters.AddWithValue("$start", startMs);
                cmd.Parameters.AddWithValue("$end", endMs);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    double x = reader.GetInt64(0);
                    double y = reader.GetDouble(1);
                    rawPoints.Add(new TimePoint(x, y));
                }
            }

            if (rawPoints.Count == 0) return Array.Empty<TimePoint>();
            if (rawPoints.Count <= targetPoints) return rawPoints;

            var decimated = new TimePoint[targetPoints];
            int written = LttbDecimation.Downsample(rawPoints, decimated, targetPoints);

            var result = new TimePoint[written];
            Array.Copy(decimated, result, written);
            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<HistorianRecord>> ReadUnsyncedBatchAsync(int batchSize = 1000, CancellationToken cancellationToken = default)
        {
            var results = new List<HistorianRecord>();
            var today = DateTime.UtcNow.Date;

            for (var d = today.AddDays(-7); d <= today; d = d.AddDays(1))
            {
                var dbPath = GetPartitionPath(d);
                if (!File.Exists(dbPath)) continue;

                var connStr = $"Data Source={dbPath};";
                using var connection = new SqliteConnection(connStr);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, TagPath, Val, Quality, Timestamp
                    FROM TagHistory
                    WHERE IsSynced = 0
                    ORDER BY Id ASC
                    LIMIT $limit;";
                cmd.Parameters.AddWithValue("$limit", batchSize - results.Count);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    long id = reader.GetInt64(0);
                    string path = reader.GetString(1);
                    double val = reader.GetDouble(2);
                    var quality = (ScadaQuality)reader.GetInt32(3);
                    var ts = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)).UtcDateTime;

                    results.Add(new HistorianRecord(path, val, quality, ts, id));
                }

                if (results.Count >= batchSize) break;
            }

            return results;
        }

        /// <inheritdoc />
        public async Task MarkSyncedAsync(long upToId, DateTime partitionDate, CancellationToken cancellationToken = default)
        {
            var dbPath = GetPartitionPath(partitionDate);
            if (!File.Exists(dbPath)) return;

            var connStr = $"Data Source={dbPath};";
            using var connection = new SqliteConnection(connStr);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE TagHistory SET IsSynced = 1 WHERE Id <= $upToId AND IsSynced = 0;";
            cmd.Parameters.AddWithValue("$upToId", upToId);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<int> PurgeExpiredPartitionsAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            if (retentionDays <= 0) return Task.FromResult(0);
            int purgedCount = 0;
            var cutoffDate = DateTime.UtcNow.Date.AddDays(-retentionDays);

            var files = Directory.GetFiles(_storageDirectory, "Historian_*.db");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("Historian_", StringComparison.OrdinalIgnoreCase))
                {
                    var datePart = fileName.Substring("Historian_".Length);
                    if (DateTime.TryParseExact(datePart, "yyyy_MM_dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoffDate)
                        {
                            try
                            {
                                File.Delete(file);
                                var walFile = file + "-wal";
                                var shmFile = file + "-shm";
                                if (File.Exists(walFile)) File.Delete(walFile);
                                if (File.Exists(shmFile)) File.Delete(shmFile);
                                purgedCount++;
                            }
                            catch
                            {
                                // In-use lock protection
                            }
                        }
                    }
                }
            }

            return Task.FromResult(purgedCount);
        }

        private string GetPartitionPath(DateTime date)
        {
            var fileName = $"Historian_{date:yyyy_MM_dd}.db";
            return Path.Combine(_storageDirectory, fileName);
        }

        private static void InitializeDatabasePragmas(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                PRAGMA cache_size = -64000;
                PRAGMA page_size = 4096;
                PRAGMA wal_autocheckpoint = 10000;

                CREATE TABLE IF NOT EXISTS TagHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TagPath TEXT NOT NULL,
                    Val REAL NOT NULL,
                    Quality INTEGER NOT NULL,
                    Timestamp INTEGER NOT NULL,
                    IsSynced INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IX_TagHistory_Tag_Time ON TagHistory (TagPath, Timestamp);
                CREATE INDEX IF NOT EXISTS IX_TagHistory_Synced ON TagHistory (IsSynced, Id);

                CREATE TABLE IF NOT EXISTS TagHistory_Rollup (
                    TagPath TEXT NOT NULL,
                    Resolution INTEGER NOT NULL,
                    BucketTime INTEGER NOT NULL,
                    MinVal REAL NOT NULL,
                    MaxVal REAL NOT NULL,
                    SumVal REAL NOT NULL,
                    LastVal REAL NOT NULL,
                    Count INTEGER NOT NULL,
                    Quality INTEGER NOT NULL,
                    PRIMARY KEY (TagPath, Resolution, BucketTime)
                );
                CREATE INDEX IF NOT EXISTS IX_TagHistory_Rollup_Lookup ON TagHistory_Rollup (TagPath, Resolution, BucketTime);
            ";
            cmd.ExecuteNonQuery();
        }

        private void CloseActivePartition()
        {
            try
            {
                _activeInsertCmd?.Dispose();
                _activeRollupCmd?.Dispose();
                _activeConnection?.Close();
                _activeConnection?.Dispose();
            }
            catch
            {
                // Cleanup guard
            }
            finally
            {
                _activeInsertCmd = null;
                _activeRollupCmd = null;
                _activeConnection = null;
                _pPath = null;
                _pVal = null;
                _pQuality = null;
                _pTs = null;
                _rpPath = null;
                _rpRes = null;
                _rpBTime = null;
                _rpMin = null;
                _rpMax = null;
                _rpSum = null;
                _rpLast = null;
                _rpCnt = null;
                _rpQual = null;
                _activePartitionDate = DateTime.MinValue;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _flushTimer.Dispose();
            _cts.Cancel();
            _cts.Dispose();
            CloseActivePartition();
            _writeLock.Dispose();
        }
    }
}
