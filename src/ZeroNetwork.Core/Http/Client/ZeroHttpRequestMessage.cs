using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Transport-agnostic HTTP request flowing through the <see cref="ZeroHttpClient"/> middleware pipeline.
    /// A single instance may be sent multiple times by retry middleware; transports must not mutate or dispose it.
    /// </summary>
    public sealed class ZeroHttpRequestMessage : IDisposable
    {
        private int _disposed;

        public ZeroHttpRequestMessage(HttpMethod method, Uri requestUri)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            RequestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        }

        public ZeroHttpRequestMessage(HttpMethod method, string requestUri)
            : this(method, ZeroHttpClient.ParseUri(requestUri))
        {
        }

        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// Gets or sets the request URI. Relative URIs are resolved against <see cref="ZeroHttpClient.BaseAddress"/>.
        /// </summary>
        public Uri RequestUri { get; set; }

        /// <summary>
        /// Gets the request headers.
        /// </summary>
        public ZeroHttpHeaders Headers { get; } = new ZeroHttpHeaders();

        /// <summary>
        /// Gets or sets the request body. Owned by this message and disposed with it.
        /// </summary>
        public ZeroHttpContent? Content { get; set; }

        /// <summary>
        /// Overrides idempotency detection for retry decisions.
        /// <c>null</c> (default) derives it from <see cref="Method"/> (GET, HEAD, PUT, DELETE, OPTIONS, TRACE).
        /// Set to <c>true</c> for POST endpoints that are idempotent by contract (e.g. protected by an Idempotency-Key).
        /// </summary>
        public bool? IsIdempotent { get; set; }

        /// <summary>
        /// Gets a per-request property bag for middleware coordination.
        /// </summary>
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

        /// <inheritdoc />
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Content?.Dispose();
            }
        }
    }
}
