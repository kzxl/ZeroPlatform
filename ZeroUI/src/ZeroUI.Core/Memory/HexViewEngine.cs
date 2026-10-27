using System;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace ZeroUI.Core.Memory
{
    public class HexByteSegment
    {
        public int Offset { get; set; }
        public int Length { get; set; }
        public string Title { get; set; } = string.Empty;
        public uint ColorArgb { get; set; } = 0xFF38BDF8;

        public HexByteSegment(int offset, int length, string title, uint color)
        {
            Offset = offset;
            Length = length;
            Title = title;
            ColorArgb = color;
        }

        public bool Contains(int byteOffset)
        {
            return byteOffset >= Offset && byteOffset < Offset + Length;
        }
    }

    /// <summary>
    /// Industrial protocol dissector for decomposing raw OT packet buffers (Modbus, S7, CAN) into color-coded fields.
    /// </summary>
    public class ProtocolDissector
    {
        public List<HexByteSegment> Segments { get; } = new List<HexByteSegment>();

        public HexByteSegment? FindSegment(int byteOffset)
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i].Contains(byteOffset))
                    return Segments[i];
            }
            return null;
        }

        /// <summary>
        /// Dissects a standard Modbus TCP frame (MBAP Header + PDU).
        /// </summary>
        public void DissectModbusTcp(ReadOnlySpan<byte> frame)
        {
            Segments.Clear();
            if (frame.Length < 7) return;

            // MBAP Header:
            // Bytes 0-1: Transaction Identifier
            Segments.Add(new HexByteSegment(0, 2, "Transaction ID", 0xFF3B82F6)); // Blue
            // Bytes 2-3: Protocol Identifier (0 = Modbus)
            Segments.Add(new HexByteSegment(2, 2, "Protocol ID", 0xFF6366F1)); // Indigo
            // Bytes 4-5: Length
            Segments.Add(new HexByteSegment(4, 2, "Length", 0xFF8B5CF6)); // Violet
            // Byte 6: Unit ID
            Segments.Add(new HexByteSegment(6, 1, "Unit ID", 0xFFA855F7)); // Purple

            if (frame.Length > 7)
            {
                // Byte 7: Function Code
                Segments.Add(new HexByteSegment(7, 1, "Function Code", 0xFFF59E0B)); // Amber
            }

            if (frame.Length > 8)
            {
                // Remaining: Data Payload
                Segments.Add(new HexByteSegment(8, frame.Length - 8, "Data Payload", 0xFF10B981)); // Emerald
            }
        }

        /// <summary>
        /// Calculates CRC-16 (Modbus polynomial 0xA001).
        /// </summary>
        public static ushort CalculateCrc16Modbus(ReadOnlySpan<byte> buffer)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < buffer.Length; pos++)
            {
                crc ^= (ushort)buffer[pos];
                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }

    /// <summary>
    /// High-speed zero-copy engine for viewing, formatting, and inspecting large binary buffers.
    /// </summary>
    public class HexViewEngine
    {
        private ReadOnlyMemory<byte> _buffer;
        private int _bytesPerRow = 16;
        private readonly ProtocolDissector _dissector = new ProtocolDissector();

        public ReadOnlyMemory<byte> Buffer
        {
            get => _buffer;
            set => _buffer = value;
        }

        public int BytesPerRow
        {
            get => _bytesPerRow;
            set => _bytesPerRow = Math.Max(8, Math.Min(64, value));
        }

        public ProtocolDissector Dissector => _dissector;

        public int TotalBytes => _buffer.Length;
        public int TotalRows => _buffer.Length == 0 ? 0 : (_buffer.Length + _bytesPerRow - 1) / _bytesPerRow;

        public HexViewEngine(ReadOnlyMemory<byte> buffer, int bytesPerRow = 16)
        {
            _buffer = buffer;
            _bytesPerRow = bytesPerRow;
        }

        /// <summary>
        /// Extracts the bytes for a specific row without heap allocation.
        /// </summary>
        public bool TryGetRow(int rowIndex, out int offset, out ReadOnlySpan<byte> rowBytes)
        {
            offset = rowIndex * _bytesPerRow;
            if (offset < 0 || offset >= _buffer.Length)
            {
                rowBytes = ReadOnlySpan<byte>.Empty;
                return false;
            }

            int count = Math.Min(_bytesPerRow, _buffer.Length - offset);
            rowBytes = _buffer.Span.Slice(offset, count);
            return true;
        }

        /// <summary>
        /// Inspects the value at the given offset as various data primitives.
        /// </summary>
        public void InspectOffset(int offset, bool isLittleEndian,
            out byte u8, out short s16, out ushort u16, out int s32, out uint u32,
            out float f32, out double f64, out string bitString)
        {
            var span = _buffer.Span;
            u8 = (offset >= 0 && offset < span.Length) ? span[offset] : (byte)0;

            // 16-bit
            if (offset >= 0 && offset + 2 <= span.Length)
            {
                var s = span.Slice(offset, 2);
                s16 = isLittleEndian ? BinaryPrimitives.ReadInt16LittleEndian(s) : BinaryPrimitives.ReadInt16BigEndian(s);
                u16 = isLittleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);
            }
            else
            {
                s16 = 0;
                u16 = 0;
            }

            // 32-bit
            if (offset >= 0 && offset + 4 <= span.Length)
            {
                var s = span.Slice(offset, 4);
                s32 = isLittleEndian ? BinaryPrimitives.ReadInt32LittleEndian(s) : BinaryPrimitives.ReadInt32BigEndian(s);
                u32 = isLittleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
                f32 = isLittleEndian
#if NET8_0_OR_GREATER
                    ? BinaryPrimitives.ReadSingleLittleEndian(s)
                    : BitConverter.ToSingle(BitConverter.GetBytes(s32), 0);
#else
                    ? BitConverter.ToSingle(BitConverter.GetBytes(s32), 0)
                    : BitConverter.ToSingle(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(s32)), 0);
#endif
            }
            else
            {
                s32 = 0;
                u32 = 0;
                f32 = 0.0f;
            }

            // 64-bit
            if (offset >= 0 && offset + 8 <= span.Length)
            {
                var s = span.Slice(offset, 8);
                long s64 = isLittleEndian ? BinaryPrimitives.ReadInt64LittleEndian(s) : BinaryPrimitives.ReadInt64BigEndian(s);
                f64 = isLittleEndian
#if NET8_0_OR_GREATER
                    ? BinaryPrimitives.ReadDoubleLittleEndian(s)
                    : BitConverter.Int64BitsToDouble(s64);
#else
                    ? BitConverter.Int64BitsToDouble(s64)
                    : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(s64));
#endif
            }
            else
            {
                f64 = 0.0;
            }

            // Bit string for current byte
            bitString = Convert.ToString(u8, 2).PadLeft(8, '0');
        }
    }
}
