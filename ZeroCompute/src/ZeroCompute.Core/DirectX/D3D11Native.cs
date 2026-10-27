using System;
using System.Runtime.InteropServices;

namespace ZeroCompute.Core.DirectX
{
    internal static unsafe class D3D11Native
    {
        private const string D3D11Dll = "d3d11.dll";

        public const int D3D_DRIVER_TYPE_HARDWARE = 1;
        public const int D3D_DRIVER_TYPE_WARP = 2;
        public const int D3D_DRIVER_TYPE_REFERENCE = 3;

        public const uint D3D11_SDK_VERSION = 7;

        public const int D3D11_USAGE_DEFAULT = 0;
        public const int D3D11_USAGE_IMMUTABLE = 1;
        public const int D3D11_USAGE_DYNAMIC = 2;
        public const int D3D11_USAGE_STAGING = 3;

        public const uint D3D11_BIND_VERTEX_BUFFER = 0x1;
        public const uint D3D11_BIND_INDEX_BUFFER = 0x2;
        public const uint D3D11_BIND_CONSTANT_BUFFER = 0x4;
        public const uint D3D11_BIND_SHADER_RESOURCE = 0x8;
        public const uint D3D11_BIND_STREAM_OUTPUT = 0x10;
        public const uint D3D11_BIND_RENDER_TARGET = 0x20;
        public const uint D3D11_BIND_DEPTH_STENCIL = 0x40;
        public const uint D3D11_BIND_UNORDERED_ACCESS = 0x80;

        public const uint D3D11_CPU_ACCESS_WRITE = 0x10000;
        public const uint D3D11_CPU_ACCESS_READ = 0x20000;

        public const uint D3D11_RESOURCE_MISC_BUFFER_STRUCTURED = 0x40;

        public const int D3D11_MAP_READ = 1;
        public const int D3D11_MAP_WRITE = 2;
        public const int D3D11_MAP_READ_WRITE = 3;
        public const int D3D11_MAP_WRITE_DISCARD = 4;
        public const int D3D11_MAP_WRITE_NO_OVERWRITE = 5;

