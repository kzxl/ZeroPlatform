using System;

namespace ZeroVideo.Core
{
    /// <summary>
    /// High-performance, low-latency video frame buffer for GigE Vision, USB3 Vision, IP RTSP streams, and media playback.
    /// Supports zero-copy memory wrapping, format metadata, strided alignment, and sub-nanosecond timestamping.
    /// </summary>
    public class VideoFrameBuffer : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public VideoPixelFormat PixelFormat { get; }

        /// <summary>Hardware capture or presentation timestamp in nanoseconds.</summary>
        public long TimestampNs { get; set; }

        /// <summary>Presentation timestamp (PTS) expressed as a TimeSpan.</summary>
        public TimeSpan PresentationTime
        {
            get => TimeSpan.FromTicks(TimestampNs / 100);
            set => TimestampNs = value.Ticks * 100;
        }

        /// <summary>Sequential frame index counter.</summary>
        public long FrameIndex { get; set; }

        /// <summary>Raw pixel data byte buffer.</summary>
        public byte[] Data { get; }

        /// <summary>Whether this frame buffer has been disposed or returned to pool.</summary>
        public virtual bool IsDisposed => false;

        public VideoFrameBuffer(int width, int height, VideoPixelFormat pixelFormat, int stride = 0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

            Width = width;
            Height = height;
            PixelFormat = pixelFormat;

            int bytesPerPixel = GetBytesPerPixel(pixelFormat);
            int minStride = (pixelFormat == VideoPixelFormat.Nv12 || pixelFormat == VideoPixelFormat.Yuv420p)
                ? width
                : width * bytesPerPixel;

            Stride = (stride >= minStride) ? stride : minStride;
            int totalBytes = CalculateTotalBytes(width, height, pixelFormat, Stride);
            Data = new byte[totalBytes];
        }

        public VideoFrameBuffer(int width, int height, VideoPixelFormat pixelFormat, byte[] existingData, int stride = 0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            PixelFormat = pixelFormat;

            int bytesPerPixel = GetBytesPerPixel(pixelFormat);
            int minStride = (pixelFormat == VideoPixelFormat.Nv12 || pixelFormat == VideoPixelFormat.Yuv420p)
                ? width
                : width * bytesPerPixel;

            Stride = (stride >= minStride) ? stride : minStride;
            Data = existingData ?? throw new ArgumentNullException(nameof(existingData));
        }

        /// <summary>
        /// Calculates the required total byte size for a frame buffer given its geometry and format.
        /// </summary>
        public static int CalculateTotalBytes(int width, int height, VideoPixelFormat pixelFormat, int stride = 0)
        {
            int bytesPerPixel = GetBytesPerPixel(pixelFormat);
            int minStride = (pixelFormat == VideoPixelFormat.Nv12 || pixelFormat == VideoPixelFormat.Yuv420p)
                ? width
                : width * bytesPerPixel;

            int actualStride = (stride >= minStride) ? stride : minStride;
            if (pixelFormat == VideoPixelFormat.Nv12)
            {
                int uvHeight = (height + 1) / 2;
                return actualStride * height + actualStride * uvHeight;
            }
            if (pixelFormat == VideoPixelFormat.Yuv420p)
            {
                int uvHeight = (height + 1) / 2;
                int uvStride = (actualStride + 1) / 2;
                return actualStride * height + (uvStride * uvHeight * 2);
            }
            return actualStride * height;
        }

        public static int GetBytesPerPixel(VideoPixelFormat format)
        {
            switch (format)
            {
                case VideoPixelFormat.Gray8: return 1;
                case VideoPixelFormat.Rgb24:
                case VideoPixelFormat.Bgr24: return 3;
                case VideoPixelFormat.Rgba32:
                case VideoPixelFormat.Bgra32: return 4;
                case VideoPixelFormat.Nv12:
                case VideoPixelFormat.Yuv420p: return 1; // 1.5 effective bytes per pixel
                default: return 1;
            }
        }

        /// <summary>
        /// Returns a span over the active pixel data buffer.
        /// </summary>
        public virtual Span<byte> AsSpan() => new Span<byte>(Data);

        /// <summary>
        /// Returns a read-only span over the active pixel data buffer.
        /// </summary>
        public virtual ReadOnlySpan<byte> AsReadOnlySpan() => new ReadOnlySpan<byte>(Data);

        /// <summary>
        /// Returns a span over a specific row of pixel data.
        /// </summary>
        public virtual Span<byte> GetRowSpan(int y)
        {
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            int rowBytes = (PixelFormat == VideoPixelFormat.Nv12 || PixelFormat == VideoPixelFormat.Yuv420p)
                ? Width
                : Width * GetBytesPerPixel(PixelFormat);
            return new Span<byte>(Data, y * Stride, rowBytes);
        }

        /// <summary>
        /// Creates a complete deep copy of this frame buffer.
        /// </summary>
        public VideoFrameBuffer Clone()
        {
            var copy = new VideoFrameBuffer(Width, Height, PixelFormat, Stride)
            {
                TimestampNs = TimestampNs,
                FrameIndex = FrameIndex
            };
            Buffer.BlockCopy(Data, 0, copy.Data, 0, Data.Length);
            return copy;
        }

        /// <summary>
        /// Reads normalized grayscale luminance (0.0 to 1.0) at specific coordinate (x, y).
        /// </summary>
        public float GetLuminance(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0f;

            int offset = y * Stride;
            switch (PixelFormat)
            {
                case VideoPixelFormat.Gray8:
                    return Data[offset + x] / 255f;

                case VideoPixelFormat.Rgb24:
                {
                    int px = offset + (x * 3);
                    return (Data[px] * 0.299f + Data[px + 1] * 0.587f + Data[px + 2] * 0.114f) / 255f;
                }

                case VideoPixelFormat.Bgr24:
                {
                    int px = offset + (x * 3);
                    return (Data[px + 2] * 0.299f + Data[px + 1] * 0.587f + Data[px] * 0.114f) / 255f;
                }

                case VideoPixelFormat.Rgba32:
                {
                    int px = offset + (x * 4);
                    return (Data[px] * 0.299f + Data[px + 1] * 0.587f + Data[px + 2] * 0.114f) / 255f;
                }

                case VideoPixelFormat.Bgra32:
                {
                    int px = offset + (x * 4);
                    return (Data[px + 2] * 0.299f + Data[px + 1] * 0.587f + Data[px] * 0.114f) / 255f;
                }

                case VideoPixelFormat.Nv12:
                case VideoPixelFormat.Yuv420p:
                    // Y-plane is standard luminance directly
                    return Data[offset + x] / 255f;

                default:
                    return 0f;
            }
        }

        public virtual void Dispose()
        {
            // Base implementation has nothing unmanaged to dispose
        }
    }
}
