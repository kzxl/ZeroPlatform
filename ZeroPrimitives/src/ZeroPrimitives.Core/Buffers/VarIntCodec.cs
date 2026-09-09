using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Variable-length integer codec (LEB128 & ZigZag encoding).
    /// Used by Protobuf, SQLite, WebAssembly, and compact telemetry packets.
    /// Encodes small numbers in 1-2 bytes instead of 4-8 bytes.
    /// </summary>
    public static class VarIntCodec
    {
        #region Unsigned 32-bit (VarUInt32)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVarUInt32(Span<byte> destination, uint value, out int bytesWritten)
        {
            int idx = 0;
            while (value >= 0x80)
            {
                destination[idx++] = (byte)(value | 0x80);
                value >>= 7;
            }
            destination[idx++] = (byte)value;
            bytesWritten = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadVarUInt32(ReadOnlySpan<byte> source, out uint value, out int bytesRead)
        {
            value = 0;
            bytesRead = 0;
            int shift = 0;

            for (int i = 0; i < source.Length && i < 5; i++)
            {
                byte b = source[i];
                value |= (uint)(b & 0x7F) << shift;
                bytesRead++;

                if ((b & 0x80) == 0)
                {
                    return true;
                }

                shift += 7;
            }

            return false;
        }

        #endregion

        #region Signed 32-bit (ZigZag VarInt32)

        /// <summary>
        /// Writes signed 32-bit integer with ZigZag encoding so small negative numbers take few bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVarInt32(Span<byte> destination, int value, out int bytesWritten)
        {
            uint zigZag = (uint)((value << 1) ^ (value >> 31));
            WriteVarUInt32(destination, zigZag, out bytesWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadVarInt32(ReadOnlySpan<byte> source, out int value, out int bytesRead)
        {
            if (TryReadVarUInt32(source, out uint raw, out bytesRead))
            {
                value = (int)(raw >> 1) ^ -(int)(raw & 1);
                return true;
            }

            value = 0;
            return false;
        }

        #endregion

        #region Unsigned 64-bit (VarUInt64)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVarUInt64(Span<byte> destination, ulong value, out int bytesWritten)
        {
            int idx = 0;
            while (value >= 0x80)
            {
                destination[idx++] = (byte)(value | 0x80);
                value >>= 7;
            }
            destination[idx++] = (byte)value;
            bytesWritten = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadVarUInt64(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
        {
            value = 0;
            bytesRead = 0;
            int shift = 0;

            for (int i = 0; i < source.Length && i < 10; i++)
            {
                byte b = source[i];
                value |= (ulong)(b & 0x7F) << shift;
                bytesRead++;

                if ((b & 0x80) == 0)
                {
                    return true;
                }

                shift += 7;
            }

            return false;
        }

        #endregion

        #region Signed 64-bit (ZigZag VarInt64)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteVarInt64(Span<byte> destination, long value, out int bytesWritten)
        {
            ulong zigZag = (ulong)((value << 1) ^ (value >> 63));
            WriteVarUInt64(destination, zigZag, out bytesWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadVarInt64(ReadOnlySpan<byte> source, out long value, out int bytesRead)
        {
            if (TryReadVarUInt64(source, out ulong raw, out bytesRead))
            {
                value = (long)(raw >> 1) ^ -(long)(raw & 1);
                return true;
            }

            value = 0;
            return false;
        }

        #endregion
    }
}
