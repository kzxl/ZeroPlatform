using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;

        public DXGI_RATIONAL(uint numerator, uint denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_MODE_DESC
    {
        public uint Width;
        public uint Height;
        public DXGI_RATIONAL RefreshRate;
        public DXGI_FORMAT Format;
        public uint ScanlineOrdering;
        public uint Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;

        public DXGI_SAMPLE_DESC(uint count, uint quality)
        {
            Count = count;
            Quality = quality;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC
    {
        public DXGI_MODE_DESC BufferDesc;
        public DXGI_SAMPLE_DESC SampleDesc;
        public DXGI_USAGE BufferUsage;
        public uint BufferCount;
        public IntPtr OutputWindow;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Windowed;
        public DXGI_SWAP_EFFECT SwapEffect;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DXGI_ADAPTER_DESC1
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public LUID AdapterLuid;
        public DXGI_ADAPTER_FLAG Flags;

        public string GetDescription()
        {
            fixed (char* ptr = Description)
            {
                return new string(ptr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }
}
