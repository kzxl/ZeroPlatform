using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Low-level memory, compression, endianness, and binary buffer utilities.
    /// </summary>
    public static class FastBuffer
    {
        #region GZip Compression

        /// <summary>
        /// Compresses raw bytes using GZip.
        /// </summary>
        public static byte[] GzipCompress(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return Array.Empty<byte>();

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
            {
#if NET8_0_OR_GREATER
                gzip.Write(data);
#else
                byte[] temp = data.ToArray();
                gzip.Write(temp, 0, temp.Length);
#endif
            }
            return output.ToArray();
        }

        /// <summary>
        /// Decompresses GZip compressed bytes into UTF-8 text.
        /// </summary>
        public static string GzipDecompressToString(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0) return string.Empty;

            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Decompresses GZip compressed bytes into raw byte array.
        /// </summary>
        public static byte[] GzipDecompress(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0) return Array.Empty<byte>();

            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        #endregion

        #region Endian Swapping (Bit-level byte reversal)

        /// <summary>
        /// Swaps endianness of a 16-bit integer.
        /// </summary>
        public static short SwapInt16(short value)
            => (short)((value << 8) | ((value >> 8) & 0xFF));

        /// <summary>
        /// Swaps endianness of an unsigned 16-bit integer.
        /// </summary>
        public static ushort SwapUInt16(ushort value)
            => (ushort)((value << 8) | (value >> 8));

        /// <summary>
        /// Swaps endianness of a 32-bit integer.
        /// </summary>
        public static int SwapInt32(int value)
            => (int)SwapUInt32((uint)value);

        /// <summary>
        /// Swaps endianness of an unsigned 32-bit integer.
        /// </summary>
        public static uint SwapUInt32(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }

        /// <summary>
        /// Swaps endianness of a 64-bit integer.
        /// </summary>
        public static long SwapInt64(long value)
            => (long)SwapUInt64((ulong)value);

        /// <summary>
        /// Swaps endianness of an unsigned 64-bit integer.
        /// </summary>
        public static ulong SwapUInt64(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) |
                   ((value & 0x000000000000FF00UL) << 40) |
                   ((value & 0x0000000000FF0000UL) << 24) |
                   ((value & 0x00000000FF000000UL) << 8) |
                   ((value & 0x000000FF00000000UL) >> 8) |
                   ((value & 0x0000FF0000000000UL) >> 24) |
                   ((value & 0x00FF000000000000UL) >> 40) |
                   ((value & 0xFF00000000000000UL) >> 56);
        }

        #endregion
    }
}
