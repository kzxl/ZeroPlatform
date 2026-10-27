using System;

namespace ZeroVideo.Core
{
    /// <summary>
    /// Supported video pixel formats for frames, decoders, and camera transport buffers.
    /// </summary>
    public enum VideoPixelFormat
    {
        /// <summary>8-bit monochrome grayscale (1 byte per pixel).</summary>
        Gray8,

        /// <summary>24-bit Red-Green-Blue (3 bytes per pixel).</summary>
        Rgb24,

        /// <summary>24-bit Blue-Green-Red standard Windows DIB format (3 bytes per pixel).</summary>
        Bgr24,

        /// <summary>32-bit Red-Green-Blue-Alpha (4 bytes per pixel).</summary>
        Rgba32,

        /// <summary>32-bit Blue-Green-Red-Alpha Direct2D/GDI format (4 bytes per pixel).</summary>
        Bgra32,

        /// <summary>YUV 4:2:0 semi-planar with interleaved UV plane (12 bits per pixel effective).</summary>
        Nv12,

        /// <summary>YUV 4:2:0 planar with separate Y, U, and V planes (12 bits per pixel effective).</summary>
        Yuv420p
    }
}
