using System;
using System.IO;
using System.Text;

namespace ZeroIoT.OpcUa
{
    /// <summary>
    /// Pure C# high-speed binary encoder for OPC-UA primitives, NodeIds, Variants, and UA-TCP messages.
    /// </summary>
    public class UaBinaryEncoder
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        public UaBinaryEncoder()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream);
        }

        public UaBinaryEncoder(int capacity)
        {
            _stream = new MemoryStream(capacity);
            _writer = new BinaryWriter(_stream);
        }

        public int Position => (int)_stream.Position;

        public void WriteBoolean(bool val) => _writer.Write(val);
        public void WriteSByte(sbyte val) => _writer.Write(val);
        public void WriteByte(byte val) => _writer.Write(val);
        public void WriteInt16(short val) => _writer.Write(val);
        public void WriteUInt16(ushort val) => _writer.Write(val);
        public void WriteInt32(int val) => _writer.Write(val);
        public void WriteUInt32(uint val) => _writer.Write(val);
        public void WriteInt64(long val) => _writer.Write(val);
        public void WriteUInt64(ulong val) => _writer.Write(val);
        public void WriteFloat(float val) => _writer.Write(val);
        public void WriteDouble(double val) => _writer.Write(val);

        public void WriteString(string? str)
        {
            if (str == null)
            {
                WriteInt32(-1);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(str);
            WriteInt32(bytes.Length);
            _writer.Write(bytes);
        }

        public void WriteByteString(byte[]? bytes)
        {
            if (bytes == null)
            {
                WriteInt32(-1);
                return;
            }

            WriteInt32(bytes.Length);
            _writer.Write(bytes);
        }

        public void WriteDateTime(DateTime dt)
        {
            long fileTime = dt.ToUniversalTime().ToFileTimeUtc();
            WriteInt64(fileTime);
        }

        public void WriteGuid(Guid guid)
        {
            _writer.Write(guid.ToByteArray());
        }

        public void WriteNodeId(NodeId nodeId)
        {
            switch (nodeId.IdentifierType)
            {
                case NodeIdType.TwoByte:
                    WriteByte(0x00);
                    WriteByte((byte)nodeId.NumericIdentifier);
                    break;
                case NodeIdType.FourByte:
                    WriteByte(0x01);
                    WriteByte((byte)nodeId.NamespaceIndex);
                    WriteUInt16((ushort)nodeId.NumericIdentifier);
                    break;
                case NodeIdType.Numeric:
                    WriteByte(0x02);
                    WriteUInt16(nodeId.NamespaceIndex);
                    WriteUInt32(nodeId.NumericIdentifier);
                    break;
                case NodeIdType.String:
                    WriteByte(0x03);
                    WriteUInt16(nodeId.NamespaceIndex);
                    WriteString(nodeId.StringIdentifier);
                    break;
                default:
                    WriteByte(0x02);
                    WriteUInt16(nodeId.NamespaceIndex);
                    WriteUInt32(nodeId.NumericIdentifier);
                    break;
            }
        }

        public void WriteVariant(UaVariant variant)
        {
            if (variant.Type == UaBuiltInType.Null || variant.Value == null)
            {
                WriteByte(0x00);
                return;
            }

            byte mask = (byte)variant.Type;
            if (variant.IsArray)
            {
                mask |= 0x80;
                WriteByte(mask);

                if (variant.Value is Array arr)
                {
                    WriteInt32(arr.Length);
                    for (int i = 0; i < arr.Length; i++)
                    {
                        WriteVariantElement(variant.Type, arr.GetValue(i));
                    }
                }
                else
                {
                    WriteInt32(0);
                }
            }
            else
            {
                WriteByte(mask);
                WriteVariantElement(variant.Type, variant.Value);
            }
        }

        private void WriteVariantElement(UaBuiltInType type, object? val)
        {
            if (val == null) return;
            switch (type)
            {
                case UaBuiltInType.Boolean: WriteBoolean((bool)val); break;
                case UaBuiltInType.SByte: WriteSByte((sbyte)val); break;
                case UaBuiltInType.Byte: WriteByte((byte)val); break;
                case UaBuiltInType.Int16: WriteInt16((short)val); break;
                case UaBuiltInType.UInt16: WriteUInt16((ushort)val); break;
                case UaBuiltInType.Int32: WriteInt32((int)val); break;
                case UaBuiltInType.UInt32: WriteUInt32((uint)val); break;
                case UaBuiltInType.Int64: WriteInt64((long)val); break;
                case UaBuiltInType.UInt64: WriteUInt64((ulong)val); break;
                case UaBuiltInType.Float: WriteFloat((float)val); break;
                case UaBuiltInType.Double: WriteDouble((double)val); break;
                case UaBuiltInType.String: WriteString((string)val); break;
                case UaBuiltInType.DateTime: WriteDateTime((DateTime)val); break;
                case UaBuiltInType.Guid: WriteGuid((Guid)val); break;
                case UaBuiltInType.ByteString: WriteByteString((byte[])val); break;
                case UaBuiltInType.NodeId: WriteNodeId((NodeId)val); break;
                default:
                    WriteString(val.ToString());
                    break;
            }
        }

        public byte[] ToArray() => _stream.ToArray();

        /// <summary>
        /// Encodes a complete UA-TCP Hello (HEL) message.
        /// </summary>
        public static byte[] EncodeHello(string endpointUrl, uint receiveBufferSize = 65536, uint sendBufferSize = 65536,
            uint maxMessageSize = 16777216, uint maxChunkCount = 5000)
        {
            var enc = new UaBinaryEncoder();
            // Header: HELF + length (placeholder)
            enc.WriteByte((byte)'H');
            enc.WriteByte((byte)'E');
            enc.WriteByte((byte)'L');
            enc.WriteByte((byte)'F');
            enc.WriteUInt32(0); // placeholder for messageSize

            enc.WriteUInt32(0); // ProtocolVersion
            enc.WriteUInt32(receiveBufferSize);
            enc.WriteUInt32(sendBufferSize);
            enc.WriteUInt32(maxMessageSize);
            enc.WriteUInt32(maxChunkCount);
            enc.WriteString(endpointUrl);

            byte[] bytes = enc.ToArray();
            uint totalSize = (uint)bytes.Length;
            bytes[4] = (byte)(totalSize & 0xFF);
            bytes[5] = (byte)((totalSize >> 8) & 0xFF);
            bytes[6] = (byte)((totalSize >> 16) & 0xFF);
            bytes[7] = (byte)((totalSize >> 24) & 0xFF);
            return bytes;
        }

        /// <summary>
        /// Encodes a complete UA-TCP Acknowledge (ACK) message.
        /// </summary>
        public static byte[] EncodeAcknowledge(uint protocolVersion = 0, uint receiveBufferSize = 65536,
            uint sendBufferSize = 65536, uint maxMessageSize = 16777216, uint maxChunkCount = 5000)
        {
            var enc = new UaBinaryEncoder();
            enc.WriteByte((byte)'A');
            enc.WriteByte((byte)'C');
            enc.WriteByte((byte)'K');
            enc.WriteByte((byte)'F');
            enc.WriteUInt32(32); // 8 header + 24 payload

            enc.WriteUInt32(protocolVersion);
            enc.WriteUInt32(receiveBufferSize);
            enc.WriteUInt32(sendBufferSize);
            enc.WriteUInt32(maxMessageSize);
            enc.WriteUInt32(maxChunkCount);
            return enc.ToArray();
        }
    }
}
