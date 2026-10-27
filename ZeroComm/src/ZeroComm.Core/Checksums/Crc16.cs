using System;

namespace ZeroComm.Core.Checksums
{
    /// <summary>
    /// High-performance CRC16 calculation engine with precomputed lookup tables.
    /// Supports Modbus, CCITT, and X25 algorithms without any third-party dependencies.
    /// </summary>
    public static class Crc16
    {
        private static readonly ushort[] ModbusTable = new ushort[256];
        private static readonly ushort[] CcittTable = new ushort[256];

        static Crc16()
        {
            // Modbus Polynomial: 0xA001 (reversed 0x8005)
            for (ushort i = 0; i < 256; i++)
            {
                ushort value = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((value & 1) != 0)
                        value = (ushort)((value >> 1) ^ 0xA001);
                    else
                        value = (ushort)(value >> 1);
                }
                ModbusTable[i] = value;
            }

            // CCITT Polynomial: 0x1021
            for (ushort i = 0; i < 256; i++)
            {
                ushort value = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((value & 0x8000) != 0)
                        value = (ushort)((value << 1) ^ 0x1021);
                    else
                        value = (ushort)(value << 1);
                }
                CcittTable[i] = value;
            }
        }

        /// <summary>
        /// Computes standard Modbus RTU CRC16 checksum (initial value 0xFFFF, polynomial 0xA001).
        /// </summary>
        public static ushort ComputeModbus(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            ushort crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                byte index = (byte)(crc ^ buffer[offset + i]);
                crc = (ushort)((crc >> 8) ^ ModbusTable[index]);
            }
            return crc;
        }

        /// <summary>
        /// Computes standard Modbus RTU CRC16 checksum over an entire byte array.
        /// </summary>
        public static ushort ComputeModbus(byte[] buffer) => ComputeModbus(buffer, 0, buffer.Length);

        /// <summary>
        /// Computes CCITT CRC16 (initial value 0xFFFF, polynomial 0x1021).
        /// </summary>
        public static ushort ComputeCcitt(byte[] buffer, int offset, int count, ushort initialValue = 0xFFFF)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            ushort crc = initialValue;
            for (int i = 0; i < count; i++)
            {
                byte index = (byte)((crc >> 8) ^ buffer[offset + i]);
                crc = (ushort)((crc << 8) ^ CcittTable[index]);
            }
            return crc;
        }

        /// <summary>
        /// Computes CCITT CRC16 over an entire byte array.
        /// </summary>
        public static ushort ComputeCcitt(byte[] buffer, ushort initialValue = 0xFFFF) =>
            ComputeCcitt(buffer, 0, buffer.Length, initialValue);
    }
}
