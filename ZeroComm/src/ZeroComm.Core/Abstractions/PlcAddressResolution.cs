namespace ZeroComm.Core.Abstractions
{
    /// <summary>
    /// Compiled, immutable descriptor of a parsed PLC tag address.
    /// Provides zero-allocation access to decoded memory area, offset, bit, and data type.
    /// </summary>
    public readonly struct PlcAddressResolution
    {
        public bool IsValid { get; }
        public PlcVendor Vendor { get; }
        public string RawAddress { get; }
        public byte AreaCode { get; }
        public ushort DbNumber { get; }       // Siemens DB number (1..65535)
        public int Offset { get; }           // Byte offset or word offset
        public byte BitOffset { get; }       // Bit index (0..15)
        public bool IsBitAccess { get; }
        public int ElementCount { get; }
        public TagDataType DataType { get; }
        public string? SymbolicPath { get; }  // Symbolic tag name for CIP/ADS
        public string? ErrorMessage { get; }

        public PlcAddressResolution(
            bool isValid,
            PlcVendor vendor,
            string rawAddress,
            byte areaCode,
            ushort dbNumber,
            int offset,
            byte bitOffset,
            bool isBitAccess,
            int elementCount,
            TagDataType dataType,
            string? symbolicPath = null,
            string? errorMessage = null)
        {
            IsValid = isValid;
            Vendor = vendor;
            RawAddress = rawAddress;
            AreaCode = areaCode;
            DbNumber = dbNumber;
            Offset = offset;
            BitOffset = bitOffset;
            IsBitAccess = isBitAccess;
            ElementCount = elementCount;
            DataType = dataType;
            SymbolicPath = symbolicPath;
            ErrorMessage = errorMessage;
        }

        public static PlcAddressResolution Success(
            PlcVendor vendor,
            string rawAddress,
            byte areaCode,
            ushort dbNumber,
            int offset,
            byte bitOffset,
            bool isBitAccess,
            int elementCount,
            TagDataType dataType,
            string? symbolicPath = null) =>
            new PlcAddressResolution(true, vendor, rawAddress, areaCode, dbNumber, offset, bitOffset, isBitAccess, elementCount, dataType, symbolicPath, null);

        public static PlcAddressResolution Failed(PlcVendor vendor, string rawAddress, string errorMessage) =>
            new PlcAddressResolution(false, vendor, rawAddress, 0, 0, 0, 0, false, 0, TagDataType.RawBytes, null, errorMessage);

        public override string ToString() =>
            IsValid ? $"{Vendor}:{RawAddress} (Area:0x{AreaCode:X2}, DB:{DbNumber}, Off:{Offset}, Bit:{BitOffset}, Type:{DataType})" : $"Invalid({Vendor}:{RawAddress} - {ErrorMessage})";
    }

    /// <summary>
    /// Represents a discrete tag read request in a multi-variable batch read operation.
    /// </summary>
    public readonly struct PlcBatchReadRequest
    {
        public int TagId { get; }
        public PlcAddressResolution Address { get; }
        public int ByteLength { get; }

        public PlcBatchReadRequest(int tagId, in PlcAddressResolution address, int byteLength)
        {
            TagId = tagId;
            Address = address;
            ByteLength = byteLength;
        }
    }

    /// <summary>
    /// Represents a decoded tag telemetry sample returned from a PLC batch read.
    /// </summary>
    public readonly struct PlcTagValue
    {
        public int TagId { get; }
        public double NumericValue { get; }
        public bool BooleanValue { get; }
        public ScadaQuality Quality { get; }
        public long TimestampUtcMs { get; }

        public PlcTagValue(int tagId, double numericValue, bool booleanValue, ScadaQuality quality, long timestampUtcMs)
        {
            TagId = tagId;
            NumericValue = numericValue;
            BooleanValue = booleanValue;
            Quality = quality;
            TimestampUtcMs = timestampUtcMs;
        }
    }
}
