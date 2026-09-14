using System;
using System.Text;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// High-speed, zero-allocation binary encoder for MQTT packets.
    /// </summary>
    public static class MqttPacketEncoder
    {
        /// <summary>
        /// Encodes a variable length integer into destination span.
        /// </summary>
        public static int EncodeVariableLength(int value, Span<byte> destination)
        {
            if (value < 0 || value > 268435455)
                throw new ArgumentOutOfRangeException(nameof(value), "Variable length integer must be between 0 and 268,435,455.");

            int count = 0;
            do
            {
                byte encodedByte = (byte)(value % 128);
                value /= 128;
                if (value > 0)
                {
                    encodedByte |= 0x80;
                }
                destination[count++] = encodedByte;
            } while (value > 0);

            return count;
        }

        /// <summary>
        /// Calculates the number of bytes required to encode a variable length integer.
        /// </summary>
        public static int GetVariableLengthSize(int value)
        {
            if (value <= 127) return 1;
            if (value <= 16383) return 2;
            if (value <= 2097151) return 3;
            return 4;
        }

        /// <summary>
        /// Encodes an MQTT UTF-8 string (2-byte big-endian length + UTF-8 payload).
        /// </summary>
        public static int EncodeString(string str, Span<byte> destination)
        {
            if (string.IsNullOrEmpty(str))
            {
                destination[0] = 0;
                destination[1] = 0;
                return 2;
            }

            int byteCount = Encoding.UTF8.GetByteCount(str);
            if (byteCount > 65535)
                throw new ArgumentException("String length exceeds 65535 bytes.", nameof(str));

            destination[0] = (byte)(byteCount >> 8);
            destination[1] = (byte)byteCount;
            Encoding.UTF8.GetBytes(str, 0, str.Length, destination.Slice(2).ToArray(), 0);
            return 2 + byteCount;
        }

        /// <summary>
        /// Encodes a CONNECT packet into a new byte array.
        /// </summary>
        public static byte[] EncodeConnect(string clientId, bool cleanSession = true, ushort keepAliveSeconds = 60,
            string? username = null, string? password = null)
        {
            // Protocol name: "MQTT", level: 4 (3.1.1)
            byte[] protoBytes = Encoding.UTF8.GetBytes("MQTT");
            int variableHeaderLen = 2 + protoBytes.Length + 1 + 1 + 2; // name (2+4) + level (1) + flags (1) + keepalive (2) = 10

            byte connectFlags = 0;
            if (cleanSession) connectFlags |= 0x02;
            if (!string.IsNullOrEmpty(username)) connectFlags |= 0x80;
            if (!string.IsNullOrEmpty(password)) connectFlags |= 0x40;

            byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId ?? string.Empty);
            int payloadLen = 2 + clientIdBytes.Length;

            byte[]? userBytes = username != null ? Encoding.UTF8.GetBytes(username) : null;
            if (userBytes != null) payloadLen += 2 + userBytes.Length;

            byte[]? passBytes = password != null ? Encoding.UTF8.GetBytes(password) : null;
            if (passBytes != null) payloadLen += 2 + passBytes.Length;

            int remainingLength = variableHeaderLen + payloadLen;
            int varLenSize = GetVariableLengthSize(remainingLength);
            byte[] packet = new byte[1 + varLenSize + remainingLength];

            int offset = 0;
            packet[offset++] = (byte)((byte)MqttPacketType.Connect << 4);
            offset += EncodeVariableLength(remainingLength, packet.AsSpan(offset));

            // Variable Header
            packet[offset++] = 0x00;
            packet[offset++] = (byte)protoBytes.Length;
            Array.Copy(protoBytes, 0, packet, offset, protoBytes.Length);
            offset += protoBytes.Length;

            packet[offset++] = 0x04; // MQTT 3.1.1
            packet[offset++] = connectFlags;
            packet[offset++] = (byte)(keepAliveSeconds >> 8);
            packet[offset++] = (byte)(keepAliveSeconds & 0xFF);

            // Payload: ClientId
            packet[offset++] = (byte)(clientIdBytes.Length >> 8);
            packet[offset++] = (byte)(clientIdBytes.Length & 0xFF);
            Array.Copy(clientIdBytes, 0, packet, offset, clientIdBytes.Length);
            offset += clientIdBytes.Length;

            // Username
            if (userBytes != null)
            {
                packet[offset++] = (byte)(userBytes.Length >> 8);
                packet[offset++] = (byte)(userBytes.Length & 0xFF);
                Array.Copy(userBytes, 0, packet, offset, userBytes.Length);
                offset += userBytes.Length;
            }

            // Password
            if (passBytes != null)
            {
                packet[offset++] = (byte)(passBytes.Length >> 8);
                packet[offset++] = (byte)(passBytes.Length & 0xFF);
                Array.Copy(passBytes, 0, packet, offset, passBytes.Length);
                offset += passBytes.Length;
            }

            return packet;
        }

        /// <summary>
        /// Encodes a PUBLISH packet.
        /// </summary>
        public static byte[] EncodePublish(string topic, ReadOnlySpan<byte> payload, MqttQoS qos = MqttQoS.AtMostOnce,
            bool retain = false, bool dup = false, ushort packetId = 0)
        {
            if (string.IsNullOrEmpty(topic))
                throw new ArgumentNullException(nameof(topic));

            byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
            int variableHeaderLen = 2 + topicBytes.Length;
            if (qos > MqttQoS.AtMostOnce)
            {
                variableHeaderLen += 2; // Packet identifier
            }

            int remainingLength = variableHeaderLen + payload.Length;
            int varLenSize = GetVariableLengthSize(remainingLength);
            byte[] packet = new byte[1 + varLenSize + remainingLength];

            int offset = 0;
            byte firstByte = (byte)((byte)MqttPacketType.Publish << 4);
            if (dup) firstByte |= 0x08;
            firstByte |= (byte)((byte)qos << 1);
            if (retain) firstByte |= 0x01;
            packet[offset++] = firstByte;

            offset += EncodeVariableLength(remainingLength, packet.AsSpan(offset));

            // Topic Name
            packet[offset++] = (byte)(topicBytes.Length >> 8);
            packet[offset++] = (byte)(topicBytes.Length & 0xFF);
            Array.Copy(topicBytes, 0, packet, offset, topicBytes.Length);
            offset += topicBytes.Length;

            // Packet ID
            if (qos > MqttQoS.AtMostOnce)
            {
                packet[offset++] = (byte)(packetId >> 8);
                packet[offset++] = (byte)(packetId & 0xFF);
            }

            // Payload
            if (!payload.IsEmpty)
            {
                payload.CopyTo(packet.AsSpan(offset));
            }

            return packet;
        }

        /// <summary>
        /// Encodes a PUBACK packet (response to QoS 1 publish).
        /// </summary>
        public static byte[] EncodePubAck(ushort packetId)
        {
            return new byte[]
            {
                (byte)((byte)MqttPacketType.PubAck << 4),
                0x02,
                (byte)(packetId >> 8),
                (byte)(packetId & 0xFF)
            };
        }

        /// <summary>
        /// Encodes a SUBSCRIBE packet.
        /// </summary>
        public static byte[] EncodeSubscribe(ushort packetId, params (string topic, MqttQoS qos)[] subscriptions)
        {
            if (subscriptions == null || subscriptions.Length == 0)
                throw new ArgumentException("At least one subscription is required.", nameof(subscriptions));

            int payloadLen = 0;
            byte[][] topicBytesList = new byte[subscriptions.Length][];
            for (int i = 0; i < subscriptions.Length; i++)
            {
                topicBytesList[i] = Encoding.UTF8.GetBytes(subscriptions[i].topic);
                payloadLen += 2 + topicBytesList[i].Length + 1; // 2 len + topic + 1 QoS
            }

            int remainingLength = 2 + payloadLen; // 2 packetId + payload
            int varLenSize = GetVariableLengthSize(remainingLength);
            byte[] packet = new byte[1 + varLenSize + remainingLength];

            int offset = 0;
            packet[offset++] = (byte)(((byte)MqttPacketType.Subscribe << 4) | 0x02); // Reserved bit 1 must be 1
            offset += EncodeVariableLength(remainingLength, packet.AsSpan(offset));

            packet[offset++] = (byte)(packetId >> 8);
            packet[offset++] = (byte)(packetId & 0xFF);

            for (int i = 0; i < subscriptions.Length; i++)
            {
                byte[] tb = topicBytesList[i];
                packet[offset++] = (byte)(tb.Length >> 8);
                packet[offset++] = (byte)(tb.Length & 0xFF);
                Array.Copy(tb, 0, packet, offset, tb.Length);
                offset += tb.Length;
                packet[offset++] = (byte)subscriptions[i].qos;
            }

            return packet;
        }

        /// <summary>
        /// Encodes a PINGREQ packet.
        /// </summary>
        public static byte[] EncodePingReq()
        {
            return new byte[] { (byte)((byte)MqttPacketType.PingReq << 4), 0x00 };
        }

        /// <summary>
        /// Encodes a DISCONNECT packet.
        /// </summary>
        public static byte[] EncodeDisconnect()
        {
            return new byte[] { (byte)((byte)MqttPacketType.Disconnect << 4), 0x00 };
        }
    }
}
