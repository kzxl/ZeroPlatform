using System;
using System.Collections.Generic;

namespace ZeroCompute.Core.DirectX
{
    /// <summary>
    /// Key identifying a reusable Direct3D 11 StructuredBuffer signature in the pool.
    /// </summary>
    public readonly struct BufferPoolKey : IEquatable<BufferPoolKey>
    {
        public readonly int ElementCount;
        public readonly int ElementStride;
        public readonly bool AllowUav;
        public readonly bool AllowSrv;
        public readonly bool AllowCpuRead;

        public BufferPoolKey(int elementCount, int elementStride, bool allowUav, bool allowSrv, bool allowCpuRead)
        {
            ElementCount = elementCount;
            ElementStride = elementStride;
            AllowUav = allowUav;
            AllowSrv = allowSrv;
            AllowCpuRead = allowCpuRead;
        }

        public bool Equals(BufferPoolKey other) =>
            ElementCount == other.ElementCount &&
            ElementStride == other.ElementStride &&
            AllowUav == other.AllowUav &&
            AllowSrv == other.AllowSrv &&
            AllowCpuRead == other.AllowCpuRead;

        public override bool Equals(object? obj) => obj is BufferPoolKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ElementCount;
                hash = (hash * 397) ^ ElementStride;
                hash = (hash * 397) ^ (AllowUav ? 1 : 0);
                hash = (hash * 397) ^ (AllowSrv ? 1 : 0);
                hash = (hash * 397) ^ (AllowCpuRead ? 1 : 0);
                return hash;
            }
        }

        public override string ToString() =>
            $"Key(Count={ElementCount}, Stride={ElementStride}, UAV={AllowUav}, SRV={AllowSrv}, CpuRead={AllowCpuRead})";
    }

    /// <summary>
    /// Thread-safe VRAM Slab &amp; Buffer recycling pool for Direct3D 11 Structured Buffers.
    /// Eliminates OS display driver allocation bottlenecks during iterative forward passes and kernel dispatches.
    /// </summary>
    public sealed class D3D11BufferPool : IDisposable
    {
        private readonly IntPtr _device;
        private readonly IntPtr _context;
        private readonly object _syncLock;
        private readonly int _maxBuffersPerBucket;
        private readonly long _maxTotalPoolBytes;

        private readonly object _poolLock = new object();
        private readonly Dictionary<BufferPoolKey, Stack<D3D11ComputeBuffer>> _buckets = new Dictionary<BufferPoolKey, Stack<D3D11ComputeBuffer>>();

        private long _totalRentedCount;
        private long _cacheHitCount;
        private long _cacheMissCount;
        private int _activeRentedCount;
        private long _currentCachedBytes;
        private bool _disposed;

        public int MaxBuffersPerBucket => _maxBuffersPerBucket;
        public long MaxTotalPoolBytes => _maxTotalPoolBytes;
        public long TotalRentedCount => _totalRentedCount;
        public long CacheHitCount => _cacheHitCount;
        public long CacheMissCount => _cacheMissCount;
        public int ActiveRentedCount => _activeRentedCount;
        public long CurrentCachedBytes => _currentCachedBytes;
        public double CacheHitRatio => _totalRentedCount == 0 ? 0.0 : (double)_cacheHitCount / _totalRentedCount;

        public int CachedBufferCount
        {
            get
            {
                lock (_poolLock)
                {
                    int total = 0;
                    foreach (var stack in _buckets.Values) total += stack.Count;
                    return total;
                }
            }
        }

        public D3D11BufferPool(IntPtr device, IntPtr context, object syncLock, int maxBuffersPerBucket = 16, long maxTotalPoolBytes = 512 * 1024 * 1024)
        {
            _device = device;
            _context = context;
            _syncLock = syncLock ?? new object();
            _maxBuffersPerBucket = maxBuffersPerBucket;
            _maxTotalPoolBytes = maxTotalPoolBytes;
        }

        /// <summary>
        /// Rents a structured buffer matching the requested count, stride and flags from the pool,
        /// or allocates a new one if no matching buffer is currently idle in the pool.
        /// </summary>
        public D3D11ComputeBuffer Rent(int elementCount, int elementStride, bool allowUav = true, bool allowSrv = true, bool allowCpuRead = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(D3D11BufferPool));
            if (elementCount <= 0) throw new ArgumentOutOfRangeException(nameof(elementCount));
            if (elementStride <= 0) throw new ArgumentOutOfRangeException(nameof(elementStride));

            var key = new BufferPoolKey(elementCount, elementStride, allowUav, allowSrv, allowCpuRead);

            lock (_poolLock)
            {
                _totalRentedCount++;
                if (_buckets.TryGetValue(key, out var stack) && stack.Count > 0)
                {
                    _cacheHitCount++;
                    _activeRentedCount++;
                    var buffer = stack.Pop();
                    _currentCachedBytes -= buffer.TotalByteWidth;
                    return buffer;
                }

                _cacheMissCount++;
                _activeRentedCount++;
            }

            // Create new buffer outside pool lock to prevent lock contention
            return new D3D11ComputeBuffer(_device, _context, elementCount, elementStride, allowUav, allowSrv, allowCpuRead, _syncLock);
        }

        /// <summary>
        /// Returns a rented structured buffer to the pool for subsequent reuse.
        /// If the pool exceeds its capacity limits, the buffer is disposed directly.
        /// </summary>
        public void Return(D3D11ComputeBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed) return;
            if (_disposed)
            {
                buffer.Dispose();
                return;
            }

            var key = new BufferPoolKey(
                buffer.ElementCount,
                buffer.ElementStride,
                allowUav: buffer.HasUav,
                allowSrv: buffer.HasSrv,
                allowCpuRead: buffer.HasStaging
            );

            lock (_poolLock)
            {
                if (_activeRentedCount > 0) _activeRentedCount--;

                // If adding this buffer exceeds max total bytes, dispose it immediately
                if (_currentCachedBytes + buffer.TotalByteWidth > _maxTotalPoolBytes)
                {
                    buffer.Dispose();
                    return;
                }

                if (!_buckets.TryGetValue(key, out var stack))
                {
                    stack = new Stack<D3D11ComputeBuffer>();
                    _buckets[key] = stack;
                }

                if (stack.Count < _maxBuffersPerBucket)
                {
                    stack.Push(buffer);
                    _currentCachedBytes += buffer.TotalByteWidth;
                    return;
                }
            }

            // Bucket full: release buffer to GPU
            buffer.Dispose();
        }

        /// <summary>
        /// Trims idle buffers across all buckets down to a maximum retention limit.
        /// </summary>
        public void Trim(int maxRetainedPerBucket = 2)
        {
            lock (_poolLock)
            {
                foreach (var stack in _buckets.Values)
                {
                    while (stack.Count > maxRetainedPerBucket)
                    {
                        var buf = stack.Pop();
                        _currentCachedBytes -= buf.TotalByteWidth;
                        buf.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes and clears all pooled GPU buffers.
        /// </summary>
        public void Clear()
        {
            lock (_poolLock)
            {
                foreach (var stack in _buckets.Values)
                {
                    while (stack.Count > 0)
                    {
                        var buf = stack.Pop();
                        buf.Dispose();
                    }
                }
                _buckets.Clear();
                _currentCachedBytes = 0;
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
