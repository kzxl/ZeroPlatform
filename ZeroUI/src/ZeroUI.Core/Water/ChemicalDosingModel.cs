using System;

namespace ZeroUI.Core.Water
{
    /// <summary>
    /// Type of treatment chemical being metered.
    /// </summary>
    public enum ChemicalType
    {
        Coagulant,      // Polyaluminum Chloride (PAC), Alum, Ferric Chloride
        Disinfectant,   // Sodium Hypochlorite (NaOCl), Chlorine dioxide
        Polymer,        // Cationic/Anionic Flocculant
        PhAdjustment,   // Caustic Soda (NaOH), Lime, Sulfuric Acid (H2SO4)
        Fluoride        // Hydrofluorosilicic acid
    }

    /// <summary>
    /// Operating role and status of a metering pump.
    /// </summary>
    public enum DosingPumpMode
    {
        Duty,
        Standby,
        Maintenance,
        Fault
    }

    /// <summary>
    /// Diaphragm/solenoid positive displacement metering pump telemetry and control.
    /// </summary>
    public class DosingPump
    {
        public string PumpTag { get; set; } = "P-101A";
        public DosingPumpMode Mode { get; set; } = DosingPumpMode.Duty;
        public bool IsRunning { get; set; } = true;
        public double StrokeRateSpm { get; set; } = 110.0; // Strokes per minute (0 - 180 SPM)
        public double StrokeLengthPct { get; set; } = 85.0; // 0 - 100%
        public double MaxCapacityLh { get; set; } = 150.0; // Liters per hour at 100% stroke & frequency
        public double DischargePressureBar { get; set; } = 3.8; // bar
        public bool DiaphragmIntact { get; set; } = true; // False if leak detection sensor triggers

        /// <summary>
        /// Current volumetric delivery rate in Liters per Hour (L/h).
        /// </summary>
        public double ActualFlowRateLh
        {
            get
            {
                if (!IsRunning || Mode == DosingPumpMode.Fault || !DiaphragmIntact)
                    return 0.0;

                double freqFraction = Math.Max(0.0, Math.Min(1.0, StrokeRateSpm / 180.0));
                double strokeFraction = Math.Max(0.0, Math.Min(1.0, StrokeLengthPct / 100.0));
                return MaxCapacityLh * freqFraction * strokeFraction;
            }
        }
    }

    /// <summary>
    /// Bulk or day storage tank containing chemical solution.
    /// </summary>
    public class ChemicalTank
    {
        public string TankTag { get; set; } = "TK-101";
        public double CapacityL { get; set; } = 2500.0;
        public double CurrentLevelL { get; set; } = 1750.0;
        public double SpecificGravity { get; set; } = 1.22; // kg/L (e.g. 1.20 - 1.25 for 12% NaOCl)
        public double SolutionConcentrationPct { get; set; } = 12.5; // % active chemical (0 - 100%)
        public double LowLevelThresholdPct { get; set; } = 15.0;

        public double LevelPct => CapacityL > 0.0 ? (CurrentLevelL / CapacityL) * 100.0 : 0.0;
        public bool IsLowLevel => LevelPct <= LowLevelThresholdPct;
    }

    /// <summary>
    /// Pure computation engine for chemical feed pacing, flow-proportional dosing,
    /// duty/standby auto-switchover, and inventory depletion tracking.
    /// </summary>
    public class ChemicalDosingEngine
    {
        public ChemicalType Chemical { get; set; } = ChemicalType.Disinfectant;
        public double WaterFlowM3H { get; set; } = 850.0; // Main water flow rate in m3/h
        public double TargetDoseMgL { get; set; } = 2.5; // Target dose setpoint in mg/L (ppm)

        public DosingPump PumpA { get; set; } = new DosingPump { PumpTag = "P-101A", Mode = DosingPumpMode.Duty, IsRunning = true };
        public DosingPump PumpB { get; set; } = new DosingPump { PumpTag = "P-101B", Mode = DosingPumpMode.Standby, IsRunning = false };
        public ChemicalTank Tank { get; set; } = new ChemicalTank();

