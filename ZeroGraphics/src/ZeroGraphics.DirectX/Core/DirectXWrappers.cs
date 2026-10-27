using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.DirectX.Core
{
    public abstract class ComObjectWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero && !_disposed;

        protected ComObjectWrapper(IntPtr handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    ComVTableHelper.Release(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        ~ComObjectWrapper()
        {
            Dispose(false);
        }
    }

    public sealed class D3D11Device : ComObjectWrapper
    {
        public D3D_FEATURE_LEVEL FeatureLevel { get; }

        public D3D11Device(IntPtr handle, D3D_FEATURE_LEVEL featureLevel) : base(handle)
        {
            FeatureLevel = featureLevel;
        }

        public unsafe D3D11Buffer CreateBuffer(ref D3D11_BUFFER_DESC desc, D3D11_SUBRESOURCE_DATA* pInitialData = null)
        {
            int hr = ComVTableHelper.CreateBuffer(Handle, ref desc, (IntPtr)pInitialData, out IntPtr ppBuffer);
            if (hr < 0 || ppBuffer == IntPtr.Zero)
                throw new COMException("Failed to create D3D11Buffer.", hr);

            return new D3D11Buffer(ppBuffer, desc);
        }

        public D3D11Texture2D CreateTexture2D(ref D3D11_TEXTURE2D_DESC desc, IntPtr initialData = default)
        {
            int hr = ComVTableHelper.CreateTexture2D(Handle, ref desc, initialData, out IntPtr ppTexture);
            if (hr < 0 || ppTexture == IntPtr.Zero)
                throw new COMException("Failed to create D3D11Texture2D.", hr);

            return new D3D11Texture2D(ppTexture, desc);
        }

        public D3D11RenderTargetView CreateRenderTargetView(IntPtr resource, IntPtr desc = default)
        {
            int hr = ComVTableHelper.CreateRenderTargetView(Handle, resource, desc, out IntPtr ppRtv);
            if (hr < 0 || ppRtv == IntPtr.Zero)
                throw new COMException("Failed to create D3D11RenderTargetView.", hr);

            return new D3D11RenderTargetView(ppRtv);
        }

        public D3D11ShaderResourceView CreateShaderResourceView(IntPtr resource, IntPtr desc = default)
        {
            int hr = ComVTableHelper.CreateShaderResourceView(Handle, resource, desc, out IntPtr ppSrv);
            if (hr < 0 || ppSrv == IntPtr.Zero)
                throw new COMException("Failed to create D3D11ShaderResourceView.", hr);

            return new D3D11ShaderResourceView(ppSrv);
        }

        public D3D11SamplerState CreateSamplerState(ref D3D11_SAMPLER_DESC desc)
        {
            int hr = ComVTableHelper.CreateSamplerState(Handle, ref desc, out IntPtr ppSampler);
            if (hr < 0 || ppSampler == IntPtr.Zero)
                throw new COMException("Failed to create D3D11SamplerState.", hr);

            return new D3D11SamplerState(ppSampler);
        }

        public unsafe D3D11VertexShader CreateVertexShader(byte[] bytecode)
        {
            if (bytecode == null || bytecode.Length == 0)
                throw new ArgumentNullException(nameof(bytecode));

            fixed (byte* pBytecode = bytecode)
            {
                int hr = ComVTableHelper.CreateVertexShader(Handle, (IntPtr)pBytecode, (UIntPtr)bytecode.Length, IntPtr.Zero, out IntPtr ppShader);
                if (hr < 0 || ppShader == IntPtr.Zero)
                    throw new COMException("Failed to create D3D11VertexShader.", hr);

                return new D3D11VertexShader(ppShader);
            }
        }

        public unsafe D3D11PixelShader CreatePixelShader(byte[] bytecode)
        {
            if (bytecode == null || bytecode.Length == 0)
                throw new ArgumentNullException(nameof(bytecode));

            fixed (byte* pBytecode = bytecode)
            {
                int hr = ComVTableHelper.CreatePixelShader(Handle, (IntPtr)pBytecode, (UIntPtr)bytecode.Length, IntPtr.Zero, out IntPtr ppShader);
                if (hr < 0 || ppShader == IntPtr.Zero)
                    throw new COMException("Failed to create D3D11PixelShader.", hr);

                return new D3D11PixelShader(ppShader);
            }
        }

        public unsafe D3D11ComputeShader CreateComputeShader(byte[] bytecode)
        {
            if (bytecode == null || bytecode.Length == 0)
                throw new ArgumentNullException(nameof(bytecode));

            fixed (byte* pBytecode = bytecode)
            {
                int hr = ComVTableHelper.CreateComputeShader(Handle, (IntPtr)pBytecode, (UIntPtr)bytecode.Length, IntPtr.Zero, out IntPtr ppShader);
                if (hr < 0 || ppShader == IntPtr.Zero)
                    throw new COMException($"Failed to create D3D11ComputeShader. HR=0x{hr:X8}, BytecodeLength={bytecode.Length}, Device=0x{Handle.ToInt64():X}", hr);

                return new D3D11ComputeShader(ppShader);
            }
        }

        public D3D11UnorderedAccessView CreateUnorderedAccessView(IntPtr resource, IntPtr desc = default)
        {
            int hr = ComVTableHelper.CreateUnorderedAccessView(Handle, resource, desc, out IntPtr ppUav);
            if (hr < 0 || ppUav == IntPtr.Zero)
                throw new COMException("Failed to create D3D11UnorderedAccessView.", hr);

            return new D3D11UnorderedAccessView(ppUav);
        }

        public D3D11Query CreateQuery(ref D3D11_QUERY_DESC desc)
        {
            int hr = ComVTableHelper.CreateQuery(Handle, ref desc, out IntPtr ppQuery);
            if (hr < 0 || ppQuery == IntPtr.Zero)
                throw new COMException("Failed to create D3D11Query.", hr);

            return new D3D11Query(ppQuery, desc);
        }

        public D3D11Query CreateQuery(D3D11_QUERY queryType, uint miscFlags = 0)
        {
            var desc = new D3D11_QUERY_DESC(queryType, miscFlags);
            return CreateQuery(ref desc);
        }


        public unsafe D3D11InputLayout CreateInputLayout(D3D11_INPUT_ELEMENT_DESC[] descs, byte[] shaderBytecode)
        {
            if (descs == null) throw new ArgumentNullException(nameof(descs));
            if (shaderBytecode == null) throw new ArgumentNullException(nameof(shaderBytecode));

            fixed (byte* pBytecode = shaderBytecode)
            {
                int hr = ComVTableHelper.CreateInputLayout(
                    Handle, descs, (uint)descs.Length, (IntPtr)pBytecode, (UIntPtr)shaderBytecode.Length, out IntPtr ppLayout);
                if (hr < 0 || ppLayout == IntPtr.Zero)
                    throw new COMException("Failed to create D3D11InputLayout.", hr);

                return new D3D11InputLayout(ppLayout);
            }
        }

        public D3D11DeviceContext CreateDeferredContext(uint contextFlags = 0)
        {
            int hr = ComVTableHelper.CreateDeferredContext(Handle, contextFlags, out IntPtr ppDeferredContext);
            if (hr < 0 || ppDeferredContext == IntPtr.Zero)
                throw new COMException("Failed to create D3D11DeferredContext.", hr);

            return new D3D11DeviceContext(ppDeferredContext, isDeferred: true);
        }

        public D3D11BlendState CreateBlendState(ref D3D11_BLEND_DESC desc)
        {
            int hr = ComVTableHelper.CreateBlendState(Handle, ref desc, out IntPtr ppBlend);
            if (hr < 0 || ppBlend == IntPtr.Zero)
                throw new COMException("Failed to create D3D11BlendState.", hr);

            return new D3D11BlendState(ppBlend);
        }

        public D3D11RasterizerState CreateRasterizerState(ref D3D11_RASTERIZER_DESC desc)
        {
            int hr = ComVTableHelper.CreateRasterizerState(Handle, ref desc, out IntPtr ppRaster);
            if (hr < 0 || ppRaster == IntPtr.Zero)
                throw new COMException("Failed to create D3D11RasterizerState.", hr);

            return new D3D11RasterizerState(ppRaster);
        }

        public int GetDeviceRemovedReason()
        {
            return ComVTableHelper.GetDeviceRemovedReason(Handle);
        }
    }

    public sealed class D3D11DeviceContext : ComObjectWrapper
    {
        public bool IsDeferred { get; }

        public D3D11DeviceContext(IntPtr handle, bool isDeferred = false) : base(handle)
        {
            IsDeferred = isDeferred;
        }

        public void ClearRenderTargetView(D3D11RenderTargetView rtv, float[] colorRGBA)
        {
            if (rtv == null || !rtv.IsValid) return;
            ComVTableHelper.ClearRenderTargetView(Handle, rtv.Handle, colorRGBA);
        }

        public void OMSetRenderTargets(D3D11RenderTargetView rtv, IntPtr dsv = default)
        {
            if (rtv == null || !rtv.IsValid)
            {
                ComVTableHelper.OMSetRenderTargets(Handle, 0, Array.Empty<IntPtr>(), dsv);
                return;
            }
            ComVTableHelper.OMSetRenderTargets(Handle, 1, new[] { rtv.Handle }, dsv);
        }

        public void OMSetBlendState(D3D11BlendState? blendState, float[]? blendFactor = null, uint sampleMask = 0xFFFFFFFF)
        {
            ComVTableHelper.OMSetBlendState(Handle, blendState?.Handle ?? IntPtr.Zero, blendFactor, sampleMask);
        }

        public void RSSetState(D3D11RasterizerState? state)
        {
            ComVTableHelper.RSSetState(Handle, state?.Handle ?? IntPtr.Zero);
        }

        public void RSSetViewports(params D3D11_VIEWPORT[] viewports)
        {
            if (viewports == null || viewports.Length == 0) return;
            ComVTableHelper.RSSetViewports(Handle, (uint)viewports.Length, viewports);
        }

        public void IASetInputLayout(D3D11InputLayout layout)
        {
            ComVTableHelper.IASetInputLayout(Handle, layout?.Handle ?? IntPtr.Zero);
        }

        public void IASetVertexBuffers(uint startSlot, D3D11Buffer buffer, uint stride, uint offset)
        {
            if (buffer == null || !buffer.IsValid) return;
            ComVTableHelper.IASetVertexBuffers(Handle, startSlot, 1, new[] { buffer.Handle }, new[] { stride }, new[] { offset });
        }

        public void IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY topology)
        {
            ComVTableHelper.IASetPrimitiveTopology(Handle, topology);
        }

        public void VSSetShader(D3D11VertexShader shader)
        {
            ComVTableHelper.VSSetShader(Handle, shader?.Handle ?? IntPtr.Zero);
        }

        public void VSSetConstantBuffers(uint startSlot, D3D11Buffer buffer)
        {
            ComVTableHelper.VSSetConstantBuffers(Handle, startSlot, 1, new[] { buffer?.Handle ?? IntPtr.Zero });
        }

        public void PSSetShader(D3D11PixelShader shader)
        {
            ComVTableHelper.PSSetShader(Handle, shader?.Handle ?? IntPtr.Zero);
        }

        public void PSSetConstantBuffers(uint startSlot, D3D11Buffer buffer)
        {
            ComVTableHelper.PSSetConstantBuffers(Handle, startSlot, 1, new[] { buffer?.Handle ?? IntPtr.Zero });
        }

        public void PSSetShaderResources(uint startSlot, params D3D11ShaderResourceView[] views)
        {
            if (views == null || views.Length == 0)
            {
                ComVTableHelper.PSSetShaderResources(Handle, startSlot, 1, new[] { IntPtr.Zero });
                return;
            }
            var handles = new IntPtr[views.Length];
            for (int i = 0; i < views.Length; i++)
                handles[i] = views[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.PSSetShaderResources(Handle, startSlot, (uint)handles.Length, handles);
        }

        public void PSSetSamplers(uint startSlot, params D3D11SamplerState[] samplers)
        {
            if (samplers == null || samplers.Length == 0)
            {
                ComVTableHelper.PSSetSamplers(Handle, startSlot, 1, new[] { IntPtr.Zero });
                return;
            }
            var handles = new IntPtr[samplers.Length];
            for (int i = 0; i < samplers.Length; i++)
                handles[i] = samplers[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.PSSetSamplers(Handle, startSlot, (uint)handles.Length, handles);
        }

        public unsafe void UpdateSubresource<T>(D3D11Buffer dstBuffer, ref T data) where T : unmanaged
        {
            if (dstBuffer == null || !dstBuffer.IsValid) return;
            fixed (T* pData = &data)
            {
                ComVTableHelper.UpdateSubresource(Handle, dstBuffer.Handle, 0, IntPtr.Zero, (IntPtr)pData, 0, 0);
            }
        }

        public void Draw(uint vertexCount, uint startVertexLocation = 0)
        {
            ComVTableHelper.Draw(Handle, vertexCount, startVertexLocation);
        }

        public int Map(D3D11Buffer buffer, uint subresource, D3D11_MAP mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE mapped)
        {
            if (buffer == null || !buffer.IsValid)
            {
                mapped = default;
                return -1;
            }
            return ComVTableHelper.Map(Handle, buffer.Handle, subresource, mapType, mapFlags, out mapped);
        }

        public void Unmap(D3D11Buffer buffer, uint subresource = 0)
        {
            if (buffer == null || !buffer.IsValid) return;
            ComVTableHelper.Unmap(Handle, buffer.Handle, subresource);
        }

        public int Map(D3D11Texture2D texture, uint subresource, D3D11_MAP mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE mapped)
        {
            if (texture == null || !texture.IsValid)
            {
                mapped = default;
                return -1;
            }
            return ComVTableHelper.Map(Handle, texture.Handle, subresource, mapType, mapFlags, out mapped);
        }

        public void Unmap(D3D11Texture2D texture, uint subresource = 0)
        {
            if (texture == null || !texture.IsValid) return;
            ComVTableHelper.Unmap(Handle, texture.Handle, subresource);
        }

        public void Begin(D3D11Query query)
        {
            if (query == null || !query.IsValid) return;
            ComVTableHelper.Begin(Handle, query.Handle);
        }

        public void End(D3D11Query query)
        {
            if (query == null || !query.IsValid) return;
            ComVTableHelper.End(Handle, query.Handle);
        }

        public int GetData(D3D11Query query, IntPtr pData, uint dataSize, uint flags = 0)
        {
            if (query == null || !query.IsValid) return -1;
            return ComVTableHelper.GetData(Handle, query.Handle, pData, dataSize, flags);
        }

        public unsafe int GetData<T>(D3D11Query query, out T data, uint flags = 0) where T : unmanaged
        {
            data = default;
            if (query == null || !query.IsValid) return -1;
            fixed (T* p = &data)
            {
                return ComVTableHelper.GetData(Handle, query.Handle, (IntPtr)p, (uint)sizeof(T), flags);
            }
        }


        public void DrawInstanced(uint vertexCountPerInstance, uint instanceCount, uint startVertexLocation = 0, uint startInstanceLocation = 0)
        {
            ComVTableHelper.DrawInstanced(Handle, vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
        }

        public void RSSetScissorRects(params D3D11_RECT[] rects)
        {
            if (rects == null || rects.Length == 0) return;
            ComVTableHelper.RSSetScissorRects(Handle, (uint)rects.Length, rects);
        }

        public void Flush()
        {
            ComVTableHelper.Flush(Handle);
        }

        public void CopyResource(D3D11Texture2D destination, D3D11Texture2D source)
        {
            if (destination == null || !destination.IsValid) throw new ArgumentNullException(nameof(destination));
            if (source == null || !source.IsValid) throw new ArgumentNullException(nameof(source));
            ComVTableHelper.CopyResource(Handle, destination.Handle, source.Handle);
        }

        public void Dispatch(uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ = 1)
        {
            ComVTableHelper.Dispatch(Handle, threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        public void CSSetShader(D3D11ComputeShader? computeShader)
        {
            ComVTableHelper.CSSetShader(Handle, computeShader?.Handle ?? IntPtr.Zero);
        }

        public void CSSetUnorderedAccessViews(uint startSlot, D3D11UnorderedAccessView? uav, uint initialCount = 0)
        {
            if (uav == null || !uav.IsValid)
            {
                ComVTableHelper.CSSetUnorderedAccessViews(Handle, startSlot, 1, new[] { IntPtr.Zero }, new[] { initialCount });
                return;
            }
            ComVTableHelper.CSSetUnorderedAccessViews(Handle, startSlot, 1, new[] { uav.Handle }, new[] { initialCount });
        }

        public void CSSetUnorderedAccessViews(uint startSlot, D3D11UnorderedAccessView[] uavs, uint[]? initialCounts = null)
        {
            if (uavs == null || uavs.Length == 0) return;
            var ptrs = new IntPtr[uavs.Length];
            for (int i = 0; i < uavs.Length; i++)
                ptrs[i] = uavs[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.CSSetUnorderedAccessViews(Handle, startSlot, (uint)uavs.Length, ptrs, initialCounts);
        }

        public void CSSetShaderResources(uint startSlot, D3D11ShaderResourceView? srv)
        {
            if (srv == null || !srv.IsValid)
            {
                ComVTableHelper.CSSetShaderResources(Handle, startSlot, 1, new[] { IntPtr.Zero });
                return;
            }
            ComVTableHelper.CSSetShaderResources(Handle, startSlot, 1, new[] { srv.Handle });
        }

        public void CSSetShaderResources(uint startSlot, D3D11ShaderResourceView[] srvs)
        {
            if (srvs == null || srvs.Length == 0) return;
            var ptrs = new IntPtr[srvs.Length];
            for (int i = 0; i < srvs.Length; i++)
                ptrs[i] = srvs[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.CSSetShaderResources(Handle, startSlot, (uint)srvs.Length, ptrs);
        }

        public void CSSetConstantBuffers(uint startSlot, D3D11Buffer? buffer)
        {
            if (buffer == null || !buffer.IsValid)
            {
                ComVTableHelper.CSSetConstantBuffers(Handle, startSlot, 1, new[] { IntPtr.Zero });
                return;
            }
            ComVTableHelper.CSSetConstantBuffers(Handle, startSlot, 1, new[] { buffer.Handle });
        }

        public void CSSetConstantBuffers(uint startSlot, D3D11Buffer[] buffers)
        {
            if (buffers == null || buffers.Length == 0) return;
            var ptrs = new IntPtr[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
                ptrs[i] = buffers[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.CSSetConstantBuffers(Handle, startSlot, (uint)buffers.Length, ptrs);
        }

        public void CSSetSamplers(uint startSlot, D3D11SamplerState? sampler)
        {
            if (sampler == null || !sampler.IsValid)
            {
                ComVTableHelper.CSSetSamplers(Handle, startSlot, 1, new[] { IntPtr.Zero });
                return;
            }
            ComVTableHelper.CSSetSamplers(Handle, startSlot, 1, new[] { sampler.Handle });
        }

        public void CSSetSamplers(uint startSlot, D3D11SamplerState[] samplers)
        {
            if (samplers == null || samplers.Length == 0) return;
            var ptrs = new IntPtr[samplers.Length];
            for (int i = 0; i < samplers.Length; i++)
                ptrs[i] = samplers[i]?.Handle ?? IntPtr.Zero;
            ComVTableHelper.CSSetSamplers(Handle, startSlot, (uint)samplers.Length, ptrs);
        }

        public D3D11CommandList FinishCommandList(bool restoreDeferredContextState = false)
        {
            if (!IsDeferred)
                throw new InvalidOperationException("FinishCommandList can only be called on a Deferred Context.");

            int hr = ComVTableHelper.FinishCommandList(Handle, restoreDeferredContextState ? 1 : 0, out IntPtr ppCommandList);
            if (hr < 0 || ppCommandList == IntPtr.Zero)
                throw new COMException("Failed to finish D3D11CommandList.", hr);

            return new D3D11CommandList(ppCommandList);
        }

        public void ExecuteCommandList(D3D11CommandList commandList, bool restoreContextState = false)
        {
            if (commandList == null || !commandList.IsValid)
                throw new ArgumentNullException(nameof(commandList));

            ComVTableHelper.ExecuteCommandList(Handle, commandList.Handle, restoreContextState ? 1 : 0);
        }
    }

    public sealed class D3D11Texture2D : ComObjectWrapper
    {
        public D3D11_TEXTURE2D_DESC Description { get; }

        public D3D11Texture2D(IntPtr handle, D3D11_TEXTURE2D_DESC description) : base(handle)
        {
            Description = description;
        }
    }

    public sealed class DxgiSwapChain : ComObjectWrapper
    {
        public DxgiSwapChain(IntPtr handle) : base(handle) { }

        public int Present(uint syncInterval = 0, uint flags = 0)
        {
            return ComVTableHelper.Present(Handle, syncInterval, flags);
        }

        public IntPtr GetBuffer(uint bufferIndex, ref Guid riid)
        {
            int hr = ComVTableHelper.GetBuffer(Handle, bufferIndex, ref riid, out IntPtr ppSurface);
            if (hr < 0 || ppSurface == IntPtr.Zero)
                throw new COMException("Failed to get swapchain backbuffer.", hr);
            return ppSurface;
        }

        public int ResizeBuffers(uint bufferCount, uint width, uint height, DXGI_FORMAT format, uint flags = 0)
        {
            return ComVTableHelper.ResizeBuffers(Handle, bufferCount, width, height, format, flags);
        }
    }

    public sealed class D3D11RenderTargetView : ComObjectWrapper
    {
        public D3D11RenderTargetView(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11Buffer : ComObjectWrapper
    {
        public D3D11_BUFFER_DESC Description { get; }
        public D3D11Buffer(IntPtr handle, D3D11_BUFFER_DESC desc) : base(handle)
        {
            Description = desc;
        }
    }

    public sealed class D3D11VertexShader : ComObjectWrapper
    {
        public D3D11VertexShader(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11PixelShader : ComObjectWrapper
    {
        public D3D11PixelShader(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11InputLayout : ComObjectWrapper
    {
        public D3D11InputLayout(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11BlendState : ComObjectWrapper
    {
        public D3D11BlendState(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11RasterizerState : ComObjectWrapper
    {
        public D3D11RasterizerState(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11ShaderResourceView : ComObjectWrapper
    {
        public D3D11ShaderResourceView(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11SamplerState : ComObjectWrapper
    {
        public D3D11SamplerState(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11ComputeShader : ComObjectWrapper
    {
        public D3D11ComputeShader(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11UnorderedAccessView : ComObjectWrapper
    {
        public D3D11UnorderedAccessView(IntPtr handle) : base(handle) { }
    }

    public sealed class D3D11Query : ComObjectWrapper
    {
        public D3D11_QUERY_DESC Description { get; }
        public D3D11Query(IntPtr handle, D3D11_QUERY_DESC desc) : base(handle)
        {
            Description = desc;
        }
    }
}
