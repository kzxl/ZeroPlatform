using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Level 11 GPU Tensor Buffer.
    /// Represents an FP32 Structured Buffer in VRAM arranged in planar NCHW layout,
    /// directly consumable by GPU inference engines (DirectML, ONNX Runtime, TensorRT)
    /// without host copies.
    /// </summary>
    public sealed class GpuTensorBuffer : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11Buffer? _gpuBuffer;
        private D3D11UnorderedAccessView? _uav;
        private D3D11Buffer? _stagingBuffer;
        private bool _disposed;

        public int Batch { get; }
        public int Channels { get; }
        public int Height { get; }
        public int Width { get; }
        public int ElementCount => Batch * Channels * Height * Width;
        public int ByteWidth => ElementCount * sizeof(float);

        public D3D11Buffer GpuBuffer => _gpuBuffer ?? throw new ObjectDisposedException(nameof(GpuTensorBuffer));
        public D3D11UnorderedAccessView Uav => _uav ?? throw new ObjectDisposedException(nameof(GpuTensorBuffer));

        public GpuTensorBuffer(D3D11Device device, D3D11DeviceContext context, int channels, int height, int width, int batch = 1)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (batch <= 0) throw new ArgumentOutOfRangeException(nameof(batch));

            Channels = channels;
            Height = height;
            Width = width;
            Batch = batch;

            InitializeGpuResources();
        }

        private unsafe void InitializeGpuResources()
        {
            // 1. Create Structured Buffer with UAV and SRV binding
            var bufferDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)ByteWidth,
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_UNORDERED_ACCESS | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
                CPUAccessFlags = 0,
                MiscFlags = 0x40, // D3D11_RESOURCE_MISC_BUFFER_STRUCTURED
                StructureByteStride = sizeof(float)
            };

            _gpuBuffer = _device.CreateBuffer(ref bufferDesc);

            // 2. Create Buffer Unordered Access View
            var uavDesc = new D3D11_UNORDERED_ACCESS_VIEW_DESC
            {
                Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                ViewDimension = D3D11_UAV_DIMENSION.D3D11_UAV_DIMENSION_BUFFER,
                Buffer = new D3D11_BUFFER_UAV
                {
                    FirstElement = 0,
                    NumElements = (uint)ElementCount,
                    Flags = 0
                }
            };

            IntPtr pUavDesc = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_UNORDERED_ACCESS_VIEW_DESC>());
            try
            {
                Marshal.StructureToPtr(uavDesc, pUavDesc, false);
                _uav = _device.CreateUnorderedAccessView(_gpuBuffer.Handle, pUavDesc);
            }
            finally
            {
                Marshal.FreeHGlobal(pUavDesc);
            }
        }

        /// <summary>
        /// Downloads the planar tensor elements from VRAM into a CPU float array.
        /// </summary>
        public unsafe void DownloadToHost(float[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < ElementCount)
                throw new ArgumentException($"Destination array length ({destination.Length}) is smaller than tensor elements ({ElementCount}).", nameof(destination));

            EnsureStagingBuffer();

            // 1. DMA Copy VRAM Default -> Staging
            ComVTableHelper.CopyResource(_context.Handle, _stagingBuffer!.Handle, GpuBuffer.Handle);

            // 2. Map Staging Buffer for CPU Read
            int hr = ComVTableHelper.Map(_context.Handle, _stagingBuffer.Handle, 0, D3D11_MAP.D3D11_MAP_READ, 0, out var mapped);
            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to map GPU tensor staging buffer. HRESULT=0x{hr:X8}");

            try
            {
                fixed (float* pDst = destination)
                {
                    Buffer.MemoryCopy((void*)mapped.pData, pDst, (long)ByteWidth, (long)ByteWidth);
                }
            }
            finally
            {
                ComVTableHelper.Unmap(_context.Handle, _stagingBuffer.Handle, 0);
            }
        }

        private void EnsureStagingBuffer()
        {
            if (_stagingBuffer != null && _stagingBuffer.IsValid) return;

            var stagingDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)ByteWidth,
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0x40, // STRUCTURED
                StructureByteStride = sizeof(float)
            };

            unsafe
            {
                _stagingBuffer = _device.CreateBuffer(ref stagingDesc);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _uav?.Dispose();
                _gpuBuffer?.Dispose();
                _stagingBuffer?.Dispose();
                _disposed = true;
            }
        }
    }
}
