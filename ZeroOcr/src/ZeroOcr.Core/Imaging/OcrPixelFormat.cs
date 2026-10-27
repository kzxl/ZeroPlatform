namespace ZeroOcr.Core.Imaging;

/// <summary>
/// Specifies the pixel format of an uncompressed image buffer.
/// </summary>
public enum OcrPixelFormat
{
    /// <summary>
    /// 8 bits per pixel, single channel grayscale.
    /// </summary>
    Gray8 = 1,

    /// <summary>
    /// 24 bits per pixel, Red, Green, Blue order.
    /// </summary>
    Rgb24 = 2,

    /// <summary>
    /// 24 bits per pixel, Blue, Green, Red order.
    /// </summary>
    Bgr24 = 3,

    /// <summary>
    /// 32 bits per pixel, Red, Green, Blue, Alpha.
    /// </summary>
    Rgba32 = 4,

    /// <summary>
    /// 32 bits per pixel, Blue, Green, Red, Alpha (Windows standard).
    /// </summary>
    Bgra32 = 5
}
