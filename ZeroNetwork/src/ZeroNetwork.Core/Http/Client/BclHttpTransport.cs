using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Options for the default handler created by <see cref="BclHttpTransport"/>.
    /// </summary>
    public sealed class BclHttpTransportOptions
    {
        /// <summary>
        /// Lifetime of a pooled connection before it is recycled to refresh DNS (net8.0 only; default: 15 minutes).
        /// </summary>
        public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Idle time before an unused pooled connection is closed (net8.0 only; default: 2 minutes).
        /// </summary>
        public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Maximum concurrent connections per endpoint (net8.0 only; default: 50).
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = 50;

        /// <summary>
        /// TCP connect timeout (net8.0 only). <c>null</c> keeps the BCL default (bounded only by the request timeout).
        /// </summary>
        public TimeSpan? ConnectTimeout { get; set; }

        /// <summary>
        /// Enables automatic GZip/Deflate (and Brotli on net8.0) response decompression (default: true).
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Explicit proxy. <c>null</c> keeps the platform default proxy behavior.
        /// </summary>
        public IWebProxy? Proxy { get; set; }

        /// <summary>
        /// Escape hatch invoked after defaults are applied. Receives a <c>SocketsHttpHandler</c> on net8.0 and an
        /// <see cref="HttpClientHandler"/> on legacy targets (TLS validation, client certificates, credentials, ...).
        /// </summary>
        public Action<HttpMessageHandler>? ConfigureHandler { get; set; }
    }

    /// <summary>
    /// Default transport delegating to <see cref="HttpClient"/>. Keeps BCL capabilities that should never be
    /// re-implemented (authenticated proxies, platform TLS, connection pooling) behind the ZeroNetwork pipeline.
    /// Requests are currently sent as HTTP/1.1.
    /// </summary>
    public sealed class BclHttpTransport : IZeroHttpTransport
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;
        private readonly BclHttpTransportOptions? _options;
        private int _disposed;

        /// <summary>
        /// Creates a transport with an optimized, owned handler. The inner <see cref="HttpClient.Timeout"/> is infinite:
        /// timeouts are enforced by the caller (e.g. <see cref="ZeroApiClient"/>) across the whole operation.
        /// </summary>
        public BclHttpTransport(BclHttpTransportOptions? options = null)
        {
            _options = options ?? new BclHttpTransportOptions();
            HttpMessageHandler handler = CreateHandler(_options);
            _httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
            _disposeClient = true;

#if !NET8_0_OR_GREATER
            if (_options.MaxConnectionsPerServer > 0)
            {
                try
                {
                    if (ServicePointManager.DefaultConnectionLimit < _options.MaxConnectionsPerServer)
                    {
                        ServicePointManager.DefaultConnectionLimit = _options.MaxConnectionsPerServer;
                    }
                }
                catch
                {
                    // Best effort on environments with restricted security policies
                }
            }
#endif
        }

        /// <summary>
        /// Wraps an existing <see cref="HttpClient"/> (e.g. from <c>IHttpClientFactory</c>).
        /// </summary>
        public BclHttpTransport(HttpClient httpClient, bool disposeClient = false)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _disposeClient = disposeClient;
        }

        /// <summary>
        /// Gets the underlying <see cref="HttpClient"/>.
        /// </summary>
        public HttpClient HttpClient => _httpClient;

        /// <inheritdoc />
        public async Task<ZeroHttpResponseMessage> SendAsync(ZeroHttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(BclHttpTransport));

#if !NET8_0_OR_GREATER
            if (request.RequestUri.IsAbsoluteUri)
            {
                ConfigureLegacyServicePoint(request.RequestUri);
            }
            else if (_httpClient.BaseAddress != null && _httpClient.BaseAddress.IsAbsoluteUri)
            {
                ConfigureLegacyServicePoint(_httpClient.BaseAddress);
            }
