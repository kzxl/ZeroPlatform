using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Imaging;

/// <summary>
/// A high-performance memory buffer wrapper representing raw uncompressed image pixels.
/// Supports both GC-managed pooled buffers (ArrayPool) and unmanaged memory with zero LOH pressure.
/// </summary>
public sealed class OcrImageBuffer : IDisposable
{
    private byte[]? _rentedArray;
    private readonly ArrayPool<byte>? _pool;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public OcrPixelFormat Format { get; }
    public ReadOnlyMemory<byte> Memory { get; }
    public bool IsPooled => _rentedArray != null;

    public int BytesPerPixel => Format switch
    {
        OcrPixelFormat.Gray8 => 1,
        OcrPixelFormat.Rgb24 => 3,
        OcrPixelFormat.Bgr24 => 3,
        OcrPixelFormat.Rgba32 => 4,
        OcrPixelFormat.Bgra32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(Format), Format, null)
    };

    public ReadOnlySpan<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Memory.Span;
        }
    }

    /// <summary>
    /// Constructs an unpooled OcrImageBuffer over an existing memory slice.
    /// </summary>
    public OcrImageBuffer(int width, int height, int stride, OcrPixelFormat format, ReadOnlyMemory<byte> memory)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        int minStride = width * (format switch
        {
            OcrPixelFormat.Gray8 => 1,
            OcrPixelFormat.Rgb24 => 3,
            OcrPixelFormat.Bgr24 => 3,
            _ => 4
        });

        if (stride < minStride)
            throw new ArgumentException($"Stride ({stride}) must be >= min stride ({minStride}) for width {width}.", nameof(stride));

        int minLength = stride * (height - 1) + minStride;
        if (memory.Length < minLength)
            throw new ArgumentException($"Memory length ({memory.Length}) is too small for {width}x{height} with stride {stride}.", nameof(memory));

        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        Memory = memory;
    }

    /// <summary>
    /// Internal constructor for pooled allocations.
    /// </summary>
    private OcrImageBuffer(int width, int height, int stride, OcrPixelFormat format, byte[] rentedArray, ArrayPool<byte> pool)
        : this(width, height, stride, format, new ReadOnlyMemory<byte>(rentedArray, 0, stride * height))
    {
        _rentedArray = rentedArray;
        _pool = pool;
    }

    #region Memory Pooling Factory Methods

    /// <summary>
    /// Rents an OcrImageBuffer from ArrayPool to prevent Gen-2 LOH allocations.
    /// Must be disposed when no longer needed.
    /// </summary>
    public static OcrImageBuffer Rent(int width, int height, OcrPixelFormat format, int? stride = null, ArrayPool<byte>? pool = null)
    {
        pool ??= ArrayPool<byte>.Shared;
        int bpp = format switch
        {
            OcrPixelFormat.Gray8 => 1,
            OcrPixelFormat.Rgb24 => 3,
            OcrPixelFormat.Bgr24 => 3,
            _ => 4
        };
        // 4-byte row alignment
        int effectiveStride = stride ?? (((width * bpp) + 3) & ~3);
        int totalBytes = effectiveStride * height;

        byte[] rented = pool.Rent(totalBytes);
        return new OcrImageBuffer(width, height, effectiveStride, format, rented, pool);
    }

    public static OcrImageBuffer RentGray8(int width, int height) => Rent(width, height, OcrPixelFormat.Gray8);
    public static OcrImageBuffer RentBgra32(int width, int height) => Rent(width, height, OcrPixelFormat.Bgra32);

    #endregion

    #region Standard Factories (Backwards Compatible)

    public static OcrImageBuffer FromBgra32(int width, int height, ReadOnlyMemory<byte> memory, int? stride = null)
    {
        int effectiveStride = stride ?? (width * 4);
        return new OcrImageBuffer(width, height, effectiveStride, OcrPixelFormat.Bgra32, memory);
    }

    public static OcrImageBuffer FromRgba32(int width, int height, ReadOnlyMemory<byte> memory, int? stride = null)
    {
        int effectiveStride = stride ?? (width * 4);
        return new OcrImageBuffer(width, height, effectiveStride, OcrPixelFormat.Rgba32, memory);
    }

    public static OcrImageBuffer FromGray8(int width, int height, ReadOnlyMemory<byte> memory, int? stride = null)
    {
        int effectiveStride = stride ?? width;
        return new OcrImageBuffer(width, height, effectiveStride, OcrPixelFormat.Gray8, memory);
    }

    #endregion

    /// <summary>
    /// Crops this buffer to a sub-rectangle region of interest (ROI).
    /// If <paramref name="destination"/> is provided, writes into it without allocating new memory.
    /// </summary>
    public OcrImageBuffer Crop(OcrRect roi, OcrImageBuffer? destination = null)
    {
        ThrowIfDisposed();

        int x = Math.Max(0, (int)Math.Floor(roi.X));
        int y = Math.Max(0, (int)Math.Floor(roi.Y));
        int w = Math.Min(Width - x, (int)Math.Ceiling(roi.Width));
        int h = Math.Min(Height - y, (int)Math.Ceiling(roi.Height));

        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("Cropped ROI falls outside the image bounds or has zero area.");

        int bpp = BytesPerPixel;
        int croppedStride = w * bpp;

        OcrImageBuffer dstBuffer;
        if (destination != null)
        {
            if (destination.Width < w || destination.Height < h || destination.Format != Format)
                throw new ArgumentException("Provided destination buffer does not match required dimensions or format.", nameof(destination));
            dstBuffer = destination;
        }
        else
        {
            byte[] targetAllocated = new byte[croppedStride * h];
            dstBuffer = new OcrImageBuffer(w, h, croppedStride, Format, targetAllocated);
        }

        var srcSpan = Span;
        unsafe
        {
            fixed (byte* pSrc = srcSpan)
            fixed (byte* pDst = dstBuffer.Span)
            {
                for (int row = 0; row < h; row++)
                {
                    int srcOffset = (y + row) * Stride + (x * bpp);
                    int dstOffset = row * dstBuffer.Stride;
                    Buffer.MemoryCopy(pSrc + srcOffset, pDst + dstOffset, croppedStride, croppedStride);
                }
            }
        }

        return dstBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OcrImageBuffer));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_rentedArray != null && _pool != null)
            {
                _pool.Return(_rentedArray);
                _rentedArray = null;
            }
        }
    }
}
