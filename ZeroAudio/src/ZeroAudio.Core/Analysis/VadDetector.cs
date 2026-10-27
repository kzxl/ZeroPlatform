using System;
using ZeroAudio.Buffers;

namespace ZeroAudio.Analysis
{
    /// <summary>
    /// Pragmatic minimal-allocation time-domain Voice Activity Detector (VAD).
    /// Uses dual-metric RMS energy and Zero-Crossing Rate (ZCR) with adaptive noise-floor tracking
    /// and hangover frame smoothing to prevent premature speech clipping.
    /// </summary>
    public sealed class VadDetector
    {
        private float _noiseFloor;
        private int _hangoverCount;

        /// <summary>
        /// Minimum absolute RMS energy threshold below which frames are always classified as silence.
        /// Default: 0.012f (-38.4 dBFS).
        /// </summary>
        public float MinEnergyThreshold { get; set; } = 0.012f;

        /// <summary>
        /// Ratio of current RMS energy to adaptive noise floor required to trigger speech.
        /// Default: 2.2x.
        /// </summary>
        public float EnergyThresholdRatio { get; set; } = 2.2f;

        /// <summary>
        /// Minimum Zero-Crossing Rate to consider the signal as human speech (filters out 50/60Hz hum).
        /// Default: 0.015f.
        /// </summary>
        public float MinZcr { get; set; } = 0.015f;

        /// <summary>
        /// Maximum Zero-Crossing Rate (filters out high-frequency electrical static or hiss).
        /// Default: 0.65f.
        /// </summary>
        public float MaxZcr { get; set; } = 0.65f;

        /// <summary>
        /// Number of consecutive silence frames to hold as "speech" before transition to silence (hangover).
        /// Prevents clipping of faint word endings and pauses between syllables.
        /// Default: 4 frames.
        /// </summary>
        public int HangoverFrames { get; set; } = 4;

        /// <summary>
        /// Learning rate for updating the adaptive background noise floor during silence (0.0 to 1.0).
        /// Default: 0.05f.
        /// </summary>
        public float NoiseAdaptationRate { get; set; } = 0.05f;

        public VadDetector(float initialNoiseFloor = 0.005f)
        {
            _noiseFloor = Math.Max(0.0001f, initialNoiseFloor);
            _hangoverCount = 0;
        }

        /// <summary>
        /// Resets the internal state and noise floor estimate.
        /// </summary>
        public void Reset(float initialNoiseFloor = 0.005f)
        {
            _noiseFloor = Math.Max(0.0001f, initialNoiseFloor);
            _hangoverCount = 0;
        }

        /// <summary>
        /// Analyzes a frame of 16-bit signed integer PCM audio samples.
        /// </summary>
        public VadDecision ProcessFrame(ReadOnlySpan<short> samples)
        {
            if (samples.IsEmpty)
            {
                return new VadDecision(false, 0f, 0f, _noiseFloor);
            }

            int n = samples.Length;
            double sumSquares = 0.0;
            int zeroCrossings = 0;
            short prev = samples[0];

            for (int i = 0; i < n; i++)
            {
                short cur = samples[i];
                float norm = cur / 32768.0f;
                sumSquares += norm * norm;

                if ((prev >= 0 && cur < 0) || (prev < 0 && cur >= 0))
                {
                    zeroCrossings++;
                }
                prev = cur;
            }

            float rms = (float)Math.Sqrt(sumSquares / n);
            float zcr = (float)zeroCrossings / n;

            return Evaluate(rms, zcr);
        }

        /// <summary>
        /// Analyzes a frame of raw 16-bit little-endian PCM byte data.
        /// </summary>
        public VadDecision ProcessFrame(ReadOnlySpan<byte> pcm16Bytes)
        {
            int sampleCount = pcm16Bytes.Length / 2;
            if (sampleCount == 0)
            {
                return new VadDecision(false, 0f, 0f, _noiseFloor);
            }

            double sumSquares = 0.0;
            int zeroCrossings = 0;
            short prev = (short)(pcm16Bytes[0] | (pcm16Bytes[1] << 8));

            for (int i = 0; i < sampleCount; i++)
            {
                int offset = i * 2;
                short cur = (short)(pcm16Bytes[offset] | (pcm16Bytes[offset + 1] << 8));
                float norm = cur / 32768.0f;
                sumSquares += norm * norm;

                if ((prev >= 0 && cur < 0) || (prev < 0 && cur >= 0))
                {
                    zeroCrossings++;
                }
                prev = cur;
            }

            float rms = (float)Math.Sqrt(sumSquares / sampleCount);
            float zcr = (float)zeroCrossings / sampleCount;

            return Evaluate(rms, zcr);
        }

        /// <summary>
        /// Analyzes a frame of normalized 32-bit floating point audio samples [-1.0, 1.0].
        /// </summary>
        public VadDecision ProcessFrame(ReadOnlySpan<float> samples)
        {
            if (samples.IsEmpty)
            {
                return new VadDecision(false, 0f, 0f, _noiseFloor);
            }

            int n = samples.Length;
            double sumSquares = 0.0;
            int zeroCrossings = 0;
            float prev = samples[0];

            for (int i = 0; i < n; i++)
            {
                float cur = samples[i];
                sumSquares += cur * cur;

                if ((prev >= 0f && cur < 0f) || (prev < 0f && cur >= 0f))
                {
                    zeroCrossings++;
                }
                prev = cur;
            }

            float rms = (float)Math.Sqrt(sumSquares / n);
            float zcr = (float)zeroCrossings / n;

            return Evaluate(rms, zcr);
        }

        /// <summary>
        /// Analyzes a channel slice of an AudioBuffer.
        /// </summary>
        public VadDecision ProcessFrame(AudioBuffer buffer, int channel = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (channel < 0 || channel >= buffer.Channels)
                throw new ArgumentOutOfRangeException(nameof(channel));

            if (buffer.Channels == 1)
            {
                return ProcessFrame(buffer.Samples);
            }

            float[] rented = System.Buffers.ArrayPool<float>.Shared.Rent(buffer.FrameCount);
            try
            {
                var span = rented.AsSpan(0, buffer.FrameCount);
                buffer.DeinterleaveChannel(channel, span);
                return ProcessFrame(span);
            }
            finally
            {
                System.Buffers.ArrayPool<float>.Shared.Return(rented);
            }
        }

        private VadDecision Evaluate(float rms, float zcr)
        {
            // Core energy condition: must exceed both absolute minimum and ratio over noise floor
            bool energyCondition = rms >= MinEnergyThreshold && rms >= (_noiseFloor * EnergyThresholdRatio);

            // Spectral condition: zero crossing rate must fall within typical voice band
            bool zcrCondition = zcr >= MinZcr && zcr <= MaxZcr;

            bool rawVoice = energyCondition && zcrCondition;

            bool isVoice;
            if (rawVoice)
            {
                _hangoverCount = HangoverFrames;
                isVoice = true;
            }
            else
            {
                if (_hangoverCount > 0)
                {
                    _hangoverCount--;
                    isVoice = true; // Hangover hold
                }
                else
                {
                    isVoice = false;
                    // Adapt noise floor slowly during confirmed silence
                    _noiseFloor = _noiseFloor * (1.0f - NoiseAdaptationRate) + rms * NoiseAdaptationRate;
                    if (_noiseFloor < 0.0001f) _noiseFloor = 0.0001f;
                }
            }

            return new VadDecision(isVoice, rms, zcr, _noiseFloor);
        }
    }
}
