using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.DirectX.Native
{
    public enum D3D12_COMMAND_LIST_TYPE
    {
        DIRECT = 0,
        BUNDLE = 1,
        COMPUTE = 2,
        COPY = 3
    }

    [Flags]
    public enum D3D12_COMMAND_QUEUE_FLAGS
    {
        NONE = 0,
        DISABLE_GPU_TIMEOUT = 1
    }

    [Flags]
    public enum D3D12_RESOURCE_STATES
    {
        COMMON = 0,
        VERTEX_AND_CONSTANT_BUFFER = 0x1,
        INDEX_BUFFER = 0x2,
        RENDER_TARGET = 0x4,
        UNORDERED_ACCESS = 0x8,
        DEPTH_WRITE = 0x10,
        DEPTH_READ = 0x20,
        NON_PIXEL_SHADER_RESOURCE = 0x40,
        PIXEL_SHADER_RESOURCE = 0x80,
        INDIRECT_ARGUMENT = 0x200,
        COPY_DEST = 0x400,
        COPY_SOURCE = 0x800,
        GENERIC_READ = 0xAC3,
        PRESENT = 0
    }

    public enum D3D12_RESOURCE_BARRIER_TYPE
    {
        TRANSITION = 0,
        ALIASING = 1,
        UAV = 2
    }

    [Flags]
    public enum D3D12_RESOURCE_BARRIER_FLAGS
    {
        NONE = 0,
        BEGIN_ONLY = 1,
        END_ONLY = 2
    }

    public enum D3D12_HEAP_TYPE
    {
        DEFAULT = 1,
        UPLOAD = 2,
        READBACK = 3,
        CUSTOM = 4
    }

    public enum D3D12_RESOURCE_DIMENSION
    {
        UNKNOWN = 0,
        BUFFER = 1,
        TEXTURE1D = 2,
        TEXTURE2D = 3,
        TEXTURE3D = 4
    }

    [Flags]
    public enum D3D12_RESOURCE_FLAGS
    {
        NONE = 0,
        ALLOW_RENDER_TARGET = 0x1,
        ALLOW_DEPTH_STENCIL = 0x2,
        ALLOW_UNORDERED_ACCESS = 0x4,
        DENY_SHADER_RESOURCE = 0x8
    }

    [Flags]
    public enum D3D12_FENCE_FLAGS
    {
        NONE = 0,
        SHARED = 1,
        SHARED_CROSS_ADAPTER = 2,
        NON_MONITORED = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_COMMAND_QUEUE_DESC
    {
        public D3D12_COMMAND_LIST_TYPE Type;
        public int Priority;
        public D3D12_COMMAND_QUEUE_FLAGS Flags;
        public uint NodeMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_HEAP_PROPERTIES
    {
        public D3D12_HEAP_TYPE Type;
        public int CPUPageProperty;
        public int MemoryPoolPreference;
        public uint CreationNodeMask;
        public uint VisibleNodeMask;

        public static D3D12_HEAP_PROPERTIES Default => new D3D12_HEAP_PROPERTIES { Type = D3D12_HEAP_TYPE.DEFAULT, CreationNodeMask = 1, VisibleNodeMask = 1 };
        public static D3D12_HEAP_PROPERTIES Upload => new D3D12_HEAP_PROPERTIES { Type = D3D12_HEAP_TYPE.UPLOAD, CreationNodeMask = 1, VisibleNodeMask = 1 };
        public static D3D12_HEAP_PROPERTIES Readback => new D3D12_HEAP_PROPERTIES { Type = D3D12_HEAP_TYPE.READBACK, CreationNodeMask = 1, VisibleNodeMask = 1 };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_RESOURCE_DESC
    {
        public D3D12_RESOURCE_DIMENSION Dimension;
        public ulong Alignment;
        public ulong Width;
        public uint Height;
        public ushort DepthOrArraySize;
        public ushort MipLevels;
        public DXGI_FORMAT Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public int Layout;
        public D3D12_RESOURCE_FLAGS Flags;

        public static D3D12_RESOURCE_DESC Buffer(ulong sizeInBytes, D3D12_RESOURCE_FLAGS flags = D3D12_RESOURCE_FLAGS.NONE)
        {
            return new D3D12_RESOURCE_DESC
            {
                Dimension = D3D12_RESOURCE_DIMENSION.BUFFER,
                Alignment = 0,
                Width = sizeInBytes,
                Height = 1,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Layout = 1, // D3D12_TEXTURE_LAYOUT_ROW_MAJOR
                Flags = flags
            };
        }

        public static D3D12_RESOURCE_DESC Texture2D(
            DXGI_FORMAT format,
            ulong width,
            uint height,
            ushort mipLevels = 1,
            D3D12_RESOURCE_FLAGS flags = D3D12_RESOURCE_FLAGS.NONE)
        {
            return new D3D12_RESOURCE_DESC
            {
                Dimension = D3D12_RESOURCE_DIMENSION.TEXTURE2D,
                Alignment = 0,
                Width = width,
                Height = height,
                DepthOrArraySize = 1,
                MipLevels = mipLevels,
                Format = format,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Layout = 0, // D3D12_TEXTURE_LAYOUT_UNKNOWN
                Flags = flags
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_RESOURCE_TRANSITION_BARRIER
    {
        public IntPtr pResource;
        public uint Subresource;
        public D3D12_RESOURCE_STATES StateBefore;
        public D3D12_RESOURCE_STATES StateAfter;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct D3D12_RESOURCE_BARRIER
    {
        [FieldOffset(0)]
        public D3D12_RESOURCE_BARRIER_TYPE Type;
        [FieldOffset(4)]
        public D3D12_RESOURCE_BARRIER_FLAGS Flags;
        [FieldOffset(8)]
        public D3D12_RESOURCE_TRANSITION_BARRIER Transition;

        public static D3D12_RESOURCE_BARRIER CreateTransition(
            IntPtr pResource,
            D3D12_RESOURCE_STATES before,
            D3D12_RESOURCE_STATES after,
            uint subresource = 0xFFFFFFFF) // D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES
        {
            var b = default(D3D12_RESOURCE_BARRIER);
            b.Type = D3D12_RESOURCE_BARRIER_TYPE.TRANSITION;
            b.Flags = D3D12_RESOURCE_BARRIER_FLAGS.NONE;
            b.Transition.pResource = pResource;
            b.Transition.Subresource = subresource;
            b.Transition.StateBefore = before;
            b.Transition.StateAfter = after;
            return b;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_RANGE
    {
        public UIntPtr Begin;
        public UIntPtr End;
    }

    /// <summary>
    /// Pure C# Direct3D 12 Native Interop and VTable dispatcher.
    /// Eliminates C++ wrapper DLLs and uses direct CIL calli function pointers.
    /// </summary>
    public static unsafe class D3D12Native
    {
        public static readonly Guid IID_ID3D12Device = new Guid("189819f1-1db6-4b57-be54-1821339b85f7");
        public static readonly Guid IID_ID3D12CommandQueue = new Guid("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");
        public static readonly Guid IID_ID3D12CommandAllocator = new Guid("6102dee4-af59-4b09-b999-b44d73f09b24");
        public static readonly Guid IID_ID3D12GraphicsCommandList = new Guid("5b160d0f-ac1b-4185-8ba8-b3ae42a5a455");
        public static readonly Guid IID_ID3D12Resource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");
        public static readonly Guid IID_ID3D12Fence = new Guid("0a753dcf-c4d8-4b91-adf6-be5a60d95a76");

        [DllImport("d3d12.dll", EntryPoint = "D3D12CreateDevice", CallingConvention = CallingConvention.StdCall)]
        public static extern int D3D12CreateDevice(
            IntPtr pAdapter,
            D3D_FEATURE_LEVEL minimumFeatureLevel,
            [In] ref Guid riid,
            out IntPtr ppDevice);

        // =========================================================================
        // ID3D12Device
        // =========================================================================

        public static int CreateCommandQueue(IntPtr device, in D3D12_COMMAND_QUEUE_DESC desc, ref Guid riid, out IntPtr ppCommandQueue)
        {
            if (device == IntPtr.Zero) { ppCommandQueue = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[8];
            fixed (D3D12_COMMAND_QUEUE_DESC* pDesc = &desc)
            {
                return ((delegate* unmanaged[Stdcall]<IntPtr, D3D12_COMMAND_QUEUE_DESC*, ref Guid, out IntPtr, int>)methodPtr)(
                    device, pDesc, ref riid, out ppCommandQueue);
            }
        }

        public static int CreateCommandAllocator(IntPtr device, D3D12_COMMAND_LIST_TYPE type, ref Guid riid, out IntPtr ppCommandAllocator)
        {
            if (device == IntPtr.Zero) { ppCommandAllocator = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[9];
            return ((delegate* unmanaged[Stdcall]<IntPtr, D3D12_COMMAND_LIST_TYPE, ref Guid, out IntPtr, int>)methodPtr)(
                device, type, ref riid, out ppCommandAllocator);
        }

        public static int CommandAllocatorReset(IntPtr allocator)
        {
            if (allocator == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)allocator)[8];
            return ((delegate* unmanaged[Stdcall]<IntPtr, int>)methodPtr)(allocator);
        }

        public static int CreateGraphicsCommandList(
            IntPtr device,
            uint nodeMask,
            D3D12_COMMAND_LIST_TYPE type,
            IntPtr pCommandAllocator,
            IntPtr pInitialState,
            ref Guid riid,
            out IntPtr ppCommandList)
        {
            if (device == IntPtr.Zero) { ppCommandList = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[12];
            return ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_COMMAND_LIST_TYPE, IntPtr, IntPtr, ref Guid, out IntPtr, int>)methodPtr)(
                device, nodeMask, type, pCommandAllocator, pInitialState, ref riid, out ppCommandList);
        }

        public static int CreateCommittedResource(
            IntPtr device,
            in D3D12_HEAP_PROPERTIES heapProps,
            int heapFlags,
            in D3D12_RESOURCE_DESC desc,
            D3D12_RESOURCE_STATES initialResourceState,
            IntPtr pOptimizedClearValue,
            ref Guid riid,
            out IntPtr ppResource)
        {
            if (device == IntPtr.Zero) { ppResource = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[27];
            fixed (D3D12_HEAP_PROPERTIES* pHeap = &heapProps)
            fixed (D3D12_RESOURCE_DESC* pDesc = &desc)
            {
                return ((delegate* unmanaged[Stdcall]<IntPtr, D3D12_HEAP_PROPERTIES*, int, D3D12_RESOURCE_DESC*, D3D12_RESOURCE_STATES, IntPtr, ref Guid, out IntPtr, int>)methodPtr)(
                    device, pHeap, heapFlags, pDesc, initialResourceState, pOptimizedClearValue, ref riid, out ppResource);
            }
        }

        public static int CreateFence(IntPtr device, ulong initialValue, D3D12_FENCE_FLAGS flags, ref Guid riid, out IntPtr ppFence)
        {
            if (device == IntPtr.Zero) { ppFence = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)device)[36];
            return ((delegate* unmanaged[Stdcall]<IntPtr, ulong, D3D12_FENCE_FLAGS, ref Guid, out IntPtr, int>)methodPtr)(
                device, initialValue, flags, ref riid, out ppFence);
        }

        // =========================================================================
        // ID3D12CommandQueue
        // =========================================================================

        public static void CommandQueueExecuteCommandLists(IntPtr queue, uint numCommandLists, IntPtr* ppCommandLists)
        {
            if (queue == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)queue)[10];
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, void>)methodPtr)(queue, numCommandLists, ppCommandLists);
        }

        public static int CommandQueueSignal(IntPtr queue, IntPtr pFence, ulong value)
        {
            if (queue == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)queue)[14];
            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ulong, int>)methodPtr)(queue, pFence, value);
        }

        public static int CommandQueueWait(IntPtr queue, IntPtr pFence, ulong value)
        {
            if (queue == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)queue)[15];
            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ulong, int>)methodPtr)(queue, pFence, value);
        }

        // =========================================================================
        // ID3D12GraphicsCommandList
        // =========================================================================

        public static int CommandListClose(IntPtr list)
        {
            if (list == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)list)[9];
            return ((delegate* unmanaged[Stdcall]<IntPtr, int>)methodPtr)(list);
        }

        public static int CommandListReset(IntPtr list, IntPtr pAllocator, IntPtr pInitialState)
        {
            if (list == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)list)[10];
            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int>)methodPtr)(list, pAllocator, pInitialState);
        }

        public static void CommandListDrawInstanced(IntPtr list, uint vertexCount, uint instanceCount, uint startVertex, uint startInstance)
        {
            if (list == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)list)[12];
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, uint, void>)methodPtr)(list, vertexCount, instanceCount, startVertex, startInstance);
        }

        public static void CommandListDrawIndexedInstanced(IntPtr list, uint indexCount, uint instanceCount, uint startIndex, int baseVertex, uint startInstance)
        {
            if (list == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)list)[13];
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, int, uint, void>)methodPtr)(list, indexCount, instanceCount, startIndex, baseVertex, startInstance);
        }

        public static void CommandListResourceBarrier(IntPtr list, uint numBarriers, D3D12_RESOURCE_BARRIER* pBarriers)
        {
            if (list == IntPtr.Zero || numBarriers == 0 || pBarriers == null) return;
            IntPtr methodPtr = (*(IntPtr**)list)[26];
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_RESOURCE_BARRIER*, void>)methodPtr)(list, numBarriers, pBarriers);
        }

        // =========================================================================
        // ID3D12Fence
        // =========================================================================

        public static ulong FenceGetCompletedValue(IntPtr fence)
        {
            if (fence == IntPtr.Zero) return 0;
            IntPtr methodPtr = (*(IntPtr**)fence)[8];
            return ((delegate* unmanaged[Stdcall]<IntPtr, ulong>)methodPtr)(fence);
        }

        public static int FenceSetEventOnCompletion(IntPtr fence, ulong value, IntPtr hEvent)
        {
            if (fence == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)fence)[9];
            return ((delegate* unmanaged[Stdcall]<IntPtr, ulong, IntPtr, int>)methodPtr)(fence, value, hEvent);
        }

        public static int FenceSignal(IntPtr fence, ulong value)
        {
            if (fence == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)fence)[10];
            return ((delegate* unmanaged[Stdcall]<IntPtr, ulong, int>)methodPtr)(fence, value);
        }

        // =========================================================================
        // ID3D12Resource
        // =========================================================================

        public static int ResourceMap(IntPtr resource, uint subresource, in D3D12_RANGE? readRange, out IntPtr ppData)
        {
            if (resource == IntPtr.Zero) { ppData = IntPtr.Zero; return unchecked((int)0x80004003); }
            IntPtr methodPtr = (*(IntPtr**)resource)[8];
            if (readRange.HasValue)
            {
                var r = readRange.Value;
                return ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_RANGE*, out IntPtr, int>)methodPtr)(resource, subresource, &r, out ppData);
            }
            return ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_RANGE*, out IntPtr, int>)methodPtr)(resource, subresource, null, out ppData);
        }

        public static void ResourceUnmap(IntPtr resource, uint subresource, in D3D12_RANGE? writtenRange)
        {
            if (resource == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)resource)[9];
            if (writtenRange.HasValue)
            {
                var r = writtenRange.Value;
                ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_RANGE*, void>)methodPtr)(resource, subresource, &r);
                return;
            }
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, D3D12_RANGE*, void>)methodPtr)(resource, subresource, null);
        }
    }
}
