using System;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif
using ZeroGraphics.Core.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// High-speed color transformation and LUT-accelerated adjustment filters.
    /// Supports AVX2/SSE SIMD vectorization and hardware-accelerated processing.
    /// </summary>
    public static unsafe class ColorTransform
    {
        /// <summary>
        /// Converts a Bgra32 image buffer to Gray8 using fixed-point ITU-R BT.709 coefficients (0.2126R + 0.7152G + 0.0722B).
        /// </summary>
        public static void ToGrayscale(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");
            if (dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Destination must be Gray8 format.", nameof(dst));

            int width = src.Width;
            int height = src.Height;

            if (src.Format == ImageFormatMode.Bgra32 &&
                ZeroGraphicsNativeBridge.DispatchMode == GraphicsNativeDispatchMode.ForceNative &&
                ZeroGraphicsNativeBridge.IsAvailable &&
                ZeroGraphicsNativeBridge.TryBgra32ToGray8(src.Scan0, src.Stride, dst.Scan0, dst.Stride, width, height))
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                byte* srcRow = src.GetRowPointer(y);
                byte* dstRow = dst.GetRowPointer(y);

                int x = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && width >= 8)
                {
                    var weight = Vector256.Create((ushort)19, 183, 54, 0, 19, 183, 54, 0, 19, 183, 54, 0, 19, 183, 54, 0);
                    int simdLimit = width - 8;
                    for (; x <= simdLimit; x += 8)
                    {
                        var v = Vector256.Load(srcRow + x * 4);
                        var lo = Vector256.WidenLower(v);
                        var hi = Vector256.WidenUpper(v);

                        var pLo = Vector256.Multiply(lo, weight);
                        var pHi = Vector256.Multiply(hi, weight);

                        ulong packed =
                            ((ulong)(byte)((pLo.GetElement(0) + pLo.GetElement(1) + pLo.GetElement(2)) >> 8)) |
                            ((ulong)(byte)((pLo.GetElement(4) + pLo.GetElement(5) + pLo.GetElement(6)) >> 8) << 8) |
                            ((ulong)(byte)((pLo.GetElement(8) + pLo.GetElement(9) + pLo.GetElement(10)) >> 8) << 16) |
                            ((ulong)(byte)((pLo.GetElement(12) + pLo.GetElement(13) + pLo.GetElement(14)) >> 8) << 24) |
                            ((ulong)(byte)((pHi.GetElement(0) + pHi.GetElement(1) + pHi.GetElement(2)) >> 8) << 32) |
                            ((ulong)(byte)((pHi.GetElement(4) + pHi.GetElement(5) + pHi.GetElement(6)) >> 8) << 40) |
                            ((ulong)(byte)((pHi.GetElement(8) + pHi.GetElement(9) + pHi.GetElement(10)) >> 8) << 48) |
                            ((ulong)(byte)((pHi.GetElement(12) + pHi.GetElement(13) + pHi.GetElement(14)) >> 8) << 56);

                        *(ulong*)(dstRow + x) = packed;
                    }
                }
#endif

                // Unroll 4 pixels at a time for remainder or non-accelerated targets
                int unrollLimit = width - 4;
                for (; x <= unrollLimit; x += 4)
                {
                    int o0 = x * 4;
                    dstRow[x]     = (byte)((54 * srcRow[o0 + 2] + 183 * srcRow[o0 + 1] + 19 * srcRow[o0]) >> 8);
                    int o1 = o0 + 4;
                    dstRow[x + 1] = (byte)((54 * srcRow[o1 + 2] + 183 * srcRow[o1 + 1] + 19 * srcRow[o1]) >> 8);
                    int o2 = o0 + 8;
                    dstRow[x + 2] = (byte)((54 * srcRow[o2 + 2] + 183 * srcRow[o2 + 1] + 19 * srcRow[o2]) >> 8);
                    int o3 = o0 + 12;
                    dstRow[x + 3] = (byte)((54 * srcRow[o3 + 2] + 183 * srcRow[o3 + 1] + 19 * srcRow[o3]) >> 8);
                }

                for (; x < width; x++)
                {
                    int offset = x * 4;
                    byte b = srcRow[offset];
                    byte g = srcRow[offset + 1];
                    byte r = srcRow[offset + 2];
                    dstRow[x] = (byte)((54 * r + 183 * g + 19 * b) >> 8);
                }
            }
        }

        /// <summary>
        /// Inverts color values (255 - value) for either Gray8 or Bgra32 buffers using SIMD acceleration.
        /// </summary>
        public static void Invert(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height || src.Format != dst.Format)
                throw new ArgumentException("Source and destination must have matching dimensions and formats.");

            int width = src.Width;
            int height = src.Height;

            if (src.Format == ImageFormatMode.Gray8)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && width >= Vector256<byte>.Count)
                {
                    var v255 = Vector256.Create((byte)255);
                    int simdLimit = width - Vector256<byte>.Count;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        byte* dstRow = dst.GetRowPointer(y);
                        int x = 0;
                        for (; x <= simdLimit; x += Vector256<byte>.Count)
                        {
                            var vSrc = Vector256.Load(srcRow + x);
                            var vInv = Vector256.Xor(vSrc, v255);
                            vInv.Store(dstRow + x);
                        }
                        for (; x < width; x++)
                        {
                            dstRow[x] = (byte)(255 - srcRow[x]);
                        }
                    }
                    return;
                }
                else if (Vector128.IsHardwareAccelerated && width >= Vector128<byte>.Count)
                {
                    var v255 = Vector128.Create((byte)255);
                    int simdLimit = width - Vector128<byte>.Count;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        byte* dstRow = dst.GetRowPointer(y);
                        int x = 0;
                        for (; x <= simdLimit; x += Vector128<byte>.Count)
                        {
                            var vSrc = Vector128.Load(srcRow + x);
                            var vInv = Vector128.Xor(vSrc, v255);
                            vInv.Store(dstRow + x);
                        }
                        for (; x < width; x++)
                        {
                            dstRow[x] = (byte)(255 - srcRow[x]);
                        }
                    }
                    return;
                }
