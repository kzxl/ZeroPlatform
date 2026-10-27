using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Unified endian-aware binary span reader and writer.
    /// Provides consistent, zero-allocation Big-Endian and Little-Endian operations across .NET Standard 2.0, .NET 4.6.2, and .NET 8.0.
    /// </summary>
    public static class FastBinary
    {
        #region Little-Endian Readers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16LittleEndian(ReadOnlySpan<byte> source)
            => (short)(source[0] | (source[1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source)
            => (ushort)(source[0] | (source[1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
            => source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
            => (uint)(source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64LittleEndian(ReadOnlySpan<byte> source)
        {
            uint lo = ReadUInt32LittleEndian(source);
            uint hi = ReadUInt32LittleEndian(source.Slice(4));
            return (long)(((ulong)hi << 32) | lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source)
        {
            uint lo = ReadUInt32LittleEndian(source);
            uint hi = ReadUInt32LittleEndian(source.Slice(4));
            return ((ulong)hi << 32) | lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingleLittleEndian(ReadOnlySpan<byte> source)
        {
            int val = ReadInt32LittleEndian(source);
            return Unsafe.As<int, float>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source)
        {
            long val = ReadInt64LittleEndian(source);
            return Unsafe.As<long, double>(ref val);
        }

        #endregion

        #region Big-Endian Readers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16BigEndian(ReadOnlySpan<byte> source)
            => (short)((source[0] << 8) | source[1]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source)
            => (ushort)((source[0] << 8) | source[1]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32BigEndian(ReadOnlySpan<byte> source)
            => (source[0] << 24) | (source[1] << 16) | (source[2] << 8) | source[3];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source)
            => (uint)((source[0] << 24) | (source[1] << 16) | (source[2] << 8) | source[3]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64BigEndian(ReadOnlySpan<byte> source)
        {
            uint hi = ReadUInt32BigEndian(source);
            uint lo = ReadUInt32BigEndian(source.Slice(4));
            return (long)(((ulong)hi << 32) | lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source)
        {
            uint hi = ReadUInt32BigEndian(source);
            uint lo = ReadUInt32BigEndian(source.Slice(4));
            return ((ulong)hi << 32) | lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
        {
            int val = ReadInt32BigEndian(source);
            return Unsafe.As<int, float>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
        {
            long val = ReadInt64BigEndian(source);
            return Unsafe.As<long, double>(ref val);
        }

        #endregion

        #region Little-Endian Writers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16LittleEndian(Span<byte> destination, short value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32LittleEndian(Span<byte> destination, int value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64LittleEndian(Span<byte> destination, long value)
        {
            WriteUInt32LittleEndian(destination, (uint)value);
            WriteUInt32LittleEndian(destination.Slice(4), (uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
        {
            WriteUInt32LittleEndian(destination, (uint)value);
            WriteUInt32LittleEndian(destination.Slice(4), (uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleLittleEndian(Span<byte> destination, float value)
        {
            int intVal = Unsafe.As<float, int>(ref value);
            WriteInt32LittleEndian(destination, intVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            long longVal = Unsafe.As<double, long>(ref value);
            WriteInt64LittleEndian(destination, longVal);
        }

        #endregion

        #region Big-Endian Writers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16BigEndian(Span<byte> destination, short value)
        {
            destination[0] = (byte)(value >> 8);
            destination[1] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
        {
            destination[0] = (byte)(value >> 8);
            destination[1] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32BigEndian(Span<byte> destination, int value)
        {
            destination[0] = (byte)(value >> 24);
            destination[1] = (byte)(value >> 16);
            destination[2] = (byte)(value >> 8);
            destination[3] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32BigEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)(value >> 24);
            destination[1] = (byte)(value >> 16);
            destination[2] = (byte)(value >> 8);
            destination[3] = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64BigEndian(Span<byte> destination, long value)
        {
            WriteUInt32BigEndian(destination, (uint)(value >> 32));
            WriteUInt32BigEndian(destination.Slice(4), (uint)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
        {
            WriteUInt32BigEndian(destination, (uint)(value >> 32));
            WriteUInt32BigEndian(destination.Slice(4), (uint)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleBigEndian(Span<byte> destination, float value)
        {
            int intVal = Unsafe.As<float, int>(ref value);
            WriteInt32BigEndian(destination, intVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleBigEndian(Span<byte> destination, double value)
        {
            long longVal = Unsafe.As<double, long>(ref value);
            WriteInt64BigEndian(destination, longVal);
        }

        #endregion
    }
}
