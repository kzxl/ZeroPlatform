using System;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// High-performance, zero-allocation sequential binary writer over Span&lt;byte&gt;.
    /// Prevents buffer overruns and simplifies sequential serialization for network protocols and file formats.
    /// Utilizes ThrowHelper cold-path isolation to achieve 100% JIT compiler method inlining.
    /// </summary>
    public ref struct SpanWriter
    {
        private readonly Span<byte> _buffer;
        private int _pos;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        public int BytesWritten
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length - _pos;
        }

        public ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(0, _pos);
        }

        public Span<byte> RemainingSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(_pos);
        }

        #region Byte Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            if ((uint)_pos >= (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            _buffer[_pos++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteByte(byte value)
        {
            if ((uint)_pos < (uint)_buffer.Length)
            {
                _buffer[_pos++] = value;
                return true;
            }
            return false;
        }

        #endregion

        #region Little-Endian Primitives

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16LittleEndian(short value)
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt16LittleEndian(_buffer.Slice(_pos), value);
            _pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt16LittleEndian(short value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt16LittleEndian(_buffer.Slice(_pos), value);
                _pos += 2;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16LittleEndian(ushort value)
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt16LittleEndian(_buffer.Slice(_pos), value);
            _pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt16LittleEndian(ushort value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt16LittleEndian(_buffer.Slice(_pos), value);
                _pos += 2;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32LittleEndian(int value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt32LittleEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt32LittleEndian(int value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt32LittleEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32LittleEndian(uint value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt32LittleEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt32LittleEndian(uint value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt32LittleEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64LittleEndian(long value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt64LittleEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt64LittleEndian(long value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt64LittleEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64LittleEndian(ulong value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt64LittleEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt64LittleEndian(ulong value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt64LittleEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingleLittleEndian(float value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteSingleLittleEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteSingleLittleEndian(float value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteSingleLittleEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleLittleEndian(double value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteDoubleLittleEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteDoubleLittleEndian(double value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteDoubleLittleEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        #endregion

        #region Big-Endian Primitives

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16BigEndian(short value)
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt16BigEndian(_buffer.Slice(_pos), value);
            _pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt16BigEndian(short value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt16BigEndian(_buffer.Slice(_pos), value);
                _pos += 2;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16BigEndian(ushort value)
        {
            if ((uint)(_pos + 2) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt16BigEndian(_buffer.Slice(_pos), value);
            _pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt16BigEndian(ushort value)
        {
            if ((uint)(_pos + 2) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt16BigEndian(_buffer.Slice(_pos), value);
                _pos += 2;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32BigEndian(int value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt32BigEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt32BigEndian(int value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt32BigEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32BigEndian(uint value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt32BigEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt32BigEndian(uint value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt32BigEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64BigEndian(long value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteInt64BigEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteInt64BigEndian(long value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteInt64BigEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64BigEndian(ulong value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteUInt64BigEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteUInt64BigEndian(ulong value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteUInt64BigEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingleBigEndian(float value)
        {
            if ((uint)(_pos + 4) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteSingleBigEndian(_buffer.Slice(_pos), value);
            _pos += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteSingleBigEndian(float value)
        {
            if ((uint)(_pos + 4) <= (uint)_buffer.Length)
            {
                FastBinary.WriteSingleBigEndian(_buffer.Slice(_pos), value);
                _pos += 4;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleBigEndian(double value)
        {
            if ((uint)(_pos + 8) > (uint)_buffer.Length)
                ThrowHelper.ThrowDestinationBufferFull();
            FastBinary.WriteDoubleBigEndian(_buffer.Slice(_pos), value);
            _pos += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteDoubleBigEndian(double value)
        {
            if ((uint)(_pos + 8) <= (uint)_buffer.Length)
            {
                FastBinary.WriteDoubleBigEndian(_buffer.Slice(_pos), value);
                _pos += 8;
                return true;
            }
            return false;
        }

        #endregion

        #region Variable-Length Integers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarUInt32(uint value)
        {
            VarIntCodec.WriteVarUInt32(_buffer.Slice(_pos), value, out int written);
            _pos += written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteVarUInt32(uint value)
        {
            if (Remaining >= 5)
            {
                VarIntCodec.WriteVarUInt32(_buffer.Slice(_pos), value, out int written);
                _pos += written;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarInt32(int value)
        {
            VarIntCodec.WriteVarInt32(_buffer.Slice(_pos), value, out int written);
            _pos += written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteVarInt32(int value)
        {
            if (Remaining >= 5)
            {
                VarIntCodec.WriteVarInt32(_buffer.Slice(_pos), value, out int written);
                _pos += written;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarUInt64(ulong value)
        {
            VarIntCodec.WriteVarUInt64(_buffer.Slice(_pos), value, out int written);
            _pos += written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteVarUInt64(ulong value)
        {
            if (Remaining >= 10)
            {
                VarIntCodec.WriteVarUInt64(_buffer.Slice(_pos), value, out int written);
                _pos += written;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarInt64(long value)
        {
            VarIntCodec.WriteVarInt64(_buffer.Slice(_pos), value, out int written);
            _pos += written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteVarInt64(long value)
        {
            if (Remaining >= 10)
            {
                VarIntCodec.WriteVarInt64(_buffer.Slice(_pos), value, out int written);
                _pos += written;
                return true;
            }
            return false;
        }

        #endregion

        #region Slices & Strings

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > Remaining)
                ThrowHelper.ThrowRequestedLengthExceedsBuffer(nameof(bytes));
            bytes.CopyTo(_buffer.Slice(_pos));
            _pos += bytes.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= Remaining)
            {
                bytes.CopyTo(_buffer.Slice(_pos));
                _pos += bytes.Length;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteStringUtf8(ReadOnlySpan<char> chars)
        {
#if NET8_0_OR_GREATER
            int written = Encoding.UTF8.GetBytes(chars, RemainingSpan);
            _pos += written;
            return written;
#else
            string s = chars.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            WriteBytes(bytes);
            return bytes.Length;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteStringUtf8(ReadOnlySpan<char> chars, out int bytesWritten)
        {
            if (chars.IsEmpty)
            {
                bytesWritten = 0;
                return true;
            }

#if NET8_0_OR_GREATER
            if (Encoding.UTF8.TryGetBytes(chars, RemainingSpan, out bytesWritten))
            {
                _pos += bytesWritten;
                return true;
            }
            bytesWritten = 0;
            return false;
#else
            string s = chars.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            if (TryWriteBytes(bytes))
            {
                bytesWritten = bytes.Length;
                return true;
            }
            bytesWritten = 0;
            return false;
#endif
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
        public void Reset()
        {
            _pos = 0;
        }

        #endregion
    }
}
