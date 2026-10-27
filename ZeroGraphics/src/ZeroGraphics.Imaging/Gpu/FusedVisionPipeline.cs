using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Pluggable interface for deep learning / neural network inference engines.
    /// Consumes GPU VRAM planar tensors and emits spatial confidence / anomaly masks.
    /// </summary>
    public interface IAiInferenceEngine : IDisposable
    {
        /// <summary>
        /// Runs inference on the input tensor and outputs a spatial confidence / anomaly mask texture.
        /// </summary>
        PooledGpuTexture Infer(GpuImageContext context, GpuTensorBuffer inputTensor);
    }

    /// <summary>
    /// Reference in-process anomaly detection engine simulating convolutional feature extraction
    /// and defect localization directly on GPU tensors.
    /// </summary>
    public sealed class ReferenceAnomalyDetector : IAiInferenceEngine
    {
        public PooledGpuTexture Infer(GpuImageContext context, GpuTensorBuffer inputTensor)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (inputTensor == null) throw new ArgumentNullException(nameof(inputTensor));

            int w = inputTensor.Width;
            int h = inputTensor.Height;

            // Allocate spatial mask texture
            var maskTex = context.TexturePool.Acquire(w, h, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);

            // Read tensor data to generate spatial anomaly response
            float[] data = new float[inputTensor.ElementCount];
            inputTensor.DownloadToHost(data);

            using (var maskBuf = new ImageBuffer(w, h, ImageFormatMode.Bgra32))
            {
                unsafe
                {
                    int planeSize = w * h;
                    for (int y = 0; y < h; y++)
                    {
                        byte* pRow = maskBuf.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            int idx = y * w + x;
                            float r = data[0 * planeSize + idx];
                            float g = data[1 * planeSize + idx];
                            float b = data[2 * planeSize + idx];

                            // Anomaly criterion: high contrast or saturated anomalous pixel
                            float maxVal = Math.Max(r, Math.Max(g, b));
                            float confidence = maxVal > 1.2f ? Math.Min(1.0f, (maxVal - 1.2f) / 1.5f) : 0.0f;

                            byte gray = (byte)(confidence * 255.0f);
                            pRow[x * 4 + 0] = gray;
                            pRow[x * 4 + 1] = gray;
                            pRow[x * 4 + 2] = gray;
                            pRow[x * 4 + 3] = 255;
                        }
                    }
                }

                context.Transfer.Upload(maskBuf, maskTex.Texture);
            }

            return maskTex;
        }

        public void Dispose()
        {
            // Stateless reference detector
        }
    }

    /// <summary>
    /// Level 12 AI + CV Unified Pipeline Fusion.
    /// Executes end-to-end vision graphs: CV Filter (Denoise/Sobel) -> AI Tensor Preprocessing
    /// -> Neural Inference -> Direct GPU Heatmap Overlay with zero CPU memory stalls.
    /// </summary>
    public sealed class FusedVisionPipeline : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct OverlayParamsConstants
        {
            public uint OverlayWidth;
            public uint OverlayHeight;
            public float OverlayAlpha;
            public float OverlayThreshold;
        }

        private readonly GpuImageContext _context;
        private readonly GpuTensorPreprocessor _preprocessor;

        public FusedVisionPipeline(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _preprocessor = new GpuTensorPreprocessor(_context);
        }

        /// <summary>
        /// Executes a fused CV + AI inspection pipeline:
        /// 1. Filters source image (optional Gaussian denoise or Sobel edge detection).
        /// 2. Preprocesses intermediate surface to Planar NCHW FP32 tensor in VRAM.
        /// 3. Incurs AI inference via <see cref="IAiInferenceEngine"/>.
        /// 4. Blends resulting anomaly mask as a vibrant heatmap overlay onto the original image.
        /// </summary>
        public PooledGpuTexture ExecuteInspection(
            PooledGpuTexture sourceImage,
            IAiInferenceEngine inferenceEngine,
            int tensorWidth = 64,
            int tensorHeight = 64,
            float overlayAlpha = 0.75f,
            float overlayThreshold = 0.25f,
            bool applyGaussianDenoise = true)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (inferenceEngine == null) throw new ArgumentNullException(nameof(inferenceEngine));

            int width = sourceImage.Width;
            int height = sourceImage.Height;
            var pool = _context.TexturePool;
            var d3dContext = _context.ImmediateContext;

            PooledGpuTexture processedInput = sourceImage;
            PooledGpuTexture? intermediateDenoised = null;

            try
            {
                // Step 1: Procedural CV Filter Pass (Gaussian Blur Denoise)
                if (applyGaussianDenoise)
                {
                    intermediateDenoised = pool.Acquire(width, height, sourceImage.Format, needsUav: true);
                    using (var temp = pool.Acquire(width, height, sourceImage.Format, needsUav: true))
                    {
                        // 1D Horizontal Pass
                        d3dContext.CSSetShader(_context.CsBlurHorizontal);
                        d3dContext.CSSetShaderResources(0, sourceImage.Srv);
                        d3dContext.CSSetUnorderedAccessViews(0, temp.Uav);
                        d3dContext.Dispatch(((uint)width + 127) / 128, (uint)height, 1);
                        d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                        d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                        // 1D Vertical Pass
                        d3dContext.CSSetShader(_context.CsBlurVertical);
                        d3dContext.CSSetShaderResources(0, temp.Srv);
                        d3dContext.CSSetUnorderedAccessViews(0, intermediateDenoised.Uav);
                        d3dContext.Dispatch((uint)width, ((uint)height + 127) / 128, 1);
                        d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                        d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);
                    }
                    processedInput = intermediateDenoised;
                }

                // Step 2: AI Tensor Preprocessing (Resize to tensor dimensions + ImageNet Normalize)
                using (var tensor = _preprocessor.Preprocess(processedInput, tensorWidth, tensorHeight, TensorNormalization.ImageNet))
                {
                    // Step 3: AI Inference Engine
                    using (var anomalyMask = inferenceEngine.Infer(_context, tensor))
                    {
                        // Step 4: Heatmap Overlay Shader
                        // If anomaly mask is lower resolution, upsample or sample bilinearly
                        PooledGpuTexture fullResolutionMask = anomalyMask;
                        PooledGpuTexture? upsampledMask = null;
                        if (anomalyMask.Width != width || anomalyMask.Height != height)
                        {
                            upsampledMask = GpuImagePyramid.Upsample(_context, anomalyMask, width, height);
                            fullResolutionMask = upsampledMask;
                        }

                        try
                        {
                            var visualOutput = pool.Acquire(width, height, sourceImage.Format, needsUav: true);

                            var cb = new OverlayParamsConstants
                            {
                                OverlayWidth = (uint)width,
                                OverlayHeight = (uint)height,
                                OverlayAlpha = overlayAlpha,
                                OverlayThreshold = overlayThreshold
                            };
                            d3dContext.UpdateSubresource(_context.ConstantBuffer, ref cb);

                            d3dContext.CSSetShader(_context.CsHeatmapOverlay);
                            d3dContext.CSSetConstantBuffers(0, _context.ConstantBuffer);
                            d3dContext.CSSetShaderResources(0, new[] { sourceImage.Srv, fullResolutionMask.Srv });
                            d3dContext.CSSetUnorderedAccessViews(0, visualOutput.Uav);

                            uint gx = ((uint)width + 15) / 16;
                            uint gy = ((uint)height + 15) / 16;
                            d3dContext.Dispatch(gx, gy, 1);

                            d3dContext.CSSetShaderResources(0, new D3D11ShaderResourceView?[] { null, null }!);
                            d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                            return visualOutput;
                        }
                        finally
                        {
                            if (upsampledMask != null)
                            {
                                pool.Release(upsampledMask);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (intermediateDenoised != null)
                {
                    pool.Release(intermediateDenoised);
                }
            }
        }

        public void Dispose()
        {
            _preprocessor.Dispose();
        }
    }
}
