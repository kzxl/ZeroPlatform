using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Logistics
{
    /// <summary>
    /// Type of Automated Guided Vehicle (AGV) or Autonomous Mobile Robot (AMR).
    /// </summary>
    public enum AgvVehicleType
    {
        AMR,
        Forklift,
        Tugger,
        UnitLoad
    }

    /// <summary>
    /// Real-time operational dispatch state of an AGV/AMR robot.
    /// </summary>
    public enum AgvStatus
    {
        Idle,
        Moving,
        Charging,
        Loading,
        ObstacleBlocked,
        EmergencyStop
    }

    /// <summary>
    /// Functional type of facility station or docking zone.
    /// </summary>
    public enum AgvStationType
    {
        Pickup,
        Dropoff,
        Charging,
        Staging
    }

    /// <summary>
    /// 2D waypoint along a planned trajectory spline.
    /// </summary>
    public struct AgvWaypoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double TargetSpeedMps { get; set; }

        public AgvWaypoint(double x, double y, double speed = 1.2)
        {
            X = x;
            Y = y;
            TargetSpeedMps = speed;
        }
    }

    /// <summary>
    /// Facility station or automated dock location.
    /// </summary>
    public class AgvStation
    {
        public string StationId { get; set; } = "ST-01";
        public string Name { get; set; } = "Charging Dock 1";
        public AgvStationType StationType { get; set; } = AgvStationType.Charging;
        public double X { get; set; } = 40.0;
        public double Y { get; set; } = 40.0;
        public bool IsOccupied { get; set; } = false;
    }

    /// <summary>
    /// Real-time state of an individual AGV/AMR robot in the fleet.
    /// </summary>
    public class AgvVehicle
    {
        public int Id { get; set; } = 1;
        public string Name { get; set; } = "AMR-101";
        public AgvVehicleType Type { get; set; } = AgvVehicleType.AMR;
        public double X { get; set; } = 100.0;
        public double Y { get; set; } = 120.0;
        public double HeadingAngleDeg { get; set; } = 45.0; // 0° = East (+X), 90° = South (+Y)
        public double VelocityMps { get; set; } = 1.2;
        public double BatterySocPct { get; set; } = 82.0; // 0.0 to 100.0%
        public AgvStatus Status { get; set; } = AgvStatus.Moving;
        public string CurrentTask { get; set; } = "Transport to Line 3";
        public string PayloadBarcode { get; set; } = "TOTE-4921";
        public double SafetyWarningRadiusM { get; set; } = 3.0;
        public double SafetyStopRadiusM { get; set; } = 1.2;
        public List<AgvWaypoint> PlannedPath { get; } = new List<AgvWaypoint>();
    }

    /// <summary>
    /// Pure computation engine for multi-vehicle fleet coordination, LiDAR safety zones, and proximity monitoring.
    /// </summary>
    public class AgvFleetEngine
    {
        public List<AgvVehicle> Vehicles { get; } = new List<AgvVehicle>();
        public List<AgvStation> Stations { get; } = new List<AgvStation>();

        public AgvFleetEngine()
        {
            // Default 3-Vehicle, 3-Station factory layout
            Stations.Add(new AgvStation { StationId = "CHG-1", Name = "Charger A", StationType = AgvStationType.Charging, X = 50, Y = 50 });
            Stations.Add(new AgvStation { StationId = "PK-1", Name = "Pickup Cell 1", StationType = AgvStationType.Pickup, X = 320, Y = 60 });
            Stations.Add(new AgvStation { StationId = "DP-1", Name = "Dropoff Line A", StationType = AgvStationType.Dropoff, X = 300, Y = 240 });

            var v1 = new AgvVehicle { Id = 1, Name = "AMR-101", Type = AgvVehicleType.AMR, X = 120, Y = 80, HeadingAngleDeg = 30, BatterySocPct = 88, Status = AgvStatus.Moving };
            v1.PlannedPath.Add(new AgvWaypoint(120, 80));
            v1.PlannedPath.Add(new AgvWaypoint(220, 80));
            v1.PlannedPath.Add(new AgvWaypoint(300, 160));
            v1.PlannedPath.Add(new AgvWaypoint(300, 240));

            var v2 = new AgvVehicle { Id = 2, Name = "FORK-201", Type = AgvVehicleType.Forklift, X = 200, Y = 180, HeadingAngleDeg = 180, BatterySocPct = 42, Status = AgvStatus.Moving, CurrentTask = "Pallet Transfer" };
            v2.PlannedPath.Add(new AgvWaypoint(200, 180));
            v2.PlannedPath.Add(new AgvWaypoint(100, 180));
            v2.PlannedPath.Add(new AgvWaypoint(50, 120));

            var v3 = new AgvVehicle { Id = 3, Name = "AMR-102", Type = AgvVehicleType.AMR, X = 50, Y = 50, HeadingAngleDeg = 0, BatterySocPct = 95, Status = AgvStatus.Charging, CurrentTask = "Fast Charge" };

            Vehicles.Add(v1);
            Vehicles.Add(v2);
            Vehicles.Add(v3);
        }

        /// <summary>
        /// Euclidean distance between two 2D points.
        /// </summary>
        public static double CalculateDistance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Evaluates pairs of active vehicles that are within warning proximity distance.
        /// </summary>
        public List<(AgvVehicle VehicleA, AgvVehicle VehicleB, double Distance)> DetectProximityWarnings(double warningRadius = 50.0)
        {
            var warnings = new List<(AgvVehicle, AgvVehicle, double)>();

            for (int i = 0; i < Vehicles.Count; i++)
            {
                var a = Vehicles[i];
                for (int j = i + 1; j < Vehicles.Count; j++)
                {
                    var b = Vehicles[j];
                    double dist = CalculateDistance(a.X, a.Y, b.X, b.Y);
                    if (dist <= warningRadius)
                    {
                        warnings.Add((a, b, dist));
                    }
                }
            }

            return warnings;
        }

        /// <summary>
        /// Aggregates fleet average Battery State of Charge (SoC %).
        /// </summary>
        public double AverageBatterySocPct
        {
            get
            {
                if (Vehicles.Count == 0) return 0.0;
                double total = 0.0;
                for (int i = 0; i < Vehicles.Count; i++)
                    total += Vehicles[i].BatterySocPct;
                return total / Vehicles.Count;
            }
        }
    }
}
