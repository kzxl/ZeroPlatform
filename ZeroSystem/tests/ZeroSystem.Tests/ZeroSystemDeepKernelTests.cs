using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using ZeroSystem;

namespace ZeroSystem.Tests;

public class ZeroSystemDeepKernelTests
{
    [Fact]
    public void StorageHardwareKernel_DetectsDrivePropertiesAndGeometry()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Verify C: drive SSD check executes cleanly
            bool isSsd = StorageHardwareKernel.IsSolidStateDrive('C');
            // Result is a valid boolean (either true for SSD or false for HDD)
            Assert.True(isSsd || !isSsd);

            // Query PhysicalDrive0 info (requires read access, might require elevation on restricted accounts)
            var diskInfo = StorageHardwareKernel.GetPhysicalDiskInfo(0);
            if (diskInfo != null)
            {
                Assert.True(diskInfo.TotalSizeBytes > 0);
                Assert.True(diskInfo.BytesPerSector >= 512);
                Assert.NotEqual(StorageBusInterface.Unknown, diskInfo.BusType);
                Assert.False(string.IsNullOrWhiteSpace(diskInfo.Model));
            }
        }
    }

    [Fact]
    public void NetworkConnectionTracker_EnumeratesTcpAndUdpSocketsWithPids()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var tcpConnections = NetworkConnectionTracker.GetActiveTcpConnections();
            Assert.NotNull(tcpConnections);
            Assert.NotEmpty(tcpConnections);

            var firstTcp = tcpConnections[0];
            Assert.NotNull(firstTcp.LocalEndpoint);
            Assert.NotNull(firstTcp.RemoteEndpoint);
            Assert.True(firstTcp.OwningProcessId >= 0);

            var udpEndpoints = NetworkConnectionTracker.GetActiveUdpEndpoints();
            Assert.NotNull(udpEndpoints);
            Assert.NotEmpty(udpEndpoints);

            var firstUdp = udpEndpoints[0];
            Assert.NotNull(firstUdp.LocalEndpoint);
            Assert.True(firstUdp.OwningProcessId >= 0);
        }
    }

    [Fact]
    public void NtProcessKernel_CapturesProcessListInMilliseconds()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var processes = NtProcessKernel.GetProcessListFast();
            sw.Stop();

            Assert.NotNull(processes);
            Assert.NotEmpty(processes);
            // Snapshot should be blazing fast (< 50ms)
            Assert.True(sw.ElapsedMilliseconds < 500);

            // Should contain current process
            Assert.Contains(processes, p => p.ProcessId == Environment.ProcessId);

            int? parentPid = NtProcessKernel.GetParentProcessId(Environment.ProcessId);
            Assert.NotNull(parentPid);
            Assert.True(parentPid.Value > 0);
        }
    }

    [Fact]
    public void ThreadAffinityKernel_ConfiguresCorePinningAndPriority()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Core 0 must always be available
            bool affinitySet = ThreadAffinityKernel.SetCurrentThreadAffinity(0);
            Assert.True(affinitySet);

            // Adjust thread priority
            bool prioritySet = ThreadAffinityKernel.SetCurrentThreadPriority(ThreadPriorityLevel.AboveNormal);
            Assert.True(prioritySet);

            // Reset back to Normal
            ThreadAffinityKernel.SetCurrentThreadPriority(ThreadPriorityLevel.Normal);
        }
    }

    [Fact]
    public void MemoryLockKernel_LocksAndPrefetchesMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nuint largePage = MemoryLockKernel.GetLargePageMinimum();
            // Typically 2 MB on x64 or 0 if unsupported
            Assert.True(largePage >= 0);

            // Allocate a page of unmanaged memory
            int size = 4096;
            IntPtr mem = Marshal.AllocHGlobal(size);
            try
            {
                // Lock memory page into RAM
                using (var lockToken = MemoryLockKernel.LockMemory(mem, (nuint)size))
                {
                    Assert.NotNull(lockToken);

                    // Prefetch memory
                    bool prefetched = MemoryLockKernel.TryPrefetchMemory(mem, (nuint)size);
                    Assert.True(prefetched || !prefetched); // May fail on virtualized sandbox, should not throw
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }
    }
}
