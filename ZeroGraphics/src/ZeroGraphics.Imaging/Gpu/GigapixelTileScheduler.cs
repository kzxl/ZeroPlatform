using System;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    public sealed class TileProgressReport
    {
        public int TilesCompleted { get; }
        public int TotalTiles { get; }
        public double PercentProgress => TotalTiles > 0 ? (double)TilesCompleted / TotalTiles * 100.0 : 0.0;

        public TileProgressReport(int completed, int total)
        {
            TilesCompleted = completed;
            TotalTiles = total;
        }
    }

    /// <summary>
    /// Level 13 Gigapixel Tiled Image Scheduler.
    /// Decomposes massive 100MP+ images into a virtual tile grid with configurable border aprons,
    /// streaming tiles through a minimal, fixed-size GPU VRAM pool (&lt; 32 MB) to eliminate
    /// out-of-memory errors, GPU TDR driver timeouts, and spatial filtering seam artifacts.
    /// </summary>
    public sealed class GigapixelTileScheduler : IDisposable
    {
        private readonly GpuImageContext _context;
        public int TileSize { get; }
        public int ApronSize { get; }

        public GigapixelTileScheduler(GpuImageContext context, int tileSize = 1024, int apronSize = 8)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (tileSize <= 0) throw new ArgumentOutOfRangeException(nameof(tileSize));
            if (apronSize < 0) throw new ArgumentOutOfRangeException(nameof(apronSize));

            TileSize = tileSize;
            ApronSize = apronSize;
        }

        /// <summary>
        /// Processes a massive gigapixel image through the specified GPU tile processor.
        /// </summary>
        public unsafe void ProcessTiled(
            ImageBuffer source,
            ImageBuffer destination,
            Func<PooledGpuTexture, PooledGpuTexture> tileProcessor,
            IProgress<TileProgressReport>? progress = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (tileProcessor == null) throw new ArgumentNullException(nameof(tileProcessor));

            int width = source.Width;
            int height = source.Height;

            int cols = (width + TileSize - 1) / TileSize;
            int rows = (height + TileSize - 1) / TileSize;
            int totalTiles = cols * rows;
            int completedTiles = 0;

            int maxTileDim = TileSize + 2 * ApronSize;
            var pool = _context.TexturePool;
            var transfer = _context.Transfer;

            // Allocate persistent reusable staging buffers for tile streaming
            using (var tileInBuf = new ImageBuffer(maxTileDim, maxTileDim, source.Format))
            using (var tileOutBuf = new ImageBuffer(maxTileDim, maxTileDim, destination.Format))
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int coreX = c * TileSize;
                        int coreY = r * TileSize;
                        int coreW = Math.Min(TileSize, width - coreX);
                        int coreH = Math.Min(TileSize, height - coreY);

                        // Calculate surrounding aprons
                        int apronLeft = Math.Min(ApronSize, coreX);
                        int apronTop = Math.Min(ApronSize, coreY);
                        int apronRight = Math.Min(ApronSize, width - (coreX + coreW));
                        int apronBottom = Math.Min(ApronSize, height - (coreY + coreH));

                        int extX = coreX - apronLeft;
                        int extY = coreY - apronTop;
                        int extW = coreW + apronLeft + apronRight;
                        int extH = coreH + apronTop + apronBottom;

                        // 1. Copy extended tile region from source into tileInBuf
                        int bytesPerPixel = source.Format == ImageFormatMode.Bgra32 ? 4 : 1;
                        int extLineBytes = extW * bytesPerPixel;

                        for (int y = 0; y < extH; y++)
                        {
                            byte* pSrcRow = source.GetRowPointer(extY + y) + extX * bytesPerPixel;
                            byte* pDstRow = tileInBuf.GetRowPointer(y);
                            Buffer.MemoryCopy(pSrcRow, pDstRow, extLineBytes, extLineBytes);
                        }

                        // 2. Upload extended tile to GPU
                        var gpuIn = pool.Acquire(extW, extH, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                        try
                        {
                            transfer.Upload(tileInBuf, gpuIn.Texture);

                            // 3. Run GPU kernel / pipeline on tile
                            var gpuOut = tileProcessor(gpuIn);
                            try
                            {
                                // 4. Download tile result to host
                                transfer.Download(gpuOut.Texture, tileOutBuf);

                                // 5. Stitch core (without apron) into destination buffer
                                int coreLineBytes = coreW * bytesPerPixel;
                                for (int y = 0; y < coreH; y++)
                                {
                                    byte* pTileCoreRow = tileOutBuf.GetRowPointer(apronTop + y) + apronLeft * bytesPerPixel;
                                    byte* pDstRow = destination.GetRowPointer(coreY + y) + coreX * bytesPerPixel;
                                    Buffer.MemoryCopy(pTileCoreRow, pDstRow, coreLineBytes, coreLineBytes);
                                }
                            }
                            finally
                            {
                                if (gpuOut != gpuIn)
                                {
                                    pool.Release(gpuOut);
                                }
                            }
                        }
                        finally
                        {
                            pool.Release(gpuIn);
                        }

                        completedTiles++;
                        progress?.Report(new TileProgressReport(completedTiles, totalTiles));
                    }
                }
            }
        }

        /// <summary>
        /// Processes a massive gigapixel image directly streamed from/to disk-backed memory-mapped files,
        /// bypassing host RAM allocations.
        /// </summary>
        public unsafe void ProcessTiled(
            MemoryMappedImageBuffer source,
            MemoryMappedImageBuffer destination,
            Func<PooledGpuTexture, PooledGpuTexture> tileProcessor,
            IProgress<TileProgressReport>? progress = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (tileProcessor == null) throw new ArgumentNullException(nameof(tileProcessor));

            int width = source.Width;
            int height = source.Height;

            int cols = (width + TileSize - 1) / TileSize;
            int rows = (height + TileSize - 1) / TileSize;
            int totalTiles = cols * rows;
            int completedTiles = 0;

            int maxTileDim = TileSize + 2 * ApronSize;
            var pool = _context.TexturePool;
            var transfer = _context.Transfer;

            using (var tileInBuf = new ImageBuffer(maxTileDim, maxTileDim, source.Format))
            using (var tileOutBuf = new ImageBuffer(maxTileDim, maxTileDim, destination.Format))
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int coreX = c * TileSize;
                        int coreY = r * TileSize;
                        int coreW = Math.Min(TileSize, width - coreX);
                        int coreH = Math.Min(TileSize, height - coreY);

                        int apronLeft = Math.Min(ApronSize, coreX);
                        int apronTop = Math.Min(ApronSize, coreY);
                        int apronRight = Math.Min(ApronSize, width - (coreX + coreW));
                        int apronBottom = Math.Min(ApronSize, height - (coreY + coreH));

                        int extX = coreX - apronLeft;
                        int extY = coreY - apronTop;
                        int extW = coreW + apronLeft + apronRight;
                        int extH = coreH + apronTop + apronBottom;

                        // 1. Read directly from disk-backed memory mapped buffer
                        source.ReadSubrect(extX, extY, extW, extH, tileInBuf);

                        // 2. Upload extended tile to GPU
                        var gpuIn = pool.Acquire(extW, extH, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                        try
                        {
                            transfer.Upload(tileInBuf, gpuIn.Texture);

                            // 3. Run GPU kernel / pipeline on tile
                            var gpuOut = tileProcessor(gpuIn);
                            try
                            {
                                // 4. Download tile result to host
                                transfer.Download(gpuOut.Texture, tileOutBuf);

                                // 5. Stitch core directly into destination disk-backed file
                                int bytesPerPixel = destination.BytesPerPixel;
                                int coreLineBytes = coreW * bytesPerPixel;
                                for (int y = 0; y < coreH; y++)
                                {
                                    byte* pTileCoreRow = tileOutBuf.GetRowPointer(apronTop + y) + apronLeft * bytesPerPixel;
                                    byte* pDstRow = destination.GetRowPointer(coreY + y) + coreX * bytesPerPixel;
                                    Buffer.MemoryCopy(pTileCoreRow, pDstRow, coreLineBytes, coreLineBytes);
                                }
                            }
                            finally
                            {
                                if (gpuOut != gpuIn)
                                {
                                    pool.Release(gpuOut);
                                }
                            }
                        }
                        finally
                        {
                            pool.Release(gpuIn);
                        }

                        completedTiles++;
                        progress?.Report(new TileProgressReport(completedTiles, totalTiles));
                    }
                }
            }

            destination.Flush();
        }

        public void Dispose()
        {
            // Context manages shared resources
        }
    }
}