#endif
                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src.GetRowPointer(y);
                    byte* dstRow = dst.GetRowPointer(y);
                    for (int x = 0; x < width; x++)
                    {
                        dstRow[x] = (byte)(255 - srcRow[x]);
                    }
                }
            }
            else // Bgra32
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && width >= 8)
                {
                    // Invert B, G, R (XOR with 255) and preserve A (XOR with 0) across 8 pixels
                    var mask = Vector256.Create(
                        (byte)255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0,
                        255, 255, 255, 0);
                    int simdLimit = width - 8;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        byte* dstRow = dst.GetRowPointer(y);
                        int x = 0;
                        for (; x <= simdLimit; x += 8)
                        {
                            var vSrc = Vector256.Load(srcRow + x * 4);
                            var vInv = Vector256.Xor(vSrc, mask);
                            vInv.Store(dstRow + x * 4);
                        }
                        for (; x < width; x++)
                        {
                            int offset = x * 4;
                            dstRow[offset]     = (byte)(255 - srcRow[offset]);
                            dstRow[offset + 1] = (byte)(255 - srcRow[offset + 1]);
                            dstRow[offset + 2] = (byte)(255 - srcRow[offset + 2]);
                            dstRow[offset + 3] = srcRow[offset + 3];
                        }
                    }
                    return;
                }
