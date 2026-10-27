using System;
using System.Collections.Generic;

namespace ZeroAudioVisual.Analysis
{
    /// <summary>
    /// Geometric specifications of a ball/roller bearing for fault frequency determination.
    /// </summary>
    [Obsolete("BearingGeometry has been moved to ZeroAudio.Analysis.BearingGeometry in ZeroAudio.Core.")]
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

        public static implicit operator ZeroAudio.Analysis.BearingGeometry(BearingGeometry g) =>
            new ZeroAudio.Analysis.BearingGeometry(g.PitchDiameterMm, g.BallDiameterMm, g.NumBalls, g.ContactAngleDeg);

        public static implicit operator BearingGeometry(ZeroAudio.Analysis.BearingGeometry g) =>
            new BearingGeometry(g.PitchDiameterMm, g.BallDiameterMm, g.NumBalls, g.ContactAngleDeg);
    }

    /// <summary>
    /// Calculated fault characteristic frequencies for a specific machine RPM.
    /// </summary>
    [Obsolete("BearingFrequencies has been moved to ZeroAudio.Analysis.BearingFrequencies in ZeroAudio.Core.")]
    public struct BearingFrequencies
    {
        public float RunningSpeedHz;
        public float Bpfo; // Ball Pass Frequency Outer Race
        public float Bpfi; // Ball Pass Frequency Inner Race
        public float Bsf;  // Ball Spin Frequency
        public float Ftf;  // Fundamental Train Frequency (Cage)
    }

    [Obsolete("BearingFaultType has been moved to ZeroAudio.Analysis.BearingFaultType in ZeroAudio.Core.")]
    public enum BearingFaultType
    {
        Normal,
        OuterRaceDefect,
        InnerRaceDefect,
        BallDefect,
        CageDefect
    }

    [Obsolete("FaultDiagnosis has been moved to ZeroAudio.Analysis.FaultDiagnosis in ZeroAudio.Core.")]
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
    [Obsolete("BearingDefectDetector has been moved to ZeroAudio.Analysis.BearingDefectDetector in ZeroAudio.Core.")]
    public static class BearingDefectDetector
    {
        public static BearingFrequencies CalculateFaultFrequencies(BearingGeometry geom, float rpm)
        {
            var res = ZeroAudio.Analysis.BearingDefectDetector.CalculateFaultFrequencies(geom, rpm);
            return new BearingFrequencies
            {
                RunningSpeedHz = res.RunningSpeedHz,
                Bpfo = res.Bpfo,
                Bpfi = res.Bpfi,
                Bsf = res.Bsf,
                Ftf = res.Ftf
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
            var innerResults = ZeroAudio.Analysis.BearingDefectDetector.Diagnose(
                spectrumDb,
                frequencies,
                geom,
                rpm,
                faultThresholdDb,
                toleranceHz);

            var list = new List<FaultDiagnosis>(innerResults.Count);
            foreach (var item in innerResults)
            {
                list.Add(new FaultDiagnosis
                {
                    FaultType = (BearingFaultType)item.FaultType,
                    DetectedFrequencyHz = item.DetectedFrequencyHz,
                    MagnitudeDb = item.MagnitudeDb,
                    Confidence = item.Confidence,
                    Description = item.Description
                });
            }
            return list;
        }
    }
}
