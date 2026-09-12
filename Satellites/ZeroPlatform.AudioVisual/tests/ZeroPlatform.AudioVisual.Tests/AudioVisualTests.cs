using System;
using System.Collections.Generic;
using Xunit;
using ZeroPlatform.AudioVisual.Acoustic;
using ZeroPlatform.AudioVisual.Analysis;
using ZeroPlatform.AudioVisual.Video;

namespace ZeroPlatform.AudioVisual.Tests
{
    public class AudioVisualTests
    {
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
        public void VideoFrameBuffer_CalculatesLuminanceCorrectly()
        {
            var frame = new VideoFrameBuffer(4, 4, VideoPixelFormat.Rgb24);

            // Fill (2, 2) with pure white (255, 255, 255)
            int offset = (2 * frame.Stride) + (2 * 3);
            frame.Data[offset] = 255;
            frame.Data[offset + 1] = 255;
            frame.Data[offset + 2] = 255;

            float lumWhite = frame.GetLuminance(2, 2);
            Assert.InRange(lumWhite, 0.99f, 1.01f);

            float lumBlack = frame.GetLuminance(0, 0);
            Assert.Equal(0f, lumBlack);

            var cloned = frame.Clone();
            Assert.Equal(frame.Width, cloned.Width);
            Assert.Equal(frame.Height, cloned.Height);
            Assert.Equal(lumWhite, cloned.GetLuminance(2, 2));
        }

        [Fact]
        public void RtspProtocol_ParsesRtpAndReassemblesH264FuA()
        {
            // 1. Test RTP Header Parse
            byte[] rawRtp = new byte[16];
            rawRtp[0] = 0x80; // V=2, P=0, X=0, CC=0
            rawRtp[1] = 0x60; // M=0, PT=96 (Dynamic payload)
            rawRtp[2] = 0x01; rawRtp[3] = 0x23; // Seq = 0x0123 (291)
            rawRtp[4] = 0x00; rawRtp[5] = 0x01; rawRtp[6] = 0x00; rawRtp[7] = 0x00; // Timestamp
            rawRtp[8] = 0x11; rawRtp[9] = 0x22; rawRtp[10] = 0x33; rawRtp[11] = 0x44; // SSRC
            rawRtp[12] = 0xAA; rawRtp[13] = 0xBB; rawRtp[14] = 0xCC; rawRtp[15] = 0xDD; // Payload

            bool parsed = RtpPacket.TryParse(rawRtp, out var rtp);
            Assert.True(parsed);
            Assert.Equal(2, rtp.Version);
            Assert.Equal(96, rtp.PayloadType);
            Assert.Equal(291, rtp.SequenceNumber);
            Assert.Equal(4, rtp.Payload.Length);
            Assert.Equal(0xAA, rtp.Payload[0]);

            // 2. Test H264 FU-A Reassembly
            var depacketizer = new H264RtpDepacketizer();
            byte[]? assembledNalu = null;
            depacketizer.OnNaluComplete += nalu => assembledNalu = nalu;

            // FU indicator: NRI=3, Type=28 (FU-A) -> 0x7C
            // Fragment 1: FU header S=1, E=0, Type=5 (IDR) -> 0x85
            byte[] frag1 = new byte[] { 0x7C, 0x85, 0x10, 0x20, 0x30 };
            depacketizer.ProcessRtpPayload(frag1, marker: false);
            Assert.Null(assembledNalu); // Not complete yet

            // Fragment 2: FU header S=0, E=1, Type=5 (IDR) -> 0x45
            byte[] frag2 = new byte[] { 0x7C, 0x45, 0x40, 0x50 };
            depacketizer.ProcessRtpPayload(frag2, marker: true);

            Assert.NotNull(assembledNalu);
            // Must have 4-byte start code {0, 0, 0, 1}
            Assert.Equal(0, assembledNalu[0]);
            Assert.Equal(0, assembledNalu[1]);
            Assert.Equal(0, assembledNalu[2]);
            Assert.Equal(1, assembledNalu[3]);
            // Reconstructed NALU header: NRI=3 (0x60), Type=5 (0x05) -> 0x65
            Assert.Equal(0x65, assembledNalu[4]);
            // Payload bytes: 10, 20, 30, 40, 50
            Assert.Equal(0x10, assembledNalu[5]);
            Assert.Equal(0x50, assembledNalu[9]);
        }
    }
}
