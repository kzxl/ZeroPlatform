using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroCompression.Core.Codecs
{
    /// <summary>
    /// Specialized high-performance research compression codec implementing Facebook Gorilla/Chimp
    /// bit-packing algorithms for numeric and time-series telemetry data.
    /// Combines timestamp delta-of-delta bit packing with IEEE 754 float64 XOR delta encoding.
    /// </summary>
    public sealed class ZeroTelemetryCodec : ICompressionCodec
    {
        public static readonly byte[] Magic = new byte[] { 0x5A, 0x54, 0x45, 0x4C }; // "ZTEL"
        public const byte FormatVersion = 1;

        public CompressionMethod Method => CompressionMethod.ZeroTelemetry;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return new ZeroTelemetryCompressStream(destination);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new ZeroTelemetryDecompressStream(source);
        }

        #region Direct Batch Telemetry APIs

        /// <summary>
        /// Compresses parallel arrays of 64-bit integer timestamps and 64-bit IEEE 754 float values
        /// directly into a destination stream with zero intermediate allocations.
        /// </summary>
        public static void CompressTelemetry(ReadOnlySpan<long> timestamps, ReadOnlySpan<double> values, Stream destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (timestamps.Length != values.Length)
                throw new ArgumentException("Timestamps and values spans must have identical length.");

            // Write Magic + Version
            destination.Write(Magic);
            destination.WriteByte(FormatVersion);
            destination.WriteByte(0); // flags

            var writer = new BitWriter(destination);
            int count = timestamps.Length;
            writer.WriteBits((ulong)count, 32);

            if (count > 0)
            {
                // Stage 1: Timestamp Delta-of-Delta
                EncodeTimestamps(timestamps, writer);

                // Stage 2: Float64 XOR Delta (cast to ulong to prevent hardware FPU NaN silencing)
                ReadOnlySpan<ulong> wordSpan = MemoryMarshal.Cast<double, ulong>(values);
                EncodeWords(wordSpan, writer);
            }

            writer.Flush();
        }

        /// <summary>
        /// Decompresses parallel arrays of 64-bit integer timestamps and 64-bit IEEE 754 float values
        /// from an input stream.
        /// </summary>
        public static void DecompressTelemetry(Stream source, Span<long> timestamps, Span<double> values)
        {
            ArgumentNullException.ThrowIfNull(source);

            Span<byte> header = stackalloc byte[6];
            source.ReadExactly(header);

            if (header[0] != Magic[0] || header[1] != Magic[1] || header[2] != Magic[2] || header[3] != Magic[3])
                throw new InvalidDataException("Invalid ZeroTelemetry stream header magic.");

            if (header[4] != FormatVersion)
                throw new InvalidDataException($"Unsupported ZeroTelemetry version: {header[4]}.");

            var reader = new BitReader(source);
            int count = (int)reader.ReadBits(32);

            if (count != timestamps.Length || count != values.Length)
                throw new InvalidOperationException($"Span capacity ({timestamps.Length}/{values.Length}) does not match payload count ({count}).");

            if (count > 0)
            {
                DecodeTimestamps(timestamps, reader);

                Span<ulong> wordSpan = MemoryMarshal.Cast<double, ulong>(values);
                DecodeWords(wordSpan, reader);
            }
        }

        #endregion

        #region Core Gorilla Timestamp & Word Encoding Engines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EncodeTimestamps(ReadOnlySpan<long> timestamps, BitWriter writer)
        {
            int count = timestamps.Length;
            if (count == 0) return;

            long t0 = timestamps[0];
            writer.WriteBits((ulong)t0, 64);

            if (count == 1) return;

            long prevDelta = timestamps[1] - t0;
            writer.WriteBits((ulong)prevDelta, 64);

            for (int i = 2; i < count; i++)
            {
                long currentDelta = timestamps[i] - timestamps[i - 1];
                long dod = currentDelta - prevDelta;

                if (dod == 0)
                {
                    writer.WriteBit(0);
                }
                else if (dod >= -63 && dod <= 64)
                {
                    writer.WriteBits(2, 2); // '10'
                    writer.WriteBits((ulong)(dod + 63), 7);
                }
                else if (dod >= -255 && dod <= 256)
                {
                    writer.WriteBits(6, 3); // '110'
                    writer.WriteBits((ulong)(dod + 255), 9);
                }
                else if (dod >= -2047 && dod <= 2048)
                {
                    writer.WriteBits(14, 4); // '1110'
                    writer.WriteBits((ulong)(dod + 2047), 12);
                }
                else if (dod >= int.MinValue && dod <= int.MaxValue)
                {
                    writer.WriteBits(30, 5); // '11110'
                    writer.WriteBits((ulong)(uint)(int)dod, 32);
                }
                else
                {
                    writer.WriteBits(31, 5); // '11111'
                    writer.WriteBits((ulong)dod, 64);
                }

                prevDelta = currentDelta;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DecodeTimestamps(Span<long> timestamps, BitReader reader)
        {
            int count = timestamps.Length;
            if (count == 0) return;

            timestamps[0] = (long)reader.ReadBits(64);
            if (count == 1) return;

            long prevDelta = (long)reader.ReadBits(64);
            timestamps[1] = timestamps[0] + prevDelta;

            for (int i = 2; i < count; i++)
            {
                int bit0 = reader.ReadBit();
                long dod;
                if (bit0 == 0)
                {
                    dod = 0;
                }
                else
                {
                    int bit1 = reader.ReadBit();
                    if (bit1 == 0) // '10'
                    {
                        dod = (long)reader.ReadBits(7) - 63;
                    }
                    else
                    {
                        int bit2 = reader.ReadBit();
                        if (bit2 == 0) // '110'
                        {
                            dod = (long)reader.ReadBits(9) - 255;
                        }
                        else
                        {
                            int bit3 = reader.ReadBit();
                            if (bit3 == 0) // '1110'
                            {
                                dod = (long)reader.ReadBits(12) - 2047;
                            }
                            else
                            {
                                int bit4 = reader.ReadBit();
                                if (bit4 == 0) // '11110'
                                {
                                    dod = (int)(uint)reader.ReadBits(32);
                                }
                                else // '11111'
                                {
                                    dod = (long)reader.ReadBits(64);
                                }
                            }
                        }
                    }
                }

                long currentDelta = prevDelta + dod;
                timestamps[i] = timestamps[i - 1] + currentDelta;
                prevDelta = currentDelta;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EncodeWords(ReadOnlySpan<ulong> values, BitWriter writer)
        {
            int count = values.Length;
            if (count == 0) return;

            ulong prevVal = values[0];
            writer.WriteBits(prevVal, 64);

            int prevLeading = -1;
            int prevTrailing = -1;

            for (int i = 1; i < count; i++)
            {
                ulong currentVal = values[i];
                ulong xor = currentVal ^ prevVal;

                if (xor == 0)
                {
                    writer.WriteBit(0);
                }
                else
                {
                    writer.WriteBit(1);
                    int lz = BitOperations.LeadingZeroCount(xor);
                    int tz = BitOperations.TrailingZeroCount(xor);

                    if (prevLeading != -1 && lz >= prevLeading && tz >= prevTrailing)
                    {
                        writer.WriteBit(0);
                        int length = 64 - prevLeading - prevTrailing;
                        ulong mask = (length == 64) ? ~0UL : ((1UL << length) - 1);
                        ulong meaningful = (xor >> prevTrailing) & mask;
                        writer.WriteBits(meaningful, length);
                    }
                    else
                    {
                        writer.WriteBit(1);
                        writer.WriteBits((ulong)lz, 6);
                        int length = 64 - lz - tz;
                        writer.WriteBits((ulong)(length - 1), 6); // 1..64 mapped to 0..63
                        ulong mask = (length == 64) ? ~0UL : ((1UL << length) - 1);
                        ulong meaningful = (xor >> tz) & mask;
                        writer.WriteBits(meaningful, length);

                        prevLeading = lz;
                        prevTrailing = tz;
                    }
                }

                prevVal = currentVal;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DecodeWords(Span<ulong> values, BitReader reader)
        {
            int count = values.Length;
            if (count == 0) return;

            ulong prevVal = reader.ReadBits(64);
            values[0] = prevVal;

            int prevLeading = -1;
            int prevTrailing = -1;

            for (int i = 1; i < count; i++)
            {
                int bit = reader.ReadBit();
                if (bit == 0)
                {
                    values[i] = prevVal;
                }
                else
                {
                    int control = reader.ReadBit();
                    if (control == 0)
                    {
                        int length = 64 - prevLeading - prevTrailing;
                        ulong meaningful = reader.ReadBits(length);
                        ulong xor = meaningful << prevTrailing;
                        prevVal ^= xor;
                        values[i] = prevVal;
                    }
                    else
                    {
                        int lz = (int)reader.ReadBits(6);
                        int length = (int)reader.ReadBits(6) + 1;
                        int tz = 64 - lz - length;
                        ulong meaningful = reader.ReadBits(length);
                        ulong xor = meaningful << tz;
                        prevVal ^= xor;
                        values[i] = prevVal;

                        prevLeading = lz;
                        prevTrailing = tz;
                    }
                }
            }
        }

        #endregion

        #region BitWriter & BitReader

        public sealed class BitWriter
        {
            private readonly Stream _stream;
            private readonly byte[] _byteBuf = new byte[8];
            private ulong _buffer;
            private int _bitCount;

            public BitWriter(Stream stream) => _stream = stream;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBit(int bit)
            {
                _buffer = (_buffer << 1) | (ulong)(uint)(bit & 1);
                _bitCount++;
                if (_bitCount == 64)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(_byteBuf, _buffer);
                    _stream.Write(_byteBuf, 0, 8);
                    _buffer = 0;
                    _bitCount = 0;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits(ulong value, int count)
            {
                if (count <= 0) return;
                while (count > 0)
                {
                    int space = 64 - _bitCount;
                    if (count <= space)
                    {
                        if (count == 64)
                        {
                            _buffer = value;
                        }
                        else
                        {
                            ulong mask = (1UL << count) - 1;
                            _buffer = (_buffer << count) | (value & mask);
                        }
                        _bitCount += count;
                        if (_bitCount == 64)
                        {
                            BinaryPrimitives.WriteUInt64BigEndian(_byteBuf, _buffer);
                            _stream.Write(_byteBuf, 0, 8);
                            _buffer = 0;
                            _bitCount = 0;
                        }
                        return;
                    }
                    else
                    {
                        int bitsToWrite = space;
                        ulong part = (value >> (count - bitsToWrite)) & ((1UL << bitsToWrite) - 1);
                        _buffer = (_buffer << bitsToWrite) | part;
                        BinaryPrimitives.WriteUInt64BigEndian(_byteBuf, _buffer);
                        _stream.Write(_byteBuf, 0, 8);
                        _buffer = 0;
                        _bitCount = 0;
                        count -= bitsToWrite;
                        value &= (count == 64) ? ~0UL : ((1UL << count) - 1);
                    }
                }
            }

            public void Flush()
            {
                if (_bitCount > 0)
                {
                    ulong shifted = _buffer << (64 - _bitCount);
                    int bytesToWrite = (_bitCount + 7) / 8;
                    BinaryPrimitives.WriteUInt64BigEndian(_byteBuf, shifted);
                    _stream.Write(_byteBuf, 0, bytesToWrite);
                    _buffer = 0;
                    _bitCount = 0;
                }
                _stream.Flush();
            }
        }

        public sealed class BitReader
        {
            private readonly Stream _stream;
            private readonly byte[] _byteBuf = new byte[8];
            private ulong _buffer;
            private int _bitsAvailable;

            public BitReader(Stream stream) => _stream = stream;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBit()
            {
                if (_bitsAvailable == 0)
                {
                    Refill();
                }
                _bitsAvailable--;
                return (int)((_buffer >> _bitsAvailable) & 1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong ReadBits(int count)
            {
                if (count <= 0) return 0;
                ulong result = 0;
                while (count > 0)
                {
                    if (_bitsAvailable == 0)
                    {
                        Refill();
                    }
                    int take = Math.Min(count, _bitsAvailable);
                    _bitsAvailable -= take;
                    ulong mask = (take == 64) ? ~0UL : ((1UL << take) - 1);
                    ulong chunk = (_buffer >> _bitsAvailable) & mask;
                    result = (take == 64) ? chunk : ((result << take) | chunk);
                    count -= take;
                }
                return result;
            }

            private void Refill()
            {
                int totalRead = 0;
                while (totalRead < 8)
                {
                    int read = _stream.Read(_byteBuf, totalRead, 8 - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    _buffer = 0;
                    _bitsAvailable = 64; // pad zeros at stream end
                    return;
                }

                if (totalRead < 8)
                {
                    Array.Clear(_byteBuf, totalRead, 8 - totalRead);
                    _buffer = BinaryPrimitives.ReadUInt64BigEndian(_byteBuf);
                    _buffer >>= (8 - totalRead) * 8;
                    _bitsAvailable = totalRead * 8;
                }
                else
                {
                    _buffer = BinaryPrimitives.ReadUInt64BigEndian(_byteBuf);
                    _bitsAvailable = 64;
                }
            }
        }

        #endregion

        #region Streaming Implementations

        private sealed class ZeroTelemetryCompressStream : Stream
        {
            private readonly Stream _destination;
            private readonly BitWriter _writer;
            private readonly byte[] _rawBuffer = new byte[16384]; // 1024 records of 16 bytes
            private int _bufferLength;
            private bool _headerWritten;
            private bool _disposed;

            public ZeroTelemetryCompressStream(Stream destination)
            {
                _destination = destination;
                _writer = new BitWriter(destination);
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ZeroTelemetryCompressStream));
                if (!_headerWritten)
                {
                    _destination.Write(Magic);
                    _destination.WriteByte(FormatVersion);
                    _destination.WriteByte(0);
                    _headerWritten = true;
                }

                int remaining = count;
                int srcOffset = offset;

                while (remaining > 0)
                {
                    int toCopy = Math.Min(remaining, _rawBuffer.Length - _bufferLength);
                    Buffer.BlockCopy(buffer, srcOffset, _rawBuffer, _bufferLength, toCopy);
                    _bufferLength += toCopy;
                    srcOffset += toCopy;
                    remaining -= toCopy;

                    if (_bufferLength == _rawBuffer.Length)
                    {
                        FlushBlock();
                    }
                }
            }

            private void FlushBlock()
            {
                if (_bufferLength == 0) return;

                int sampleCount = _bufferLength / 16;
                int trailingBytes = _bufferLength % 16;

                // Write block header: sampleCount (24 bits) + trailingBytes (8 bits)
                _writer.WriteBits((ulong)sampleCount, 24);
                _writer.WriteBits((ulong)trailingBytes, 8);

                if (sampleCount > 0)
                {
                    Span<long> timestamps = new long[sampleCount];
                    Span<ulong> words = new ulong[sampleCount];

                    for (int i = 0; i < sampleCount; i++)
                    {
                        int baseIdx = i * 16;
                        timestamps[i] = BinaryPrimitives.ReadInt64LittleEndian(_rawBuffer.AsSpan(baseIdx, 8));
                        words[i] = BinaryPrimitives.ReadUInt64LittleEndian(_rawBuffer.AsSpan(baseIdx + 8, 8));
                    }

                    EncodeTimestamps(timestamps, _writer);
                    EncodeWords(words, _writer);
                }

                if (trailingBytes > 0)
                {
                    int trailStart = sampleCount * 16;
                    for (int i = 0; i < trailingBytes; i++)
                    {
                        _writer.WriteBits(_rawBuffer[trailStart + i], 8);
                    }
                }

                _bufferLength = 0;
            }

            public override void Flush()
            {
                _destination.Flush();
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        if (!_headerWritten)
                        {
                            _destination.Write(Magic);
                            _destination.WriteByte(FormatVersion);
                            _destination.WriteByte(0);
                            _headerWritten = true;
                        }

                        FlushBlock();

                        // Write End-of-Stream marker (sampleCount = 0, trailingBytes = 0)
                        _writer.WriteBits(0, 24);
                        _writer.WriteBits(0, 8);
                        _writer.Flush();
                    }
                    _disposed = true;
                }
                base.Dispose(disposing);
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        private sealed class ZeroTelemetryDecompressStream : Stream
        {
            private readonly Stream _source;
            private readonly BitReader _reader;
            private readonly byte[] _decodedBuffer = new byte[16384];
            private int _decodedOffset;
            private int _decodedAvailable;
            private bool _headerRead;
            private bool _eofReached;

            public ZeroTelemetryDecompressStream(Stream source)
            {
                _source = source;
                _reader = new BitReader(source);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!_headerRead)
                {
                    ReadHeader();
                }

                int totalRead = 0;
                while (totalRead < count)
                {
                    if (_decodedAvailable == 0)
                    {
                        if (_eofReached || !DecodeNextBlock())
                        {
                            break;
                        }
                    }

                    int toDeliver = Math.Min(count - totalRead, _decodedAvailable);
                    Buffer.BlockCopy(_decodedBuffer, _decodedOffset, buffer, offset + totalRead, toDeliver);
                    _decodedOffset += toDeliver;
                    _decodedAvailable -= toDeliver;
                    totalRead += toDeliver;
                }

                return totalRead;
            }

            private void ReadHeader()
            {
                Span<byte> magic = stackalloc byte[4];
                _source.ReadExactly(magic);
                if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    throw new InvalidDataException("Invalid ZeroTelemetry magic bytes.");

                int version = _source.ReadByte();
                if (version != FormatVersion)
                    throw new InvalidDataException($"Unsupported ZeroTelemetry version: {version}.");

                _source.ReadByte(); // flags
                _headerRead = true;
            }

            private bool DecodeNextBlock()
            {
                int sampleCount = (int)_reader.ReadBits(24);
                int trailingBytes = (int)_reader.ReadBits(8);

                if (sampleCount == 0 && trailingBytes == 0)
                {
                    _eofReached = true;
                    return false;
                }

                _decodedOffset = 0;
                _decodedAvailable = sampleCount * 16 + trailingBytes;

                if (sampleCount > 0)
                {
                    Span<long> timestamps = new long[sampleCount];
                    Span<ulong> words = new ulong[sampleCount];

                    DecodeTimestamps(timestamps, _reader);
                    DecodeWords(words, _reader);

                    for (int i = 0; i < sampleCount; i++)
                    {
                        int baseIdx = i * 16;
                        BinaryPrimitives.WriteInt64LittleEndian(_decodedBuffer.AsSpan(baseIdx, 8), timestamps[i]);
                        BinaryPrimitives.WriteUInt64LittleEndian(_decodedBuffer.AsSpan(baseIdx + 8, 8), words[i]);
                    }
                }

                if (trailingBytes > 0)
                {
                    int trailStart = sampleCount * 16;
                    for (int i = 0; i < trailingBytes; i++)
                    {
                        _decodedBuffer[trailStart + i] = (byte)_reader.ReadBits(8);
                    }
                }

                return true;
            }

            public override void Flush() { }
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        #endregion
    }
}
