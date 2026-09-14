using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroAudioVisual.Video
{
    /// <summary>
    /// Represents an RFC 3550 Real-time Transport Protocol (RTP) packet header and payload.
    /// </summary>
    public class RtpPacket
    {
        public byte Version { get; set; }
        public bool HasPadding { get; set; }
        public bool HasExtension { get; set; }
        public byte CsrcCount { get; set; }
        public bool Marker { get; set; }
        public byte PayloadType { get; set; }
        public ushort SequenceNumber { get; set; }
        public uint Timestamp { get; set; }
        public uint Ssrc { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public static bool TryParse(ReadOnlySpan<byte> data, out RtpPacket packet)
        {
            packet = null!;
            if (data.Length < 12) return false;

            byte b0 = data[0];
            byte version = (byte)((b0 >> 6) & 0x03);
            if (version != 2) return false; // Standard RTP is version 2

            bool padding = ((b0 >> 5) & 0x01) != 0;
            bool extension = ((b0 >> 4) & 0x01) != 0;
            byte cc = (byte)(b0 & 0x0F);

            byte b1 = data[1];
            bool marker = ((b1 >> 7) & 0x01) != 0;
            byte pt = (byte)(b1 & 0x7F);

            ushort seq = (ushort)((data[2] << 8) | data[3]);
            uint timestamp = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            uint ssrc = (uint)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);

            int headerLen = 12 + cc * 4;
            if (extension && data.Length >= headerLen + 4)
            {
                int extLen = ((data[headerLen + 2] << 8) | data[headerLen + 3]) * 4;
                headerLen += 4 + extLen;
            }

            if (data.Length < headerLen) return false;

            int payloadLen = data.Length - headerLen;
            if (padding && payloadLen > 0)
            {
                byte padCount = data[data.Length - 1];
                payloadLen -= padCount;
            }

            if (payloadLen < 0) return false;

            byte[] payload = new byte[payloadLen];
            data.Slice(headerLen, payloadLen).CopyTo(payload);

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
    }

    public enum NaluType : byte
    {
        Unspecified = 0,
        NonIdrSlice = 1,
        DataPartitionA = 2,
        DataPartitionB = 3,
        DataPartitionC = 4,
        IdrSlice = 5,
        Sei = 6,
        Sps = 7,
        Pps = 8,
        AccessUnitDelimiter = 9,
        EndOfSequence = 10,
        EndOfStream = 11,
        FilterData = 12,
        FuA = 28 // Fragmentation Unit A
    }

    /// <summary>
    /// Lightweight H.264 video NAL unit parser and RTP depacketizer for industrial IP cameras.
    /// </summary>
    public class H264RtpDepacketizer
    {
        private readonly MemoryStream _fuStream = new MemoryStream();
        private byte _fuNaluHeader = 0;
        private bool _isAssemblingFu = false;

        public event Action<byte[]>? OnNaluComplete;

        /// <summary>
        /// Processes an incoming RTP packet payload containing H.264 stream data (RFC 6184).
        /// </summary>
        public void ProcessRtpPayload(byte[] payload, bool marker)
        {
            if (payload == null || payload.Length == 0) return;

            byte naluHeader = payload[0];
            byte naluType = (byte)(naluHeader & 0x1F);

            // Single NAL unit packet (1-23)
            if (naluType >= 1 && naluType <= 23)
            {
                var nalu = new byte[payload.Length + 4];
                nalu[0] = 0; nalu[1] = 0; nalu[2] = 0; nalu[3] = 1; // 4-byte start code
                Buffer.BlockCopy(payload, 0, nalu, 4, payload.Length);
                OnNaluComplete?.Invoke(nalu);
                return;
            }

            // FU-A Fragmented packet (Type 28)
            if (naluType == (byte)NaluType.FuA && payload.Length >= 2)
            {
                byte fuHeader = payload[1];
                bool start = (fuHeader & 0x80) != 0;
                bool end = (fuHeader & 0x40) != 0;
                byte innerType = (byte)(fuHeader & 0x1F);

                if (start)
                {
                    _fuStream.SetLength(0);
                    // Reconstruct original NALU header: F & NRI from packet[0], Type from packet[1]
                    _fuNaluHeader = (byte)((naluHeader & 0xE0) | innerType);

                    // Write start code and header
                    _fuStream.Write(new byte[] { 0, 0, 0, 1, _fuNaluHeader }, 0, 5);
                    _fuStream.Write(payload, 2, payload.Length - 2);
                    _isAssemblingFu = true;
                }
                else if (_isAssemblingFu)
                {
                    _fuStream.Write(payload, 2, payload.Length - 2);
                }

                if (end && _isAssemblingFu)
                {
                    _isAssemblingFu = false;
                    OnNaluComplete?.Invoke(_fuStream.ToArray());
                }
            }
        }
    }
}
