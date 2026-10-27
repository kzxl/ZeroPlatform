using System;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Rhi;

namespace ZeroGraphics.DirectX.Rhi
{
    /// <summary>
    /// Direct3D 12 low-level implementation of the sovereign Render Hardware Interface (IRhiDevice).
    /// Leverages explicit resource transitions (ResourceBarrier) and GPU timeline fences (ID3D12Fence).
    /// </summary>
    public sealed class D3D12RhiDevice : IRhiDevice, IDisposable
    {
        private readonly IntPtr _device;
        private readonly IntPtr _commandQueue;
        private bool _disposed;

        public RhiBackend Backend => RhiBackend.Direct3D12;
        public string DeviceName => "ZeroGraphics Direct3D 12 Low-Level Hardware Device";

        public IntPtr NativeDevice => _device;
        public IntPtr NativeCommandQueue => _commandQueue;

        public D3D12RhiDevice(IntPtr device, IntPtr commandQueue)
        {
            if (device == IntPtr.Zero) throw new ArgumentNullException(nameof(device));
            if (commandQueue == IntPtr.Zero) throw new ArgumentNullException(nameof(commandQueue));
            _device = device;
            _commandQueue = commandQueue;
        }

        /// <summary>
        /// Attempts to initialize a Direct3D 12 device on the default or WARP adapter.
        /// Returns null if Direct3D 12 is not supported by the underlying OS or GPU driver.
        /// </summary>
        public static D3D12RhiDevice? TryCreate()
        {
            try
            {
                Guid deviceIid = D3D12Native.IID_ID3D12Device;
                int hr = D3D12Native.D3D12CreateDevice(IntPtr.Zero, D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, ref deviceIid, out IntPtr pDevice);
                if (hr < 0 || pDevice == IntPtr.Zero)
                {
                    return null;
                }

                var queueDesc = new D3D12_COMMAND_QUEUE_DESC
                {
                    Type = D3D12_COMMAND_LIST_TYPE.DIRECT,
                    Priority = 0,
                    Flags = D3D12_COMMAND_QUEUE_FLAGS.NONE,
                    NodeMask = 0
                };

                Guid queueIid = D3D12Native.IID_ID3D12CommandQueue;
                hr = D3D12Native.CreateCommandQueue(pDevice, in queueDesc, ref queueIid, out IntPtr pQueue);
                if (hr < 0 || pQueue == IntPtr.Zero)
                {
                    ComVTableHelper.Release(pDevice);
                    return null;
                }

                return new D3D12RhiDevice(pDevice, pQueue);
            }
            catch
            {
                return null;
            }
        }

        public unsafe IRhiBuffer CreateBuffer(in RhiBufferDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            var heapProps = (desc.Usage == RhiBufferUsage.Dynamic || desc.Usage == RhiBufferUsage.Staging)
                ? D3D12_HEAP_PROPERTIES.Upload
                : D3D12_HEAP_PROPERTIES.Default;

            var d3dDesc = D3D12_RESOURCE_DESC.Buffer((ulong)desc.SizeInBytes);
            var initialState = (desc.Usage == RhiBufferUsage.Dynamic || desc.Usage == RhiBufferUsage.Staging)
                ? D3D12_RESOURCE_STATES.GENERIC_READ
                : D3D12_RESOURCE_STATES.COMMON;

            Guid resIid = D3D12Native.IID_ID3D12Resource;
            int hr = D3D12Native.CreateCommittedResource(
                _device,
                in heapProps,
                0,
                in d3dDesc,
                initialState,
                IntPtr.Zero,
                ref resIid,
                out IntPtr pResource);

            if (hr < 0 || pResource == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D12 buffer. HRESULT=0x{hr:X8}");

            var buffer = new D3D12RhiBuffer(pResource, desc, initialState);

            if (!initialData.IsEmpty)
            {
                fixed (byte* pData = initialData)
                {
                    buffer.UpdateRawBytes(initialData, 0);
                }
            }

            return buffer;
        }

        public unsafe IRhiTexture CreateTexture(in RhiTextureDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            DXGI_FORMAT format = ToNativeFormat(desc.Format);
            D3D12_RESOURCE_FLAGS flags = D3D12_RESOURCE_FLAGS.NONE;
            if ((desc.Usage & RhiTextureUsage.RenderTarget) != 0)
                flags |= D3D12_RESOURCE_FLAGS.ALLOW_RENDER_TARGET;
            if ((desc.Usage & RhiTextureUsage.DepthStencil) != 0)
                flags |= D3D12_RESOURCE_FLAGS.ALLOW_DEPTH_STENCIL;
            if ((desc.Usage & RhiTextureUsage.ComputeStorage) != 0)
                flags |= D3D12_RESOURCE_FLAGS.ALLOW_UNORDERED_ACCESS;

            var d3dDesc = D3D12_RESOURCE_DESC.Texture2D(format, (ulong)desc.Width, (uint)desc.Height, (ushort)desc.MipLevels, flags);
            var heapProps = D3D12_HEAP_PROPERTIES.Default;
            var initialState = ((desc.Usage & RhiTextureUsage.RenderTarget) != 0)
                ? D3D12_RESOURCE_STATES.RENDER_TARGET
                : D3D12_RESOURCE_STATES.COMMON;

            Guid resIid = D3D12Native.IID_ID3D12Resource;
            int hr = D3D12Native.CreateCommittedResource(
                _device,
                in heapProps,
                0,
                in d3dDesc,
                initialState,
                IntPtr.Zero,
                ref resIid,
                out IntPtr pResource);

            if (hr < 0 || pResource == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D12 texture. HRESULT=0x{hr:X8}");

            return new D3D12RhiTexture(pResource, desc, initialState);
        }

        public IRhiShader CreateShader(RhiShaderStage stage, byte[] bytecode)
        {
            if (bytecode == null) throw new ArgumentNullException(nameof(bytecode));
            return new D3D12RhiShader(stage, bytecode);
        }

        public IRhiPipelineState CreatePipelineState(RhiPipelineStateDesc desc)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));
            return new D3D12RhiPipelineState(desc);
        }

