using System;
using Xunit;
using ZeroComm.Core.Abstractions;

namespace ZeroComm.Tests
{
    public class PlcAddressParserTests
    {
        #region Siemens S7 Tests

        [Theory]
        [InlineData("DB1.DBD0", 1, 0, 0, false, 4, TagDataType.Float)]
        [InlineData("DB5.DBW10", 5, 10, 0, false, 2, TagDataType.Int16)]
        [InlineData("DB2.DBX4.0", 2, 4, 0, true, 1, TagDataType.Bool)]
        [InlineData("DB2.DBX4.7", 2, 4, 7, true, 1, TagDataType.Bool)]
        [InlineData("DB10.DBB20", 10, 20, 0, false, 1, TagDataType.Byte)]
        public void Parse_Siemens_DataBlocks_Success(
            string address,
            ushort expectedDb,
            int expectedOffset,
            byte expectedBit,
            bool expectedIsBit,
            int expectedLen,
            TagDataType expectedType)
        {
            var res = PlcAddressParser.Parse(address, PlcVendor.SiemensS7);

            Assert.True(res.IsValid);
            Assert.Equal(0x84, res.AreaCode);
            Assert.Equal(expectedDb, res.DbNumber);
            Assert.Equal(expectedOffset, res.Offset);
            Assert.Equal(expectedBit, res.BitOffset);
            Assert.Equal(expectedIsBit, res.IsBitAccess);
            Assert.Equal(expectedLen, res.ElementCount);
            Assert.Equal(expectedType, res.DataType);
        }

        [Theory]
        [InlineData("M0.5", 0x83, 0, 5, true, TagDataType.Bool)]
        [InlineData("MB10", 0x83, 10, 0, false, TagDataType.Byte)]
        [InlineData("MW100", 0x83, 100, 0, false, TagDataType.Int16)]
        [InlineData("MD200", 0x83, 200, 0, false, TagDataType.Int32)]
        [InlineData("I0.0", 0x81, 0, 0, true, TagDataType.Bool)]
        [InlineData("IW64", 0x81, 64, 0, false, TagDataType.Int16)]
        [InlineData("Q0.7", 0x82, 0, 7, true, TagDataType.Bool)]
        [InlineData("QD0", 0x82, 0, 0, false, TagDataType.Int32)]
        public void Parse_Siemens_MemoryAreas_Success(
            string address,
            byte expectedArea,
            int expectedOffset,
            byte expectedBit,
            bool expectedIsBit,
            TagDataType expectedType)
        {
            var res = PlcAddressParser.Parse(address, PlcVendor.SiemensS7);

            Assert.True(res.IsValid);
            Assert.Equal(expectedArea, res.AreaCode);
            Assert.Equal(expectedOffset, res.Offset);
            Assert.Equal(expectedBit, res.BitOffset);
            Assert.Equal(expectedIsBit, res.IsBitAccess);
            Assert.Equal(expectedType, res.DataType);
        }

        #endregion

        #region Mitsubishi MELSEC Tests

        [Theory]
        [InlineData("D100", 0xA8, 100, 0, false, TagDataType.Int16)]
        [InlineData("D100.5", 0xA8, 100, 5, true, TagDataType.Bool)]
        [InlineData("W10", 0xB4, 16, 0, false, TagDataType.Int16)] // Hex 10 = Dec 16
        [InlineData("ZR1000", 0xB0, 1000, 0, false, TagDataType.Int16)]
        [InlineData("M100", 0x90, 100, 0, true, TagDataType.Bool)]
        [InlineData("X1A", 0x9C, 26, 0, true, TagDataType.Bool)]    // Hex 1A = Dec 26
        [InlineData("Y20", 0x9D, 32, 0, true, TagDataType.Bool)]    // Hex 20 = Dec 32
        public void Parse_Mitsubishi_Devices_Success(
            string address,
            byte expectedDeviceCode,
            int expectedOffset,
            byte expectedBit,
            bool expectedIsBit,
            TagDataType expectedType)
        {
            var res = PlcAddressParser.Parse(address, PlcVendor.MitsubishiMelsec);

            Assert.True(res.IsValid);
            Assert.Equal(expectedDeviceCode, res.AreaCode);
            Assert.Equal(expectedOffset, res.Offset);
            Assert.Equal(expectedBit, res.BitOffset);
            Assert.Equal(expectedIsBit, res.IsBitAccess);
            Assert.Equal(expectedType, res.DataType);
        }

