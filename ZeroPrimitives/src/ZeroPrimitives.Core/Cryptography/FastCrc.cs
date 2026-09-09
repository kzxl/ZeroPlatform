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
    }
}
