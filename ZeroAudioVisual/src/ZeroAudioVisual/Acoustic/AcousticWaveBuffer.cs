using System;

namespace ZeroAudioVisual.Acoustic
{
    /// <summary>
    /// Circular ring buffer optimized for real-time acoustic telemetry and vibration sensor acquisition.
    /// Provides statistical metrics calculation (RMS, Peak, Crest Factor, Kurtosis) for condition monitoring.
    /// </summary>
    [Obsolete("AcousticWaveBuffer has been moved to ZeroAudio.Analysis.AcousticWaveBuffer in ZeroAudio.Core.")]
    public class AcousticWaveBuffer : ZeroAudio.Analysis.AcousticWaveBuffer
    {
        public AcousticWaveBuffer(int capacity, int sampleRate = 44100)
            : base(capacity, sampleRate)
        {
        }

        public new VibrationMetrics ComputeMetrics(int windowSamples = 0)
        {
            var m = base.ComputeMetrics(windowSamples);
            return new VibrationMetrics
            {
                Rms = m.Rms,
                Peak = m.Peak,
                CrestFactor = m.CrestFactor,
                Kurtosis = m.Kurtosis,
                SampleCount = m.SampleCount
            };
        }
    }

    /// <summary>
    /// Statistical vibration and acoustic telemetry condition metrics.
    /// </summary>
    [Obsolete("VibrationMetrics has been moved to ZeroAudio.Analysis.VibrationMetrics in ZeroAudio.Core.")]
    public struct VibrationMetrics
    {
        public float Rms;
        public float Peak;
        public float CrestFactor;
        public float Kurtosis;
        public int SampleCount;

        public static implicit operator ZeroAudio.Analysis.VibrationMetrics(VibrationMetrics m) =>
            new ZeroAudio.Analysis.VibrationMetrics
            {
                Rms = m.Rms,
                Peak = m.Peak,
                CrestFactor = m.CrestFactor,
                Kurtosis = m.Kurtosis,
                SampleCount = m.SampleCount
            };

        public static implicit operator VibrationMetrics(ZeroAudio.Analysis.VibrationMetrics m) =>
            new VibrationMetrics
            {
                Rms = m.Rms,
                Peak = m.Peak,
                CrestFactor = m.CrestFactor,
                Kurtosis = m.Kurtosis,
                SampleCount = m.SampleCount
            };
    }

    /// <summary>
    /// Pure C# Standard RIFF/WAVE audio file encoder and decoder without external native DLLs.
    /// </summary>
    [Obsolete("WavCodec has been moved to ZeroAudio.Formats.WavCodec in ZeroAudio.Core.")]
    public static class WavCodec
    {
        public static byte[] EncodePcm16(float[] samples, int sampleRate = 44100, short channels = 1) =>
            ZeroAudio.Formats.WavCodec.EncodePcm16(samples, sampleRate, channels);

        public static float[] DecodePcm16(byte[] wavBytes, out int sampleRate, out short channels) =>
            ZeroAudio.Formats.WavCodec.DecodePcm16(wavBytes, out sampleRate, out channels);
    }
}
