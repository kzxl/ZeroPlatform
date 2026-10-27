using System;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Low-level memory pinning, large page querying, and virtual memory prefetching kernel.
/// Prevents critical buffers (cryptographic keys, real-time caches) from being paged to disk.
/// </summary>
public static class MemoryLockKernel
{
    /// <summary>
    /// Locks the specified virtual address range into physical memory (RAM).
    /// Locked pages cannot be trimmed or written to pagefile.sys by the operating system.
    /// </summary>
    public static bool TryLockMemory(IntPtr address, nuint size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || address == IntPtr.Zero || size == 0)
            return false;

        return NativeMethods.VirtualLock(address, (UIntPtr)size);
    }

    /// <summary>
    /// Unlocks a previously locked virtual memory region, allowing it to be paged out as needed.
    /// </summary>
    public static bool TryUnlockMemory(IntPtr address, nuint size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || address == IntPtr.Zero || size == 0)
            return false;

        return NativeMethods.VirtualUnlock(address, (UIntPtr)size);
    }

    /// <summary>
    /// Locks a memory region with an <see cref="IDisposable"/> token that automatically unlocks upon disposal.
    /// </summary>
    public static IDisposable? LockMemory(IntPtr address, nuint size)
    {
        if (TryLockMemory(address, size))
        {
            return new MemoryLockToken(address, size);
        }

        return null;
    }

    /// <summary>
    /// Retrieves the minimum size of a large memory page supported by the processor architecture (typically 2 MB).
    /// </summary>
    public static nuint GetLargePageMinimum()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return 0;

        return (nuint)NativeMethods.GetLargePageMinimum();
    }

    /// <summary>
    /// Hints the Windows memory manager to asynchronously prefetch the specified virtual memory region into physical RAM.
    /// Eliminates page-fault latency during critical execution sections.
    /// </summary>
    public static bool TryPrefetchMemory(IntPtr address, nuint size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || address == IntPtr.Zero || size == 0)
            return false;

        try
        {
            var range = new NativeMethods.WIN32_MEMORY_RANGE_ENTRY
            {
                VirtualAddress = address,
                NumberOfBytes = (UIntPtr)size
            };

            IntPtr hProcess = NativeMethods.GetCurrentProcess();
            return NativeMethods.PrefetchVirtualMemory(hProcess, (UIntPtr)1, [range], 0);
        }
        catch
        {
            return false;
        }
    }

    #region Private Token

    private sealed class MemoryLockToken : IDisposable
    {
        private IntPtr _address;
        private nuint _size;

        public MemoryLockToken(IntPtr address, nuint size)
        {
            _address = address;
            _size = size;
        }

        public void Dispose()
        {
            if (_address != IntPtr.Zero)
            {
                NativeMethods.VirtualUnlock(_address, (UIntPtr)_size);
                _address = IntPtr.Zero;
                _size = 0;
            }
        }
    }

    #endregion
}
