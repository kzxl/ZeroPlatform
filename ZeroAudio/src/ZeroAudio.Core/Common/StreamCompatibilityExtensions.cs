using System;
using System.Buffers;
using System.IO;

namespace ZeroAudio.Common
{
    internal static class StreamCompatibilityExtensions
    {
#if !NETCOREAPP && !NET6_0_OR_GREATER
        public static int ReadCompat(this Stream stream, Span<byte> buffer)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int read = stream.Read(rented, 0, buffer.Length);
                if (read > 0)
                {
                    rented.AsSpan(0, read).CopyTo(buffer);
                }
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public static void WriteCompat(this Stream stream, ReadOnlySpan<byte> buffer)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(rented);
                stream.Write(rented, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
#else
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static int ReadCompat(this Stream stream, Span<byte> buffer) => stream.Read(buffer);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void WriteCompat(this Stream stream, ReadOnlySpan<byte> buffer) => stream.Write(buffer);
#endif
    }
}
