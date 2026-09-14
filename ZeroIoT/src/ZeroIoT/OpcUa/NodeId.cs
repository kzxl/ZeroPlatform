using System;

namespace ZeroIoT.OpcUa
{
    /// <summary>
    /// OPC-UA NodeId identifier type.
    /// </summary>
    public enum NodeIdType
    {
        TwoByte = 0x00,
        FourByte = 0x01,
        Numeric = 0x02,
        String = 0x03,
        Guid = 0x04,
        ByteString = 0x05
    }

    /// <summary>
    /// Represents an OPC-UA NodeId across any namespace.
    /// </summary>
    public readonly struct NodeId : IEquatable<NodeId>
    {
        public ushort NamespaceIndex { get; }
        public NodeIdType IdentifierType { get; }
        public uint NumericIdentifier { get; }
        public string? StringIdentifier { get; }

        public NodeId(uint numericId, ushort ns = 0)
        {
            NamespaceIndex = ns;
            NumericIdentifier = numericId;
            StringIdentifier = null;
            if (ns == 0 && numericId <= 255)
            {
                IdentifierType = NodeIdType.TwoByte;
            }
            else if (ns <= 255 && numericId <= 65535)
            {
                IdentifierType = NodeIdType.FourByte;
            }
            else
            {
                IdentifierType = NodeIdType.Numeric;
            }
        }

        public NodeId(string stringId, ushort ns = 1)
        {
            NamespaceIndex = ns;
            NumericIdentifier = 0;
            StringIdentifier = stringId ?? throw new ArgumentNullException(nameof(stringId));
            IdentifierType = NodeIdType.String;
        }

        public static NodeId FromString(string formattedNodeId)
        {
            if (string.IsNullOrEmpty(formattedNodeId))
                throw new ArgumentNullException(nameof(formattedNodeId));

            ushort ns = 0;
            string idPart = formattedNodeId;

            if (formattedNodeId.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
            {
                int semiIndex = formattedNodeId.IndexOf(';');
                if (semiIndex > 3)
                {
                    string nsStr = formattedNodeId.Substring(3, semiIndex - 3);
                    ushort.TryParse(nsStr, out ns);
                    idPart = formattedNodeId.Substring(semiIndex + 1);
                }
            }

            if (idPart.StartsWith("s=", StringComparison.OrdinalIgnoreCase))
            {
                return new NodeId(idPart.Substring(2), ns);
            }
            else if (idPart.StartsWith("i=", StringComparison.OrdinalIgnoreCase))
            {
                uint.TryParse(idPart.Substring(2), out uint num);
                return new NodeId(num, ns);
            }
            else if (uint.TryParse(idPart, out uint numOnly))
            {
                return new NodeId(numOnly, ns);
            }

            return new NodeId(idPart, ns);
        }

        public override string ToString()
        {
            string prefix = NamespaceIndex > 0 ? $"ns={NamespaceIndex};" : "";
            if (IdentifierType == NodeIdType.String)
                return $"{prefix}s={StringIdentifier}";
            return $"{prefix}i={NumericIdentifier}";
        }

        public bool Equals(NodeId other)
        {
            if (NamespaceIndex != other.NamespaceIndex || IdentifierType != other.IdentifierType)
                return false;

            if (IdentifierType == NodeIdType.String)
                return string.Equals(StringIdentifier, other.StringIdentifier, StringComparison.Ordinal);

            return NumericIdentifier == other.NumericIdentifier;
        }

        public override bool Equals(object? obj) => obj is NodeId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (NamespaceIndex.GetHashCode() * 397) ^ IdentifierType.GetHashCode();
                if (IdentifierType == NodeIdType.String && StringIdentifier != null)
                    hash = (hash * 397) ^ StringIdentifier.GetHashCode();
                else
                    hash = (hash * 397) ^ NumericIdentifier.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);
        public static bool operator !=(NodeId left, NodeId right) => !left.Equals(right);
    }
}
