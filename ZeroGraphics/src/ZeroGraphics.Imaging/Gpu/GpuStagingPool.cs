using System;
using System.Collections.Generic;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Represents a pooled Direct3D 11 staging texture (D3D11_USAGE_STAGING) for CPU-GPU transfers.
    /// </summary>
    public sealed class PooledStagingTexture : IDisposable
    {
        public D3D11Texture2D Texture { get; }
        public int Width { get; }
        public int Height { get; }
        public DXGI_FORMAT Format { get; }
        public bool IsReadStaging { get; }

        internal bool IsInUse { get; set; }

        public PooledStagingTexture(D3D11Texture2D texture, int width, int height, DXGI_FORMAT format, bool isReadStaging)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            Width = width;
            Height = height;
            Format = format;
            IsReadStaging = isReadStaging;
            IsInUse = false;
        }

        public void Dispose()
        {
            Texture.Dispose();
        }
    }

    /// <summary>
    /// Pool of reusable Direct3D 11 staging textures for zero-allocation batch upload and download transfers.
    /// </summary>
    public sealed class GpuStagingPool : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly List<PooledStagingTexture> _pool = new List<PooledStagingTexture>();
        private readonly object _lock = new object();
        private bool _disposed;

        public int TotalAllocatedCount
        {
            get
            {
                lock (_lock) return _pool.Count;
            }
        }

        public int ActiveLeasedCount
        {
            get
            {
                lock (_lock)
                {
                    int count = 0;
                    for (int i = 0; i < _pool.Count; i++)
                    {
                        if (_pool[i].IsInUse) count++;
                    }
                    return count;
                }
            }
        }

        public GpuStagingPool(D3D11Device device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <summary>
        /// Acquires a staging texture configured for CPU read (GPU-to-CPU download).
        /// </summary>
        public PooledStagingTexture AcquireReadStaging(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            return AcquireInternal(width, height, format, isReadStaging: true);
        }

        /// <summary>
        /// Acquires a staging texture configured for CPU write (CPU-to-GPU upload).
        /// </summary>
        public PooledStagingTexture AcquireWriteStaging(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            return AcquireInternal(width, height, format, isReadStaging: false);
        }

        private PooledStagingTexture AcquireInternal(int width, int height, DXGI_FORMAT format, bool isReadStaging)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuStagingPool));

                for (int i = 0; i < _pool.Count; i++)
                {
                    var item = _pool[i];
                    if (!item.IsInUse &&
                        item.Width == width &&
                        item.Height == height &&
                        item.Format == format &&
                        item.IsReadStaging == isReadStaging)
                    {
                        item.IsInUse = true;
                        return item;
                    }
                }

                // Allocate a new staging texture
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
                    CPUAccessFlags = isReadStaging
                        ? D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ
                        : D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE,
                    MiscFlags = 0
                };

                var texture = _device.CreateTexture2D(ref desc);
                var pooled = new PooledStagingTexture(texture, width, height, format, isReadStaging)
                {
                    IsInUse = true
                };
                _pool.Add(pooled);
                return pooled;
            }
        }

        /// <summary>
        /// Returns a staging texture to the pool.
        /// </summary>
        public void Release(PooledStagingTexture stagingTexture)
        {
            if (stagingTexture == null) return;

            lock (_lock)
            {
                stagingTexture.IsInUse = false;
            }
        }

        /// <summary>
        /// Acquires a leased read staging texture that automatically returns to the pool upon disposal.
        /// </summary>
        public StagingLease LeaseRead(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            var tex = AcquireReadStaging(width, height, format);
            return new StagingLease(this, tex);
        }

        /// <summary>
        /// Acquires a leased write staging texture that automatically returns to the pool upon disposal.
        /// </summary>
        public StagingLease LeaseWrite(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            var tex = AcquireWriteStaging(width, height, format);
            return new StagingLease(this, tex);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                for (int i = 0; i < _pool.Count; i++)
                {
                    _pool[i].Dispose();
                }
                _pool.Clear();
            }
        }

        /// <summary>
        /// Disposable lease struct for RAII pattern.
        /// </summary>
        public readonly struct StagingLease : IDisposable
        {
            private readonly GpuStagingPool _pool;
            public PooledStagingTexture Texture { get; }

            public StagingLease(GpuStagingPool pool, PooledStagingTexture texture)
            {
                _pool = pool;
                Texture = texture;
            }

            public void Dispose()
            {
                _pool?.Release(Texture);
            }
        }
    }
}
