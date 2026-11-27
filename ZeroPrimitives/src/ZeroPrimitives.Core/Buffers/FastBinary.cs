using System;
using System.Buffers.Binary;
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
            => BinaryPrimitives.ReadInt16LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt16LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt32LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt32LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64LittleEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt64LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt64LittleEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingleLittleEndian(ReadOnlySpan<byte> source)
        {
            int val = BinaryPrimitives.ReadInt32LittleEndian(source);
            return Unsafe.As<int, float>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source)
        {
            long val = BinaryPrimitives.ReadInt64LittleEndian(source);
            return Unsafe.As<long, double>(ref val);
        }

        #endregion

        #region Big-Endian Readers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt16BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt16BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt32BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt32BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadInt64BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source)
            => BinaryPrimitives.ReadUInt64BigEndian(source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
        {
            int val = BinaryPrimitives.ReadInt32BigEndian(source);
            return Unsafe.As<int, float>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
        {
            long val = BinaryPrimitives.ReadInt64BigEndian(source);
            return Unsafe.As<long, double>(ref val);
        }

        #endregion

        #region Little-Endian Writers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16LittleEndian(Span<byte> destination, short value)
            => BinaryPrimitives.WriteInt16LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
            => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32LittleEndian(Span<byte> destination, int value)
            => BinaryPrimitives.WriteInt32LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
            => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64LittleEndian(Span<byte> destination, long value)
            => BinaryPrimitives.WriteInt64LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
            => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleLittleEndian(Span<byte> destination, float value)
        {
            int intVal = Unsafe.As<float, int>(ref value);
            BinaryPrimitives.WriteInt32LittleEndian(destination, intVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            long longVal = Unsafe.As<double, long>(ref value);
            BinaryPrimitives.WriteInt64LittleEndian(destination, longVal);
        }

        #endregion

        #region Big-Endian Writers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16BigEndian(Span<byte> destination, short value)
            => BinaryPrimitives.WriteInt16BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
            => BinaryPrimitives.WriteUInt16BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32BigEndian(Span<byte> destination, int value)
            => BinaryPrimitives.WriteInt32BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32BigEndian(Span<byte> destination, uint value)
            => BinaryPrimitives.WriteUInt32BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64BigEndian(Span<byte> destination, long value)
            => BinaryPrimitives.WriteInt64BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
            => BinaryPrimitives.WriteUInt64BigEndian(destination, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleBigEndian(Span<byte> destination, float value)
        {
            int intVal = Unsafe.As<float, int>(ref value);
            BinaryPrimitives.WriteInt32BigEndian(destination, intVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleBigEndian(Span<byte> destination, double value)
        {
            long longVal = Unsafe.As<double, long>(ref value);
            BinaryPrimitives.WriteInt64BigEndian(destination, longVal);
        }

        #endregion
    }
}
