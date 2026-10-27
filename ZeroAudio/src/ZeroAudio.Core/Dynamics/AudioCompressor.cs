using System;
using ZeroAudio.Processing;

namespace ZeroAudio.Dynamics
{
    /// <summary>
    /// Dynamic range compressor with configurable threshold, ratio, attack, release, and makeup gain.
    /// Reduces the dynamic range of audio to maintain consistent volume levels.
    /// </summary>
    public sealed class AudioCompressor
    {
        private readonly float _attackCoeff;
        private readonly float _releaseCoeff;
        private readonly float _makeupGainLinear;
        private float _envelopeDb;

        public float ThresholdDb { get; set; }
        public float Ratio { get; set; }
        public float AttackMs { get; }
        public float ReleaseMs { get; }
        public float MakeupGainDb { get; }

        public AudioCompressor(
            int sampleRate,
            float thresholdDb = -16.0f,
            float ratio = 4.0f,
            float attackMs = 10.0f,
            float releaseMs = 100.0f,
            float makeupGainDb = 0.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (ratio < 1.0f) throw new ArgumentOutOfRangeException(nameof(ratio), "Ratio must be >= 1.0.");

            ThresholdDb = thresholdDb;
            Ratio = ratio;
            AttackMs = attackMs;
            ReleaseMs = releaseMs;
            MakeupGainDb = makeupGainDb;

            _makeupGainLinear = GainController.DbToLinear(makeupGainDb);
            _attackCoeff = (float)Math.Exp(-1.0 / (sampleRate * (attackMs / 1000.0)));
            _releaseCoeff = (float)Math.Exp(-1.0 / (sampleRate * (releaseMs / 1000.0)));
            _envelopeDb = -120.0f;
        }

        /// <summary>
        /// Compresses audio samples in-place.
        /// </summary>
        public void Process(Span<float> samples)
        {
            float threshold = ThresholdDb;
            float ratio = Ratio;
            float makeup = _makeupGainLinear;
            float slope = 1.0f - (1.0f / ratio);

            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i];
                float absX = Math.Abs(x);
                float inputDb = GainController.LinearToDb(absX);

                // Attack/Release envelope smoothing in dB domain
                if (inputDb > _envelopeDb)
                {
                    _envelopeDb = _attackCoeff * _envelopeDb + (1.0f - _attackCoeff) * inputDb;
                }
                else
                {
                    _envelopeDb = _releaseCoeff * _envelopeDb + (1.0f - _releaseCoeff) * inputDb;
                }

                // Compute gain reduction in dB
                float gainReductionDb = 0.0f;
                if (_envelopeDb > threshold)
                {
                    gainReductionDb = slope * (threshold - _envelopeDb);
                }

                float linearGain = GainController.DbToLinear(gainReductionDb) * makeup;
                samples[i] = x * linearGain;
            }
        }

        /// <summary>
        /// Resets the internal envelope follower state.
        /// </summary>
        public void Reset() => _envelopeDb = -120.0f;
    }
}
