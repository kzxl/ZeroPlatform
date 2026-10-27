using System;
using System.Collections.Generic;

namespace ZeroAudio.Analysis
{
    /// <summary>
    /// Statistical condition metrics for acoustic telemetry and vibration sensor evaluation.
    /// </summary>
    public struct VibrationMetrics
    {
        public float Rms;
        public float Peak;
        public float CrestFactor;
        public float Kurtosis;
        public int SampleCount;

        public override string ToString() =>
            $"RMS={Rms:F3}, Peak={Peak:F3}, Crest={CrestFactor:F2}, Kurtosis={Kurtosis:F2} (N={SampleCount})";
    }

    /// <summary>
    /// Circular ring buffer optimized for real-time acoustic telemetry and vibration sensor acquisition.
    /// Provides statistical metrics calculation (RMS, Peak, Crest Factor, Kurtosis) for condition monitoring.
    /// </summary>
    public class AcousticWaveBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private int _writeIndex = 0;
        private int _count = 0;
        private readonly object _syncLock = new object();

        public int SampleRate { get; }
        public int Capacity => _capacity;
        public int Count => _count;

        public AcousticWaveBuffer(int capacity, int sampleRate = 44100)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _buffer = new float[capacity];
            SampleRate = sampleRate;
        }

        public void Write(ReadOnlySpan<float> samples)
        {
            lock (_syncLock)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _buffer[_writeIndex] = samples[i];
                    _writeIndex = (_writeIndex + 1) % _capacity;
                    if (_count < _capacity)
                        _count++;
                }
            }
        }

        public void Write(float[] samples, int offset, int count)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            Write(new ReadOnlySpan<float>(samples, offset, count));
        }

        /// <summary>
        /// Reads the most recent N samples into the destination span.
        /// </summary>
        public int ReadLatest(Span<float> destination)
        {
            lock (_syncLock)
            {
                int toRead = Math.Min(destination.Length, _count);
                int start = (_writeIndex - toRead + _capacity) % _capacity;

                for (int i = 0; i < toRead; i++)
                {
                    destination[i] = _buffer[(start + i) % _capacity];
                }
                return toRead;
            }
        }

        /// <summary>
        /// Calculates statistical metrics for rotating machinery condition evaluation.
        /// </summary>
        public VibrationMetrics ComputeMetrics(int windowSamples = 0)
        {
            lock (_syncLock)
            {
                int samplesToEval = (windowSamples > 0 && windowSamples <= _count) ? windowSamples : _count;
                if (samplesToEval == 0) return default;

                int start = (_writeIndex - samplesToEval + _capacity) % _capacity;

                float sum = 0f;
                float sumSq = 0f;
                float peak = 0f;

                for (int i = 0; i < samplesToEval; i++)
                {
                    float val = _buffer[(start + i) % _capacity];
                    float absVal = Math.Abs(val);
                    if (absVal > peak) peak = absVal;

                    sum += val;
                    sumSq += val * val;
                }

                float mean = sum / samplesToEval;
                float rms = (float)Math.Sqrt(sumSq / samplesToEval);
                float crestFactor = (rms > 1e-6f) ? (peak / rms) : 0f;

                // Fourth standardized moment (Kurtosis)
                float sumKurt = 0f;
                for (int i = 0; i < samplesToEval; i++)
                {
                    float diff = _buffer[(start + i) % _capacity] - mean;
                    sumKurt += diff * diff * diff * diff;
                }
                float variance = (sumSq / samplesToEval) - (mean * mean);
                float kurtosis = (variance > 1e-6f) ? (sumKurt / (samplesToEval * variance * variance)) : 3.0f;

                return new VibrationMetrics
                {
                    Rms = rms,
                    Peak = peak,
                    CrestFactor = crestFactor,
                    Kurtosis = kurtosis,
                    SampleCount = samplesToEval
                };
            }
        }
    }

    /// <summary>
    /// Geometric specifications of a ball/roller bearing for fault frequency determination.
    /// </summary>
    public struct BearingGeometry
    {
        public float PitchDiameterMm;
        public float BallDiameterMm;
        public int NumBalls;
        public float ContactAngleDeg;

        public BearingGeometry(float pitchDiameterMm, float ballDiameterMm, int numBalls, float contactAngleDeg = 0f)
        {
            PitchDiameterMm = pitchDiameterMm;
            BallDiameterMm = ballDiameterMm;
            NumBalls = numBalls;
            ContactAngleDeg = contactAngleDeg;
        }
    }

    /// <summary>
    /// Calculated fault characteristic frequencies for a specific machine RPM.
    /// </summary>
    public struct BearingFrequencies
    {
        public float RunningSpeedHz;
        public float Bpfo; // Ball Pass Frequency Outer Race
        public float Bpfi; // Ball Pass Frequency Inner Race
        public float Bsf;  // Ball Spin Frequency
        public float Ftf;  // Fundamental Train Frequency (Cage)
    }

    public enum BearingFaultType
    {
        Normal,
        OuterRaceDefect,
        InnerRaceDefect,
        BallDefect,
        CageDefect
    }

    public struct FaultDiagnosis
    {
        public BearingFaultType FaultType;
        public float DetectedFrequencyHz;
        public float MagnitudeDb;
        public float Confidence;
        public string Description;
    }

    /// <summary>
    /// Industrial Predictive Maintenance (PdM) vibration and acoustic bearing fault detector.
    /// Analyzes frequency spectra to isolate mechanical degradation in rotary equipment.
    /// </summary>
    public static class BearingDefectDetector
    {
        public static BearingFrequencies CalculateFaultFrequencies(BearingGeometry geom, float rpm)
        {
            float fr = rpm / 60.0f; // Shaft rotational frequency in Hz
            double rad = geom.ContactAngleDeg * Math.PI / 180.0;
            double cosAngle = Math.Cos(rad);
            double ratio = (geom.PitchDiameterMm > 0) ? (geom.BallDiameterMm / geom.PitchDiameterMm) : 0.0;

            float bpfo = (float)(0.5 * geom.NumBalls * fr * (1.0 - ratio * cosAngle));
            float bpfi = (float)(0.5 * geom.NumBalls * fr * (1.0 + ratio * cosAngle));
            float bsf = (float)((1.0 / (2.0 * ratio)) * fr * (1.0 - Math.Pow(ratio * cosAngle, 2)));
            float ftf = (float)(0.5 * fr * (1.0 - ratio * cosAngle));

            return new BearingFrequencies
            {
                RunningSpeedHz = fr,
                Bpfo = bpfo,
                Bpfi = bpfi,
                Bsf = bsf,
                Ftf = ftf
            };
        }

        /// <summary>
        /// Scans an averaged spectrum or single FFT frame to diagnose bearing defect anomalies.
        /// </summary>
        public static List<FaultDiagnosis> Diagnose(
            float[] spectrumDb,
            float[] frequencies,
            BearingGeometry geom,
            float rpm,
            float faultThresholdDb = -45.0f,
            float toleranceHz = 2.5f)
        {
            var results = new List<FaultDiagnosis>();
            var freqs = CalculateFaultFrequencies(geom, rpm);

            CheckFault(spectrumDb, frequencies, freqs.Bpfo, BearingFaultType.OuterRaceDefect, "Outer race flaw (BPFO)", faultThresholdDb, toleranceHz, results);
            CheckFault(spectrumDb, frequencies, freqs.Bpfi, BearingFaultType.InnerRaceDefect, "Inner race flaw (BPFI)", faultThresholdDb, toleranceHz, results);
            CheckFault(spectrumDb, frequencies, freqs.Bsf, BearingFaultType.BallDefect, "Rolling element flaw (BSF)", faultThresholdDb, toleranceHz, results);
            CheckFault(spectrumDb, frequencies, freqs.Ftf, BearingFaultType.CageDefect, "Cage structural flaw (FTF)", faultThresholdDb, toleranceHz, results);

            if (results.Count == 0)
            {
                results.Add(new FaultDiagnosis
                {
                    FaultType = BearingFaultType.Normal,
                    DetectedFrequencyHz = freqs.RunningSpeedHz,
                    MagnitudeDb = -999f,
                    Confidence = 0.95f,
                    Description = "No critical bearing fault patterns detected within operating thresholds."
                });
            }

            return results;
        }

        private static void CheckFault(
            float[] spectrumDb,
            float[] frequencies,
            float targetFreqHz,
            BearingFaultType faultType,
            string desc,
            float thresholdDb,
            float toleranceHz,
            List<FaultDiagnosis> targetList)
        {
            if (targetFreqHz <= 0) return;

            float maxDb = float.MinValue;
            float bestFreq = 0f;

            for (int i = 0; i < frequencies.Length; i++)
            {
                if (Math.Abs(frequencies[i] - targetFreqHz) <= toleranceHz)
                {
                    if (spectrumDb[i] > maxDb)
                    {
                        maxDb = spectrumDb[i];
                        bestFreq = frequencies[i];
                    }
                }
            }

            if (maxDb >= thresholdDb)
            {
                float confidence = Math.Min(1.0f, Math.Max(0.2f, (maxDb - thresholdDb) / 20.0f + 0.5f));
                targetList.Add(new FaultDiagnosis
                {
                    FaultType = faultType,
                    DetectedFrequencyHz = bestFreq,
                    MagnitudeDb = maxDb,
                    Confidence = confidence,
                    Description = $"{desc}: High vibration amplitude ({maxDb:F1} dB) detected near {targetFreqHz:F1} Hz."
                });
            }
        }
    }
}
