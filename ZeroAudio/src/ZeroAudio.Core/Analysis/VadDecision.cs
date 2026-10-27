namespace ZeroAudio.Analysis
{
    /// <summary>
    /// Result of Voice Activity Detection (VAD) analysis on a single audio frame.
    /// </summary>
    public readonly struct VadDecision
    {
        /// <summary>
        /// True if voice/speech is detected in the frame; false if silence or ambient noise.
        /// </summary>
        public bool IsVoice { get; }

        /// <summary>
        /// Root Mean Square (RMS) energy normalized between 0.0 and 1.0.
        /// </summary>
        public float RmsEnergy { get; }

        /// <summary>
        /// Zero-Crossing Rate normalized between 0.0 and 1.0 (number of sign changes per sample).
        /// </summary>
        public float ZeroCrossingRate { get; }

        /// <summary>
        /// Estimated background noise floor RMS level.
        /// </summary>
        public float NoiseFloor { get; }

        public VadDecision(bool isVoice, float rmsEnergy, float zeroCrossingRate, float noiseFloor)
        {
            IsVoice = isVoice;
            RmsEnergy = rmsEnergy;
            ZeroCrossingRate = zeroCrossingRate;
            NoiseFloor = noiseFloor;
        }

        public override string ToString() =>
            $"IsVoice={IsVoice}, RMS={RmsEnergy:F4}, ZCR={ZeroCrossingRate:F3}, Floor={NoiseFloor:F4}";
    }
}
