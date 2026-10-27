using System;

namespace ZeroComm.Core.Checksums
{
    /// <summary>
    /// High-performance standard IEEE 802.3 CRC32 calculation engine with precomputed 256-entry table.
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = new uint[256];

        static Crc32()
        {
            const uint polynomial = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry >>= 1;
                }
                Table[i] = entry;
            }
        }

        /// <summary>
        /// Computes 32-bit CRC according to IEEE 802.3 standard.
        /// </summary>
        public static uint Compute(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < count; i++)
            {
                byte index = (byte)((crc & 0xFF) ^ buffer[offset + i]);
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }

        /// <summary>
        /// Computes 32-bit CRC over an entire byte array.
        /// </summary>
        public static uint Compute(byte[] buffer) => Compute(buffer, 0, buffer.Length);
    }
}
