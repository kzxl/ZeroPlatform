using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency.RateLimiting;
using ZeroPrimitives.Cryptography;

namespace ZeroNetwork.Rpc
{
    public delegate ValueTask<(ZeroRpcStatus Status, ReadOnlyMemory<byte> Payload)> ZeroRpcHandler(ZeroRpcContext context);

    /// <summary>
    /// High-performance asynchronous binary RPC server.
    /// Provides multiplexed request-response dispatching, zero-allocation header framing,
    /// rate limiting via <see cref="TokenBucketRateLimiter"/>, and CRC32 payload verification.
    /// </summary>
    public sealed class ZeroRpcServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Dictionary<ushort, ZeroRpcHandler> _handlers = new Dictionary<ushort, ZeroRpcHandler>();
        private readonly ConcurrentDictionary<Socket, byte> _activeSockets = new ConcurrentDictionary<Socket, byte>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private TokenBucketRateLimiter? _rateLimiter;
        private Task? _acceptTask;
        private volatile bool _isRunning;
        private bool _disposed;

        public EndPoint LocalEndPoint => _listener.LocalEndpoint;
        public bool IsRunning => _isRunning;

        public ZeroRpcServer(IPAddress ipAddress, int port)
        {
            _listener = new TcpListener(ipAddress, port);
        }

        public ZeroRpcServer(int port) : this(IPAddress.Any, port) { }

        /// <summary>
        /// Registers a handler for a given method identifier.
        /// </summary>
        public ZeroRpcServer RegisterHandler(ushort methodId, ZeroRpcHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_handlers)
            {
                _handlers[methodId] = handler;
            }
            return this;
        }

        /// <summary>
        /// Attaches a <see cref="TokenBucketRateLimiter"/> to protect the server from RPC request flooding.
        /// </summary>
        public ZeroRpcServer WithRateLimiter(TokenBucketRateLimiter rateLimiter)
        {
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            return this;
        }

        /// <summary>
        /// Starts the RPC server and begins listening for client connections.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _listener.Start();
            _acceptTask = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    Socket socket = await _listener.AcceptSocketAsync().ConfigureAwait(false);
                    socket.NoDelay = true;
                    _activeSockets.TryAdd(socket, 0);
                    _ = Task.Run(() => HandleClientAsync(socket, _cts.Token));
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) when (!_isRunning) { break; }
                catch { }
            }
        }

        private async Task HandleClientAsync(Socket socket, CancellationToken ct)
        {
            byte[] headerBuffer = new byte[ZeroRpcHeader.HeaderLength];
            EndPoint? remoteEndPoint = null;
            try { remoteEndPoint = socket.RemoteEndPoint; } catch { }

            using var stream = new NetworkStream(socket, ownsSocket: false);

            try
            {
                while (_isRunning && !ct.IsCancellationRequested && socket.Connected)
                {
                    bool readOk = await ReadExactAsync(stream, headerBuffer, 0, ZeroRpcHeader.HeaderLength, ct).ConfigureAwait(false);
                    if (!readOk) break; // Client disconnected cleanly

                    if (!ZeroRpcHeader.TryDecode(headerBuffer, out var header))
                    {
                        break; // Invalid frame header
                    }

                    // Handle Heartbeat (Ping/Pong)
                    if (header.IsHeartbeat)
                    {
                        var pongHeader = ZeroRpcHeader.CreateHeartbeat(isPong: true, header.CorrelationId);
                        pongHeader.Encode(headerBuffer);
                        await SendExactAsync(stream, headerBuffer, 0, ZeroRpcHeader.HeaderLength, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Read Payload if present
                    byte[]? payload = null;
                    if (header.PayloadLength > 0)
                    {
                        payload = new byte[header.PayloadLength];
                        bool payloadOk = await ReadExactAsync(stream, payload, 0, header.PayloadLength, ct).ConfigureAwait(false);
                        if (!payloadOk) break;

                        // Checksum validation
                        if (header.Checksum != 0)
                        {
                            uint computedCrc = FastCrc.Crc32(payload);
                            if (computedCrc != header.Checksum)
                            {
                                if (!header.IsOneWay)
                                {
                                    var (errStatus, errPayload) = ZeroRpcContext.Error(ZeroRpcStatus.BadRequest, "CRC32 checksum mismatch");
                                    await SendResponseAsync(stream, header, errStatus, errPayload, ct).ConfigureAwait(false);
                                }
                                continue;
                            }
                        }
                    }

                    // Check Rate Limiter
                    if (_rateLimiter != null && !_rateLimiter.TryAcquire())
                    {
                        if (!header.IsOneWay)
                        {
                            var (errStatus, errPayload) = ZeroRpcContext.Error(ZeroRpcStatus.TooManyRequests, "RPC rate limit exceeded");
                            await SendResponseAsync(stream, header, errStatus, errPayload, ct).ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Dispatch to registered handler
                    ZeroRpcHandler? handler = null;
                    lock (_handlers)
                    {
                        _handlers.TryGetValue(header.MethodId, out handler);
                    }

                    if (handler == null)
                    {
                        if (!header.IsOneWay)
                        {
                            var (errStatus, errPayload) = ZeroRpcContext.Error(ZeroRpcStatus.NotFound, $"MethodId {header.MethodId} not found");
                            await SendResponseAsync(stream, header, errStatus, errPayload, ct).ConfigureAwait(false);
                        }
                        continue;
                    }

                    var context = new ZeroRpcContext(header, payload ?? ReadOnlyMemory<byte>.Empty, remoteEndPoint);

                    try
                    {
                        var result = await handler(context).ConfigureAwait(false);
                        if (!header.IsOneWay)
                        {
                            await SendResponseAsync(stream, header, result.Status, result.Payload, ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!header.IsOneWay)
                        {
                            var (errStatus, errPayload) = ZeroRpcContext.Error(ZeroRpcStatus.InternalError, ex.Message);
                            await SendResponseAsync(stream, header, errStatus, errPayload, ct).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _activeSockets.TryRemove(socket, out _);
                try { socket.Shutdown(SocketShutdown.Both); } catch { }
                try { socket.Close(); } catch { }
                try { socket.Dispose(); } catch { }
            }
        }

        private static async Task SendResponseAsync(NetworkStream stream, ZeroRpcHeader reqHeader, ZeroRpcStatus status, ReadOnlyMemory<byte> payload, CancellationToken ct)
        {
            uint checksum = payload.Length > 0 ? FastCrc.Crc32(payload.Span) : 0;
            var respHeader = ZeroRpcHeader.CreateResponse(reqHeader.CorrelationId, reqHeader.MethodId, payload.Length, status, checksum);

            byte[] headerBuf = new byte[ZeroRpcHeader.HeaderLength];
            respHeader.Encode(headerBuf);

            await SendExactAsync(stream, headerBuf, 0, ZeroRpcHeader.HeaderLength, ct).ConfigureAwait(false);

            if (payload.Length > 0)
            {
                byte[] rawPayload = payload.ToArray();
                await SendExactAsync(stream, rawPayload, 0, rawPayload.Length, ct).ConfigureAwait(false);
            }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                ct.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct).ConfigureAwait(false);
                if (read == 0) return false;
                totalRead += read;
            }
            return true;
        }

        private static async Task SendExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(buffer, offset, count, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts.Cancel();

            try { _listener.Stop(); } catch { }

            foreach (var s in _activeSockets.Keys)
            {
                try { s.Close(); s.Dispose(); } catch { }
            }
            _activeSockets.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts.Dispose();
        }
    }
}
