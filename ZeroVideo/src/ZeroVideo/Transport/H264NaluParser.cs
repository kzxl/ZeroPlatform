using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroVideo.Transport
{
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
        FuA = 28 // RFC 6184 Fragmentation Unit A
    }

    /// <summary>
    /// Represents an individual H.264 Network Abstraction Layer Unit (NALU).
    /// </summary>
    public class H264Nalu
    {
        public NaluType Type { get; }
        public byte RefIdc { get; }
        public byte[] Payload { get; }

        public bool IsKeyFrame => Type == NaluType.IdrSlice;
        public bool IsParameterSet => Type == NaluType.Sps || Type == NaluType.Pps;

        public H264Nalu(byte header, byte[] payload)
        {
            RefIdc = (byte)((header >> 5) & 0x03);
            Type = (NaluType)(header & 0x1F);
            Payload = payload ?? Array.Empty<byte>();
        }

        public H264Nalu(NaluType type, byte refIdc, byte[] payload)
        {
            Type = type;
            RefIdc = refIdc;
            Payload = payload ?? Array.Empty<byte>();
        }

        public byte GetHeaderByte()
        {
            return (byte)(((RefIdc & 0x03) << 5) | ((byte)Type & 0x1F));
        }

        /// <summary>
        /// Wraps NALU into an Annex B byte sequence with 4-byte start code (0x00, 0x00, 0x00, 0x01).
        /// </summary>
        public byte[] ToAnnexB()
        {
            byte[] buf = new byte[4 + 1 + Payload.Length];
            buf[0] = 0x00;
            buf[1] = 0x00;
            buf[2] = 0x00;
            buf[3] = 0x01;
            buf[4] = GetHeaderByte();
            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(Payload, 0, buf, 5, Payload.Length);
            }
            return buf;
        }
    }

    /// <summary>
    /// Parser and Annex B stream scanner for H.264 AVC NAL units.
    /// </summary>
    public static class H264NaluParser
    {
        /// <summary>
        /// Extracts all complete NAL units from an Annex B byte stream containing 3-byte or 4-byte start codes.
        /// </summary>
        public static List<H264Nalu> ExtractNalus(byte[] data, int offset, int length)
        {
            var nalus = new List<H264Nalu>();
            if (data == null || length < 4) return nalus;

            int end = offset + length;
            int startCodePos = -1;
            int startCodeLen = 0;

            int i = offset;
            while (i < end - 3)
            {
                if (data[i] == 0 && data[i + 1] == 0)
                {
                    if (data[i + 2] == 1) // 3-byte start code
                    {
                        if (startCodePos >= 0)
                        {
                            int naluDataStart = startCodePos + startCodeLen;
                            int naluLen = i - naluDataStart;
                            if (naluLen > 0)
                            {
                                byte header = data[naluDataStart];
                                byte[] payload = new byte[naluLen - 1];
                                Buffer.BlockCopy(data, naluDataStart + 1, payload, 0, payload.Length);
                                nalus.Add(new H264Nalu(header, payload));
                            }
                        }
                        startCodePos = i;
                        startCodeLen = 3;
                        i += 3;
                        continue;
                    }
                    else if (data[i + 2] == 0 && data[i + 3] == 1) // 4-byte start code
                    {
                        if (startCodePos >= 0)
                        {
                            int naluDataStart = startCodePos + startCodeLen;
                            int naluLen = i - naluDataStart;
                            if (naluLen > 0)
                            {
                                byte header = data[naluDataStart];
                                byte[] payload = new byte[naluLen - 1];
                                Buffer.BlockCopy(data, naluDataStart + 1, payload, 0, payload.Length);
                                nalus.Add(new H264Nalu(header, payload));
                            }
                        }
                        startCodePos = i;
                        startCodeLen = 4;
                        i += 4;
                        continue;
                    }
                }
                i++;
            }

            // Final NALU to the end of buffer
            if (startCodePos >= 0)
            {
                int naluDataStart = startCodePos + startCodeLen;
                int naluLen = end - naluDataStart;
                if (naluLen > 0)
                {
                    byte header = data[naluDataStart];
                    byte[] payload = new byte[naluLen - 1];
                    Buffer.BlockCopy(data, naluDataStart + 1, payload, 0, payload.Length);
                    nalus.Add(new H264Nalu(header, payload));
                }
            }

            return nalus;
        }

        public static List<H264Nalu> ExtractNalus(byte[] data)
        {
            if (data == null) return new List<H264Nalu>();
            return ExtractNalus(data, 0, data.Length);
        }
    }

    /// <summary>
    /// Reassembles RFC 6184 Fragmentation Unit A (FU-A) packets transmitted over RTP into a single complete NALU.
    /// Supports RTP sequence number continuity verification and zero-allocation reusable assembly buffering.
    /// </summary>
    public class H264FuAReassembler
    {
        private byte[] _reassemblyBuffer = new byte[65536];
        private int _assembledLength = 0;
        private byte _naluHeader;
        private bool _isAssembling;
        private int _lastSequenceNumber = -1;

        public bool IsAssembling => _isAssembling;
        public int AssembledBytesCount => _assembledLength;

        public void Reset()
        {
            _assembledLength = 0;
            _isAssembling = false;
            _lastSequenceNumber = -1;
        }

        private void EnsureCapacity(int needed)
        {
            if (_assembledLength + needed > _reassemblyBuffer.Length)
            {
                int newCap = Math.Max(_reassemblyBuffer.Length * 2, _assembledLength + needed);
                Array.Resize(ref _reassemblyBuffer, newCap);
            }
        }

        /// <summary>
        /// Processes a full RTP packet containing FU-A fragments or single NAL unit with sequence checking.
        /// </summary>
        public H264Nalu? ProcessRtpPacket(RtpPacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            return ProcessRtpPayload(packet.Payload, packet.SequenceNumber);
        }

        /// <summary>
        /// Processes an RTP payload containing FU-A or single NAL unit with sequence number continuity check.
        /// If a sequence gap is detected while assembling, the incomplete NAL unit is aborted and dropped.
        /// </summary>
        public H264Nalu? ProcessRtpPayload(byte[] payload, ushort sequenceNumber)
        {
            return ProcessInternal(payload, (int)sequenceNumber);
        }

        /// <summary>
        /// Processes an RTP payload containing FU-A or single NAL unit without sequence continuity checking.
        /// </summary>
        public H264Nalu? ProcessRtpPayload(byte[] payload)
        {
            return ProcessInternal(payload, -1);
        }

        private H264Nalu? ProcessInternal(byte[] payload, int sequenceNumber)
        {
            if (payload == null || payload.Length < 2) return null;

            byte indicator = payload[0];
            NaluType type = (NaluType)(indicator & 0x1F);

            if (type == NaluType.FuA)
            {
                byte fuHeader = payload[1];
                bool start = (fuHeader & 0x80) != 0;
                bool end = (fuHeader & 0x40) != 0;
                NaluType originalType = (NaluType)(fuHeader & 0x1F);
                byte refIdc = (byte)((indicator >> 5) & 0x03);
                int chunkLen = payload.Length - 2;

                if (start)
                {
                    _naluHeader = (byte)(((refIdc & 0x03) << 5) | ((byte)originalType & 0x1F));
                    _assembledLength = 0;
                    EnsureCapacity(chunkLen);
                    Buffer.BlockCopy(payload, 2, _reassemblyBuffer, 0, chunkLen);
                    _assembledLength = chunkLen;
                    _isAssembling = true;
                    _lastSequenceNumber = sequenceNumber;
                    return null;
                }
                else if (_isAssembling)
                {
                    // If sequence number checking is active, verify continuity
                    if (sequenceNumber >= 0 && _lastSequenceNumber >= 0)
                    {
                        ushort expectedSeq = (ushort)(_lastSequenceNumber + 1);
                        if ((ushort)sequenceNumber != expectedSeq)
                        {
                            // Packet loss or out-of-order packet detected! Abort incomplete fragment
                            Reset();
                            return null;
                        }
                        _lastSequenceNumber = sequenceNumber;
                    }

                    EnsureCapacity(chunkLen);
                    Buffer.BlockCopy(payload, 2, _reassemblyBuffer, _assembledLength, chunkLen);
                    _assembledLength += chunkLen;

                    if (end)
                    {
                        byte[] reassembledPayload = new byte[_assembledLength];
                        Buffer.BlockCopy(_reassemblyBuffer, 0, reassembledPayload, 0, _assembledLength);

                        _assembledLength = 0;
                        _isAssembling = false;
                        _lastSequenceNumber = -1;

                        return new H264Nalu(_naluHeader, reassembledPayload);
                    }
                }

                return null;
            }
            else
            {
                // Single NAL unit packet (RFC 6184 Section 5.6)
                byte header = payload[0];
                byte[] naluPayload = new byte[payload.Length - 1];
                Buffer.BlockCopy(payload, 1, naluPayload, 0, naluPayload.Length);
                return new H264Nalu(header, naluPayload);
            }
        }
    }
}
