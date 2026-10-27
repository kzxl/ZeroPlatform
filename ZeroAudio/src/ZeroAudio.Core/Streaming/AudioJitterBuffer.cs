using System;
using System.Collections.Generic;

namespace ZeroAudio.Streaming
{
    /// <summary>
    /// Jitter buffer for real-time audio/voice streaming over unreliable transports (UDP/RTP).
    /// Reorders out-of-sequence packets, deduplicates retransmissions, absorbs arrival jitter,
    /// and ensures smooth, continuous playout.
    /// </summary>
    public sealed class AudioJitterBuffer
    {
        private readonly List<AudioPacket> _buffer;
        private readonly object _syncLock = new object();
        private uint _lastPlayedSeq;
        private bool _hasPlayedAny;
        private bool _isBuffering = true;

        /// <summary>
        /// Target playout delay in milliseconds.
        /// Default: 40 ms.
        /// </summary>
        public int TargetDelayMs { get; set; } = 40;

        /// <summary>
        /// Expected duration of each audio packet in milliseconds.
        /// Default: 20 ms.
        /// </summary>
        public int PacketDurationMs { get; set; } = 20;

        /// <summary>
        /// Maximum number of packets the buffer can hold before dropping old frames.
        /// Default: 50 frames (approx 1000 ms at 20ms/frame).
        /// </summary>
        public int MaxCapacity { get; set; } = 50;

        /// <summary>
        /// Total number of packets received and accepted.
        /// </summary>
        public long TotalPushed { get; private set; }

        /// <summary>
        /// Total number of packets successfully read for playout.
        /// </summary>
        public long TotalPopped { get; private set; }

        /// <summary>
        /// Number of packets arrived after their playout time window expired.
        /// </summary>
        public long LatePacketsDropped { get; private set; }

        /// <summary>
        /// Number of duplicate packets dropped.
        /// </summary>
        public long DuplicatePacketsDropped { get; private set; }

        /// <summary>
        /// Number of packets dropped due to buffer capacity overflow.
        /// </summary>
        public long OverflowPacketsDropped { get; private set; }

        /// <summary>
        /// Current number of packets waiting in the jitter buffer.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncLock)
                {
                    return _buffer.Count;
                }
            }
        }

        /// <summary>
        /// Gets the current estimated buffered playout delay time.
        /// </summary>
        public TimeSpan CurrentBufferedDuration
        {
            get
            {
                lock (_syncLock)
                {
                    return TimeSpan.FromMilliseconds(_buffer.Count * PacketDurationMs);
                }
            }
        }

        public AudioJitterBuffer(int targetDelayMs = 40, int packetDurationMs = 20, int maxCapacity = 50)
        {
            TargetDelayMs = targetDelayMs;
            PacketDurationMs = Math.Max(1, packetDurationMs);
            MaxCapacity = maxCapacity;
            _buffer = new List<AudioPacket>(Math.Min(maxCapacity, 32));
        }

        /// <summary>
        /// Enqueues an incoming audio packet into the jitter buffer.
        /// Reorders by sequence number and discards duplicates or expired packets.
        /// </summary>
        public bool Push(uint sequenceNumber, uint timestampMs, byte[] payload)
        {
            lock (_syncLock)
            {
                if (_hasPlayedAny)
                {
                    int seqDiff = (int)(sequenceNumber - _lastPlayedSeq);
                    if (seqDiff <= 0)
                    {
                        // Packet arrived too late; its playout slot has already passed
                        LatePacketsDropped++;
                        return false;
                    }
                }

                // Check for duplicates and find insertion point (ordered by SequenceNumber)
                int insertIndex = -1;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    int diff = (int)(sequenceNumber - _buffer[i].SequenceNumber);
                    if (diff == 0)
                    {
                        // Duplicate packet
                        DuplicatePacketsDropped++;
                        return false;
                    }
                    if (diff < 0)
                    {
                        insertIndex = i;
                        break;
                    }
                }

                var packet = new AudioPacket(sequenceNumber, timestampMs, payload);

                if (insertIndex >= 0)
                {
                    _buffer.Insert(insertIndex, packet);
                }
                else
                {
                    _buffer.Add(packet);
                }

                // Handle capacity overflow by dropping the oldest unplayed packet
                if (_buffer.Count > MaxCapacity)
                {
                    _buffer.RemoveAt(0);
                    OverflowPacketsDropped++;
                }

                TotalPushed++;
                return true;
            }
        }

        /// <summary>
        /// Attempts to retrieve the next in-order audio packet for playout.
        /// Returns false if the buffer is currently buffering to absorb jitter, or if it is empty (underrun).
        /// </summary>
        public bool TryPop(out AudioPacket packet)
        {
            lock (_syncLock)
            {
                int minPacketsToStart = Math.Max(1, TargetDelayMs / PacketDurationMs);

                if (_isBuffering)
                {
                    if (_buffer.Count >= minPacketsToStart)
                    {
                        _isBuffering = false;
                    }
                    else
                    {
                        packet = default;
                        return false;
                    }
                }

                if (_buffer.Count == 0)
                {
                    _isBuffering = true; // Underrun occurred, re-buffer
                    packet = default;
                    return false;
                }

                packet = _buffer[0];
                _buffer.RemoveAt(0);

                _lastPlayedSeq = packet.SequenceNumber;
                _hasPlayedAny = true;
                TotalPopped++;
                return true;
            }
        }

        /// <summary>
        /// Clears all buffered packets and resets jitter buffer state.
        /// </summary>
        public void Flush()
        {
            lock (_syncLock)
            {
                _buffer.Clear();
                _isBuffering = true;
                _hasPlayedAny = false;
            }
        }
    }
}
