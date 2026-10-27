using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroSystem;

#region Telemetry Data Models

/// <summary>
/// Comprehensive system and hardware telemetry snapshot captured at a single instant.
/// Features a unique, time-ordered 128-bit <see cref="Uuid7"/> SnapshotId.
/// </summary>
public sealed record SystemDiagnosticsSnapshot(
    DateTime TimestampUtc,
    TimeSpan SystemUptime,
    string MachineName,
    string OsDescription,
    bool Is64BitOperatingSystem,
    CpuInfo Cpu,
    MemoryInfo Memory,
    IReadOnlyList<GpuAdapterInfo> Gpus,
    IReadOnlyList<DriveInfoSnapshot> Storage,
    IReadOnlyList<NetworkAdapterInfo> Network,
    Uuid7 SnapshotId = default)
{
    public Uuid7 SnapshotId { get; init; } = SnapshotId == default ? Uuid7.NewUuid() : SnapshotId;
}

public sealed record CpuInfo(
    string ModelName,
    int LogicalProcessorCount,
    int PhysicalCoreCount,
    float CpuUsagePercent);

public sealed record MemoryInfo(
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes,
    ulong UsedPhysicalBytes,
    uint MemoryLoadPercent,
    ulong TotalPageFileBytes,
    ulong AvailablePageFileBytes);

public sealed record GpuAdapterInfo(
    string AdapterName,
    ulong DedicatedVideoMemoryBytes,
    ulong DedicatedSystemMemoryBytes,
    ulong SharedSystemMemoryBytes,
    uint VendorId,
    uint DeviceId);

public sealed record DriveInfoSnapshot(
    string DriveLetter,
    string VolumeLabel,
    string FileSystemName,
    DriveType DriveType,
    ulong TotalBytes,
    ulong FreeBytes,
    float FreePercent);

public sealed record NetworkAdapterInfo(
    string Id,
    string Name,
    string Description,
    string MacAddress,
    OperationalStatus Status,
    long SpeedBitsPerSecond,
    IReadOnlyList<string> IpAddresses);

#endregion

/// <summary>
/// Sovereign high-performance native Windows hardware telemetry and diagnostics engine.
/// Queries Win32 APIs, DXGI, and Registry with zero external dependencies and sub-millisecond execution.
/// Partitioned into partial classes by hardware subsystem (CPU, GPU, Storage, Network).
/// </summary>
public static partial class HardwareTelemetry
{
    /// <summary>
    /// Captures a complete hardware, memory, GPU, disk, and network diagnostic snapshot.
    /// </summary>
    public static SystemDiagnosticsSnapshot GetSnapshot()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return new SystemDiagnosticsSnapshot(
            TimestampUtc: DateTime.UtcNow,
            SystemUptime: uptime,
            MachineName: Environment.MachineName,
            OsDescription: RuntimeInformation.OSDescription,
            Is64BitOperatingSystem: Environment.Is64BitOperatingSystem,
            Cpu: GetCpuInfo(),
            Memory: GetMemoryInfo(),
            Gpus: GetGpuAdapters(),
            Storage: GetStorageDrives(),
            Network: GetNetworkAdapters(),
            SnapshotId: Uuid7.NewUuid());
    }

    /// <summary>
    /// Gets physical and virtual memory statistics via GlobalMemoryStatusEx.
    /// </summary>
    public static MemoryInfo GetMemoryInfo()
    {
        var mem = MemoryKernel.GetMemoryStatus();
        ulong used = mem.TotalPhysicalBytes >= mem.AvailablePhysicalBytes
            ? mem.TotalPhysicalBytes - mem.AvailablePhysicalBytes
            : 0;

        return new MemoryInfo(
            TotalPhysicalBytes: mem.TotalPhysicalBytes,
            AvailablePhysicalBytes: mem.AvailablePhysicalBytes,
            UsedPhysicalBytes: used,
            MemoryLoadPercent: mem.MemoryLoadPercentage,
            TotalPageFileBytes: mem.TotalPageFileBytes,
            AvailablePageFileBytes: mem.AvailablePageFileBytes);
    }
}
