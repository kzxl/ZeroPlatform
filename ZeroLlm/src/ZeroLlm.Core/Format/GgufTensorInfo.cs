using System;

namespace ZeroLlm.Core.Format
{
    /// <summary>
    /// Metadata descriptor for a tensor stored inside a GGUF model file.
    /// </summary>
    public sealed class GgufTensorInfo
    {
        public string Name { get; }
        public ulong[] Dimensions { get; }
        public GgufTensorType Type { get; }
        public ulong Offset { get; }

        public GgufTensorInfo(string name, ulong[] dimensions, GgufTensorType type, ulong offset)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
            Type = type;
            Offset = offset;
        }

        /// <summary>
        /// Total number of elements across all dimensions.
        /// </summary>
        public ulong ElementCount
        {
            get
            {
                if (Dimensions.Length == 0) return 0;
                ulong count = 1;
                for (int i = 0; i < Dimensions.Length; i++)
                {
                    count *= Dimensions[i];
                }
                return count;
            }
        }

        /// <summary>
        /// Calculates the total memory size in bytes of the tensor payload.
        /// </summary>
        public ulong GetSizeInBytes()
        {
            ulong elements = ElementCount;
            switch (Type)
            {
                case GgufTensorType.F32:
                case GgufTensorType.I32:
                    return elements * 4;
                case GgufTensorType.F16:
                case GgufTensorType.BF16:
                case GgufTensorType.I16:
                    return elements * 2;
                case GgufTensorType.F64:
                case GgufTensorType.I64:
                    return elements * 8;
                case GgufTensorType.I8:
                    return elements;
                case GgufTensorType.Q8_0:
                    return (elements / 32) * 34;
                case GgufTensorType.Q4_0:
                    return (elements / 32) * 18;
                default:
                    // Fallback to F32 byte size
                    return elements * 4;
            }
        }

        public override string ToString() =>
            $"{Name} [{string.Join("x", Dimensions)}] ({Type}, offset: {Offset})";
    }
}
