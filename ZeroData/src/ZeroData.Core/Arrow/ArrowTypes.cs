using System;

namespace ZeroData.Core.Arrow
{
    /// <summary>
    /// Supported Apache Arrow logical data types in ZeroData.
    /// </summary>
    public enum ArrowTypeId : byte
    {
        Null = 0,
        Int32 = 1,
        Int64 = 2,
        Float = 3,
        Double = 4,
        Utf8 = 5,
        Boolean = 6,
        Date64 = 7,
        Decimal128 = 8,
        Timestamp = 9
    }

    /// <summary>
    /// Represents a field in an Apache Arrow Schema.
    /// </summary>
    public sealed class ArrowField
    {
        public string Name { get; }
        public ArrowTypeId TypeId { get; }
        public bool IsNullable { get; }

        public ArrowField(string name, ArrowTypeId typeId, bool isNullable = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeId = typeId;
            IsNullable = isNullable;
        }
    }
}
