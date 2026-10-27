using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// Sovereign thread affinity and CPU core pinning manager.
    /// Eliminates thread hopping, context switching, and cache thrashing across worker threads.
    /// Pure sovereign implementation without third-party dependencies.
    /// </summary>
    internal static class CpuAffinity
    {
#if NETFRAMEWORK
        private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        private static readonly bool IsLinux = Environment.OSVersion.Platform == PlatformID.Unix;
#else
        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        [DllImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
        private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref ulong mask);

        /// <summary>
        /// Pins the calling thread strictly to a specific logical CPU core index.
        /// Cross-platform support for Windows (SetThreadAffinityMask) and Linux (sched_setaffinity).
        /// </summary>
        /// <param name="coreIndex">Zero-based core index (0 to ProcessorCount - 1).</param>
        /// <returns>True if successfully pinned; false otherwise.</returns>
        public static bool PinCurrentThread(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= 64)
                return false;

            if (IsWindows)
            {
                try
                {
                    ulong mask = 1UL << (coreIndex % 64);
                    IntPtr hThread = GetCurrentThread();
                    UIntPtr prev = SetThreadAffinityMask(hThread, (UIntPtr)mask);
                    return prev != UIntPtr.Zero;
                }
                catch
                {
                    return false;
                }
            }
            else if (IsLinux)
            {
                try
                {
                    ulong mask = 1UL << (coreIndex % 64);
                    int ret = sched_setaffinity(0, (IntPtr)sizeof(ulong), ref mask);
                    return ret == 0;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets high scheduling priority for dedicated compute worker threads.
        /// </summary>
        public static void SetHighPriority()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            }
            catch
            {
                // Ignored in constrained execution environments
            }
        }

        private static int _physicalCoreCount;

        /// <summary>
        /// Gets the detected physical CPU core count (excluding SMT / Hyper-Threading logical threads).
        /// </summary>
        public static int PhysicalCoreCount
        {
            get
            {
                if (_physicalCoreCount > 0)
                    return _physicalCoreCount;

                int count = DetectPhysicalCores();
                _physicalCoreCount = count > 0 ? count : Math.Max(1, Environment.ProcessorCount / 2);
                return _physicalCoreCount;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnedLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
        {
            public UIntPtr ProcessorMask;
            public int Relationship; // 0 = RelationProcessorCore
            public ulong Dummy1;
            public ulong Dummy2;
        }

        private static int DetectPhysicalCores()
        {
            if (IsWindows)
            {
                try
                {
                    uint length = 0;
                    GetLogicalProcessorInformation(IntPtr.Zero, ref length);
                    if (length == 0) return 0;

                    IntPtr buffer = Marshal.AllocHGlobal((int)length);
                    try
                    {
                        if (GetLogicalProcessorInformation(buffer, ref length))
                        {
                            int structSize = Marshal.SizeOf(typeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION));
                            int count = (int)(length / structSize);
                            int physicalCores = 0;
                            for (int i = 0; i < count; i++)
                            {
                                IntPtr ptr = new IntPtr(buffer.ToInt64() + i * structSize);
                                var info = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION)Marshal.PtrToStructure(ptr, typeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION))!;
                                if (info.Relationship == 0) // RelationProcessorCore
                                {
                                    physicalCores++;
                                }
                            }
                            if (physicalCores > 0)
                                return physicalCores;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                catch
                {
                    // Fallback
                }
            }
            else if (IsLinux)
            {
                int linuxCores = DetectLinuxPhysicalCores();
                if (linuxCores > 0)
                    return linuxCores;
            }

            return Math.Max(1, Environment.ProcessorCount / 2);
        }

        private static int DetectLinuxPhysicalCores()
        {
            try
            {
                if (!System.IO.File.Exists("/proc/cpuinfo"))
                    return 0;

                var coreKeys = new System.Collections.Generic.HashSet<string>();
                string currentPhysicalId = "0";
                string currentCoreId = "";

                using (var reader = new System.IO.StreamReader("/proc/cpuinfo"))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            string key = line.Substring(0, colonIdx).Trim();
                            string val = line.Substring(colonIdx + 1).Trim();

                            if (key.Equals("physical id", StringComparison.OrdinalIgnoreCase))
                            {
                                currentPhysicalId = val;
                            }
                            else if (key.Equals("core id", StringComparison.OrdinalIgnoreCase))
                            {
                                currentCoreId = val;
                            }
                        }
                        else if (string.IsNullOrWhiteSpace(line))
                        {
                            if (!string.IsNullOrEmpty(currentCoreId))
                            {
                                coreKeys.Add(currentPhysicalId + ":" + currentCoreId);
                                currentCoreId = "";
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentCoreId))
                {
                    coreKeys.Add(currentPhysicalId + ":" + currentCoreId);
                }

                return coreKeys.Count > 0 ? coreKeys.Count : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
