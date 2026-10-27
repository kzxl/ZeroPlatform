using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Logistics
{
    /// <summary>
    /// Operating state of an automated parcel conveyor line.
    /// </summary>
    public enum ConveyorLineStatus
    {
        Running,
        Stopped,
        JamAlert,
        Maintenance
    }

    /// <summary>
    /// Sorter divert decision action for a parcel.
    /// </summary>
    public enum DivertAction
    {
        Straight,
        DivertLeft,
        DivertRight
    }

    /// <summary>
    /// Individual parcel package or carton traveling along the conveyor.
    /// </summary>
    public class ParcelItem
    {
        public string Barcode { get; set; } = "PKG-84920";
        public double LengthMm { get; set; } = 400.0;
        public double WidthMm { get; set; } = 300.0;
        public double WeightKg { get; set; } = 4.5;
        public double PositionRatio { get; set; } = 0.0; // 0.0 (infeed) to 1.0 (discharge)
        public int DestinationChuteId { get; set; } = 1;
        public bool IsDiverted { get; set; } = false;

        public ParcelItem()
        {
        }

        public ParcelItem(string barcode, double pos, int targetChute)
        {
            Barcode = barcode;
            PositionRatio = pos;
            DestinationChuteId = targetChute;
        }
    }

    /// <summary>
    /// Optical infrared photo-eye beam sensor detecting package presence and conveyor jams.
    /// </summary>
    public class PhotoEyeSensor
    {
        public int SensorId { get; set; } = 1;
        public string Name { get; set; } = "PE-01 Infeed";
        public double PositionRatio { get; set; } = 0.25; // 0.0 to 1.0
        public bool IsBlocked { get; set; } = false;
        public double BlockDurationMs { get; set; } = 0.0;
        public double JamThresholdMs { get; set; } = 3000.0; // Over 3s continuous block = Jam

        public bool IsJamAlert => IsBlocked && BlockDurationMs >= JamThresholdMs;
    }

    /// <summary>
    /// Sorter divert chute or spur destination.
    /// </summary>
    public class DivertChute
    {
        public int ChuteId { get; set; } = 1;
        public string DestinationCode { get; set; } = "BAY-A (North Express)";
        public double PositionRatio { get; set; } = 0.50; // Along main conveyor
        public bool IsDiverting { get; set; } = false;
        public int DivertedCount { get; set; } = 0;
    }

    /// <summary>
    /// Pure computation engine for high-speed conveyor merge, singulation spacing, and sorting diverts.
    /// </summary>
    public class ConveyorMergeEngine
    {
        public ConveyorLineStatus Status { get; set; } = ConveyorLineStatus.Running;
        public double MainSpeedMpm { get; set; } = 90.0; // Meters per minute (~1.5 m/s)
        public double LineLengthMeters { get; set; } = 30.0;
        public List<ParcelItem> Parcels { get; } = new List<ParcelItem>();
        public List<PhotoEyeSensor> Sensors { get; } = new List<PhotoEyeSensor>();
        public List<DivertChute> Chutes { get; } = new List<DivertChute>();
        public double HourlyThroughputPph { get; set; } = 4200.0; // Parcels per hour

        public ConveyorMergeEngine()
        {
            // Default 3 Divert Chutes along the line
            Chutes.Add(new DivertChute { ChuteId = 1, DestinationCode = "CH-01 Local", PositionRatio = 0.35 });
            Chutes.Add(new DivertChute { ChuteId = 2, DestinationCode = "CH-02 Priority", PositionRatio = 0.60 });
            Chutes.Add(new DivertChute { ChuteId = 3, DestinationCode = "CH-03 Air Freight", PositionRatio = 0.85 });

            // Default 3 Optical Photo-eyes
            Sensors.Add(new PhotoEyeSensor { SensorId = 1, Name = "PE-01 Infeed", PositionRatio = 0.15 });
            Sensors.Add(new PhotoEyeSensor { SensorId = 2, Name = "PE-02 Merge", PositionRatio = 0.45 });
            Sensors.Add(new PhotoEyeSensor { SensorId = 3, Name = "PE-03 Divert Matrix", PositionRatio = 0.75 });

            // Seed sample active packages
            Parcels.Add(new ParcelItem("PKG-101", 0.08, 1));
            Parcels.Add(new ParcelItem("PKG-102", 0.30, 2));
            Parcels.Add(new ParcelItem("PKG-103", 0.52, 1));
            Parcels.Add(new ParcelItem("PKG-104", 0.78, 3));
        }

        /// <summary>
        /// Advances parcel positions along the conveyor by elapsed delta time in seconds.
        /// </summary>
        public void AdvanceParcels(double deltaSeconds)
        {
            if (Status != ConveyorLineStatus.Running || deltaSeconds <= 0.0)
                return;

            double speedMps = MainSpeedMpm / 60.0;
            double deltaRatio = LineLengthMeters > 0 ? (speedMps * deltaSeconds) / LineLengthMeters : 0.0;

            for (int i = Parcels.Count - 1; i >= 0; i--)
            {
                var p = Parcels[i];
                p.PositionRatio += deltaRatio;

                // Check divert chute intersection
                for (int c = 0; c < Chutes.Count; c++)
                {
                    var chute = Chutes[c];
                    if (!p.IsDiverted && p.DestinationChuteId == chute.ChuteId)
                    {
                        double prevRatio = p.PositionRatio - deltaRatio;
                        bool crossed = (p.PositionRatio >= chute.PositionRatio && prevRatio <= chute.PositionRatio) ||
                                       Math.Abs(p.PositionRatio - chute.PositionRatio) <= 0.05;

                        if (crossed)
                        {
                            p.IsDiverted = true;
                            chute.DivertedCount++;
                            chute.IsDiverting = true;
                        }
                    }
                }

                // Recycle parcels that exit the discharge end
                if (p.PositionRatio >= 1.0)
                {
                    p.PositionRatio = 0.0;
                    p.IsDiverted = false;
                }
            }

            // Update photo-eye occlusion status
            UpdatePhotoEyeSensors();
        }

        /// <summary>
        /// Checks whether any parcel is currently obstructing a photo-eye sensor.
        /// </summary>
        private void UpdatePhotoEyeSensors()
        {
            for (int s = 0; s < Sensors.Count; s++)
            {
                var pe = Sensors[s];
                bool blocked = false;

                for (int p = 0; p < Parcels.Count; p++)
                {
                    var parcel = Parcels[p];
                    if (Math.Abs(parcel.PositionRatio - pe.PositionRatio) < 0.03)
                    {
                        blocked = true;
                        break;
                    }
                }

                pe.IsBlocked = blocked;
                if (!blocked)
                {
                    pe.BlockDurationMs = 0.0;
                }
            }
        }

        /// <summary>
        /// Calculates sorting throughput in Parcels Per Hour (PPH).
        /// </summary>
        public static double CalculatePph(int parcelCount, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0.0) return 0.0;
            return (parcelCount / elapsedSeconds) * 3600.0;
        }
    }
}
