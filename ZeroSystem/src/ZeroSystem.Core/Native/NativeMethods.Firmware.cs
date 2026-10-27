using System;
using System.Runtime.InteropServices;

namespace ZeroSystem.Native;

internal static partial class NativeMethods
{
    // Firmware Table (SMBIOS)
    public const uint FIRMWARE_TABLE_PROVIDER_RSMB = 0x52534D42; // 'RSMB'

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetSystemFirmwareTable(
        uint FirmwareTableProviderSignature,
        uint FirmwareTableID,
        IntPtr pFirmwareTableBuffer,
        uint BufferSize);
}
