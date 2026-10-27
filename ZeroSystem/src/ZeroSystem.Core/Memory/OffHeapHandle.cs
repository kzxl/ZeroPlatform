using System;
using System.Runtime.InteropServices;

namespace ZeroSystem.Memory;

/// <summary>
/// Lightweight 64-bit unmanaged handle addressing an off-heap memory block.
/// Combines a 32-bit slot index and a 32-bit generation version to prevent ABA and use-after-free corruption.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct OffHeapHandle(int Index, uint Version)
{
    public static readonly OffHeapHandle Invalid = new(-1, 0);

    /// <summary>
    /// Gets whether this handle contains non-negative index and non-zero version.
    /// </summary>
    public bool IsValid => Index >= 0 && Version > 0;

    public override string ToString() => $"[OffHeapHandle Index={Index}, Ver={Version}]";
}
