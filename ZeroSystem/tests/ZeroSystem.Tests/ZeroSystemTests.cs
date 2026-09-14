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
}
