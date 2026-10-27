using System;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// High-performance, zero-allocation sequential binary reader over ReadOnlySpan&lt;byte&gt;.
    /// Eliminates manual offset tracking and bounds bugs during network packet or binary stream parsing.
    /// Utilizes ThrowHelper cold-path isolation to achieve 100% JIT compiler method inlining.
    /// </summary>
    public ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private int _pos;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length - _pos;
        }

        public bool HasRemaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos < _buffer.Length;
        }

        public ReadOnlySpan<byte> RemainingSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(_pos);
        }

        public ReadOnlySpan<byte> ConsumedSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(0, _pos);
        }

        #region Byte Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if ((uint)_pos >= (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            return _buffer[_pos++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadByte(out byte value)
        {
            if ((uint)_pos < (uint)_buffer.Length)
            {
                value = _buffer[_pos++];
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte PeekByte()
        {
            if ((uint)_pos >= (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            return _buffer[_pos];
        }

        #endregion

        #region Little-Endian Primitives

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16LittleEndian()
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            short val = FastBinary.ReadInt16LittleEndian(_buffer.Slice(_pos));
            _pos += 2;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt16LittleEndian(out short value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt16LittleEndian(_buffer.Slice(_pos));
                _pos += 2;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16LittleEndian()
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            ushort val = FastBinary.ReadUInt16LittleEndian(_buffer.Slice(_pos));
            _pos += 2;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt16LittleEndian(out ushort value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt16LittleEndian(_buffer.Slice(_pos));
                _pos += 2;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32LittleEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            int val = FastBinary.ReadInt32LittleEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt32LittleEndian(out int value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt32LittleEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32LittleEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            uint val = FastBinary.ReadUInt32LittleEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt32LittleEndian(out uint value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt32LittleEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64LittleEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            long val = FastBinary.ReadInt64LittleEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt64LittleEndian(out long value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt64LittleEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64LittleEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            ulong val = FastBinary.ReadUInt64LittleEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt64LittleEndian(out ulong value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt64LittleEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingleLittleEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            float val = FastBinary.ReadSingleLittleEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSingleLittleEndian(out float value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadSingleLittleEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDoubleLittleEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            double val = FastBinary.ReadDoubleLittleEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadDoubleLittleEndian(out double value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadDoubleLittleEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        #endregion

        #region Big-Endian Primitives

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16BigEndian()
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            short val = FastBinary.ReadInt16BigEndian(_buffer.Slice(_pos));
            _pos += 2;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt16BigEndian(out short value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt16BigEndian(_buffer.Slice(_pos));
                _pos += 2;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16BigEndian()
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            ushort val = FastBinary.ReadUInt16BigEndian(_buffer.Slice(_pos));
            _pos += 2;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt16BigEndian(out ushort value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt16BigEndian(_buffer.Slice(_pos));
                _pos += 2;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32BigEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            int val = FastBinary.ReadInt32BigEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt32BigEndian(out int value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt32BigEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32BigEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            uint val = FastBinary.ReadUInt32BigEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt32BigEndian(out uint value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt32BigEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64BigEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            long val = FastBinary.ReadInt64BigEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt64BigEndian(out long value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadInt64BigEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64BigEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            ulong val = FastBinary.ReadUInt64BigEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadUInt64BigEndian(out ulong value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadUInt64BigEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingleBigEndian()
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            float val = FastBinary.ReadSingleBigEndian(_buffer.Slice(_pos));
            _pos += 4;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSingleBigEndian(out float value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadSingleBigEndian(_buffer.Slice(_pos));
                _pos += 4;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDoubleBigEndian()
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowEndOfBuffer();
            double val = FastBinary.ReadDoubleBigEndian(_buffer.Slice(_pos));
            _pos += 8;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadDoubleBigEndian(out double value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                value = FastBinary.ReadDoubleBigEndian(_buffer.Slice(_pos));
                _pos += 8;
                return true;
            }
            value = 0;
            return false;
        }

        #endregion

        #region Variable-Length Integers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadVarUInt32()
        {
            if (!VarIntCodec.TryReadVarUInt32(_buffer.Slice(_pos), out uint val, out int bytesRead))
                ThrowHelper.ThrowInvalidVarIntEncoding(nameof(UInt32));
            _pos += bytesRead;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVarUInt32(out uint value)
        {
            if (VarIntCodec.TryReadVarUInt32(_buffer.Slice(_pos), out value, out int bytesRead))
            {
                _pos += bytesRead;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVarInt32()
        {
            if (!VarIntCodec.TryReadVarInt32(_buffer.Slice(_pos), out int val, out int bytesRead))
                ThrowHelper.ThrowInvalidVarIntEncoding(nameof(Int32));
            _pos += bytesRead;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVarInt32(out int value)
        {
            if (VarIntCodec.TryReadVarInt32(_buffer.Slice(_pos), out value, out int bytesRead))
            {
                _pos += bytesRead;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadVarUInt64()
        {
            if (!VarIntCodec.TryReadVarUInt64(_buffer.Slice(_pos), out ulong val, out int bytesRead))
                ThrowHelper.ThrowInvalidVarIntEncoding(nameof(UInt64));
            _pos += bytesRead;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVarUInt64(out ulong value)
        {
            if (VarIntCodec.TryReadVarUInt64(_buffer.Slice(_pos), out value, out int bytesRead))
            {
                _pos += bytesRead;
                return true;
            }
            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadVarInt64()
        {
            if (!VarIntCodec.TryReadVarInt64(_buffer.Slice(_pos), out long val, out int bytesRead))
                ThrowHelper.ThrowInvalidVarIntEncoding(nameof(Int64));
            _pos += bytesRead;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVarInt64(out long value)
        {
            if (VarIntCodec.TryReadVarInt64(_buffer.Slice(_pos), out value, out int bytesRead))
            {
                _pos += bytesRead;
                return true;
            }
            value = 0;
            return false;
        }

        #endregion

        #region Slices & Strings

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (count < 0 || (uint)(_pos + count) > (uint)_buffer.Length)
                ThrowHelper.ThrowRequestedLengthExceedsBuffer(nameof(count));
            var slice = _buffer.Slice(_pos, count);
            _pos += count;
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadBytes(int count, out ReadOnlySpan<byte> bytes)
        {
            if (count >= 0 && (uint)(_pos + count) <= (uint)_buffer.Length)
            {
                bytes = _buffer.Slice(_pos, count);
                _pos += count;
                return true;
            }
            bytes = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe string ReadStringUtf8(int byteLength)
        {
            var bytes = ReadBytes(byteLength);
#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(bytes);
#else
            if (bytes.IsEmpty) return string.Empty;
            fixed (byte* p = bytes)
            {
                return Encoding.UTF8.GetString(p, bytes.Length);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryReadStringUtf8(int byteLength, out string value)
        {
            if (TryReadBytes(byteLength, out var bytes))
            {
#if NET8_0_OR_GREATER
                value = Encoding.UTF8.GetString(bytes);
#else
                if (bytes.IsEmpty)
                {
                    value = string.Empty;
                    return true;
                }
                fixed (byte* p = bytes)
                {
                    value = Encoding.UTF8.GetString(p, bytes.Length);
                }
#endif
                return true;
            }
            value = string.Empty;
            return false;
        }

        #endregion

        #region Navigation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (count < 0 || (uint)(_pos + count) > (uint)_buffer.Length)
                ThrowHelper.ThrowCannotAdvanceBeyondBounds(nameof(count));
            _pos += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(int count)
        {
            if (count < 0 || _pos - count < 0)
                ThrowHelper.ThrowCannotRewindBeforeStart(nameof(count));
            _pos -= count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _pos = 0;
        }

        #endregion
    }
}
