using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Energy
{
    /// <summary>
    /// Nominal electrical voltage class according to standard utility grids.
    /// </summary>
    public enum VoltageLevel
    {
        UHV_500kV,
        EHV_220kV,
        HV_110kV,
        MV_22kV,
        LV_400V
    }

    /// <summary>
    /// Operating topological energization state of an electrical busbar.
    /// </summary>
    public enum BusbarState
    {
        Energized,
        DeEnergized,
        Grounded,
        Faulted
    }

    /// <summary>
    /// Circuit breaker and disconnector contact position.
    /// </summary>
    public enum BreakerState
    {
        Open,
        Closed,
        Tripped,
        InTransit
    }

    /// <summary>
    /// Substation electrical busbar node.
    /// </summary>
    public class SldBusbar
    {
        public string BusId { get; set; } = "BUS-110A";
        public string Name { get; set; } = "110kV Main Bus A";
        public VoltageLevel Voltage { get; set; } = VoltageLevel.HV_110kV;
        public double NominalKv { get; set; } = 110.0;
        public double MeasuredKv { get; set; } = 112.4;
        public BusbarState State { get; set; } = BusbarState.Energized;
        public double FrequencyHz { get; set; } = 50.02;
    }

    /// <summary>
    /// Circuit breaker device model with active telemetry.
    /// </summary>
    public class SldBreaker
    {
        public string BreakerId { get; set; } = "CB-101";
        public string Name { get; set; } = "Feeder 1 Breaker";
        public BreakerState State { get; set; } = BreakerState.Closed;
        public bool IsInterlocked { get; set; } = false;
        public double CurrentAmps { get; set; } = 420.0;
        public double ActivePowerMw { get; set; } = 78.5;
        public double ReactivePowerMvar { get; set; } = 14.2;
    }

    /// <summary>
    /// Step-up or step-down electrical power transformer.
    /// </summary>
    public class SldTransformer
    {
        public string TransformerId { get; set; } = "T-1";
        public string Name { get; set; } = "Main Step-Down Transformer 110/22kV";
        public double PrimaryNominalKv { get; set; } = 110.0;
        public double SecondaryNominalKv { get; set; } = 22.0;
        public double RatedMva { get; set; } = 63.0;
        public double ActualMva { get; set; } = 48.2;
        public double OilTemperatureC { get; set; } = 58.0;
        public double WindingTemperatureC { get; set; } = 64.5;
        public double LoadPercentage => RatedMva > 0 ? (ActualMva / RatedMva) * 100.0 : 0.0;
    }

    /// <summary>
    /// Pure computation engine for IEC 61850 single-line diagrams, voltage coloring, and power flows.
    /// </summary>
    public class SldEngine
    {
        public List<SldBusbar> Busbars { get; } = new List<SldBusbar>();
        public List<SldBreaker> Breakers { get; } = new List<SldBreaker>();
        public List<SldTransformer> Transformers { get; } = new List<SldTransformer>();

        public SldEngine()
        {
            // Seed sample 110kV / 22kV distribution substation configuration
            Busbars.Add(new SldBusbar { BusId = "BUS-110A", Name = "110kV Bus A", Voltage = VoltageLevel.HV_110kV, NominalKv = 110, MeasuredKv = 111.8 });
            Busbars.Add(new SldBusbar { BusId = "BUS-22A", Name = "22kV Bus A", Voltage = VoltageLevel.MV_22kV, NominalKv = 22, MeasuredKv = 22.4 });

            Transformers.Add(new SldTransformer { TransformerId = "T-1", Name = "110/22kV T1", PrimaryNominalKv = 110, SecondaryNominalKv = 22, RatedMva = 63, ActualMva = 42.5 });

            Breakers.Add(new SldBreaker { BreakerId = "CB-110", Name = "110kV Incomer", State = BreakerState.Closed, CurrentAmps = 240, ActivePowerMw = 42.0, ReactivePowerMvar = 8.5 });
            Breakers.Add(new SldBreaker { BreakerId = "CB-22", Name = "22kV Outgoer 1", State = BreakerState.Closed, CurrentAmps = 580, ActivePowerMw = 21.0, ReactivePowerMvar = 4.1 });
        }

        /// <summary>
        /// Resolves the standard IEC 61850 display color hex for a given voltage level.
        /// </summary>
        public static string GetVoltageColorHex(VoltageLevel voltage)
        {
            return voltage switch
            {
                VoltageLevel.UHV_500kV => "#EF4444", // Red
                VoltageLevel.EHV_220kV => "#A855F7", // Purple
                VoltageLevel.HV_110kV => "#F97316",  // Orange
                VoltageLevel.MV_22kV => "#22C55E",   // Green
                VoltageLevel.LV_400V => "#38BDF8",   // Light Blue
                _ => "#94A3B8"
            };
        }

        /// <summary>
        /// Calculates apparent power in MVA from active (MW) and reactive (MVAr) components.
        /// Formula: S = sqrt(P^2 + Q^2)
        /// </summary>
        public static double CalculateApparentPowerMva(double activeMw, double reactiveMvar)
        {
            return Math.Sqrt(activeMw * activeMw + reactiveMvar * reactiveMvar);
        }

        /// <summary>
        /// Calculates 3-phase line current in Amperes given MVA and line-to-line kV.
        /// Formula: I (Amps) = (MVA * 1000) / (sqrt(3) * kV)
        /// </summary>
        public static double CalculateThreePhaseCurrentAmps(double apparentMva, double lineKv)
        {
            if (lineKv <= 0.0) return 0.0;
            return (apparentMva * 1000.0) / (Math.Sqrt(3.0) * lineKv);
        }
    }
}
