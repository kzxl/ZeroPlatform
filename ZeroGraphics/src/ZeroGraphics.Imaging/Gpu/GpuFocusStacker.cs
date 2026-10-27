using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Level 9 GPU Focus Stacking Processor.
    /// Fuses multiple photos captured across varying focal planes into a single all-in-focus composite
    /// by measuring modified Laplacian energy maps and executing pixel-wise maximum sharpness blending on the GPU.
    /// </summary>
    public sealed class GpuFocusStacker : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct FocusMeasureConstants
        {
            public uint FocusWidth;
            public uint FocusHeight;
            public uint FocusRadius;
            public float FocusPad;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct FocusBlendConstants
        {
            public uint BlendWidth;
            public uint BlendHeight;
            public uint IsFirstSlice;
            public float BlendPad;
        }

        private readonly GpuImageContext _context;

        public GpuFocusStacker(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Stacks multiple focal slice textures into a single all-in-focus GPU texture.
        /// </summary>
        public unsafe PooledGpuTexture Stack(IReadOnlyList<PooledGpuTexture> slices)
        {
            if (slices == null || slices.Count == 0)
                throw new ArgumentException("At least one focal slice is required.", nameof(slices));

            int width = slices[0].Width;
            int height = slices[0].Height;
            var format = slices[0].Format;

            var d3dContext = _context.ImmediateContext;
            var pool = _context.TexturePool;

            // 1. Output Composite Color Texture
            var composite = pool.Acquire(width, height, format, needsUav: true);

            // 2. Best Energy Buffer (R32_FLOAT) and Slice Energy Buffer (R32_FLOAT)
            var bestEnergy = pool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT, needsUav: true);
            var sliceEnergy = pool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT, needsUav: true);

            try
            {
                uint gx = ((uint)width + 15) / 16;
                uint gy = ((uint)height + 15) / 16;

                for (int i = 0; i < slices.Count; i++)
                {
                    var slice = slices[i];

                    // Step A: Measure Modified Laplacian Energy
                    var measureCb = new FocusMeasureConstants
                    {
                        FocusWidth = (uint)width,
                        FocusHeight = (uint)height,
                        FocusRadius = 1,
                        FocusPad = 0f
                    };
                    d3dContext.UpdateSubresource(_context.ConstantBuffer, ref measureCb);

                    d3dContext.CSSetShader(_context.CsFocusMeasure);
                    d3dContext.CSSetConstantBuffers(0, _context.ConstantBuffer);
                    d3dContext.CSSetShaderResources(0, slice.Srv);
                    d3dContext.CSSetUnorderedAccessViews(0, sliceEnergy.Uav);

                    d3dContext.Dispatch(gx, gy, 1);

                    // Unbind
                    d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                    d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                    // Step B: Blend into Composite
                    var blendCb = new FocusBlendConstants
                    {
                        BlendWidth = (uint)width,
                        BlendHeight = (uint)height,
                        IsFirstSlice = (uint)(i == 0 ? 1 : 0),
                        BlendPad = 0f
                    };
                    d3dContext.UpdateSubresource(_context.ConstantBuffer, ref blendCb);

                    d3dContext.CSSetShader(_context.CsFocusBlend);
                    d3dContext.CSSetConstantBuffers(0, _context.ConstantBuffer);
                    d3dContext.CSSetShaderResources(0, new[] { slice.Srv, sliceEnergy.Srv });
                    d3dContext.CSSetUnorderedAccessViews(0, new[] { composite.Uav!, bestEnergy.Uav! });

                    d3dContext.Dispatch(gx, gy, 1);

                    // Unbind
                    d3dContext.CSSetShaderResources(0, new D3D11ShaderResourceView?[] { null, null }!);
                    d3dContext.CSSetUnorderedAccessViews(0, new D3D11UnorderedAccessView?[] { null, null }!);
                }

                return composite;
            }
            finally
            {
                pool.Release(bestEnergy);
                pool.Release(sliceEnergy);
            }
        }

        /// <summary>
        /// Stacks multiple CPU ImageBuffers into a single all-in-focus ImageBuffer.
        /// </summary>
        public ImageBuffer Stack(IReadOnlyList<ImageBuffer> slices)
        {
            if (slices == null || slices.Count == 0)
                throw new ArgumentException("At least one focal slice is required.", nameof(slices));

            int count = slices.Count;
            int width = slices[0].Width;
            int height = slices[0].Height;
            var format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;

            var gpuSlices = new List<PooledGpuTexture>(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var tex = _context.TexturePool.Acquire(width, height, format, needsUav: false);
                    _context.Transfer.Upload(slices[i], tex.Texture);
                    gpuSlices.Add(tex);
                }

                using var compositeGpu = Stack(gpuSlices);
                var result = new ImageBuffer(width, height, slices[0].Format);
                _context.Transfer.Download(compositeGpu.Texture, result);
                return result;
            }
            finally
            {
                for (int i = 0; i < gpuSlices.Count; i++)
                {
                    _context.TexturePool.Release(gpuSlices[i]);
                }
            }
        }

        public void Dispose()
        {
            // Stateless operator; context manages shaders and pools
        }
    }
}
