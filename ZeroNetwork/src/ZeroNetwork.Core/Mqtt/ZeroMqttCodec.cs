using System;
using System.Text;

namespace ZeroNetwork.Mqtt
{
    /// <summary>
    /// High-performance, zero-allocation binary encoder and decoder for OASIS MQTT v3.1.1 packet structures.
    /// Handles Variable Byte Integers, two-byte Big-Endian length prefixes, and UTF-8 payloads.
    /// </summary>
    public static class ZeroMqttCodec
    {
        /// <summary>
        /// Reads an MQTT Variable Byte Integer from the source span.
        /// </summary>
        /// <param name="source">The input byte span.</param>
        /// <param name="bytesConsumed">The number of bytes read (1 to 4).</param>
        /// <returns>The decoded integer value.</returns>
        public static int ReadVariableByteInteger(ReadOnlySpan<byte> source, out int bytesConsumed)
        {
            int multiplier = 1;
            int value = 0;
            bytesConsumed = 0;

            for (int i = 0; i < source.Length && i < 4; i++)
            {
                byte encodedByte = source[i];
                bytesConsumed++;
                value += (encodedByte & 127) * multiplier;

                if ((encodedByte & 128) == 0)
                {
                    return value;
                }

                multiplier *= 128;
            }

            if (bytesConsumed == 0)
                throw new ArgumentException("Insufficient bytes for Variable Byte Integer.", nameof(source));

            return value;
        }

        /// <summary>
        /// Writes an MQTT Variable Byte Integer into the destination span.
        /// </summary>
        /// <param name="value">The integer to encode (0 to 268,435,455).</param>
        /// <param name="destination">The destination byte span.</param>
        /// <returns>The number of bytes written (1 to 4).</returns>
        public static int WriteVariableByteInteger(int value, Span<byte> destination)
        {
            if (value < 0 || value > 268435455)
                throw new ArgumentOutOfRangeException(nameof(value), "Variable Byte Integer must be between 0 and 268,435,455.");

            int bytesWritten = 0;
            do
            {
                byte encodedByte = (byte)(value % 128);
                value /= 128;

                if (value > 0)
                {
                    encodedByte |= 128;
                }

                destination[bytesWritten++] = encodedByte;
            }
            while (value > 0);

            return bytesWritten;
        }

        /// <summary>
        /// Reads a 16-bit Big-Endian unsigned integer from the span.
        /// </summary>
        public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source, ref int offset)
        {
            if (offset + 2 > source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            ushort val = (ushort)((source[offset] << 8) | source[offset + 1]);
            offset += 2;
            return val;
        }

        /// <summary>
        /// Writes a 16-bit Big-Endian unsigned integer into the destination span.
        /// </summary>
        public static void WriteUInt16BigEndian(ushort value, Span<byte> destination, ref int offset)
        {
            if (offset + 2 > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            destination[offset++] = (byte)(value >> 8);
            destination[offset++] = (byte)value;
        }

        /// <summary>
        /// Reads an MQTT UTF-8 encoded string (2-byte length prefix + UTF-8 payload).
        /// </summary>
        public static string ReadMqttString(ReadOnlySpan<byte> source, ref int offset)
        {
            ushort length = ReadUInt16BigEndian(source, ref offset);
            if (length == 0) return string.Empty;

            if (offset + length > source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            string result = Encoding.UTF8.GetString(source.Slice(offset, length));
#else
            string result = Encoding.UTF8.GetString(source.Slice(offset, length).ToArray());
#endif
            offset += length;
            return result;
        }

        /// <summary>
        /// Writes an MQTT UTF-8 encoded string (2-byte length prefix + UTF-8 payload).
        /// </summary>
        public static void WriteMqttString(string value, Span<byte> destination, ref int offset)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUInt16BigEndian(0, destination, ref offset);
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > 65535)
                throw new ArgumentException("MQTT string exceeds 65535 byte limit.", nameof(value));

            WriteUInt16BigEndian((ushort)byteCount, destination, ref offset);

#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Encoding.UTF8.GetBytes(value, destination.Slice(offset, byteCount));
            offset += byteCount;
#else
            byte[] rawBytes = Encoding.UTF8.GetBytes(value);
            rawBytes.CopyTo(destination.Slice(offset, byteCount));
            offset += byteCount;
#endif
        }
    }
}
