using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    public enum TensorNormalization
    {
        /// <summary>
        /// ImageNet normalization: Mean=[0.485, 0.456, 0.406], Std=[0.229, 0.224, 0.225].
        /// </summary>
        ImageNet = 0,

        /// <summary>
        /// Direct [0, 1] range: Mean=[0, 0, 0], Std=[1, 1, 1].
        /// </summary>
        ZeroToOne = 1,

        /// <summary>
        /// Symmetric [-1, 1] range: Mean=[0.5, 0.5, 0.5], Std=[0.5, 0.5, 0.5].
        /// </summary>
        Symmetric = 2
    }

    /// <summary>
    /// Level 11 AI Tensor Preprocessor.
    /// Resizes, normalizes, and transforms textures into planar NCHW FP32 Structured Buffers
    /// directly on the GPU in a single compute dispatch.
    /// </summary>
    public sealed class GpuTensorPreprocessor : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 48)]
        private struct TensorParamsConstants
        {
            public uint TensorSrcWidth;
            public uint TensorSrcHeight;
            public uint TensorDstWidth;
            public uint TensorDstHeight;

            public float MeanR;
            public float MeanG;
            public float MeanB;
            public float PadMean;

            public float StdR;
            public float StdG;
            public float StdB;
            public float PadStd;
        }

        private readonly GpuImageContext _context;

        public GpuTensorPreprocessor(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Preprocesses a GPU VRAM texture into a Planar NCHW FP32 tensor buffer.
        /// </summary>
        public unsafe GpuTensorBuffer Preprocess(
            PooledGpuTexture input,
            int targetWidth,
            int targetHeight,
            TensorNormalization normalization = TensorNormalization.ImageNet)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));

            var tensor = new GpuTensorBuffer(_context.Device, _context.ImmediateContext, 3, targetHeight, targetWidth);

            GetMeanStd(normalization, out float mr, out float mg, out float mb, out float sr, out float sg, out float sb);

            var cb = new TensorParamsConstants
            {
                TensorSrcWidth = (uint)input.Width,
                TensorSrcHeight = (uint)input.Height,
                TensorDstWidth = (uint)targetWidth,
                TensorDstHeight = (uint)targetHeight,
                MeanR = mr,
                MeanG = mg,
                MeanB = mb,
                PadMean = 0f,
                StdR = sr,
                StdG = sg,
                StdB = sb,
                PadStd = 0f
            };

            var d3dContext = _context.ImmediateContext;
            d3dContext.UpdateSubresource(_context.ConstantBuffer, ref cb);

            d3dContext.CSSetShader(_context.CsTensorPreprocessNCHW);
            d3dContext.CSSetConstantBuffers(0, _context.ConstantBuffer);
            d3dContext.CSSetShaderResources(0, input.Srv);
            d3dContext.CSSetUnorderedAccessViews(0, tensor.Uav);

            uint gx = ((uint)targetWidth + 15) / 16;
            uint gy = ((uint)targetHeight + 15) / 16;
            d3dContext.Dispatch(gx, gy, 1);

            // Unbind resources
            d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
            d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

            return tensor;
        }

        /// <summary>
        /// Convenience overload: Preprocesses a CPU ImageBuffer into a Planar NCHW FP32 tensor buffer.
        /// </summary>
        public GpuTensorBuffer Preprocess(
            ImageBuffer input,
            int targetWidth,
            int targetHeight,
            TensorNormalization normalization = TensorNormalization.ImageNet)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var pool = _context.TexturePool;
            var inputTex = pool.Acquire(input.Width, input.Height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
            try
            {
                _context.Transfer.Upload(input, inputTex.Texture);
                return Preprocess(inputTex, targetWidth, targetHeight, normalization);
            }
            finally
            {
                pool.Release(inputTex);
            }
        }

        private static void GetMeanStd(
            TensorNormalization norm,
            out float mr, out float mg, out float mb,
            out float sr, out float sg, out float sb)
        {
            switch (norm)
            {
                case TensorNormalization.ZeroToOne:
                    mr = mg = mb = 0.0f;
                    sr = sg = sb = 1.0f;
                    break;
                case TensorNormalization.Symmetric:
                    mr = mg = mb = 0.5f;
                    sr = sg = sb = 0.5f;
                    break;
                case TensorNormalization.ImageNet:
                default:
                    // ImageNet RGB Means & Stds
                    mr = 0.485f;
                    mg = 0.456f;
                    mb = 0.406f;
                    sr = 0.229f;
                    sg = 0.224f;
                    sb = 0.225f;
                    break;
            }
        }

        public void Dispose()
        {
            // Stateless preprocessor
        }
    }
}
