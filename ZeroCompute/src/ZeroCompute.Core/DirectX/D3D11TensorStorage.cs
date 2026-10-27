using System;
using System.Buffers;
using ZeroTensor.Core.Storage;

namespace ZeroCompute.Core.DirectX
{
    /// <summary>
    /// Direct3D 11 GPU hardware memory storage for tensors.
    /// Implements <see cref="ITensorStorage{T}"/> and <see cref="IDeviceStorageTransfer{T}"/> to enable
    /// persistent VRAM tensor residency without CPU roundtrips across chained compute dispatches.
    /// </summary>
    /// <typeparam name="T">Unmanaged scalar type.</typeparam>
    public sealed class D3D11TensorStorage<T> : ITensorStorage<T>, IDeviceStorageTransfer<T> where T : unmanaged, IEquatable<T>
    {
        private readonly D3D11ComputeBuffer _buffer;
        private readonly bool _ownsBuffer;
        private readonly D3D11BufferPool? _originPool;
        private bool _disposed;

        public DeviceType Device => DeviceType.Direct3D11;
        public int Length => _buffer.ElementCount;
        public bool IsCpuAccessible => false;
        public bool IsDisposed => _disposed;
        public unsafe T* UnsafePointer => null;

        /// <summary>
        /// Gets the underlying Direct3D 11 StructuredBuffer wrapper.
        /// </summary>
        public D3D11ComputeBuffer Buffer => _buffer;

        /// <summary>
        /// Gets the native GPU buffer COM pointer (ID3D11Buffer*).
        /// </summary>
        public IntPtr DeviceHandle => _buffer.BufferHandle;

        public D3D11TensorStorage(D3D11ComputeBuffer buffer, bool ownsBuffer = true, D3D11BufferPool? originPool = null)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _ownsBuffer = ownsBuffer;
            _originPool = originPool;
        }

        public Span<T> AsSpan(int offset, int length)
        {
            throw new NotSupportedException("Cannot directly obtain a CPU Span from Direct3D 11 GPU VRAM storage. Call tensor.ToCpu() first.");
        }

        public ReadOnlySpan<T> AsReadOnlySpan(int offset, int length)
        {
            throw new NotSupportedException("Cannot directly obtain a CPU ReadOnlySpan from Direct3D 11 GPU VRAM storage. Call tensor.ToCpu() first.");
        }

        public ref T GetPinnableReference(int offset)
        {
            throw new NotSupportedException("Cannot pin GPU VRAM memory directly from CPU host code.");
        }

        public T[]? TryGetArray(out int arrayOffset)
        {
            arrayOffset = 0;
            return null;
        }

        public void CopyToHost(int offset, int length, Span<T> destination)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(D3D11TensorStorage<T>));
            if (offset < 0 || length < 0 || offset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (destination.Length < length)
                throw new ArgumentException("Destination span is shorter than the requested length.", nameof(destination));
            if (length == 0)
                return;

            if (offset == 0)
            {
                _buffer.Download(destination.Slice(0, length));
            }
            else
            {
                int requiredCount = offset + length;
                if (requiredCount <= 128)
                {
                    Span<T> temp = stackalloc T[requiredCount];
                    _buffer.Download(temp);
                    temp.Slice(offset, length).CopyTo(destination);
                }
                else
                {
                    T[] rented = ArrayPool<T>.Shared.Rent(requiredCount);
                    try
                    {
                        Span<T> temp = rented.AsSpan(0, requiredCount);
                        _buffer.Download(temp);
                        temp.Slice(offset, length).CopyTo(destination);
                    }
                    finally
                    {
                        ArrayPool<T>.Shared.Return(rented);
                    }
                }
            }
        }

        public void CopyFromHost(int offset, int length, ReadOnlySpan<T> source)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(D3D11TensorStorage<T>));
            if (offset < 0 || length < 0 || offset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (source.Length < length)
                throw new ArgumentException("Source span is shorter than the requested length.", nameof(source));
            if (length == 0)
                return;

            if (offset == 0 && length == Length)
            {
                _buffer.Upload(source.Slice(0, length));
            }
            else if (Length <= 128)
            {
                Span<T> full = stackalloc T[Length];
                _buffer.Download(full);
                source.Slice(0, length).CopyTo(full.Slice(offset, length));
                _buffer.Upload((ReadOnlySpan<T>)full);
            }
            else
            {
                // Read-modify-write if partial upload
                T[] rented = ArrayPool<T>.Shared.Rent(Length);
                try
                {
                    Span<T> full = rented.AsSpan(0, Length);
                    _buffer.Download(full);
                    source.Slice(0, length).CopyTo(full.Slice(offset, length));
                    _buffer.Upload((ReadOnlySpan<T>)full);
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(rented);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_originPool != null)
                {
                    _originPool.Return(_buffer);
                }
                else if (_ownsBuffer)
                {
                    _buffer.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
