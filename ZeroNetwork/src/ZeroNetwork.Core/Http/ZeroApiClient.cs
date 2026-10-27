using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Http.Client;

namespace ZeroNetwork.Http
{
    /// <summary>
    /// High-performance, stream-based JSON API client built on the <see cref="ZeroHttpClient"/> middleware pipeline.
    /// Responses are deserialized directly from the network stream; request bodies are buffered in pooled chunks
    /// (known Content-Length, retry-safe, no LOH copies). Includes idempotency-aware retry, an operation-wide timeout
    /// and connection pooling through the default <see cref="BclHttpTransport"/>.
    /// </summary>
    public class ZeroApiClient : IDisposable
    {
        private static volatile IZeroJsonSerializer _defaultSerializer =
#if NET8_0_OR_GREATER
            new SystemTextJsonStreamSerializer();
#else
            new FallbackStreamSerializer();
#endif

        /// <summary>
        /// Gets or sets the global default JSON serializer used by every <see cref="ZeroApiClient"/> that has no
        /// explicit serializer. Read at call time, so it can be configured after clients (including
        /// <see cref="ZeroHttpExtensions.Shared"/>) were created.
        /// </summary>
        public static IZeroJsonSerializer DefaultSerializer
        {
            get => _defaultSerializer;
            set => _defaultSerializer = value ?? throw new ArgumentNullException(nameof(value));
        }

        private readonly ZeroHttpClient _client;
        private readonly bool _ownsClient;
        private readonly ZeroApiClientOptions _options;
        private readonly IZeroJsonSerializer? _explicitSerializer;
        private readonly TimeSpan _timeout;
        private int _disposed;

        public ZeroApiClientOptions Options => _options;

        /// <summary>
        /// Gets the effective serializer (explicit serializer, otherwise the current <see cref="DefaultSerializer"/>).
        /// </summary>
        public IZeroJsonSerializer Serializer => _explicitSerializer ?? DefaultSerializer;

        /// <summary>
        /// Gets the underlying middleware pipeline (for raw <see cref="ZeroHttpRequestMessage"/> calls).
        /// </summary>
        public ZeroHttpClient Pipeline => _client;

        public ZeroApiClient(ZeroApiClientOptions? options = null)
        {
            _options = options ?? new ZeroApiClientOptions();
            ZeroTimeoutScope.Validate(_options.Timeout, nameof(options));

            _explicitSerializer = _options.Serializer;
            _timeout = _options.Timeout;

            Uri? baseAddress = string.IsNullOrWhiteSpace(_options.BaseAddress)
                ? null
                : new Uri(_options.BaseAddress!.TrimEnd('/') + "/");

            var transport = new BclHttpTransport(new BclHttpTransportOptions
            {
                PooledConnectionLifetime = _options.PooledConnectionLifetime,
                PooledConnectionIdleTimeout = _options.PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
                EnableCompression = _options.EnableCompression,
                ConnectTimeout = _options.ConnectTimeout,
                Proxy = _options.Proxy,
                ConfigureHandler = _options.ConfigureHandler
            });

            _client = BuildDefaultPipeline(transport, baseAddress);
            _ownsClient = true;
        }

        public ZeroApiClient(string baseAddress) : this(new ZeroApiClientOptions { BaseAddress = baseAddress })
        {
        }

