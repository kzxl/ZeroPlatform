using System;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Cryptography;

namespace ZeroCompression.Core.Hashing
{
    /// <summary>
    /// Standard CRC-32 (IEEE 802.3, polynomial 0xEDB88320) with Span and streaming support.
    /// Employs 4-way loop unrolled acceleration aligned with <see cref="FastCrc.Crc32"/>.
    /// </summary>
    public sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _crc = 0xFFFFFFFFu;

        private static uint[] BuildTable()
        {
            const uint poly = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Append(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return;

            uint crc = _crc;
            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    crc = (crc >> 8) ^ Table[(crc ^ ptr[0]) & 0xFF];
                    crc = (crc >> 8) ^ Table[(crc ^ ptr[1]) & 0xFF];
                    crc = (crc >> 8) ^ Table[(crc ^ ptr[2]) & 0xFF];
                    crc = (crc >> 8) ^ Table[(crc ^ ptr[3]) & 0xFF];
                    ptr += 4;
                }

                while (ptr < end)
                {
                    crc = (crc >> 8) ^ Table[(crc ^ *ptr++) & 0xFF];
                }
            }
            _crc = crc;
        }

        public uint Value => _crc ^ 0xFFFFFFFFu;

        public void Reset() => _crc = 0xFFFFFFFFu;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Compute(ReadOnlySpan<byte> data) => FastCrc.Crc32(data);
    }
}
