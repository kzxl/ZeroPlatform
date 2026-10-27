using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    public enum ToneMappingOperator
    {
        Reinhard = 0,
        AcesFilmic = 1,
        LinearClamp = 2
    }

    /// <summary>
    /// Level 10 GPU HDR Tone Mapping Processor.
    /// Maps high dynamic range textures (FP16/FP32 float4) to standard dynamic range targets
    /// using Reinhard, ACES Filmic curve, exposure compensation, and perceptual gamma correction.
    /// </summary>
    public sealed class GpuHdrToneMapper : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct HdrParamsConstants
        {
            public uint HdrWidth;
            public uint HdrHeight;
            public uint ToneMappingMode;
            public float Exposure;
            public float HdrGamma;
            public float Pad1;
            public float Pad2;
            public float Pad3;
        }

        private readonly GpuImageContext _context;

        public GpuHdrToneMapper(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Converts an HDR float texture to an SDR texture using the specified tone mapping operator and exposure.
        /// </summary>
        public unsafe PooledGpuTexture ToneMap(
            PooledGpuTexture hdrInput,
            ToneMappingOperator toneMappingOp = ToneMappingOperator.AcesFilmic,
            float exposure = 0.0f,
            float gamma = 2.2f,
            PooledGpuTexture? destination = null)
        {
            if (hdrInput == null) throw new ArgumentNullException(nameof(hdrInput));

            int width = hdrInput.Width;
            int height = hdrInput.Height;

            var d3dContext = _context.ImmediateContext;
            var pool = _context.TexturePool;

            var output = destination ?? pool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);

            var cb = new HdrParamsConstants
            {
                HdrWidth = (uint)width,
                HdrHeight = (uint)height,
                ToneMappingMode = (uint)toneMappingOp,
                Exposure = exposure,
                HdrGamma = gamma,
                Pad1 = 0,
                Pad2 = 0,
                Pad3 = 0
            };
            d3dContext.UpdateSubresource(_context.ConstantBuffer, ref cb);

            d3dContext.CSSetShader(_context.CsHdrToneMapping);
            d3dContext.CSSetConstantBuffers(0, _context.ConstantBuffer);
            d3dContext.CSSetShaderResources(0, hdrInput.Srv);
            d3dContext.CSSetUnorderedAccessViews(0, output.Uav);

            uint gx = ((uint)width + 15) / 16;
            uint gy = ((uint)height + 15) / 16;
            d3dContext.Dispatch(gx, gy, 1);

            d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
            d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

            return output;
        }

        public void Dispose()
        {
            // Stateless operator; context manages shaders and pools
        }
    }
}
