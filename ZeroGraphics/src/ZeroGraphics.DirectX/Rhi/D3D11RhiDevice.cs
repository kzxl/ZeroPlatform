using System;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Pipeline;
using ZeroGraphics.Rhi;

namespace ZeroGraphics.DirectX.Rhi
{
    /// <summary>
    /// Direct3D 11 implementation of the sovereign Render Hardware Interface (IRhiDevice).
    /// Leverages COM VTable calls with zero external dependencies and hardware flip model.
    /// </summary>
    public sealed class D3D11RhiDevice : IRhiDevice
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        public RhiBackend Backend => RhiBackend.Direct3D11;
        public string DeviceName => "ZeroGraphics Direct3D 11 Hardware Rasterizer";

        public D3D11Device NativeDevice => _device;
        public D3D11DeviceContext NativeContext => _context;

        public D3D11RhiDevice() : this(D3D11DeviceManager.Device, D3D11DeviceManager.Context)
        {
        }

        public D3D11RhiDevice(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static IRhiDevice GetOrCreateShared()
        {
            D3D11DeviceManager.EnsureInitialized();
            return new D3D11RhiDevice();
        }

        public unsafe IRhiBuffer CreateBuffer(in RhiBufferDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            D3D11_BUFFER_DESC nativeDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)desc.SizeInBytes,
                Usage = ToNativeUsage(desc.Usage),
                BindFlags = ToNativeBindFlags(desc.Type),
                CPUAccessFlags = ToNativeCpuAccess(desc.Usage),
                MiscFlags = 0,
                StructureByteStride = 0
            };

            D3D11Buffer buffer;
            if (!initialData.IsEmpty)
            {
                fixed (byte* pData = initialData)
                {
                    D3D11_SUBRESOURCE_DATA subData = new D3D11_SUBRESOURCE_DATA
                    {
                        pSysMem = (IntPtr)pData,
                        SysMemPitch = (uint)desc.SizeInBytes,
                        SysMemSlicePitch = 0
                    };
                    buffer = _device.CreateBuffer(ref nativeDesc, &subData);
                }
            }
            else
            {
                buffer = _device.CreateBuffer(ref nativeDesc, null);
            }