        #endregion

        #region Modbus Tests

        [Theory]
        [InlineData("40001", 3, 0, 0, false, TagDataType.UInt16)]
        [InlineData("40100:F", 3, 99, 0, false, TagDataType.Float)]
        [InlineData("40100:D", 3, 99, 0, false, TagDataType.Double)]
        [InlineData("40001.3", 3, 0, 3, true, TagDataType.Bool)]
        [InlineData("30050", 4, 49, 0, false, TagDataType.UInt16)]
        [InlineData("00010", 1, 9, 0, true, TagDataType.Bool)]
        [InlineData("10005", 2, 4, 0, true, TagDataType.Bool)]
        [InlineData("HR100:F", 3, 100, 0, false, TagDataType.Float)]
        [InlineData("COIL5", 1, 5, 0, true, TagDataType.Bool)]
        public void Parse_Modbus_Addresses_Success(
            string address,
            byte expectedFc,
            int expectedOffset,
            byte expectedBit,
            bool expectedIsBit,
            TagDataType expectedType)
        {
            var res = PlcAddressParser.Parse(address, PlcVendor.ModbusTcp);

            Assert.True(res.IsValid);
            Assert.Equal(expectedFc, res.AreaCode);
            Assert.Equal(expectedOffset, res.Offset);
            Assert.Equal(expectedBit, res.BitOffset);
            Assert.Equal(expectedIsBit, res.IsBitAccess);
            Assert.Equal(expectedType, res.DataType);
        }

        #endregion

        #region Omron FINS Tests

        [Theory]
        [InlineData("D100", 0x82, 100, 0, false, TagDataType.UInt16)]
        [InlineData("D100.05", 0x02, 100, 5, true, TagDataType.Bool)]
        [InlineData("CIO50", 0xB0, 50, 0, false, TagDataType.UInt16)]
        [InlineData("CIO50.01", 0x30, 50, 1, true, TagDataType.Bool)]
        [InlineData("W10.02", 0x31, 10, 2, true, TagDataType.Bool)]
        [InlineData("H20", 0xB2, 20, 0, false, TagDataType.UInt16)]
        [InlineData("A400", 0xB3, 400, 0, false, TagDataType.UInt16)]
        [InlineData("E0_100", 0x50, 100, 0, false, TagDataType.UInt16)]
        public void Parse_Omron_Addresses_Success(
            string address,
            byte expectedArea,
            int expectedOffset,
            byte expectedBit,
            bool expectedIsBit,
            TagDataType expectedType)
        {
            var res = PlcAddressParser.Parse(address, PlcVendor.OmronFins);

            Assert.True(res.IsValid);
            Assert.Equal(expectedArea, res.AreaCode);
            Assert.Equal(expectedOffset, res.Offset);
            Assert.Equal(expectedBit, res.BitOffset);
            Assert.Equal(expectedIsBit, res.IsBitAccess);
            Assert.Equal(expectedType, res.DataType);
        }

        #endregion

        #region Allen-Bradley CIP Tests

        [Theory]
        [InlineData("Tank1_Temp")]
        [InlineData("Motor[2].Speed")]
        [InlineData("Conveyor.Running")]
        public void Parse_AllenBradley_SymbolicTags_Success(string tagName)
        {
            var res = PlcAddressParser.Parse(tagName, PlcVendor.AllenBradleyCip);

            Assert.True(res.IsValid);
            Assert.Equal(PlcVendor.AllenBradleyCip, res.Vendor);
            Assert.Equal(tagName, res.SymbolicPath);
        }

        #endregion
    }
}