        /// <summary>
        /// Wraps an existing <see cref="HttpClient"/> (not disposed by this instance). Retry uses default options;
        /// the overall timeout follows <see cref="HttpClient.Timeout"/> and also bounds body deserialization.
        /// </summary>
        public ZeroApiClient(HttpClient httpClient, IZeroJsonSerializer? serializer = null)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));

            _options = new ZeroApiClientOptions();
            _explicitSerializer = serializer;
            _timeout = httpClient.Timeout;

            // No base address in the pipeline: HttpClient.BaseAddress keeps resolving relative URIs exactly as before
            // (including later changes to it and RFC 3986 leading-slash semantics).
            _client = BuildDefaultPipeline(new BclHttpTransport(httpClient, disposeClient: false), null);
            _ownsClient = true; // owns the pipeline wrapper only; the HttpClient itself is never disposed here
        }

        /// <summary>
        /// Uses a caller-built pipeline (custom middleware / transports). The pipeline is not disposed by this instance.
        /// Retry, auth and transport behavior are entirely defined by <paramref name="client"/>.
        /// </summary>
        /// <param name="client">Pre-configured pipeline.</param>
        /// <param name="serializer">Explicit serializer; <c>null</c> uses <see cref="DefaultSerializer"/>.</param>
        /// <param name="timeout">Overall call timeout; <c>null</c> uses 30 seconds.</param>
        public ZeroApiClient(ZeroHttpClient client, IZeroJsonSerializer? serializer = null, TimeSpan? timeout = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = new ZeroApiClientOptions { MaxRetryAttempts = 0 };
            _explicitSerializer = serializer;
            _timeout = timeout ?? _options.Timeout;
            ZeroTimeoutScope.Validate(_timeout, nameof(timeout));
            _ownsClient = false;
        }

        private ZeroHttpClient BuildDefaultPipeline(IZeroHttpTransport transport, Uri? baseAddress)
        {
            return ZeroHttpClient.CreateBuilder()
                .WithBaseAddress(baseAddress)
                // Options are read per call so runtime changes to client.Options keep working as before.
                .Use(new ZeroRetryMiddleware(CreateRetryOptions))
                .UseTransport(transport, disposeWithClient: true)
                .Build();
        }

        private ZeroRetryOptions CreateRetryOptions()
        {
            return new ZeroRetryOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                InitialDelay = _options.InitialRetryDelay,
                MaxDelay = _options.MaxRetryDelay,
                RespectRetryAfter = _options.RespectRetryAfter,
                RetryNonIdempotentRequests = _options.RetryNonIdempotentRequests,
                OnRetry = _options.OnRetry
            };
        }

        /// <summary>
        /// Sends an HTTP GET request and streams response directly into <typeparamref name="T"/> without intermediate string allocations.
        /// Returns <c>default</c> for responses without a body (204, 205, 304, Content-Length: 0).
        /// </summary>
        public Task<T?> GetAsync<T>(
            string uri,
            object? queryParams = null,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            string fullUri = AppendQueryParams(uri, queryParams);
            return SendJsonAsync<T>(HttpMethod.Get, fullUri, null, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP POST request with a JSON payload and streams response directly into <typeparamref name="TResult"/>.
        /// POST is not retried unless <see cref="ZeroApiClientOptions.RetryNonIdempotentRequests"/> is enabled.
        /// </summary>
        public async Task<TResult?> PostAsync<TRequest, TResult>(
            string uri,
            TRequest payload,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ZeroHttpContent content = await ZeroPooledContent.SerializeJsonAsync(Serializer, payload, cancellationToken).ConfigureAwait(false);
            return await SendJsonAsync<TResult>(HttpMethod.Post, uri, content, bearerToken, apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an HTTP PUT request with a JSON payload and streams response directly into <typeparamref name="TResult"/>.
        /// </summary>
        public async Task<TResult?> PutAsync<TRequest, TResult>(
            string uri,
            TRequest payload,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ZeroHttpContent content = await ZeroPooledContent.SerializeJsonAsync(Serializer, payload, cancellationToken).ConfigureAwait(false);
            return await SendJsonAsync<TResult>(HttpMethod.Put, uri, content, bearerToken, apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an HTTP DELETE request and streams response directly into <typeparamref name="TResult"/>.
        /// Returns <c>default</c> for <c>204 No Content</c>.
        /// </summary>
        public Task<TResult?> DeleteAsync<TResult>(
            string uri,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return SendJsonAsync<TResult>(HttpMethod.Delete, uri, null, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP GET request and returns the raw response stream.
        /// Caller is responsible for disposing the returned Stream; disposing it releases the connection.
        /// The configured timeout applies until response headers are received.
        /// </summary>
        public async Task<Stream> GetStreamAsync(
            string uri,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            ZeroHttpRequestMessage request = CreateRequest(HttpMethod.Get, uri, null, bearerToken, apiKey);
            ZeroHttpResponseMessage? response = null;

            using (var scope = new ZeroTimeoutScope(_timeout, cancellationToken))
            {
                try
                {
                    response = await _client.SendAsync(request, scope.Token).ConfigureAwait(false);
                    EnsureSuccessStatusCode(response);
                    return response.DetachBody(request);
                }
                catch (Exception ex)
                {
                    response?.Dispose();
                    request.Dispose();

                    Exception? translated = scope.Translate(ex);
                    if (translated != null) throw translated;
                    throw;
                }
            }
        }

        private async Task<T?> SendJsonAsync<T>(
            HttpMethod method,
            string uri,
            ZeroHttpContent? content,
            string? bearerToken,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            ZeroHttpRequestMessage request;
            try
            {
                ThrowIfDisposed();
                request = CreateRequest(method, uri, content, bearerToken, apiKey);
            }
            catch
            {
                content?.Dispose();
                throw;
            }

            using (request)
            using (var scope = new ZeroTimeoutScope(_timeout, cancellationToken))
            {
                try
                {
                    using (ZeroHttpResponseMessage response = await _client.SendAsync(request, scope.Token).ConfigureAwait(false))
                    {
                        EnsureSuccessStatusCode(response);

                        if (response.HasNoBody)
                        {
                            return EmptyResult<T>();
                        }

                        return await DeserializeBodyAsync<T>(response, scope.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Exception? translated = scope.Translate(ex);
                    if (translated != null) throw translated;
                    throw;
                }
            }
        }

        /// <summary>
        /// Deserializes the body while guaranteeing that cancellation/timeout interrupts a stalled read.
        /// </summary>
        private async Task<T?> DeserializeBodyAsync<T>(ZeroHttpResponseMessage response, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            // Modern response streams honor the token; disposing the response is a second line of defense.
            using (cancellationToken.Register(state => ((IDisposable)state!).Dispose(), response))
            {
                return await DeserializeCoreAsync<T>(response.Body, cancellationToken).ConfigureAwait(false);
            }
#else
            // Legacy stacks (HttpWebRequest) may ignore the token, and disposing an unread response can drain the
            // connection synchronously. Abort on a pool thread and stop awaiting the read once cancelled.
            using (cancellationToken.Register(
                state => ThreadPool.QueueUserWorkItem(r => ((IDisposable)r!).Dispose(), state),
                response))
            {
                Task<T?> work = DeserializeCoreAsync<T>(response.Body, cancellationToken);
                if (!work.IsCompleted)
                {
                    var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
                    {
                        Task finished = await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false);
                        if (!ReferenceEquals(finished, work))
                        {
                            // Observe the orphaned read so its eventual fault is not reported as unobserved.
                            _ = work.ContinueWith(
                                t => _ = t.Exception,
                                CancellationToken.None,
                                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                                TaskScheduler.Default);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
                return await work.ConfigureAwait(false);
            }
#endif
        }

        private async Task<T?> DeserializeCoreAsync<T>(Stream body, CancellationToken cancellationToken)
        {
            // Peek one byte: empty chunked/close-delimited/decompressed bodies carry no Content-Length.
            var first = new byte[1];
            int read = await body.ReadAsync(first, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return EmptyResult<T>();
            }

            using (var prefixed = new PrefixedReadStream(first[0], body))
            {
                return await Serializer.DeserializeAsync<T>(prefixed, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Result for a body-less success response: empty string/byte array (legacy fallback behavior), otherwise default.
        /// </summary>
        private static T? EmptyResult<T>()
        {
            if (typeof(T) == typeof(string)) return (T)(object)string.Empty;
            if (typeof(T) == typeof(byte[])) return (T)(object)Array.Empty<byte>();
            return default;
        }

        private ZeroHttpRequestMessage CreateRequest(
            HttpMethod method,
            string uri,
            ZeroHttpContent? content,
            string? bearerToken,
            string? apiKey)
        {
            var request = new ZeroHttpRequestMessage(method, uri) { Content = content };

            string? token = !string.IsNullOrEmpty(bearerToken) ? bearerToken : _options.DefaultBearerToken;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Set("Authorization", "Bearer " + token);
            }

            string? key = !string.IsNullOrEmpty(apiKey) ? apiKey : _options.DefaultApiKey;
            if (!string.IsNullOrEmpty(key))
            {
                request.Headers.Set("x-api-key", key!);
            }

            return request;
        }

        private static void EnsureSuccessStatusCode(ZeroHttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            string message = $"Response status code does not indicate success: {response.StatusCode} ({response.ReasonPhrase}).";
#if NET5_0_OR_GREATER
            var exception = new HttpRequestException(message, null, (HttpStatusCode)response.StatusCode);
#else
            var exception = new HttpRequestException(message);
#endif
            exception.Data["StatusCode"] = response.StatusCode;
            throw exception;
        }

        /// <summary>
        /// Appends an object's properties or dictionary key-values to a URL as a query string.
        /// Values are formatted with the invariant culture (decimals use '.', dates use ISO 8601, booleans are lowercase);
        /// collections become repeated keys (<c>ids=1&amp;ids=2</c>); an existing fragment is preserved at the end.
        /// </summary>
        public static string AppendQueryParams(string url, object? queryParams)
        {
            return ZeroQueryStringBuilder.Append(url, queryParams);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(ZeroApiClient));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            if (_ownsClient)
            {
                _client.Dispose();
            }
        }
    }
}
