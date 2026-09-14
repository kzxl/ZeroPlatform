using System;
using System.Text;

namespace ZeroIoT.OpcUa
{
    /// <summary>
    /// Parsed UA-TCP message header.
    /// </summary>
    public readonly struct UaTcpHeader
    {
        public string MessageType { get; }
        public char ChunkType { get; }
        public uint MessageSize { get; }

        public UaTcpHeader(string messageType, char chunkType, uint messageSize)
        {
            MessageType = messageType;
            ChunkType = chunkType;
            MessageSize = messageSize;
        }
    }

    /// <summary>
    /// Pure C# high-speed, zero-allocation binary decoder for OPC-UA frames and primitives.
    /// </summary>
    public ref struct UaBinaryDecoder
    {
        private ReadOnlySpan<byte> _buffer;
        private int _position;

        public UaBinaryDecoder(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Position => _position;
        public int Remaining => _buffer.Length - _position;

        public bool ReadBoolean()
        {
            Ensure(1);
            return _buffer[_position++] != 0;
        }

        public sbyte ReadSByte()
        {
            Ensure(1);
            return (sbyte)_buffer[_position++];
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _buffer[_position++];
        }

        public short ReadInt16()
        {
            Ensure(2);
            short val = (short)(_buffer[_position] | (_buffer[_position + 1] << 8));
            _position += 2;
            return val;
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            ushort val = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
            _position += 2;
            return val;
        }

        public int ReadInt32()
        {
            Ensure(4);
            int val = _buffer[_position] |
                     (_buffer[_position + 1] << 8) |
                     (_buffer[_position + 2] << 16) |
                     (_buffer[_position + 3] << 24);
            _position += 4;
            return val;
        }

        public uint ReadUInt32()
        {
            Ensure(4);
            uint val = (uint)(_buffer[_position] |
                             (_buffer[_position + 1] << 8) |
                             (_buffer[_position + 2] << 16) |
                             (_buffer[_position + 3] << 24));
            _position += 4;
            return val;
        }

        public long ReadInt64()
        {
            Ensure(8);
            uint low = ReadUInt32();
            uint high = ReadUInt32();
            return (long)(((ulong)high << 32) | low);
        }

        public ulong ReadUInt64()
        {
            Ensure(8);
            uint low = ReadUInt32();
            uint high = ReadUInt32();
            return ((ulong)high << 32) | low;
        }

        public float ReadFloat()
        {
            Ensure(4);
            int intVal = ReadInt32();
            unsafe
            {
                return *(float*)&intVal;
            }
        }

        public double ReadDouble()
        {
            Ensure(8);
            long longVal = ReadInt64();
            unsafe
            {
                return *(double*)&longVal;
            }
        }

        public string? ReadString()
        {
            int length = ReadInt32();
            if (length < 0) return null;
            if (length == 0) return string.Empty;

            Ensure(length);
            string str = Encoding.UTF8.GetString(_buffer.Slice(_position, length).ToArray());
            _position += length;
            return str;
        }

        public byte[]? ReadByteString()
        {
            int length = ReadInt32();
            if (length < 0) return null;
            if (length == 0) return Array.Empty<byte>();

            Ensure(length);
            byte[] bytes = _buffer.Slice(_position, length).ToArray();
            _position += length;
            return bytes;
        }

        public DateTime ReadDateTime()
        {
            long fileTime = ReadInt64();
            if (fileTime <= 0) return DateTime.MinValue;
            if (fileTime >= 2650467743999999999L) return DateTime.MaxValue;
            return DateTime.FromFileTimeUtc(fileTime);
        }

        public Guid ReadGuid()
        {
            Ensure(16);
            byte[] guidBytes = _buffer.Slice(_position, 16).ToArray();
            _position += 16;
            return new Guid(guidBytes);
        }

        public NodeId ReadNodeId()
        {
            byte typeByte = ReadByte();
            switch (typeByte)
            {
                case 0x00: // TwoByte
                    return new NodeId(ReadByte(), 0);
                case 0x01: // FourByte
                    byte ns = ReadByte();
                    ushort id16 = ReadUInt16();
                    return new NodeId(id16, ns);
                case 0x02: // Numeric
                    ushort nsNum = ReadUInt16();
                    uint id32 = ReadUInt32();
                    return new NodeId(id32, nsNum);
                case 0x03: // String
                    ushort nsStr = ReadUInt16();
                    string strId = ReadString() ?? string.Empty;
                    return new NodeId(strId, nsStr);
                default:
                    return new NodeId(0, 0);
            }
        }

        public UaVariant ReadVariant()
        {
            byte mask = ReadByte();
            if (mask == 0) return UaVariant.Null;

            UaBuiltInType type = (UaBuiltInType)(mask & 0x3F);
            bool isArray = (mask & 0x80) != 0;

            if (isArray)
            {
                int length = ReadInt32();
                if (length <= 0) return new UaVariant(Array.Empty<object>(), type, true);

                object[] array = new object[length];
                for (int i = 0; i < length; i++)
                {
                    array[i] = ReadScalarValue(type)!;
                }
                return new UaVariant(array, type, true);
            }

            object? scalar = ReadScalarValue(type);
            return new UaVariant(scalar, type, false);
        }

        private object? ReadScalarValue(UaBuiltInType type)
        {
            switch (type)
            {
                case UaBuiltInType.Boolean: return ReadBoolean();
                case UaBuiltInType.SByte: return ReadSByte();
                case UaBuiltInType.Byte: return ReadByte();
                case UaBuiltInType.Int16: return ReadInt16();
                case UaBuiltInType.UInt16: return ReadUInt16();
                case UaBuiltInType.Int32: return ReadInt32();
                case UaBuiltInType.UInt32: return ReadUInt32();
                case UaBuiltInType.Int64: return ReadInt64();
                case UaBuiltInType.UInt64: return ReadUInt64();
                case UaBuiltInType.Float: return ReadFloat();
                case UaBuiltInType.Double: return ReadDouble();
                case UaBuiltInType.String: return ReadString();
                case UaBuiltInType.DateTime: return ReadDateTime();
                case UaBuiltInType.Guid: return ReadGuid();
                case UaBuiltInType.ByteString: return ReadByteString();
                case UaBuiltInType.NodeId: return ReadNodeId();
                default: return ReadString();
            }
        }

        public UaDataValue ReadDataValue()
        {
            byte mask = ReadByte();
            UaVariant val = UaVariant.Null;
            uint statusCode = 0;
            DateTime? sourceTime = null;

            if ((mask & 0x01) != 0) val = ReadVariant();
            if ((mask & 0x02) != 0) statusCode = ReadUInt32();
            if ((mask & 0x04) != 0) sourceTime = ReadDateTime();
            if ((mask & 0x08) != 0) ReadDateTime(); // skip server timestamp
            if ((mask & 0x10) != 0) ReadUInt16();   // skip source picoseconds
            if ((mask & 0x20) != 0) ReadUInt16();   // skip server picoseconds

            return new UaDataValue(val, statusCode, sourceTime);
        }

        /// <summary>
        /// Reads an 8-byte UA-TCP header.
        /// </summary>
        public static bool TryReadHeader(ReadOnlySpan<byte> buffer, out UaTcpHeader header)
        {
            header = default;
            if (buffer.Length < 8) return false;

            string msgType = Encoding.ASCII.GetString(buffer.Slice(0, 3).ToArray());
            char chunkType = (char)buffer[3];
            uint msgSize = (uint)(buffer[4] | (buffer[5] << 8) | (buffer[6] << 16) | (buffer[7] << 24));

            header = new UaTcpHeader(msgType, chunkType, msgSize);
            return true;
        }

        private void Ensure(int count)
        {
            if (_position + count > _buffer.Length)
                throw new IndexOutOfRangeException($"Insufficient buffer bytes for OPC-UA read: needed {count}, available {_buffer.Length - _position}");
        }
    }
}
