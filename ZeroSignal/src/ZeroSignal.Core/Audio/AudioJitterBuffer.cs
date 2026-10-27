using System;

namespace ZeroSignal.Core.Audio
{
    /// <summary>
    /// Represents an audio packet with sequence number, timestamp, and payload.
    /// </summary>
    [Obsolete("AudioPacket has been moved to ZeroAudio.Streaming.AudioPacket in ZeroAudio.Core.")]
    public readonly struct AudioPacket
    {
        private readonly ZeroAudio.Streaming.AudioPacket _inner;

        public uint SequenceNumber => _inner.SequenceNumber;
        public uint TimestampMs => _inner.TimestampMs;
        public byte[] Payload => _inner.Payload;

        public AudioPacket(uint sequenceNumber, uint timestampMs, byte[] payload)
        {
            _inner = new ZeroAudio.Streaming.AudioPacket(sequenceNumber, timestampMs, payload);
        }

        public AudioPacket(ZeroAudio.Streaming.AudioPacket inner)
        {
            _inner = inner;
        }

        public static implicit operator ZeroAudio.Streaming.AudioPacket(AudioPacket p) => p._inner;
        public static implicit operator AudioPacket(ZeroAudio.Streaming.AudioPacket p) => new AudioPacket(p);
    }

    /// <summary>
    /// Jitter buffer for real-time audio/voice streaming over unreliable transports (UDP/RTP).
    /// </summary>
    [Obsolete("AudioJitterBuffer has been moved to ZeroAudio.Streaming.AudioJitterBuffer in ZeroAudio.Core.")]
    public sealed class AudioJitterBuffer
    {
        private readonly ZeroAudio.Streaming.AudioJitterBuffer _inner;

        public int TargetDelayMs
        {
            get => _inner.TargetDelayMs;
            set => _inner.TargetDelayMs = value;
        }

        public int PacketDurationMs
        {
            get => _inner.PacketDurationMs;
            set => _inner.PacketDurationMs = value;
        }

        public int MaxCapacity
        {
            get => _inner.MaxCapacity;
            set => _inner.MaxCapacity = value;
        }

        public long TotalPushed => _inner.TotalPushed;
        public long TotalPopped => _inner.TotalPopped;
        public long LatePacketsDropped => _inner.LatePacketsDropped;
        public long DuplicatePacketsDropped => _inner.DuplicatePacketsDropped;
        public long OverflowPacketsDropped => _inner.OverflowPacketsDropped;
        public int Count => _inner.Count;

        public AudioJitterBuffer(int targetDelayMs = 40, int packetDurationMs = 20, int maxCapacity = 50)
        {
            _inner = new ZeroAudio.Streaming.AudioJitterBuffer(targetDelayMs, packetDurationMs, maxCapacity);
        }

        public bool Push(uint sequenceNumber, uint timestampMs, byte[] payload) =>
            _inner.Push(sequenceNumber, timestampMs, payload);

        public bool TryPop(out AudioPacket packet)
        {
            if (_inner.TryPop(out var innerPacket))
            {
                packet = new AudioPacket(innerPacket);
                return true;
            }
            packet = default;
            return false;
        }

        public void Flush() => _inner.Flush();
    }
}
