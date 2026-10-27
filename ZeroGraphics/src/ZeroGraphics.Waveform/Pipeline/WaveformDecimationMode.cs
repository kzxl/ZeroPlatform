namespace ZeroGraphics.Waveform.Pipeline
{
    /// <summary>
    /// Specifies the downsampling algorithm used when rendering high-frequency waveforms.
    /// </summary>
    public enum WaveformDecimationMode
    {
        /// <summary>
        /// Direct or stride-based subsampling without algorithmic peak preservation.
        /// </summary>
        None = 0,

        /// <summary>
        /// Largest Triangle Three Buckets (LTTB) decimation.
        /// Optimizes visual shape and smoothness for curves and slow trends.
        /// </summary>
        Lttb = 1,

        /// <summary>
        /// MinMax peak-preserving decimation.
        /// Retains absolute minimum and maximum values in each bucket; guarantees narrow spikes and anomalies are never lost.
        /// </summary>
        MinMax = 2
    }
}
