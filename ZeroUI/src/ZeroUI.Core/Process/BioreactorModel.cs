using System;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// Operating biological process phase of a sanitary fermentation vessel or single-use bioreactor.
    /// </summary>
    public enum BioreactorPhase
    {
        Inoculation,
        ExponentialGrowth,
        StationaryFeed,
        Harvest,
        SterilizationSIP,
        CleaningCIP
    }

    /// <summary>
    /// Operating health and alert status of critical bioreactor environmental parameters.
    /// </summary>
    public enum BioreactorAlarmStatus
    {
        Normal,
        Warning,
        OutOfSpecAlarm
    }

    /// <summary>
    /// Pure computation engine and telemetry model for sanitary bioreactor cell culture,
    /// agitation kinematics, dissolved oxygen (DO %) cascade, pH regulation, and thermal jacket balance.
    /// </summary>
    public class BioreactorEngine
    {
        public string VesselTag { get; set; } = "BR-301";
        public BioreactorPhase Phase { get; set; } = BioreactorPhase.ExponentialGrowth;

        // Vessel Physical Geometry
        public double WorkingVolumeL { get; set; } = 350.0;
        public double TotalCapacityL { get; set; } = 500.0;

        // Agitation & Hydrodynamics
        public double AgitationRpm { get; set; } = 420.0; // 0 - 1200 RPM
        public double ImpellerAngleDeg { get; set; } = 0.0; // 0 - 360 degrees
        public bool IsAgitatorRunning { get; set; } = true;

        // Dissolved Oxygen (DO) & Sparging
        public double DissolvedOxygenPct { get; set; } = 45.0; // Target typically 40 - 50%
        public double TargetDoPct { get; set; } = 45.0;
        public double SpargingAirLpm { get; set; } = 18.0; // Liters per minute
        public double SpargingO2Lpm { get; set; } = 3.5; // O2 enrichment flow

        // pH Chemistry Regulation
        public double PhValue { get; set; } = 7.02; // Physiological pH 6.9 - 7.2
        public double TargetPh { get; set; } = 7.00;
        public bool AcidPumpActive { get; set; } = false;
        public bool BasePumpActive { get; set; } = false;

        // Thermal Regulation & Jacket
        public double VesselTempC { get; set; } = 37.0; // 37.0°C mammalian cell culture
        public double TargetTempC { get; set; } = 37.0;
        public double JacketTempC { get; set; } = 37.2;

        // Foam Detection & Antifoam
        public bool FoamDetected { get; set; } = false;
        public bool AntifoamPumpActive { get; set; } = false;

        public double VolumeFillPct => TotalCapacityL > 0.0 ? (WorkingVolumeL / TotalCapacityL) * 100.0 : 0.0;

        /// <summary>
        /// Volumetric oxygen mass transfer coefficient kLa (hr^-1) estimated from agitation and sparging velocity.
        /// </summary>
        public double EstimatedKLaHr
        {
            get
            {
                if (!IsAgitatorRunning || AgitationRpm <= 0.0) return 2.0;
                // Empirical correlation proportional to RPM^1.2 and sparge flow^0.5
                double pTerm = Math.Pow(AgitationRpm / 300.0, 1.2);
                double vTerm = Math.Pow(Math.Max(0.1, (SpargingAirLpm + SpargingO2Lpm) / 10.0), 0.5);
                return Math.Round(25.0 * pTerm * vTerm, 1);
            }
        }

        public BioreactorAlarmStatus AlarmStatus
        {
            get
            {
                if (Math.Abs(VesselTempC - TargetTempC) > 1.5 ||
                    Math.Abs(PhValue - TargetPh) > 0.4 ||
                    DissolvedOxygenPct < 20.0)
                {
                    return BioreactorAlarmStatus.OutOfSpecAlarm;
                }

                if (Math.Abs(VesselTempC - TargetTempC) > 0.5 ||
                    Math.Abs(PhValue - TargetPh) > 0.15 ||
                    DissolvedOxygenPct < 30.0 ||
                    FoamDetected)
                {
                    return BioreactorAlarmStatus.Warning;
                }

                return BioreactorAlarmStatus.Normal;
            }
        }

        /// <summary>
        /// Advances simulation kinematics (impeller rotation and micro-drift) over delta seconds.
        /// </summary>
        public void AdvanceSimulation(double deltaSeconds)
        {
            if (IsAgitatorRunning && AgitationRpm > 0.0)
            {
                double degPerSec = (AgitationRpm * 360.0) / 60.0;
                ImpellerAngleDeg = (ImpellerAngleDeg + degPerSec * deltaSeconds) % 360.0;
            }

            // Foam pump auto-clear
            if (FoamDetected && AntifoamPumpActive)
            {
                FoamDetected = false;
                AntifoamPumpActive = false;
            }
        }
    }
}
