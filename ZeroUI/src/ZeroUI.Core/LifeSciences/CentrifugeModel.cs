using System;

namespace ZeroUI.Core.LifeSciences
{
    /// <summary>
    /// Laboratory centrifuge lid safety latch interlock state.
    /// </summary>
    public enum LidLockState
    {
        Unlocked,
        LidOpen,
        Locked,
        SpeedInterlocked
    }

    /// <summary>
    /// Type of centrifuge rotor assembly installed in the chamber.
    /// </summary>
    public enum RotorType
    {
        SwingingBucket4,
        FixedAngle24,
        Microtube30
    }

    /// <summary>
    /// Telemetry for an individual centrifuge bucket or carrier position.
    /// </summary>
    public class CentrifugeBucket
    {
        public int BucketId { get; set; }
        public double ActualWeightGrams { get; set; } = 425.0;
        public bool HasTube { get; set; } = true;
    }

    /// <summary>
    /// Real-time operational telemetry for refrigerated laboratory centrifuge.
    /// </summary>
    public class CentrifugeTelemetry
    {
        public string ModelTag { get; set; } = "ULTRA-CENTRIFUGE-400R";
        public RotorType Rotor { get; set; } = RotorType.SwingingBucket4;
        public double RotorRadiusMm { get; set; } = 185.0; // mm maximum radius (r_max)
        public double TargetRpm { get; set; } = 14500.0;
        public double CurrentRpm { get; set; } = 14480.0;
        public double TargetTempC { get; set; } = 4.0;
        public double CurrentTempC { get; set; } = 4.2;
        public double VibrationMmS { get; set; } = 0.85; // mm/s RMS
        public LidLockState LidState { get; set; } = LidLockState.SpeedInterlocked;
        public bool IsRefrigerationActive { get; set; } = true;
        public bool IsRunning => CurrentRpm > 50.0;
        public TimeSpan ElapsedRunTime { get; set; } = TimeSpan.FromMinutes(18.5);
        public TimeSpan TargetRunTime { get; set; } = TimeSpan.FromMinutes(30.0);
    }

    /// <summary>
    /// Pure computation engine for centrifuge rotor physics, RCF/g-force conversion, and dynamic balance heuristics.
    /// </summary>
    public class CentrifugeEngine
    {
        public CentrifugeTelemetry Telemetry { get; } = new CentrifugeTelemetry();
        public CentrifugeBucket[] Buckets { get; }

        public CentrifugeEngine()
        {
            Buckets = new CentrifugeBucket[4];
            for (int i = 0; i < 4; i++)
            {
                Buckets[i] = new CentrifugeBucket
                {
                    BucketId = i + 1,
                    ActualWeightGrams = 425.2 + (i % 2) * 0.4
                };
            }
        }

        /// <summary>
        /// Calculates Relative Centrifugal Force (RCF / g-force) using standard rotor physics:
        /// RCF = 1.118 * 10^-6 * radius_mm * (RPM)^2  (equivalent to 1.118 * 10^-5 * radius_cm * RPM^2)
        /// </summary>
        public static double CalculateRcf(double rpm, double radiusMm)
        {
            if (rpm <= 0.0 || radiusMm <= 0.0) return 0.0;
            return 1.118e-6 * radiusMm * (rpm * rpm);
        }

        /// <summary>
        /// Calculates required rotational speed (RPM) from target RCF (g):
        /// RPM = sqrt(RCF / (1.118 * 10^-6 * radius_mm))
        /// </summary>
        public static double CalculateRpmFromRcf(double rcf, double radiusMm)
        {
            if (rcf <= 0.0 || radiusMm <= 0.0) return 0.0;
            return Math.Sqrt(rcf / (1.118e-6 * radiusMm));
        }

        /// <summary>
        /// Current Relative Centrifugal Force in units of Earth gravity (g).
        /// </summary>
        public double CurrentRcfG => CalculateRcf(Telemetry.CurrentRpm, Telemetry.RotorRadiusMm);

        /// <summary>
        /// Target Relative Centrifugal Force in units of Earth gravity (g).
        /// </summary>
        public double TargetRcfG => CalculateRcf(Telemetry.TargetRpm, Telemetry.RotorRadiusMm);

        /// <summary>
        /// Maximum weight imbalance between opposing bucket pairs (Bucket 1 vs 3, Bucket 2 vs 4).
        /// </summary>
        public double MaxBucketImbalanceGrams
        {
            get
            {
                if (Buckets == null || Buckets.Length < 4) return 0.0;
                double pair1 = Math.Abs(Buckets[0].ActualWeightGrams - Buckets[2].ActualWeightGrams);
                double pair2 = Math.Abs(Buckets[1].ActualWeightGrams - Buckets[3].ActualWeightGrams);
                return Math.Max(pair1, pair2);
            }
        }

        /// <summary>
        /// Validates dynamic rotor balance. Standard threshold: imbalance delta &lt; 1.5g.
        /// </summary>
        public bool IsRotorBalanced(double maxAllowedDeltaGrams = 1.5)
        {
            return MaxBucketImbalanceGrams <= maxAllowedDeltaGrams;
        }

        /// <summary>
        /// Evaluates vibration severity level.
        /// Warning at &gt;= 2.5 mm/s, emergency trip shutoff at &gt;= 5.0 mm/s.
        /// </summary>
        public static bool EvaluateVibrationAlarm(double vibrationMmS, out bool isCriticalTrip)
        {
            if (vibrationMmS >= 5.0)
            {
                isCriticalTrip = true;
                return true;
            }
            if (vibrationMmS >= 2.5)
            {
                isCriticalTrip = false;
                return true;
            }
            isCriticalTrip = false;
            return false;
        }
    }
}
