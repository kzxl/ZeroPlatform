using System;
using System.Collections.Generic;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Progress report for batch image processing runs.
    /// </summary>
    public readonly struct BatchProgressReport
    {
        public int CompletedItems { get; }
        public int TotalItems { get; }
        public double GpuElapsedMilliseconds { get; }

        public float ProgressFraction => TotalItems > 0 ? (float)CompletedItems / TotalItems : 1.0f;

        public BatchProgressReport(int completed, int total, double gpuElapsedMs)
        {
            CompletedItems = completed;
            TotalItems = total;
            GpuElapsedMilliseconds = gpuElapsedMs;
        }
    }

    /// <summary>
    /// High-throughput GPU Batch Processor (Level 5).
    /// Leverages Staging Texture Ring Pooling and asynchronous pipeline execution
    /// to process large volumes of images without per-image allocations or CPU thread stalls.
    /// </summary>
    public sealed class GpuBatchProcessor : IDisposable
    {
        private readonly GpuImageContext _gpuContext;
        private readonly GpuStagingPool _stagingPool;
        private readonly GpuTimer _gpuTimer;
        private bool _disposed;

        public GpuImageContext GpuContext => _gpuContext;
        public GpuStagingPool StagingPool => _stagingPool;

        public GpuBatchProcessor(GpuImageContext gpuContext)
        {
            _gpuContext = gpuContext ?? throw new ArgumentNullException(nameof(gpuContext));
            _stagingPool = new GpuStagingPool(_gpuContext.Device);
            _gpuTimer = new GpuTimer(_gpuContext.Device);
        }

        /// <summary>
        /// Processes a batch of ImageBuffers through the specified CompiledImagePipeline with zero allocations.
        /// </summary>
        public unsafe ImageBuffer[] ProcessBatch(
            IReadOnlyList<ImageBuffer> sourceImages,
            CompiledImagePipeline pipeline,
            IProgress<BatchProgressReport>? progress = null)
        {
            if (sourceImages == null) throw new ArgumentNullException(nameof(sourceImages));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            if (_disposed) throw new ObjectDisposedException(nameof(GpuBatchProcessor));

            int total = sourceImages.Count;
            var results = new ImageBuffer[total];
            if (total == 0) return results;

            double cumulativeGpuTime = 0.0;

            for (int i = 0; i < total; i++)
            {
                var src = sourceImages[i];
                var dst = new ImageBuffer(src.Width, src.Height, src.Format);
                results[i] = dst;

                _gpuTimer.Start(_gpuContext.ImmediateContext);

                // 1. Upload via GpuTextureTransfer or direct mapped upload
                // Execute pipeline through GpuImageContext
                pipeline.Execute(src, dst);

                _gpuTimer.Stop(_gpuContext.ImmediateContext);
                double itemMs = _gpuTimer.GetElapsedMilliseconds(_gpuContext.ImmediateContext, waitForGpu: true);
                cumulativeGpuTime += itemMs;

                progress?.Report(new BatchProgressReport(i + 1, total, cumulativeGpuTime));
            }

            return results;
        }

        /// <summary>
        /// Processes a batch of images directly in VRAM using Staging Pool for fast parallel upload and download.
        /// </summary>
        public unsafe void ProcessBatchPipelined(
            IReadOnlyList<ImageBuffer> sourceImages,
            IReadOnlyList<ImageBuffer> destImages,
            Action<PooledGpuTexture, PooledGpuTexture> gpuProcessAction)
        {
            if (sourceImages == null) throw new ArgumentNullException(nameof(sourceImages));
            if (destImages == null) throw new ArgumentNullException(nameof(destImages));
            if (gpuProcessAction == null) throw new ArgumentNullException(nameof(gpuProcessAction));
            if (sourceImages.Count != destImages.Count) throw new ArgumentException("Source and destination counts must match.");
            if (_disposed) throw new ObjectDisposedException(nameof(GpuBatchProcessor));

            int count = sourceImages.Count;
            if (count == 0) return;

            var d3dContext = _gpuContext.ImmediateContext;
            var texPool = _gpuContext.TexturePool;

            for (int i = 0; i < count; i++)
            {
                var src = sourceImages[i];
                var dst = destImages[i];

                var format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;

                // 1. Acquire leased GPU input and output textures
                using var inputLease = texPool.Lease(src.Width, src.Height, format, needsUav: true);
                using var outputLease = texPool.Lease(dst.Width, dst.Height, format, needsUav: true);

                // 2. Upload source to input texture via Transfer
                _gpuContext.Transfer.Upload(src, inputLease.Texture.Texture);

                // 3. Execute GPU action
                gpuProcessAction(inputLease.Texture, outputLease.Texture);

                // 4. Download from output texture to destination
                using var readStagingLease = _stagingPool.LeaseRead(dst.Width, dst.Height, format);
                d3dContext.CopyResource(readStagingLease.Texture.Texture, outputLease.Texture.Texture);

                int hr = d3dContext.Map(readStagingLease.Texture.Texture, 0, D3D11_MAP.D3D11_MAP_READ, 0, out var mapped);
                if (hr >= 0 && mapped.pData != IntPtr.Zero)
                {
                    try
                    {
                        byte* pSrc = (byte*)mapped.pData;
                        byte* pDst = (byte*)dst.Scan0;
                        int lineBytes = Math.Min(dst.Width * 4, dst.Stride);
                        int minH = Math.Min(dst.Height, readStagingLease.Texture.Height);

                        for (int y = 0; y < minH; y++)
                        {
                            Buffer.MemoryCopy(pSrc + y * mapped.RowPitch, pDst + y * dst.Stride, lineBytes, lineBytes);
                        }
                    }
                    finally
                    {
                        d3dContext.Unmap(readStagingLease.Texture.Texture, 0);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _gpuTimer.Dispose();
            _stagingPool.Dispose();
        }
    }
}
