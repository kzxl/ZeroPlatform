using System;

namespace ZeroAudioVisual.Video
{
    public enum VideoPixelFormat
    {
        Gray8,
        Rgb24,
        Bgr24,
        Rgba32,
        Nv12
    }

    /// <summary>
    /// High-performance video frame buffer for GigE Vision and industrial RTSP camera streaming.
    /// Supports zero-copy memory wrapping, format metadata, and timestamp tracking.
    /// </summary>
    public class VideoFrameBuffer
    {
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public VideoPixelFormat PixelFormat { get; }
        public long TimestampNs { get; set; }
        public long FrameIndex { get; set; }
        public byte[] Data { get; }

        public VideoFrameBuffer(int width, int height, VideoPixelFormat pixelFormat, int stride = 0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            PixelFormat = pixelFormat;

            int bytesPerPixel = GetBytesPerPixel(pixelFormat);
            int minStride = (pixelFormat == VideoPixelFormat.Nv12) ? width : width * bytesPerPixel;
            Stride = (stride >= minStride) ? stride : minStride;

            int totalBytes = (pixelFormat == VideoPixelFormat.Nv12)
                ? Stride * height * 3 / 2
                : Stride * height;

            Data = new byte[totalBytes];
        }

        public VideoFrameBuffer(int width, int height, VideoPixelFormat pixelFormat, byte[] existingData, int stride = 0)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            int bytesPerPixel = GetBytesPerPixel(pixelFormat);
            int minStride = (pixelFormat == VideoPixelFormat.Nv12) ? width : width * bytesPerPixel;
            Stride = (stride >= minStride) ? stride : minStride;
            Data = existingData ?? throw new ArgumentNullException(nameof(existingData));
        }

        public static int GetBytesPerPixel(VideoPixelFormat format)
        {
            switch (format)
            {
                case VideoPixelFormat.Gray8: return 1;
                case VideoPixelFormat.Rgb24:
                case VideoPixelFormat.Bgr24: return 3;
                case VideoPixelFormat.Rgba32: return 4;
                case VideoPixelFormat.Nv12: return 1; // 12 bits per pixel effective
                default: return 1;
            }
        }

        /// <summary>
        /// Creates a deep copy of this video frame.
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
        /// Converts a grayscale or RGB pixel at coordinate (x, y) into normalized intensity (0.0 .. 1.0).
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
                    int px = offset + x * 3;
                    return (0.299f * Data[px] + 0.587f * Data[px + 1] + 0.114f * Data[px + 2]) / 255f;
                }

                case VideoPixelFormat.Bgr24:
                {
                    int px = offset + x * 3;
                    return (0.114f * Data[px] + 0.587f * Data[px + 1] + 0.299f * Data[px + 2]) / 255f;
                }

                case VideoPixelFormat.Rgba32:
                {
                    int px = offset + x * 4;
                    return (0.299f * Data[px] + 0.587f * Data[px + 1] + 0.114f * Data[px + 2]) / 255f;
                }

                default:
                    return Data[offset + x] / 255f;
            }
        }
    }
}
