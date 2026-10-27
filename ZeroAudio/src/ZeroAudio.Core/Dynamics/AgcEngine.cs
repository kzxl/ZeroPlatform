using System;
using ZeroAudio.Processing;

namespace ZeroAudio.Dynamics
{
    /// <summary>
    /// Automatic Gain Control (AGC) engine that continuously normalizes dynamic speech/audio volume to a target RMS level.
    /// Essential for VoIP, intercom, microphone capture, and voice recognition pipelines.
    /// </summary>
    public sealed class AgcEngine
    {
        private readonly float _smoothCoeff;
        private readonly float _targetRms;
        private readonly float _maxGainLinear;
        private readonly float _minGainLinear;
        private float _currentGain;
        private float _currentRms;

        public float TargetRmsDb { get; }
        public float MaxGainDb { get; }
        public float MinGainDb { get; }

        public AgcEngine(
            int sampleRate,
            float targetRmsDb = -18.0f,
            float maxGainDb = 24.0f,
            float minGainDb = -12.0f,
            float responseTimeMs = 150.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

            TargetRmsDb = targetRmsDb;
            MaxGainDb = maxGainDb;
            MinGainDb = minGainDb;

            _targetRms = GainController.DbToLinear(targetRmsDb);
            _maxGainLinear = GainController.DbToLinear(maxGainDb);
            _minGainLinear = GainController.DbToLinear(minGainDb);
            _smoothCoeff = (float)Math.Exp(-128.0 / (sampleRate * (responseTimeMs / 1000.0)));

            _currentGain = 1.0f;
            _currentRms = _targetRms;
        }

        /// <summary>
        /// Processes audio samples in-place, dynamically adjusting gain to reach target RMS.
        /// </summary>
        public void Process(Span<float> samples)
        {
            const int subBlockSize = 128;
            int offset = 0;
            while (offset < samples.Length)
            {
                int len = Math.Min(subBlockSize, samples.Length - offset);
                var block = samples.Slice(offset, len);

                // 1. Calculate sub-block RMS energy
                double sumSquares = 0.0;
                for (int i = 0; i < block.Length; i++)
                {
                    float s = block[i];
                    sumSquares += (double)s * s;
                }
                float blockRms = (float)Math.Sqrt(sumSquares / block.Length);

                // 2. Ignore near-silent frames to prevent ramping up noise floor
                if (blockRms > 0.001f)
                {
                    _currentRms = _smoothCoeff * _currentRms + (1.0f - _smoothCoeff) * blockRms;
                    float desiredGain = _targetRms / Math.Max(0.001f, _currentRms);
                    float clampedGain = Math.Max(_minGainLinear, Math.Min(_maxGainLinear, desiredGain));

                    // Smoothly update current gain
                    _currentGain = 0.90f * _currentGain + 0.10f * clampedGain;
                }

                // 3. Apply computed gain
                GainController.ApplyGain(block, _currentGain);
                offset += len;
            }
        }

        public void Reset()
        {
            _currentGain = 1.0f;
            _currentRms = _targetRms;
        }
    }
}
