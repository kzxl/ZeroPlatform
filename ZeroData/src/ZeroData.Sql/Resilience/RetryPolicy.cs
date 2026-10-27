using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql.Resilience
{
    /// <summary>
    /// Configures retry policies for transient database errors (deadlocks, connection drops, network timeouts).
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Maximum number of retry attempts.
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// Initial delay before the first retry.
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Maximum backoff delay cap.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Multiplier for exponential backoff.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// When true (default), applies random jitter (+/- 20% variance) to backoff delays to prevent thundering herd deadlocks.
        /// </summary>
        public bool EnableJitter { get; set; } = true;

        /// <summary>
        /// Optional callback invoked on each retry attempt: (Exception ex, TimeSpan nextDelay, int attemptNumber).
        /// Useful for enterprise logging and diagnostics.
        /// </summary>
        public Action<Exception, TimeSpan, int> OnRetry { get; set; }

        /// <summary>
        /// Optional custom predicate to classify transient exceptions.
        /// </summary>
        public Func<Exception, bool> TransientPredicate { get; set; }

        private static readonly Random _rng = new Random();
        private static readonly object _rngLock = new object();

        /// <summary>
        /// Default retry policy instance.
        /// </summary>
        public static RetryPolicy Default => new RetryPolicy();

        private TimeSpan ComputeDelay(TimeSpan currentDelay)
        {
            if (!EnableJitter) return currentDelay;

            double factor;
            lock (_rngLock)
            {
                factor = 0.8 + (_rng.NextDouble() * 0.4); // 0.8x to 1.2x
            }

            var ms = currentDelay.TotalMilliseconds * factor;
            return TimeSpan.FromMilliseconds(Math.Min(ms, MaxDelay.TotalMilliseconds));
        }

        /// <summary>
        /// Determines whether an exception represents a transient database failure.
        /// </summary>
        public bool IsTransient(Exception ex)
        {
            if (ex == null) return false;
            if (TransientPredicate != null && TransientPredicate(ex)) return true;

            if (ex is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (IsTransient(inner)) return true;
                }
            }

            if (ex.InnerException != null && IsTransient(ex.InnerException))
                return true;

            if (ex is TimeoutException) return true;

            var typeName = ex.GetType().FullName ?? "";
            var message = ex.Message ?? "";

            if (message.IndexOf("deadlock", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("transport-level", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (typeName.IndexOf("SocketException", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                foreach (Microsoft.Data.SqlClient.SqlError err in sqlEx.Errors)
                {
                    switch (err.Number)
                    {
                        case 1205: // Deadlock
                        case -2:   // Client timeout
                        case 20:   // Connection broken
                        case 64:   // Connection error
                        case 233:  // Connection initialization error
                        case 10053:
                        case 10054: // Connection reset
                        case 10060: // Network timeout
                        case 40197: // Azure error processing request
                        case 40501: // Azure service busy
                        case 40613: // Azure database unavailable
                        case 49918:
                        case 49919:
                        case 49920:
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Executes a synchronous operation with transient fault retries.
        /// </summary>
        public T Execute<T>(Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int attempts = 0;
            var delay = InitialDelay;

            while (true)
            {
                try
                {
                    attempts++;
                    return operation();
                }
                catch (Exception ex) when (attempts <= MaxRetryCount && IsTransient(ex))
                {
                    var actualDelay = ComputeDelay(delay);
                    OnRetry?.Invoke(ex, actualDelay, attempts);
                    Thread.Sleep(actualDelay);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
                }
            }
        }

        /// <summary>
        /// Asynchronously executes an operation with transient fault retries.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int attempts = 0;
            var delay = InitialDelay;

            while (true)
            {
                try
                {
                    attempts++;
                    return await operation(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempts <= MaxRetryCount && IsTransient(ex))
                {
                    var actualDelay = ComputeDelay(delay);
                    OnRetry?.Invoke(ex, actualDelay, attempts);
                    await Task.Delay(actualDelay, ct).ConfigureAwait(false);
                    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
                }
            }
        }
    }
}
