using System;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Pipeline;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Level 7 Zero-Copy Presenter.
    /// Pipes processed textures directly from GPU VRAM / Compute Shader UAVs into a DXGI SwapChain BackBuffer
    /// via direct VRAM-to-VRAM DMA CopyResource, completely bypassing CPU host memory roundtrips.
    /// </summary>
    public static class ZeroCopyPresenter
    {
        /// <summary>
        /// Presents a GPU texture directly to the specified HwndSwapChain with 0 CPU memory copies.
        /// </summary>
        public static bool Present(D3D11DeviceContext context, HwndSwapChain swapChain, D3D11Texture2D sourceTexture, uint syncInterval = 0)
        {
            if (context == null || !context.IsValid) throw new ArgumentNullException(nameof(context));
            if (swapChain == null || !swapChain.IsValid) throw new ArgumentNullException(nameof(swapChain));
            if (sourceTexture == null || !sourceTexture.IsValid) throw new ArgumentNullException(nameof(sourceTexture));

            IntPtr pBackBuffer = swapChain.GetBackBufferTexture();
            if (pBackBuffer == IntPtr.Zero) return false;

            try
            {
                // Direct GPU VRAM-to-VRAM copy
                ComVTableHelper.CopyResource(context.Handle, pBackBuffer, sourceTexture.Handle);
            }
            finally
            {
                ComVTableHelper.Release(pBackBuffer);
            }

            return swapChain.Present(syncInterval);
        }

        /// <summary>
        /// Presents a PooledGpuTexture directly to the specified HwndSwapChain with 0 CPU memory copies.
        /// </summary>
        public static bool Present(D3D11DeviceContext context, HwndSwapChain swapChain, PooledGpuTexture sourceTexture, uint syncInterval = 0)
        {
            if (sourceTexture == null) throw new ArgumentNullException(nameof(sourceTexture));
            return Present(context, swapChain, sourceTexture.Texture, syncInterval);
        }

        /// <summary>
        /// Executes a CompiledImagePipeline directly into the SwapChain's backbuffer without downloading to CPU memory.
        /// </summary>
        public static bool ExecuteAndPresent(
            CompiledImagePipeline pipeline,
            ImageBuffer sourceImage,
            HwndSwapChain swapChain,
            uint syncInterval = 0)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (swapChain == null || !swapChain.IsValid) throw new ArgumentNullException(nameof(swapChain));

            // Execute pipeline to GPU texture (Data Residency on VRAM)
            using var gpuResult = pipeline.ExecuteToGpu(sourceImage);

            // Present directly from VRAM to SwapChain
            return Present(pipeline.Context.ImmediateContext, swapChain, gpuResult.Texture, syncInterval);
        }
    }
}
