using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum PsuHealthStatus
    {
        Normal,
        Warning,
        Fault,
        Absent
    }

    public enum FanHealthStatus
    {
        Normal,
        Warning,
        Failed
    }

    public enum OpticalDdmStatus
    {
        Normal,
        Warning,
        Alarm
    }

    public enum ChassisOverallHealth
    {
        Healthy,
        Degraded,
        Critical
    }

    public class PsuStatus
    {
        public int PsuIndex { get; set; } = 1;
        public bool IsPresent { get; set; } = true;
        public bool IsPowered { get; set; } = true;
        public double InputVoltageVolts { get; set; } = 230.0;
        public double OutputCurrentAmps { get; set; } = 1.2;
        public double PowerWatts { get; set; } = 275.0;
        public double TemperatureCelsius { get; set; } = 38.0;
        public PsuHealthStatus Status { get; set; } = PsuHealthStatus.Normal;
    }

    public class FanStatus
    {
        public int FanIndex { get; set; } = 1;
        public string Name { get; set; } = "Fan 1";
        public int CurrentRpm { get; set; } = 7200;
        public int MaxRpm { get; set; } = 12000;
        public double DutyCyclePercent { get; set; } = 60.0;
        public FanHealthStatus Status { get; set; } = FanHealthStatus.Normal;

        public double RpmRatio => MaxRpm > 0 ? Math.Min(1.0, (double)CurrentRpm / MaxRpm) : 0;
    }

    public class SfpDdmMetric
    {
        public string PortName { get; set; } = "SFP+ 1";
        public double TxPowerDbm { get; set; } = -2.5;
        public double RxPowerDbm { get; set; } = -6.8;
        public double TransceiverTempCelsius { get; set; } = 41.5;
        public double VoltageVolts { get; set; } = 3.31;
        public double TxBiasCurrentMa { get; set; } = 22.0;
        public OpticalDdmStatus Status { get; set; } = OpticalDdmStatus.Normal;

        public bool IsRxOpticalSignalAcceptable => RxPowerDbm >= -18.0 && RxPowerDbm <= -1.0;
    }

    /// <summary>
    /// Telemetry profile and evaluation logic for physical hardware chassis operating health.
    /// Manages dual redundant PSUs, fan tachometers, and SFP digital optical diagnostics.
    /// </summary>
    public class ChassisHealthProfile
    {
        public string DeviceName { get; set; } = "CoreSwitch-01";
        public string Model { get; set; } = "ZeroSwitch 48G-4X";
        public string SerialNumber { get; set; } = "ZS-8942-A0";
        public TimeSpan Uptime { get; set; } = TimeSpan.FromDays(42);

        public PsuStatus Psu1 { get; set; } = new PsuStatus { PsuIndex = 1, PowerWatts = 220 };
        public PsuStatus Psu2 { get; set; } = new PsuStatus { PsuIndex = 2, PowerWatts = 210 };

        public List<FanStatus> Fans { get; } = new List<FanStatus>();
        public List<SfpDdmMetric> OpticalTransceivers { get; } = new List<SfpDdmMetric>();

        public ChassisHealthProfile()
        {
            Fans.Add(new FanStatus { FanIndex = 1, Name = "Fan Tray 1", CurrentRpm = 7200 });
            Fans.Add(new FanStatus { FanIndex = 2, Name = "Fan Tray 2", CurrentRpm = 7150 });
            Fans.Add(new FanStatus { FanIndex = 3, Name = "Fan Tray 3", CurrentRpm = 7300 });

            OpticalTransceivers.Add(new SfpDdmMetric { PortName = "Uplink SFP+ 1", TxPowerDbm = -2.1, RxPowerDbm = -5.4 });
            OpticalTransceivers.Add(new SfpDdmMetric { PortName = "Uplink SFP+ 2", TxPowerDbm = -2.3, RxPowerDbm = -5.8 });
        }

        public bool IsPowerRedundant => Psu1.IsPowered && Psu2.IsPowered && 
                                        Psu1.Status == PsuHealthStatus.Normal && 
                                        Psu2.Status == PsuHealthStatus.Normal;

        public ChassisOverallHealth EvaluateOverallHealth()
        {
            if (Psu1.Status == PsuHealthStatus.Fault || Psu2.Status == PsuHealthStatus.Fault)
                return ChassisOverallHealth.Critical;

            for (int i = 0; i < Fans.Count; i++)
            {
                if (Fans[i].Status == FanHealthStatus.Failed)
                    return ChassisOverallHealth.Critical;
            }

            for (int i = 0; i < OpticalTransceivers.Count; i++)
            {
                if (OpticalTransceivers[i].Status == OpticalDdmStatus.Alarm)
                    return ChassisOverallHealth.Critical;
            }

            // Degraded conditions (single PSU loss, warning on fans or optics)
            if (!IsPowerRedundant)
                return ChassisOverallHealth.Degraded;

            for (int i = 0; i < Fans.Count; i++)
            {
                if (Fans[i].Status == FanHealthStatus.Warning)
                    return ChassisOverallHealth.Degraded;
            }

            for (int i = 0; i < OpticalTransceivers.Count; i++)
            {
                if (OpticalTransceivers[i].Status == OpticalDdmStatus.Warning)
                    return ChassisOverallHealth.Degraded;
            }

            return ChassisOverallHealth.Healthy;
        }
    }
}
