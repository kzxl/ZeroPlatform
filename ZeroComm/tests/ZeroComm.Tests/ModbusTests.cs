using Xunit;
using ZeroComm.Core.Checksums;
using ZeroComm.Core.Modbus;

namespace ZeroComm.Tests
{
    public class ModbusTests
    {
        [Fact]
        public void ModbusRtu_CreateReadHoldingRegistersRequest_HasCorrectStructure()
        {
            byte unitId = 1;
            ushort startAddr = 100;
            ushort count = 10;

            byte[] frame = ModbusRtuFrame.CreateReadHoldingRegistersRequest(unitId, startAddr, count);

            Assert.Equal(8, frame.Length);
            Assert.Equal(unitId, frame[0]);
            Assert.Equal((byte)ModbusFunctionCode.ReadHoldingRegisters, frame[1]);
            Assert.Equal(0, frame[2]);
            Assert.Equal(100, frame[3]);
            Assert.Equal(0, frame[4]);
            Assert.Equal(10, frame[5]);

            // Validate CRC
            Assert.True(ModbusRtuFrame.ValidateCrc(frame, 0, frame.Length));
        }

        [Fact]
        public void ModbusRtu_ParseReadHoldingRegistersResponse_Success()
        {
            // Simulate response from slave 1 with 2 registers: 1000 (0x03E8) and 2000 (0x07D0)
            byte[] raw = new byte[]
            {
                0x01, // Unit ID
                0x03, // Function Code
                0x04, // Byte count (4 bytes = 2 registers)
                0x03, 0xE8, // 1000
                0x07, 0xD0, // 2000
                0x00, 0x00  // CRC placeholder
            };
            ushort crc = Crc16.ComputeModbus(raw, 0, 7);
            raw[7] = (byte)(crc & 0xFF);
            raw[8] = (byte)((crc >> 8) & 0xFF);

            bool ok = ModbusRtuFrame.ParseReadRegistersResponse(raw, 0, raw.Length, out byte unitId, out ushort[] registers, out var ex);

            Assert.True(ok);
            Assert.Equal(ModbusExceptionCode.None, ex);
            Assert.Equal(1, unitId);
            Assert.Equal(2, registers.Length);
            Assert.Equal(1000, registers[0]);
            Assert.Equal(2000, registers[1]);
        }

        [Fact]
        public void ModbusTcp_BuildAndParse_RoundTrip()
        {
            ushort txId = 42;
            byte unitId = 2;
            ushort startAddr = 200;
            ushort[] writeValues = new ushort[] { 111, 222, 333 };

            byte[] request = ModbusTcpFrame.CreateWriteMultipleRegistersRequest(txId, unitId, startAddr, writeValues);

            Assert.True(ModbusTcpFrame.ParseHeader(request, 0, out ushort pTx, out ushort pProto, out ushort pLen, out byte pUnit));
            Assert.Equal(txId, pTx);
            Assert.Equal(0, pProto);
            Assert.Equal(unitId, pUnit);
            Assert.Equal((ushort)(7 + 6), pLen); // FC + Addr(2) + Count(2) + ByteCount(1) + 6 data bytes
        }

        [Fact]
        public void ModbusTcp_ParseExceptionResponse_RecognizesErrorCode()
        {
            // Exception response: TxId(2B) + Proto(2B) + Len(2B) + UnitId(1B) + (0x80 | FC) + ExceptionCode
            byte[] exFrame = new byte[]
            {
                0x00, 0x05, // TxId = 5
                0x00, 0x00, // Proto = 0
                0x00, 0x03, // Len = 3
                0x01,       // UnitId = 1
                0x83,       // FC = ReadHoldingRegisters | 0x80
                0x02        // ExceptionCode = IllegalDataAddress
            };

            bool ok = ModbusTcpFrame.ParseReadRegistersResponse(
                exFrame, 0, exFrame.Length,
                out ushort txId, out byte unitId, out _, out ModbusExceptionCode ex);

            Assert.True(ok);
            Assert.Equal(5, txId);
            Assert.Equal(1, unitId);
            Assert.Equal(ModbusExceptionCode.IllegalDataAddress, ex);
        }
    }
}