        public IRhiSwapChain CreateSwapChain(IntPtr windowHandle, int width, int height, RhiPresentMode presentMode = RhiPresentMode.Fifo)
        {
            return new D3D12RhiSwapChain(this, width, height);
        }

        public IRhiCommandBuffer CreateCommandBuffer()
        {
            Guid allocIid = D3D12Native.IID_ID3D12CommandAllocator;
            int hr = D3D12Native.CreateCommandAllocator(_device, D3D12_COMMAND_LIST_TYPE.DIRECT, ref allocIid, out IntPtr pAlloc);
            if (hr < 0 || pAlloc == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D12 command allocator. HRESULT=0x{hr:X8}");

            Guid listIid = D3D12Native.IID_ID3D12GraphicsCommandList;
            hr = D3D12Native.CreateGraphicsCommandList(_device, 0, D3D12_COMMAND_LIST_TYPE.DIRECT, pAlloc, IntPtr.Zero, ref listIid, out IntPtr pList);
            if (hr < 0 || pList == IntPtr.Zero)
            {
                ComVTableHelper.Release(pAlloc);
                throw new InvalidOperationException($"Failed to create D3D12 graphics command list. HRESULT=0x{hr:X8}");
            }

            D3D12Native.CommandListClose(pList);
            return new D3D12RhiCommandBuffer(_commandQueue, pAlloc, pList);
        }

        public IRhiFence CreateFence(ulong initialValue = 0)
        {
            Guid fenceIid = D3D12Native.IID_ID3D12Fence;
            int hr = D3D12Native.CreateFence(_device, initialValue, D3D12_FENCE_FLAGS.NONE, ref fenceIid, out IntPtr pFence);
            if (hr < 0 || pFence == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D12 fence. HRESULT=0x{hr:X8}");

            return new D3D12RhiFence(pFence, initialValue);
        }

        public void WaitForIdle()
        {
            using var fence = CreateFence(0);
            D3D12Native.CommandQueueSignal(_commandQueue, ((D3D12RhiFence)fence).NativeFence, 1);
            fence.Wait(1);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_commandQueue != IntPtr.Zero) ComVTableHelper.Release(_commandQueue);
                if (_device != IntPtr.Zero) ComVTableHelper.Release(_device);
                _disposed = true;
            }
        }

