using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ZeroPrimitives.Cryptography;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroNetwork.Rpc
{
    /// <summary>
    /// High-performance multiplexed binary RPC client.
    /// Supports concurrent request-response interleaving over a single TCP connection,
    /// tracking inflight requests via <see cref="FastUlid"/> correlation identifiers with sub-microsecond latency.
    /// </summary>
    public sealed class ZeroRpcClient : IDisposable
    {
        private Socket? _socket;
        private NetworkStream? _stream;
        private readonly ConcurrentDictionary<FastUlid, TaskCompletionSource<ZeroRpcResponse>> _pending =
            new ConcurrentDictionary<FastUlid, TaskCompletionSource<ZeroRpcResponse>>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _receiveTask;
        private bool _disposed;

        public bool IsConnected => _socket != null && _socket.Connected;

        public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            if (addresses == null || addresses.Length == 0)
                throw new ArgumentException($"Could not resolve host: {host}", nameof(host));

            await ConnectAsync(new IPEndPoint(addresses[0], port), ct).ConfigureAwait(false);
        }

        public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken ct = default)
        {
            if (_socket != null)
            {
                Disconnect();
            }

            _socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

#if NETFRAMEWORK
            await Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, endPoint, null).ConfigureAwait(false);
#else
            await _socket.ConnectAsync(endPoint).ConfigureAwait(false);
#endif
            _stream = new NetworkStream(_socket, ownsSocket: false);
            _receiveTask = Task.Run(ReceiveLoopAsync);
        }

        /// <summary>
        /// Asynchronously invokes an RPC method and awaits the response.
        /// Fully multiplexed: multiple threads can call this simultaneously across the same connection.
        /// </summary>
        public async Task<ZeroRpcResponse> InvokeAsync(ushort methodId, ReadOnlyMemory<byte> payload = default, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (!IsConnected || _socket == null)
                throw new InvalidOperationException("ZeroRpcClient is not connected to any server.");

            var correlationId = FastUlid.NewUlid();
            var tcs = new TaskCompletionSource<ZeroRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pending.TryAdd(correlationId, tcs))
                throw new InvalidOperationException("Failed to register correlation identifier.");

            uint checksum = payload.Length > 0 ? FastCrc.Crc32(payload.Span) : 0;
            var header = ZeroRpcHeader.CreateRequest(methodId, payload.Length, checksum, correlationId);

            byte[] headerBuf = new byte[ZeroRpcHeader.HeaderLength];
            header.Encode(headerBuf);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            try
            {
                await _sendLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                try
                {
                    await SendExactAsync(_stream!, headerBuf, 0, ZeroRpcHeader.HeaderLength, linkedCts.Token).ConfigureAwait(false);
                    if (payload.Length > 0)
                    {
                        byte[] rawPayload = payload.ToArray();
                        await SendExactAsync(_stream!, rawPayload, 0, rawPayload.Length, linkedCts.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _sendLock.Release();
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _pending.TryRemove(correlationId, out _);
                throw new TimeoutException($"RPC invocation for MethodId {methodId} timed out.");
            }
            catch
            {
                _pending.TryRemove(correlationId, out _);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-way fire-and-forget message without waiting for a server response.
        /// </summary>
        public async Task SendOneWayAsync(ushort methodId, ReadOnlyMemory<byte> payload = default, CancellationToken ct = default)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("ZeroRpcClient is not connected to any server.");

            uint checksum = payload.Length > 0 ? FastCrc.Crc32(payload.Span) : 0;
            var header = ZeroRpcHeader.CreateOneWay(methodId, payload.Length, checksum);

            byte[] headerBuf = new byte[ZeroRpcHeader.HeaderLength];
            header.Encode(headerBuf);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            await _sendLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                await SendExactAsync(_stream, headerBuf, 0, ZeroRpcHeader.HeaderLength, linkedCts.Token).ConfigureAwait(false);
                if (payload.Length > 0)
                {
                    byte[] rawPayload = payload.ToArray();
                    await SendExactAsync(_stream, rawPayload, 0, rawPayload.Length, linkedCts.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Sends a heartbeat ping and waits for the server pong response.
        /// </summary>
        public async Task<bool> PingAsync(TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (!IsConnected || _stream == null) return false;

            var correlationId = FastUlid.NewUlid();
            var tcs = new TaskCompletionSource<ZeroRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[correlationId] = tcs;

            var header = ZeroRpcHeader.CreateHeartbeat(isPong: false, correlationId);
            byte[] headerBuf = new byte[ZeroRpcHeader.HeaderLength];
            header.Encode(headerBuf);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            linkedCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));

            try
            {
                await _sendLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                try
                {
                    await SendExactAsync(_stream, headerBuf, 0, ZeroRpcHeader.HeaderLength, linkedCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    _sendLock.Release();
                }

                var resp = await tcs.Task.ConfigureAwait(false);
                return resp.IsSuccess;
            }
            catch
            {
                _pending.TryRemove(correlationId, out _);
                return false;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] headerBuffer = new byte[ZeroRpcHeader.HeaderLength];

            try
            {
                while (!_cts.Token.IsCancellationRequested && _stream != null && _socket != null && _socket.Connected)
                {
                    bool readOk = await ReadExactAsync(_stream, headerBuffer, 0, ZeroRpcHeader.HeaderLength, _cts.Token).ConfigureAwait(false);
                    if (!readOk) break;

                    if (!ZeroRpcHeader.TryDecode(headerBuffer, out var header))
                    {
                        break;
                    }

                    byte[]? payload = null;
                    if (header.PayloadLength > 0)
                    {
                        payload = new byte[header.PayloadLength];
                        bool payloadOk = await ReadExactAsync(_stream, payload, 0, header.PayloadLength, _cts.Token).ConfigureAwait(false);
                        if (!payloadOk) break;
                    }

                    if (_pending.TryRemove(header.CorrelationId, out var tcs))
                    {
                        var response = new ZeroRpcResponse(header.CorrelationId, header.MethodId, (ZeroRpcStatus)header.StatusCode, payload ?? ReadOnlyMemory<byte>.Empty);
                        tcs.TrySetResult(response);
                    }
                }
            }
            catch { }
            finally
            {
                // Cancel all remaining pending requests
                foreach (var kvp in _pending)
                {
                    kvp.Value.TrySetException(new SocketException((int)SocketError.ConnectionReset));
                }
                _pending.Clear();
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

        public void Disconnect()
        {
            _cts.Cancel();
            _stream?.Dispose();
            _stream = null;
            if (_socket != null)
            {
                try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                try { _socket.Close(); } catch { }
                try { _socket.Dispose(); } catch { }
                _socket = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            _sendLock.Dispose();
            _cts.Dispose();
        }
    }
}