        /// <summary>
        /// Calculates the required chemical dosing solution flow rate in Liters per Hour (L/h).
        /// Equation: Q_chem = (Q_water [m3/h] * TargetDose [mg/L]) / (ConcentrationFraction * SpecificGravity * 1000)
        /// </summary>
        public double CalculateRequiredDosingRateLh()
        {
            if (WaterFlowM3H <= 0.0 || TargetDoseMgL <= 0.0 || Tank.SolutionConcentrationPct <= 0.0 || Tank.SpecificGravity <= 0.0)
                return 0.0;

            double concFraction = Tank.SolutionConcentrationPct / 100.0;
            // 1 mg/L = 1 g/m3. Total pure chemical (grams/h) = Q_water * Dose
            double pureChemicalGramsPerHour = WaterFlowM3H * TargetDoseMgL;
            // 1 Liter of solution weighs (SpecificGravity * 1000) grams and contains (concFraction * SpecificGravity * 1000) grams neat chemical
            double gramsActivePerLiter = concFraction * Tank.SpecificGravity * 1000.0;

            return pureChemicalGramsPerHour / gramsActivePerLiter;
        }

        /// <summary>
        /// Active volumetric delivery from all currently running pumps (L/h).
        /// </summary>
        public double TotalDeliveredDosingRateLh => PumpA.ActualFlowRateLh + PumpB.ActualFlowRateLh;

        /// <summary>
        /// Evaluates current pumps and triggers automatic failover from duty to standby pump
        /// if the duty pump trips, loses diaphragm integrity, or reports a fault.
        /// </summary>
        public bool CheckAndApplyAutoFailover()
        {
            if (PumpA.Mode == DosingPumpMode.Duty && (!PumpA.DiaphragmIntact || PumpA.Mode == DosingPumpMode.Fault))
            {
                PumpA.IsRunning = false;
                PumpA.Mode = DosingPumpMode.Fault;

                if (PumpB.Mode == DosingPumpMode.Standby && PumpB.DiaphragmIntact)
                {
                    PumpB.Mode = DosingPumpMode.Duty;
                    PumpB.IsRunning = true;
                    // Match required stroke frequency
                    AdjustPumpsToTargetRate();
                    return true;
                }
            }
            else if (PumpB.Mode == DosingPumpMode.Duty && (!PumpB.DiaphragmIntact || PumpB.Mode == DosingPumpMode.Fault))
            {
                PumpB.IsRunning = false;
                PumpB.Mode = DosingPumpMode.Fault;

                if (PumpA.Mode == DosingPumpMode.Standby && PumpA.DiaphragmIntact)
                {
                    PumpA.Mode = DosingPumpMode.Duty;
                    PumpA.IsRunning = true;
                    AdjustPumpsToTargetRate();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Automatically adjusts the active duty pump stroke frequency (SPM) to deliver the calculated required flow rate.
        /// </summary>
        public void AdjustPumpsToTargetRate()
        {
            double targetLh = CalculateRequiredDosingRateLh();
            DosingPump? dutyPump = PumpA.Mode == DosingPumpMode.Duty ? PumpA :
                                   PumpB.Mode == DosingPumpMode.Duty ? PumpB : null;

            if (dutyPump != null && dutyPump.IsRunning && dutyPump.DiaphragmIntact)
            {
                double strokeFrac = Math.Max(0.1, dutyPump.StrokeLengthPct / 100.0);
                double neededFraction = targetLh / (dutyPump.MaxCapacityLh * strokeFrac);
                double neededSpm = neededFraction * 180.0;
                dutyPump.StrokeRateSpm = Math.Max(0.0, Math.Min(180.0, neededSpm));
            }
        }

        /// <summary>
        /// Deducts chemical consumption from the storage tank over the elapsed time step.
        /// </summary>
        public void AdvanceConsumption(double deltaSeconds)
        {
            double deliveredLh = TotalDeliveredDosingRateLh;
            if (deliveredLh <= 0.0) return;

            double litersUsed = (deliveredLh / 3600.0) * deltaSeconds;
            Tank.CurrentLevelL = Math.Max(0.0, Tank.CurrentLevelL - litersUsed);
        }

        /// <summary>
        /// Estimated hours of chemical storage remaining at the current dosing rate.
        /// </summary>
        public double EstimatedRunHoursRemaining
        {
            get
            {
                double rate = TotalDeliveredDosingRateLh;
                if (rate <= 0.0) return double.PositiveInfinity;
                return Tank.CurrentLevelL / rate;
            }
        }
    }
}
