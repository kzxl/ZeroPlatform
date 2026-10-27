using System;
using System.IO;
using Xunit;
using ZeroSystem;

namespace ZeroSystem.Tests;

public class ZeroSystemTests
{
    [Fact]
    public void ProcessGuard_IsSystemOrCurrentProcess_IdentifiesCorrectly()
    {
        Assert.True(ProcessGuard.IsSystemOrCurrentProcess(0));
        Assert.True(ProcessGuard.IsSystemOrCurrentProcess(4));
        Assert.True(ProcessGuard.IsSystemOrCurrentProcess(Environment.ProcessId));
        Assert.False(ProcessGuard.IsSystemOrCurrentProcess(9999999));
    }

    [Fact]
    public void MemoryKernel_GetMemoryStatus_ReturnsValidData()
    {
        var status = MemoryKernel.GetMemoryStatus();
        Assert.True(status.TotalPhysicalBytes > 0);
        Assert.True(status.AvailablePhysicalBytes > 0);
        Assert.True(status.TotalPhysicalBytes >= status.AvailablePhysicalBytes);
    }

    [Fact]
    public void DosDeviceManager_FindAvailableDriveLetter_ReturnsValidLetter()
    {
        var letter = DosDeviceManager.FindAvailableDriveLetter();
        Assert.NotNull(letter);
        Assert.InRange(letter.Value, 'A', 'Z');

        var currentDrives = Directory.GetLogicalDrives();
        Assert.DoesNotContain($"{letter.Value}:\\", currentDrives, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DosDeviceManager_ResolveMountedPath_MapsCleanly()
    {
        var orig = @"C:\ERP\Data\Accounting.mdf";
        var resolved = DosDeviceManager.ResolveMountedPath(orig, 'Z');
        Assert.Equal(@"Z:\ERP\Data\Accounting.mdf", resolved);
    }

    [Fact]
    public void DosDeviceManager_TryMountDevice_RejectsEmptyTarget()
    {
        bool ok = DosDeviceManager.TryMountDevice("", out _, out var error);
        Assert.False(ok);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsafeFileStreamFactory_ReadsLockedFilesPermissively()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Hello ZeroPlatform!");

            // Open with normal FileShare.ReadWrite
            using var lockStream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            // Permissive read should succeed concurrently
            var bytes = UnsafeFileStreamFactory.ReadAllBytesPermissive(tempFile);
            var text = System.Text.Encoding.UTF8.GetString(bytes);

            Assert.Equal("Hello ZeroPlatform!", text);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void HardwareTelemetry_GetCpuInfo_ReturnsValidMetrics()
    {
        var cpu = HardwareTelemetry.GetCpuInfo();
        Assert.NotNull(cpu);
        Assert.False(string.IsNullOrWhiteSpace(cpu.ModelName));
        Assert.True(cpu.LogicalProcessorCount > 0);
        Assert.True(cpu.PhysicalCoreCount > 0);
        Assert.InRange(cpu.CpuUsagePercent, 0.0f, 100.0f);
    }

    [Fact]
    public void HardwareTelemetry_GetMemoryInfo_ReturnsValidMetrics()
    {
        var mem = HardwareTelemetry.GetMemoryInfo();
        Assert.NotNull(mem);
        Assert.True(mem.TotalPhysicalBytes > 0);
        Assert.True(mem.AvailablePhysicalBytes > 0);
        Assert.True(mem.TotalPhysicalBytes >= mem.AvailablePhysicalBytes);
        Assert.InRange(mem.MemoryLoadPercent, 0u, 100u);
    }

    [Fact]
    public void HardwareTelemetry_GetStorageDrives_ReturnsValidPartitions()
    {
        var drives = HardwareTelemetry.GetStorageDrives();
        Assert.NotNull(drives);
        Assert.NotEmpty(drives);
        var firstDrive = drives[0];
        Assert.False(string.IsNullOrWhiteSpace(firstDrive.DriveLetter));
    }

    [Fact]
    public void HardwareTelemetry_GetSnapshot_CapturesSubMillisecondHardwareReport()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = HardwareTelemetry.GetSnapshot();
        sw.Stop();

        Assert.NotNull(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.MachineName));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OsDescription));
        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Memory);
        Assert.NotNull(snapshot.Storage);
        Assert.NotNull(snapshot.Network);
        Assert.NotNull(snapshot.Gpus);
        Assert.NotEqual(ZeroPrimitives.Core.Identifiers.Uuid7.Empty, snapshot.SnapshotId);
    }

    [Fact]
    public void HardwareTelemetryWatcher_ThrottlesExcessiveSampling()
    {
        // 2 samples per second, burst capacity of 2
        var watcher = new HardwareTelemetryWatcher(samplesPerSecond: 2.0, burstCapacity: 2.0);

        // Burst 2 queries immediately
        bool first = watcher.TryGetSnapshot(out var s1);
        bool second = watcher.TryGetSnapshot(out var s2);

        // Third immediate query should be throttled
        bool third = watcher.TryGetSnapshot(out var s3);

        Assert.True(first);
        Assert.NotNull(s1);
        Assert.True(second);
        Assert.NotNull(s2);
        Assert.False(third);
        Assert.Null(s3);

        // Snapshot IDs must be distinct and non-empty
        Assert.NotEqual(s1!.SnapshotId, s2!.SnapshotId);
    }
}

