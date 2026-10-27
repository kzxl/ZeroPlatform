using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Configuration for <see cref="ZeroRetryMiddleware"/>.
    /// </summary>
    public sealed class ZeroRetryOptions
    {
        /// <summary>
        /// Maximum retry attempts after the initial attempt (default: 3). 0 disables retry.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay for exponential backoff (default: 200 ms).
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Upper bound for a single backoff delay (default: 30 s). A server <c>Retry-After</c> larger than this
        /// stops retrying and returns the response instead of hammering the server early.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Honors <c>Retry-After</c> (delta-seconds or HTTP-date) on retryable responses (default: true).
        /// </summary>
        public bool RespectRetryAfter { get; set; } = true;

        /// <summary>
        /// Allows retrying non-idempotent requests (POST, PATCH) (default: false).
        /// Enabling this risks duplicate side effects (e.g. duplicate orders) when the server processed the
        /// request but the response was lost. Prefer <see cref="ZeroHttpRequestMessage.IsIdempotent"/> per request.
        /// </summary>
        public bool RetryNonIdempotentRequests { get; set; }

        /// <summary>
        /// Status codes treated as transient. <c>null</c> uses 408, 429, 502, 503, 504.
        /// </summary>
        public ISet<int>? TransientStatusCodes { get; set; }

        /// <summary>
        /// Invoked before each retry delay (logging, metrics).
        /// </summary>
        public Action<ZeroRetryContext>? OnRetry { get; set; }
    }

    /// <summary>
    /// Information about a scheduled retry.
    /// </summary>
    public sealed class ZeroRetryContext
    {
        internal ZeroRetryContext(ZeroHttpRequestMessage request, int attempt, TimeSpan delay, int? statusCode, Exception? exception)
        {
            Request = request;
            Attempt = attempt;
            Delay = delay;
            StatusCode = statusCode;
            Exception = exception;
        }

        /// <summary>Gets the request being retried.</summary>
        public ZeroHttpRequestMessage Request { get; }

        /// <summary>Gets the 1-based number of the attempt that just failed.</summary>
        public int Attempt { get; }

        /// <summary>Gets the delay before the next attempt.</summary>
        public TimeSpan Delay { get; }

        /// <summary>Gets the transient status code, when the failure was a response.</summary>
        public int? StatusCode { get; }

        /// <summary>Gets the transient exception, when the failure was an exception.</summary>
        public Exception? Exception { get; }
    }

    /// <summary>
    /// Idempotency-aware retry with exponential backoff, equal jitter, delay cap and <c>Retry-After</c> support.
    /// </summary>
    public sealed class ZeroRetryMiddleware : IZeroHttpMiddleware
    {
        private static readonly HashSet<int> s_defaultTransientStatusCodes = new HashSet<int> { 408, 429, 502, 503, 504 };
        private static readonly TimeSpan s_maxTaskDelay = TimeSpan.FromMilliseconds(int.MaxValue - 1);

        private readonly Func<ZeroRetryOptions> _optionsAccessor;

        public ZeroRetryMiddleware(ZeroRetryOptions? options = null)
        {
            var resolved = options ?? new ZeroRetryOptions();
            _optionsAccessor = () => resolved;
        }

        /// <summary>
        /// Reads options on every invocation so callers can keep a mutable options object live.
        /// </summary>
        internal ZeroRetryMiddleware(Func<ZeroRetryOptions> optionsAccessor)
        {
            _optionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        /// <inheritdoc />
        public async Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken)
        {
            ZeroRetryOptions options = _optionsAccessor();

            if (options.MaxRetryAttempts <= 0 || !IsRetryEligible(request, options))
            {
                return await next(request, cancellationToken).ConfigureAwait(false);
            }

            ISet<int> transientStatusCodes = options.TransientStatusCodes ?? s_defaultTransientStatusCodes;
            int attempt = 0;

            while (true)
            {
                attempt++;
                cancellationToken.ThrowIfCancellationRequested();

                ZeroHttpResponseMessage? response = null;
                ExceptionDispatchInfo? failure = null;

                try
                {
                    response = await next(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTransientException(ex, cancellationToken))
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }

                if (response != null && !transientStatusCodes.Contains(response.StatusCode))
                {
                    return response;
                }

                TimeSpan delay = TimeSpan.Zero;
                bool canRetry = attempt <= options.MaxRetryAttempts && TryComputeDelay(response, attempt, options, out delay);

                if (!canRetry)
                {
                    if (response != null) return response;
                    failure!.Throw(); // preserves the original stack trace
                }

                int? retriedStatusCode = response?.StatusCode;
                response?.Dispose(); // release the connection before user code runs
                options.OnRetry?.Invoke(new ZeroRetryContext(request, attempt, delay, retriedStatusCode, failure?.SourceException));

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal static bool IsIdempotentMethod(HttpMethod method)
        {
            return method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put ||
                   method == HttpMethod.Delete || method == HttpMethod.Options || method == HttpMethod.Trace;
        }

        private static bool IsRetryEligible(ZeroHttpRequestMessage request, ZeroRetryOptions options)
        {
            bool idempotent = request.IsIdempotent ?? IsIdempotentMethod(request.Method);
            if (!idempotent && !options.RetryNonIdempotentRequests) return false;

            // A one-shot body cannot be sent twice.
            return request.Content == null || request.Content.IsReplayable;
        }

        private static bool IsTransientException(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            return ex is HttpRequestException || ex is System.Net.Sockets.SocketException || ex is System.IO.IOException || ex is TimeoutException;
        }

        private static bool TryComputeDelay(ZeroHttpResponseMessage? response, int attempt, ZeroRetryOptions options, out TimeSpan delay)
        {
            TimeSpan maxDelay = options.MaxDelay < TimeSpan.Zero ? TimeSpan.Zero : options.MaxDelay;
            if (maxDelay > s_maxTaskDelay) maxDelay = s_maxTaskDelay; // Task.Delay limit

            if (options.RespectRetryAfter && response != null && TryGetRetryAfter(response, out TimeSpan retryAfter))
            {
                if (retryAfter > maxDelay)
                {
                    // The server asked us to back off longer than we are allowed to wait: surface the response.
                    delay = TimeSpan.Zero;
                    return false;
                }
                delay = retryAfter;
                return true;
            }

            double baseMs = Math.Max(0, options.InitialDelay.TotalMilliseconds) * Math.Pow(2, attempt - 1);
            double capMs = Math.Min(baseMs, maxDelay.TotalMilliseconds);

            // Equal jitter: [cap/2, cap). Avoids synchronized retries while keeping a meaningful minimum delay.
            delay = TimeSpan.FromMilliseconds(capMs / 2 + ZeroJitter.NextDouble() * capMs / 2);
            return true;
        }

        internal static bool TryGetRetryAfter(ZeroHttpResponseMessage response, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            string? raw = response.Headers.Get("Retry-After");
            if (string.IsNullOrWhiteSpace(raw)) return false;

            raw = raw!.Trim();
            if (long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            {
                retryAfter = TimeSpan.FromSeconds(Math.Min(seconds, int.MaxValue));
                return true;
            }

            if (DateTimeOffset.TryParseExact(raw, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
            {
                TimeSpan delta = date - DateTimeOffset.UtcNow;
                retryAfter = delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Thread-safe jitter source. Avoids <c>new Random()</c> per call, which on .NET Framework is seeded from
    /// <c>Environment.TickCount</c> and yields identical sequences for concurrent callers.
    /// </summary>
    internal static class ZeroJitter
    {
#if NET6_0_OR_GREATER
        public static double NextDouble() => Random.Shared.NextDouble();
#else
        [ThreadStatic]
        private static Random? t_random;
        private static int s_seed = Environment.TickCount;

        public static double NextDouble()
        {
            Random random = t_random ??= new Random(Interlocked.Increment(ref s_seed) ^ Guid.NewGuid().GetHashCode());
            return random.NextDouble();
        }
#endif
    }
}
