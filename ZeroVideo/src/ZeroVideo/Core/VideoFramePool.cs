using System;
using System.Buffers;
using System.Threading;
using ZeroPrimitives.Memory;

namespace ZeroVideo.Core
{
    /// <summary>
    /// Specifies the underlying memory allocation backend for video frame pooling.
    /// </summary>
    public enum FramePoolBackend
    {
        /// <summary>
        /// Allocates from the managed <see cref="ArrayPool{T}.Shared"/>. Suitable for general UI and desktop apps.
        /// </summary>
        ManagedArrayPool,

        /// <summary>
        /// Allocates from an off-heap <see cref="SlabAllocator"/>. Completely outside GC scan/compaction;
        /// eliminates Gen-2 LOH fragmentation during continuous 4K/8K camera streaming.
        /// </summary>
        OffHeapSlab
    }

    /// <summary>
    /// Represents a video frame buffer whose underlying memory is rented from an <see cref="ArrayPool{T}"/>.
    /// Disposing this frame buffer returns the rented byte array back to the pool, preventing LOH allocations.
    /// </summary>
    public class PooledVideoFrameBuffer : VideoFrameBuffer
    {
        private readonly ArrayPool<byte> _pool;
        private byte[]? _rentedArray;
        private bool _isDisposed;

        public override bool IsDisposed => _isDisposed;

        internal PooledVideoFrameBuffer(
            int width,
            int height,
            VideoPixelFormat pixelFormat,
            byte[] rentedArray,
            ArrayPool<byte> pool,
            int stride = 0)
            : base(width, height, pixelFormat, rentedArray, stride)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _rentedArray = rentedArray ?? throw new ArgumentNullException(nameof(rentedArray));
        }

