using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Water
{
    /// <summary>
    /// Configuration point along a pipeline conveying water via gravity and/or booster pumps.
    /// </summary>
    public class HydraulicStation
    {
        public double StationM { get; set; } // Distance along pipeline chainage in meters
        public double GroundElevationM { get; set; } // Terrain ground surface elevation (m MSL)
        public double PipeInvertM { get; set; } // Bottom inside pipe elevation (m MSL)
        public double PipeDiameterMm { get; set; } = 500.0; // Pipe diameter in mm
        public double PumpHeadBoostM { get; set; } = 0.0; // Positive head addition if a booster pump is located here
        public string? Description { get; set; }
    }

    /// <summary>
    /// Calculated hydraulic status and grade lines at a specific pipeline chainage coordinate.
    /// </summary>
    public class HydraulicProfilePoint
    {
        public double StationM { get; set; }
        public double GroundElevationM { get; set; }
        public double PipeInvertM { get; set; }
        public double PipeDiameterM { get; set; }
        public double PipeCrownM => PipeInvertM + PipeDiameterM;
        public double HglElevationM { get; set; } // Hydraulic Grade Line (piezometric head: z + P/gamma)
        public double EglElevationM { get; set; } // Energy Grade Line (total energy: HGL + v^2 / 2g)
        public double FlowVelocityMS { get; set; }

        /// <summary>
        /// Gauge pressure head in meters of water column above the pipe crown.
        /// If negative, indicates vacuum/siphon condition with cavitation risk.
        /// </summary>
        public double PressureHeadM => HglElevationM - PipeCrownM;

        /// <summary>
        /// Static pressure in kilopascals (kPa). 1 m H2O ≈ 9.80665 kPa.
        /// </summary>
        public double PressureKpa => PressureHeadM * 9.80665;

        /// <summary>
        /// True if the Hydraulic Grade Line drops below the top (crown) of the pipe conduit.
        /// </summary>
        public bool IsNegativePressure => PressureHeadM < 0.0;
    }

    /// <summary>
    /// Pure computation engine for pipeline hydraulic profiles, friction loss modeling (Hazen-Williams),
    /// Hydraulic Grade Line (HGL), Energy Grade Line (EGL), and negative pressure risk detection.
    /// </summary>
    public class HydraulicGradientEngine
    {
        private readonly List<HydraulicStation> _stations = new List<HydraulicStation>();

        public List<HydraulicStation> Stations => _stations;

        public double FlowRateM3S { get; set; } = 0.28; // ~1,000 m3/h in m3/s
        public double HazenWilliamsC { get; set; } = 130.0; // Ductile iron / HDPE roughness coefficient
        public double StartingHeadM { get; set; } = 65.0; // Reservoir / initial water surface elevation (m MSL)

        public HydraulicGradientEngine()
        {
            SeedDefaultProfile();
        }

        public void SeedDefaultProfile()
        {
            _stations.Clear();
            _stations.Add(new HydraulicStation { StationM = 0, GroundElevationM = 50.0, PipeInvertM = 48.0, PipeDiameterMm = 600, Description = "Water Intake & Reservoir" });
            _stations.Add(new HydraulicStation { StationM = 800, GroundElevationM = 42.0, PipeInvertM = 40.0, PipeDiameterMm = 600, PumpHeadBoostM = 38.0, Description = "Lift Station Booster P-101" });
            _stations.Add(new HydraulicStation { StationM = 1600, GroundElevationM = 68.0, PipeInvertM = 66.0, PipeDiameterMm = 500, Description = "Ridge Summit Saddle" });
            _stations.Add(new HydraulicStation { StationM = 2400, GroundElevationM = 58.0, PipeInvertM = 56.0, PipeDiameterMm = 500, Description = "Intermediate Valve Vault" });
            _stations.Add(new HydraulicStation { StationM = 3200, GroundElevationM = 46.0, PipeInvertM = 44.0, PipeDiameterMm = 500, Description = "Valley Crossing Culvert" });
            _stations.Add(new HydraulicStation { StationM = 4000, GroundElevationM = 54.0, PipeInvertM = 52.0, PipeDiameterMm = 500, Description = "Water Treatment Plant (WTP)" });
        }

        /// <summary>
        /// Computes the complete hydraulic grade profile along all configured stations.
        /// </summary>
        public List<HydraulicProfilePoint> CalculateProfile()
        {
            var results = new List<HydraulicProfilePoint>(_stations.Count);
            if (_stations.Count == 0)
                return results;

            // Sort by chainage station distance
            _stations.Sort((a, b) => a.StationM.CompareTo(b.StationM));

            double currentHgl = StartingHeadM;

            for (int i = 0; i < _stations.Count; i++)
            {
                var st = _stations[i];
                double diaM = Math.Max(0.05, st.PipeDiameterMm / 1000.0);
                double area = Math.PI * Math.Pow(diaM * 0.5, 2);
                double velocity = area > 0.0 ? FlowRateM3S / area : 0.0;
                double velocityHead = (velocity * velocity) / (2.0 * 9.80665);

                if (i > 0)
                {
                    var prevSt = _stations[i - 1];
                    double distance = Math.Max(0.0, st.StationM - prevSt.StationM);

                    // Hazen-Williams Head Loss: hf = 10.67 * L * Q^1.852 / (C^1.852 * D^4.87)
                    double q = Math.Max(0.0001, FlowRateM3S);
                    double c = Math.Max(10.0, HazenWilliamsC);
                    double hf = (10.67 * distance * Math.Pow(q, 1.852)) / (Math.Pow(c, 1.852) * Math.Pow(diaM, 4.87));

                    currentHgl -= hf;
                }

                // Add pump head boost if any at this station
                currentHgl += st.PumpHeadBoostM;

                double egl = currentHgl + velocityHead;

                results.Add(new HydraulicProfilePoint
                {
                    StationM = st.StationM,
                    GroundElevationM = st.GroundElevationM,
                    PipeInvertM = st.PipeInvertM,
                    PipeDiameterM = diaM,
                    HglElevationM = currentHgl,
                    EglElevationM = egl,
                    FlowVelocityMS = velocity
                });
            }

            return results;
        }

        /// <summary>
        /// Checks if any station on the pipeline experiences sub-atmospheric / negative pressure.
        /// </summary>
        public bool HasNegativePressureAnomaly(out double worstStationM, out double minPressureHeadM)
        {
            worstStationM = 0;
            minPressureHeadM = double.MaxValue;
            var profile = CalculateProfile();

            bool hasNegative = false;
            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                if (pt.PressureHeadM < minPressureHeadM)
                {
                    minPressureHeadM = pt.PressureHeadM;
                    worstStationM = pt.StationM;
                }
                if (pt.IsNegativePressure)
                {
                    hasNegative = true;
                }
            }

            return hasNegative;
        }
    }
}
