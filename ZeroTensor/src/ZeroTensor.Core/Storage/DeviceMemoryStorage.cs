using System;
using System.Threading;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Delegate for copying data from device buffer to host CPU destination span.
    /// </summary>
    public delegate void DeviceCopyToHostCallback<T>(int offset, int length, Span<T> destination);

    /// <summary>
    /// Delegate for copying data from host CPU source span to device buffer.
    /// </summary>
    public delegate void DeviceCopyFromHostCallback<T>(int offset, int length, ReadOnlySpan<T> source);

    /// <summary>
    /// Generic device memory storage representing hardware accelerator memory (GPU, VRAM, NPU).
    /// Used by backends like ZeroCompute (Direct3D 11, Vulkan) and CUDA without tight coupling.
    /// </summary>
    public class DeviceMemoryStorage<T> : ITensorStorage<T>, IDeviceStorageTransfer<T> where T : unmanaged, IEquatable<T>
    {
        private readonly DeviceType _device;
        private readonly int _length;
        private readonly IntPtr _deviceHandle;
        private readonly DeviceCopyToHostCallback<T>? _copyToHost;
        private readonly DeviceCopyFromHostCallback<T>? _copyFromHost;
        private readonly Action? _onDispose;
        private int _disposed;

        public DeviceType Device => _device;
        public int Length => _length;
        public bool IsCpuAccessible => false;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public unsafe T* UnsafePointer => null;

        /// <summary>
        /// Gets the underlying native device resource handle (e.g. ID3D11Buffer*, VkBuffer, CUdeviceptr).
        /// </summary>
        public IntPtr DeviceHandle => _deviceHandle;

        public DeviceMemoryStorage(
            DeviceType device,
            int length,
            IntPtr deviceHandle,
            DeviceCopyToHostCallback<T>? copyToHost = null,
            DeviceCopyFromHostCallback<T>? copyFromHost = null,
            Action? onDispose = null)
        {
            if (device == DeviceType.Cpu) throw new ArgumentException("Device must not be CPU for DeviceMemoryStorage.", nameof(device));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

            _device = device;
            _length = length;
            _deviceHandle = deviceHandle;
            _copyToHost = copyToHost;
            _copyFromHost = copyFromHost;
            _onDispose = onDispose;
        }

        public Span<T> AsSpan(int offset, int length)
        {
            throw new NotSupportedException($"Cannot directly access Span over device memory ({_device}). Call ToCpu() first.");
        }

        public ReadOnlySpan<T> AsReadOnlySpan(int offset, int length)
        {
            throw new NotSupportedException($"Cannot directly access ReadOnlySpan over device memory ({_device}). Call ToCpu() first.");
        }

        public ref T GetPinnableReference(int offset)
        {
            throw new NotSupportedException($"Cannot directly obtain reference to device memory ({_device}). Call ToCpu() first.");
        }

        public T[]? TryGetArray(out int arrayOffset)
        {
            arrayOffset = 0;
            return null;
        }

        public void CopyToHost(int offset, int length, Span<T> destination)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(DeviceMemoryStorage<T>));
            if (_copyToHost == null)
            {
                throw new NotSupportedException($"Storage for device {_device} does not implement Host copy-back.");
            }

            _copyToHost(offset, length, destination);
        }

        public void CopyFromHost(int offset, int length, ReadOnlySpan<T> source)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(DeviceMemoryStorage<T>));
            if (_copyFromHost == null)
            {
                throw new NotSupportedException($"Storage for device {_device} does not implement Host upload.");
            }

            _copyFromHost(offset, length, source);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDispose?.Invoke();
            }
        }
    }
}
