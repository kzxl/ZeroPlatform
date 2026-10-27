using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Native
{
    public static class DirectXNative
    {
        public const uint D3D11_SDK_VERSION = 7;

        public static readonly Guid IID_IDXGIFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        public static readonly Guid IID_IDXGIAdapter1 = new Guid("29038f61-3839-4626-91fd-086879011a05");
        public static readonly Guid IID_IDXGIDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid IID_IDXGISwapChain = new Guid("310d36a0-d2e7-4c0a-aa04-6a9d23b8886a");
        public static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid IID_ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bb6137");
        public static readonly Guid IID_IDXGISurface = new Guid("cafcb56c-6ac3-4889-bf47-9e23bbd260ec");
        public static readonly Guid IID_IDXGIDevice1 = new Guid("77db970f-6276-48ba-ba28-070143b4392c");

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall, SetLastError = false)]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            D3D_DRIVER_TYPE DriverType,
            IntPtr Software,
            D3D11_CREATE_DEVICE_FLAG Flags,
            [In] D3D_FEATURE_LEVEL[]? pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out D3D_FEATURE_LEVEL pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("dxgi.dll", CallingConvention = CallingConvention.StdCall, SetLastError = false)]
        public static extern int CreateDXGIFactory1(
            [In] ref Guid riid,
            out IntPtr ppFactory);
    }
}
