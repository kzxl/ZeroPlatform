using System;

namespace ZeroUI.Core.Bms
{
    /// <summary>
    /// Represents the overall operating state of an Air Handling Unit (AHU).
    /// </summary>
    public enum AhuOperatingMode
    {
        Off,
        Ventilation,
        Cooling,
        Heating,
        Economizer,
        Purge
    }

    /// <summary>
    /// Operating health and alert status of an AHU mechanical component.
    /// </summary>
    public enum AhuComponentStatus
    {
        Normal,
        Warning,
        Alarm,
        Maintenance
    }

    /// <summary>
    /// Represents damper positions (0.0 to 100.0%) for air mixing chambers.
    /// </summary>
    public class AhuDamperState
    {
        public double OutsideAirPct { get; set; } = 20.0;
        public double ReturnAirPct { get; set; } = 80.0;
        public double ExhaustAirPct { get; set; } = 20.0;
        public AhuComponentStatus Status { get; set; } = AhuComponentStatus.Normal;
    }

    /// <summary>
    /// Filter differential pressure and cleanliness state.
    /// </summary>
    public class AhuFilterState
    {
        public double PreFilterDeltaP { get; set; } = 0.25; // in. wg
        public double FinalFilterDeltaP { get; set; } = 0.45; // in. wg
        public double MaxAllowedDeltaP { get; set; } = 1.0; // in. wg
        public bool IsDirty => PreFilterDeltaP > MaxAllowedDeltaP || FinalFilterDeltaP > MaxAllowedDeltaP;
        public AhuComponentStatus Status => IsDirty ? AhuComponentStatus.Warning : AhuComponentStatus.Normal;
    }

    /// <summary>
    /// Heating and cooling coil hydronic valve positions and safety freeze stat.
    /// </summary>
    public class AhuCoilState
    {
        public double HeatingValvePct { get; set; } = 0.0;
        public double CoolingValvePct { get; set; } = 45.0;
        public bool FreezeStatTripped { get; set; } = false;
        public AhuComponentStatus Status => FreezeStatTripped ? AhuComponentStatus.Alarm : AhuComponentStatus.Normal;
    }

    /// <summary>
    /// Supply and return air fan states, static pressure, and motor speed.
    /// </summary>
    public class AhuFanState
    {
        public bool IsSupplyFanRunning { get; set; } = true;
        public double SupplyFanRpm { get; set; } = 1450.0;
        public double SupplyStaticPressureInWg { get; set; } = 1.5; // in. wg
        public bool IsReturnFanRunning { get; set; } = true;
        public double ReturnFanRpm { get; set; } = 1100.0;
        public AhuComponentStatus Status { get; set; } = AhuComponentStatus.Normal;
    }

    /// <summary>
    /// Temperature, humidity, and airflow measurements across the AHU airstream.
    /// </summary>
    public class AhuAirState
    {
        public double OutsideTempC { get; set; } = 32.0;
        public double ReturnTempC { get; set; } = 24.0;
        public double MixedTempC { get; set; } = 25.6;
        public double SupplyTempC { get; set; } = 14.5;
        public double AirflowCfm { get; set; } = 12500.0;
    }

    /// <summary>
    /// Pure computation engine for Air Handling Unit thermodynamics, mixing, and economizer logic.
    /// </summary>
    public class AhuEngine
    {
        public AhuOperatingMode Mode { get; set; } = AhuOperatingMode.Cooling;
        public AhuDamperState Dampers { get; set; } = new AhuDamperState();
        public AhuFilterState Filters { get; set; } = new AhuFilterState();
        public AhuCoilState Coils { get; set; } = new AhuCoilState();
        public AhuFanState Fans { get; set; } = new AhuFanState();
        public AhuAirState Air { get; set; } = new AhuAirState();

        /// <summary>
        /// Calculates the ideal mixed air temperature (°C) based on outside air damper percentage.
        /// Formula: T_ma = (T_oa * %_oa + T_ra * %_ra) / 100
        /// </summary>
        public static double CalculateMixedAirTemp(double outsideTempC, double returnTempC, double outsideAirPct)
        {
            outsideAirPct = Math.Max(0.0, Math.Min(100.0, outsideAirPct));
            double returnAirPct = 100.0 - outsideAirPct;
            return (outsideTempC * outsideAirPct + returnTempC * returnAirPct) / 100.0;
        }

        /// <summary>
        /// Calculates sensible heating or cooling thermal duty in BTU/hr.
        /// Formula: Q (BTU/hr) = 1.08 * CFM * (T_mixed_F - T_supply_F)
        /// </summary>
        public static double CalculateSensibleThermalDutyBtu(double airflowCfm, double deltaTempF)
        {
            return 1.08 * Math.Max(0.0, airflowCfm) * Math.Abs(deltaTempF);
        }

        /// <summary>
        /// Evaluates whether airside free cooling (economizer) is thermodynamically favorable.
        /// Economizer is feasible when outside air temperature is cooler than return air and above freeze limit.
        /// </summary>
        public static bool EvaluateEconomizerFeasible(double outsideTempC, double returnTempC, double minEconomizerTempC = 10.0)
        {
            return outsideTempC >= minEconomizerTempC && outsideTempC < (returnTempC - 2.0);
        }

        /// <summary>
        /// Converts Celsius to Fahrenheit.
        /// </summary>
        public static double CelsiusToFahrenheit(double celsius)
        {
            return (celsius * 9.0 / 5.0) + 32.0;
        }

        /// <summary>
        /// Converts Fahrenheit to Celsius.
        /// </summary>
        public static double FahrenheitToCelsius(double fahrenheit)
        {
            return (fahrenheit - 32.0) * 5.0 / 9.0;
        }
    }
}
