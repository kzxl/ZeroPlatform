using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Mqtt;
using ZeroNetwork.PubSub;

namespace ZeroNetwork.Tests
{
    public class ZeroMqttServerTests
    {
        #region Codec Tests

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(16383)]
        [InlineData(16384)]
        [InlineData(2097151)]
        [InlineData(268435455)]
        public void ZeroMqttCodec_VariableByteInteger_RoundTrip_Succeeds(int expectedValue)
        {
            Span<byte> buffer = stackalloc byte[4];
            int bytesWritten = ZeroMqttCodec.WriteVariableByteInteger(expectedValue, buffer);

            Assert.True(bytesWritten >= 1 && bytesWritten <= 4);
            int decoded = ZeroMqttCodec.ReadVariableByteInteger(buffer.Slice(0, bytesWritten), out int bytesConsumed);

            Assert.Equal(bytesWritten, bytesConsumed);
            Assert.Equal(expectedValue, decoded);
        }

        [Fact]
        public void ZeroMqttCodec_UInt16AndString_RoundTrip_Succeeds()
        {
            byte[] buffer = new byte[256];
            int writeOffset = 0;

            ZeroMqttCodec.WriteUInt16BigEndian(1883, buffer, ref writeOffset);
            string testTopic = "factory/hanoi/line1/sensor/áp-suất-nhiệt-độ";
            ZeroMqttCodec.WriteMqttString(testTopic, buffer, ref writeOffset);

            int readOffset = 0;
            ushort port = ZeroMqttCodec.ReadUInt16BigEndian(buffer, ref readOffset);
            string decodedTopic = ZeroMqttCodec.ReadMqttString(buffer, ref readOffset);

            Assert.Equal(1883, port);
            Assert.Equal(testTopic, decodedTopic);
            Assert.Equal(writeOffset, readOffset);
        }

        #endregion

        #region Protocol Helpers

        private static async Task SendMqttConnectAsync(NetworkStream stream, string clientId)
        {
            byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);
            int varHeaderLen = 10; // "MQTT" (6) + level (1) + flags (1) + keepalive (2)
            int payloadLen = 2 + clientIdBytes.Length;
            int remainingLen = varHeaderLen + payloadLen;

            byte[] packet = new byte[1 + 4 + remainingLen];
            int offset = 0;
            packet[offset++] = 0x10; // CONNECT packet type
            offset += ZeroMqttCodec.WriteVariableByteInteger(remainingLen, packet.AsSpan(offset));

            // Variable header
            ZeroMqttCodec.WriteMqttString("MQTT", packet, ref offset);
            packet[offset++] = 4;    // Protocol Level (v3.1.1)
            packet[offset++] = 0x02; // Clean Session
            ZeroMqttCodec.WriteUInt16BigEndian(60, packet, ref offset); // Keep Alive 60s

            // Payload
            ZeroMqttCodec.WriteMqttString(clientId, packet, ref offset);

