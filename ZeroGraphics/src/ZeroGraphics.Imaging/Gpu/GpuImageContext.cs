using System;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Gpu.Shaders;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// GPU execution context for image processing.
    /// Manages Direct3D 11 device, immediate context, texture pools, and precompiled HLSL shaders.
    /// </summary>
    public sealed class GpuImageContext : IDisposable
    {
        private readonly bool _ownsDevice;
        private bool _disposed;

        public D3D11Device Device { get; }
        public D3D11DeviceContext ImmediateContext { get; }
        public GpuTexturePool TexturePool { get; }
        public GpuTextureTransfer Transfer { get; }

        public D3D11VertexShader VsFullscreen { get; }
        public D3D11PixelShader PsResize { get; }
        public D3D11PixelShader PsColorAdjust { get; }
        public D3D11PixelShader PsGaussianBlur { get; }
        public D3D11PixelShader PsSobel { get; }
        public D3D11PixelShader PsSharpen { get; }
        public D3D11PixelShader PsThreshold { get; }
        public D3D11PixelShader PsFused { get; }
        public D3D11PixelShader PsDilate { get; }
        public D3D11PixelShader PsErode { get; }
        public D3D11PixelShader PsAffineTransform { get; }
        public D3D11PixelShader PsCannyNms { get; }
        public D3D11PixelShader PsGamma { get; }

        public D3D11ComputeShader CsColorAdjust { get; }
        public D3D11ComputeShader CsBlurHorizontal { get; }
        public D3D11ComputeShader CsBlurVertical { get; }
        public D3D11ComputeShader CsConvolution3x3 { get; }
        public D3D11ComputeShader CsPyramidDown { get; }
        public D3D11ComputeShader CsPyramidUp { get; }
        public D3D11ComputeShader CsFocusMeasure { get; }
        public D3D11ComputeShader CsFocusBlend { get; }
        public D3D11ComputeShader CsHdrToneMapping { get; }
        public D3D11ComputeShader CsTensorPreprocessNCHW { get; }
        public D3D11ComputeShader CsHeatmapOverlay { get; }


        public D3D11SamplerState LinearSampler { get; }
        public D3D11SamplerState PointSampler { get; }
        public D3D11RasterizerState RasterizerState { get; }
        public D3D11Buffer ConstantBuffer { get; }

        /// <summary>
        /// Creates a GPU image context using an existing D3D11Device and context.
        /// </summary>
        public GpuImageContext(D3D11Device device, D3D11DeviceContext context, bool ownsDevice = false)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            ImmediateContext = context ?? throw new ArgumentNullException(nameof(context));
            _ownsDevice = ownsDevice;

            TexturePool = new GpuTexturePool(Device);
            Transfer = new GpuTextureTransfer(Device, ImmediateContext);

            // 1. Shaders
            VsFullscreen = Device.CreateVertexShader(GpuImageShaderBytecodes.VsFullscreenBytecode);
            PsResize = Device.CreatePixelShader(GpuImageShaderBytecodes.PsResizeBytecode);
            PsColorAdjust = Device.CreatePixelShader(GpuImageShaderBytecodes.PsColorAdjustBytecode);
            PsGaussianBlur = Device.CreatePixelShader(GpuImageShaderBytecodes.PsGaussianBlurBytecode);
            PsSobel = Device.CreatePixelShader(GpuImageShaderBytecodes.PsSobelBytecode);
            PsSharpen = Device.CreatePixelShader(GpuImageShaderBytecodes.PsSharpenBytecode);
            PsThreshold = Device.CreatePixelShader(GpuImageShaderBytecodes.PsThresholdBytecode);
            PsFused = Device.CreatePixelShader(GpuImageShaderBytecodes.PsFusedBytecode);
            PsDilate = Device.CreatePixelShader(GpuImageShaderBytecodes.PsDilateBytecode);
            PsErode = Device.CreatePixelShader(GpuImageShaderBytecodes.PsErodeBytecode);
            PsAffineTransform = Device.CreatePixelShader(GpuImageShaderBytecodes.PsAffineTransformBytecode);
            PsCannyNms = Device.CreatePixelShader(GpuImageShaderBytecodes.PsCannyNmsBytecode);
            PsGamma = Device.CreatePixelShader(GpuImageShaderBytecodes.PsGammaBytecode);

            // Compute Shaders (DirectCompute 5.0)
            CsColorAdjust = Device.CreateComputeShader(ComputeShaderBytecodes.CsColorAdjustBytecode);
            CsBlurHorizontal = Device.CreateComputeShader(ComputeShaderBytecodes.CsBlurHorizontalBytecode);
            CsBlurVertical = Device.CreateComputeShader(ComputeShaderBytecodes.CsBlurVerticalBytecode);
            CsConvolution3x3 = Device.CreateComputeShader(ComputeShaderBytecodes.CsConvolution3x3Bytecode);
            CsPyramidDown = Device.CreateComputeShader(ComputeShaderBytecodes.CsPyramidDownBytecode);
            CsPyramidUp = Device.CreateComputeShader(ComputeShaderBytecodes.CsPyramidUpBytecode);
            CsFocusMeasure = Device.CreateComputeShader(ComputeShaderBytecodes.CsFocusMeasureBytecode);
            CsFocusBlend = Device.CreateComputeShader(ComputeShaderBytecodes.CsFocusBlendBytecode);
            CsHdrToneMapping = Device.CreateComputeShader(ComputeShaderBytecodes.CsHdrToneMappingBytecode);
            CsTensorPreprocessNCHW = Device.CreateComputeShader(ComputeShaderBytecodes.CsTensorPreprocessNCHWBytecode);
            CsHeatmapOverlay = Device.CreateComputeShader(ComputeShaderBytecodes.CsHeatmapOverlayBytecode);


            // 2. Samplers (Linear & Point)
            var linearDesc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
                MaxLOD = float.MaxValue
            };
            LinearSampler = Device.CreateSamplerState(ref linearDesc);

            var pointDesc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_POINT,
                AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
                MaxLOD = float.MaxValue
            };
            PointSampler = Device.CreateSamplerState(ref pointDesc);

            // 3. Rasterizer State (No culling, solid fill)
            var rasterDesc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
                DepthClipEnable = 0
            };
            RasterizerState = Device.CreateRasterizerState(ref rasterDesc);

            // 4. Universal 64-byte Constant Buffer for parameters
            var cbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = 64, // Multiple of 16
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER
            };
            unsafe
            {
                ConstantBuffer = Device.CreateBuffer(ref cbDesc);
            }
        }

        /// <summary>
        /// Creates a default standalone GPU image context.
        /// </summary>
        public static GpuImageContext CreateDefault()
        {
            return new GpuImageContext(D3D11DeviceManager.Device, D3D11DeviceManager.Context, ownsDevice: false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                TexturePool.Dispose();
                Transfer.Dispose();

                VsFullscreen.Dispose();
                PsResize.Dispose();
                PsColorAdjust.Dispose();
                PsGaussianBlur.Dispose();
                PsSobel.Dispose();
                PsSharpen.Dispose();
                PsThreshold.Dispose();
                PsFused.Dispose();
                PsDilate.Dispose();
                PsErode.Dispose();
                PsAffineTransform.Dispose();
                PsCannyNms.Dispose();
                PsGamma.Dispose();

                CsColorAdjust.Dispose();
                CsBlurHorizontal.Dispose();
                CsBlurVertical.Dispose();
                CsConvolution3x3.Dispose();
                CsPyramidDown.Dispose();
                CsPyramidUp.Dispose();
                CsFocusMeasure.Dispose();
                CsFocusBlend.Dispose();
                CsHdrToneMapping.Dispose();
                CsTensorPreprocessNCHW.Dispose();
                CsHeatmapOverlay.Dispose();


                LinearSampler.Dispose();
                PointSampler.Dispose();
                RasterizerState.Dispose();
                ConstantBuffer.Dispose();

                if (_ownsDevice)
                {
                    ImmediateContext.Dispose();
                    Device.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
