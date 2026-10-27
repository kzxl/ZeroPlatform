using System;
using System.Text;

namespace ZeroInference.Core.Format
{
    /// <summary>
    /// Lightweight zero-dependency binary protocol buffers (protobuf) wire format parser.
    /// Operates directly on byte buffers without external runtime dependencies.
    /// </summary>
    public ref struct ProtobufWireReader
    {
        private readonly ReadOnlySpan<byte> _span;
        private int _pos;

        public bool HasMore => _pos < _span.Length;
        public int Position => _pos;
        public int Length => _span.Length;

        public ProtobufWireReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _pos = 0;
        }

        public bool TryReadTag(out int fieldNumber, out int wireType)
        {
            if (!HasMore)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            ulong tag = ReadVarint();
            fieldNumber = (int)(tag >> 3);
            wireType = (int)(tag & 0x07);
            return true;
        }

        public ulong ReadVarint()
        {
            ulong result = 0;
            int shift = 0;

            while (HasMore)
            {
                byte b = _span[_pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    return result;

                shift += 7;
                if (shift >= 64)
                    throw new FormatException("Malformed protobuf varint encoding.");
            }

            throw new FormatException("Unexpected EOF while reading protobuf varint.");
        }

        public long ReadInt64() => (long)ReadVarint();
        public int ReadInt32() => (int)ReadVarint();
        public bool ReadBool() => ReadVarint() != 0;

        public float ReadFloat()
        {
            if (_pos + 4 > _span.Length)
                throw new FormatException("Unexpected EOF while reading float32.");

#if NET8_0_OR_GREATER
            float val = BitConverter.ToSingle(_span.Slice(_pos, 4));
#else
            float val = BitConverter.ToSingle(_span.Slice(_pos, 4).ToArray(), 0);
#endif
            _pos += 4;
            return val;
        }

        public double ReadDouble()
        {
            if (_pos + 8 > _span.Length)
                throw new FormatException("Unexpected EOF while reading float64.");

#if NET8_0_OR_GREATER
            double val = BitConverter.ToDouble(_span.Slice(_pos, 8));
#else
            double val = BitConverter.ToDouble(_span.Slice(_pos, 8).ToArray(), 0);
#endif
            _pos += 8;
            return val;
        }

        public ReadOnlySpan<byte> ReadLengthDelimitedSpan()
        {
            int len = (int)ReadVarint();
            if (len < 0 || _pos + len > _span.Length)
                throw new FormatException($"Invalid length-delimited field size: {len}.");

            var slice = _span.Slice(_pos, len);
            _pos += len;
            return slice;
        }

        public byte[] ReadBytes()
        {
            return ReadLengthDelimitedSpan().ToArray();
        }

        public string ReadString()
        {
            var span = ReadLengthDelimitedSpan();
            if (span.IsEmpty) return string.Empty;

#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(span);
#else
            return Encoding.UTF8.GetString(span.ToArray());
#endif
        }

        public ProtobufWireReader ReadSubReader()
        {
            var span = ReadLengthDelimitedSpan();
            return new ProtobufWireReader(span);
        }

        public void Skip(int wireType)
        {
            switch (wireType)
            {
                case 0: // Varint
                    ReadVarint();
                    break;
                case 1: // 64-bit
                    if (_pos + 8 > _span.Length) throw new FormatException("EOF on 64-bit skip.");
                    _pos += 8;
                    break;
                case 2: // Length-delimited
                    int len = (int)ReadVarint();
                    if (_pos + len > _span.Length) throw new FormatException("EOF on length-delimited skip.");
                    _pos += len;
                    break;
                case 5: // 32-bit
                    if (_pos + 4 > _span.Length) throw new FormatException("EOF on 32-bit skip.");
                    _pos += 4;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported wire type {wireType}.");
            }
        }
    }
}
