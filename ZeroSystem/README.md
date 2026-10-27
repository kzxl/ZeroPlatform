# ⚡ ZeroSystem: Sovereign Windows Native & Kernel Subsystem

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%201%20(Compute%20%26%20System)-4f46e5.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.4.0-blue.svg)](https://www.nuget.org/packages/ZeroSystem.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Dependencies: 0](https://img.shields.io/badge/Dependencies-0%20External-brightgreen.svg)]()
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Tests: 33 Passed](https://img.shields.io/badge/Tests-33%20Passed%20(100%25)-brightgreen.svg)]()

**ZeroSystem** (`ZeroSystem.Core`) is the sovereign Windows Native, Kernel P/Invoke, DWM Desktop, Off-Heap Memory, and OS Internals Subsystem of the **ZeroPlatform** / **Zero Universe** ecosystem. It provides type-safe, resource-managed wrappers around low-level Win32, NTDLL, and Kernel APIs with **zero external dependencies** (100% pure C# BCL).

---

## 📦 Key Subsystem Capabilities

| Component | Description |
| :--- | :--- |
| **`OffHeapMemoryStore`** | Zero-GC unmanaged memory block store with 64-bit ABA-protected handles (`OffHeapHandle`), zero-copy ref/span reads, and physical RAM pinning (`VirtualLock`). |
| **`OffHeapCircularTelemetryBuffer<T>`** | Unmanaged sliding-window circular buffer for high-frequency telemetry and time-series sensor points with zero managed GC churn. |
| **`NativeMethods`** | Centralized, hardened Win32 P/Invoke declarations for `Kernel32`, `User32`, `Advapi32`, `Ntdll`, `Shell32`, `DwmApi`, `RstrtMgr`, `ShCore`, `IpHlpApi`, and `Avrt`. |
| **`StorageHardwareKernel`** | Low-level storage IOCTLs: SSD vs HDD seek penalty detection, NVMe/SATA bus type, and disk geometry without WMI. |
| **`NetworkConnectionTracker`** | Maps every active IPv4/IPv6 TCP connection and UDP listening endpoint to its owning Process ID (PID) via IP Helper APIs. |
| **`NtProcessKernel`** | Ultra-fast process tree snapshot (&lt; 2ms, 50x faster than `Process.GetProcesses()`), true parent PID resolution, and `NtSuspendProcess` / `NtResumeProcess`. |
| **`ThreadAffinityKernel`** | CPU core pinning (`SetThreadAffinityMask`), time-critical thread priority, and Windows MMCSS multimedia scheduling (`AvSetMmThreadCharacteristics`). |
| **`MemoryLockKernel`** | Locks virtual address ranges into physical RAM (`VirtualLock`), large memory page sizing (`GetLargePageMinimum`), and asynchronous memory prefetching. |
| **`StartupManager`** | Cross-platform application autostart: Windows `HKCU\...\Run` & `schtasks` SYSTEM boot service, Linux `~/.config/autostart` & `systemd`, macOS `LaunchAgents`, with Win32 console hiding. |
| **`FileLockManager`** | Identifies processes and services locking files or folders via the Windows Restart Manager API (`rstrtmgr.dll`) without external tools. |
| **`SmbiosReader`** | Sub-millisecond (&lt; 0.1ms) zero-WMI extraction of Motherboard Serial, System UUID, and BIOS metadata via raw `GetSystemFirmwareTable('RSMB')`. |
| **`DisplayManager`** | Multi-monitor enumeration, work areas, per-monitor DPI scaling factors (100%, 125%, 150%, 200%), and virtual desktop bounding box. |
| **`BatteryTelemetry`** | Battery and power telemetry (`GetSystemPowerStatus`): battery percentage, AC online status, charging state, estimated run-time. |
| **`AppInstanceManager`** | Single-instance application coordinator via named system `Mutex` with automatic foreground window activation of existing instances. |
| **`WindowsServiceManager`** | Native Windows Service Controller (`Advapi32.dll`): query service status, check existence, start and stop services without `System.ServiceProcess`. |
| **`HardwareTelemetry`** | Real-time CPU usage (`GetSystemTimes`), RAM (`GlobalMemoryStatusEx`), dedicated/shared GPU memory (`DXGI`), drive geometry, and NICs. |
| **`DosDeviceManager`** | High-level MS-DOS device mapping (`DefineDosDevice`). Mounts VSS Shadow Copies (`\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy{N}`) to drive letters (`Z:`). |
| **`ProcessGuard`** | System privileges escalation (`SeBackupPrivilege`, `SeRestorePrivilege`, `SeProfileSingleProcessPrivilege`), safe PID detection, and foreground window querying. |
| **`MemoryKernel`** | Deep RAM optimization via undocumented `NtSetSystemInformation` (PurgeStandbyList = 4, SystemMemoryList = 80) and process working set trimming. |
| **`UnsafeFileStreamFactory`** | Opens permissive non-locking file streams (`FileShare.ReadWrite \| FileShare.Delete`) to inspect locked files without sharing violations. |
| **`DesktopWallpaperInjector`** | Interacts with `Progman` and DWM (`0x052C`) to spawn the behind-icon `WorkerW` canvas for Live Wallpapers and HUD overlays. |
| **`PowerAndDisplayWatcher`** | Real-time monitoring of AC/Battery power state and detection of active full-screen applications. |
| **`ShellRegistryHelper`** | Registers Windows Explorer context menu actions directly into `HKCU\Software\Classes` without requiring Administrator privileges. |

---

## 🚀 Quick Start

### NuGet Package Installation
```bash
dotnet add package ZeroSystem.Core
```

### Example 1: Mount VSS Shadow Copy
```csharp
using ZeroSystem;

if (DosDeviceManager.TryMountDevice(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1", out char driveLetter, out string err))
{
    Console.WriteLine($"VSS mounted at {driveLetter}:");
    // Access historical files via $"{driveLetter}:\\..."
    DosDeviceManager.TryUnmountDevice(driveLetter, null, out _);
}
```

### Example 2: Purge Standby List (RAM Optimization)
```csharp
using ZeroSystem;

if (ProcessGuard.TryEnablePrivilege(NativeMethods.SE_PROFILE_SINGLE_PROCESS_NAME))
{
    bool success = MemoryKernel.PurgeStandbyList();
    Console.WriteLine($"Standby List Purged: {success}");
}
```

### Example 3: Detect Locking Processes (Restart Manager)
```csharp
using ZeroSystem;

var lockers = FileLockManager.GetLockingProcesses(@"C:\Program Files\App\locked.dll");
foreach (var proc in lockers)
{
    Console.WriteLine($"Locked by PID {proc.ProcessId}: {proc.ProcessName} ({proc.ApplicationType})");
}
```

### Example 4: Sub-Millisecond Hardware UUID (Zero-WMI SMBIOS)
```csharp
using ZeroSystem;

string uuid = SmbiosReader.GetSystemUuid();
string boardSerial = SmbiosReader.GetMotherboardSerial();
Console.WriteLine($"Hardware Fingerprint: UUID={uuid}, Motherboard={boardSerial}");
```

### Example 5: Single-Instance Mutex & Window Activation
```csharp
using ZeroSystem;

if (!AppInstanceManager.TryAcquireSingleInstance("ZeroUniverse.ZProbe", out var appLock))
{
    Console.WriteLine("Another instance is already running. Activating existing window...");
    AppInstanceManager.BringProcessToForeground(Process.GetProcessesByName("ZProbe")[0].Id);
    return;
}

using (appLock)
{
    // Run main application loop
}
```

### Example 6: Ultra-Fast Process Snapshot & Parent PID
```csharp
using ZeroSystem;

var processes = NtProcessKernel.GetProcessListFast(); // Executes in 1-2ms (<50x faster than Process.GetProcesses())
foreach (var proc in processes)
{
    Console.WriteLine($"PID {proc.ProcessId} (Parent: {proc.ParentProcessId}) - {proc.ProcessName} [Threads: {proc.ThreadCount}]");
}
```

### Example 7: SSD vs HDD Seek Penalty & NVMe Bus Type
```csharp
using ZeroSystem;

bool isC_Ssd = StorageHardwareKernel.IsSolidStateDrive('C');
var disk0 = StorageHardwareKernel.GetPhysicalDiskInfo(0);

Console.WriteLine($"Drive C is SSD: {isC_Ssd}");
Console.WriteLine($"Disk 0: {disk0.Model}, Bus: {disk0.BusType}, Size: {disk0.TotalSizeBytes / (1024*1024*1024)} GB");
```

### Example 8: Find Process Owning Network Port (IP Helper)
```csharp
using ZeroSystem;

// Find which application is listening on port 6310 (IPP) or 9200
int? pid = NetworkConnectionTracker.FindProcessOwningTcpPort(6310);
if (pid.HasValue)
{
    Console.WriteLine($"Port 6310 is owned by PID: {pid.Value}");
}
```

### Example 9: CPU Core Pinning & Multimedia Real-Time Priority
```csharp
using ZeroSystem;

// Pin current thread to CPU Core 0 to eliminate cache thrashing
ThreadAffinityKernel.SetCurrentThreadAffinity(0);
ThreadAffinityKernel.SetCurrentThreadPriority(ThreadPriorityLevel.TimeCritical);

// Enable Windows MMCSS Pro Audio scheduling slice
using (ThreadAffinityKernel.EnableMultimediaScheduling("Pro Audio"))
{
    // Real-time vision inspection / DSP audio loop
}
```

### Example 10: Off-Heap GC-Free Memory Store (ABA Protected)
```csharp
using ZeroSystem.Memory;

// Initialize 64MB unmanaged off-heap store
using var store = new OffHeapMemoryStore(initialCapacityBytes: 64 * 1024 * 1024);

// Allocate 4KB unmanaged block (zero GC pause)
OffHeapHandle handle = store.Allocate(4096);

// Zero-copy span write
Span<byte> span = store.GetSpan(handle);
span[0] = 0xAA;

// Free block safely (recycled without fragmentation)
store.Free(handle);
```

### Example 11: Off-Heap Circular Telemetry Buffer
```csharp
using ZeroSystem.Memory;

// Create 10,000-sample sliding window buffer in unmanaged RAM
using var telemetryBuffer = new OffHeapCircularTelemetryBuffer<double>(capacity: 10_000);

for (int i = 0; i < 50_000; i++)
{
    telemetryBuffer.Push(25.4 + (i * 0.01));
}

// Zero-GC snapshot read
int count = telemetryBuffer.ReadLatest(stackalloc double[100]);
```

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.4.0`** | 2026-09-29 | **Off-Heap GC-Free Memory Store & OS RAM Pinning**:<br/>• Added `OffHeapMemoryStore`: Unmanaged memory store with 64-bit ABA-protected handles and zero-copy ref/span reads.<br/>• Added `OffHeapCircularTelemetryBuffer<T>`: Zero-GC unmanaged sliding-window circular buffer.<br/>• Native Windows OS physical RAM pinning via `VirtualLock`.<br/>• 33 unit and system tests passing (100% success rate). |
| **`v1.2.0`** | 2026-09-21 | **Kernel Hardware Diagnostics & Thread Pinning**:<br/>• Added `ThreadAffinityKernel` & MMCSS multimedia scheduling.<br/>• Added `StorageHardwareKernel` for SSD/NVMe detection without WMI.<br/>• Added `NetworkConnectionTracker` IP Helper connection inspection.<br/>• 26 automated tests passing (100% success rate). |

---

## 📄 License
Released under the permissive **MIT License**.
Architected and developed by **Phong Võ** (`kzxl`).
