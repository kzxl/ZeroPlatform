using System;

namespace ZeroIoT.OpcUa
{
    public enum UaBuiltInType : byte
    {
        Null = 0,
        Boolean = 1,
        SByte = 2,
        Byte = 3,
        Int16 = 4,
        UInt16 = 5,
        Int32 = 6,
        UInt32 = 7,
        Int64 = 8,
        UInt64 = 9,
        Float = 10,
        Double = 11,
        String = 12,
        DateTime = 13,
        Guid = 14,
        ByteString = 15,
        XmlElement = 16,
        NodeId = 17,
        ExpandedNodeId = 18,
        StatusCode = 19,
        QualifiedName = 20,
        LocalizedText = 21,
        ExtensionObject = 22,
        DataValue = 23,
        Variant = 24,
        DiagnosticInfo = 25
    }

    /// <summary>
    /// Type-safe, memory-efficient OPC-UA Variant container.
    /// </summary>
    public readonly struct UaVariant
    {
        public UaBuiltInType Type { get; }
        public object? Value { get; }
        public bool IsArray { get; }

        public static readonly UaVariant Null = new UaVariant(null, UaBuiltInType.Null);

        public UaVariant(object? value, UaBuiltInType type, bool isArray = false)
        {
            Value = value;
            Type = type;
            IsArray = isArray;
        }

        public static UaVariant FromBoolean(bool val) => new UaVariant(val, UaBuiltInType.Boolean);
        public static UaVariant FromInt16(short val) => new UaVariant(val, UaBuiltInType.Int16);
        public static UaVariant FromUInt16(ushort val) => new UaVariant(val, UaBuiltInType.UInt16);
        public static UaVariant FromInt32(int val) => new UaVariant(val, UaBuiltInType.Int32);
        public static UaVariant FromUInt32(uint val) => new UaVariant(val, UaBuiltInType.UInt32);
        public static UaVariant FromInt64(long val) => new UaVariant(val, UaBuiltInType.Int64);
        public static UaVariant FromUInt64(ulong val) => new UaVariant(val, UaBuiltInType.UInt64);
        public static UaVariant FromFloat(float val) => new UaVariant(val, UaBuiltInType.Float);
        public static UaVariant FromDouble(double val) => new UaVariant(val, UaBuiltInType.Double);
        public static UaVariant FromString(string val) => new UaVariant(val, UaBuiltInType.String);
        public static UaVariant FromDateTime(DateTime val) => new UaVariant(val, UaBuiltInType.DateTime);
        public static UaVariant FromByteString(byte[] val) => new UaVariant(val, UaBuiltInType.ByteString);
        public static UaVariant FromNodeId(NodeId val) => new UaVariant(val, UaBuiltInType.NodeId);

        public double ToDouble()
        {
            if (Value == null) return 0.0;
            switch (Type)
            {
                case UaBuiltInType.Double: return (double)Value;
                case UaBuiltInType.Float: return (float)Value;
                case UaBuiltInType.Int32: return (int)Value;
                case UaBuiltInType.UInt32: return (uint)Value;
                case UaBuiltInType.Int16: return (short)Value;
                case UaBuiltInType.UInt16: return (ushort)Value;
                case UaBuiltInType.Int64: return (long)Value;
                case UaBuiltInType.UInt64: return (ulong)Value;
                case UaBuiltInType.Boolean: return (bool)Value ? 1.0 : 0.0;
                case UaBuiltInType.Byte: return (byte)Value;
                case UaBuiltInType.SByte: return (sbyte)Value;
                case UaBuiltInType.String:
                    return double.TryParse((string)Value, out double d) ? d : 0.0;
                default:
                    return 0.0;
            }
        }

        public override string ToString()
        {
            if (Value == null) return "null";
            return Value.ToString() ?? "";
        }
    }

    /// <summary>
    /// Represents an OPC-UA DataValue with value, status code, and timestamp metadata.
    /// </summary>
    public class UaDataValue
    {
        public UaVariant Value { get; set; }
        public uint StatusCode { get; set; }
        public DateTime? SourceTimestamp { get; set; }
        public DateTime? ServerTimestamp { get; set; }

        public bool IsGood => (StatusCode & 0xC0000000) == 0;

        public UaDataValue(UaVariant value, uint statusCode = 0, DateTime? sourceTimestamp = null)
        {
            Value = value;
            StatusCode = statusCode;
            SourceTimestamp = sourceTimestamp ?? DateTime.UtcNow;
            ServerTimestamp = DateTime.UtcNow;
        }
    }
}