            await stream.WriteAsync(packet, 0, offset);
            await stream.FlushAsync();
        }

        private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var token = cts.Token;

            byte[] header = new byte[1];
            int r0 = await stream.ReadAsync(header, 0, 1, token);
            if (r0 <= 0) throw new EndOfStreamException("Stream closed while reading packet header.");
            byte b0 = header[0];

            int multiplier = 1;
            int remainingLength = 0;
            while (true)
            {
                int r1 = await stream.ReadAsync(header, 0, 1, token);
                if (r1 <= 0) throw new EndOfStreamException("Stream closed while reading remaining length.");
                byte eb = header[0];
                remainingLength += (eb & 127) * multiplier;
                if ((eb & 128) == 0) break;
                multiplier *= 128;
            }

            byte[] payload = new byte[remainingLength];
            if (remainingLength > 0)
            {
                int totalRead = 0;
                while (totalRead < remainingLength)
                {
                    int r = await stream.ReadAsync(payload, totalRead, remainingLength - totalRead, token);
                    if (r <= 0) throw new EndOfStreamException("Stream closed while reading packet payload.");
                    totalRead += r;
                }
            }

            byte[] fullPacket = new byte[1 + payload.Length];
            fullPacket[0] = b0;
            Array.Copy(payload, 0, fullPacket, 1, payload.Length);
            return fullPacket;
        }

        private static async Task SendMqttSubscribeAsync(NetworkStream stream, ushort packetId, string topicFilter, byte qos)
        {
            byte[] topicBytes = Encoding.UTF8.GetBytes(topicFilter);
            int remainingLen = 2 + (2 + topicBytes.Length) + 1; // packetId (2) + topic (2 + len) + qos (1)

            byte[] packet = new byte[1 + 4 + remainingLen];
            int offset = 0;
            packet[offset++] = 0x82; // SUBSCRIBE packet type (flags = 0010)
            offset += ZeroMqttCodec.WriteVariableByteInteger(remainingLen, packet.AsSpan(offset));

            ZeroMqttCodec.WriteUInt16BigEndian(packetId, packet, ref offset);
            ZeroMqttCodec.WriteMqttString(topicFilter, packet, ref offset);
            packet[offset++] = qos;

            await stream.WriteAsync(packet, 0, offset);
            await stream.FlushAsync();
        }

        private static async Task SendMqttPublishAsync(NetworkStream stream, string topic, byte[] data, MqttQoS qos = MqttQoS.AtMostOnce, ushort packetId = 0)
        {
            byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
            int varHeaderLen = 2 + topicBytes.Length + (qos > MqttQoS.AtMostOnce ? 2 : 0);
            int remainingLen = varHeaderLen + data.Length;

            byte[] packet = new byte[1 + 4 + remainingLen];
            int offset = 0;
            byte flags = (byte)(((byte)qos << 1) & 0x06);
            packet[offset++] = (byte)(0x30 | flags); // PUBLISH
            offset += ZeroMqttCodec.WriteVariableByteInteger(remainingLen, packet.AsSpan(offset));

            ZeroMqttCodec.WriteMqttString(topic, packet, ref offset);
            if (qos > MqttQoS.AtMostOnce)
            {
                ZeroMqttCodec.WriteUInt16BigEndian(packetId, packet, ref offset);
            }
            Array.Copy(data, 0, packet, offset, data.Length);
            offset += data.Length;

            await stream.WriteAsync(packet, 0, offset);
            await stream.FlushAsync();
        }

        #endregion

        #region Server Integration Tests

        [Fact]
        public async Task ZeroMqttServer_ConnectAndConnack_Handshake_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            Assert.True(server.IsRunning);
            int port = server.Port;

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await SendMqttConnectAsync(stream, "TestClient_01");

            byte[] connAckPacket = await ReadPacketAsync(stream);
            Assert.Equal(0x20, connAckPacket[0]); // CONNACK
            Assert.Equal(3, connAckPacket.Length); // header(1) + remaining(2: flags, returnCode)
            Assert.Equal(0x00, connAckPacket[2]); // ReturnCode: ConnectionAccepted
        }

        [Fact]
        public async Task ZeroMqttServer_PublishAndSubscribe_QoS0_RoutesMessage_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            int port = server.Port;

            // Client 1: Subscriber
            using var clientSub = new TcpClient();
            await clientSub.ConnectAsync("127.0.0.1", port);
            var streamSub = clientSub.GetStream();
            await SendMqttConnectAsync(streamSub, "SubClient");
            _ = await ReadPacketAsync(streamSub); // Consume CONNACK

            await SendMqttSubscribeAsync(streamSub, 101, "sensors/+/temperature", 0);
            byte[] subAckPacket = await ReadPacketAsync(streamSub);
            Assert.Equal(0x90, subAckPacket[0]); // SUBACK

            // Client 2: Publisher
            using var clientPub = new TcpClient();
            await clientPub.ConnectAsync("127.0.0.1", port);
            var streamPub = clientPub.GetStream();
            await SendMqttConnectAsync(streamPub, "PubClient");
            _ = await ReadPacketAsync(streamPub); // Consume CONNACK

            byte[] payload = Encoding.UTF8.GetBytes("Temperature: 28.5C");
            await SendMqttPublishAsync(streamPub, "sensors/machine1/temperature", payload, MqttQoS.AtMostOnce);

            // Client 1 should receive routed message
            byte[] routedPublish = await ReadPacketAsync(streamSub);
            Assert.Equal(0x30, routedPublish[0]); // PUBLISH QoS 0

            int offset = 1;
            ReadOnlySpan<byte> span = routedPublish.AsSpan();
            string receivedTopic = ZeroMqttCodec.ReadMqttString(span, ref offset);
            string receivedBody = Encoding.UTF8.GetString(span.Slice(offset).ToArray());

            Assert.Equal("sensors/machine1/temperature", receivedTopic);
            Assert.Equal("Temperature: 28.5C", receivedBody);
        }

        [Fact]
        public async Task ZeroMqttServer_WildcardRouting_MultiLevelHash_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            int port = server.Port;

            using var clientSub = new TcpClient();
            await clientSub.ConnectAsync("127.0.0.1", port);
            var streamSub = clientSub.GetStream();
            await SendMqttConnectAsync(streamSub, "WildcardSub");
            _ = await ReadPacketAsync(streamSub);

            // Subscribe to "factory/#"
            await SendMqttSubscribeAsync(streamSub, 1, "factory/#", 0);
            _ = await ReadPacketAsync(streamSub); // SUBACK

            // Publish message from server itself
            byte[] testPayload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            await server.PublishAsync("factory/zoneA/press/unit4/status", testPayload);

            byte[] receivedPacket = await ReadPacketAsync(streamSub);
            Assert.Equal(0x30, receivedPacket[0]);

            int offset = 1;
            ReadOnlySpan<byte> span = receivedPacket.AsSpan();
            string recTopic = ZeroMqttCodec.ReadMqttString(span, ref offset);
            byte[] recBody = span.Slice(offset).ToArray();

            Assert.Equal("factory/zoneA/press/unit4/status", recTopic);
            Assert.Equal(testPayload, recBody);
        }

        [Fact]
        public async Task ZeroMqttServer_QoS1_ReceivesPubAck_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            int port = server.Port;

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            await SendMqttConnectAsync(stream, "QoS1Client");
            _ = await ReadPacketAsync(stream);

            byte[] payload = Encoding.UTF8.GetBytes("Critical Alarm");
            ushort packetId = 4321;
            await SendMqttPublishAsync(stream, "alarms/fire", payload, MqttQoS.AtLeastOnce, packetId);

            byte[] pubAck = await ReadPacketAsync(stream);
            Assert.Equal(0x40, pubAck[0]); // PUBACK
            Assert.Equal(3, pubAck.Length); // header(1) + remaining(2: packetId)

            int offset = 1;
            ushort ackedId = ZeroMqttCodec.ReadUInt16BigEndian(pubAck.AsSpan(), ref offset);
            Assert.Equal(packetId, ackedId);
        }

        [Fact]
        public async Task ZeroMqttServer_PingReq_ReceivesPingResp_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            int port = server.Port;

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            await SendMqttConnectAsync(stream, "PingClient");
            _ = await ReadPacketAsync(stream);

            // Send PINGREQ
            byte[] pingReq = new byte[] { 0xC0, 0x00 };
            await stream.WriteAsync(pingReq, 0, pingReq.Length);
            await stream.FlushAsync();

            byte[] pingResp = await ReadPacketAsync(stream);
            Assert.Equal(0xD0, pingResp[0]); // PINGRESP
            Assert.Single(pingResp); // remaining length 0
        }

        [Fact]
        public async Task ZeroMqttServer_BridgeToBus_Bidirectional_Succeeds()
        {
            using var bus = new InProcessZeroBus();
            using var server = new ZeroMqttServer(0, "127.0.0.1", bus);
            server.Start();
            int port = server.Port;

            var busReceivedTcs = new TaskCompletionSource<(string Topic, string Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Bus subscribes to MQTT topics
            bus.SubscribeTopic("iot/#", (topic, payload) =>
            {
                busReceivedTcs.TrySetResult((topic, Encoding.UTF8.GetString(payload.ToArray())));
            });

            // MQTT Client publishes
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            await SendMqttConnectAsync(stream, "IotBridgeClient");
            _ = await ReadPacketAsync(stream);

            await SendMqttPublishAsync(stream, "iot/press/pressure", Encoding.UTF8.GetBytes("85.4 bar"));

            var busMsg = await busReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("iot/press/pressure", busMsg.Topic);
            Assert.Equal("85.4 bar", busMsg.Payload);
        }

        [Fact]
        public async Task ZeroMqttServer_DisconnectAndCleanup_Succeeds()
        {
            using var server = new ZeroMqttServer(0, "127.0.0.1");
            server.Start();
            int port = server.Port;

            var disconnectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            server.ClientDisconnected += s => disconnectTcs.TrySetResult(true);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port);
                var stream = client.GetStream();
                await SendMqttConnectAsync(stream, "DisconnectClient");
                _ = await ReadPacketAsync(stream);

                Assert.Equal(1, server.ConnectedClientsCount);

                // Send DISCONNECT packet (0xE0, 0x00)
                byte[] disconnectPacket = new byte[] { 0xE0, 0x00 };
                await stream.WriteAsync(disconnectPacket, 0, 2);
                await stream.FlushAsync();
            }

            bool disconnected = await disconnectTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(disconnected);
            Assert.Equal(0, server.ConnectedClientsCount);
        }

        #endregion
    }
}
