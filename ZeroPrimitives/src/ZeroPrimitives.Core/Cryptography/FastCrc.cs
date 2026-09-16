using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Cryptography
{
    /// <summary>
    /// High-throughput industrial checksum calculation for Modbus RTU, PLCs, barcode scanners, and camera frames.
    /// Employs precomputed 256-entry lookup tables for single-instruction-per-byte speed.
    /// </summary>
    public static class FastCrc
    {
        #region CRC16 Modbus (Polynomial 0xA001, Init 0xFFFF)

        private static readonly ushort[] Crc16ModbusTable = GenerateCrc16ModbusTable();

        private static ushort[] GenerateCrc16ModbusTable()
        {
            var table = new ushort[256];
            for (ushort i = 0; i < 256; i++)
            {
                ushort value = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((value & 1) != 0)
                        value = (ushort)((value >> 1) ^ 0xA001);
                    else
                        value >>= 1;
                }
                table[i] = value;
            }
            return table;
        }

        /// <summary>
        /// Computes 16-bit Modbus CRC for industrial communication protocols (RS485, scales, PLCs).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ushort Crc16Modbus(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            if (data.IsEmpty) return crc;

            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    crc = (ushort)((crc >> 8) ^ Crc16ModbusTable[(crc ^ ptr[0]) & 0xFF]);
                    crc = (ushort)((crc >> 8) ^ Crc16ModbusTable[(crc ^ ptr[1]) & 0xFF]);
                    crc = (ushort)((crc >> 8) ^ Crc16ModbusTable[(crc ^ ptr[2]) & 0xFF]);
                    crc = (ushort)((crc >> 8) ^ Crc16ModbusTable[(crc ^ ptr[3]) & 0xFF]);
                    ptr += 4;
                }

                while (ptr < end)
                {
                    crc = (ushort)((crc >> 8) ^ Crc16ModbusTable[(crc ^ *ptr++) & 0xFF]);
                }
            }

            return crc;
        }

        #endregion

        #region CRC16 CCITT (Polynomial 0x1021)

        private static readonly ushort[] Crc16CcittTable = GenerateCrc16CcittTable();

        private static ushort[] GenerateCrc16CcittTable()
        {
            var table = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                ushort curr = 0;
                ushort temp = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    if (((curr ^ temp) & 0x8000) != 0)
                        curr = (ushort)((curr << 1) ^ 0x1021);
                    else
                        curr <<= 1;
                    temp <<= 1;
                }
                table[i] = curr;
            }
            return table;
        }

        /// <summary>
        /// Computes 16-bit CRC-CCITT (X.25, HDLC, XMODEM).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ushort Crc16Ccitt(ReadOnlySpan<byte> data, ushort seed = 0xFFFF)
        {
            ushort crc = seed;
            if (data.IsEmpty) return crc;

            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;
                while (ptr < end)
                {
                    crc = (ushort)((crc << 8) ^ Crc16CcittTable[((crc >> 8) ^ *ptr++) & 0xFF]);
                }
            }

            return crc;
        }

        #endregion

        #region CRC32 (IEEE 802.3, Polynomial 0xEDB88320)

        private static readonly uint[] Crc32Table = GenerateCrc32Table();

        private static uint[] GenerateCrc32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ 0xEDB88320u;
                    else
                        entry >>= 1;
                }
                table[i] = entry;
            }
            return table;
        }

        /// <summary>
        /// Computes standard 32-bit CRC (Ethernet, PNG, ZIP, camera frame buffer integrity).
        /// Features 4-way loop unrolling for maximum instruction throughput.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Crc32(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            if (data.IsEmpty) return 0;

            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ ptr[0]) & 0xFF];
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ ptr[1]) & 0xFF];
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ ptr[2]) & 0xFF];
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ ptr[3]) & 0xFF];
                    ptr += 4;
                }

                while (ptr < end)
                {
                    crc = (crc >> 8) ^ Crc32Table[(crc ^ *ptr++) & 0xFF];
                }
            }

            return crc ^ 0xFFFFFFFFu;
        }

        #endregion

        #region CRC32C (Castagnoli, Polynomial 0x82F63B78 - Hardware Accelerated)

        private static readonly uint[] Crc32CTable = GenerateCrc32CTable();

        private static uint[] GenerateCrc32CTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ 0x82F63B78u;
                    else
                        entry >>= 1;
                }
                table[i] = entry;
            }
            return table;
        }

        /// <summary>
        /// Indicates whether hardware-accelerated CRC32C instructions (SSE4.2 on x86/x64 or ARM64) are active.
        /// </summary>
        public static bool IsHardwareAcceleratedCrc32C
        {
            get
            {
#if NET8_0_OR_GREATER
                return System.Runtime.Intrinsics.X86.Sse42.IsSupported ||
                       System.Runtime.Intrinsics.Arm.Crc32.IsSupported;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Computes 32-bit Castagnoli CRC (CRC-32C).
        /// Executes via native CPU hardware instructions (SSE4.2 or ARM64) on .NET 8+, processing 8 bytes per single cycle.
        /// Falls back to 4-way loop unrolled lookup table on .NET Framework.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Crc32C(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return 0;

#if NET8_0_OR_GREATER
            if (System.Runtime.Intrinsics.X86.Sse42.X64.IsSupported)
            {
                ulong crc = 0xFFFFFFFFu;
                fixed (byte* p = data)
                {
                    byte* ptr = p;
                    int length = data.Length;

                    while (length >= 8)
                    {
                        crc = System.Runtime.Intrinsics.X86.Sse42.X64.Crc32(crc, *(ulong*)ptr);
                        ptr += 8;
                        length -= 8;
                    }
                    if (length >= 4)
                    {
                        crc = System.Runtime.Intrinsics.X86.Sse42.Crc32((uint)crc, *(uint*)ptr);
                        ptr += 4;
                        length -= 4;
                    }
                    while (length > 0)
                    {
                        crc = System.Runtime.Intrinsics.X86.Sse42.Crc32((uint)crc, *ptr);
                        ptr++;
                        length--;
                    }
                }
                return (uint)crc ^ 0xFFFFFFFFu;
            }
            else if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
            {
                uint crc = 0xFFFFFFFFu;
                fixed (byte* p = data)
                {
                    byte* ptr = p;
                    int length = data.Length;

                    while (length >= 4)
                    {
                        crc = System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, *(uint*)ptr);
                        ptr += 4;
                        length -= 4;
                    }
                    while (length > 0)
                    {
                        crc = System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, *ptr);
                        ptr++;
                        length--;
                    }
                }
                return crc ^ 0xFFFFFFFFu;
            }
            else if (System.Runtime.Intrinsics.Arm.Crc32.Arm64.IsSupported)
            {
                uint crc = 0xFFFFFFFFu;
                fixed (byte* p = data)
                {
                    byte* ptr = p;
                    int length = data.Length;

                    while (length >= 8)
                    {
                        crc = System.Runtime.Intrinsics.Arm.Crc32.Arm64.ComputeCrc32C(crc, *(ulong*)ptr);
                        ptr += 8;
                        length -= 8;
                    }
                    while (length > 0)
                    {
                        crc = System.Runtime.Intrinsics.Arm.Crc32.ComputeCrc32C(crc, *ptr);
                        ptr++;
                        length--;
                    }
                }
                return crc ^ 0xFFFFFFFFu;
            }
#endif

            // Software fallback table with 4-way unrolling
            uint swCrc = 0xFFFFFFFFu;
            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    swCrc = (swCrc >> 8) ^ Crc32CTable[(swCrc ^ ptr[0]) & 0xFF];
                    swCrc = (swCrc >> 8) ^ Crc32CTable[(swCrc ^ ptr[1]) & 0xFF];
                    swCrc = (swCrc >> 8) ^ Crc32CTable[(swCrc ^ ptr[2]) & 0xFF];
                    swCrc = (swCrc >> 8) ^ Crc32CTable[(swCrc ^ ptr[3]) & 0xFF];
                    ptr += 4;
                }

                while (ptr < end)
                {
                    swCrc = (swCrc >> 8) ^ Crc32CTable[(swCrc ^ *ptr++) & 0xFF];
                }
            }

            return swCrc ^ 0xFFFFFFFFu;
        }

        #endregion
    }
}
