using System;

namespace ZeroAudio.Equalization
{
    /// <summary>
    /// 10-band ISO standard graphic equalizer cascading peaking EQ biquad filters per audio channel.
    /// Standard center frequencies: 31Hz, 63Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz.
    /// </summary>
    public sealed class GraphicEqualizer
    {
        public static readonly float[] StandardFrequencies = new float[]
        {
            31.25f, 62.5f, 125.0f, 250.0f, 500.0f, 1000.0f, 2000.0f, 4000.0f, 8000.0f, 16000.0f
        };

        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly BiquadFilter[][] _filters; // [channel][band]
        private readonly float[] _bandGains;

        public int BandCount => StandardFrequencies.Length;
        public int Channels => _channels;

        public GraphicEqualizer(int sampleRate, int channels)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

            _sampleRate = sampleRate;
            _channels = channels;
            _bandGains = new float[StandardFrequencies.Length];
            _filters = new BiquadFilter[channels][];

            for (int ch = 0; ch < channels; ch++)
            {
                _filters[ch] = new BiquadFilter[StandardFrequencies.Length];
                for (int b = 0; b < StandardFrequencies.Length; b++)
                {
                    _filters[ch][b] = BiquadFilter.CreatePeakingEq(sampleRate, StandardFrequencies[b], 1.4142f, 0.0f);
                }
            }
        }

        /// <summary>
        /// Sets the gain adjustment for a specific frequency band in decibels (typically -12dB to +12dB).
        /// </summary>
        public void SetBandGain(int bandIndex, float gainDb)
        {
            if (bandIndex < 0 || bandIndex >= StandardFrequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(bandIndex));

            _bandGains[bandIndex] = gainDb;
            float freq = StandardFrequencies[bandIndex];

            for (int ch = 0; ch < _channels; ch++)
            {
                _filters[ch][bandIndex] = BiquadFilter.CreatePeakingEq(_sampleRate, freq, 1.4142f, gainDb);
            }
        }

        public float GetBandGain(int bandIndex)
        {
            if (bandIndex < 0 || bandIndex >= StandardFrequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(bandIndex));
            return _bandGains[bandIndex];
        }

        /// <summary>
        /// Processes interleaved multi-channel audio samples in-place through the 10-band equalizer cascade.
        /// </summary>
        public void Process(Span<float> samples)
        {
            int frames = samples.Length / _channels;
            int channels = _channels;
            int numBands = StandardFrequencies.Length;

            for (int f = 0; f < frames; f++)
            {
                int frameOffset = f * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = samples[frameOffset + ch];
                    var channelFilters = _filters[ch];

                    for (int b = 0; b < numBands; b++)
                    {
                        sample = channelFilters[b].ProcessSample(sample);
                    }

                    samples[frameOffset + ch] = sample;
                }
            }
        }

        /// <summary>
        /// Resets all filter delay states across all bands and channels.
        /// </summary>
        public void Reset()
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                for (int b = 0; b < _filters[ch].Length; b++)
                {
                    _filters[ch][b].Reset();
                }
            }
        }
    }
}
