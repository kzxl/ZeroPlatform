using System;
using System.IO;
using ZeroVideo.Color;
using ZeroVideo.Core;

namespace ZeroVideo.Playback
{
    /// <summary>
    /// Pure C# high-performance snapshot and frame exporter for industrial machine vision and video recording.
    /// Exports raw <see cref="VideoFrameBuffer"/> into uncompressed standard Windows Bitmap (.bmp) files/streams
    /// with zero native dependencies (supports monochrome 8-bit grayscale palette, 24-bit BGR, and 32-bit BGRA).
    /// </summary>
    public static class SnapshotExporter
    {
        /// <summary>
        /// Exports the given video frame to an uncompressed standard BMP byte array.
        /// </summary>
        public static byte[] ExportToBmpBytes(VideoFrameBuffer frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            using (var ms = new MemoryStream())
            {
                ExportToBmp(frame, ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Saves the video frame as an uncompressed standard BMP image file to disk.
        /// </summary>
        public static void ExportToBmp(VideoFrameBuffer frame, string filePath)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ExportToBmp(frame, fs);
            }
        }

        /// <summary>
        /// Writes the video frame as an uncompressed standard BMP image directly into the destination stream.
        /// </summary>
        public static void ExportToBmp(VideoFrameBuffer frame, Stream stream)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            int width = frame.Width;
            int height = frame.Height;

            // Normalize frame to supported format if YUV
            VideoFrameBuffer workFrame = frame;
            if (frame.PixelFormat == VideoPixelFormat.Yuv420p || frame.PixelFormat == VideoPixelFormat.Nv12)
            {
                workFrame = new VideoFrameBuffer(width, height, VideoPixelFormat.Bgr24);
                if (frame.PixelFormat == VideoPixelFormat.Yuv420p)
                    ColorConverter.Yuv420pToRgb(frame, workFrame, isBgr: true);
                else
                    ColorConverter.Nv12ToRgb(frame, workFrame, isBgr: true);
            }

            bool isGray8 = workFrame.PixelFormat == VideoPixelFormat.Gray8;
            bool is32Bit = workFrame.PixelFormat == VideoPixelFormat.Bgra32 || workFrame.PixelFormat == VideoPixelFormat.Rgba32;

            ushort bitCount = isGray8 ? (ushort)8 : (is32Bit ? (ushort)32 : (ushort)24);
            int bytesPerPixel = bitCount / 8;
            int rowStride = (width * bytesPerPixel + 3) & ~3; // 4-byte aligned per BMP spec
            int paletteSize = isGray8 ? 1024 : 0; // 256 colors * 4 bytes
            uint pixelDataOffset = (uint)(14 + 40 + paletteSize);
            uint imageSize = (uint)(rowStride * height);
            uint fileSize = pixelDataOffset + imageSize;

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8))
            {
                // --- 1. BITMAPFILEHEADER (14 bytes) ---
                writer.Write((byte)'B');
                writer.Write((byte)'M');
                writer.Write(fileSize);
                writer.Write((ushort)0); // Reserved1
                writer.Write((ushort)0); // Reserved2
                writer.Write(pixelDataOffset);

                // --- 2. BITMAPINFOHEADER (40 bytes) ---
                writer.Write((uint)40);        // biSize
                writer.Write(width);           // biWidth
                writer.Write(height);          // biHeight (positive = bottom-up)
                writer.Write((ushort)1);       // biPlanes
                writer.Write(bitCount);        // biBitCount (8, 24, or 32)
                writer.Write((uint)0);         // biCompression (BI_RGB = 0)
                writer.Write(imageSize);       // biSizeImage
                writer.Write(2835);            // biXPelsPerMeter (~72 DPI)
                writer.Write(2835);            // biYPelsPerMeter (~72 DPI)
                writer.Write(isGray8 ? (uint)256 : (uint)0); // biClrUsed
                writer.Write((uint)0);         // biClrImportant

                // --- 3. COLOR PALETTE (For 8-bit Gray8 only) ---
                if (isGray8)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        byte b = (byte)i;
                        writer.Write(b); // Blue
                        writer.Write(b); // Green
                        writer.Write(b); // Red
                        writer.Write((byte)0); // Reserved
                    }
                }

                // --- 4. PIXEL DATA (Bottom-up row order) ---
                byte[] rowBuffer = new byte[rowStride];
                byte[] srcData = workFrame.Data;
                int srcStride = workFrame.Stride;

                for (int y = height - 1; y >= 0; y--)
                {
                    int srcRowStart = y * srcStride;
                    Array.Clear(rowBuffer, 0, rowStride);

                    if (workFrame.PixelFormat == VideoPixelFormat.Gray8)
                    {
                        Buffer.BlockCopy(srcData, srcRowStart, rowBuffer, 0, width);
                    }
                    else if (workFrame.PixelFormat == VideoPixelFormat.Bgr24)
                    {
                        Buffer.BlockCopy(srcData, srcRowStart, rowBuffer, 0, width * 3);
                    }
                    else if (workFrame.PixelFormat == VideoPixelFormat.Rgb24)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int srcIdx = srcRowStart + (x * 3);
                            int dstIdx = x * 3;
                            rowBuffer[dstIdx] = srcData[srcIdx + 2];     // B
                            rowBuffer[dstIdx + 1] = srcData[srcIdx + 1]; // G
                            rowBuffer[dstIdx + 2] = srcData[srcIdx];     // R
                        }
                    }
                    else if (workFrame.PixelFormat == VideoPixelFormat.Bgra32)
                    {
                        Buffer.BlockCopy(srcData, srcRowStart, rowBuffer, 0, width * 4);
                    }
                    else if (workFrame.PixelFormat == VideoPixelFormat.Rgba32)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int srcIdx = srcRowStart + (x * 4);
                            int dstIdx = x * 4;
                            rowBuffer[dstIdx] = srcData[srcIdx + 2];     // B
                            rowBuffer[dstIdx + 1] = srcData[srcIdx + 1]; // G
                            rowBuffer[dstIdx + 2] = srcData[srcIdx];     // R
                            rowBuffer[dstIdx + 3] = srcData[srcIdx + 3]; // A
                        }
                    }

                    writer.Write(rowBuffer, 0, rowStride);
                }

                writer.Flush();
            }
        }
    }
}