#endif
                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src.GetRowPointer(y);
                    byte* dstRow = dst.GetRowPointer(y);
                    for (int x = 0; x < width; x++)
                    {
                        int offset = x * 4;
                        dstRow[offset]     = (byte)(255 - srcRow[offset]);         // B
                        dstRow[offset + 1] = (byte)(255 - srcRow[offset + 1]);     // G
                        dstRow[offset + 2] = (byte)(255 - srcRow[offset + 2]);     // R
                        dstRow[offset + 3] = srcRow[offset + 3];                   // Preserve Alpha
                    }
                }
            }
        }

        /// <summary>
        /// Adjusts brightness (-1.0 to 1.0) and contrast (0.0 to 3.0) using an ultra-fast precomputed 256-entry Look-Up Table (LUT).
        /// </summary>
        public static void AdjustBrightnessContrast(ImageBuffer src, ImageBuffer dst, float brightness, float contrast)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            // Generate 256-entry LUT
            byte[] lut = new byte[256];
            float bOffset = brightness * 255.0f;

            for (int i = 0; i < 256; i++)
            {
                // Contrast formula: ((i - 128) * contrast) + 128 + bOffset
                float val = ((i - 128.0f) * contrast) + 128.0f + bOffset;
                if (val < 0.0f) val = 0.0f;
                else if (val > 255.0f) val = 255.0f;
                lut[i] = (byte)val;
            }

            ApplyLut(src, dst, lut);
        }

        /// <summary>
        /// Applies an arbitrary 256-byte Look-Up Table to an image buffer.
        /// </summary>
        public static void ApplyLut(ImageBuffer src, ImageBuffer dst, byte[] lut)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (lut == null || lut.Length != 256) throw new ArgumentException("LUT must contain exactly 256 entries.", nameof(lut));
            if (src.Width != dst.Width || src.Height != dst.Height || src.Format != dst.Format)
                throw new ArgumentException("Source and destination must have matching dimensions and formats.");

            int width = src.Width;
            int height = src.Height;

            fixed (byte* pLut = lut)
            {
                if (src.Format == ImageFormatMode.Gray8)
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        byte* dstRow = dst.GetRowPointer(y);
                        for (int x = 0; x < width; x++)
                        {
                            dstRow[x] = pLut[srcRow[x]];
                        }
                    }
                }
                else // Bgra32
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src.GetRowPointer(y);
                        byte* dstRow = dst.GetRowPointer(y);
                        for (int x = 0; x < width; x++)
                        {
                            int offset = x * 4;
                            dstRow[offset] = pLut[srcRow[offset]];         // B
                            dstRow[offset + 1] = pLut[srcRow[offset + 1]]; // G
                            dstRow[offset + 2] = pLut[srcRow[offset + 2]]; // R
                            dstRow[offset + 3] = srcRow[offset + 3];       // A
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs histogram equalization on a Gray8 image buffer to enhance contrast across uneven lighting conditions.
        /// </summary>
        public static void HistogramEqualization(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Histogram equalization requires Gray8 format.");

            int width = src.Width;
            int height = src.Height;
            int totalPixels = width * height;

            // 1. Calculate histogram
            int[] hist = new int[256];
            for (int y = 0; y < height; y++)
            {
                byte* row = src.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    hist[row[x]]++;
                }
            }

            // 2. Cumulative Distribution Function (CDF)
            int[] cdf = new int[256];
            int sum = 0;
            int cdfMin = 0;
            bool foundMin = false;

            for (int i = 0; i < 256; i++)
            {
                sum += hist[i];
                cdf[i] = sum;
                if (!foundMin && sum > 0)
                {
                    cdfMin = sum;
                    foundMin = true;
                }
            }

            // 3. Build Equalization LUT
            byte[] lut = new byte[256];
            int denominator = totalPixels - cdfMin;
            if (denominator <= 0) denominator = 1;

            for (int i = 0; i < 256; i++)
            {
                int val = (int)System.Math.Round((float)(cdf[i] - cdfMin) / denominator * 255.0f);
                if (val < 0) val = 0;
                else if (val > 255) val = 255;
                lut[i] = (byte)val;
            }

            // 4. Apply LUT
            ApplyLut(src, dst, lut);
        }
    }
}
