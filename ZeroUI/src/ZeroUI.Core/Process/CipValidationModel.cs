using System;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// Sequential stages of a validated Clean-in-Place (CIP) and Steam-in-Place (SIP) cycle.
    /// </summary>
    public enum CipCyclePhase
    {
        PreRinse,
        CausticWash,
        IntermediateRinse,
        AcidWash,
        FinalWfiRinse,
        SipSterilization,
        CoolDownAirBlow,
        CycleComplete
    }

    /// <summary>
    /// 4 TACT validation criteria (Time, Action, Chemical, Temperature) per FDA/ISPE Baseline Guide.
    /// </summary>
    public class TactStatus
    {
        // Time
        public double ElapsedSec { get; set; } = 420.0;
        public double TargetDurationSec { get; set; } = 900.0;
        public bool IsTimeValid => ElapsedSec >= TargetDurationSec;

        // Action (Fluid Scouring & Velocity)
        public double FlowVelocityMS { get; set; } = 1.85; // m/s (minimum 1.5 m/s required for turbulent scouring)
        public double TargetVelocityMS { get; set; } = 1.50;
        public bool IsActionValid => FlowVelocityMS >= TargetVelocityMS;

        // Chemical Concentration (Conductivity)
        public string ChemicalName { get; set; } = "1.5% Sodium Hydroxide (NaOH)";
        public double ConductivityMsCm { get; set; } = 46.2; // mS/cm
        public double TargetConductivityMsCm { get; set; } = 42.0;
        public bool IsChemicalValid => ConductivityMsCm >= TargetConductivityMsCm;

        // Temperature (Lowest return loop temp must meet target)
        public double SupplyTempC { get; set; } = 83.5;
        public double ReturnTempC { get; set; } = 81.2;
        public double TargetTempC { get; set; } = 80.0;
        public bool IsTemperatureValid => ReturnTempC >= TargetTempC;

        public bool AreAllTactCriteriaMet => IsActionValid && IsChemicalValid && IsTemperatureValid;
    }

    /// <summary>
    /// Pure computation and validation engine for CIP/SIP cycles and FDA 21 CFR Part 11
    /// thermal sterilization equivalent lethality (F0 value) integration.
    /// </summary>
    public class CipValidationEngine
    {
        public string SkidTag { get; set; } = "CIP-SKID-02";
        public string CircuitTag { get; set; } = "FERM-301-PRODUCT-LOOP";
        public CipCyclePhase CurrentPhase { get; set; } = CipCyclePhase.CausticWash;

        public TactStatus Tact { get; set; } = new TactStatus();

        // SIP Sterilization Lethality (F0 Accumulator per USP <1211>)
        public double AccumulatedF0Minutes { get; set; } = 0.0;
        public double TargetF0Minutes { get; set; } = 15.0; // 15.0 minutes required for SAL 10^-6
        public double SteamPressureBar { get; set; } = 2.1; // Gauge steam pressure (bar)

        /// <summary>
        /// Calculates the equivalent lethality rate L at temperature T:
        /// L = 10^((T - 121.1) / z), where standard z-value for Geobacillus stearothermophilus is 10.0°C.
        /// </summary>
        public static double CalculateLethalityRate(double tempC, double zValueC = 10.0)
        {
            if (tempC < 100.0) return 0.0;
            return Math.Pow(10.0, (tempC - 121.1) / zValueC);
        }

        public bool IsSterilizationValidated => AccumulatedF0Minutes >= TargetF0Minutes;

        /// <summary>
        /// Advances CIP/SIP execution clock, accumulates TACT duration, and integrates F0 lethality during SIP.
        /// </summary>
        public void AdvanceTime(double deltaSeconds)
        {
            Tact.ElapsedSec += deltaSeconds;

            // In SIP Sterilization phase, integrate thermal death time lethality
            if (CurrentPhase == CipCyclePhase.SipSterilization && Tact.ReturnTempC >= 100.0)
            {
                double rate = CalculateLethalityRate(Tact.ReturnTempC);
                // Delta F0 in minutes = (deltaSeconds / 60.0) * Lethality Rate
                AccumulatedF0Minutes += (deltaSeconds / 60.0) * rate;
            }
        }

        /// <summary>
        /// Advances to next phase in the CIP/SIP validated recipe.
        /// </summary>
        public void AdvanceToNextPhase()
        {
            CurrentPhase = CurrentPhase switch
            {
                CipCyclePhase.PreRinse => CipCyclePhase.CausticWash,
                CipCyclePhase.CausticWash => CipCyclePhase.IntermediateRinse,
                CipCyclePhase.IntermediateRinse => CipCyclePhase.AcidWash,
                CipCyclePhase.AcidWash => CipCyclePhase.FinalWfiRinse,
                CipCyclePhase.FinalWfiRinse => CipCyclePhase.SipSterilization,
                CipCyclePhase.SipSterilization => CipCyclePhase.CoolDownAirBlow,
                _ => CipCyclePhase.CycleComplete
            };

            Tact.ElapsedSec = 0.0;

            // Configure target temperature for phase
            switch (CurrentPhase)
            {
                case CipCyclePhase.PreRinse:
                case CipCyclePhase.IntermediateRinse:
                case CipCyclePhase.FinalWfiRinse:
                    Tact.TargetTempC = 25.0;
                    Tact.SupplyTempC = 26.0;
                    Tact.ReturnTempC = 25.5;
                    Tact.ChemicalName = "Purified Water (PW / WFI)";
                    Tact.ConductivityMsCm = 1.2;
                    Tact.TargetConductivityMsCm = 0.5;
                    break;
                case CipCyclePhase.CausticWash:
                    Tact.TargetTempC = 80.0;
                    Tact.SupplyTempC = 83.5;
                    Tact.ReturnTempC = 81.2;
                    Tact.ChemicalName = "1.5% NaOH Caustic";
                    Tact.ConductivityMsCm = 46.2;
                    Tact.TargetConductivityMsCm = 42.0;
                    break;
                case CipCyclePhase.AcidWash:
                    Tact.TargetTempC = 65.0;
                    Tact.SupplyTempC = 68.0;
                    Tact.ReturnTempC = 66.5;
                    Tact.ChemicalName = "1.0% Nitric Acid (HNO3)";
                    Tact.ConductivityMsCm = 28.5;
                    Tact.TargetConductivityMsCm = 25.0;
                    break;
                case CipCyclePhase.SipSterilization:
                    Tact.TargetTempC = 121.1;
                    Tact.SupplyTempC = 122.5;
                    Tact.ReturnTempC = 121.8;
                    Tact.ChemicalName = "Pure Clean Steam";
                    break;
            }
        }
    }
}
