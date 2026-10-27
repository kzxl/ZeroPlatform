using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Represents a multi-scale Gaussian or Laplacian Image Pyramid (Level 8).
    /// Generates multi-octave representations in VRAM using CS 5.0 5x5 binomial filtering.
    /// </summary>
    public sealed class GpuImagePyramid : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct PyramidDownConstants
        {
            public uint DstWidth;
            public uint DstHeight;
            public uint SrcWidth;
            public uint SrcHeight;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct PyramidUpConstants
        {
            public uint UpDstWidth;
            public uint UpDstHeight;
            public uint UpSrcWidth;
            public uint UpSrcHeight;
        }

        private readonly GpuImageContext _context;
        private readonly List<PooledGpuTexture> _levels = new List<PooledGpuTexture>();
        private bool _disposed;

        public IReadOnlyList<PooledGpuTexture> Levels => _levels;
        public int OctaveCount => _levels.Count;
        public int Count => _levels.Count;
        public PooledGpuTexture this[int index] => _levels[index];

        private GpuImagePyramid(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Builds an N-level Gaussian Pyramid from an input GPU texture.
        /// Level 0 is the original texture (or full-resolution copy), followed by successive 2x downscaled octaves.
        /// </summary>
        public static unsafe GpuImagePyramid BuildGaussian(GpuImageContext context, PooledGpuTexture input, int octaves = 4)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (octaves < 1) throw new ArgumentOutOfRangeException(nameof(octaves));

            var pyramid = new GpuImagePyramid(context);
            var d3dContext = context.ImmediateContext;
            var pool = context.TexturePool;

            int curW = input.Width;
            int curH = input.Height;

            // Level 0: Full-resolution copy
            var level0 = pool.Acquire(curW, curH, input.Format, needsUav: true);
            d3dContext.CopyResource(level0.Texture, input.Texture);
            pyramid._levels.Add(level0);

            PooledGpuTexture previousLevel = level0;

            for (int lvl = 1; lvl < octaves; lvl++)
            {
                int nextW = Math.Max(1, curW / 2);
                int nextH = Math.Max(1, curH / 2);
                if (nextW == curW && nextH == curH) break;

                var nextLevel = pool.Acquire(nextW, nextH, input.Format, needsUav: true);

                // Prepare Downscale Constant Buffer
                var cb = new PyramidDownConstants
                {
                    DstWidth = (uint)nextW,
                    DstHeight = (uint)nextH,
                    SrcWidth = (uint)curW,
                    SrcHeight = (uint)curH
                };
                d3dContext.UpdateSubresource(context.ConstantBuffer, ref cb);

                // Dispatch CS_PyramidDown
                d3dContext.CSSetShader(context.CsPyramidDown);
                d3dContext.CSSetConstantBuffers(0, context.ConstantBuffer);
                d3dContext.CSSetShaderResources(0, previousLevel.Srv);
                d3dContext.CSSetUnorderedAccessViews(0, nextLevel.Uav);

                uint gx = ((uint)nextW + 15) / 16;
                uint gy = ((uint)nextH + 15) / 16;
                d3dContext.Dispatch(gx, gy, 1);

                // Unbind resources
                d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                pyramid._levels.Add(nextLevel);
                previousLevel = nextLevel;
                curW = nextW;
                curH = nextH;
            }

            return pyramid;
        }

        /// <summary>
        /// Upsamples a pyramid level to target dimensions using CS 5.0 Bilinear Interpolation.
        /// </summary>
        public static unsafe PooledGpuTexture Upsample(GpuImageContext context, PooledGpuTexture sourceLevel, int targetWidth, int targetHeight)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (sourceLevel == null) throw new ArgumentNullException(nameof(sourceLevel));

            var pool = context.TexturePool;
            var d3dContext = context.ImmediateContext;

            var target = pool.Acquire(targetWidth, targetHeight, sourceLevel.Format, needsUav: true);

            var cb = new PyramidUpConstants
            {
                UpDstWidth = (uint)targetWidth,
                UpDstHeight = (uint)targetHeight,
                UpSrcWidth = (uint)sourceLevel.Width,
                UpSrcHeight = (uint)sourceLevel.Height
            };
            d3dContext.UpdateSubresource(context.ConstantBuffer, ref cb);

            d3dContext.CSSetShader(context.CsPyramidUp);
            d3dContext.CSSetConstantBuffers(0, context.ConstantBuffer);
            d3dContext.CSSetShaderResources(0, sourceLevel.Srv);
            d3dContext.CSSetUnorderedAccessViews(0, target.Uav);

            uint gx = ((uint)targetWidth + 15) / 16;
            uint gy = ((uint)targetHeight + 15) / 16;
            d3dContext.Dispatch(gx, gy, 1);

            d3dContext.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
            d3dContext.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

            return target;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _levels.Count; i++)
            {
                _context.TexturePool.Release(_levels[i]);
            }
            _levels.Clear();
        }
    }
}
