using System;
using System.Net;
using System.Net.Http;
using ZeroNetwork.Http.Client;

namespace ZeroNetwork.Http
{
    /// <summary>
    /// Configuration options for <see cref="ZeroApiClient"/>.
    /// </summary>
    public class ZeroApiClientOptions
    {
        /// <summary>
        /// Base URI for all relative API requests (e.g. "https://api.erp.local/").
        /// Relative request paths are resolved against the base path, with or without a leading '/'.
        /// </summary>
        public string? BaseAddress { get; set; }

        /// <summary>
        /// Overall timeout of one API call, covering all retry attempts, backoff delays and response body
        /// deserialization (default: 30 seconds). Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable.
        /// For <see cref="ZeroApiClient.GetStreamAsync"/> the timeout applies until response headers are received.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum retry attempts for transient network failures (default: 3).
        /// Set to 0 to disable retry. Only idempotent requests (GET, PUT, DELETE, ...) are retried unless
        /// <see cref="RetryNonIdempotentRequests"/> is enabled.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Initial delay before first retry (default: 200ms, scales exponentially).
        /// </summary>
        public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Upper bound for a single retry delay (default: 30 seconds). A server <c>Retry-After</c> above this value
        /// stops retrying and surfaces the response.
        /// </summary>
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Honors the server <c>Retry-After</c> header on 429/503 responses (default: true).
        /// </summary>
        public bool RespectRetryAfter { get; set; } = true;

        /// <summary>
        /// Retries POST requests on transient failures (default: false).
        /// WARNING: may create duplicate business records when the server processed the request but the response was lost.
        /// </summary>
        public bool RetryNonIdempotentRequests { get; set; }

        /// <summary>
        /// Invoked before each retry delay (logging / metrics).
        /// </summary>
        public Action<ZeroRetryContext>? OnRetry { get; set; }

        /// <summary>
        /// TCP connect timeout (net8.0 only). <c>null</c> keeps the BCL default.
        /// </summary>
        public TimeSpan? ConnectTimeout { get; set; }

        /// <summary>
        /// Explicit proxy. <c>null</c> keeps the platform default proxy behavior.
        /// </summary>
        public IWebProxy? Proxy { get; set; }

        /// <summary>
        /// Escape hatch to customize the created handler (<c>SocketsHttpHandler</c> on net8.0,
        /// <see cref="HttpClientHandler"/> on legacy targets): TLS validation, client certificates, credentials.
        /// </summary>
        public Action<HttpMessageHandler>? ConfigureHandler { get; set; }

        /// <summary>
        /// Default Bearer Token added to the Authorization header of every request.
        /// </summary>
        public string? DefaultBearerToken { get; set; }

        /// <summary>
        /// Default API Key added to the 'x-api-key' header of every request.
        /// </summary>
        public string? DefaultApiKey { get; set; }

        /// <summary>
        /// Enables automatic socket-level GZip, Deflate, and Brotli decompression (default: true).
        /// Saves 70-80% of network transmission bandwidth.
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// How long a pooled TCP connection is kept alive before refreshing DNS (default: 15 minutes).
        /// Prevents DNS staleness issues when servers change IP or failover.
        /// </summary>
        public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Maximum idle time before an unused connection is closed (default: 2 minutes).
        /// </summary>
        public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Maximum concurrent TCP connections allowed per destination endpoint (default: 50).
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = 50;

        /// <summary>
        /// Custom JSON serializer. If null, the default serializer will be used.
        /// </summary>
        public IZeroJsonSerializer? Serializer { get; set; }
    }
}
