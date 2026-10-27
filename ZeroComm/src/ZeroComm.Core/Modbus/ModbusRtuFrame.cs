using System;
using ZeroComm.Core.Checksums;

namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Builder and parser for Modbus RTU frames with CRC16 validation and zero dependencies.
    /// </summary>
    public static class ModbusRtuFrame
    {
        public static byte[] CreateReadHoldingRegistersRequest(byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(unitId, ModbusFunctionCode.ReadHoldingRegisters, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadInputRegistersRequest(byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(unitId, ModbusFunctionCode.ReadInputRegisters, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadCoilsRequest(byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(unitId, ModbusFunctionCode.ReadCoils, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadDiscreteInputsRequest(byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(unitId, ModbusFunctionCode.ReadDiscreteInputs, startAddress, numberOfPoints);
        }

        private static byte[] BuildReadRequest(byte unitId, ModbusFunctionCode functionCode, ushort startAddress, ushort numberOfPoints)
        {
            byte[] frame = new byte[8];
            frame[0] = unitId;
            frame[1] = (byte)functionCode;
            frame[2] = (byte)(startAddress >> 8);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)(numberOfPoints >> 8);
            frame[5] = (byte)(numberOfPoints & 0xFF);

            ushort crc = Crc16.ComputeModbus(frame, 0, 6);
            frame[6] = (byte)(crc & 0xFF);        // CRC Low
            frame[7] = (byte)((crc >> 8) & 0xFF); // CRC High
            return frame;
        }

        public static byte[] CreateWriteSingleRegisterRequest(byte unitId, ushort registerAddress, ushort value)
        {
            byte[] frame = new byte[8];
            frame[0] = unitId;
            frame[1] = (byte)ModbusFunctionCode.WriteSingleRegister;
            frame[2] = (byte)(registerAddress >> 8);
            frame[3] = (byte)(registerAddress & 0xFF);
            frame[4] = (byte)(value >> 8);
            frame[5] = (byte)(value & 0xFF);

            ushort crc = Crc16.ComputeModbus(frame, 0, 6);
            frame[6] = (byte)(crc & 0xFF);
            frame[7] = (byte)((crc >> 8) & 0xFF);
            return frame;
        }

        public static byte[] CreateWriteSingleCoilRequest(byte unitId, ushort coilAddress, bool state)
        {
            byte[] frame = new byte[8];
            frame[0] = unitId;
            frame[1] = (byte)ModbusFunctionCode.WriteSingleCoil;
            frame[2] = (byte)(coilAddress >> 8);
            frame[3] = (byte)(coilAddress & 0xFF);
            frame[4] = state ? (byte)0xFF : (byte)0x00;
            frame[5] = 0x00;

            ushort crc = Crc16.ComputeModbus(frame, 0, 6);
            frame[6] = (byte)(crc & 0xFF);
            frame[7] = (byte)((crc >> 8) & 0xFF);
            return frame;
        }

        public static byte[] CreateWriteMultipleRegistersRequest(byte unitId, ushort startAddress, ushort[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values cannot be empty.", nameof(values));
            if (values.Length > 123)
                throw new ArgumentException("Modbus RTU cannot write more than 123 registers in a single request.");

            byte byteCount = (byte)(values.Length * 2);
            int frameLength = 7 + byteCount + 2;
            byte[] frame = new byte[frameLength];

            frame[0] = unitId;
            frame[1] = (byte)ModbusFunctionCode.WriteMultipleRegisters;
            frame[2] = (byte)(startAddress >> 8);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)(values.Length >> 8);
            frame[5] = (byte)(values.Length & 0xFF);
            frame[6] = byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                frame[7 + i * 2] = (byte)(values[i] >> 8);
                frame[8 + i * 2] = (byte)(values[i] & 0xFF);
            }

            ushort crc = Crc16.ComputeModbus(frame, 0, 7 + byteCount);
            frame[frameLength - 2] = (byte)(crc & 0xFF);
            frame[frameLength - 1] = (byte)((crc >> 8) & 0xFF);
            return frame;
        }

        /// <summary>
        /// Validates that the frame has a minimum length of 5 bytes and a matching CRC16 checksum.
        /// </summary>
        public static bool ValidateCrc(byte[] frame, int offset, int count)
        {
            if (frame == null || count < 5 || offset + count > frame.Length) return false;

            ushort computedCrc = Crc16.ComputeModbus(frame, offset, count - 2);
            byte expectedLow = (byte)(computedCrc & 0xFF);
            byte expectedHigh = (byte)((computedCrc >> 8) & 0xFF);

            return frame[offset + count - 2] == expectedLow && frame[offset + count - 1] == expectedHigh;
        }

        /// <summary>
        /// Parses a Read Holding Registers or Read Input Registers response frame.
        /// </summary>
        public static bool ParseReadRegistersResponse(
            byte[] frame,
            int offset,
            int count,
            out byte unitId,
            out ushort[] registers,
            out ModbusExceptionCode exception)
        {
            unitId = 0;
            registers = Array.Empty<ushort>();
            exception = ModbusExceptionCode.None;

            if (!ValidateCrc(frame, offset, count)) return false;

            unitId = frame[offset];
            byte functionCode = frame[offset + 1];

            // Check if exception response (function code has MSB set: 0x80 | FC)
            if ((functionCode & 0x80) != 0)
            {
                exception = (ModbusExceptionCode)frame[offset + 2];
                return true;
            }

            byte byteCount = frame[offset + 2];
            int numRegisters = byteCount / 2;
            registers = new ushort[numRegisters];

            for (int i = 0; i < numRegisters; i++)
            {
                int high = frame[offset + 3 + i * 2];
                int low = frame[offset + 4 + i * 2];
                registers[i] = (ushort)((high << 8) | low);
            }

            return true;
        }

        /// <summary>
        /// Parses a Read Coils or Read Discrete Inputs response frame.
        /// </summary>
        public static bool ParseReadCoilsResponse(
            byte[] frame,
            int offset,
            int count,
            int expectedCoilCount,
            out byte unitId,
            out bool[] coils,
            out ModbusExceptionCode exception)
        {
            unitId = 0;
            coils = Array.Empty<bool>();
            exception = ModbusExceptionCode.None;

            if (!ValidateCrc(frame, offset, count)) return false;

            unitId = frame[offset];
            byte functionCode = frame[offset + 1];

            if ((functionCode & 0x80) != 0)
            {
                exception = (ModbusExceptionCode)frame[offset + 2];
                return true;
            }

            byte byteCount = frame[offset + 2];
            coils = new bool[expectedCoilCount];

            for (int i = 0; i < expectedCoilCount; i++)
            {
                int byteIndex = offset + 3 + (i / 8);
                int bitIndex = i % 8;
                if (byteIndex < offset + 3 + byteCount)
                {
                    coils[i] = ((frame[byteIndex] >> bitIndex) & 1) == 1;
                }
            }

            return true;
        }
    }
}
