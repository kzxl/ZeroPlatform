using System;
using System.IO;

namespace ZeroGraphics.Vector.Text
{
    /// <summary>
    /// Big-Endian binary reader for OpenType and TrueType font data tables.
    /// </summary>
    public sealed class TrueTypeReader
    {
        private readonly byte[] _data;
        private int _position;

        public int Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _data.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public int Length => _data.Length;

        public TrueTypeReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _position = 0;
        }

        public byte ReadByte()
        {
            if (_position >= _data.Length) throw new EndOfStreamException();
            return _data[_position++];
        }

        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public byte[] ReadBytes(int count)
        {
            if (_position + count > _data.Length) throw new EndOfStreamException();
            var bytes = new byte[count];
            Buffer.BlockCopy(_data, _position, bytes, 0, count);
            _position += count;
            return bytes;
        }

        public ushort ReadUInt16()
        {
            if (_position + 2 > _data.Length) throw new EndOfStreamException();
            ushort val = (ushort)((_data[_position] << 8) | _data[_position + 1]);
            _position += 2;
            return val;
        }

        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        public uint ReadUInt32()
        {
            if (_position + 4 > _data.Length) throw new EndOfStreamException();
            uint val = ((uint)_data[_position] << 24) |
                       ((uint)_data[_position + 1] << 16) |
                       ((uint)_data[_position + 2] << 8) |
                       _data[_position + 3];
            _position += 4;
            return val;
        }

        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        public float ReadFixed()
        {
            int raw = ReadInt32();
            return raw / 65536.0f;
        }

        public float ReadF2Dot14()
        {
            short raw = ReadInt16();
            return raw / 16384.0f;
        }

        public void Skip(int bytes)
        {
            Position += bytes;
        }

        public uint ReadTag()
        {
            return ReadUInt32();
        }

        public static string TagToString(uint tag)
        {
            char c0 = (char)((tag >> 24) & 0xFF);
            char c1 = (char)((tag >> 16) & 0xFF);
            char c2 = (char)((tag >> 8) & 0xFF);
            char c3 = (char)(tag & 0xFF);
            return new string(new[] { c0, c1, c2, c3 });
        }

        public static uint StringToTag(string s)
        {
            if (s.Length != 4) throw new ArgumentException("Tag must be exactly 4 characters.");
            return ((uint)s[0] << 24) | ((uint)s[1] << 16) | ((uint)s[2] << 8) | (uint)s[3];
        }
    }
}
