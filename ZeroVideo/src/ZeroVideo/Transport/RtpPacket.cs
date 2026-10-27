using System;

namespace ZeroVideo.Transport
{
    /// <summary>
    /// Represents an RFC 3550 Real-time Transport Protocol (RTP) packet header and payload.
    /// Provides high-speed parsing and serialization for RTP video streaming feeds.
    /// </summary>
    public class RtpPacket
    {
        public byte Version { get; set; } = 2;
        public bool HasPadding { get; set; }
        public bool HasExtension { get; set; }
        public byte CsrcCount { get; set; }
        public bool Marker { get; set; }
        public byte PayloadType { get; set; } = 96; // Standard dynamic payload type (H.264)
        public ushort SequenceNumber { get; set; }
        public uint Timestamp { get; set; }
        public uint Ssrc { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Parses an RTP packet from raw byte data.
        /// </summary>
        public static bool TryParse(byte[] data, int offset, int count, out RtpPacket packet)
        {
            packet = null!;
            if (data == null || count < 12 || offset + count > data.Length) return false;

            byte b0 = data[offset];
            byte version = (byte)((b0 >> 6) & 0x03);
            if (version != 2) return false;

            bool padding = ((b0 >> 5) & 0x01) != 0;
            bool extension = ((b0 >> 4) & 0x01) != 0;
            byte cc = (byte)(b0 & 0x0F);

            byte b1 = data[offset + 1];
            bool marker = ((b1 >> 7) & 0x01) != 0;
            byte pt = (byte)(b1 & 0x7F);

            ushort seq = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            uint timestamp = (uint)((data[offset + 4] << 24) | (data[offset + 5] << 16) | (data[offset + 6] << 8) | data[offset + 7]);
            uint ssrc = (uint)((data[offset + 8] << 24) | (data[offset + 9] << 16) | (data[offset + 10] << 8) | data[offset + 11]);

            int headerLen = 12 + cc * 4;
            if (extension && count >= headerLen + 4)
            {
                int extLen = ((data[offset + headerLen + 2] << 8) | data[offset + headerLen + 3]) * 4;
                headerLen += 4 + extLen;
            }

            if (count < headerLen) return false;

            int payloadLen = count - headerLen;
            if (padding && payloadLen > 0)
            {
                byte padCount = data[offset + count - 1];
                payloadLen -= padCount;
            }

            if (payloadLen < 0) return false;

            byte[] payload = new byte[payloadLen];
            if (payloadLen > 0)
            {
                Buffer.BlockCopy(data, offset + headerLen, payload, 0, payloadLen);
            }

            packet = new RtpPacket
            {
                Version = version,
                HasPadding = padding,
                HasExtension = extension,
                CsrcCount = cc,
                Marker = marker,
                PayloadType = pt,
                SequenceNumber = seq,
                Timestamp = timestamp,
                Ssrc = ssrc,
                Payload = payload
            };
            return true;
        }

        public static bool TryParse(byte[] data, out RtpPacket packet)
        {
            if (data == null) { packet = null!; return false; }
            return TryParse(data, 0, data.Length, out packet);
        }

        /// <summary>
        /// Serializes this RTP packet into a byte array.
        /// </summary>
        public byte[] ToByteArray()
        {
            int totalLen = 12 + Payload.Length;
            byte[] buf = new byte[totalLen];

            buf[0] = (byte)((Version << 6) | ((HasPadding ? 1 : 0) << 5) | ((HasExtension ? 1 : 0) << 4) | (CsrcCount & 0x0F));
            buf[1] = (byte)(((Marker ? 1 : 0) << 7) | (PayloadType & 0x7F));
            buf[2] = (byte)(SequenceNumber >> 8);
            buf[3] = (byte)(SequenceNumber & 0xFF);
            buf[4] = (byte)(Timestamp >> 24);
            buf[5] = (byte)((Timestamp >> 16) & 0xFF);
            buf[6] = (byte)((Timestamp >> 8) & 0xFF);
            buf[7] = (byte)(Timestamp & 0xFF);
            buf[8] = (byte)(Ssrc >> 24);
            buf[9] = (byte)((Ssrc >> 16) & 0xFF);
            buf[10] = (byte)((Ssrc >> 8) & 0xFF);
            buf[11] = (byte)(Ssrc & 0xFF);

            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(Payload, 0, buf, 12, Payload.Length);
            }

            return buf;
        }
    }
}
