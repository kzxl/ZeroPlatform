using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// Lightweight, asynchronous pure C# MQTT 3.1.1 client with QoS 0/1/2 support,
    /// X.509 mTLS encryption, automatic keep-alive ping, and non-blocking event dispatch.
    /// </summary>
    public class MqttClient : IDisposable
    {
        private readonly MqttClientOptions _options;
        private readonly Qos2FlightTable _flightTable = new Qos2FlightTable();
        private TcpClient? _tcpClient;
        private Stream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;
        private Task? _pingLoopTask;
        private ushort _nextPacketId = 1;
        private readonly object _sendLock = new object();

        public string Host => _options.Host;
        public int Port => _options.Port;
        public string ClientId => _options.ClientId!;
        public ushort KeepAliveSeconds => _options.KeepAliveSeconds;
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        public event Action<string, byte[], MqttQoS, bool>? MessageReceived;
        public event Action? Connected;
        public event Action<Exception?>? Disconnected;

        public MqttClient(string host, int port = 1883, string? clientId = null, ushort keepAliveSeconds = 60)
            : this(new MqttClientOptions
            {
                Host = host ?? throw new ArgumentNullException(nameof(host)),
                Port = port,
                ClientId = clientId,
                KeepAliveSeconds = keepAliveSeconds
            })
        {
        }

        public MqttClient(MqttClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(_options.ClientId))
            {
                _options.ClientId = "ZeroIoT_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }
        }

        public async Task<bool> ConnectAsync(string? username = null, string? password = null, CancellationToken cancellationToken = default)
        {
            if (IsConnected) return true;

            string effectiveUser = username ?? _options.Username!;
            string effectivePass = password ?? _options.Password!;

            _tcpClient = new TcpClient();
            _tcpClient.NoDelay = true;
            await _tcpClient.ConnectAsync(Host, Port).ConfigureAwait(false);

            Stream networkStream = _tcpClient.GetStream();

            if (_options.UseTls)
            {
                var sslStream = new SslStream(
                    networkStream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: _options.CertificateValidationCallback
                );

                string targetHost = _options.TargetHost ?? Host;
                var clientCerts = _options.ClientCertificates ?? new X509CertificateCollection();

                #if NET8_0_OR_GREATER
                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                    ClientCertificates = clientCerts,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };
                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                #else
                await sslStream.AuthenticateAsClientAsync(
                    targetHost,
                    clientCerts,
                    SslProtocols.Tls12,
                    checkCertificateRevocation: false
                ).ConfigureAwait(false);
                #endif

                _stream = sslStream;
            }
            else
            {
                _stream = networkStream;
            }

            // Send CONNECT packet
            byte[] connectPacket = MqttPacketEncoder.EncodeConnect(ClientId, _options.CleanSession, KeepAliveSeconds, effectiveUser, effectivePass);
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
            TaskCompletionSource<bool>? tcs = null;
            if (qos > MqttQoS.AtMostOnce)
            {
                packetId = GetNextPacketId();
                if (qos == MqttQoS.ExactlyOnce)
                {
                    tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _flightTable.RegisterOutbound(packetId, tcs);
                }
            }

            byte[] packet = MqttPacketEncoder.EncodePublish(topic, payload, qos, retain, false, packetId);
            lock (_sendLock)
            {
                _stream.Write(packet, 0, packet.Length);
            }

            if (tcs != null)
            {
                await tcs.Task.ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }
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
                            MessageReceived?.Invoke(publish.Topic, publish.Payload.ToArray(), publish.QoS, publish.Retain);
                        }
                        else if (header.QoS == MqttQoS.ExactlyOnce && _stream != null)
                        {
                            // Inbound QoS 2 Step 1 & 2: Store message and return PUBREC
                            _flightTable.StoreInbound(publish.PacketId, publish.Topic, publish.Payload.ToArray(), publish.Retain);
                            byte[] pubRec = MqttPacketEncoder.EncodePubRec(publish.PacketId);
                            lock (_sendLock)
                            {
                                _stream.Write(pubRec, 0, pubRec.Length);
                            }
                        }
                        else
                        {
                            MessageReceived?.Invoke(publish.Topic, publish.Payload.ToArray(), publish.QoS, publish.Retain);
                        }
                    }
                    break;

                case MqttPacketType.PubRec:
                    // Outbound QoS 2 Step 3: Broker received publish, reply with PUBREL
                    if (MqttPacketDecoder.TryDecodePubRec(payload, out ushort pubRecId) && _stream != null)
                    {
                        byte[] pubRel = MqttPacketEncoder.EncodePubRel(pubRecId);
                        lock (_sendLock)
                        {
                            _stream.Write(pubRel, 0, pubRel.Length);
                        }
                    }
                    break;

                case MqttPacketType.PubRel:
                    // Inbound QoS 2 Step 4: Broker released publish, notify app and reply with PUBCOMP
                    if (MqttPacketDecoder.TryDecodePubRel(payload, out ushort pubRelId) && _stream != null)
                    {
                        if (_flightTable.TryReleaseInbound(pubRelId, out var inboundMsg))
                        {
                            MessageReceived?.Invoke(inboundMsg.Topic, inboundMsg.Payload, MqttQoS.ExactlyOnce, inboundMsg.Retain);
                        }
                        byte[] pubComp = MqttPacketEncoder.EncodePubComp(pubRelId);
                        lock (_sendLock)
                        {
                            _stream.Write(pubComp, 0, pubComp.Length);
                        }
                    }
                    break;

                case MqttPacketType.PubComp:
                    // Outbound QoS 2 Step 5: Final completion acknowledged by broker
                    if (MqttPacketDecoder.TryDecodePubComp(payload, out ushort pubCompId))
                    {
                        _flightTable.CompleteOutbound(pubCompId);
                    }
                    break;

                case MqttPacketType.PubAck:
                    if (MqttPacketDecoder.TryDecodePubAck(payload, out ushort pubAckId))
                    {
                        _flightTable.CompleteOutbound(pubAckId);
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
            _flightTable.FailAll(new OperationCanceledException("Client disconnected."));
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
