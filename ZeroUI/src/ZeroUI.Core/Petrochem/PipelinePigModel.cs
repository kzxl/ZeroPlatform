using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Petrochem
{
    /// <summary>
    /// Type of Pipeline Inspection Gauge (PIG) tool deployed in the line.
    /// </summary>
    public enum PigType
    {
        FoamCleaning,
        BrushScraper,
        MflSmartPig,           // Magnetic Flux Leakage (Metal loss & corrosion)
        UltrasonicCrack,       // High-resolution ultrasonic crack detection
        CaliperGeometry        // Deformations, dents, and ovality
    }

    /// <summary>
    /// Pipeline pigging operational progress and transit state.
    /// </summary>
    public enum PigRunStatus
    {
        InLauncher,
        RunningInPipeline,
        ApproachingReceiver,
        InReceiver,
        StalledAlert
    }

    /// <summary>
    /// Severity classification for detected pipeline wall thickness anomalies.
    /// </summary>
    public enum AnomalySeverity
    {
        Negligible,             // < 10% wall loss
        Moderate,               // 10% - 30% wall loss
        SevereActionRequired    // > 30% wall loss or acute dent
    }

    /// <summary>
    /// Above Ground Marker (AGM) or acoustic passage detection station.
    /// </summary>
    public class PipelineMarker
    {
        public string StationName { get; set; } = string.Empty;
        public double DistanceKm { get; set; }
        public bool HasPassed { get; set; }
        public DateTime? PassageTimestamp { get; set; }
        public double SignalStrengthDb { get; set; } = 85.0;
    }

    /// <summary>
    /// Wall anomaly or metal loss defect detected by the intelligent pig inspection instrumentation.
    /// </summary>
    public class PipelineAnomaly
    {
        public double DistanceKm { get; set; }
        public double ClockPositionHour { get; set; } = 6.0; // 1.0 - 12.0
        public double WallLossDepthPct { get; set; }
        public AnomalySeverity Severity { get; set; } = AnomalySeverity.Moderate;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pure computation engine and telemetry model for long-distance oil & gas pipeline pigging operations,
    /// tracking intelligent PIG velocity, driving differential pressure, AGM marker crossings, and defect logs.
    /// </summary>
    public class PipelinePigEngine
    {
        public string PipelineTag { get; set; } = "PL-24-TRANS";
        public string RouteDescription { get; set; } = "Offshore Platform Alpha -> Coastal Refinery Terminal B";

        // Pipeline Specifications
        public double TotalLengthKm { get; set; } = 85.0;
        public double PipeDiameterInches { get; set; } = 24.0;
        public double NominalWallThicknessMm { get; set; } = 14.3;

        // PIG Tool Telemetry
        public PigType ToolType { get; set; } = PigType.MflSmartPig;
        public PigRunStatus Status { get; set; } = PigRunStatus.RunningInPipeline;
        public double CurrentDistanceKm { get; set; } = 28.4;
        public double PigVelocityMs { get; set; } = 2.45; // Standard inspection velocity 1.5 - 3.5 m/s
        public double DifferentialPressureBar { get; set; } = 1.85; // Driving dP across cups
        public double BatteryPct { get; set; } = 84.0;
        public double StorageMemoryPct { get; set; } = 42.0;
        public double OdometerRevs { get; set; } = 14200.0;

        public double ProgressPct => TotalLengthKm > 0.0 ? Math.Min(100.0, Math.Round((CurrentDistanceKm / TotalLengthKm) * 100.0, 1)) : 0.0;

        public TimeSpan EstimatedTimeToReceiver
        {
            get
            {
                if (Status == PigRunStatus.InReceiver) return TimeSpan.Zero;
                if (PigVelocityMs <= 0.05) return TimeSpan.FromHours(99.0);
                double remainingMeters = Math.Max(0.0, (TotalLengthKm - CurrentDistanceKm) * 1000.0);
                double seconds = remainingMeters / PigVelocityMs;
                return TimeSpan.FromSeconds(Math.Min(seconds, 360000.0));
            }
        }

        public List<PipelineMarker> Markers { get; } = new List<PipelineMarker>();
        public List<PipelineAnomaly> Anomalies { get; } = new List<PipelineAnomaly>();

        public PipelinePigEngine()
        {
            InitializeDefaultPipeline();
        }

        public void InitializeDefaultPipeline()
        {
            Markers.Clear();
            Markers.Add(new PipelineMarker { StationName = "Launcher Trap A (KP 0.0)", DistanceKm = 0.0, HasPassed = true, PassageTimestamp = DateTime.UtcNow.AddHours(-3.5) });
            Markers.Add(new PipelineMarker { StationName = "Block Valve Stn 1 (KP 18.5)", DistanceKm = 18.5, HasPassed = true, PassageTimestamp = DateTime.UtcNow.AddHours(-1.4) });
            Markers.Add(new PipelineMarker { StationName = "River Crossing AGM (KP 38.2)", DistanceKm = 38.2, HasPassed = false });
            Markers.Add(new PipelineMarker { StationName = "Booster Station 2 (KP 61.0)", DistanceKm = 61.0, HasPassed = false });
            Markers.Add(new PipelineMarker { StationName = "Receiver Trap B (KP 85.0)", DistanceKm = 85.0, HasPassed = false });

            Anomalies.Clear();
            Anomalies.Add(new PipelineAnomaly { DistanceKm = 12.4, ClockPositionHour = 6.0, WallLossDepthPct = 18.0, Severity = AnomalySeverity.Moderate, Description = "Internal bottom line pitting" });
            Anomalies.Add(new PipelineAnomaly { DistanceKm = 24.8, ClockPositionHour = 2.5, WallLossDepthPct = 8.5, Severity = AnomalySeverity.Negligible, Description = "Minor surface abrasion" });
            Anomalies.Add(new PipelineAnomaly { DistanceKm = 39.1, ClockPositionHour = 5.5, WallLossDepthPct = 42.0, Severity = AnomalySeverity.SevereActionRequired, Description = "Channel corrosion gouge" });
            Anomalies.Add(new PipelineAnomaly { DistanceKm = 55.6, ClockPositionHour = 12.0, WallLossDepthPct = 14.2, Severity = AnomalySeverity.Moderate, Description = "Girth weld root anomaly" });
            Anomalies.Add(new PipelineAnomaly { DistanceKm = 71.2, ClockPositionHour = 8.0, WallLossDepthPct = 26.5, Severity = AnomalySeverity.Moderate, Description = "External soil corrosion" });
        }

        /// <summary>
        /// Advances pig kinematics along the pipeline, checking marker passage and detecting stalled conditions.
        /// </summary>
        public void AdvanceTime(double deltaSeconds)
        {
            if (Status == PigRunStatus.InReceiver) return;

            // Stalled detection: high differential pressure while speed is near zero
            if (PigVelocityMs < 0.1 && DifferentialPressureBar > 4.5)
            {
                Status = PigRunStatus.StalledAlert;
                return;
            }

            if (Status == PigRunStatus.StalledAlert && PigVelocityMs > 0.3)
            {
                Status = PigRunStatus.RunningInPipeline;
            }

            if (PigVelocityMs <= 0.0) return;

            double advanceKm = (PigVelocityMs * deltaSeconds) / 1000.0;
            CurrentDistanceKm = Math.Min(TotalLengthKm, CurrentDistanceKm + advanceKm);
            OdometerRevs += (PigVelocityMs * deltaSeconds) / 0.6; // approx wheel circumference

            // Check marker passage
            for (int i = 0; i < Markers.Count; i++)
            {
                var marker = Markers[i];
                if (!marker.HasPassed && CurrentDistanceKm >= marker.DistanceKm)
                {
                    marker.HasPassed = true;
                    marker.PassageTimestamp = DateTime.UtcNow;
                }
            }

            // Check receiver arrival
            if (CurrentDistanceKm >= TotalLengthKm)
            {
                CurrentDistanceKm = TotalLengthKm;
                Status = PigRunStatus.InReceiver;
                PigVelocityMs = 0.0;
            }
            else if (CurrentDistanceKm >= TotalLengthKm - 2.5)
            {
                Status = PigRunStatus.ApproachingReceiver;
            }
        }
    }
}
