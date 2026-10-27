using System;
using System.Threading;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// High-precision Direct3D 11 GPU Timestamp Query Timer.
    /// Accurately measures GPU kernel execution and pipeline dispatch latency without CPU timer distortion.
    /// </summary>
    public sealed class GpuTimer : IDisposable
    {
        private readonly D3D11Query _disjointQuery;
        private readonly D3D11Query _startQuery;
        private readonly D3D11Query _stopQuery;
        private bool _disposed;
        private bool _inProgress;

        public GpuTimer(D3D11Device device)
        {
            if (device == null || !device.IsValid) throw new ArgumentNullException(nameof(device));

            _disjointQuery = device.CreateQuery(D3D11_QUERY.D3D11_QUERY_TIMESTAMP_DISJOINT);
            _startQuery = device.CreateQuery(D3D11_QUERY.D3D11_QUERY_TIMESTAMP);
            _stopQuery = device.CreateQuery(D3D11_QUERY.D3D11_QUERY_TIMESTAMP);
        }

        /// <summary>
        /// Begins timing GPU execution.
        /// </summary>
        public void Start(D3D11DeviceContext context)
        {
            if (context == null || !context.IsValid) throw new ArgumentNullException(nameof(context));
            if (_disposed) throw new ObjectDisposedException(nameof(GpuTimer));

            context.Begin(_disjointQuery);
            context.End(_startQuery);
            _inProgress = true;
        }

        /// <summary>
        /// Stops timing GPU execution.
        /// </summary>
        public void Stop(D3D11DeviceContext context)
        {
            if (context == null || !context.IsValid) throw new ArgumentNullException(nameof(context));
            if (_disposed) throw new ObjectDisposedException(nameof(GpuTimer));
            if (!_inProgress) throw new InvalidOperationException("GpuTimer was not started.");

            context.End(_stopQuery);
            context.End(_disjointQuery);
            _inProgress = false;
        }

        /// <summary>
        /// Retrieves the elapsed time in microseconds on the GPU.
        /// Returns 0 if the query is disjoint or data is not yet ready and non-blocking polling was requested.
        /// </summary>
        public double GetElapsedMicroseconds(D3D11DeviceContext context, bool waitForGpu = true)
        {
            if (context == null || !context.IsValid) throw new ArgumentNullException(nameof(context));
            if (_disposed) throw new ObjectDisposedException(nameof(GpuTimer));

            uint flags = waitForGpu ? 0 : D3D11QueryFlags.D3D11_ASYNC_GETDATA_DONOTFLUSH;

            // Wait/poll for disjoint query
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT disjoint;
            while (true)
            {
                int hr = context.GetData(_disjointQuery, out disjoint, flags);
                if (hr == D3D11QueryFlags.S_OK) break;
                if (!waitForGpu) return 0.0;
                Thread.Yield();
            }

            if (disjoint.Disjoint != 0 || disjoint.Frequency == 0)
            {
                return 0.0; // Clock changed or invalid reading
            }

            ulong startTs = 0;
            while (true)
            {
                int hr = context.GetData(_startQuery, out startTs, flags);
                if (hr == D3D11QueryFlags.S_OK) break;
                if (!waitForGpu) return 0.0;
                Thread.Yield();
            }

            ulong stopTs = 0;
            while (true)
            {
                int hr = context.GetData(_stopQuery, out stopTs, flags);
                if (hr == D3D11QueryFlags.S_OK) break;
                if (!waitForGpu) return 0.0;
                Thread.Yield();
            }

            if (stopTs < startTs) return 0.0;

            ulong delta = stopTs - startTs;
            return (double)delta * 1_000_000.0 / disjoint.Frequency;
        }

        /// <summary>
        /// Retrieves the elapsed time in milliseconds on the GPU.
        /// </summary>
        public double GetElapsedMilliseconds(D3D11DeviceContext context, bool waitForGpu = true)
        {
            return GetElapsedMicroseconds(context, waitForGpu) / 1000.0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopQuery.Dispose();
            _startQuery.Dispose();
            _disjointQuery.Dispose();
        }
    }
}