#endif

            // A fresh HttpRequestMessage per attempt: BCL messages cannot be re-sent, and on .NET Framework
            // HttpClient disposes request content after sending. The adapter never disposes the Zero content.
            HttpRequestMessage message = CreateMessage(request, cancellationToken);
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                var headers = new ZeroHttpHeaders();
                CopyHeaders(response.Headers, headers);

                Stream body;
                if (response.Content != null)
                {
                    CopyHeaders(response.Content.Headers, headers);
#if NET5_0_OR_GREATER
                    body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                    body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                }
                else
                {
                    body = Stream.Null;
                }

                return new ZeroHttpResponseMessage(
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    headers,
                    body,
                    new CompositeDisposable(response, message));
            }
            catch
            {
                response?.Dispose();
                message.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_disposeClient) _httpClient.Dispose();
        }

        private static HttpRequestMessage CreateMessage(ZeroHttpRequestMessage request, CancellationToken cancellationToken)
        {
            var message = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content != null)
            {
                var content = new ContentAdapter(request.Content, cancellationToken);
                if (!string.IsNullOrEmpty(request.Content.ContentType))
                {
                    content.Headers.TryAddWithoutValidation("Content-Type", request.Content.ContentType);
                }
                foreach (var header in request.Content.Headers)
                {
                    AddContentHeader(content, header.Key, header.Value);
                }
                message.Content = content;
            }

            foreach (var header in request.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value) && message.Content != null)
                {
                    // Content-level header placed on the request (e.g. Content-Encoding); requires a body.
                    AddContentHeader(message.Content, header.Key, header.Value);
                }
            }

            return message;
        }

        private static void AddContentHeader(HttpContent content, string name, string value)
        {
            // Single-value content headers: the latest explicit value wins instead of producing "a, b".
            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                content.Headers.Remove(name);
            }
            content.Headers.TryAddWithoutValidation(name, value);
        }

        private static void CopyHeaders(System.Net.Http.Headers.HttpHeaders source, ZeroHttpHeaders target)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in source)
            {
                foreach (string value in header.Value)
                {
                    target.AddWithoutValidation(header.Key, value);
                }
            }
        }

        private static HttpMessageHandler CreateHandler(BclHttpTransportOptions options)
        {
#if NET8_0_OR_GREATER
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = options.PooledConnectionLifetime,
                PooledConnectionIdleTimeout = options.PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = options.MaxConnectionsPerServer,
                AutomaticDecompression = options.EnableCompression
                    ? DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
                    : DecompressionMethods.None
            };
            if (options.ConnectTimeout.HasValue)
            {
                handler.ConnectTimeout = options.ConnectTimeout.Value;
            }
#else
            var handler = new HttpClientHandler();
            if (options.EnableCompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
#endif
            if (options.Proxy != null)
            {
                handler.Proxy = options.Proxy;
                handler.UseProxy = true;
            }

            options.ConfigureHandler?.Invoke(handler);
            return handler;
        }

#if !NET8_0_OR_GREATER
        private void ConfigureLegacyServicePoint(Uri uri)
        {
            if (_options == null || !uri.IsAbsoluteUri) return;
            try
            {
                var sp = ServicePointManager.FindServicePoint(uri);
                if (sp != null)
                {
                    if (_options.MaxConnectionsPerServer > 0 && sp.ConnectionLimit < _options.MaxConnectionsPerServer)
                    {
                        sp.ConnectionLimit = _options.MaxConnectionsPerServer;
                    }
                    if (_options.PooledConnectionLifetime > TimeSpan.Zero)
                    {
                        sp.ConnectionLeaseTimeout = (int)Math.Min(_options.PooledConnectionLifetime.TotalMilliseconds, int.MaxValue);
                    }
                    if (_options.PooledConnectionIdleTimeout > TimeSpan.Zero)
                    {
                        sp.MaxIdleTime = (int)Math.Min(_options.PooledConnectionIdleTimeout.TotalMilliseconds, int.MaxValue);
                    }
                }
            }
            catch
            {
                // Best effort on environments where ServicePointManager is constrained
            }
        }
#endif

        private sealed class ContentAdapter : HttpContent
        {
            private readonly ZeroHttpContent _inner;
            private readonly CancellationToken _cancellationToken;

            public ContentAdapter(ZeroHttpContent inner, CancellationToken cancellationToken)
            {
                _inner = inner;
                _cancellationToken = cancellationToken;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => _inner.CopyToAsync(stream, _cancellationToken);

#if NET5_0_OR_GREATER
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
                => _inner.CopyToAsync(stream, cancellationToken);
#endif

            protected override bool TryComputeLength(out long length)
            {
                long? known = _inner.Length;
                length = known ?? 0;
                return known.HasValue;
            }

            // Intentionally does not dispose _inner: the ZeroHttpRequestMessage owns it across retries.
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IDisposable _first;
            private readonly IDisposable _second;

            public CompositeDisposable(IDisposable first, IDisposable second)
            {
                _first = first;
                _second = second;
            }

            public void Dispose()
            {
                _first.Dispose();
                _second.Dispose();
            }
        }
    }
}
