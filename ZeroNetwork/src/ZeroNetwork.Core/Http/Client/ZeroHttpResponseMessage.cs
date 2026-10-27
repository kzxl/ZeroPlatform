using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Transport-agnostic HTTP response. Disposing the response releases the body stream and
    /// any transport resources (pooled connection, BCL response object).
    /// </summary>
    public sealed class ZeroHttpResponseMessage : IDisposable
    {
        private readonly IDisposable? _owner;
        private int _disposed;

        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="reasonPhrase">Reason phrase; may be <c>null</c>.</param>
        /// <param name="headers">Response and content headers.</param>
        /// <param name="body">Response body stream (use <see cref="Stream.Null"/> for no body).</param>
        /// <param name="owner">Transport resource released together with the response.</param>
        public ZeroHttpResponseMessage(int statusCode, string? reasonPhrase, ZeroHttpHeaders headers, Stream body, IDisposable? owner = null)
        {
            if (statusCode < 100 || statusCode > 999) throw new ArgumentOutOfRangeException(nameof(statusCode));
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase ?? string.Empty;
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            _owner = owner;
        }

        /// <summary>
        /// Gets the numeric HTTP status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets the HTTP status code as <see cref="HttpStatusCode"/>.
        /// </summary>
        public HttpStatusCode Status => (HttpStatusCode)StatusCode;

        /// <summary>
        /// Gets the reason phrase (may be empty).
        /// </summary>
        public string ReasonPhrase { get; }

        /// <summary>
        /// Gets response and content headers.
        /// </summary>
        public ZeroHttpHeaders Headers { get; }

        /// <summary>
        /// Gets the response body stream.
        /// </summary>
        public Stream Body { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="StatusCode"/> is in the 2xx range.
        /// </summary>
        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;

        /// <summary>
        /// Gets the parsed <c>Content-Length</c> header, or <c>null</c> when absent or invalid.
        /// </summary>
        public long? ContentLength
        {
            get
            {
                string? raw = Headers.Get("Content-Length");
                return raw != null && long.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long value)
                    ? value
                    : (long?)null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the response is known to carry no body
        /// (status 1xx/204/205/304, or <c>Content-Length: 0</c>).
        /// </summary>
        public bool HasNoBody => (StatusCode >= 100 && StatusCode < 200) || StatusCode == 204 || StatusCode == 205 || StatusCode == 304 || ContentLength == 0;

        /// <summary>
        /// Transfers ownership of the body to the returned stream: disposing the stream disposes this response
        /// and every additional owner.
        /// </summary>
        internal Stream DetachBody(IDisposable? additionalOwner)
        {
            return new OwnedStream(Body, this, additionalOwner);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try { Body.Dispose(); } catch { /* best effort */ }
            _owner?.Dispose();
        }

        private sealed class OwnedStream : Stream
        {
            private readonly Stream _inner;
            private readonly IDisposable _owner;
            private readonly IDisposable? _additionalOwner;
            private int _disposed;

            public OwnedStream(Stream inner, IDisposable owner, IDisposable? additionalOwner)
            {
                _inner = inner;
                _owner = owner;
                _additionalOwner = additionalOwner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _inner.ReadAsync(buffer, offset, count, cancellationToken);

#if NETCOREAPP2_1_OR_GREATER
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => _inner.ReadAsync(buffer, cancellationToken);
#endif

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
                => _inner.CopyToAsync(destination, bufferSize, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _owner.Dispose();
                    _additionalOwner?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