            return new D3D11RhiBuffer(buffer, desc, _context);
        }

        public unsafe IRhiTexture CreateTexture(in RhiTextureDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            DXGI_FORMAT format = ToNativeFormat(desc.Format);
            D3D11_BIND_FLAG bindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
            if ((desc.Usage & RhiTextureUsage.RenderTarget) != 0)
                bindFlags |= D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET;
            if ((desc.Usage & RhiTextureUsage.DepthStencil) != 0)
                bindFlags |= D3D11_BIND_FLAG.D3D11_BIND_DEPTH_STENCIL;
            if ((desc.Usage & RhiTextureUsage.ComputeStorage) != 0)
                bindFlags |= D3D11_BIND_FLAG.D3D11_BIND_UNORDERED_ACCESS;

            D3D11_TEXTURE2D_DESC nativeDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)desc.Width,
                Height = (uint)desc.Height,
                MipLevels = (uint)desc.MipLevels,
                ArraySize = 1,
                Format = format,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = bindFlags,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };

            D3D11Texture2D texture;
            if (!initialData.IsEmpty)
            {
                fixed (byte* pData = initialData)
                {
                    int bpp = GetBytesPerPixel(desc.Format);
                    D3D11_SUBRESOURCE_DATA subData = new D3D11_SUBRESOURCE_DATA
                    {
                        pSysMem = (IntPtr)pData,
                        SysMemPitch = (uint)(desc.Width * bpp),
                        SysMemSlicePitch = 0
                    };
                    texture = _device.CreateTexture2D(ref nativeDesc, (IntPtr)(&subData));
                }
            }
            else
            {
                texture = _device.CreateTexture2D(ref nativeDesc, IntPtr.Zero);
            }

            D3D11RenderTargetView? rtv = null;
            if ((desc.Usage & RhiTextureUsage.RenderTarget) != 0)
            {
                rtv = _device.CreateRenderTargetView(texture.Handle, IntPtr.Zero);
            }

            D3D11ShaderResourceView? srv = null;
            if ((desc.Usage & RhiTextureUsage.ShaderResource) != 0)
            {
                srv = _device.CreateShaderResourceView(texture.Handle, IntPtr.Zero);
            }

            return new D3D11RhiTexture(texture, desc, rtv, srv, _context);
        }

        public IRhiShader CreateShader(RhiShaderStage stage, byte[] bytecode)
        {
            if (bytecode == null || bytecode.Length == 0)
                throw new ArgumentException("Bytecode cannot be null or empty.", nameof(bytecode));

            switch (stage)
            {
                case RhiShaderStage.Vertex:
                    var vs = _device.CreateVertexShader(bytecode);
                    return new D3D11RhiShader(stage, bytecode, vs);
                case RhiShaderStage.Pixel:
                    var ps = _device.CreatePixelShader(bytecode);
                    return new D3D11RhiShader(stage, bytecode, ps);
                default:
                    return new D3D11RhiShader(stage, bytecode, null);
            }
        }

        public IRhiPipelineState CreatePipelineState(RhiPipelineStateDesc desc)
        {
            D3D11InputLayout? inputLayout = null;
            if (desc.InputLayout != null && desc.InputLayout.Length > 0 && desc.VertexShader != null)
            {
                IntPtr[] semanticPtrs = new IntPtr[desc.InputLayout.Length];
                try
                {
                    D3D11_INPUT_ELEMENT_DESC[] elements = new D3D11_INPUT_ELEMENT_DESC[desc.InputLayout.Length];
                    for (int i = 0; i < desc.InputLayout.Length; i++)
                    {
                        var elem = desc.InputLayout[i];
                        semanticPtrs[i] = Marshal.StringToHGlobalAnsi(elem.SemanticName);
                        elements[i] = new D3D11_INPUT_ELEMENT_DESC
                        {
                            SemanticName = semanticPtrs[i],
                            SemanticIndex = (uint)elem.SemanticIndex,
                            Format = ToNativeFormat(elem.Format),
                            InputSlot = (uint)elem.Slot,
                            AlignedByteOffset = (uint)elem.Offset,
                            InputSlotClass = 0, // D3D11_INPUT_PER_VERTEX_DATA
                            InstanceDataStepRate = 0
                        };
                    }
                    inputLayout = _device.CreateInputLayout(elements, desc.VertexShader.Bytecode);
                }
                finally
                {
                    for (int i = 0; i < semanticPtrs.Length; i++)
                    {
                        if (semanticPtrs[i] != IntPtr.Zero)
                            Marshal.FreeHGlobal(semanticPtrs[i]);
                    }
                }
            }

            return new D3D11RhiPipelineState(desc, inputLayout);
        }

        public IRhiSwapChain CreateSwapChain(IntPtr windowHandle, int width, int height, RhiPresentMode presentMode = RhiPresentMode.Fifo)
        {
            var hwndChain = new HwndSwapChain(windowHandle, width, height);
            return new D3D11RhiSwapChain(hwndChain, this);
        }

        public IRhiCommandBuffer CreateCommandBuffer()
        {
            return new D3D11RhiCommandBuffer(_context);
        }

        public IRhiFence CreateFence(ulong initialValue = 0)
        {
            return new D3D11RhiFence(_context, initialValue);
        }

        public void Dispose()
        {
            // Shared device management: we do not dispose the singleton D3D11DeviceManager here
        }

        #region Format and Flag Converters
        internal static DXGI_FORMAT ToNativeFormat(RhiFormat format)
        {
            switch (format)
            {
                case RhiFormat.R8_UNorm: return DXGI_FORMAT.DXGI_FORMAT_R8_UNORM;
                case RhiFormat.R8G8B8A8_UNorm: return DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM;
                case RhiFormat.B8G8R8A8_UNorm: return DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
                case RhiFormat.R16_Float: return DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT;
                case RhiFormat.R16G16B16A16_Float: return DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT;
                case RhiFormat.R32_Float: return DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT;
                case RhiFormat.R32G32_Float: return DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT;
                case RhiFormat.R32G32B32_Float: return DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT;
                case RhiFormat.R32G32B32A32_Float: return DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT;
                case RhiFormat.D24_UNorm_S8_UInt: return DXGI_FORMAT.DXGI_FORMAT_D24_UNORM_S8_UINT;
                case RhiFormat.D32_Float: return DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT;
                default: return DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM;
            }
        }

        internal static D3D11_USAGE ToNativeUsage(RhiBufferUsage usage)
        {
            switch (usage)
            {
                case RhiBufferUsage.Dynamic: return D3D11_USAGE.D3D11_USAGE_DYNAMIC;
                case RhiBufferUsage.Immutable: return D3D11_USAGE.D3D11_USAGE_IMMUTABLE;
                case RhiBufferUsage.Staging: return D3D11_USAGE.D3D11_USAGE_STAGING;
                default: return D3D11_USAGE.D3D11_USAGE_DEFAULT;
            }
        }

        internal static D3D11_BIND_FLAG ToNativeBindFlags(RhiBufferType type)
        {
            switch (type)
            {
                case RhiBufferType.Vertex: return D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER;
                case RhiBufferType.Index: return D3D11_BIND_FLAG.D3D11_BIND_INDEX_BUFFER;
                case RhiBufferType.Constant: return D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER;
                default: return 0;
            }
        }

        internal static D3D11_CPU_ACCESS_FLAG ToNativeCpuAccess(RhiBufferUsage usage)
        {
            switch (usage)
            {
                case RhiBufferUsage.Dynamic: return D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE;
                case RhiBufferUsage.Staging: return D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE;
                default: return 0;
            }
        }

        internal static int GetBytesPerPixel(RhiFormat format)
        {
            switch (format)
            {
                case RhiFormat.R8_UNorm: return 1;
                case RhiFormat.R16_Float: return 2;
                case RhiFormat.R8G8B8A8_UNorm:
                case RhiFormat.B8G8R8A8_UNorm:
                case RhiFormat.D24_UNorm_S8_UInt:
                case RhiFormat.R32_Float:
                case RhiFormat.D32_Float: return 4;
                case RhiFormat.R16G16B16A16_Float:
                case RhiFormat.R32G32_Float: return 8;
                case RhiFormat.R32G32B32_Float: return 12;
                case RhiFormat.R32G32B32A32_Float: return 16;
                default: return 4;
            }
        }
        #endregion
    }

    internal sealed class D3D11RhiBuffer : IRhiBuffer
    {
        private readonly D3D11Buffer _nativeBuffer;
        private readonly D3D11DeviceContext _context;

        public RhiBufferType Type { get; }
        public RhiBufferUsage Usage { get; }
        public int SizeInBytes { get; }
        public D3D11Buffer NativeBuffer => _nativeBuffer;

        public D3D11RhiBuffer(D3D11Buffer nativeBuffer, in RhiBufferDesc desc, D3D11DeviceContext context)
        {
            _nativeBuffer = nativeBuffer;
            _context = context;
            Type = desc.Type;
            Usage = desc.Usage;
            SizeInBytes = desc.SizeInBytes;
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int offsetBytes = 0) where T : unmanaged
        {
            int byteLength = data.Length * sizeof(T);
            fixed (T* ptr = data)
            {
                if (Usage == RhiBufferUsage.Dynamic)
                {
                    if (_context.Map(_nativeBuffer, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, out var mapped) >= 0)
                    {
                        try
                        {
                            byte* dst = (byte*)mapped.pData + offsetBytes;
                            Buffer.MemoryCopy(ptr, dst, SizeInBytes - offsetBytes, byteLength);
                        }
                        finally
                        {
                            _context.Unmap(_nativeBuffer, 0);
                        }
                    }
                }
                else
                {
                    ComVTableHelper.UpdateSubresource(_context.Handle, _nativeBuffer.Handle, 0, IntPtr.Zero, (IntPtr)ptr, (uint)byteLength, 0);
                }
            }
        }

        public void Dispose()
        {
            _nativeBuffer.Dispose();
        }
    }

    internal sealed class D3D11RhiTexture : IRhiTexture
    {
        private readonly D3D11Texture2D _nativeTexture;
        private readonly D3D11RenderTargetView? _rtv;
        private readonly D3D11ShaderResourceView? _srv;
        private readonly D3D11DeviceContext _context;

        public int Width { get; }
        public int Height { get; }
        public RhiFormat Format { get; }
        public RhiTextureUsage Usage { get; }
        public int MipLevels { get; }

        public D3D11Texture2D NativeTexture => _nativeTexture;
        public D3D11RenderTargetView? NativeRtv => _rtv;
        public D3D11ShaderResourceView? NativeSrv => _srv;

        public D3D11RhiTexture(
            D3D11Texture2D nativeTexture,
            in RhiTextureDesc desc,
            D3D11RenderTargetView? rtv,
            D3D11ShaderResourceView? srv,
            D3D11DeviceContext context)
        {
            _nativeTexture = nativeTexture;
            _rtv = rtv;
            _srv = srv;
            _context = context;
            Width = desc.Width;
            Height = desc.Height;
            Format = desc.Format;
            Usage = desc.Usage;
            MipLevels = desc.MipLevels;
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int rowPitch) where T : unmanaged
        {
            int byteLength = data.Length * sizeof(T);
            fixed (T* ptr = data)
            {
                ComVTableHelper.UpdateSubresource(_context.Handle, _nativeTexture.Handle, 0, IntPtr.Zero, (IntPtr)ptr, (uint)rowPitch, (uint)byteLength);
            }
        }

        public void Dispose()
        {
            _rtv?.Dispose();
            _srv?.Dispose();
            _nativeTexture.Dispose();
        }
    }

    internal sealed class D3D11RhiShader : IRhiShader
    {
        public RhiShaderStage Stage { get; }
        public byte[] Bytecode { get; }
        public ComObjectWrapper? NativeShader { get; }

        public D3D11RhiShader(RhiShaderStage stage, byte[] bytecode, ComObjectWrapper? nativeShader)
        {
            Stage = stage;
            Bytecode = bytecode;
            NativeShader = nativeShader;
        }

        public void Dispose()
        {
            NativeShader?.Dispose();
        }
    }

    internal sealed class D3D11RhiPipelineState : IRhiPipelineState
    {
        public RhiPipelineStateDesc Description { get; }
        public D3D11InputLayout? NativeInputLayout { get; }

        public D3D11RhiPipelineState(RhiPipelineStateDesc description, D3D11InputLayout? nativeInputLayout)
        {
            Description = description;
            NativeInputLayout = nativeInputLayout;
        }

        public void Dispose()
        {
            NativeInputLayout?.Dispose();
        }
    }

    internal sealed class D3D11RhiSwapChain : IRhiSwapChain
    {
        private readonly HwndSwapChain _swapChain;
        private readonly D3D11RhiDevice _device;
        private IRhiTexture? _backBufferTexture;

        public int Width => _swapChain.Width;
        public int Height => _swapChain.Height;
        public IRhiTexture BackBuffer
        {
            get
            {
                if (_backBufferTexture == null && _swapChain.RenderTargetView != null)
                {
                    var desc = new RhiTextureDesc(Width, Height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget);
                    _backBufferTexture = new D3D11RhiTexture(
                        new D3D11Texture2D(_swapChain.RenderTargetView.Handle, default),
                        desc,
                        _swapChain.RenderTargetView,
                        null,
                        _device.NativeContext);
                }
                return _backBufferTexture!;
            }
        }

        public D3D11RhiSwapChain(HwndSwapChain swapChain, D3D11RhiDevice device)
        {
            _swapChain = swapChain;
            _device = device;
        }

        public void Resize(int width, int height)
        {
            _backBufferTexture = null;
            _swapChain.Resize(width, height);
        }

        public void Present(int syncInterval = 1)
        {
            _swapChain.Present((uint)Math.Max(0, syncInterval));
        }

        public void Dispose()
        {
            _backBufferTexture = null;
            _swapChain.Dispose();
        }
    }

    internal sealed class D3D11RhiCommandBuffer : IRhiCommandBuffer
    {
        private readonly D3D11DeviceContext _context;

        public D3D11RhiCommandBuffer(D3D11DeviceContext context)
        {
            _context = context;
        }

        public void Begin() { }
        public void End() { }

        public void SetViewport(in RhiViewport viewport)
        {
            D3D11_VIEWPORT vp = new D3D11_VIEWPORT
            {
                TopLeftX = viewport.X,
                TopLeftY = viewport.Y,
                Width = viewport.Width,
                Height = viewport.Height,
                MinDepth = viewport.MinDepth,
                MaxDepth = viewport.MaxDepth
            };
            _context.RSSetViewports(vp);
        }

        public void SetScissorRect(in RhiRect scissorRect)
        {
            D3D11_RECT rect = new D3D11_RECT
            {
                Left = scissorRect.Left,
                Top = scissorRect.Top,
                Right = scissorRect.Right,
                Bottom = scissorRect.Bottom
            };
            ComVTableHelper.RSSetScissorRects(_context.Handle, 1, new[] { rect });
        }

        public void BeginRenderPass(IRhiTexture renderTarget, RhiClearFlags clearFlags = RhiClearFlags.None, RhiColor clearColor = default)
        {
            if (renderTarget is D3D11RhiTexture d3dTex && d3dTex.NativeRtv != null)
            {
                _context.OMSetRenderTargets(d3dTex.NativeRtv, IntPtr.Zero);
                if ((clearFlags & RhiClearFlags.Color) != 0)
                {
                    _context.ClearRenderTargetView(d3dTex.NativeRtv, new[] { clearColor.R, clearColor.G, clearColor.B, clearColor.A });
                }
            }
        }

        public void EndRenderPass()
        {
            ComVTableHelper.OMSetRenderTargets(_context.Handle, 0, Array.Empty<IntPtr>(), IntPtr.Zero);
        }

        public void SetPipelineState(IRhiPipelineState pipelineState)
        {
            if (pipelineState is D3D11RhiPipelineState d3dState)
            {
                if (d3dState.NativeInputLayout != null)
                    _context.IASetInputLayout(d3dState.NativeInputLayout);

                if (d3dState.Description.VertexShader is D3D11RhiShader vs && vs.NativeShader is D3D11VertexShader d3dVs)
                    _context.VSSetShader(d3dVs);

                if (d3dState.Description.PixelShader is D3D11RhiShader ps && ps.NativeShader is D3D11PixelShader d3dPs)
                    _context.PSSetShader(d3dPs);

                D3D11_PRIMITIVE_TOPOLOGY top = ToNativeTopology(d3dState.Description.PrimitiveTopology);
                _context.IASetPrimitiveTopology(top);
            }
        }

        public void SetVertexBuffer(int slot, IRhiBuffer buffer, int stride, int offset = 0)
        {
            if (buffer is D3D11RhiBuffer d3dBuf)
            {
                _context.IASetVertexBuffers((uint)slot, d3dBuf.NativeBuffer, (uint)stride, (uint)offset);
            }
        }

        public void SetIndexBuffer(IRhiBuffer buffer, RhiIndexFormat format, int offset = 0)
        {
            if (buffer is D3D11RhiBuffer d3dBuf)
            {
                DXGI_FORMAT nativeFmt = (format == RhiIndexFormat.SixteenBit)
                    ? DXGI_FORMAT.DXGI_FORMAT_R16_UINT
                    : DXGI_FORMAT.DXGI_FORMAT_R32_UINT;
                ComVTableHelper.IASetIndexBuffer(_context.Handle, d3dBuf.NativeBuffer.Handle, nativeFmt, (uint)offset);
            }
        }

        public void SetConstantBuffer(int slot, IRhiBuffer buffer, RhiShaderStage stage = RhiShaderStage.Vertex | RhiShaderStage.Pixel)
        {
            if (buffer is D3D11RhiBuffer d3dBuf)
            {
                if ((stage & RhiShaderStage.Vertex) != 0)
                    _context.VSSetConstantBuffers((uint)slot, d3dBuf.NativeBuffer);
                if ((stage & RhiShaderStage.Pixel) != 0)
                    _context.PSSetConstantBuffers((uint)slot, d3dBuf.NativeBuffer);
            }
        }

        public void SetShaderResource(int slot, IRhiTexture texture, RhiShaderStage stage = RhiShaderStage.Pixel)
        {
            if (texture is D3D11RhiTexture d3dTex && d3dTex.NativeSrv != null)
            {
                if ((stage & RhiShaderStage.Pixel) != 0)
                    _context.PSSetShaderResources((uint)slot, d3dTex.NativeSrv);
                if ((stage & RhiShaderStage.Vertex) != 0)
                    ComVTableHelper.VSSetShaderResources(_context.Handle, (uint)slot, 1, new[] { d3dTex.NativeSrv.Handle });
            }
        }

        public void Draw(int vertexCount, int startVertex = 0)
        {
            _context.Draw((uint)vertexCount, (uint)startVertex);
        }

        public void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0)
        {
            ComVTableHelper.DrawIndexed(_context.Handle, (uint)indexCount, (uint)startIndex, baseVertex);
        }

        public void DispatchCompute(int groupCountX, int groupCountY, int groupCountZ)
        {
            ComVTableHelper.Dispatch(_context.Handle, (uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
        }

        public void ResourceBarrier(in RhiBarrier barrier)
        {
            // Direct3D 11 runtime implicitly manages hazard tracking and state transitions.
            // Explicit barrier calls ensure pipeline validation consistency with modern D3D12/Vulkan RHI.
            if (barrier.Texture is D3D11RhiTexture d3dTex)
            {
                // Validate texture integrity
                if (!d3dTex.NativeTexture.IsValid) return;
            }
        }

        public void ResourceBarriers(ReadOnlySpan<RhiBarrier> barriers)
        {
            for (int i = 0; i < barriers.Length; i++)
            {
                ResourceBarrier(in barriers[i]);
            }
        }

        public void SignalFence(IRhiFence fence, ulong value)
        {
            if (fence == null) throw new ArgumentNullException(nameof(fence));
            // Flush commands through context so GPU execution pipeline processes up to this point
            ComVTableHelper.Flush(_context.Handle);
            fence.Signal(value);
        }

        public void WaitFence(IRhiFence fence, ulong value)
        {
            if (fence == null) throw new ArgumentNullException(nameof(fence));
            fence.Wait(value);
        }

        public void Dispose() { }

        private static D3D11_PRIMITIVE_TOPOLOGY ToNativeTopology(RhiPrimitiveTopology topology)
        {
            switch (topology)
            {
                case RhiPrimitiveTopology.LineList: return D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINELIST;
                case RhiPrimitiveTopology.LineStrip: return D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP;
                case RhiPrimitiveTopology.PointList: return D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_POINTLIST;
                case RhiPrimitiveTopology.TriangleStrip: return D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP;
                default: return D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
            }
        }
    }

    /// <summary>
    /// Timeline synchronization fence for Direct3D 11.
    /// Combines CPU-side signaling with device context flushes.
    /// </summary>
    public sealed class D3D11RhiFence : IRhiFence
    {
        private readonly D3D11DeviceContext _context;
        private long _currentValue;
        private readonly ManualResetEventSlim _event = new(false);
        private bool _disposed;

        public D3D11RhiFence(D3D11DeviceContext context, ulong initialValue)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentValue = (long)initialValue;
            if (initialValue > 0) _event.Set();
        }

        public ulong CompletedValue => (ulong)Interlocked.Read(ref _currentValue);

        public void Signal(ulong value)
        {
            Interlocked.Exchange(ref _currentValue, (long)value);
            _event.Set();
        }

        public bool Wait(ulong value, int timeoutMilliseconds = -1)
        {
            while (CompletedValue < value)
            {
                _event.Reset();
                if (CompletedValue >= value) return true;
                if (!_event.Wait(timeoutMilliseconds))
                {
                    return CompletedValue >= value;
                }
            }
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _event.Dispose();
            }
        }
    }
}
