using System;
using System.Runtime.InteropServices;

namespace ZeroUI.Wpf.Rendering
{
    /// <summary>
    /// Native structures, constants, and VTable definitions for DirectX 11 and Direct3D 9Ex interop.
    /// Operates with zero external dependencies, pure Windows SDK COM interop.
    /// </summary>
    public static class D3DNative
    {
        #region Constants

        public const uint D3D_SDK_VERSION = 32;
        public const uint D3D11_SDK_VERSION = 7;

        public const int D3DDEVTYPE_HAL = 1;
        public const int D3DDEVTYPE_NULLREF = 4;

        public const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x00000040;
        public const uint D3DCREATE_MULTITHREADED = 0x00000004;
        public const uint D3DCREATE_FPU_PRESERVE = 0x00000002;

        public const uint D3DSWAPEFFECT_DISCARD = 1;

        public const uint D3DFMT_UNKNOWN = 0;
        public const uint D3DFMT_A8R8G8B8 = 21;
        public const uint D3DFMT_X8R8G8B8 = 22;

        public const uint D3DUSAGE_RENDERTARGET = 0x00000001;
        public const uint D3DPOOL_DEFAULT = 0;

        public const int D3D_DRIVER_TYPE_HARDWARE = 1;
        public const int D3D_DRIVER_TYPE_WARP = 2;

        public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x00000020;
        public const uint D3D11_CREATE_DEVICE_SINGLETHREADED = 0x00000001;

        public const int D3D11_USAGE_DEFAULT = 0;
        public const uint D3D11_BIND_RENDER_TARGET = 0x00000020;
        public const uint D3D11_BIND_SHADER_RESOURCE = 0x00000008;
        public const uint D3D11_RESOURCE_MISC_SHARED = 0x00000002;

        public const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
        public const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;

        public static readonly Guid IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");
        public static readonly Guid IID_IDXGIResource = new Guid("035f3ab4-482e-4e50-b41f-8a7f8bd8960b");
        public static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid IID_IDXGIDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid IID_IDXGIAdapter = new Guid("2411e6e1-12ac-4ccf-bd14-9798e8534d7f");

        #endregion

        #region Native Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct D3DPRESENT_PARAMETERS
        {
            public uint BackBufferWidth;
            public uint BackBufferHeight;
            public uint BackBufferFormat;
            public uint BackBufferCount;
            public uint MultiSampleType;
            public uint MultiSampleQuality;
            public uint SwapEffect;
            public IntPtr hDeviceWindow;
            public int Windowed;
            public int EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat;
            public uint Flags;
            public uint FullScreen_RefreshRateInHz;
            public uint PresentationInterval;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SAMPLE_DESC
        {
            public uint Count;
            public uint Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_TEXTURE2D_DESC
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public int Format;
            public DXGI_SAMPLE_DESC SampleDesc;
            public int Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DXGI_ADAPTER_DESC
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public long AdapterLuid;
        }

        #endregion

        #region P/Invoke APIs

        [DllImport("d3d9.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int Direct3DCreate9Ex(uint SDKVersion, out IntPtr ppD3D);

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType,
            IntPtr Software,
            uint Flags,
            int[]? pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        #endregion

        #region COM VTable Delegates

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int Direct3D9Ex_CreateDeviceEx(
            IntPtr pD3D9Ex,
            uint Adapter,
            int DeviceType,
            IntPtr hFocusWindow,
            uint BehaviorFlags,
            ref D3DPRESENT_PARAMETERS pPresentationParameters,
            IntPtr pFullscreenDisplayMode,
            out IntPtr ppReturnedDeviceInterface);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int Direct3DDevice9_CreateTexture(
            IntPtr pDevice,
            uint Width,
            uint Height,
            uint Levels,
            uint Usage,
            uint Format,
            uint Pool,
            out IntPtr ppTexture,
            ref IntPtr pSharedHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int Direct3DTexture9_GetSurfaceLevel(
            IntPtr pTexture,
            uint Level,
            out IntPtr ppSurfaceLevel);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int D3D11Device_CreateTexture2D(
            IntPtr pDevice,
            ref D3D11_TEXTURE2D_DESC pDesc,
            IntPtr pInitialData,
            out IntPtr ppTexture2D);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int D3D11Device_CreateRenderTargetView(
            IntPtr pDevice,
            IntPtr pResource,
            IntPtr pDesc,
            out IntPtr ppRTView);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int IUnknown_QueryInterface(
            IntPtr pUnknown,
            ref Guid riid,
            out IntPtr ppvObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int DXGIResource_GetSharedHandle(
            IntPtr pResource,
            out IntPtr pSharedHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void D3D11DeviceContext_ClearRenderTargetView(
            IntPtr pContext,
            IntPtr pRenderTargetView,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] colorRGBA);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void D3D11DeviceContext_Flush(
            IntPtr pContext);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int IDXGIDevice_GetAdapter(
            IntPtr pDevice,
            out IntPtr ppAdapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int IDXGIAdapter_GetDesc(
            IntPtr pAdapter,
            out DXGI_ADAPTER_DESC pDesc);

        #endregion

        #region VTable Slot Calling Helpers

        public static unsafe T GetVTableDelegate<T>(IntPtr pObject, int slotIndex) where T : Delegate
        {
            if (pObject == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pObject));

            IntPtr vtable = *(IntPtr*)pObject;
            IntPtr methodPtr = *((IntPtr*)vtable + slotIndex);
            return Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
        }

        public static void SafeRelease(ref IntPtr pUnknown)
        {
            if (pUnknown != IntPtr.Zero)
            {
                Marshal.Release(pUnknown);
                pUnknown = IntPtr.Zero;
            }
        }

        #endregion
    }
}
