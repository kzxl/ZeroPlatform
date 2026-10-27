using System;

namespace ZeroUI.Core.Logistics
{
    /// <summary>
    /// Telescopic fork extension direction into the high-bay storage rack.
    /// </summary>
    public enum AsrsForkDirection
    {
        Center,
        Left,
        Right
    }

    /// <summary>
    /// Operational motion state of an Automated Storage and Retrieval System (ASRS) stacker crane.
    /// </summary>
    public enum AsrsCraneState
    {
        Idle,
        Travelling,
        Hoisting,
        ForksExtending,
        ForksRetracting,
        Faulted
    }

    /// <summary>
    /// Physical dimensional configuration of an ASRS storage aisle.
    /// </summary>
    public class AsrsAisleConfiguration
    {
        public int TotalBays { get; set; } = 24; // Number of horizontal column positions
        public int TotalTiers { get; set; } = 10; // Number of vertical storage levels
        public double BayWidthMm { get; set; } = 1400.0;
        public double TierHeightMm { get; set; } = 1600.0;
        public double MaxPayloadWeightKg { get; set; } = 1500.0;
        public double MaxTravelSpeedMps { get; set; } = 4.0; // Horizontal travel speed (m/s)
        public double MaxHoistSpeedMps { get; set; } = 2.0; // Vertical hoist speed (m/s)
    }

    /// <summary>
    /// Real-time position, speed, and fork state of the stacker crane.
    /// </summary>
    public class AsrsCranePosition
    {
        public double CurrentBay { get; set; } = 1.0; // Can be fractional during travel
        public double CurrentTier { get; set; } = 1.0; // Can be fractional during hoist
        public double TargetBay { get; set; } = 1.0;
        public double TargetTier { get; set; } = 1.0;
        public double ForkExtensionPct { get; set; } = 0.0; // 0.0% = centered, 100.0% = fully extended
        public AsrsForkDirection Direction { get; set; } = AsrsForkDirection.Center;
        public AsrsCraneState State { get; set; } = AsrsCraneState.Idle;
    }

    /// <summary>
    /// Payload presence, weight, and tracking identity on the crane fork carriage.
    /// </summary>
    public class AsrsPayloadState
    {
        public bool HasPallet { get; set; } = true;
        public string PalletBarcode { get; set; } = "PLT-98421";
        public double WeightKg { get; set; } = 740.0;
        public bool IsOverweight(double maxLimitKg) => WeightKg > maxLimitKg;
    }

    /// <summary>
    /// Operational throughput and cycle statistics.
    /// </summary>
    public class AsrsCycleStats
    {
        public double HourlyThroughputPph { get; set; } = 54.0; // Pallets per hour
        public int CompletedSingleCycles { get; set; } = 184;
        public int CompletedDualCycles { get; set; } = 92;
        public double LastCycleTimeSeconds { get; set; } = 48.5;
        public string FaultMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pure computation engine for ASRS stacker crane kinematics, cycle time estimation, and position mapping.
    /// </summary>
    public class AsrsEngine
    {
        public AsrsAisleConfiguration Config { get; set; } = new AsrsAisleConfiguration();
        public AsrsCranePosition Position { get; set; } = new AsrsCranePosition();
        public AsrsPayloadState Payload { get; set; } = new AsrsPayloadState();
        public AsrsCycleStats Stats { get; set; } = new AsrsCycleStats();

        /// <summary>
        /// Estimates the single-command travel time in seconds between two rack coordinates.
        /// Considers simultaneous X-travel and Y-hoist kinematics: Time = Max(dX / V_x, dY / V_y) + ForkCycleTime.
        /// </summary>
        public static double EstimateMoveDurationSeconds(
            double fromBay, double fromTier,
            double toBay, double toTier,
            AsrsAisleConfiguration config,
            double forkCycleSec = 7.0)
        {
            if (config == null) return forkCycleSec;

            double deltaXMeters = Math.Abs(toBay - fromBay) * (config.BayWidthMm / 1000.0);
            double deltaYMeters = Math.Abs(toTier - fromTier) * (config.TierHeightMm / 1000.0);

            double travelTimeSec = config.MaxTravelSpeedMps > 0.0 ? deltaXMeters / config.MaxTravelSpeedMps : 0.0;
            double hoistTimeSec = config.MaxHoistSpeedMps > 0.0 ? deltaYMeters / config.MaxHoistSpeedMps : 0.0;

            return Math.Max(travelTimeSec, hoistTimeSec) + forkCycleSec;
        }

        /// <summary>
        /// Validates whether a target rack cell coordinate is within aisle boundaries.
        /// </summary>
        public static bool IsCoordinateValid(int bay, int tier, AsrsAisleConfiguration config)
        {
            if (config == null) return false;
            return bay >= 1 && bay <= config.TotalBays && tier >= 1 && tier <= config.TotalTiers;
        }

        /// <summary>
        /// Computes normalized position ratio (0.0 to 1.0) along X-aisle and Y-mast.
        /// </summary>
        public void GetNormalizedPosition(out double xRatio, out double yRatio)
        {
            int totalBays = Math.Max(1, Config.TotalBays);
            int totalTiers = Math.Max(1, Config.TotalTiers);

            xRatio = Math.Max(0.0, Math.Min(1.0, (Position.CurrentBay - 1.0) / (totalBays - 1.0)));
            yRatio = Math.Max(0.0, Math.Min(1.0, (Position.CurrentTier - 1.0) / (totalTiers - 1.0)));
        }
    }
}
