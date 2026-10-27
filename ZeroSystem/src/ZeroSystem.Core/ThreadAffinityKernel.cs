using System;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Execution priority level for time-critical processing threads.
/// </summary>
public enum ThreadPriorityLevel
{
    Normal = 0,
    AboveNormal = 1,
    Highest = 2,
    TimeCritical = 15
}

/// <summary>
/// Sovereign thread and CPU core affinity kernel.
/// Provides core pinning, real-time priority boosting, and MMCSS (Multimedia Class Scheduler Service) registration.
/// </summary>
public static class ThreadAffinityKernel
{
    /// <summary>
    /// Pins the calling thread strictly to a specific logical CPU core index (0 to ProcessorCount - 1).
    /// Prevents CPU cache thrashing and thread hopping in industrial vision / DSP processing loops.
    /// </summary>
    public static bool SetCurrentThreadAffinity(int coreIndex)
    {
        if (coreIndex < 0 || coreIndex >= 64)
            return false;

        ulong mask = 1UL << coreIndex;
        return SetCurrentThreadAffinityMask(mask);
    }

    /// <summary>
    /// Sets the CPU core affinity bitmask for the calling thread.
    /// </summary>
    public static bool SetCurrentThreadAffinityMask(ulong coreBitmask)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || coreBitmask == 0)
            return false;

        IntPtr hThread = NativeMethods.GetCurrentThread();
        UIntPtr prev = NativeMethods.SetThreadAffinityMask(hThread, (UIntPtr)coreBitmask);
        return prev != UIntPtr.Zero;
    }

    /// <summary>
    /// Sets the CPU core affinity bitmask for the entire process.
    /// </summary>
    public static bool SetProcessAffinityMask(ulong coreBitmask)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || coreBitmask == 0)
            return false;

        IntPtr hProcess = NativeMethods.GetCurrentProcess();
        return NativeMethods.SetProcessAffinityMask(hProcess, (UIntPtr)coreBitmask);
    }

    /// <summary>
    /// Adjusts the execution priority of the calling thread (e.g. TimeCritical for hard real-time loops).
    /// </summary>
    public static bool SetCurrentThreadPriority(ThreadPriorityLevel priority)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        IntPtr hThread = NativeMethods.GetCurrentThread();
        return NativeMethods.SetThreadPriority(hThread, (int)priority);
    }

    /// <summary>
    /// Registers the calling thread with the Windows Multimedia Class Scheduler Service (MMCSS).
    /// Guarantees prioritized CPU time-slices for audio/video rendering and real-time vision pipelines.
    /// Returns an <see cref="IDisposable"/> token that reverts the thread characteristics upon disposal.
    /// </summary>
    /// <param name="taskName">Task name registered in registry, e.g. "Pro Audio", "Playback", "Games", "Window Manager".</param>
    public static IDisposable? EnableMultimediaScheduling(string taskName = "Pro Audio")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(taskName))
            return null;

        try
        {
            IntPtr avrtHandle = NativeMethods.AvSetMmThreadCharacteristics(taskName, out _);
            if (avrtHandle != IntPtr.Zero)
            {
                return new MmcssToken(avrtHandle);
            }
        }
        catch
        {
            // Avrt.dll not present on some minimal server editions
        }

        return null;
    }

    #region Private Token

    private sealed class MmcssToken : IDisposable
    {
        private IntPtr _handle;

        public MmcssToken(IntPtr handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.AvRevertMmThreadCharacteristics(_handle);
                }
                catch
                {
                    // Ignore revert errors
                }

                _handle = IntPtr.Zero;
            }
        }
    }

    #endregion
}
