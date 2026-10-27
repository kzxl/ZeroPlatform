using System;

namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Builder and parser for Modbus TCP frames with MBAP Header (Transaction ID, Protocol ID, Length, Unit ID).
    /// </summary>
    public static class ModbusTcpFrame
    {
        public const int MbapHeaderLength = 7;

        public static byte[] CreateReadHoldingRegistersRequest(ushort transactionId, byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(transactionId, unitId, ModbusFunctionCode.ReadHoldingRegisters, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadInputRegistersRequest(ushort transactionId, byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(transactionId, unitId, ModbusFunctionCode.ReadInputRegisters, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadCoilsRequest(ushort transactionId, byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(transactionId, unitId, ModbusFunctionCode.ReadCoils, startAddress, numberOfPoints);
        }

        public static byte[] CreateReadDiscreteInputsRequest(ushort transactionId, byte unitId, ushort startAddress, ushort numberOfPoints)
        {
            return BuildReadRequest(transactionId, unitId, ModbusFunctionCode.ReadDiscreteInputs, startAddress, numberOfPoints);
        }

        private static byte[] BuildReadRequest(ushort transactionId, byte unitId, ModbusFunctionCode functionCode, ushort startAddress, ushort numberOfPoints)
        {
            byte[] frame = new byte[12];
            // MBAP Header
            frame[0] = (byte)(transactionId >> 8);
            frame[1] = (byte)(transactionId & 0xFF);
            frame[2] = 0x00; // Protocol ID (0 = Modbus)
            frame[3] = 0x00;
            frame[4] = 0x00; // Length = 6 (UnitId + FC + StartAddr + Count)
            frame[5] = 0x06;
            frame[6] = unitId;

            // PDU
            frame[7] = (byte)functionCode;
            frame[8] = (byte)(startAddress >> 8);
            frame[9] = (byte)(startAddress & 0xFF);
            frame[10] = (byte)(numberOfPoints >> 8);
            frame[11] = (byte)(numberOfPoints & 0xFF);
            return frame;
        }

        public static byte[] CreateWriteSingleRegisterRequest(ushort transactionId, byte unitId, ushort registerAddress, ushort value)
        {
            byte[] frame = new byte[12];
            frame[0] = (byte)(transactionId >> 8);
            frame[1] = (byte)(transactionId & 0xFF);
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = 0x00;
            frame[5] = 0x06;
            frame[6] = unitId;

            frame[7] = (byte)ModbusFunctionCode.WriteSingleRegister;
            frame[8] = (byte)(registerAddress >> 8);
            frame[9] = (byte)(registerAddress & 0xFF);
            frame[10] = (byte)(value >> 8);
            frame[11] = (byte)(value & 0xFF);
            return frame;
        }

        public static byte[] CreateWriteSingleCoilRequest(ushort transactionId, byte unitId, ushort coilAddress, bool state)
        {
            byte[] frame = new byte[12];
            frame[0] = (byte)(transactionId >> 8);
            frame[1] = (byte)(transactionId & 0xFF);
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = 0x00;
            frame[5] = 0x06;
            frame[6] = unitId;

            frame[7] = (byte)ModbusFunctionCode.WriteSingleCoil;
            frame[8] = (byte)(coilAddress >> 8);
            frame[9] = (byte)(coilAddress & 0xFF);
            frame[10] = state ? (byte)0xFF : (byte)0x00;
            frame[11] = 0x00;
            return frame;
        }

        public static byte[] CreateWriteMultipleRegistersRequest(ushort transactionId, byte unitId, ushort startAddress, ushort[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values cannot be empty.", nameof(values));

            byte byteCount = (byte)(values.Length * 2);
            ushort length = (ushort)(7 + byteCount); // UnitId(1) + FC(1) + Addr(2) + Count(2) + ByteCount(1) + Bytes(2*N)

            byte[] frame = new byte[6 + length];
            frame[0] = (byte)(transactionId >> 8);
            frame[1] = (byte)(transactionId & 0xFF);
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = (byte)(length >> 8);
            frame[5] = (byte)(length & 0xFF);
            frame[6] = unitId;

            frame[7] = (byte)ModbusFunctionCode.WriteMultipleRegisters;
            frame[8] = (byte)(startAddress >> 8);
            frame[9] = (byte)(startAddress & 0xFF);
            frame[10] = (byte)(values.Length >> 8);
            frame[11] = (byte)(values.Length & 0xFF);
            frame[12] = byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                frame[13 + i * 2] = (byte)(values[i] >> 8);
                frame[14 + i * 2] = (byte)(values[i] & 0xFF);
            }

            return frame;
        }

        public static bool ParseHeader(
            byte[] frame,
            int offset,
            out ushort transactionId,
            out ushort protocolId,
            out ushort length,
            out byte unitId)
        {
            transactionId = 0;
            protocolId = 0;
            length = 0;
            unitId = 0;

            if (frame == null || offset < 0 || offset + MbapHeaderLength > frame.Length)
                return false;

            transactionId = (ushort)((frame[offset] << 8) | frame[offset + 1]);
            protocolId = (ushort)((frame[offset + 2] << 8) | frame[offset + 3]);
            length = (ushort)((frame[offset + 4] << 8) | frame[offset + 5]);
            unitId = frame[offset + 6];
            return true;
        }

        public static bool ParseReadRegistersResponse(
            byte[] frame,
            int offset,
            int count,
            out ushort transactionId,
            out byte unitId,
            out ushort[] registers,
            out ModbusExceptionCode exception)
        {
            transactionId = 0;
            unitId = 0;
            registers = Array.Empty<ushort>();
            exception = ModbusExceptionCode.None;

            if (!ParseHeader(frame, offset, out transactionId, out _, out _, out unitId))
                return false;

            if (count < MbapHeaderLength + 2) return false;

            byte functionCode = frame[offset + 7];
            if ((functionCode & 0x80) != 0)
            {
                exception = (ModbusExceptionCode)frame[offset + 8];
                return true;
            }

            byte byteCount = frame[offset + 8];
            int numRegisters = byteCount / 2;
            registers = new ushort[numRegisters];

            for (int i = 0; i < numRegisters; i++)
            {
                int high = frame[offset + 9 + i * 2];
                int low = frame[offset + 10 + i * 2];
                registers[i] = (ushort)((high << 8) | low);
            }

            return true;
        }

        public static bool ParseReadCoilsResponse(
            byte[] frame,
            int offset,
            int count,
            int expectedCoils,
            out ushort transactionId,
            out byte unitId,
            out bool[] coils,
            out ModbusExceptionCode exception)
        {
            transactionId = 0;
            unitId = 0;
            coils = Array.Empty<bool>();
            exception = ModbusExceptionCode.None;

            if (!ParseHeader(frame, offset, out transactionId, out _, out _, out unitId))
                return false;

            if (count < MbapHeaderLength + 2) return false;

            byte functionCode = frame[offset + 7];
            if ((functionCode & 0x80) != 0)
            {
                exception = (ModbusExceptionCode)frame[offset + 8];
                return true;
            }

            byte byteCount = frame[offset + 8];
            coils = new bool[expectedCoils];

            for (int i = 0; i < expectedCoils; i++)
            {
                int byteIndex = offset + 9 + (i / 8);
                int bitIndex = i % 8;
                if (byteIndex < offset + 9 + byteCount)
                {
                    coils[i] = ((frame[byteIndex] >> bitIndex) & 1) == 1;
                }
            }

            return true;
        }
    }
}
