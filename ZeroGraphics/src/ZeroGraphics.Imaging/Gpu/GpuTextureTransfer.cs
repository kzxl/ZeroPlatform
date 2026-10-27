using System;
using System.Buffers;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// High-performance Zero-Copy DMA Host &lt;-&gt; Device transfer manager.
    /// Bridges pinned CPU ImageBuffer memory and GPU VRAM textures with zero managed allocations.
    /// </summary>
    public sealed class GpuTextureTransfer : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11Texture2D? _cachedStagingTexture;
        private int _cachedStagingWidth;
        private int _cachedStagingHeight;
        private DXGI_FORMAT _cachedStagingFormat;

        private bool _disposed;

        public GpuTextureTransfer(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Uploads CPU ImageBuffer directly to a GPU texture via direct DMA UpdateSubresource.
        /// Zero managed allocations on both R8_UNORM and BGRA paths.
        /// </summary>
        public unsafe void Upload(ImageBuffer source, D3D11Texture2D targetTexture)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetTexture == null || !targetTexture.IsValid) throw new ArgumentNullException(nameof(targetTexture));

            UploadRaw((IntPtr)source.Scan0, source.Width, source.Height, source.Stride, source.Format, targetTexture);
        }

        /// <summary>
        /// Uploads unmanaged frame memory directly to a GPU texture via direct DMA UpdateSubresource.
        /// Completely bypasses managed object creation (0 GC allocations).
        /// </summary>
        public unsafe void UploadRaw(
            IntPtr pScan0,
            int width,
            int height,
            int stride,
            ImageFormatMode format,
            D3D11Texture2D targetTexture)
        {
            if (pScan0 == IntPtr.Zero) throw new ArgumentNullException(nameof(pScan0));
            if (targetTexture == null || !targetTexture.IsValid) throw new ArgumentNullException(nameof(targetTexture));

            if (format == ImageFormatMode.Bgra32)
            {
                ComVTableHelper.UpdateSubresource(
                    _context.Handle,
                    targetTexture.Handle,
                    0,
                    IntPtr.Zero,
                    pScan0,
                    (uint)stride,
                    0);
            }
            else if (format == ImageFormatMode.Gray8)
            {
                // If the target is R8_UNORM, upload directly with 0 conversion and 0 allocations
                if (targetTexture.Description.Format == DXGI_FORMAT.DXGI_FORMAT_R8_UNORM)
                {
                    ComVTableHelper.UpdateSubresource(
                        _context.Handle,
                        targetTexture.Handle,
                        0,
                        IntPtr.Zero,
                        pScan0,
                        (uint)stride,
                        0);
                }
                else if (targetTexture.Description.Format == DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
                {
                    // Expand Gray8 -> BGRA via pooled array (0 heap allocation)
                    int bgraStride = width * 4;
                    int totalBytes = bgraStride * height;
                    byte[] rented = ArrayPool<byte>.Shared.Rent(totalBytes);

                    try
                    {
                        fixed (byte* pBgra = rented)
                        {
                            byte* pSrcBase = (byte*)pScan0;
                            for (int y = 0; y < height; y++)
                            {
                                byte* pSrcRow = pSrcBase + y * stride;
                                uint* pDstRow = (uint*)(pBgra + y * bgraStride);
                                for (int x = 0; x < width; x++)
                                {
                                    byte g = pSrcRow[x];
                                    pDstRow[x] = (uint)(g | (g << 8) | (g << 16) | (0xFF << 24));
                                }
                            }

                            ComVTableHelper.UpdateSubresource(
                                _context.Handle,
                                targetTexture.Handle,
                                0,
                                IntPtr.Zero,
                                (IntPtr)pBgra,
                                (uint)bgraStride,
                                0);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
                else
                {
                    ComVTableHelper.UpdateSubresource(
                        _context.Handle,
                        targetTexture.Handle,
                        0,
                        IntPtr.Zero,
                        pScan0,
                        (uint)stride,
                        0);
                }
            }
        }

        /// <summary>
        /// Downloads GPU texture back to CPU ImageBuffer via DMA Staging Readback.
        /// </summary>
        public unsafe void Download(D3D11Texture2D sourceGpuTexture, ImageBuffer destination)
        {
            if (sourceGpuTexture == null || !sourceGpuTexture.IsValid) throw new ArgumentNullException(nameof(sourceGpuTexture));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            int width = (int)sourceGpuTexture.Description.Width;
            int height = (int)sourceGpuTexture.Description.Height;
            var format = sourceGpuTexture.Description.Format;

            EnsureStagingTexture(width, height, format);

            // 1. Copy VRAM Default -> Staging
            ComVTableHelper.CopyResource(_context.Handle, _cachedStagingTexture!.Handle, sourceGpuTexture.Handle);

            // 2. Map Staging Texture for CPU Read
            int hr = ComVTableHelper.Map(_context.Handle, _cachedStagingTexture.Handle, 0, D3D11_MAP.D3D11_MAP_READ, 0, out var mapped);
            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to map GPU staging texture for readback. HRESULT=0x{hr:X8}");

            try
            {
                byte* pSrc = (byte*)mapped.pData;
                byte* pDst = (byte*)destination.Scan0;

                int minHeight = Math.Min(height, destination.Height);

                if (destination.Format == ImageFormatMode.Bgra32)
                {
                    int lineBytes = Math.Min(destination.Width * 4, destination.Stride);
                    for (int y = 0; y < minHeight; y++)
                    {
                        Buffer.MemoryCopy(pSrc + y * mapped.RowPitch, pDst + y * destination.Stride, lineBytes, lineBytes);
                    }
                }
                else if (destination.Format == ImageFormatMode.Gray8)
                {
                    int minWidth = Math.Min(width, destination.Width);
                    for (int y = 0; y < minHeight; y++)
                    {
                        byte* pSrcRow = pSrc + y * mapped.RowPitch;
                        byte* pDstRow = destination.GetRowPointer(y);

                        // If GPU texture is BGRA32, read green/blue channel or compute luma
                        if (format == DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
                        {
                            int x = 0;
                            int unrollLimit = minWidth - 4;
                            for (; x <= unrollLimit; x += 4)
                            {
                                int o0 = x * 4;
                                pDstRow[x]     = pSrcRow[o0];
                                pDstRow[x + 1] = pSrcRow[o0 + 4];
                                pDstRow[x + 2] = pSrcRow[o0 + 8];
                                pDstRow[x + 3] = pSrcRow[o0 + 12];
                            }
                            for (; x < minWidth; x++)
                            {
                                pDstRow[x] = pSrcRow[x * 4]; // blue/gray channel
                            }
                        }
                        else
                        {
                            Buffer.MemoryCopy(pSrcRow, pDstRow, minWidth, minWidth);
                        }
                    }
                }
            }
            finally
            {
                ComVTableHelper.Unmap(_context.Handle, _cachedStagingTexture.Handle, 0);
            }
        }

        private void EnsureStagingTexture(int width, int height, DXGI_FORMAT format)
        {
            if (_cachedStagingTexture != null &&
                _cachedStagingWidth == width &&
                _cachedStagingHeight == height &&
                _cachedStagingFormat == format)
            {
                return;
            }

            _cachedStagingTexture?.Dispose();

            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0
            };

            _cachedStagingTexture = _device.CreateTexture2D(ref desc);
            _cachedStagingWidth = width;
            _cachedStagingHeight = height;
            _cachedStagingFormat = format;
        }

        /// <summary>
        /// Creates an asynchronous double/triple-buffered staging ring buffer for zero-stall GPU readback.
        /// </summary>
        public AsyncStagingRingBuffer CreateAsyncStagingRingBuffer(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, int ringSize = 3)
        {
            return new AsyncStagingRingBuffer(_device, _context, width, height, format, ringSize);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cachedStagingTexture?.Dispose();
                _cachedStagingTexture = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Asynchronous double/triple-buffered staging ring buffer for zero-stall GPU-to-CPU readbacks.
    /// Decouples CPU pipeline from GPU execution by rotating staging surfaces and supporting non-blocking Map queries.
    /// </summary>
    public sealed class AsyncStagingRingBuffer : IDisposable
    {
        private const uint D3D11_MAP_FLAG_DO_NOT_WAIT = 0x00100000;
        private const int DXGI_ERROR_WAS_STILL_DRAWING = unchecked((int)0x887A000A);

        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;
        private readonly D3D11Texture2D[] _stagingTextures;
        private readonly int _capacity;
        private int _writeIndex;
        private bool _disposed;

        public int Width { get; }
        public int Height { get; }
        public DXGI_FORMAT Format { get; }
        public int Capacity => _capacity;

        public AsyncStagingRingBuffer(D3D11Device device, D3D11DeviceContext context, int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, int ringSize = 3)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (ringSize < 2) ringSize = 2;

            Width = width;
            Height = height;
            Format = format;
            _capacity = ringSize;
            _stagingTextures = new D3D11Texture2D[ringSize];

            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0
            };

            for (int i = 0; i < ringSize; i++)
            {
                _stagingTextures[i] = _device.CreateTexture2D(ref desc);
            }
        }

        /// <summary>
        /// Dispatches an asynchronous GPU-to-staging copy command and returns a ticket index for subsequent readback.
        /// Does NOT stall the CPU thread.
        /// </summary>
        public int EnqueueCopy(D3D11Texture2D sourceGpuTexture)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AsyncStagingRingBuffer));
            if (sourceGpuTexture == null || !sourceGpuTexture.IsValid) throw new ArgumentNullException(nameof(sourceGpuTexture));

            int slot = _writeIndex;
            _writeIndex = (_writeIndex + 1) % _capacity;

            ComVTableHelper.CopyResource(_context.Handle, _stagingTextures[slot].Handle, sourceGpuTexture.Handle);
            return slot;
        }

        /// <summary>
        /// Attempts readback of the specified frame ticket. If non-blocking and the GPU is still rendering/copying, returns false immediately without stalling CPU.
        /// </summary>
        public unsafe bool TryReadback(int ticket, ImageBuffer destination, bool waitForGpu = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AsyncStagingRingBuffer));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (ticket < 0 || ticket >= _capacity) throw new ArgumentOutOfRangeException(nameof(ticket));

            var staging = _stagingTextures[ticket];
            uint mapFlags = waitForGpu ? 0 : D3D11_MAP_FLAG_DO_NOT_WAIT;

            int hr = ComVTableHelper.Map(_context.Handle, staging.Handle, 0, D3D11_MAP.D3D11_MAP_READ, mapFlags, out var mapped);
            if (hr == DXGI_ERROR_WAS_STILL_DRAWING)
            {
                return false; // GPU is still processing frame, CPU continues without blocking!
            }

            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to map staging texture for readback. HRESULT=0x{hr:X8}");

            try
            {
                byte* pSrc = (byte*)mapped.pData;
                byte* pDst = (byte*)destination.Scan0;

                int minHeight = Math.Min(Height, destination.Height);

                if (destination.Format == ImageFormatMode.Bgra32)
                {
                    int lineBytes = Math.Min(destination.Width * 4, destination.Stride);
                    for (int y = 0; y < minHeight; y++)
                    {
                        Buffer.MemoryCopy(pSrc + y * mapped.RowPitch, pDst + y * destination.Stride, lineBytes, lineBytes);
                    }
                }
                else if (destination.Format == ImageFormatMode.Gray8)
                {
                    int minWidth = Math.Min(Width, destination.Width);
                    for (int y = 0; y < minHeight; y++)
                    {
                        byte* pSrcRow = pSrc + y * mapped.RowPitch;
                        byte* pDstRow = destination.GetRowPointer(y);

                        if (Format == DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
                        {
                            int x = 0;
                            int unrollLimit = minWidth - 4;
                            for (; x <= unrollLimit; x += 4)
                            {
                                int o0 = x * 4;
                                pDstRow[x]     = pSrcRow[o0];
                                pDstRow[x + 1] = pSrcRow[o0 + 4];
                                pDstRow[x + 2] = pSrcRow[o0 + 8];
                                pDstRow[x + 3] = pSrcRow[o0 + 12];
                            }
                            for (; x < minWidth; x++)
                            {
                                pDstRow[x] = pSrcRow[x * 4];
                            }
                        }
                        else
                        {
                            Buffer.MemoryCopy(pSrcRow, pDstRow, minWidth, minWidth);
                        }
                    }
                }
                return true;
            }
            finally
            {
                ComVTableHelper.Unmap(_context.Handle, staging.Handle, 0);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                for (int i = 0; i < _stagingTextures.Length; i++)
                {
                    _stagingTextures[i]?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
