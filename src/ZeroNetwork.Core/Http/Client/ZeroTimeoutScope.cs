using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Combines a caller token with an operation timeout and translates the resulting failures into
    /// BCL-compatible exceptions (<see cref="TaskCanceledException"/> with an inner <see cref="TimeoutException"/>).
    /// </summary>
    internal sealed class ZeroTimeoutScope : IDisposable
    {
        private readonly TimeSpan _timeout;
        private readonly CancellationToken _callerToken;
        private readonly CancellationTokenSource? _timeoutCts;
        private readonly CancellationTokenSource? _linkedCts;

        public ZeroTimeoutScope(TimeSpan timeout, CancellationToken callerToken)
        {
            _timeout = timeout;
            _callerToken = callerToken;

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                Token = callerToken;
                return;
            }

            _timeoutCts = new CancellationTokenSource(timeout);
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _timeoutCts.Token);
            Token = _linkedCts.Token;
        }

        /// <summary>Gets the token combining caller cancellation and timeout.</summary>
        public CancellationToken Token { get; }

        /// <summary>Gets a value indicating whether the timeout (not the caller) cancelled the operation.</summary>
        public bool TimedOut => _timeoutCts != null && _timeoutCts.IsCancellationRequested && !_callerToken.IsCancellationRequested;

        /// <summary>
        /// Returns a translated exception, or <c>null</c> when <paramref name="exception"/> should propagate unchanged.
        /// </summary>
        public Exception? Translate(Exception exception)
        {
            if (TimedOut)
            {
                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "The request was canceled due to the configured Timeout of {0} seconds elapsing.",
                    _timeout.TotalSeconds);
                return new TaskCanceledException(message, new TimeoutException(message, exception));
            }

            if (_callerToken.IsCancellationRequested)
            {
                // Re-associate with the caller token (the inner exception may carry the internal linked token),
                // and normalize aborted body reads (IOException/ObjectDisposedException) into cancellation.
                return new OperationCanceledException("The operation was canceled.", exception, _callerToken);
            }

            return null;
        }

        public static void Validate(TimeSpan timeout, string paramName)
        {
            if (timeout != Timeout.InfiniteTimeSpan && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue))
            {
                throw new ArgumentOutOfRangeException(paramName, timeout, "Timeout must be positive and at most Int32.MaxValue milliseconds, or Timeout.InfiniteTimeSpan.");
            }
        }

        public void Dispose()
        {
            _linkedCts?.Dispose();
            _timeoutCts?.Dispose();
        }
    }
}
