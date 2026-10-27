using System;
using System.Collections.Generic;

namespace ZeroUI.Core.LifeSciences
{
    /// <summary>
    /// Cold storage regulatory tier for pharmaceuticals, vaccines, and biologics.
    /// </summary>
    public enum StorageTier
    {
        Refrigerated_2_8C, // +2°C to +8°C standard vaccines & antibodies
        Freezer_M20C,      // -20°C enzyme & plasma storage
        UltraLow_M80C,     // -80°C mRNA & viral vectors
        Cryogenic_M150C    // -150°C to -196°C stem cells & liquid nitrogen vapor
    }

    /// <summary>
    /// Regulatory and operational cold chain storage telemetry.
    /// </summary>
    public class ColdChainTelemetry
    {
        public string UnitTag { get; set; } = "ULT-FREEZER-04";
        public StorageTier Tier { get; set; } = StorageTier.UltraLow_M80C;
        public double CurrentTempC { get; set; } = -81.4;
        public double SetpointTempC { get; set; } = -80.0;
        public double HighAlarmLimitC { get; set; } = -70.0;
        public double LowAlarmLimitC { get; set; } = -88.0;
        public bool IsDoorOpen { get; set; } = false;
        public int DoorOpenCountToday { get; set; } = 4;
        public double BackupBatteryPct { get; set; } = 98.5;
        public bool IsLn2BackupArmed { get; set; } = true;
        public double ExcursionMinutesToday { get; set; } = 0.0;
        public List<double> TempHistory { get; } = new List<double>();

        public ColdChainTelemetry()
        {
            // Seed 40 historical temperature points oscillating tightly around -81.4°C
            for (int i = 0; i < 40; i++)
            {
                TempHistory.Add(-81.4 + Math.Sin(i * 0.4) * 0.8);
            }
        }
    }

    /// <summary>
    /// Pure computation engine for Cold Chain Mean Kinetic Temperature (MKT) and regulatory compliance.
    /// </summary>
    public class ColdChainEngine
    {
        public ColdChainTelemetry Telemetry { get; } = new ColdChainTelemetry();

        /// <summary>
        /// Universal gas constant R in J / (mol * K)
        /// </summary>
        public const double R = 8.314472;

        /// <summary>
        /// Standard activation energy delta H in J / mol for pharmaceutical thermal degradation (USP &lt;1079&gt; standard: 83.144 kJ/mol).
        /// </summary>
        public const double DefaultDeltaH = 83144.0;

        /// <summary>
        /// Calculates Mean Kinetic Temperature (MKT) in Celsius according to USP &lt;1079&gt; / FDA guidelines:
        /// T_mkt = (DeltaH / R) / -ln( (1/n) * sum( exp(-DeltaH / (R * T_k)) ) ) - 273.15
        /// </summary>
        public static double CalculateMeanKineticTemperature(IReadOnlyList<double> tempsC, double deltaH = DefaultDeltaH)
        {
            if (tempsC == null || tempsC.Count == 0) return 0.0;

            double sumExp = 0.0;
            int count = tempsC.Count;

            for (int i = 0; i < count; i++)
            {
                double tk = tempsC[i] + 273.15; // Convert °C to Kelvin
                if (tk <= 0.0) tk = 0.01;
                double exponent = -deltaH / (R * tk);
                sumExp += Math.Exp(exponent);
            }

            double meanExp = sumExp / count;
            if (meanExp <= 0.0) return tempsC[0];

            double mktKelvin = (deltaH / R) / (-Math.Log(meanExp));
            return mktKelvin - 273.15; // Convert back to °C
        }

        /// <summary>
        /// Evaluates whether the current temperature violates safety excursion thresholds.
        /// </summary>
        public bool IsInExcursion => Telemetry.CurrentTempC > Telemetry.HighAlarmLimitC ||
                                     Telemetry.CurrentTempC < Telemetry.LowAlarmLimitC;
    }
}
