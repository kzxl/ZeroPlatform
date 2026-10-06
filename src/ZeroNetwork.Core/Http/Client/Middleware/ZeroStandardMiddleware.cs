using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Diagnostics;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Adds <c>Authorization: Bearer</c> when the request does not already carry an Authorization header.
    /// </summary>
    public sealed class ZeroBearerTokenMiddleware : IZeroHttpMiddleware
    {
        private readonly Func<CancellationToken, Task<string?>> _tokenProvider;

        public ZeroBearerTokenMiddleware(string token)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            Task<string?> cached = Task.FromResult<string?>(token);
            _tokenProvider = _ => cached;
        }

        /// <param name="tokenProvider">Resolves the current token (e.g. from a token cache with refresh).</param>
        public ZeroBearerTokenMiddleware(Func<CancellationToken, Task<string?>> tokenProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        /// <inheritdoc />
        public async Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains("Authorization"))
            {
                string? token = await _tokenProvider(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Set("Authorization", "Bearer " + token);
                }
            }
            return await next(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds an API key header when the request does not already carry it.
    /// </summary>
    public sealed class ZeroApiKeyMiddleware : IZeroHttpMiddleware
    {
        private readonly string _apiKey;
        private readonly string _headerName;

        public ZeroApiKeyMiddleware(string apiKey, string headerName = "x-api-key")
        {
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));
            if (string.IsNullOrEmpty(headerName)) throw new ArgumentNullException(nameof(headerName));
            _apiKey = apiKey;
            _headerName = headerName;
        }

        /// <inheritdoc />
        public Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains(_headerName))
            {
                request.Headers.Set(_headerName, _apiKey);
            }
            return next(request, cancellationToken);
        }
    }

    /// <summary>
    /// Records request metrics into <see cref="ZeroTelemetry"/> (exported by <see cref="ZeroHttpServer"/> <c>/metrics</c>).
    /// Metrics: <c>{prefix}_requests_total</c>, <c>{prefix}_unsuccessful_responses_total</c>,
    /// <c>{prefix}_errors_total</c>, <c>{prefix}_request_duration_ms</c> (time to response headers).
    /// Register it after retry middleware to observe every attempt, or before to observe logical calls.
    /// </summary>
    public sealed class ZeroTelemetryMiddleware : IZeroHttpMiddleware
    {
        private readonly string _requestsName;
        private readonly string _unsuccessfulName;
        private readonly string _errorsName;
        private readonly string _durationName;

        public ZeroTelemetryMiddleware(string metricPrefix = "zero_http_client")
        {
            if (string.IsNullOrWhiteSpace(metricPrefix)) throw new ArgumentNullException(nameof(metricPrefix));
            _requestsName = metricPrefix + "_requests_total";
            _unsuccessfulName = metricPrefix + "_unsuccessful_responses_total";
            _errorsName = metricPrefix + "_errors_total";
            _durationName = metricPrefix + "_request_duration_ms";
        }

        /// <inheritdoc />
        public async Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken)
        {
            // Metrics are resolved per call (cheap dictionary lookup) so ZeroTelemetry.ResetAll() never orphans them.
            ZeroTelemetry.CreateCounter(_requestsName, "Total HTTP client requests.").Increment();
            long start = Stopwatch.GetTimestamp();
            try
            {
                ZeroHttpResponseMessage response = await next(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    ZeroTelemetry.CreateCounter(_unsuccessfulName, "HTTP client responses with non-2xx status.").Increment();
                }
                return response;
            }
            catch
            {
                ZeroTelemetry.CreateCounter(_errorsName, "HTTP client requests failed with an exception.").Increment();
                throw;
            }
            finally
            {
                double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                ZeroTelemetry.CreateHistogram(_durationName, "HTTP client time to response headers (ms).").Record(elapsedMs);
            }
        }
    }
}
