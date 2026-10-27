using System;
using ZeroAudio.Processing;

namespace ZeroAudio.Dynamics
{
    /// <summary>
    /// Noise gate processor that attenuates or mutes signals below an audible threshold to eliminate background noise.
    /// </summary>
    public sealed class NoiseGate
    {
        private readonly float _attackCoeff;
        private readonly float _releaseCoeff;
        private readonly int _holdSamples;
        private int _holdCounter;
        private float _currentGain;

        public float ThresholdDb { get; set; }
        public float AttackMs { get; }
        public float HoldMs { get; }
        public float ReleaseMs { get; }

        public NoiseGate(int sampleRate, float thresholdDb = -45.0f, float attackMs = 2.0f, float holdMs = 50.0f, float releaseMs = 100.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

            ThresholdDb = thresholdDb;
            AttackMs = attackMs;
            HoldMs = holdMs;
            ReleaseMs = releaseMs;

            _attackCoeff = (float)Math.Exp(-1.0 / (sampleRate * (attackMs / 1000.0)));
            _releaseCoeff = (float)Math.Exp(-1.0 / (sampleRate * (releaseMs / 1000.0)));
            _holdSamples = (int)(sampleRate * (holdMs / 1000.0));
            _holdCounter = 0;
            _currentGain = 0.0f; // Start closed (muted)
        }

        /// <summary>
        /// Processes audio samples in-place, gating noise below the threshold.
        /// </summary>
        public void Process(Span<float> samples)
        {
            float thresholdLinear = GainController.DbToLinear(ThresholdDb);

            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i];
                float absX = Math.Abs(x);

                float targetGain;
                if (absX >= thresholdLinear)
                {
                    targetGain = 1.0f;
                    _holdCounter = _holdSamples;
                }
                else if (_holdCounter > 0)
                {
                    targetGain = 1.0f;
                    _holdCounter--;
                }
                else
                {
                    targetGain = 0.0f;
                }

                // Smooth gain transition
                if (targetGain > _currentGain)
                {
                    _currentGain = _attackCoeff * _currentGain + (1.0f - _attackCoeff) * targetGain;
                }
                else
                {
                    _currentGain = _releaseCoeff * _currentGain + (1.0f - _releaseCoeff) * targetGain;
                }

                samples[i] = x * _currentGain;
            }
        }

        public void Reset()
        {
            _holdCounter = 0;
            _currentGain = 0.0f;
        }
    }
}
