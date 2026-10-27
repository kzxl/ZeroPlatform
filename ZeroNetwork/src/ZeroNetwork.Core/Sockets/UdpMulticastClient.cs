using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Sockets
{
    /// <summary>
    /// High-throughput, non-blocking UDP multicast and broadcast manager.
    /// Supports IGMPv2/v3 multicast group joining, interface binding, and continuous async packet reception.
    /// </summary>
    public class UdpMulticastClient : IDisposable
    {
        private readonly UdpClient _client;
        private readonly int _port;
        private readonly IPAddress? _localInterfaceAddress;
        private CancellationTokenSource? _listenCts;
        private bool _disposed;
        private readonly object _lock = new object();

        public event EventHandler<(byte[] Data, IPEndPoint RemoteEndPoint)>? PacketReceived;

        public bool IsListening => _listenCts != null && !_listenCts.IsCancellationRequested;

        public UdpMulticastClient(int port, IPAddress? localInterfaceAddress = null)
        {
            _port = port;
            _localInterfaceAddress = localInterfaceAddress;

            _client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            var localEp = localInterfaceAddress != null
                ? new IPEndPoint(localInterfaceAddress, port)
                : new IPEndPoint(IPAddress.Any, port);

            _client.Client.Bind(localEp);
        }

        /// <summary>
        /// Joins an IGMP multicast group on the specified address.
        /// </summary>
        public void JoinMulticastGroup(IPAddress multicastAddress, int timeToLive = 2)
        {
            if (multicastAddress == null) throw new ArgumentNullException(nameof(multicastAddress));

            _client.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive,
                Math.Max(1, Math.Min(255, timeToLive)));

            if (_localInterfaceAddress != null)
            {
                _client.JoinMulticastGroup(multicastAddress, _localInterfaceAddress);
            }
            else
            {
                _client.JoinMulticastGroup(multicastAddress);
            }
        }

        /// <summary>
        /// Leaves an IGMP multicast group.
        /// </summary>
        public void DropMulticastGroup(IPAddress multicastAddress)
        {
            if (multicastAddress == null) throw new ArgumentNullException(nameof(multicastAddress));
            _client.DropMulticastGroup(multicastAddress);
        }

        /// <summary>
        /// Sends a datagram asynchronously to the specified endpoint.
        /// </summary>
        public Task<int> SendAsync(byte[] data, IPEndPoint targetEndPoint)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (targetEndPoint == null) throw new ArgumentNullException(nameof(targetEndPoint));

            return _client.SendAsync(data, data.Length, targetEndPoint);
        }

        /// <summary>
        /// Starts the continuous asynchronous packet reception loop.
        /// </summary>
        public void StartListening()
        {
            lock (_lock)
            {
                if (_disposed || IsListening) return;

                _listenCts = new CancellationTokenSource();
                var token = _listenCts.Token;

                Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var result = await _client.ReceiveAsync().ConfigureAwait(false);
                            PacketReceived?.Invoke(this, (result.Buffer, result.RemoteEndPoint));
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                                         ex.SocketErrorCode == SocketError.Interrupted)
                        {
                            break;
                        }
                        catch
                        {
                            // Backoff briefly on unexpected socket errors
                            try { await Task.Delay(10, token).ConfigureAwait(false); } catch { break; }
                        }
                    }
                }, token);
            }
        }

        /// <summary>
        /// Stops packet reception.
        /// </summary>
        public void StopListening()
        {
            lock (_lock)
            {
                _listenCts?.Cancel();
                _listenCts?.Dispose();
                _listenCts = null;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                StopListening();
                try { _client.Close(); } catch { }
                _client.Dispose();
            }
        }
    }
}
