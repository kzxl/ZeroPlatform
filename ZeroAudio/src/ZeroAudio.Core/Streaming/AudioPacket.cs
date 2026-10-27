using System;

namespace ZeroAudio.Streaming
{
    /// <summary>
    /// Represents an audio packet with sequence number, timestamp, and payload.
    /// Used for real-time network audio streaming (VoIP, RTP, WebRTC).
    /// </summary>
    public readonly struct AudioPacket
    {
        public uint SequenceNumber { get; }
        public uint TimestampMs { get; }
        public byte[] Payload { get; }

        public AudioPacket(uint sequenceNumber, uint timestampMs, byte[] payload)
        {
            SequenceNumber = sequenceNumber;
            TimestampMs = timestampMs;
            Payload = payload ?? Array.Empty<byte>();
        }

        public override string ToString() =>
            $"AudioPacket(Seq={SequenceNumber}, Time={TimestampMs}ms, Size={Payload.Length}B)";
    }
}
