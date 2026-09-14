using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// Lightweight, asynchronous pure C# MQTT 3.1.1 client with automatic keep-alive ping and event dispatch.
    /// </summary>
    public class MqttClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;
        private Task? _pingLoopTask;
        private ushort _nextPacketId = 1;
        private readonly object _sendLock = new object();

        public string Host { get; }
        public int Port { get; }
        public string ClientId { get; }
        public ushort KeepAliveSeconds { get; }
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        public event Action<string, byte[], MqttQoS, bool>? MessageReceived;
        public event Action? Connected;
        public event Action<Exception?>? Disconnected;

        public MqttClient(string host, int port = 1883, string? clientId = null, ushort keepAliveSeconds = 60)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            ClientId = clientId ?? ("ZeroIoT_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            KeepAliveSeconds = keepAliveSeconds;
        }

        public async Task<bool> ConnectAsync(string? username = null, string? password = null, CancellationToken cancellationToken = default)
        {
            if (IsConnected) return true;

            _tcpClient = new TcpClient();
            _tcpClient.NoDelay = true;
            await _tcpClient.ConnectAsync(Host, Port).ConfigureAwait(false);
            _stream = _tcpClient.GetStream();

            // Send CONNECT packet
            byte[] connectPacket = MqttPacketEncoder.EncodeConnect(ClientId, true, KeepAliveSeconds, username, password);
            await _stream.WriteAsync(connectPacket, 0, connectPacket.Length, cancellationToken).ConfigureAwait(false);

            // Read CONNACK (fixed 4 bytes)
            byte[] connAckBuf = new byte[4];
            int readTotal = 0;
            while (readTotal < 4)
            {
                int read = await _stream.ReadAsync(connAckBuf, readTotal, 4 - readTotal, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Broker disconnected during CONNACK.");
                readTotal += read;
            }

            if (!MqttPacketDecoder.TryDecodeFixedHeader(connAckBuf, out var header, out int headerLen) ||
                header.PacketType != MqttPacketType.ConnAck)
            {
                throw new InvalidOperationException("Invalid CONNACK packet received from broker.");
            }

            if (!MqttPacketDecoder.TryDecodeConnAck(connAckBuf.AsSpan(headerLen), out _, out byte returnCode) ||
                returnCode != 0)
            {
                throw new InvalidOperationException($"Broker refused connection with code: {returnCode}");
            }

            _cts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ReadLoopAsync(_cts.Token));
            if (KeepAliveSeconds > 0)
            {
                _pingLoopTask = Task.Run(() => PingLoopAsync(_cts.Token));
            }

            Connected?.Invoke();
            return true;
        }

        public async Task PublishAsync(string topic, byte[] payload, MqttQoS qos = MqttQoS.AtMostOnce, bool retain = false)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("Client is not connected.");

            ushort packetId = 0;
            if (qos > MqttQoS.AtMostOnce)
            {
                packetId = GetNextPacketId();
            }

            byte[] packet = MqttPacketEncoder.EncodePublish(topic, payload, qos, retain, false, packetId);
            lock (_sendLock)
            {
                _stream.Write(packet, 0, packet.Length);
            }
            await Task.Yield();
        }

        public async Task SubscribeAsync(params (string topic, MqttQoS qos)[] subscriptions)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("Client is not connected.");

            ushort packetId = GetNextPacketId();
            byte[] packet = MqttPacketEncoder.EncodeSubscribe(packetId, subscriptions);
            lock (_sendLock)
            {
                _stream.Write(packet, 0, packet.Length);
            }
            await Task.Yield();
        }

        public void Disconnect()
        {
            if (!IsConnected || _stream == null) return;

            try
            {
                byte[] disconnect = MqttPacketEncoder.EncodeDisconnect();
                lock (_sendLock)
                {
                    _stream.Write(disconnect, 0, disconnect.Length);
                }
            }
            catch { }

            Cleanup();
            Disconnected?.Invoke(null);
        }

        private ushort GetNextPacketId()
        {
            lock (_sendLock)
            {
                if (_nextPacketId == 0) _nextPacketId = 1;
                return _nextPacketId++;
            }
        }

        private async Task PingLoopAsync(CancellationToken token)
        {
            int pingIntervalMs = (int)(KeepAliveSeconds * 1000 * 0.75);
            byte[] pingPacket = MqttPacketEncoder.EncodePingReq();

            while (!token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await Task.Delay(pingIntervalMs, token).ConfigureAwait(false);
                    if (_stream != null && IsConnected)
                    {
                        lock (_sendLock)
                        {
                            _stream.Write(pingPacket, 0, pingPacket.Length);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Cleanup();
                    Disconnected?.Invoke(ex);
                    break;
                }
            }
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            int bufferCount = 0;

            try
            {
                while (!token.IsCancellationRequested && _stream != null)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, bufferCount, buffer.Length - bufferCount, token).ConfigureAwait(false);
                    if (bytesRead == 0) break;

                    bufferCount += bytesRead;
                    int processedOffset = 0;

                    while (processedOffset < bufferCount)
                    {
                        ReadOnlySpan<byte> remaining = buffer.AsSpan(processedOffset, bufferCount - processedOffset);
                        if (!MqttPacketDecoder.TryDecodeFixedHeader(remaining, out var header, out int headerLen))
                        {
                            break; // Need more header bytes
                        }

                        int totalPacketLen = headerLen + header.RemainingLength;
                        if (remaining.Length < totalPacketLen)
                        {
                            break; // Need more payload bytes
                        }

                        ReadOnlySpan<byte> packetPayload = remaining.Slice(headerLen, header.RemainingLength);
                        HandleIncomingPacket(header, packetPayload);
                        processedOffset += totalPacketLen;
                    }

                    if (processedOffset > 0)
                    {
                        int remainingBytes = bufferCount - processedOffset;
                        if (remainingBytes > 0)
                        {
                            Buffer.BlockCopy(buffer, processedOffset, buffer, 0, remainingBytes);
                        }
                        bufferCount = remainingBytes;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Cleanup();
                Disconnected?.Invoke(ex);
                return;
            }

            Cleanup();
            Disconnected?.Invoke(null);
        }

        private void HandleIncomingPacket(MqttFixedHeader header, ReadOnlySpan<byte> payload)
        {
            switch (header.PacketType)
            {
                case MqttPacketType.Publish:
                    if (MqttPacketDecoder.TryDecodePublish(header, payload, out var publish))
                    {
                        if (header.QoS == MqttQoS.AtLeastOnce && _stream != null)
                        {
                            byte[] pubAck = MqttPacketEncoder.EncodePubAck(publish.PacketId);
                            lock (_sendLock)
                            {
                                _stream.Write(pubAck, 0, pubAck.Length);
                            }
                        }
                        MessageReceived?.Invoke(publish.Topic, publish.Payload.ToArray(), publish.QoS, publish.Retain);
                    }
                    break;
                case MqttPacketType.PingResp:
                    // Keep-alive acknowledged
                    break;
            }
        }

        private void Cleanup()
        {
            _cts?.Cancel();
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
            _stream = null;
            _tcpClient = null;
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}
