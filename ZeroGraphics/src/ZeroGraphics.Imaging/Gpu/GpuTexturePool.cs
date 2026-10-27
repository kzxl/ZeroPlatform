using System;
using System.Collections.Generic;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Represents a pooled GPU texture with pre-created RenderTargetView and ShaderResourceView.
    /// Eliminates runtime VRAM allocations during repetitive image processing loops.
    /// </summary>
    public sealed class PooledGpuTexture : IDisposable
    {
        public D3D11Texture2D Texture { get; }
        public D3D11RenderTargetView Rtv { get; }
        public D3D11ShaderResourceView Srv { get; }
        public D3D11UnorderedAccessView? Uav { get; }
        public int Width { get; }
        public int Height { get; }
        public DXGI_FORMAT Format { get; }

        internal bool IsInUse { get; set; }

        public PooledGpuTexture(D3D11Texture2D texture, D3D11RenderTargetView rtv, D3D11ShaderResourceView srv, int width, int height, DXGI_FORMAT format, D3D11UnorderedAccessView? uav = null)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            Rtv = rtv ?? throw new ArgumentNullException(nameof(rtv));
            Srv = srv ?? throw new ArgumentNullException(nameof(srv));
            Uav = uav;
            Width = width;
            Height = height;
            Format = format;
            IsInUse = false;
        }

        public void Dispose()
        {
            Uav?.Dispose();
            Rtv.Dispose();
            Srv.Dispose();
            Texture.Dispose();
        }
    }

    /// <summary>
    /// GPU Texture Lifecycle Manager and transient resource pool.
    /// Manages allocation, reuse, and recycling of intermediate GPU surfaces across Render Graph passes.
    /// </summary>
    public sealed class GpuTexturePool : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly List<PooledGpuTexture> _pool = new List<PooledGpuTexture>();
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

        public GpuTexturePool(D3D11Device device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <summary>
        /// Leases a GPU texture with matching dimensions and format.
        /// Reuses idle textures in the pool; only allocates if no matching idle texture exists.
        /// </summary>
        public PooledGpuTexture Acquire(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, bool needsUav = false)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuTexturePool));

                for (int i = 0; i < _pool.Count; i++)
                {
                    var item = _pool[i];
                    if (!item.IsInUse && item.Width == width && item.Height == height && item.Format == format)
                    {
                        if (!needsUav || item.Uav != null)
                        {
                            item.IsInUse = true;
                            return item;
                        }
                    }
                }

                // Create new pooled texture
                var bindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
                if (needsUav)
                {
                    bindFlags |= D3D11_BIND_FLAG.D3D11_BIND_UNORDERED_ACCESS;
                }

                var desc = new D3D11_TEXTURE2D_DESC
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = format,
                    SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                    Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                    BindFlags = bindFlags,
                    CPUAccessFlags = 0,
                    MiscFlags = 0
                };

                var texture = _device.CreateTexture2D(ref desc);
                var rtv = _device.CreateRenderTargetView(texture.Handle);
                var srv = _device.CreateShaderResourceView(texture.Handle);
                D3D11UnorderedAccessView? uav = null;
                if (needsUav)
                {
                    uav = _device.CreateUnorderedAccessView(texture.Handle);
                }

                var pooled = new PooledGpuTexture(texture, rtv, srv, width, height, format, uav)
                {
                    IsInUse = true
                };

                _pool.Add(pooled);
                return pooled;
            }
        }

        /// <summary>
        /// Returns a leased texture back to the pool for reuse by subsequent passes.
        /// </summary>
        public void Release(PooledGpuTexture texture)
        {
            if (texture == null) return;

            lock (_lock)
            {
                texture.IsInUse = false;
            }
        }

        /// <summary>
        /// Leases a GPU texture with an IDisposable wrapper that automatically returns it to the pool upon disposal.
        /// </summary>
        public GpuTextureLease Lease(int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, bool needsUav = false)
        {
            var tex = Acquire(width, height, format, needsUav);
            return new GpuTextureLease(this, tex);
        }

        public readonly struct GpuTextureLease : IDisposable
        {
            private readonly GpuTexturePool _pool;
            public PooledGpuTexture Texture { get; }

            public GpuTextureLease(GpuTexturePool pool, PooledGpuTexture texture)
            {
                _pool = pool;
                Texture = texture;
            }

            public void Dispose()
            {
                _pool?.Release(Texture);
            }
        }


        /// <summary>
        /// Releases all allocated GPU textures and views.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _pool.Count; i++)
                {
                    _pool[i].Dispose();
                }
                _pool.Clear();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
    }
}
