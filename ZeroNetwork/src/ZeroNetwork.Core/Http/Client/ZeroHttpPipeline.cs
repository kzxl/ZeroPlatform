using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Next-step delegate in the <see cref="ZeroHttpClient"/> pipeline.
    /// </summary>
    public delegate Task<ZeroHttpResponseMessage> ZeroHttpHandler(ZeroHttpRequestMessage request, CancellationToken cancellationToken);

    /// <summary>
    /// Cross-cutting pipeline component (retry, auth, telemetry, ...).
    /// Middleware registered first runs outermost.
    /// </summary>
    public interface IZeroHttpMiddleware
    {
        /// <summary>
        /// Processes the request and optionally invokes <paramref name="next"/> (zero, one or many times).
        /// </summary>
        Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Terminal pipeline component that puts the request on the wire.
    /// Implementations must support sending the same <see cref="ZeroHttpRequestMessage"/> multiple times
    /// (retries) and must not dispose it.
    /// </summary>
    public interface IZeroHttpTransport : IDisposable
    {
        /// <summary>
        /// Sends the request and returns once response headers are available; the body is streamed lazily.
        /// </summary>
        Task<ZeroHttpResponseMessage> SendAsync(ZeroHttpRequestMessage request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Adapts an inline delegate to <see cref="IZeroHttpMiddleware"/>.
    /// </summary>
    public sealed class ZeroDelegateMiddleware : IZeroHttpMiddleware
    {
        private readonly Func<ZeroHttpRequestMessage, ZeroHttpHandler, CancellationToken, Task<ZeroHttpResponseMessage>> _invoke;

        public ZeroDelegateMiddleware(Func<ZeroHttpRequestMessage, ZeroHttpHandler, CancellationToken, Task<ZeroHttpResponseMessage>> invoke)
        {
            _invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        }

        /// <inheritdoc />
        public Task<ZeroHttpResponseMessage> InvokeAsync(ZeroHttpRequestMessage request, ZeroHttpHandler next, CancellationToken cancellationToken)
            => _invoke(request, next, cancellationToken);
    }
}
