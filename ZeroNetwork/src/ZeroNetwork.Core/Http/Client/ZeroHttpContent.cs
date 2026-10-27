using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Common;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Transport-agnostic HTTP request body.
    /// </summary>
    public abstract class ZeroHttpContent : IDisposable
    {
        /// <summary>
        /// Gets or sets the <c>Content-Type</c> header value (e.g. <c>application/json; charset=utf-8</c>).
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets additional content-level headers (e.g. <c>Content-Encoding</c>).
        /// </summary>
        public ZeroHttpHeaders Headers { get; } = new ZeroHttpHeaders();

        /// <summary>
        /// Gets the body length in bytes when known. A known length allows transports to send
        /// <c>Content-Length</c> instead of chunked transfer encoding.
        /// </summary>
        public abstract long? Length { get; }

        /// <summary>
        /// Gets a value indicating whether the body can be written more than once.
        /// Retry middleware only re-sends requests whose content is replayable.
        /// </summary>
        public abstract bool IsReplayable { get; }

        /// <summary>
        /// Writes the full body to <paramref name="destination"/>.
        /// </summary>
        public abstract Task CopyToAsync(Stream destination, CancellationToken cancellationToken);

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources held by the content.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    /// Replayable content backed by a caller-owned byte array segment.
    /// </summary>
    public sealed class ZeroByteArrayContent : ZeroHttpContent
    {
        private readonly byte[] _buffer;
        private readonly int _offset;
        private readonly int _count;

        public ZeroByteArrayContent(byte[] buffer) : this(buffer, 0, buffer?.Length ?? 0)
        {
        }

        public ZeroByteArrayContent(byte[] buffer, int offset, int count)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            _offset = offset;
            _count = count;
        }

        /// <inheritdoc />
        public override long? Length => _count;

        /// <inheritdoc />
        public override bool IsReplayable => true;

        /// <inheritdoc />
        public override Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            return destination.WriteAsync(_buffer, _offset, _count, cancellationToken);
        }
    }

    /// <summary>
    /// Replayable content buffered in pooled <see cref="ZeroRecyclableStream"/> chunks.
    /// Avoids the <c>MemoryStream</c> growth + <c>ToArray()</c> copies that place large payloads on the
    /// Large Object Heap, while still providing a known <c>Content-Length</c> and retry-safe replay.
    /// Pooled buffers are returned on <see cref="ZeroHttpContent.Dispose()"/>.
    /// </summary>
    public sealed class ZeroPooledContent : ZeroHttpContent
    {
        private readonly ZeroRecyclableStream _buffer;
        private int _disposed;

        private ZeroPooledContent(ZeroRecyclableStream buffer, string? contentType)
        {
            _buffer = buffer;
            ContentType = contentType;
        }

        /// <summary>
        /// Serializes <paramref name="value"/> as JSON into pooled buffers.
        /// </summary>
        public static async Task<ZeroPooledContent> SerializeJsonAsync<T>(
            IZeroJsonSerializer serializer,
            T value,
            CancellationToken cancellationToken = default)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            var buffer = new ZeroRecyclableStream();
            try
            {
                await serializer.SerializeAsync(buffer, value, cancellationToken).ConfigureAwait(false);
                return new ZeroPooledContent(buffer, "application/json; charset=utf-8");
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Copies <paramref name="source"/> (from its current position) into pooled buffers.
        /// </summary>
        public static async Task<ZeroPooledContent> FromStreamAsync(
            Stream source,
            string? contentType,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var buffer = new ZeroRecyclableStream();
            try
            {
                await source.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
                return new ZeroPooledContent(buffer, contentType);
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public override long? Length => _buffer.Length;

        /// <inheritdoc />
        public override bool IsReplayable => true;

        /// <inheritdoc />
        public override Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
        {
            return _buffer.WriteToStreamAsync(destination, cancellationToken);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _buffer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