        public const int D3D11_UAV_DIMENSION_BUFFER = 1;
        public const int D3D11_SRV_DIMENSION_BUFFER = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_BUFFER_DESC
        {
            public uint ByteWidth;
            public int Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
            public uint StructureByteStride;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_SUBRESOURCE_DATA
        {
            public IntPtr pSysMem;
            public uint SysMemPitch;
            public uint SysMemSlicePitch;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_MAPPED_SUBRESOURCE
        {
            public IntPtr pData;
            public uint RowPitch;
            public uint DepthPitch;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_BUFFER_UAV
        {
            public uint FirstElement;
            public uint NumElements;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_UNORDERED_ACCESS_VIEW_DESC
        {
            public int Format; // DXGI_FORMAT_UNKNOWN = 0
            public int ViewDimension; // D3D11_UAV_DIMENSION_BUFFER = 1
            public D3D11_BUFFER_UAV Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_BUFFER_SRV
        {
            public uint FirstElement;
            public uint NumElements;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            public int Format; // DXGI_FORMAT_UNKNOWN = 0
            public int ViewDimension; // D3D11_SRV_DIMENSION_BUFFER = 1
            public D3D11_BUFFER_SRV Buffer;
        }

        [DllImport(D3D11Dll, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int driverType,
            IntPtr software,
            uint flags,
            [In] int[]? pFeatureLevels,
            uint featureLevels,
            uint sdkVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        // =========================================================================
        // COM VTable helpers
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr thisPtr);

        public static uint Release(IntPtr comPtr)
        {
            if (comPtr == IntPtr.Zero) return 0;
            IntPtr methodPtr = (*(IntPtr**)comPtr)[2];
            return Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(methodPtr)(comPtr);
        }

        // ID3D11Device::CreateBuffer (Slot 3)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateBufferDelegate(IntPtr thisPtr, ref D3D11_BUFFER_DESC pDesc, IntPtr pInitialData, out IntPtr ppBuffer);

        public static int CreateBuffer(IntPtr device, ref D3D11_BUFFER_DESC desc, IntPtr initialData, out IntPtr buffer)
        {
            if (device == IntPtr.Zero) { buffer = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[3];
            return Marshal.GetDelegateForFunctionPointer<CreateBufferDelegate>(methodPtr)(device, ref desc, initialData, out buffer);
        }

        // ID3D11Device::CreateShaderResourceView (Slot 7)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateShaderResourceViewDelegate(IntPtr thisPtr, IntPtr pResource, ref D3D11_SHADER_RESOURCE_VIEW_DESC pDesc, out IntPtr ppSRView);

        public static int CreateShaderResourceView(IntPtr device, IntPtr resource, ref D3D11_SHADER_RESOURCE_VIEW_DESC desc, out IntPtr srv)
        {
            if (device == IntPtr.Zero) { srv = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[7];
            return Marshal.GetDelegateForFunctionPointer<CreateShaderResourceViewDelegate>(methodPtr)(device, resource, ref desc, out srv);
        }

        // ID3D11Device::CreateUnorderedAccessView (Slot 8)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateUnorderedAccessViewDelegate(IntPtr thisPtr, IntPtr pResource, ref D3D11_UNORDERED_ACCESS_VIEW_DESC pDesc, out IntPtr ppUAView);

        public static int CreateUnorderedAccessView(IntPtr device, IntPtr resource, ref D3D11_UNORDERED_ACCESS_VIEW_DESC desc, out IntPtr uav)
        {
            if (device == IntPtr.Zero) { uav = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[8];
            return Marshal.GetDelegateForFunctionPointer<CreateUnorderedAccessViewDelegate>(methodPtr)(device, resource, ref desc, out uav);
        }

        // ID3D11Device::CreateComputeShader (Slot 18)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateComputeShaderDelegate(IntPtr thisPtr, byte* pBytecode, UIntPtr length, IntPtr pClassLinkage, out IntPtr ppComputeShader);

        public static int CreateComputeShader(IntPtr device, byte[] bytecode, out IntPtr computeShader)
        {
            if (device == IntPtr.Zero || bytecode == null) { computeShader = IntPtr.Zero; return unchecked((int)0x80004003); }
            fixed (byte* pBytes = bytecode)
            {
                IntPtr methodPtr = (*(IntPtr**)device)[18];
                return Marshal.GetDelegateForFunctionPointer<CreateComputeShaderDelegate>(methodPtr)(
                    device, pBytes, (UIntPtr)bytecode.Length, IntPtr.Zero, out computeShader);
            }
        }

        // ID3D11DeviceContext::Map (Slot 14)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapDelegate(IntPtr thisPtr, IntPtr pResource, uint subresource, int mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);

        public static int Map(IntPtr context, IntPtr resource, uint subresource, int mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE mapped)
        {
            if (context == IntPtr.Zero) { mapped = default; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)context)[14];
            return Marshal.GetDelegateForFunctionPointer<MapDelegate>(methodPtr)(context, resource, subresource, mapType, mapFlags, out mapped);
        }

        // ID3D11DeviceContext::Unmap (Slot 15)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapDelegate(IntPtr thisPtr, IntPtr pResource, uint subresource);

        public static void Unmap(IntPtr context, IntPtr resource, uint subresource)
        {
            if (context == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)context)[15];
            Marshal.GetDelegateForFunctionPointer<UnmapDelegate>(methodPtr)(context, resource, subresource);
        }

        // ID3D11DeviceContext::Dispatch (Slot 41)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DispatchDelegate(IntPtr thisPtr, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ);

        public static void Dispatch(IntPtr context, uint x, uint y, uint z)
        {
            if (context == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)context)[41];
            Marshal.GetDelegateForFunctionPointer<DispatchDelegate>(methodPtr)(context, x, y, z);
        }

        // ID3D11DeviceContext::CopyResource (Slot 47)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyResourceDelegate(IntPtr thisPtr, IntPtr pDstResource, IntPtr pSrcResource);

        public static void CopyResource(IntPtr context, IntPtr dst, IntPtr src)
        {
            if (context == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)context)[47];
            Marshal.GetDelegateForFunctionPointer<CopyResourceDelegate>(methodPtr)(context, dst, src);
        }

        // ID3D11DeviceContext::UpdateSubresource (Slot 48)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UpdateSubresourceDelegate(IntPtr thisPtr, IntPtr pDstResource, uint dstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint srcRowPitch, uint srcDepthPitch);

        public static void UpdateSubresource(IntPtr context, IntPtr dst, IntPtr srcData)
        {
            if (context == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)context)[48];
            Marshal.GetDelegateForFunctionPointer<UpdateSubresourceDelegate>(methodPtr)(context, dst, 0, IntPtr.Zero, srcData, 0, 0);
        }

        // ID3D11DeviceContext::CSSetShaderResources (Slot 67)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CSSetShaderResourcesDelegate(IntPtr thisPtr, uint startSlot, uint numViews, IntPtr* ppShaderResourceViews);

        public static void CSSetShaderResources(IntPtr context, uint startSlot, IntPtr[] srvs)
        {
            if (context == IntPtr.Zero || srvs == null || srvs.Length == 0) return;
            fixed (IntPtr* ptr = srvs)
            {
                IntPtr methodPtr = (*(IntPtr**)context)[67];
                Marshal.GetDelegateForFunctionPointer<CSSetShaderResourcesDelegate>(methodPtr)(context, startSlot, (uint)srvs.Length, ptr);
            }
        }

        // ID3D11DeviceContext::CSSetUnorderedAccessViews (Slot 68)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CSSetUnorderedAccessViewsDelegate(IntPtr thisPtr, uint startSlot, uint numUAVs, IntPtr* ppUnorderedAccessViews, uint* pUAVInitialCounts);

        public static void CSSetUnorderedAccessViews(IntPtr context, uint startSlot, IntPtr[] uavs)
        {
            if (context == IntPtr.Zero || uavs == null || uavs.Length == 0) return;
            fixed (IntPtr* ptr = uavs)
            {
                uint* counts = stackalloc uint[uavs.Length];
                for (int i = 0; i < uavs.Length; i++) counts[i] = unchecked((uint)-1);

                IntPtr methodPtr = (*(IntPtr**)context)[68];
                Marshal.GetDelegateForFunctionPointer<CSSetUnorderedAccessViewsDelegate>(methodPtr)(context, startSlot, (uint)uavs.Length, ptr, counts);
            }
        }

        // ID3D11DeviceContext::CSSetShader (Slot 69)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CSSetShaderDelegate(IntPtr thisPtr, IntPtr pComputeShader, IntPtr* ppClassInstances, uint numClassInstances);

        public static void CSSetShader(IntPtr context, IntPtr computeShader)
        {
            if (context == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)context)[69];
            Marshal.GetDelegateForFunctionPointer<CSSetShaderDelegate>(methodPtr)(context, computeShader, null, 0);
        }

        // ID3D11DeviceContext::CSSetConstantBuffers (Slot 71)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CSSetConstantBuffersDelegate(IntPtr thisPtr, uint startSlot, uint numBuffers, IntPtr* ppConstantBuffers);

        public static void CSSetConstantBuffers(IntPtr context, uint startSlot, IntPtr buffer)
        {
            if (context == IntPtr.Zero) return;
            IntPtr* ptr = stackalloc IntPtr[1];
            ptr[0] = buffer;
            IntPtr methodPtr = (*(IntPtr**)context)[71];
            Marshal.GetDelegateForFunctionPointer<CSSetConstantBuffersDelegate>(methodPtr)(context, startSlot, 1, ptr);
        }
    }
}
