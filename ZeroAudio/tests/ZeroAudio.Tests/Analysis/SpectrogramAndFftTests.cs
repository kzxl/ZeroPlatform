using System;
using Xunit;
using ZeroAudio.Analysis;
using ZeroAudio.Buffers;
using ZeroAudio.Formats;

namespace ZeroAudio.Tests.Analysis
{
    public class SpectrogramAndFftTests
    {
        [Fact]
        public void FastFourierTransform_ForwardAndInverseReconstructSignal()
        {
            int n = 64;
            float[] real = new float[n];
            float[] imag = new float[n];

            for (int i = 0; i < n; i++)
            {
                real[i] = (float)Math.Sin(2 * Math.PI * 4 * i / n);
                imag[i] = 0.0f;
            }

            float[] original = (float[])real.Clone();

            FastFourierTransform.Forward(real, imag);
            FastFourierTransform.Inverse(real, imag);

            for (int i = 0; i < n; i++)
            {
                Assert.InRange(real[i], original[i] - 0.001f, original[i] + 0.001f);
                Assert.InRange(imag[i], -0.001f, 0.001f);
            }
        }

        [Fact]
        public void SpectrogramEngine_DetectsTargetFrequency()
        {
            int sampleRate = 8000;
            int nSamples = 4096;
            float targetFreq = 1000f; // 1 kHz pure tone

            float[] signal = new float[nSamples];
            for (int i = 0; i < nSamples; i++)
            {
                signal[i] = (float)Math.Sin(2 * Math.PI * targetFreq * i / sampleRate);
            }

            var spec = SpectrogramEngine.ComputeStft(signal, sampleRate, windowSize: 512, hopSize: 256, WindowType.Hann);
            Assert.True(spec.FrameCount > 10);
            Assert.Equal(257, spec.BinCount); // 512 / 2 + 1

            float[] avgPsd = spec.ComputeAverageSpectrum();

            // Find peak frequency
            int maxBin = 0;
            float maxDb = float.MinValue;
            for (int k = 0; k < avgPsd.Length; k++)
            {
                if (avgPsd[k] > maxDb)
                {
                    maxDb = avgPsd[k];
                    maxBin = k;
                }
            }

            float peakFreq = spec.Frequencies[maxBin];
            // Delta frequency per bin is 8000 / 512 = 15.625 Hz
            Assert.InRange(peakFreq, 980f, 1020f);
        }

        [Fact]
        public void AcousticWaveBuffer_WritesAndComputesMetrics()
        {
            var buffer = new AcousticWaveBuffer(1024, sampleRate: 48000);
            float[] sine = new float[512];
            for (int i = 0; i < sine.Length; i++)
            {
                sine[i] = (float)Math.Sin(2 * Math.PI * i / 64.0); // Sine wave with peak 1.0
            }

            buffer.Write(sine);
            Assert.Equal(512, buffer.Count);

            var metrics = buffer.ComputeMetrics();
            Assert.InRange(metrics.Peak, 0.99f, 1.01f);
            Assert.InRange(metrics.Rms, 0.69f, 0.72f); // RMS of sine is 1/sqrt(2) ~ 0.707
            Assert.InRange(metrics.CrestFactor, 1.39f, 1.45f); // Crest factor ~ 1.414

            // Test ReadLatest
            float[] readBack = new float[100];
            int readCount = buffer.ReadLatest(readBack);
            Assert.Equal(100, readCount);
            Assert.Equal(sine[512 - 100], readBack[0], 4);
        }

        [Fact]
        public void BearingDefectDetector_IdentifiesOuterRaceFault()
        {
            // Standard 6205 deep groove ball bearing at 1800 RPM (30 Hz)
            var geom = new BearingGeometry(pitchDiameterMm: 39.0f, ballDiameterMm: 7.94f, numBalls: 9, contactAngleDeg: 0f);
            float rpm = 1800f;

            var freqs = BearingDefectDetector.CalculateFaultFrequencies(geom, rpm);
            Assert.InRange(freqs.RunningSpeedHz, 29.9f, 30.1f);
            Assert.True(freqs.Bpfo > 100f && freqs.Bpfo < 115f, $"Calculated BPFO: {freqs.Bpfo}");

            // Synthesize spectrum with elevated noise at BPFO
            float[] freqsAxis = new float[512];
            float[] spectrumDb = new float[512];
            float deltaF = 1.0f; // 1 Hz per bin

            for (int i = 0; i < 512; i++)
            {
                freqsAxis[i] = i * deltaF;
                spectrumDb[i] = -80f; // Noise floor
            }

            // Inject defect spike at calculated BPFO
            int bpfoBin = (int)Math.Round(freqs.Bpfo);
            spectrumDb[bpfoBin] = -22.0f; // Strong fault spike!

            var diagnoses = BearingDefectDetector.Diagnose(spectrumDb, freqsAxis, geom, rpm, faultThresholdDb: -40.0f);

            Assert.NotEmpty(diagnoses);
            var defect = diagnoses.Find(d => d.FaultType == BearingFaultType.OuterRaceDefect);
            Assert.Equal(BearingFaultType.OuterRaceDefect, defect.FaultType);
            Assert.InRange(defect.DetectedFrequencyHz, freqs.Bpfo - 1.5f, freqs.Bpfo + 1.5f);
            Assert.True(defect.Confidence > 0.8f);
        }

        [Fact]
        public void AudioBuffer_ComputesCrestFactorAndKurtosis()
        {
            using var buffer = AudioBuffer.Create(AudioFormat.Pcm16(48000, 1), 1024);
            var span = buffer.Samples;
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = (float)Math.Sin(2 * Math.PI * i / 64.0);
            }

            float crest = buffer.CalculateCrestFactor();
            Assert.InRange(crest, 1.39f, 1.45f);

            float kurtosis = buffer.CalculateKurtosis();
            // Kurtosis of a pure sine wave is 1.5
            Assert.InRange(kurtosis, 1.45f, 1.55f);
        }

        [Fact]
        public void WavCodec_EncodesAndDecodesLosslessPcm()
        {
            float[] original = new float[1000];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = 0.5f * (float)Math.Cos(2 * Math.PI * 440.0 * i / 44100.0);
            }

            byte[] wavBytes = WavCodec.EncodePcm16(original, 44100, channels: 1);
            Assert.NotNull(wavBytes);
            Assert.True(wavBytes.Length > 2000);

            float[] decoded = WavCodec.DecodePcm16(wavBytes, out int rate, out short channels);
            Assert.Equal(44100, rate);
            Assert.Equal(1, channels);
            Assert.Equal(original.Length, decoded.Length);

            // Verify sample values match within 16-bit quantization threshold (1 / 32767 ~ 0.0001)
            for (int i = 0; i < 100; i++)
            {
                Assert.InRange(decoded[i], original[i] - 0.001f, original[i] + 0.001f);
            }
        }
    }
}
