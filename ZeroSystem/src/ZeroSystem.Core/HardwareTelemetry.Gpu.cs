using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZeroSystem;

public static partial class HardwareTelemetry
{
    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    /// <summary>
    /// Enumerates graphics adapters and dedicated/shared video memory via DXGI native API.
    /// </summary>
    public static IReadOnlyList<GpuAdapterInfo> GetGpuAdapters()
    {
        var adapters = new List<GpuAdapterInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return adapters;

        try
        {
            int hr = CreateDXGIFactory1(in IID_IDXGIFactory1, out IntPtr factoryPtr);
            if (hr < 0 || factoryPtr == IntPtr.Zero)
                return adapters;

            try
            {
                uint index = 0;
                while (true)
                {
                    // Call IDXGIFactory1::EnumAdapters1 (slot 12 in IDXGIFactory1 vtable)
                    IntPtr vtable = Marshal.ReadIntPtr(factoryPtr);
                    IntPtr enumAdapters1Ptr = Marshal.ReadIntPtr(vtable, 12 * IntPtr.Size);
                    var enumAdapters1 = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(enumAdapters1Ptr);

                    hr = enumAdapters1(factoryPtr, index, out IntPtr adapterPtr);
                    if (hr < 0 || adapterPtr == IntPtr.Zero)
                        break;

                    try
                    {
                        // IDXGIAdapter1::GetDesc1 (slot 10 in IDXGIAdapter1 vtable)
                        IntPtr adapterVtable = Marshal.ReadIntPtr(adapterPtr);
                        IntPtr getDesc1Ptr = Marshal.ReadIntPtr(adapterVtable, 10 * IntPtr.Size);
                        var getDesc1 = Marshal.GetDelegateForFunctionPointer<GetDesc1Delegate>(getDesc1Ptr);

                        DXGI_ADAPTER_DESC1 desc = default;
                        hr = getDesc1(adapterPtr, ref desc);
                        if (hr >= 0)
                        {
                            adapters.Add(new GpuAdapterInfo(
                                AdapterName: desc.Description?.TrimEnd('\0') ?? "Unknown GPU",
                                DedicatedVideoMemoryBytes: (ulong)desc.DedicatedVideoMemory,
                                DedicatedSystemMemoryBytes: (ulong)desc.DedicatedSystemMemory,
                                SharedSystemMemoryBytes: (ulong)desc.SharedSystemMemory,
                                VendorId: desc.VendorId,
                                DeviceId: desc.DeviceId));
                        }
                    }
                    finally
                    {
                        Marshal.Release(adapterPtr);
                    }

                    index++;
                }
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }
        catch
        {
            // Fallback gracefully if DXGI is unavailable
        }

        return adapters;
    }

    #region DXGI Interop

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(in Guid riid, out IntPtr ppFactory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Delegate(IntPtr thisPtr, uint adapterIndex, out IntPtr ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Delegate(IntPtr thisPtr, ref DXGI_ADAPTER_DESC1 pDesc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public IntPtr DedicatedVideoMemory;
        public IntPtr DedicatedSystemMemory;
        public IntPtr SharedSystemMemory;
        public LUID AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    #endregion
}
