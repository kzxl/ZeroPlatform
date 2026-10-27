using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Energy
{
    /// <summary>
    /// Health and operational status of a solar photovoltaic (PV) string.
    /// </summary>
    public enum PvStringStatus
    {
        Normal,
        Shaded,
        BlownFuse,
        Degraded
    }

    /// <summary>
    /// Represents a series-connected string of photovoltaic modules connected to a central or string inverter MPPT.
    /// </summary>
    public class PvString
    {
        public int StringId { get; set; } = 1;
        public int InverterId { get; set; } = 1;
        public int ModuleCount { get; set; } = 24;
        public double VoltageV { get; set; } = 740.0;
        public double CurrentA { get; set; } = 11.5;
        public double PowerKw => (VoltageV * CurrentA) / 1000.0;
        public double MpptEfficiencyPct { get; set; } = 98.4;
        public PvStringStatus Status { get; set; } = PvStringStatus.Normal;
    }

    /// <summary>
    /// Utility-scale or commercial solar PV generation plant telemetry.
    /// </summary>
    public class SolarPvPlant
    {
        public string PlantId { get; set; } = "SOLAR-FARM-01";
        public string Name { get; set; } = "Solar Array Block A";
        public double SolarIrradianceWm2 { get; set; } = 960.0; // W/m² global tilted irradiance
        public double AmbientTempC { get; set; } = 32.0;
        public double CellTempC { get; set; } = 48.5;
        public double PerformanceRatioPct { get; set; } = 82.5; // PR %
        public List<PvString> Strings { get; } = new List<PvString>();

        public SolarPvPlant()
        {
            // Seed 16 PV strings (e.g. 4 strings x 4 MPPT channels)
            for (int i = 1; i <= 16; i++)
            {
                Strings.Add(new PvString
                {
                    StringId = i,
                    InverterId = (i - 1) / 4 + 1,
                    VoltageV = 735.0 + (i % 3) * 5.0,
                    CurrentA = 11.4 + (i % 2) * 0.2,
                    Status = (i == 13) ? PvStringStatus.Shaded : PvStringStatus.Normal
                });
            }
        }

        public double TotalPowerKw
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < Strings.Count; i++)
                    total += Strings[i].PowerKw;
                return total;
            }
        }
    }

    /// <summary>
    /// Pure computation engine for PV generation modeling, temperature derating, and string mismatch heuristics.
    /// </summary>
    public class SolarPvEngine
    {
        public SolarPvPlant Plant { get; } = new SolarPvPlant();

        /// <summary>
        /// Calculates temperature derated PV output power.
        /// Typical silicon module power temperature coefficient: gamma = -0.38% / °C above 25°C STC.
        /// Formula: P_derated = P_stc * [1 + (gamma / 100) * (T_cell - 25.0)]
        /// </summary>
        public static double CalculateTemperatureDeratedPower(
            double stcPowerKw,
            double cellTempC,
            double tempCoeffPctPerC = -0.38)
        {
            if (stcPowerKw <= 0.0) return 0.0;
            double deltaT = cellTempC - 25.0;
            double derateFactor = 1.0 + (tempCoeffPctPerC / 100.0) * deltaT;
            return Math.Max(0.0, stcPowerKw * derateFactor);
        }

        /// <summary>
        /// Analyzes string currents against the fleet median to identify shaded modules or blown fuses.
        /// </summary>
        public void EvaluateStringMismatch(double expectedCurrentA = 11.5)
        {
            for (int i = 0; i < Plant.Strings.Count; i++)
            {
                var s = Plant.Strings[i];
                if (s.CurrentA < 1.0)
                {
                    s.Status = PvStringStatus.BlownFuse;
                }
                else if (s.CurrentA < expectedCurrentA * 0.75)
                {
                    s.Status = PvStringStatus.Shaded;
                }
                else if (s.CurrentA < expectedCurrentA * 0.90)
                {
                    s.Status = PvStringStatus.Degraded;
                }
                else
                {
                    s.Status = PvStringStatus.Normal;
                }
            }
        }
    }
}
