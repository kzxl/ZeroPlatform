using System;
using System.IO;
using Xunit;
using ZeroSystem;

namespace ZeroSystem.Tests;

public class StartupManagerTests
{
    [Fact]
    public void GetDefaultLogPath_ValidApp_ReturnsValidPath()
    {
        string appName = "ZeroSystemTestApp";
        string logFileName = "test.log";

        string path = StartupManager.GetDefaultLogPath(appName, logFileName);

        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains(appName, path);
        Assert.EndsWith(logFileName, path);
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void GetCurrentExecutablePath_ReturnsNonEmptyPath()
    {
        string exePath = StartupManager.GetCurrentExecutablePath();
        Assert.False(string.IsNullOrWhiteSpace(exePath));
    }

    [Fact]
    public void ConsoleWindow_SafeToCallOnAnyEnvironment()
    {
        bool _ = StartupManager.IsConsoleVisible();
        StartupManager.HideConsoleWindow();
        StartupManager.ShowConsoleWindow();
    }

    [Fact]
    public void GetStatus_NonExistentApp_ReturnsDisabled()
    {
        string fakeApp = $"NonExistentApp_{Guid.NewGuid():N}";

        var userStatus = StartupManager.GetStatus(fakeApp, StartupScope.UserLogin);
        Assert.False(userStatus.IsEnabled);
        Assert.Equal(StartupScope.UserLogin, userStatus.Scope);

        var bootStatus = StartupManager.GetStatus(fakeApp, StartupScope.SystemBoot);
        Assert.False(bootStatus.IsEnabled);
        Assert.Equal(StartupScope.SystemBoot, bootStatus.Scope);
    }

    [Fact]
    public void UserLogin_RegisterAndUnregister_Roundtrip()
    {
        if (!StartupManager.IsWindows && !StartupManager.IsLinux)
        {
            return;
        }

        string testAppName = $"ZeroSystem_UnitTest_{Guid.NewGuid():N}";
        string testExe = StartupManager.GetCurrentExecutablePath();

        try
        {
            // Register
            var (regOk, regMsg) = StartupManager.Register(testAppName, testExe, "--test-arg", StartupScope.UserLogin);
            Assert.True(regOk, $"Register failed: {regMsg}");

            // Verify Enabled
            var status = StartupManager.GetStatus(testAppName, StartupScope.UserLogin);
            Assert.True(status.IsEnabled, "Expected status to be enabled after registration.");

            // Unregister
            var (unregOk, unregMsg) = StartupManager.Unregister(testAppName, StartupScope.UserLogin);
            Assert.True(unregOk, $"Unregister failed: {unregMsg}");

            // Verify Disabled
            var statusAfter = StartupManager.GetStatus(testAppName, StartupScope.UserLogin);
            Assert.False(statusAfter.IsEnabled, "Expected status to be disabled after unregistration.");
        }
        finally
        {
            StartupManager.Unregister(testAppName, StartupScope.UserLogin);
        }
    }
}
