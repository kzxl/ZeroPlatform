using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ZeroGraphics.Imaging.Core
{
    /// <summary>
    /// Represents a multi-gigabyte or gigapixel image surface mapped directly to/from disk files.
    /// Eliminates physical host RAM saturation by streaming spatial subregions (tiles)
    /// through unmanaged memory-mapped virtual pages.
    /// </summary>
    public sealed unsafe class MemoryMappedImageBuffer : IDisposable
    {
        private readonly MemoryMappedFile _mmFile;
        private readonly MemoryMappedViewAccessor _accessor;
        private byte* _pointer;
        private bool _disposed;

        public int Width { get; }
        public int Height { get; }
        public ImageFormatMode Format { get; }
        public int BytesPerPixel { get; }
        public int Stride { get; }
        public long TotalBytes => (long)Height * Stride;

        public byte* BasePointer => _pointer;

        private MemoryMappedImageBuffer(
            MemoryMappedFile mmFile,
            int width,
            int height,
            ImageFormatMode format)
        {
            _mmFile = mmFile ?? throw new ArgumentNullException(nameof(mmFile));
            Width = width > 0 ? width : throw new ArgumentOutOfRangeException(nameof(width));
            Height = height > 0 ? height : throw new ArgumentOutOfRangeException(nameof(height));
            Format = format;

            BytesPerPixel = format == ImageFormatMode.Bgra32 ? 4 : 1;
            Stride = width * BytesPerPixel;

            _accessor = _mmFile.CreateViewAccessor(0, TotalBytes, MemoryMappedFileAccess.ReadWrite);
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _pointer);
        }

        /// <summary>
        /// Creates a new disk-backed memory-mapped image file.
        /// </summary>
        public static MemoryMappedImageBuffer CreateNew(string filePath, int width, int height, ImageFormatMode format = ImageFormatMode.Bgra32)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            int bpp = format == ImageFormatMode.Bgra32 ? 4 : 1;
            long capacity = (long)width * height * bpp;

            var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            fileStream.SetLength(capacity);

            var mmFile = MemoryMappedFile.CreateFromFile(
                fileStream,
                mapName: null,
                capacity: capacity,
                access: MemoryMappedFileAccess.ReadWrite,
                inheritability: HandleInheritability.None,
                leaveOpen: false);

            return new MemoryMappedImageBuffer(mmFile, width, height, format);
        }

        /// <summary>
        /// Gets a pointer to the start of a specific scanline.
        /// </summary>
        public byte* GetRowPointer(int y)
        {
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _pointer + ((long)y * Stride);
        }

        /// <summary>
        /// Reads a rectangular subregion directly into a host ImageBuffer (zero managed allocations).
        /// </summary>
        public void ReadSubrect(int srcX, int srcY, int subWidth, int subHeight, ImageBuffer destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (srcX < 0 || srcY < 0 || srcX + subWidth > Width || srcY + subHeight > Height)
                throw new ArgumentOutOfRangeException("Subrect falls outside image boundaries.");

            int lineBytes = subWidth * BytesPerPixel;
            for (int y = 0; y < subHeight; y++)
            {
                byte* pSrc = GetRowPointer(srcY + y) + (srcX * BytesPerPixel);
                byte* pDst = destination.GetRowPointer(y);
                Buffer.MemoryCopy(pSrc, pDst, lineBytes, lineBytes);
            }
        }

        /// <summary>
        /// Writes a rectangular subregion from a host ImageBuffer into this disk-backed file.
        /// </summary>
        public void WriteSubrect(int dstX, int dstY, int subWidth, int subHeight, ImageBuffer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (dstX < 0 || dstY < 0 || dstX + subWidth > Width || dstY + subHeight > Height)
                throw new ArgumentOutOfRangeException("Subrect falls outside image boundaries.");

            int lineBytes = subWidth * BytesPerPixel;
            for (int y = 0; y < subHeight; y++)
            {
                byte* pSrc = source.GetRowPointer(y);
                byte* pDst = GetRowPointer(dstY + y) + (dstX * BytesPerPixel);
                Buffer.MemoryCopy(pSrc, pDst, lineBytes, lineBytes);
            }
        }

        public void Flush()
        {
            _accessor.Flush();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_pointer != null)
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _pointer = null;
                }
                _accessor.Dispose();
                _mmFile.Dispose();
                _disposed = true;
            }
        }
    }
}
