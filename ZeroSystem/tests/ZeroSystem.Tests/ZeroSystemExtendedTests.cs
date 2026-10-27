using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using ZeroSystem;

namespace ZeroSystem.Tests;

public class ZeroSystemExtendedTests
{
    [Fact]
    public void FileLockManager_DetectsLockedFilesAndLockingProcesses()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"zsystem_test_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempFile, "Testing FileLockManager locks");

        try
        {
            // Initially unlocked
            Assert.False(FileLockManager.IsFileLocked(tempFile));
            var initialLockers = FileLockManager.GetLockingProcesses(tempFile);
            Assert.Empty(initialLockers);

            // Open with exclusive lock
            using (var fs = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.True(FileLockManager.IsFileLocked(tempFile));

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var lockers = FileLockManager.GetLockingProcesses(tempFile);
                    Assert.NotEmpty(lockers);
                    Assert.Contains(lockers, l => l.ProcessId == Environment.ProcessId);
                }
            }

            // Once closed, file is unlocked again
            Assert.False(FileLockManager.IsFileLocked(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void SmbiosReader_ReadsHardwareMetadataWithoutWmi()
    {
        var snapshot = SmbiosReader.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Bios);
        Assert.NotNull(snapshot.System);
        Assert.NotNull(snapshot.Board);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string uuid = SmbiosReader.GetSystemUuid();
            Assert.False(string.IsNullOrWhiteSpace(uuid));

            string boardSerial = SmbiosReader.GetMotherboardSerial();
            Assert.NotNull(boardSerial);
        }
    }

    [Fact]
    public void DisplayManager_EnumeratesActiveMonitorsAndDpi()
    {
        var monitors = DisplayManager.GetMonitors();
        Assert.NotNull(monitors);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.NotEmpty(monitors);

            var primary = DisplayManager.GetPrimaryMonitor();
            Assert.NotNull(primary);
            Assert.True(primary.IsPrimary);
            Assert.True(primary.Bounds.Width > 0);
            Assert.True(primary.Bounds.Height > 0);
            Assert.True(primary.DpiX >= 96);
            Assert.True(primary.ScalePercent >= 100.0f);

            var virtualBounds = DisplayManager.GetVirtualScreenBounds();
            Assert.True(virtualBounds.Width > 0);
            Assert.True(virtualBounds.Height > 0);
        }
    }

    [Fact]
    public void BatteryTelemetry_CapturesPowerStatusGracefully()
    {
        var status = BatteryTelemetry.GetStatus();
        Assert.NotNull(status);
        Assert.True(Enum.IsDefined(typeof(PowerSourceStatus), status.PowerSource));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Battery percent should be 0-100 or 255 (unknown / AC only desktop)
            Assert.True(status.BatteryLifePercent <= 100 || status.BatteryLifePercent == 255);
        }
    }

    [Fact]
    public void AppInstanceManager_EnforcesSingleInstanceLifetime()
    {
        string testAppKey = $"ZeroUniverse.TestApp.{Guid.NewGuid():N}";

        // First acquisition must succeed
        bool acquired1 = AppInstanceManager.TryAcquireSingleInstance(testAppKey, out var lock1);
        Assert.True(acquired1);
        Assert.NotNull(lock1);

        try
        {
            // Second acquisition for the same app key must fail
            bool acquired2 = AppInstanceManager.TryAcquireSingleInstance(testAppKey, out var lock2);
            Assert.False(acquired2);
            Assert.Null(lock2);
        }
        finally
        {
            // Release the first lock
            lock1?.Dispose();
        }

        // After releasing, acquisition should succeed again
        bool acquired3 = AppInstanceManager.TryAcquireSingleInstance(testAppKey, out var lock3);
        Assert.True(acquired3);
        Assert.NotNull(lock3);
        lock3?.Dispose();
    }

    [Fact]
    public void WindowsServiceManager_QueriesServiceStatusAccurately()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // "Spooler" (Print Spooler) is standard on all Windows machines
            bool spoolerExists = WindowsServiceManager.ServiceExists("Spooler");
            Assert.True(spoolerExists);

            var status = WindowsServiceManager.GetServiceStatus("Spooler");
            Assert.NotEqual(ServiceState.NotFound, status);

            var details = WindowsServiceManager.GetServiceDetails("Spooler");
            Assert.NotNull(details);
            Assert.Equal("Spooler", details.ServiceName);

            // Non-existent service should return NotFound
            string fakeService = $"Fake_Service_{Guid.NewGuid():N}";
            Assert.False(WindowsServiceManager.ServiceExists(fakeService));
            Assert.Equal(ServiceState.NotFound, WindowsServiceManager.GetServiceStatus(fakeService));
        }
    }
}
