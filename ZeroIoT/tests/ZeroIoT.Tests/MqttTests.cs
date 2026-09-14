using System;
using System.Text;
using Xunit;
using ZeroIoT.Mqtt;

namespace ZeroIoT.Tests
{
    public class MqttTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(127, 1)]
        [InlineData(128, 2)]
        [InlineData(16383, 2)]
        [InlineData(16384, 3)]
        [InlineData(2097151, 3)]
        [InlineData(2097152, 4)]
        [InlineData(268435455, 4)]
        public void VariableLength_RoundTrip_MatchesExpectedBytes(int value, int expectedBytes)
        {
            Span<byte> buffer = stackalloc byte[4];
            int encodedCount = MqttPacketEncoder.EncodeVariableLength(value, buffer);
            Assert.Equal(expectedBytes, encodedCount);

            bool decoded = MqttPacketDecoder.TryDecodeVariableLength(buffer.Slice(0, encodedCount), out int decodedVal, out int bytesConsumed);
            Assert.True(decoded);
            Assert.Equal(value, decodedVal);
            Assert.Equal(expectedBytes, bytesConsumed);
        }

        [Fact]
        public void FixedHeader_Decode_ExtractsPacketTypeAndFlags()
        {
            byte[] packet = new byte[] { (byte)((byte)MqttPacketType.Publish << 4 | 0x03), 0x0A }; // Retain=1, QoS=1 (0x02), total=0x03
            bool success = MqttPacketDecoder.TryDecodeFixedHeader(packet, out var header, out int headerLen);

            Assert.True(success);
            Assert.Equal(MqttPacketType.Publish, header.PacketType);
            Assert.Equal(MqttQoS.AtLeastOnce, header.QoS);
            Assert.True(header.Retain);
            Assert.False(header.Dup);
            Assert.Equal(10, header.RemainingLength);
            Assert.Equal(2, headerLen);
        }

        [Fact]
        public void Connect_Packet_EncodesCorrectly()
        {
            byte[] connectPacket = MqttPacketEncoder.EncodeConnect("TestClient_001", cleanSession: true, keepAliveSeconds: 30);
            Assert.NotNull(connectPacket);
            Assert.True(connectPacket.Length > 10);

            // First byte must be 0x10 (Connect << 4)
            Assert.Equal((byte)((byte)MqttPacketType.Connect << 4), connectPacket[0]);

            bool success = MqttPacketDecoder.TryDecodeFixedHeader(connectPacket, out var header, out int headerLen);
            Assert.True(success);
            Assert.Equal(MqttPacketType.Connect, header.PacketType);
            Assert.Equal(connectPacket.Length - headerLen, header.RemainingLength);
        }

        [Fact]
        public void ConnAck_Decoder_ParsesReturnCode()
        {
            byte[] connAck = new byte[] { (byte)((byte)MqttPacketType.ConnAck << 4), 0x02, 0x00, 0x00 }; // Accepted
            bool success = MqttPacketDecoder.TryDecodeFixedHeader(connAck, out var header, out int headerLen);
            Assert.True(success);
            Assert.Equal(MqttPacketType.ConnAck, header.PacketType);

            bool payloadOk = MqttPacketDecoder.TryDecodeConnAck(connAck.AsSpan(headerLen), out bool sessionPresent, out byte retCode);
            Assert.True(payloadOk);
            Assert.False(sessionPresent);
            Assert.Equal(0, retCode);
        }

        [Fact]
        public void Publish_QoS0_RoundTrip()
        {
            string topic = "factory/sensor/temperature";
            byte[] payload = Encoding.UTF8.GetBytes("{\"value\": 42.5}");

            byte[] packet = MqttPacketEncoder.EncodePublish(topic, payload, MqttQoS.AtMostOnce, retain: false);
            Assert.NotNull(packet);

            bool headerOk = MqttPacketDecoder.TryDecodeFixedHeader(packet, out var header, out int headerLen);
            Assert.True(headerOk);
            Assert.Equal(MqttPacketType.Publish, header.PacketType);
            Assert.Equal(MqttQoS.AtMostOnce, header.QoS);

            bool pubOk = MqttPacketDecoder.TryDecodePublish(header, packet.AsSpan(headerLen), out var pub);
            Assert.True(pubOk);
            Assert.Equal(topic, pub.Topic);
            Assert.Equal(payload, pub.Payload.ToArray());
            Assert.Equal("{\"value\": 42.5}", pub.PayloadAsString);
        }

        [Fact]
        public void Publish_QoS1_IncludesPacketId()
        {
            string topic = "robot/axis/position";
            byte[] payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ushort expectedPacketId = 1337;

            byte[] packet = MqttPacketEncoder.EncodePublish(topic, payload, MqttQoS.AtLeastOnce, packetId: expectedPacketId);

            bool headerOk = MqttPacketDecoder.TryDecodeFixedHeader(packet, out var header, out int headerLen);
            Assert.True(headerOk);
            Assert.Equal(MqttQoS.AtLeastOnce, header.QoS);

            bool pubOk = MqttPacketDecoder.TryDecodePublish(header, packet.AsSpan(headerLen), out var pub);
            Assert.True(pubOk);
            Assert.Equal(expectedPacketId, pub.PacketId);
            Assert.Equal(payload, pub.Payload.ToArray());
        }

        [Fact]
        public void Subscribe_And_SubAck_RoundTrip()
        {
            ushort packetId = 42;
            byte[] subPacket = MqttPacketEncoder.EncodeSubscribe(packetId, ("sensors/#", MqttQoS.AtLeastOnce), ("status/+", MqttQoS.AtMostOnce));
            Assert.NotNull(subPacket);

            byte[] subAckPacket = new byte[] { (byte)((byte)MqttPacketType.SubAck << 4), 0x04, 0x00, 0x2A, 0x01, 0x00 };
            bool headerOk = MqttPacketDecoder.TryDecodeFixedHeader(subAckPacket, out var header, out int headerLen);
            Assert.True(headerOk);
            Assert.Equal(MqttPacketType.SubAck, header.PacketType);

            bool subAckOk = MqttPacketDecoder.TryDecodeSubAck(subAckPacket.AsSpan(headerLen), out ushort ackId, out byte[] returnCodes);
            Assert.True(subAckOk);
            Assert.Equal(packetId, ackId);
            Assert.Equal(2, returnCodes.Length);
            Assert.Equal(1, returnCodes[0]);
            Assert.Equal(0, returnCodes[1]);
        }

        [Fact]
        public void PingReq_And_Disconnect_HaveZeroRemainingLength()
        {
            byte[] ping = MqttPacketEncoder.EncodePingReq();
            Assert.Equal(2, ping.Length);
            Assert.Equal((byte)((byte)MqttPacketType.PingReq << 4), ping[0]);
            Assert.Equal(0x00, ping[1]);

            byte[] disc = MqttPacketEncoder.EncodeDisconnect();
            Assert.Equal(2, disc.Length);
            Assert.Equal((byte)((byte)MqttPacketType.Disconnect << 4), disc[0]);
            Assert.Equal(0x00, disc[1]);
        }
    }
}
