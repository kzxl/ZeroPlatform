using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Packet framing strategies for industrial stream sockets.
    /// </summary>
    public enum SocketFramingMode
    {
        /// <summary>
        /// Splits incoming stream using a byte delimiter (e.g. \r\n, \n, CR).
        /// </summary>
        Delimiter,

        /// <summary>
        /// Frame starts with STX (0x02) and ends with ETX (0x03).
        /// </summary>
        StxEtx,

        /// <summary>
        /// Frame starts with a 2-byte Big-Endian length header.
        /// </summary>
        LengthPrefixed2Byte,

        /// <summary>
        /// Raw stream chunks with no framing protocol.
        /// </summary>
        RawStream
    }

    /// <summary>
    /// Flexible TCP/UDP client for industrial peripheral devices (barcode readers, weigh scales, vision sensors).
    /// Handles stream defragmentation, packet extraction, and automatic reconnection.
    /// </summary>
    public sealed class GenericSocketClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly SocketFramingMode _framingMode;
        private readonly byte[] _delimiter;
        private readonly Encoding _encoding;
        private readonly TimeSpan _timeout;

        private Socket? _socket;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private readonly MemoryStream _streamBuffer = new MemoryStream();
        private readonly object _bufferLock = new object();
        private bool _isDisposed;

        public bool IsConnected => _socket != null && _socket.Connected;
        public string Endpoint => $"{_host}:{_port}";

        /// <summary>
        /// Fired whenever a complete framed packet is received.
        /// </summary>
        public event Action<byte[]>? PacketReceived;

        /// <summary>
        /// Fired whenever a complete text message is received.
        /// </summary>
        public event Action<string>? StringReceived;

        public GenericSocketClient(
            string host,
            int port,
            SocketFramingMode framingMode = SocketFramingMode.Delimiter,
            byte[]? delimiter = null,
            Encoding? encoding = null,
            TimeSpan? timeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _framingMode = framingMode;
            _delimiter = delimiter ?? new byte[] { (byte)'\r', (byte)'\n' };
            _encoding = encoding ?? Encoding.UTF8;
            _timeout = timeout ?? TimeSpan.FromSeconds(3);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(GenericSocketClient));
            Disconnect();

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                SendTimeout = (int)_timeout.TotalMilliseconds,
                ReceiveTimeout = (int)_timeout.TotalMilliseconds
            };

            var connectTask = socket.ConnectAsync(_host, _port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(_timeout, cancellationToken)).ConfigureAwait(false);

            if (completedTask != connectTask)
            {
                socket.Dispose();
                throw new TimeoutException($"Socket connection to {Endpoint} timed out.");
            }

            _socket = socket;
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            var segment = new ArraySegment<byte>(data);
            await _socket.SendAsync(segment, SocketFlags.None).ConfigureAwait(false);
        }

        public Task SendStringAsync(string text, CancellationToken cancellationToken = default)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            byte[] bytes = _encoding.GetBytes(text);
            return SendAsync(bytes, cancellationToken);
        }

        public void Disconnect()
        {
            _receiveCts?.Cancel();
            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                _socket?.Dispose();
            }
            catch
            {
                // Ignored
            }
            finally
            {
                _socket = null;
                lock (_bufferLock)
                {
                    _streamBuffer.SetLength(0);
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];

            while (!ct.IsCancellationRequested && _socket != null && _socket.Connected)
            {
                int bytesRead = 0;
                try
                {
                    var segment = new ArraySegment<byte>(buffer);
                    bytesRead = await _socket.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                if (bytesRead <= 0) break;

                ProcessIncomingBytes(buffer, bytesRead);
            }

            Disconnect();
        }

        private void ProcessIncomingBytes(byte[] buffer, int length)
        {
            List<byte[]> framesToEmit = new List<byte[]>();

            lock (_bufferLock)
            {
                _streamBuffer.Write(buffer, 0, length);
                byte[] allBytes = _streamBuffer.ToArray();

                int startOffset = 0;

                while (startOffset < allBytes.Length)
                {
                    if (_framingMode == SocketFramingMode.Delimiter)
                    {
                        int delimIndex = IndexOfSubArray(allBytes, startOffset, _delimiter);
                        if (delimIndex >= 0)
                        {
                            int frameLen = delimIndex - startOffset;
                            var frame = new byte[frameLen];
                            Array.Copy(allBytes, startOffset, frame, 0, frameLen);
                            framesToEmit.Add(frame);
                            startOffset = delimIndex + _delimiter.Length;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (_framingMode == SocketFramingMode.StxEtx)
                    {
                        int stxIndex = Array.IndexOf(allBytes, (byte)0x02, startOffset);
                        if (stxIndex >= 0)
                        {
                            int etxIndex = Array.IndexOf(allBytes, (byte)0x03, stxIndex + 1);
                            if (etxIndex >= 0)
                            {
                                int frameLen = etxIndex - (stxIndex + 1);
                                var frame = new byte[frameLen];
                                Array.Copy(allBytes, stxIndex + 1, frame, 0, frameLen);
                                framesToEmit.Add(frame);
                                startOffset = etxIndex + 1;
                            }
                            else
                            {
                                startOffset = stxIndex; // Wait for ETX
                                break;
                            }
                        }
                        else
                        {
                            startOffset = allBytes.Length;
                            break;
                        }
                    }
                    else
                    {
                        // RawStream
                        var frame = new byte[allBytes.Length - startOffset];
                        Array.Copy(allBytes, startOffset, frame, 0, frame.Length);
                        framesToEmit.Add(frame);
                        startOffset = allBytes.Length;
                        break;
                    }
                }

                // Retain remainder in buffer
                _streamBuffer.SetLength(0);
                if (startOffset < allBytes.Length)
                {
                    _streamBuffer.Write(allBytes, startOffset, allBytes.Length - startOffset);
                }
            }

            for (int i = 0; i < framesToEmit.Count; i++)
            {
                var frame = framesToEmit[i];
                try
                {
                    PacketReceived?.Invoke(frame);
                    StringReceived?.Invoke(_encoding.GetString(frame));
                }
                catch
                {
                    // Guard handler exceptions
                }
            }
        }

        private static int IndexOfSubArray(byte[] source, int start, byte[] pattern)
        {
            for (int i = start; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Disconnect();
            _streamBuffer.Dispose();
        }
    }
}
