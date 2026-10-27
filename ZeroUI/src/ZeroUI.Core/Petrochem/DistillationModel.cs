using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Petrochem
{
    /// <summary>
    /// Type of vapor-liquid contacting internal in a fractional distillation column.
    /// </summary>
    public enum DistillationTrayType
    {
        SieveTray,
        ValveTray,
        BubbleCap,
        StructuredPacking
    }

    /// <summary>
    /// Hydrodynamic and thermodynamic operating condition of the distillation column.
    /// </summary>
    public enum ColumnOperatingState
    {
        Normal,
        FloodingRisk,
        WeepingRisk,
        DryTray,
        Shutdown
    }

    /// <summary>
    /// Telemetry and mechanical state of an individual fractionation tray stage.
    /// </summary>
    public class DistillationTray
    {
        public int TrayIndex { get; set; }
        public double TemperatureC { get; set; }
        public double PressureKpa { get; set; }
        public double LiquidHoldupMm { get; set; } = 45.0;
        public double VaporVelocityMs { get; set; } = 1.35;
        public string FractionName { get; set; } = string.Empty;
        public bool IsFeedTray { get; set; }
        public bool HasSideDraw { get; set; }
    }

    /// <summary>
    /// Pure computation engine and telemetry model for continuous multi-stage fractional distillation columns,
    /// reflux ratio balance, reboiler/condenser thermal duty, and vapor flooding/weeping hydraulics.
    /// </summary>
    public class DistillationEngine
    {
        public string ColumnTag { get; set; } = "T-101";
        public string ServiceDescription { get; set; } = "Deethanizer Fractionator";
        public DistillationTrayType TrayType { get; set; } = DistillationTrayType.ValveTray;

        // Column Physical Dimensions
        public int TrayCount { get; set; } = 24;
        public double DiameterM { get; set; } = 2.4;
        public double HeightM { get; set; } = 36.0;

        // Feed Stream Parameters
        public int FeedTrayIndex { get; set; } = 12;
        public double FeedFlowRateKgH { get; set; } = 45000.0;
        public double FeedTempC { get; set; } = 85.0;

        // Overhead Condenser & Reflux Subsystem
        public double OverheadTempC { get; set; } = 42.0;
        public double CondenserDutyKw { get; set; } = 3200.0;
        public double RefluxRatio { get; set; } = 2.45; // L / D
        public double RefluxFlowRateM3H { get; set; } = 28.5;
        public double DistillateFlowRateM3H { get; set; } = 11.6;
        public double RefluxDrumLevelPct { get; set; } = 55.0;

        // Bottom Reboiler & Sump Subsystem
        public double BottomsTempC { get; set; } = 168.0;
        public double ReboilerDutyKw { get; set; } = 3850.0;
        public double BoilupRatio { get; set; } = 1.85; // V / B
        public double SumpLevelPct { get; set; } = 62.0;
        public double BottomsFlowRateM3H { get; set; } = 18.2;

        // Pressure Profile
        public double TopPressureKpa { get; set; } = 180.0;
        public double BottomPressureKpa { get; set; } = 245.0;
        public double DifferentialPressureKpa => Math.Max(0.0, BottomPressureKpa - TopPressureKpa);

        // Hydrodynamics & Souders-Brown Flooding Index
        public double FloodVelocityMs { get; set; } = 1.80;
        public double ActualVaporVelocityMs { get; set; } = 1.45;

        public double FloodMarginPct => FloodVelocityMs > 0.0 ? Math.Round((ActualVaporVelocityMs / FloodVelocityMs) * 100.0, 1) : 0.0;
        public double NetHeatDutyKw => Math.Round(ReboilerDutyKw - CondenserDutyKw, 1);

        public ColumnOperatingState OperatingState
        {
            get
            {
                if (ReboilerDutyKw <= 0.0 && FeedFlowRateKgH <= 0.0) return ColumnOperatingState.Shutdown;
                if (FloodMarginPct >= 90.0 || DifferentialPressureKpa > 95.0) return ColumnOperatingState.FloodingRisk;
                if (FloodMarginPct < 25.0 || ActualVaporVelocityMs < 0.35) return ColumnOperatingState.WeepingRisk;
                if (SumpLevelPct < 15.0) return ColumnOperatingState.DryTray;
                return ColumnOperatingState.Normal;
            }
        }

        public List<DistillationTray> Trays { get; } = new List<DistillationTray>();

        public DistillationEngine()
        {
            RecomputeTrays();
        }

        /// <summary>
        /// Re-populates the trays array with linear pressure and temperature gradient interpolations.
        /// </summary>
        public void RecomputeTrays()
        {
            Trays.Clear();
            int count = Math.Max(4, TrayCount);

            for (int i = 1; i <= count; i++)
            {
                // Fraction 0.0 at top tray (count), 1.0 at bottom tray (1)
                double factor = (count - i) / (double)(count - 1);
                double temp = OverheadTempC + factor * (BottomsTempC - OverheadTempC);
                double pressure = TopPressureKpa + factor * (BottomPressureKpa - TopPressureKpa);

                string fraction = string.Empty;
                if (i >= count - 2) fraction = "Off-gas / LPG";
                else if (i >= count - 6) fraction = "Light Naphtha";
                else if (i >= count - 12) fraction = "Heavy Naphtha";
                else if (i >= count - 18) fraction = "Kerosene";
                else fraction = "Gas Oil / Residue";

                Trays.Add(new DistillationTray
                {
                    TrayIndex = i,
                    TemperatureC = Math.Round(temp, 1),
                    PressureKpa = Math.Round(pressure, 1),
                    LiquidHoldupMm = Math.Round(35.0 + factor * 25.0, 1),
                    VaporVelocityMs = Math.Round(ActualVaporVelocityMs * (1.1 - factor * 0.2), 2),
                    FractionName = fraction,
                    IsFeedTray = (i == FeedTrayIndex),
                    HasSideDraw = (i == 7 || i == 15)
                });
            }
        }
    }
}