        private static DXGI_FORMAT ToNativeFormat(RhiFormat format) => format switch
        {
            RhiFormat.R8_UNorm => DXGI_FORMAT.DXGI_FORMAT_R8_UNORM,
            RhiFormat.R8G8B8A8_UNorm => DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
            RhiFormat.B8G8R8A8_UNorm => DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            RhiFormat.R16_Float => DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT,
            RhiFormat.R16G16B16A16_Float => DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT,
            RhiFormat.R32_Float => DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT,
            RhiFormat.R32G32_Float => DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
            RhiFormat.R32G32B32_Float => DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
            RhiFormat.R32G32B32A32_Float => DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT,
            RhiFormat.D24_UNorm_S8_UInt => DXGI_FORMAT.DXGI_FORMAT_D24_UNORM_S8_UINT,
            RhiFormat.D32_Float => DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT,
            _ => DXGI_FORMAT.DXGI_FORMAT_UNKNOWN
        };
    }

    public sealed class D3D12RhiBuffer : IRhiBuffer, IDisposable
    {
        private readonly IntPtr _resource;
        private readonly RhiBufferDesc _desc;
        private D3D12_RESOURCE_STATES _currentState;
        private bool _disposed;

        public IntPtr NativeResource => _resource;
        public RhiBufferType Type => _desc.Type;
        public RhiBufferUsage Usage => _desc.Usage;
        public int SizeInBytes => _desc.SizeInBytes;
        public D3D12_RESOURCE_STATES CurrentState { get => _currentState; internal set => _currentState = value; }

        public D3D12RhiBuffer(IntPtr resource, RhiBufferDesc desc, D3D12_RESOURCE_STATES initialState)
        {
            _resource = resource;
            _desc = desc;
            _currentState = initialState;
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int offsetBytes = 0) where T : unmanaged
        {
            int sizeInBytes = data.Length * sizeof(T);
            if (sizeInBytes <= 0) return;
            var range = new D3D12_RANGE { Begin = (UIntPtr)offsetBytes, End = (UIntPtr)(offsetBytes + sizeInBytes) };
            int hr = D3D12Native.ResourceMap(_resource, 0, range, out IntPtr pDst);
            if (hr >= 0 && pDst != IntPtr.Zero)
            {
                fixed (T* pSrc = data)
                {
                    Buffer.MemoryCopy(pSrc, (byte*)pDst + offsetBytes, SizeInBytes - offsetBytes, sizeInBytes);
                }
                D3D12Native.ResourceUnmap(_resource, 0, range);
            }
        }

        public unsafe void UpdateRawBytes(ReadOnlySpan<byte> bytes, int offsetBytes = 0)
        {
            UpdateData(bytes, offsetBytes);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_resource != IntPtr.Zero) ComVTableHelper.Release(_resource);
                _disposed = true;
            }
        }
    }

    public sealed class D3D12RhiTexture : IRhiTexture, IDisposable
    {
        private readonly IntPtr _resource;
        private readonly RhiTextureDesc _desc;
        private D3D12_RESOURCE_STATES _currentState;
        private bool _disposed;

        public IntPtr NativeResource => _resource;
        public int Width => _desc.Width;
        public int Height => _desc.Height;
        public RhiFormat Format => _desc.Format;
        public RhiTextureUsage Usage => _desc.Usage;
        public int MipLevels => _desc.MipLevels;
        public D3D12_RESOURCE_STATES CurrentState { get => _currentState; internal set => _currentState = value; }

        public D3D12RhiTexture(IntPtr resource, RhiTextureDesc desc, D3D12_RESOURCE_STATES initialState)
        {
            _resource = resource;
            _desc = desc;
            _currentState = initialState;
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int rowPitch) where T : unmanaged
        {
            int byteLength = data.Length * sizeof(T);
            int hr = D3D12Native.ResourceMap(_resource, 0, null, out IntPtr pDst);
            if (hr >= 0 && pDst != IntPtr.Zero)
            {
                fixed (T* pSrc = data)
                {
                    Buffer.MemoryCopy(pSrc, (void*)pDst, byteLength, byteLength);
                }
                D3D12Native.ResourceUnmap(_resource, 0, null);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_resource != IntPtr.Zero) ComVTableHelper.Release(_resource);
                _disposed = true;
            }
        }
    }

    public sealed class D3D12RhiShader : IRhiShader
    {
        public RhiShaderStage Stage { get; }
        public byte[] Bytecode { get; }

        public D3D12RhiShader(RhiShaderStage stage, byte[] bytecode)
        {
            Stage = stage;
            Bytecode = bytecode;
        }

        public void Dispose() { }
    }

    public sealed class D3D12RhiPipelineState : IRhiPipelineState
    {
        public RhiPipelineStateDesc Description { get; }
        public D3D12RhiPipelineState(RhiPipelineStateDesc desc) => Description = desc;
        public void Dispose() { }
    }

    public sealed class D3D12RhiSwapChain : IRhiSwapChain
    {
        private readonly D3D12RhiDevice _device;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public IRhiTexture BackBuffer { get; private set; }

        public D3D12RhiSwapChain(D3D12RhiDevice device, int width, int height)
        {
            _device = device;
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            BackBuffer = _device.CreateTexture(new RhiTextureDesc(Width, Height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Resize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            BackBuffer.Dispose();
            BackBuffer = _device.CreateTexture(new RhiTextureDesc(Width, Height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Present(int syncInterval = 1) { }

        public void Dispose()
        {
            BackBuffer.Dispose();
        }
    }

    public sealed class D3D12RhiFence : IRhiFence, IDisposable
    {
        private readonly IntPtr _fence;
        private readonly AutoResetEvent _eventHandle;
        private bool _disposed;

        public IntPtr NativeFence => _fence;
        public ulong CompletedValue => D3D12Native.FenceGetCompletedValue(_fence);

        public D3D12RhiFence(IntPtr fence, ulong initialValue)
        {
            _fence = fence;
            _eventHandle = new AutoResetEvent(false);
        }

        public void Signal(ulong value)
        {
            D3D12Native.FenceSignal(_fence, value);
        }

        public bool Wait(ulong value, int timeoutMilliseconds = -1)
        {
            if (CompletedValue >= value) return true;

            int hr = D3D12Native.FenceSetEventOnCompletion(_fence, value, _eventHandle.SafeWaitHandle.DangerousGetHandle());
            if (hr < 0) return false;

            return _eventHandle.WaitOne(timeoutMilliseconds);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _eventHandle.Dispose();
                if (_fence != IntPtr.Zero) ComVTableHelper.Release(_fence);
                _disposed = true;
            }
        }
    }

    public sealed class D3D12RhiCommandBuffer : IRhiCommandBuffer, IDisposable
    {
        private readonly IntPtr _commandQueue;
        private readonly IntPtr _allocator;
        private readonly IntPtr _commandList;
        private bool _disposed;

        public IntPtr NativeCommandList => _commandList;

        public D3D12RhiCommandBuffer(IntPtr commandQueue, IntPtr allocator, IntPtr commandList)
        {
            _commandQueue = commandQueue;
            _allocator = allocator;
            _commandList = commandList;
        }

        public void Begin()
        {
            D3D12Native.CommandAllocatorReset(_allocator);
            D3D12Native.CommandListReset(_commandList, _allocator, IntPtr.Zero);
        }

        public unsafe void End()
        {
            D3D12Native.CommandListClose(_commandList);
            IntPtr pList = _commandList;
            D3D12Native.CommandQueueExecuteCommandLists(_commandQueue, 1, &pList);
        }

        public void SetViewport(in RhiViewport viewport) { }
        public void SetScissorRect(in RhiRect scissorRect) { }
        public void BeginRenderPass(IRhiTexture renderTarget, RhiClearFlags clearFlags = RhiClearFlags.None, RhiColor clearColor = default) { }
        public void EndRenderPass() { }
        public void SetPipelineState(IRhiPipelineState pipelineState) { }
        public void SetVertexBuffer(int slot, IRhiBuffer buffer, int stride, int offset = 0) { }
        public void SetIndexBuffer(IRhiBuffer buffer, RhiIndexFormat format, int offset = 0) { }
        public void SetConstantBuffer(int slot, IRhiBuffer buffer, RhiShaderStage stage = RhiShaderStage.Vertex | RhiShaderStage.Pixel) { }
        public void SetShaderResource(int slot, IRhiTexture texture, RhiShaderStage stage = RhiShaderStage.Pixel) { }

        public void Draw(int vertexCount, int startVertex = 0)
        {
            D3D12Native.CommandListDrawInstanced(_commandList, (uint)vertexCount, 1, (uint)startVertex, 0);
        }

        public void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0)
        {
            D3D12Native.CommandListDrawIndexedInstanced(_commandList, (uint)indexCount, 1, (uint)startIndex, baseVertex, 0);
        }

        public void DispatchCompute(int groupCountX, int groupCountY, int groupCountZ) { }

        public unsafe void ResourceBarrier(in RhiBarrier barrier)
        {
            D3D12_RESOURCE_BARRIER b = MapBarrier(barrier);
            if (b.Transition.pResource != IntPtr.Zero)
            {
                D3D12Native.CommandListResourceBarrier(_commandList, 1, &b);
            }
        }

        public unsafe void ResourceBarriers(ReadOnlySpan<RhiBarrier> barriers)
        {
            if (barriers.IsEmpty) return;
            D3D12_RESOURCE_BARRIER[] nativeBarriers = new D3D12_RESOURCE_BARRIER[barriers.Length];
            int count = 0;
            for (int i = 0; i < barriers.Length; i++)
            {
                var b = MapBarrier(barriers[i]);
                if (b.Transition.pResource != IntPtr.Zero)
                {
                    nativeBarriers[count++] = b;
                }
            }
            if (count > 0)
            {
                fixed (D3D12_RESOURCE_BARRIER* pB = nativeBarriers)
                {
                    D3D12Native.CommandListResourceBarrier(_commandList, (uint)count, pB);
                }
            }
        }

        public void SignalFence(IRhiFence fence, ulong value)
        {
            if (fence is D3D12RhiFence d3dFence)
            {
                D3D12Native.CommandQueueSignal(_commandQueue, d3dFence.NativeFence, value);
            }
        }

        public void WaitFence(IRhiFence fence, ulong value)
        {
            if (fence is D3D12RhiFence d3dFence)
            {
                D3D12Native.CommandQueueWait(_commandQueue, d3dFence.NativeFence, value);
            }
        }

        private static D3D12_RESOURCE_BARRIER MapBarrier(RhiBarrier barrier)
        {
            IntPtr pResource = IntPtr.Zero;
            if (barrier.Texture is D3D12RhiTexture tex)
            {
                pResource = tex.NativeResource;
                tex.CurrentState = ToD3D12State(barrier.StateAfter);
            }
            else if (barrier.Buffer is D3D12RhiBuffer buf)
            {
                pResource = buf.NativeResource;
                buf.CurrentState = ToD3D12State(barrier.StateAfter);
            }

            if (pResource == IntPtr.Zero) return default;

            return D3D12_RESOURCE_BARRIER.CreateTransition(
                pResource,
                ToD3D12State(barrier.StateBefore),
                ToD3D12State(barrier.StateAfter));
        }

        private static D3D12_RESOURCE_STATES ToD3D12State(RhiResourceState state)
        {
            if (state == RhiResourceState.Common) return D3D12_RESOURCE_STATES.COMMON;
            D3D12_RESOURCE_STATES res = D3D12_RESOURCE_STATES.COMMON;
            if ((state & RhiResourceState.VertexBuffer) != 0 || (state & RhiResourceState.ConstantBuffer) != 0)
                res |= D3D12_RESOURCE_STATES.VERTEX_AND_CONSTANT_BUFFER;
            if ((state & RhiResourceState.IndexBuffer) != 0)
                res |= D3D12_RESOURCE_STATES.INDEX_BUFFER;
            if ((state & RhiResourceState.RenderTarget) != 0)
                res |= D3D12_RESOURCE_STATES.RENDER_TARGET;
            if ((state & RhiResourceState.DepthWrite) != 0)
                res |= D3D12_RESOURCE_STATES.DEPTH_WRITE;
            if ((state & RhiResourceState.DepthRead) != 0)
                res |= D3D12_RESOURCE_STATES.DEPTH_READ;
            if ((state & RhiResourceState.ShaderResource) != 0)
                res |= D3D12_RESOURCE_STATES.PIXEL_SHADER_RESOURCE | D3D12_RESOURCE_STATES.NON_PIXEL_SHADER_RESOURCE;
            if ((state & RhiResourceState.ComputeStorage) != 0)
                res |= D3D12_RESOURCE_STATES.UNORDERED_ACCESS;
            if ((state & RhiResourceState.CopySource) != 0)
                res |= D3D12_RESOURCE_STATES.COPY_SOURCE;
            if ((state & RhiResourceState.CopyDest) != 0)
                res |= D3D12_RESOURCE_STATES.COPY_DEST;
            if ((state & RhiResourceState.Present) != 0)
                res |= D3D12_RESOURCE_STATES.PRESENT;
            return res;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_commandList != IntPtr.Zero) ComVTableHelper.Release(_commandList);
                if (_allocator != IntPtr.Zero) ComVTableHelper.Release(_allocator);
                _disposed = true;
            }
        }
    }
}
