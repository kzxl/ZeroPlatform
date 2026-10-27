using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Channels
{
    /// <summary>
    /// Thread-safe, low-allocation half-duplex request-response channel for industrial protocols
    /// (Siemens S7comm, Mitsubishi MELSEC MC 3E, Modbus RTU, Omron FINS TCP).
    /// Serializes concurrent dispatchers, manages pending completion, and guarantees deterministic timeout cleanup.
    /// </summary>
    public sealed class HalfDuplexChannel<TResponse> : IDisposable
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private TaskCompletionSource<TResponse>? _pendingResponse;
        private bool _isDisposed;

        /// <summary>
        /// Gets whether a request is currently awaiting a response.
        /// </summary>
        public bool HasPendingRequest
        {
            get
            {
                lock (_stateLock)
                {
                    return _pendingResponse != null;
                }
            }
        }

        /// <summary>
        /// Dispatches a request action and asynchronously awaits the correlated response from the transport.
        /// </summary>
        /// <param name="sendAction">The delegate that transmits the request bytes over transport.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">External cancellation token.</param>
        /// <returns>The extracted response frame.</returns>
        public async Task<TResponse> ExecuteRequestAsync(
            Func<CancellationToken, Task> sendAction,
            int timeoutMs,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            TaskCompletionSource<TResponse> tcs;

            lock (_stateLock)
            {
                tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingResponse = tcs;
            }

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            CancellationTokenRegistration reg = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetException(new TimeoutException($"Industrial protocol request timed out after {timeoutMs}ms."));
                }
                else
                {
                    tcs.TrySetCanceled();
                }
            });

            try
            {
                await sendAction(linkedCts.Token).ConfigureAwait(false);
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                reg.Dispose();
                lock (_stateLock)
                {
                    if (ReferenceEquals(_pendingResponse, tcs))
                    {
                        _pendingResponse = null;
                    }
                }
                _gate.Release();
            }
        }

        /// <summary>
        /// Attempts to complete the currently pending request with the received response frame.
        /// Returns true if a pending request was satisfied; false if the response was unexpected.
        /// </summary>
        public bool TrySetResponse(TResponse response)
        {
            TaskCompletionSource<TResponse>? tcs;
            lock (_stateLock)
            {
                tcs = _pendingResponse;
                _pendingResponse = null;
            }

            return tcs != null && tcs.TrySetResult(response);
        }

        /// <summary>
        /// Faults the currently pending request when the transport is severed or an unrecoverable error occurs.
        /// </summary>
        public void FaultPending(Exception exception)
        {
            TaskCompletionSource<TResponse>? tcs;
            lock (_stateLock)
            {
                tcs = _pendingResponse;
                _pendingResponse = null;
            }

            tcs?.TrySetException(exception);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HalfDuplexChannel<TResponse>));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            FaultPending(new ObjectDisposedException(nameof(HalfDuplexChannel<TResponse>)));
            _gate.Dispose();
        }
    }
}
