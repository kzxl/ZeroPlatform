using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using ZeroLlm.Core.Quantization;

namespace ZeroLlm.Core.Format
{
    /// <summary>
    /// High-performance memory-mapped file reader for GGUF model files.
    /// Provides zero-copy pointers directly into the OS page cache for sub-100ms model boot.
    /// </summary>
    public sealed unsafe class GgufMemoryMappedReader : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private byte* _basePointer;
        private readonly long _fileLength;
        private bool _disposed;

        public GgufReader Header { get; }

        public GgufMemoryMappedReader(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("GGUF model file not found.", filePath);

            var fi = new FileInfo(filePath);
            _fileLength = fi.Length;

            // 1. Read header metadata
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Header = GgufReader.FromStream(fs);
            }

            // 2. Open MemoryMappedFile for zero-copy memory access
            _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
        }

        /// <summary>
        /// Gets a direct zero-copy byte span for the specified tensor.
        /// </summary>
        public ReadOnlySpan<byte> GetTensorBytes(GgufTensorInfo tensor)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            ThrowIfDisposed();

            long absOffset = Header.TensorDataOffset + (long)tensor.Offset;
            long sizeInBytes = (long)tensor.GetSizeInBytes();

            if (absOffset + sizeInBytes > _fileLength)
                throw new InvalidDataException($"Tensor '{tensor.Name}' extends beyond file boundary.");

            return new ReadOnlySpan<byte>(_basePointer + absOffset, (int)sizeInBytes);
        }

        /// <summary>
        /// Gets a typed zero-copy span of float32 weights for an F32 tensor.
        /// </summary>
        public ReadOnlySpan<float> GetTensorF32(GgufTensorInfo tensor)
        {
            if (tensor.Type != GgufTensorType.F32)
                throw new InvalidOperationException($"Tensor '{tensor.Name}' is not F32 (actual: {tensor.Type}).");

            var bytes = GetTensorBytes(tensor);
            fixed (byte* p = bytes)
            {
                return new ReadOnlySpan<float>(p, bytes.Length / sizeof(float));
            }
        }

        /// <summary>
        /// Gets a typed zero-copy span of Q8_0 blocks for a Q8_0 tensor.
        /// </summary>
        public ReadOnlySpan<BlockQ8_0> GetTensorQ8_0(GgufTensorInfo tensor)
        {
            if (tensor.Type != GgufTensorType.Q8_0)
                throw new InvalidOperationException($"Tensor '{tensor.Name}' is not Q8_0 (actual: {tensor.Type}).");

            var bytes = GetTensorBytes(tensor);
            fixed (byte* p = bytes)
            {
                return new ReadOnlySpan<BlockQ8_0>(p, bytes.Length / sizeof(BlockQ8_0));
            }
        }

        /// <summary>
        /// Gets a typed zero-copy span of Q4_0 blocks for a Q4_0 tensor.
        /// </summary>
        public ReadOnlySpan<BlockQ4_0> GetTensorQ4_0(GgufTensorInfo tensor)
        {
            if (tensor.Type != GgufTensorType.Q4_0)
                throw new InvalidOperationException($"Tensor '{tensor.Name}' is not Q4_0 (actual: {tensor.Type}).");

            var bytes = GetTensorBytes(tensor);
            fixed (byte* p = bytes)
            {
                return new ReadOnlySpan<BlockQ4_0>(p, bytes.Length / sizeof(BlockQ4_0));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GgufMemoryMappedReader));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_basePointer != null)
                {
                    _accessor?.SafeMemoryMappedViewHandle.ReleasePointer();
                    _basePointer = null;
                }
                _accessor?.Dispose();
                _mmf?.Dispose();
                _disposed = true;
            }
        }
    }
}
