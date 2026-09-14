using System;
using System.Collections.Generic;

namespace ZeroAudioVisual.Analysis
{
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
    public class BearingDefectDetector
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
