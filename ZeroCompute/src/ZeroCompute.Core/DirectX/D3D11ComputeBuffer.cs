using System;
using System.Runtime.InteropServices;

namespace ZeroCompute.Core.DirectX
{
    /// <summary>
    /// Represents a Direct3D 11 GPU Structured Buffer with optional UAV, SRV, and CPU staging capabilities.
    /// </summary>
    public sealed class D3D11ComputeBuffer : IDisposable
    {
        private IntPtr _bufferHandle;
        private IntPtr _uavHandle;
        private IntPtr _srvHandle;
        private IntPtr _stagingBufferHandle;
        private readonly IntPtr _device;
        private readonly IntPtr _context;
        private readonly object _syncLock;
        private bool _disposed;

        public int ElementCount { get; }
        public int ElementStride { get; }
        public uint TotalByteWidth => (uint)(ElementCount * ElementStride);

        public IntPtr BufferHandle => _bufferHandle;
        public IntPtr UavHandle => _uavHandle;
        public IntPtr SrvHandle => _srvHandle;
        public bool HasUav => _uavHandle != IntPtr.Zero;
        public bool HasSrv => _srvHandle != IntPtr.Zero;
        public bool HasStaging => _stagingBufferHandle != IntPtr.Zero;
        public bool IsDisposed => _disposed;

        internal D3D11ComputeBuffer(IntPtr device, IntPtr context, int elementCount, int elementStride, bool createUav, bool createSrv, bool allowCpuRead, object? syncLock = null)
        {
            _device = device;
            _context = context;
            _syncLock = syncLock ?? new object();
            ElementCount = elementCount;
            ElementStride = elementStride;

            uint byteWidth = (uint)(elementCount * elementStride);
            if (byteWidth == 0) byteWidth = (uint)elementStride;

            // 1. Create Default Structured Buffer
            uint bindFlags = 0;
            if (createUav) bindFlags |= D3D11Native.D3D11_BIND_UNORDERED_ACCESS;
            if (createSrv) bindFlags |= D3D11Native.D3D11_BIND_SHADER_RESOURCE;

            var desc = new D3D11Native.D3D11_BUFFER_DESC
            {
                ByteWidth = byteWidth,
                Usage = D3D11Native.D3D11_USAGE_DEFAULT,
                BindFlags = bindFlags,
                CPUAccessFlags = 0,
                MiscFlags = D3D11Native.D3D11_RESOURCE_MISC_BUFFER_STRUCTURED,
                StructureByteStride = (uint)elementStride
            };

            int hr = D3D11Native.CreateBuffer(_device, ref desc, IntPtr.Zero, out _bufferHandle);
            if (hr < 0 || _bufferHandle == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D11 Structured Buffer (HRESULT 0x{hr:X8}).");

            // 2. Create UAV if requested
            if (createUav)
            {
                var uavDesc = new D3D11Native.D3D11_UNORDERED_ACCESS_VIEW_DESC
                {
                    Format = 0, // DXGI_FORMAT_UNKNOWN
                    ViewDimension = D3D11Native.D3D11_UAV_DIMENSION_BUFFER,
                    Buffer = new D3D11Native.D3D11_BUFFER_UAV
                    {
                        FirstElement = 0,
                        NumElements = (uint)elementCount,
                        Flags = 0
                    }
                };
                hr = D3D11Native.CreateUnorderedAccessView(_device, _bufferHandle, ref uavDesc, out _uavHandle);
                if (hr < 0) throw new InvalidOperationException($"Failed to create D3D11 UAV (HRESULT 0x{hr:X8}).");
            }

            // 3. Create SRV if requested
            if (createSrv)
            {
                var srvDesc = new D3D11Native.D3D11_SHADER_RESOURCE_VIEW_DESC
                {
                    Format = 0,
                    ViewDimension = D3D11Native.D3D11_SRV_DIMENSION_BUFFER,
                    Buffer = new D3D11Native.D3D11_BUFFER_SRV
                    {
                        FirstElement = 0,
                        NumElements = (uint)elementCount
                    }
                };
                hr = D3D11Native.CreateShaderResourceView(_device, _bufferHandle, ref srvDesc, out _srvHandle);
                if (hr < 0) throw new InvalidOperationException($"Failed to create D3D11 SRV (HRESULT 0x{hr:X8}).");
            }

            // 4. Create CPU Staging Buffer if readback needed
            if (allowCpuRead)
            {
                var stagingDesc = new D3D11Native.D3D11_BUFFER_DESC
                {
                    ByteWidth = byteWidth,
                    Usage = D3D11Native.D3D11_USAGE_STAGING,
                    BindFlags = 0,
                    CPUAccessFlags = D3D11Native.D3D11_CPU_ACCESS_READ,
                    MiscFlags = 0,
                    StructureByteStride = (uint)elementStride
                };
                hr = D3D11Native.CreateBuffer(_device, ref stagingDesc, IntPtr.Zero, out _stagingBufferHandle);
                if (hr < 0) throw new InvalidOperationException($"Failed to create D3D11 Staging Buffer (HRESULT 0x{hr:X8}).");
            }
        }

        public unsafe void Upload<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (data.Length > ElementCount)
                throw new ArgumentException($"Data length {data.Length} exceeds buffer capacity {ElementCount}.");

            lock (_syncLock)
            {
                fixed (T* ptr = data)
                {
                    D3D11Native.UpdateSubresource(_context, _bufferHandle, (IntPtr)ptr);
                }
            }
        }

