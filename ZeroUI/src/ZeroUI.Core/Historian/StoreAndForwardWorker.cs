using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Background worker orchestrating Store-and-Forward synchronization between
    /// the local SQLite historian and a central enterprise time-series database.
    /// </summary>
    public sealed class StoreAndForwardWorker : IDisposable
    {
        private readonly IHistorianEngine _localEngine;
        private readonly Func<CancellationToken, Task<bool>> _connectivityCheck;
        private readonly Func<IReadOnlyList<HistorianRecord>, CancellationToken, Task<bool>> _forwardHandler;
        private readonly int _batchSize;
        private readonly TimeSpan _pollInterval;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _workerTask;
        private bool _isDisposed;
        private bool _isOnline;
        private long _totalSyncedCount;
        private DateTime? _lastSyncTime;

        public bool IsOnline => _isOnline;
        public long TotalSyncedCount => Interlocked.Read(ref _totalSyncedCount);
        public DateTime? LastSyncTime => _lastSyncTime;

        public StoreAndForwardWorker(
            IHistorianEngine localEngine,
            Func<CancellationToken, Task<bool>> connectivityCheck,
            Func<IReadOnlyList<HistorianRecord>, CancellationToken, Task<bool>> forwardHandler,
            int batchSize = 1000,
            TimeSpan? pollInterval = null)
        {
            _localEngine = localEngine ?? throw new ArgumentNullException(nameof(localEngine));
            _connectivityCheck = connectivityCheck ?? throw new ArgumentNullException(nameof(connectivityCheck));
            _forwardHandler = forwardHandler ?? throw new ArgumentNullException(nameof(forwardHandler));
            _batchSize = Math.Max(10, batchSize);
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Starts the background Store-and-Forward synchronization loop.
        /// </summary>
        public void Start()
        {
            if (_workerTask != null) return;
            _workerTask = Task.Run(SyncLoopAsync);
        }

        private async Task SyncLoopAsync()
        {
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool online = false;
                    try
                    {
                        online = await _connectivityCheck(token).ConfigureAwait(false);
                    }
                    catch
                    {
                        online = false;
                    }

                    _isOnline = online;

                    if (online)
                    {
                        var batch = await _localEngine.ReadUnsyncedBatchAsync(_batchSize, token).ConfigureAwait(false);
                        if (batch.Count > 0)
                        {
                            bool success = await _forwardHandler(batch, token).ConfigureAwait(false);
                            if (success)
                            {
                                var latestRecord = batch[batch.Count - 1];
                                await _localEngine.MarkSyncedAsync(latestRecord.Id, latestRecord.Timestamp.Date, token).ConfigureAwait(false);
                                Interlocked.Add(ref _totalSyncedCount, batch.Count);
                                _lastSyncTime = DateTime.UtcNow;

                                // If full batch was processed, loop immediately for remaining
                                if (batch.Count >= _batchSize)
                                {
                                    continue;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Guard loop against unhandled driver exceptions
                }

                try
                {
                    await Task.Delay(_pollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
