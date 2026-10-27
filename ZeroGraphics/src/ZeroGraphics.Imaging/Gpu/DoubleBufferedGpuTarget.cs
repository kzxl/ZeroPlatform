using System;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Double-buffered GPU target for realtime video streams and camera capture pipelines.
    /// Eliminates reader-writer contention by ping-ponging front and back GPU texture buffers.
    /// </summary>
    public sealed class DoubleBufferedGpuTarget : IDisposable
    {
        private readonly GpuTexturePool _pool;
        private readonly PooledGpuTexture[] _buffers = new PooledGpuTexture[2];
        private readonly D3D11Query[] _syncQueries = new D3D11Query[2];
        private int _writeIndex = 0;
        private bool _disposed;

        public int Width { get; }
        public int Height { get; }
        public DXGI_FORMAT Format { get; }

        /// <summary>
        /// The buffer currently available for reading/display.
        /// </summary>
        public PooledGpuTexture CurrentRead => _buffers[1 - _writeIndex];

        /// <summary>
        /// The buffer currently targeted for writing/compute dispatch.
        /// </summary>
        public PooledGpuTexture CurrentWrite => _buffers[_writeIndex];

        public DoubleBufferedGpuTarget(D3D11Device device, GpuTexturePool pool, int width, int height, DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, bool needsUav = true)
        {
            if (device == null || !device.IsValid) throw new ArgumentNullException(nameof(device));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));

            Width = width;
            Height = height;
            Format = format;

            _buffers[0] = _pool.Acquire(width, height, format, needsUav);
            _buffers[1] = _pool.Acquire(width, height, format, needsUav);

            _syncQueries[0] = device.CreateQuery(D3D11_QUERY.D3D11_QUERY_EVENT);
            _syncQueries[1] = device.CreateQuery(D3D11_QUERY.D3D11_QUERY_EVENT);
        }

        /// <summary>
        /// Signals completion of current write operations and swaps active buffers.
        /// </summary>
        public void Swap(D3D11DeviceContext context)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DoubleBufferedGpuTarget));

            if (context != null && context.IsValid)
            {
                // Mark end of work on current write buffer
                context.End(_syncQueries[_writeIndex]);
            }

            _writeIndex = 1 - _writeIndex;
        }

        /// <summary>
        /// Waits until the GPU has completely finished rendering into the current read buffer.
        /// </summary>
        public void WaitForReadReady(D3D11DeviceContext context)
        {
            if (context == null || !context.IsValid) return;

            int readIndex = 1 - _writeIndex;
            var query = _syncQueries[readIndex];

            while (context.GetData(query, IntPtr.Zero, 0, D3D11QueryFlags.D3D11_ASYNC_GETDATA_DONOTFLUSH) == D3D11QueryFlags.S_FALSE)
            {
                System.Threading.Thread.Yield();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _syncQueries[0].Dispose();
            _syncQueries[1].Dispose();

            _pool.Release(_buffers[0]);
            _pool.Release(_buffers[1]);
        }
    }
}
