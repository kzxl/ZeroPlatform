using System;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

#region Battery Data Models

/// <summary>
/// Power line source connection status.
/// </summary>
public enum PowerSourceStatus
{
    Offline = 0, // Battery
    Online = 1,  // AC Power
    Unknown = 255
}

/// <summary>
/// Status flags of the system battery.
/// </summary>
[Flags]
public enum BatteryStatusFlags : byte
{
    High = 1,
    Low = 2,
    Critical = 4,
    Charging = 8,
    NoSystemBattery = 128,
    Unknown = 255
}

/// <summary>
/// Real-time snapshot of system power and battery capacity.
/// </summary>
public sealed record BatterySnapshot(
    PowerSourceStatus PowerSource,
    BatteryStatusFlags StatusFlags,
    byte BatteryLifePercent,
    TimeSpan? EstimatedRemainingTime,
    TimeSpan? EstimatedFullLifeTime,
    bool IsBatteryPresent,
    bool IsCharging);

#endregion

/// <summary>
/// Sovereign battery and power status telemetry engine.
/// Queries Win32 power management subsystem with sub-millisecond execution.
/// </summary>
public static class BatteryTelemetry
{
    /// <summary>
    /// Captures current system power and battery status.
    /// </summary>
    public static BatterySnapshot GetStatus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new BatterySnapshot(
                PowerSource: PowerSourceStatus.Online,
                StatusFlags: BatteryStatusFlags.NoSystemBattery,
                BatteryLifePercent: 100,
                EstimatedRemainingTime: null,
                EstimatedFullLifeTime: null,
                IsBatteryPresent: false,
                IsCharging: false);
        }

        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            return new BatterySnapshot(
                PowerSource: PowerSourceStatus.Unknown,
                StatusFlags: BatteryStatusFlags.Unknown,
                BatteryLifePercent: 255,
                EstimatedRemainingTime: null,
                EstimatedFullLifeTime: null,
                IsBatteryPresent: false,
                IsCharging: false);
        }

        var source = status.ACLineStatus switch
        {
            0 => PowerSourceStatus.Offline,
            1 => PowerSourceStatus.Online,
            _ => PowerSourceStatus.Unknown
        };

        var flags = (BatteryStatusFlags)status.BatteryFlag;
        bool hasNoBattery = (flags & BatteryStatusFlags.NoSystemBattery) != 0 || status.BatteryFlag == 128;
        bool isCharging = (flags & BatteryStatusFlags.Charging) != 0;

        TimeSpan? remTime = status.BatteryLifeTime > 0
            ? TimeSpan.FromSeconds(status.BatteryLifeTime)
            : null;

        TimeSpan? fullTime = status.BatteryFullLifeTime > 0
            ? TimeSpan.FromSeconds(status.BatteryFullLifeTime)
            : null;

        return new BatterySnapshot(
            PowerSource: source,
            StatusFlags: flags,
            BatteryLifePercent: status.BatteryLifePercent,
            EstimatedRemainingTime: remTime,
            EstimatedFullLifeTime: fullTime,
            IsBatteryPresent: !hasNoBattery,
            IsCharging: isCharging);
    }

    /// <summary>
    /// Checks whether the machine is currently running on battery power (unplugged from AC).
    /// </summary>
    public static bool IsRunningOnBattery()
    {
        var status = GetStatus();
        return status.PowerSource == PowerSourceStatus.Offline && status.IsBatteryPresent;
    }

    /// <summary>
    /// Checks whether the battery is actively charging.
    /// </summary>
    public static bool IsCharging()
    {
        return GetStatus().IsCharging;
    }
}
