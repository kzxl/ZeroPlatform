using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Middleware-pipeline HTTP client. Transport-agnostic: the terminal <see cref="IZeroHttpTransport"/> can be the
    /// BCL (<see cref="BclHttpTransport"/>, default) or any custom engine, optionally routed per request.
    /// Create instances with <see cref="CreateBuilder"/>. Thread-safe; intended to be long-lived.
    /// </summary>
    public sealed class ZeroHttpClient : IDisposable
    {
        private readonly ZeroHttpHandler _pipeline;
        private readonly IReadOnlyList<IDisposable> _ownedResources;
        private int _disposed;

        internal ZeroHttpClient(Uri? baseAddress, ZeroHttpHandler pipeline, IReadOnlyList<IDisposable> ownedResources)
        {
            BaseAddress = baseAddress;
            _pipeline = pipeline;
            _ownedResources = ownedResources;
        }

        /// <summary>
        /// Gets the base address used to resolve relative request URIs (always ends with '/').
        /// </summary>
        public Uri? BaseAddress { get; }

        /// <summary>
        /// Creates a new pipeline builder.
        /// </summary>
        public static ZeroHttpClientBuilder CreateBuilder() => new ZeroHttpClientBuilder();

        /// <summary>
        /// Sends the request through the middleware pipeline. The caller owns both request and response.
        /// </summary>
        public Task<ZeroHttpResponseMessage> SendAsync(ZeroHttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(ZeroHttpClient));

            request.RequestUri = ResolveUri(BaseAddress, request.RequestUri);
            return _pipeline(request, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            for (int i = 0; i < _ownedResources.Count; i++)
            {
                try { _ownedResources[i].Dispose(); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Parses a user-supplied URI string. Paths such as "/orders" stay relative on every OS
        /// (on Unix, <see cref="Uri"/> would otherwise parse them as absolute file URIs).
        /// </summary>
        internal static Uri ParseUri(string? uri)
        {
            if (string.IsNullOrEmpty(uri)) return new Uri(string.Empty, UriKind.Relative); // targets the base address
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute) && !IsUnixRootedPath(absolute, uri!))
            {
                return absolute;
            }
            return new Uri(uri!, UriKind.Relative);
        }

        /// <summary>
        /// Resolves <paramref name="uri"/> against <paramref name="baseAddress"/>.
        /// A leading '/' is treated as relative to the base path (not the host root), so
        /// base "https://erp/api/v1/" + "/orders" yields "https://erp/api/v1/orders".
        /// </summary>
        internal static Uri ResolveUri(Uri? baseAddress, Uri uri)
        {
            if (uri.IsAbsoluteUri && !IsUnixRootedPath(uri, uri.OriginalString)) return uri;
            if (baseAddress == null) return uri;

            string relative = uri.OriginalString.TrimStart('/');
            return new Uri(baseAddress, relative);
        }

        internal static Uri? NormalizeBaseAddress(Uri? baseAddress)
        {
            if (baseAddress == null) return null;
            if (!baseAddress.IsAbsoluteUri) throw new ArgumentException("Base address must be an absolute URI.", nameof(baseAddress));
            if (baseAddress.AbsolutePath.EndsWith("/", StringComparison.Ordinal)) return baseAddress;

            // Append '/' to the path (not the raw string) so a query or fragment stays intact.
            var builder = new UriBuilder(baseAddress);
            builder.Path += "/";
            return builder.Uri;
        }

        private static bool IsUnixRootedPath(Uri uri, string original)
        {
            return uri.IsFile && original.StartsWith("/", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Fluent builder for <see cref="ZeroHttpClient"/>. Middleware registered first runs outermost.
    /// </summary>
    public sealed class ZeroHttpClientBuilder
    {
        private readonly List<IZeroHttpMiddleware> _middlewares = new List<IZeroHttpMiddleware>();
        private readonly List<ZeroTransportRoute> _routes = new List<ZeroTransportRoute>();
        private readonly List<IDisposable> _ownedResources = new List<IDisposable>();
        private IZeroHttpTransport? _defaultTransport;
        private Uri? _baseAddress;

        /// <summary>Sets the base address for relative request URIs.</summary>
        public ZeroHttpClientBuilder WithBaseAddress(Uri? baseAddress)
        {
            _baseAddress = ZeroHttpClient.NormalizeBaseAddress(baseAddress);
            return this;
        }

        /// <summary>Sets the base address for relative request URIs.</summary>
        public ZeroHttpClientBuilder WithBaseAddress(string? baseAddress)
        {
            return WithBaseAddress(string.IsNullOrWhiteSpace(baseAddress) ? null : new Uri(baseAddress, UriKind.Absolute));
        }

        /// <summary>Appends a middleware.</summary>
        public ZeroHttpClientBuilder Use(IZeroHttpMiddleware middleware)
        {
            _middlewares.Add(middleware ?? throw new ArgumentNullException(nameof(middleware)));
            return this;
        }

        /// <summary>Appends an inline middleware.</summary>
        public ZeroHttpClientBuilder Use(Func<ZeroHttpRequestMessage, ZeroHttpHandler, CancellationToken, Task<ZeroHttpResponseMessage>> middleware)
        {
            return Use(new ZeroDelegateMiddleware(middleware));
        }

        /// <summary>Appends <see cref="ZeroRetryMiddleware"/>.</summary>
        public ZeroHttpClientBuilder UseRetry(Action<ZeroRetryOptions>? configure = null)
        {
            var options = new ZeroRetryOptions();
            configure?.Invoke(options);
            return Use(new ZeroRetryMiddleware(options));
        }

        /// <summary>Appends <see cref="ZeroBearerTokenMiddleware"/> with a static token.</summary>
        public ZeroHttpClientBuilder UseBearerToken(string token) => Use(new ZeroBearerTokenMiddleware(token));

        /// <summary>Appends <see cref="ZeroBearerTokenMiddleware"/> with a dynamic token provider.</summary>
        public ZeroHttpClientBuilder UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider) => Use(new ZeroBearerTokenMiddleware(tokenProvider));

        /// <summary>Appends <see cref="ZeroApiKeyMiddleware"/>.</summary>
        public ZeroHttpClientBuilder UseApiKey(string apiKey, string headerName = "x-api-key") => Use(new ZeroApiKeyMiddleware(apiKey, headerName));

        /// <summary>Appends <see cref="ZeroTelemetryMiddleware"/>.</summary>
        public ZeroHttpClientBuilder UseTelemetry(string metricPrefix = "zero_http_client") => Use(new ZeroTelemetryMiddleware(metricPrefix));

        /// <summary>
        /// Sets the default transport. When omitted, an owned <see cref="BclHttpTransport"/> is created.
        /// </summary>
        public ZeroHttpClientBuilder UseTransport(IZeroHttpTransport transport, bool disposeWithClient = true)
        {
            _defaultTransport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (disposeWithClient) _ownedResources.Add(transport);
            return this;
        }

        /// <summary>
        /// Routes requests matching <paramref name="predicate"/> to <paramref name="transport"/>.
        /// Routes are evaluated in registration order; unmatched requests use the default transport.
        /// </summary>
        public ZeroHttpClientBuilder RouteTransport(Func<ZeroHttpRequestMessage, bool> predicate, IZeroHttpTransport transport, bool disposeWithClient = true)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            _routes.Add(new ZeroTransportRoute(predicate, transport));
            if (disposeWithClient) _ownedResources.Add(transport);
            return this;
        }

        /// <summary>
        /// Routes requests whose host matches a wildcard pattern (e.g. <c>10.0.*.*</c>, <c>*.plc.local</c>).
        /// </summary>
        public ZeroHttpClientBuilder RouteHost(string hostPattern, IZeroHttpTransport transport, bool disposeWithClient = true)
        {
            if (string.IsNullOrWhiteSpace(hostPattern)) throw new ArgumentNullException(nameof(hostPattern));

            var regex = new Regex(
                "^" + Regex.Escape(hostPattern.Trim()).Replace("\\*", "[^/]*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return RouteTransport(
                request => request.RequestUri.IsAbsoluteUri && regex.IsMatch(request.RequestUri.Host),
                transport,
                disposeWithClient);
        }

        /// <summary>
        /// Builds the client. The builder should not be reused afterwards.
        /// </summary>
        public ZeroHttpClient Build()
        {
            var owned = new List<IDisposable>(_ownedResources);

            IZeroHttpTransport defaultTransport = _defaultTransport ?? CreateOwnedDefaultTransport(owned);
            ZeroHttpHandler terminal = CreateTerminal(defaultTransport, _routes.ToArray());

            ZeroHttpHandler pipeline = terminal;
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                IZeroHttpMiddleware middleware = _middlewares[i];
                ZeroHttpHandler next = pipeline;
                pipeline = (request, cancellationToken) => middleware.InvokeAsync(request, next, cancellationToken);
            }

            return new ZeroHttpClient(_baseAddress, pipeline, owned);
        }

        private static IZeroHttpTransport CreateOwnedDefaultTransport(List<IDisposable> owned)
        {
            var transport = new BclHttpTransport();
            owned.Add(transport);
            return transport;
        }

        private static ZeroHttpHandler CreateTerminal(IZeroHttpTransport defaultTransport, ZeroTransportRoute[] routes)
        {
            if (routes.Length == 0)
            {
                return defaultTransport.SendAsync;
            }

            return (request, cancellationToken) =>
            {
                for (int i = 0; i < routes.Length; i++)
                {
                    if (routes[i].Predicate(request))
                    {
                        return routes[i].Transport.SendAsync(request, cancellationToken);
                    }
                }
                return defaultTransport.SendAsync(request, cancellationToken);
            };
        }

        private sealed class ZeroTransportRoute
        {
            public ZeroTransportRoute(Func<ZeroHttpRequestMessage, bool> predicate, IZeroHttpTransport transport)
            {
                Predicate = predicate;
                Transport = transport;
            }

            public Func<ZeroHttpRequestMessage, bool> Predicate { get; }
            public IZeroHttpTransport Transport { get; }
        }
    }
}
