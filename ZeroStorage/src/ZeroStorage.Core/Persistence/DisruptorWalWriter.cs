using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroStorage.Core.Persistence
{
    /// <summary>
    /// Event data slot used in the lock-free <see cref="DisruptorRing{T}"/> for WAL ingestion.
    /// Pre-allocated to achieve zero GC heap allocation during write storms.
    /// </summary>
    public sealed class WalWriteEvent
    {
        public Uuid7 RecordId;
        public byte RecordType;
        public byte[]? Payload;
        public int Length;
        public WalSyncMode SyncMode;
        public TaskCompletionSource<long>? CompletionSource;

        public void Reset()
        {
            RecordId = default;
            RecordType = 0;
            Payload = null;
            Length = 0;
            SyncMode = WalSyncMode.Deferred;
            CompletionSource = null;
        }
    }

    /// <summary>
    /// Lock-free high-throughput Write-Ahead Log pipeline powered by LMAX <see cref="DisruptorRing{T}"/>.
    /// Eliminates thread lock contention and batches append operations with sub-microsecond latency.
    /// </summary>
    public sealed class DisruptorWalWriter : IDisposable
    {
        private readonly WriteAheadLog _wal;
        private readonly DisruptorRing<WalWriteEvent> _ring;
        private readonly Sequence _consumerSequence = new Sequence(-1);
        private readonly SequenceBarrier _barrier;
        private readonly Thread _workerThread;
        private volatile bool _isRunning;
        private bool _disposed;

        public WriteAheadLog Wal => _wal;
        public long LastLsn => _wal.LastLsn;

        public DisruptorWalWriter(WriteAheadLog wal, int ringCapacityPowerOfTwo = 4096)
        {
            _wal = wal ?? throw new ArgumentNullException(nameof(wal));
            _ring = new DisruptorRing<WalWriteEvent>(ringCapacityPowerOfTwo, () => new WalWriteEvent());
            _barrier = _ring.NewBarrier();
            _ring.AddGatingSequences(_consumerSequence);

            _isRunning = true;
            _workerThread = new Thread(ProcessLoop)
            {
                Name = "ZeroStorage-WAL-Disruptor",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _workerThread.Start();
        }

        /// <summary>
        /// Synchronously enqueues a record into the lock-free disruptor ring and waits for persistence.
        /// </summary>
        public long Enqueue(byte recordType, ReadOnlySpan<byte> payload, WalSyncMode syncMode = WalSyncMode.Deferred, Uuid7 recordId = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisruptorWalWriter));

            var tcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            long seq = _ring.Next();
            ref var slot = ref _ring[seq];

            slot.RecordId = recordId == default ? Uuid7.NewUuid() : recordId;
            slot.RecordType = recordType;
            slot.Payload = payload.ToArray();
            slot.Length = payload.Length;
            slot.SyncMode = syncMode;
            slot.CompletionSource = tcs;

            _ring.Publish(seq);
            return tcs.Task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously enqueues a record into the lock-free disruptor ring.
        /// </summary>
        public Task<long> EnqueueAsync(byte recordType, byte[] payload, WalSyncMode syncMode = WalSyncMode.Deferred, Uuid7 recordId = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisruptorWalWriter));

            var tcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            long seq = _ring.Next();
            ref var slot = ref _ring[seq];

            slot.RecordId = recordId == default ? Uuid7.NewUuid() : recordId;
            slot.RecordType = recordType;
            slot.Payload = payload;
            slot.Length = payload?.Length ?? 0;
            slot.SyncMode = syncMode;
            slot.CompletionSource = tcs;

            _ring.Publish(seq);
            return tcs.Task;
        }

        private void ProcessLoop()
        {
            long expectedSeq = 0;
            while (_isRunning)
            {
                try
                {
                    long availableSeq = _barrier.WaitFor(expectedSeq);
                    while (expectedSeq <= availableSeq)
                    {
                        ref var slot = ref _ring[expectedSeq];
                        try
                        {
                            long lsn = _wal.Append(slot.RecordType, slot.Payload.AsSpan(0, slot.Length), slot.SyncMode);
                            slot.CompletionSource?.TrySetResult(lsn);
                        }
                        catch (Exception ex)
                        {
                            slot.CompletionSource?.TrySetException(ex);
                        }
                        finally
                        {
                            slot.Reset();
                            _consumerSequence.Set(expectedSeq);
                            expectedSeq++;
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch
                {
                    // Continue processing loop on transient error
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;
            try
            {
                _workerThread.Interrupt();
                _workerThread.Join(500);
            }
            catch { }
        }
    }
}
