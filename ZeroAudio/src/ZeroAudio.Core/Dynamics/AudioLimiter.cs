using System;
using ZeroAudio.Processing;

namespace ZeroAudio.Dynamics
{
    /// <summary>
    /// Fast, transparent brickwall peak limiter to guard against clipping distortion in real-time streams.
    /// </summary>
    public sealed class AudioLimiter
    {
        private float _envelope;
        private readonly float _attackCoeff;
        private readonly float _releaseCoeff;
        private readonly float _ceiling;

        public float CeilingDb { get; }
        public float AttackMs { get; }
        public float ReleaseMs { get; }

        public AudioLimiter(int sampleRate, float ceilingDb = -0.2f, float attackMs = 1.0f, float releaseMs = 50.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            CeilingDb = ceilingDb;
            AttackMs = attackMs;
            ReleaseMs = releaseMs;

            _ceiling = GainController.DbToLinear(ceilingDb);
            _attackCoeff = (float)Math.Exp(-1.0 / (sampleRate * (attackMs / 1000.0)));
            _releaseCoeff = (float)Math.Exp(-1.0 / (sampleRate * (releaseMs / 1000.0)));
            _envelope = 0f;
        }

        /// <summary>
        /// Processes audio samples in-place, clamping peaks exceeding the ceiling.
        /// </summary>
        public void Process(Span<float> samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i];
                float absX = Math.Abs(x);

                // Peak detector with instantaneous attack and smooth exponential release
                if (absX >= _envelope)
                {
                    _envelope = absX;
                }
                else
                {
                    _envelope = _releaseCoeff * _envelope + (1.0f - _releaseCoeff) * absX;
                }

                // Compute limiting attenuation factor
                float gain = 1.0f;
                if (_envelope > _ceiling)
                {
                    gain = _ceiling / _envelope;
                }

                float limited = x * gain;
                if (limited > _ceiling) limited = _ceiling;
                else if (limited < -_ceiling) limited = -_ceiling;

                samples[i] = limited;
            }
        }

        /// <summary>
        /// Resets the internal envelope follower state.
        /// </summary>
        public void Reset() => _envelope = 0f;
    }
}