        public unsafe void Upload<T>(T[] data) where T : unmanaged
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Upload((ReadOnlySpan<T>)data.AsSpan());
        }

        public unsafe void UploadBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length > TotalByteWidth)
                throw new ArgumentException($"Data length {data.Length} bytes exceeds buffer capacity {TotalByteWidth} bytes.");

            lock (_syncLock)
            {
                fixed (byte* ptr = data)
                {
                    D3D11Native.UpdateSubresource(_context, _bufferHandle, (IntPtr)ptr);
                }
            }
        }

        public unsafe void UploadBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            UploadBytes(data.AsSpan());
        }

        public unsafe void Download<T>(Span<T> destination) where T : unmanaged
        {
            if (_stagingBufferHandle == IntPtr.Zero)
                throw new InvalidOperationException("Buffer was not created with allowCpuRead: true.");

            lock (_syncLock)
            {
                // Copy GPU Default buffer -> GPU Staging buffer
                D3D11Native.CopyResource(_context, _stagingBufferHandle, _bufferHandle);

                // Map staging buffer
                int hr = D3D11Native.Map(_context, _stagingBufferHandle, 0, D3D11Native.D3D11_MAP_READ, 0, out var mapped);
                if (hr < 0) throw new InvalidOperationException($"Failed to map staging buffer (HRESULT 0x{hr:X8}).");

                try
                {
                    int copyCount = Math.Min(destination.Length, ElementCount);
                    int byteCount = copyCount * ElementStride;
                    fixed (T* pDst = destination)
                    {
                        Buffer.MemoryCopy((void*)mapped.pData, pDst, byteCount, byteCount);
                    }
                }
                finally
                {
                    D3D11Native.Unmap(_context, _stagingBufferHandle, 0);
                }
            }
        }

        public unsafe void Download<T>(T[] destination) where T : unmanaged
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            Download(destination.AsSpan());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_uavHandle != IntPtr.Zero) { D3D11Native.Release(_uavHandle); _uavHandle = IntPtr.Zero; }
                if (_srvHandle != IntPtr.Zero) { D3D11Native.Release(_srvHandle); _srvHandle = IntPtr.Zero; }
                if (_stagingBufferHandle != IntPtr.Zero) { D3D11Native.Release(_stagingBufferHandle); _stagingBufferHandle = IntPtr.Zero; }
                if (_bufferHandle != IntPtr.Zero) { D3D11Native.Release(_bufferHandle); _bufferHandle = IntPtr.Zero; }
                _disposed = true;
            }
        }
    }
}
