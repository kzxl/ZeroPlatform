using System;
using System.Text;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// Parsed MQTT Publish packet.
    /// </summary>
    public readonly ref struct MqttReceivedPublish
    {
        public string Topic { get; }
        public ReadOnlySpan<byte> Payload { get; }
        public MqttQoS QoS { get; }
        public bool Retain { get; }
        public bool Dup { get; }
        public ushort PacketId { get; }

        public MqttReceivedPublish(string topic, ReadOnlySpan<byte> payload, MqttQoS qos, bool retain, bool dup, ushort packetId)
        {
            Topic = topic;
            Payload = payload;
            QoS = qos;
            Retain = retain;
            Dup = dup;
            PacketId = packetId;
        }

        public string PayloadAsString => Encoding.UTF8.GetString(Payload.ToArray());
    }

    /// <summary>
    /// High-performance, zero-allocation binary decoder for incoming MQTT packets.
    /// </summary>
    public static class MqttPacketDecoder
    {
        /// <summary>
        /// Attempts to decode the fixed header from the buffer.
        /// </summary>
        public static bool TryDecodeFixedHeader(ReadOnlySpan<byte> buffer, out MqttFixedHeader header, out int headerLength)
        {
            header = default;
            headerLength = 0;

            if (buffer.Length < 2)
                return false;

            byte firstByte = buffer[0];
            MqttPacketType packetType = (MqttPacketType)(firstByte >> 4);
            bool dup = (firstByte & 0x08) != 0;
            MqttQoS qos = (MqttQoS)((firstByte & 0x06) >> 1);
            bool retain = (firstByte & 0x01) != 0;

            if (!TryDecodeVariableLength(buffer.Slice(1), out int remainingLength, out int varLenBytes))
                return false;

            headerLength = 1 + varLenBytes;
            header = new MqttFixedHeader(packetType, dup, qos, retain, remainingLength);
            return true;
        }

        /// <summary>
        /// Decodes a variable byte integer from the buffer.
        /// </summary>
        public static bool TryDecodeVariableLength(ReadOnlySpan<byte> buffer, out int value, out int bytesConsumed)
        {
            value = 0;
            bytesConsumed = 0;
            int multiplier = 1;

            for (int i = 0; i < buffer.Length && i < 4; i++)
            {
                byte encodedByte = buffer[i];
                bytesConsumed++;
                value += (encodedByte & 0x7F) * multiplier;
                multiplier *= 128;

                if ((encodedByte & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Decodes a CONNACK packet payload.
        /// </summary>
        public static bool TryDecodeConnAck(ReadOnlySpan<byte> remainingBuffer, out bool sessionPresent, out byte returnCode)
        {
            sessionPresent = false;
            returnCode = 0xFF;

            if (remainingBuffer.Length < 2)
                return false;

            sessionPresent = (remainingBuffer[0] & 0x01) != 0;
            returnCode = remainingBuffer[1];
            return true;
        }

        /// <summary>
        /// Decodes a PUBLISH packet from the remaining buffer after fixed header.
        /// </summary>
        public static bool TryDecodePublish(MqttFixedHeader header, ReadOnlySpan<byte> remainingBuffer, out MqttReceivedPublish publish)
        {
            publish = default;

            if (remainingBuffer.Length < 2)
                return false;

            int topicLen = (remainingBuffer[0] << 8) | remainingBuffer[1];
            if (remainingBuffer.Length < 2 + topicLen)
                return false;

            string topic = Encoding.UTF8.GetString(remainingBuffer.Slice(2, topicLen).ToArray());
            int offset = 2 + topicLen;

            ushort packetId = 0;
            if (header.QoS > MqttQoS.AtMostOnce)
            {
                if (remainingBuffer.Length < offset + 2)
                    return false;

                packetId = (ushort)((remainingBuffer[offset] << 8) | remainingBuffer[offset + 1]);
                offset += 2;
            }

            ReadOnlySpan<byte> payload = remainingBuffer.Slice(offset);
            publish = new MqttReceivedPublish(topic, payload, header.QoS, header.Retain, header.Dup, packetId);
            return true;
        }

        /// <summary>
        /// Decodes a PUBACK packet (extracts 2-byte packet ID).
        /// </summary>
        public static bool TryDecodePubAck(ReadOnlySpan<byte> remainingBuffer, out ushort packetId)
        {
            packetId = 0;
            if (remainingBuffer.Length < 2)
                return false;

            packetId = (ushort)((remainingBuffer[0] << 8) | remainingBuffer[1]);
            return true;
        }

        /// <summary>
        /// Decodes a SUBACK packet.
        /// </summary>
        public static bool TryDecodeSubAck(ReadOnlySpan<byte> remainingBuffer, out ushort packetId, out byte[] returnCodes)
        {
            packetId = 0;
            returnCodes = Array.Empty<byte>();

            if (remainingBuffer.Length < 2)
                return false;

            packetId = (ushort)((remainingBuffer[0] << 8) | remainingBuffer[1]);
            int returnCodeCount = remainingBuffer.Length - 2;
            returnCodes = new byte[returnCodeCount];
            for (int i = 0; i < returnCodeCount; i++)
            {
                returnCodes[i] = remainingBuffer[2 + i];
            }

            return true;
        }
    }
}
