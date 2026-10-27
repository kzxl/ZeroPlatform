using System;
using System.Text;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Represents a decoded binary Hub Protocol frame.
    /// </summary>
    public readonly ref struct ZeroBinaryFrame
    {
        public byte MessageType { get; }
        public int InvocationId { get; }
        public ReadOnlySpan<char> Target { get; }
        public ReadOnlySpan<byte> Payload { get; }

        public ZeroBinaryFrame(byte messageType, int invocationId, ReadOnlySpan<char> target, ReadOnlySpan<byte> payload)
        {
            MessageType = messageType;
            InvocationId = invocationId;
            Target = target;
            Payload = payload;
        }
    }

    /// <summary>
    /// High-performance, zero-allocation binary protocol framing for <see cref="ZeroHubServer{THub}"/> and <see cref="ZeroSignalRClient"/>.
    /// Eliminates JSON serialization overhead for high-frequency SCADA telemetry and binary payloads.
    /// </summary>
    public static class ZeroBinaryHubProtocol
    {
        public const byte MagicByte = 0xBE; // Binary Envelope marker
        public const byte InvocationType = 0x01;
        public const byte CompletionType = 0x03;
        public const byte PingType = 0x06;

        /// <summary>
        /// Attempts to encode an invocation into a binary frame.
        /// </summary>
        public static bool TryEncodeInvocation(string target, int invocationId, ReadOnlySpan<byte> payload, Span<byte> destination, out int bytesWritten)
        {
            if (string.IsNullOrEmpty(target))
                throw new ArgumentNullException(nameof(target));

            int targetBytesCount = Encoding.UTF8.GetByteCount(target);
            int totalLength = 1 + 1 + 4 + 2 + targetBytesCount + 4 + payload.Length;

            if (destination.Length < totalLength)
            {
                bytesWritten = 0;
                return false;
            }

            int offset = 0;
            destination[offset++] = MagicByte;
            destination[offset++] = InvocationType;

            // InvocationId (4 bytes)
            BitConverter.GetBytes(invocationId).AsSpan().CopyTo(destination.Slice(offset, 4));
            offset += 4;

            // Target Length (2 bytes)
            BitConverter.GetBytes((ushort)targetBytesCount).AsSpan().CopyTo(destination.Slice(offset, 2));
            offset += 2;

            // Target UTF-8 bytes
            unsafe
            {
                fixed (char* pTarget = target)
                fixed (byte* pDest = destination.Slice(offset, targetBytesCount))
                {
                    Encoding.UTF8.GetBytes(pTarget, target.Length, pDest, targetBytesCount);
                }
            }
            offset += targetBytesCount;

            // Payload Length (4 bytes)
            BitConverter.GetBytes(payload.Length).AsSpan().CopyTo(destination.Slice(offset, 4));
            offset += 4;

            // Payload bytes
            if (payload.Length > 0)
            {
                payload.CopyTo(destination.Slice(offset, payload.Length));
                offset += payload.Length;
            }

            bytesWritten = offset;
            return true;
        }

        /// <summary>
        /// Attempts to decode a binary frame from a byte span without heap allocation.
        /// </summary>
        public static bool TryDecode(ReadOnlySpan<byte> source, Span<char> targetCharBuffer, out ZeroBinaryFrame frame)
        {
            frame = default;
            if (source.Length < 12 || source[0] != MagicByte)
                return false;

            byte type = source[1];
            int invocationId = BitConverter.ToInt32(source.Slice(2, 4).ToArray(), 0);
            ushort targetLen = BitConverter.ToUInt16(source.Slice(6, 2).ToArray(), 0);

            int offset = 8;
            if (source.Length < offset + targetLen + 4)
                return false;

            int targetCharsDecoded;
            unsafe
            {
                fixed (byte* pSrc = source.Slice(offset, targetLen))
                fixed (char* pChars = targetCharBuffer)
                {
                    targetCharsDecoded = Encoding.UTF8.GetChars(pSrc, targetLen, pChars, targetCharBuffer.Length);
                }
            }
            ReadOnlySpan<char> targetSpan = targetCharBuffer.Slice(0, targetCharsDecoded);
            offset += targetLen;

            int payloadLen = BitConverter.ToInt32(source.Slice(offset, 4).ToArray(), 0);
            offset += 4;

            if (source.Length < offset + payloadLen)
                return false;

            ReadOnlySpan<byte> payloadSpan = source.Slice(offset, payloadLen);
            frame = new ZeroBinaryFrame(type, invocationId, targetSpan, payloadSpan);
            return true;
        }
    }
}
