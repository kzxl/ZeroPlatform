using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Mqtt
{
    /// <summary>
    /// Represents an active server-side MQTT client session adhering to OASIS MQTT v3.1.1.
    /// Manages client identification, keep-alive tracking, and thread-safe framed packet transmission.
    /// </summary>
    public sealed class ZeroMqttSession : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly HashSet<string> _subscribedTopics = new HashSet<string>();
        private int _disposed;
        private bool _isClosed;

        /// <summary>
        /// Gets the unique client identifier assigned or presented during CONNECT.
        /// </summary>
        public string ClientId { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets the remote client endpoint.
        /// </summary>
        public EndPoint? RemoteEndPoint => _tcpClient.Client.RemoteEndPoint;

        /// <summary>
        /// Gets the negotiated keep-alive interval in seconds.
        /// </summary>
        public int KeepAliveSeconds { get; internal set; }

        /// <summary>
        /// Gets whether the MQTT session is active and open.
        /// </summary>
        public bool IsOpen => Volatile.Read(ref _disposed) == 0 && !_isClosed && _tcpClient.Connected;

        /// <summary>
        /// Gets the collection of active topic subscriptions for this session.
        /// </summary>
        public IReadOnlyCollection<string> SubscribedTopics
        {
            get
            {
                lock (_subscribedTopics)
                {
                    return _subscribedTopics.ToArray();
                }
            }
        }

        /// <summary>
        /// Event fired when a client successfully sends a CONNECT packet and is acknowledged.
        /// </summary>
        public event Action<ZeroMqttSession>? Connected;

        /// <summary>
        /// Event fired when a client sends a PUBLISH packet.
        /// </summary>
        public event Action<ZeroMqttSession, string, byte[], MqttQoS, ushort>? MessageReceived;

        /// <summary>
        /// Event fired when a client subscribes to topic filters.
        /// </summary>
        public event Action<ZeroMqttSession, ushort, (string TopicFilter, MqttQoS RequestedQos)[]>? Subscribed;

        /// <summary>
        /// Event fired when a client unsubscribes from topic filters.
        /// </summary>
        public event Action<ZeroMqttSession, ushort, string[]>? Unsubscribed;

        /// <summary>
        /// Event fired when the session disconnects.
        /// </summary>
        public event Action<ZeroMqttSession, Exception?>? Disconnected;

        internal ZeroMqttSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _stream = tcpClient.GetStream();
        }

        internal async Task RunReceiveLoopAsync()
        {
            var token = _cts.Token;
            Exception? terminationException = null;

            try
            {
                byte[] headerByte = new byte[1];
                while (!token.IsCancellationRequested && IsOpen)
                {
                    // 1. Read first byte (PacketType + Flags)
                    if (!await ReadExactAsync(_stream, headerByte, 0, 1, token).ConfigureAwait(false))
                        break;

                    byte b0 = headerByte[0];
                    var packetType = (MqttPacketType)(b0 >> 4);
                    int flags = b0 & 0x0F;

                    // 2. Read Remaining Length (1..4 bytes)
                    int multiplier = 1;
                    int remainingLength = 0;
                    int lengthBytesRead = 0;
                    bool lengthComplete = false;

                    while (lengthBytesRead < 4)
                    {
                        if (!await ReadExactAsync(_stream, headerByte, 0, 1, token).ConfigureAwait(false))
                            break;

                        byte encodedByte = headerByte[0];
                        lengthBytesRead++;
                        remainingLength += (encodedByte & 127) * multiplier;

                        if ((encodedByte & 128) == 0)
                        {
                            lengthComplete = true;
                            break;
                        }

                        multiplier *= 128;
                    }

                    if (!lengthComplete) break;

                    // 3. Read Remaining Payload
                    byte[] payload = new byte[remainingLength];
                    if (remainingLength > 0)
                    {
                        if (!await ReadExactAsync(_stream, payload, 0, remainingLength, token).ConfigureAwait(false))
                            break;
                    }

                    // 4. Process packet by type
                    await ProcessPacketAsync(packetType, flags, payload).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    terminationException = ex;
            }
            finally
            {
                _isClosed = true;
                Disconnected?.Invoke(this, terminationException);
                Dispose();
            }
        }

        private async Task ProcessPacketAsync(MqttPacketType packetType, int flags, byte[] payload)
        {
            int offset = 0;
            var span = payload.AsSpan();

            switch (packetType)
            {
                case MqttPacketType.Connect:
                    // Protocol Name (e.g. "MQTT")
                    string protocolName = ZeroMqttCodec.ReadMqttString(span, ref offset);
                    byte protocolLevel = span[offset++]; // 4 for v3.1.1
                    byte connectFlags = span[offset++];
                    ushort keepAlive = ZeroMqttCodec.ReadUInt16BigEndian(span, ref offset);
                    KeepAliveSeconds = keepAlive;

                    // Payload: ClientId
                    ClientId = ZeroMqttCodec.ReadMqttString(span, ref offset);
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        ClientId = Guid.NewGuid().ToString("N");
                    }

                    // Respond with CONNACK (Connection Accepted)
                    Connected?.Invoke(this);
                    await SendConnAckAsync(MqttConnectReturnCode.ConnectionAccepted, sessionPresent: false).ConfigureAwait(false);
                    break;

                case MqttPacketType.Publish:
                    MqttQoS qos = (MqttQoS)((flags >> 1) & 0x03);
                    string topic = ZeroMqttCodec.ReadMqttString(span, ref offset);
                    ushort packetId = 0;
                    if (qos > MqttQoS.AtMostOnce)
                    {
                        packetId = ZeroMqttCodec.ReadUInt16BigEndian(span, ref offset);
                    }

                    int messageLen = payload.Length - offset;
                    byte[] messageData = new byte[messageLen];
                    if (messageLen > 0)
                    {
                        Array.Copy(payload, offset, messageData, 0, messageLen);
                    }

                    // If QoS 1, respond with PUBACK
                    if (qos == MqttQoS.AtLeastOnce)
                    {
                        await SendPubAckAsync(packetId).ConfigureAwait(false);
                    }

                    MessageReceived?.Invoke(this, topic, messageData, qos, packetId);
                    break;

                case MqttPacketType.Subscribe:
                    ushort subPacketId = ZeroMqttCodec.ReadUInt16BigEndian(span, ref offset);
                    var subscriptions = new System.Collections.Generic.List<(string, MqttQoS)>();

                    while (offset < payload.Length)
                    {
                        string filter = ZeroMqttCodec.ReadMqttString(span, ref offset);
                        byte requestedQos = span[offset++];
                        subscriptions.Add((filter, (MqttQoS)requestedQos));
                    }

                    lock (_subscribedTopics)
                    {
                        for (int i = 0; i < subscriptions.Count; i++)
                        {
                            _subscribedTopics.Add(subscriptions[i].Item1);
                        }
                    }

                    byte[] grantedQos = new byte[subscriptions.Count];
                    for (int i = 0; i < grantedQos.Length; i++)
                    {
                        grantedQos[i] = (byte)subscriptions[i].Item2;
                    }

                    Subscribed?.Invoke(this, subPacketId, subscriptions.ToArray());
                    await SendSubAckAsync(subPacketId, grantedQos).ConfigureAwait(false);
                    break;

                case MqttPacketType.Unsubscribe:
                    ushort unsubPacketId = ZeroMqttCodec.ReadUInt16BigEndian(span, ref offset);
                    var unsubs = new System.Collections.Generic.List<string>();

                    while (offset < payload.Length)
                    {
                        string filter = ZeroMqttCodec.ReadMqttString(span, ref offset);
                        unsubs.Add(filter);
                    }

                    lock (_subscribedTopics)
                    {
                        for (int i = 0; i < unsubs.Count; i++)
                        {
                            _subscribedTopics.Remove(unsubs[i]);
                        }
                    }

                    Unsubscribed?.Invoke(this, unsubPacketId, unsubs.ToArray());
                    await SendUnsubAckAsync(unsubPacketId).ConfigureAwait(false);
                    break;

                case MqttPacketType.PingReq:
                    await SendPingRespAsync().ConfigureAwait(false);
                    break;

                case MqttPacketType.Disconnect:
                    _isClosed = true;
                    Dispose();
                    break;
            }
        }

        /// <summary>
        /// Sends a CONNACK packet to the client.
        /// </summary>
        public async Task SendConnAckAsync(MqttConnectReturnCode returnCode, bool sessionPresent)
        {
            byte[] packet = new byte[4];
            packet[0] = (byte)((byte)MqttPacketType.ConnAck << 4);
            packet[1] = 2; // Remaining length
            packet[2] = (byte)(sessionPresent ? 1 : 0);
            packet[3] = (byte)returnCode;

            await SendRawAsync(packet, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PUBLISH packet to the client.
        /// </summary>
        public async Task SendPublishAsync(string topic, byte[] payload, MqttQoS qos = MqttQoS.AtMostOnce, ushort packetId = 0)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (payload == null) payload = Array.Empty<byte>();

            int topicLen = Encoding.UTF8.GetByteCount(topic);
            int variableHeaderLen = 2 + topicLen + (qos > MqttQoS.AtMostOnce ? 2 : 0);
            int remainingLen = variableHeaderLen + payload.Length;

            byte[] buffer = new byte[1 + 4 + remainingLen];
            int offset = 0;

            byte flags = (byte)(((byte)qos << 1) & 0x06);
            buffer[offset++] = (byte)(((byte)MqttPacketType.Publish << 4) | flags);

            offset += ZeroMqttCodec.WriteVariableByteInteger(remainingLen, buffer.AsSpan(offset));
            ZeroMqttCodec.WriteMqttString(topic, buffer.AsSpan(), ref offset);

            if (qos > MqttQoS.AtMostOnce)
            {
                ZeroMqttCodec.WriteUInt16BigEndian(packetId, buffer.AsSpan(), ref offset);
            }

            if (payload.Length > 0)
            {
                Array.Copy(payload, 0, buffer, offset, payload.Length);
                offset += payload.Length;
            }

            await SendRawAsync(buffer.AsSpan(0, offset).ToArray(), CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PUBACK packet for QoS 1 acknowledgement.
        /// </summary>
        public async Task SendPubAckAsync(ushort packetId)
        {
            byte[] packet = new byte[4];
            packet[0] = (byte)((byte)MqttPacketType.PubAck << 4);
            packet[1] = 2;
            packet[2] = (byte)(packetId >> 8);
            packet[3] = (byte)packetId;

            await SendRawAsync(packet, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a SUBACK packet acknowledging topic subscriptions.
        /// </summary>
        public async Task SendSubAckAsync(ushort packetId, byte[] grantedQos)
        {
            int remainingLen = 2 + grantedQos.Length;
            byte[] packet = new byte[1 + 4 + remainingLen];
            int offset = 0;

            packet[offset++] = (byte)((byte)MqttPacketType.SubAck << 4);
            offset += ZeroMqttCodec.WriteVariableByteInteger(remainingLen, packet.AsSpan(offset));
            ZeroMqttCodec.WriteUInt16BigEndian(packetId, packet.AsSpan(), ref offset);

            for (int i = 0; i < grantedQos.Length; i++)
            {
                packet[offset++] = grantedQos[i];
            }

            await SendRawAsync(packet.AsSpan(0, offset).ToArray(), CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an UNSUBACK packet acknowledging unsubscription.
        /// </summary>
        public async Task SendUnsubAckAsync(ushort packetId)
        {
            byte[] packet = new byte[4];
            packet[0] = (byte)((byte)MqttPacketType.UnsubAck << 4);
            packet[1] = 2;
            packet[2] = (byte)(packetId >> 8);
            packet[3] = (byte)packetId;

            await SendRawAsync(packet, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PINGRESP packet in response to PINGREQ.
        /// </summary>
        public async Task SendPingRespAsync()
        {
            byte[] packet = new byte[2];
            packet[0] = (byte)((byte)MqttPacketType.PingResp << 4);
            packet[1] = 0;

            await SendRawAsync(packet, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task SendRawAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (!IsOpen) return;
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken).ConfigureAwait(false);
                if (read <= 0) return false;
                totalRead += read;
            }
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _isClosed = true;
            try { _cts.Cancel(); } catch { }
            try { _stream.Dispose(); } catch { }
            try { _tcpClient.Close(); } catch { }
            _cts.Dispose();
            _sendLock.Dispose();
        }
    }
}