        public override Span<byte> AsSpan()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PooledVideoFrameBuffer));
            return base.AsSpan();
        }

        public override ReadOnlySpan<byte> AsReadOnlySpan()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PooledVideoFrameBuffer));
            return base.AsReadOnlySpan();
        }

        public override Span<byte> GetRowSpan(int y)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PooledVideoFrameBuffer));
            return base.GetRowSpan(y);
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_rentedArray != null)
                {
                    _pool.Return(_rentedArray);
                    _rentedArray = null;
                }
            }
        }
    }

    /// <summary>
    /// Represents a video frame buffer whose underlying memory is rented from an off-heap <see cref="SlabAllocator"/>.
    /// 100% off-heap, zero GC allocations, zero Gen-2 LOH pauses.
    /// </summary>
    public unsafe class NativeVideoFrameBuffer : VideoFrameBuffer
    {
        private NativeMemoryBlock? _block;

        public override bool IsDisposed => _block == null || _block.IsDisposed;

        /// <summary>
        /// Gets the underlying off-heap <see cref="NativeMemoryBlock"/>.
        /// </summary>
        public NativeMemoryBlock? NativeBlock => _block;

        internal NativeVideoFrameBuffer(
            int width,
            int height,
            VideoPixelFormat pixelFormat,
            NativeMemoryBlock block,
            int stride = 0)
            : base(width, height, pixelFormat, Array.Empty<byte>(), stride)
        {
            _block = block ?? throw new ArgumentNullException(nameof(block));
        }

        public override Span<byte> AsSpan()
        {
            if (IsDisposed || _block == null)
                throw new ObjectDisposedException(nameof(NativeVideoFrameBuffer));
            return _block.Span;
        }

        public override ReadOnlySpan<byte> AsReadOnlySpan()
        {
            if (IsDisposed || _block == null)
                throw new ObjectDisposedException(nameof(NativeVideoFrameBuffer));
            return _block.ReadOnlySpan;
        }

        public override Span<byte> GetRowSpan(int y)
        {
            if (IsDisposed || _block == null)
                throw new ObjectDisposedException(nameof(NativeVideoFrameBuffer));
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y));

            int rowBytes = (PixelFormat == VideoPixelFormat.Nv12 || PixelFormat == VideoPixelFormat.Yuv420p)
                ? Width
                : Width * GetBytesPerPixel(PixelFormat);

            return new Span<byte>(_block.Pointer + (y * Stride), rowBytes);
        }

        public override void Dispose()
        {
            if (_block != null)
            {
                _block.Dispose();
                _block = null;
            }
        }
    }

    /// <summary>
    /// High-throughput buffer pool for recycling video frame memory in continuous streaming pipelines.
    /// Supports both managed <see cref="ArrayPool{T}"/> and off-heap <see cref="SlabAllocator"/> backends.
    /// </summary>
    public class VideoFramePool : IDisposable
    {
        private readonly FramePoolBackend _backend;
        private readonly ArrayPool<byte>? _managedPool;
        private readonly SlabAllocator? _slabAllocator;
        private int _disposed;

        /// <summary>
        /// Global shared video frame memory pool (managed ArrayPool backend).
        /// </summary>
        public static VideoFramePool Shared { get; } = new VideoFramePool(FramePoolBackend.ManagedArrayPool);

        /// <summary>
        /// Global shared off-heap video frame memory pool (SlabAllocator backend for zero-LOH streaming).
        /// Pre-allocates 8MB slabs suited for up to 4K RGB/YUV uncompressed frames.
        /// </summary>
        public static VideoFramePool OffHeap { get; } = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 8 * 1024 * 1024);

        /// <summary>
        /// Gets the allocation backend used by this pool.
        /// </summary>
        public FramePoolBackend Backend => _backend;

        /// <summary>
        /// Gets whether the pool has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Gets the count of active blocks leased from the slab allocator, if using off-heap backend.
        /// </summary>
        public int ActiveBlocks => _slabAllocator?.ActiveBlocks ?? 0;

        /// <summary>
        /// Gets whether there are active unmanaged frame blocks currently rented from the slab allocator.
        /// </summary>
        public bool HasActiveLeases => (_slabAllocator?.ActiveBlocks ?? 0) > 0;

        public VideoFramePool(FramePoolBackend backend = FramePoolBackend.ManagedArrayPool, int slabSize = 4 * 1024 * 1024)
        {
            _backend = backend;
            if (backend == FramePoolBackend.OffHeapSlab)
            {
                _slabAllocator = new SlabAllocator(slabSize, initialSlabs: 4, maxSlabs: 64);
            }
            else
            {
                _managedPool = ArrayPool<byte>.Shared;
            }
        }

        public VideoFramePool(ArrayPool<byte> pool)
        {
            _backend = FramePoolBackend.ManagedArrayPool;
            _managedPool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>
        /// Rents a pooled video frame buffer with the specified dimensions and pixel format.
        /// </summary>
        public VideoFrameBuffer Rent(int width, int height, VideoPixelFormat pixelFormat, int stride = 0)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(VideoFramePool), "Cannot rent from a disposed VideoFramePool.");

            int totalBytes = VideoFrameBuffer.CalculateTotalBytes(width, height, pixelFormat, stride);

            if (_backend == FramePoolBackend.OffHeapSlab && _slabAllocator != null)
            {
                if (totalBytes > _slabAllocator.BlockSize)
                {
                    // Oversize frame exceeding slab block size: fall back cleanly to unpooled native allocation
                    NativeMemoryBlock unpooledBlock = NativeMemoryBlock.Allocate(totalBytes);
                    return new NativeVideoFrameBuffer(width, height, pixelFormat, unpooledBlock, stride);
                }

                NativeMemoryBlock block = _slabAllocator.Rent(totalBytes);
                return new NativeVideoFrameBuffer(width, height, pixelFormat, block, stride);
            }

            byte[] rented = (_managedPool ?? ArrayPool<byte>.Shared).Rent(totalBytes);
            return new PooledVideoFrameBuffer(width, height, pixelFormat, rented, _managedPool ?? ArrayPool<byte>.Shared, stride);
        }

        /// <summary>
        /// Rents a pooled video frame buffer with timestamp and frame index initialized.
        /// </summary>
        public VideoFrameBuffer Rent(
            int width,
            int height,
            VideoPixelFormat pixelFormat,
            long timestampNs,
            long frameIndex,
            int stride = 0)
        {
            var buffer = Rent(width, height, pixelFormat, stride);
            buffer.TimestampNs = timestampNs;
            buffer.FrameIndex = frameIndex;
            return buffer;
        }

        /// <summary>
        /// Performs a graceful shutdown of the pool, waiting up to the specified timeout for active frame leases to be returned before disposing.
        /// </summary>
        public bool WaitForShutdown(TimeSpan timeout)
        {
            if (_slabAllocator == null)
            {
                Dispose();
                return true;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_slabAllocator.ActiveBlocks > 0 && sw.Elapsed < timeout)
            {
                Thread.Sleep(10);
            }

            bool drained = _slabAllocator.ActiveBlocks == 0;
            Dispose();
            return drained;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _slabAllocator?.Dispose();
        }
    }
}
