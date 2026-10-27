using System;

namespace ZeroSignal.Core.Audio
{
    /// <summary>
    /// Result of Voice Activity Detection (VAD) analysis on a single audio frame.
    /// </summary>
    [Obsolete("VadDecision has been moved to ZeroAudio.Analysis.VadDecision in ZeroAudio.Core.")]
    public readonly struct VadDecision
    {
        private readonly ZeroAudio.Analysis.VadDecision _inner;

        public bool IsVoice => _inner.IsVoice;
        public float RmsEnergy => _inner.RmsEnergy;
        public float ZeroCrossingRate => _inner.ZeroCrossingRate;
        public float NoiseFloor => _inner.NoiseFloor;

        public VadDecision(bool isVoice, float rmsEnergy, float zeroCrossingRate, float noiseFloor)
        {
            _inner = new ZeroAudio.Analysis.VadDecision(isVoice, rmsEnergy, zeroCrossingRate, noiseFloor);
        }

        public VadDecision(ZeroAudio.Analysis.VadDecision inner)
        {
            _inner = inner;
        }

        public static implicit operator ZeroAudio.Analysis.VadDecision(VadDecision d) => d._inner;
        public static implicit operator VadDecision(ZeroAudio.Analysis.VadDecision d) => new VadDecision(d);

        public override string ToString() => _inner.ToString();
    }

    /// <summary>
    /// Zero-allocation time-domain Voice Activity Detector (VAD).
    /// </summary>
    [Obsolete("VadDetector has been moved to ZeroAudio.Analysis.VadDetector in ZeroAudio.Core.")]
    public sealed class VadDetector
    {
        private readonly ZeroAudio.Analysis.VadDetector _inner;

        public float MinEnergyThreshold
        {
            get => _inner.MinEnergyThreshold;
            set => _inner.MinEnergyThreshold = value;
        }

        public float EnergyThresholdRatio
        {
            get => _inner.EnergyThresholdRatio;
            set => _inner.EnergyThresholdRatio = value;
        }

        public float MinZcr
        {
            get => _inner.MinZcr;
            set => _inner.MinZcr = value;
        }

        public float MaxZcr
        {
            get => _inner.MaxZcr;
            set => _inner.MaxZcr = value;
        }

        public int HangoverFrames
        {
            get => _inner.HangoverFrames;
            set => _inner.HangoverFrames = value;
        }

        public float NoiseAdaptationRate
        {
            get => _inner.NoiseAdaptationRate;
            set => _inner.NoiseAdaptationRate = value;
        }

        public VadDetector(float initialNoiseFloor = 0.005f)
        {
            _inner = new ZeroAudio.Analysis.VadDetector(initialNoiseFloor);
        }

        public void Reset(float initialNoiseFloor = 0.005f) => _inner.Reset(initialNoiseFloor);

        public VadDecision ProcessFrame(ReadOnlySpan<short> samples)
        {
            return new VadDecision(_inner.ProcessFrame(samples));
        }

        public VadDecision ProcessFrame(ReadOnlySpan<byte> pcm16Bytes)
        {
            return new VadDecision(_inner.ProcessFrame(pcm16Bytes));
        }

        public VadDecision ProcessFrame(ReadOnlySpan<float> samples)
        {
            return new VadDecision(_inner.ProcessFrame(samples));
        }
    }
}
