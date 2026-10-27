using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroAudio.Common;

namespace ZeroAudio.Formats
{
    /// <summary>
    /// High-performance, streaming reader and decoder for RIFF/WAVE audio streams and files.
    /// Supports 8/16/24/32-bit PCM and 32-bit IEEE Float formats.
    /// </summary>
    public sealed class WavReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly long _dataStartPosition;
        private long _dataBytesRemaining;
        private byte[]? _rentedBuffer;

        public AudioFormat Format { get; }
        public long DataByteCount { get; }
        public long TotalFrames => Format.BytesToFrames(DataByteCount);
        public TimeSpan Duration => Format.CalculateDuration(DataByteCount);

        public WavReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;

            // Parse header and locate 'fmt ' and 'data' chunks
            (Format, _dataStartPosition, DataByteCount) = ParseWavHeader(_stream);
            _dataBytesRemaining = DataByteCount;

            if (_stream.CanSeek)
            {
                _stream.Position = _dataStartPosition;
            }
        }

        public WavReader(string filePath)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), leaveOpen: false)
        {
        }

        /// <summary>
        /// Reads and decodes normalized float samples (-1.0f to +1.0f) directly into the destination span.
        /// Returns the number of float samples written to destination.
        /// </summary>
        public int ReadSamples(Span<float> destination)
        {
            if (destination.IsEmpty || _dataBytesRemaining <= 0) return 0;

            int bytesPerSample = Format.BytesPerSample;
            int maxSamplesToRead = Math.Min(destination.Length, (int)(_dataBytesRemaining / bytesPerSample));
            if (maxSamplesToRead <= 0) return 0;

            int bytesNeeded = maxSamplesToRead * bytesPerSample;
            byte[] buffer = RentBuffer(bytesNeeded);

            int bytesRead = _stream.Read(buffer, 0, bytesNeeded);
            if (bytesRead <= 0) return 0;

            _dataBytesRemaining -= bytesRead;
            int samplesRead = bytesRead / bytesPerSample;

            DecodePcmToFloat(buffer.AsSpan(0, bytesRead), destination.Slice(0, samplesRead), Format.SampleFormat, Format.BitsPerSample);
            return samplesRead;
        }

        /// <summary>
        /// Reads raw encoded bytes directly from the audio data chunk.
        /// </summary>
        public int ReadRaw(Span<byte> destination)
        {
            if (destination.IsEmpty || _dataBytesRemaining <= 0) return 0;

            int bytesToRead = (int)Math.Min(destination.Length, _dataBytesRemaining);
            byte[] temp = RentBuffer(bytesToRead);
            int read = _stream.Read(temp, 0, bytesToRead);
            if (read > 0)
            {
                temp.AsSpan(0, read).CopyTo(destination);
                _dataBytesRemaining -= read;
            }
            return read;
        }

        /// <summary>
        /// Decodes a span of raw audio bytes into normalized float samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecodePcmToFloat(ReadOnlySpan<byte> sourceBytes, Span<float> destinationFloats, AudioSampleFormat sampleFormat, int bitsPerSample)
        {
            switch (sampleFormat)
            {
                case AudioSampleFormat.Pcm16:
                {
                    int count = Math.Min(sourceBytes.Length / 2, destinationFloats.Length);
                    ref byte srcRef = ref MemoryMarshal.GetReference(sourceBytes);
                    ref float dstRef = ref MemoryMarshal.GetReference(destinationFloats);

                    const float scale = 1.0f / 32768.0f;
                    for (int i = 0; i < count; i++)
                    {
                        short val = Unsafe.ReadUnaligned<short>(ref Unsafe.Add(ref srcRef, i * 2));
                        Unsafe.Add(ref dstRef, i) = val * scale;
                    }
                    break;
                }
                case AudioSampleFormat.IeeeFloat32:
                {
                    int count = Math.Min(sourceBytes.Length / 4, destinationFloats.Length);
                    MemoryMarshal.Cast<byte, float>(sourceBytes.Slice(0, count * 4)).CopyTo(destinationFloats);
                    break;
                }
                case AudioSampleFormat.Pcm8:
                {
                    int count = Math.Min(sourceBytes.Length, destinationFloats.Length);
                    const float scale = 1.0f / 128.0f;
                    for (int i = 0; i < count; i++)
                    {
                        destinationFloats[i] = (sourceBytes[i] - 128) * scale;
                    }
                    break;
                }
                case AudioSampleFormat.Pcm24:
                {
                    int count = Math.Min(sourceBytes.Length / 3, destinationFloats.Length);
                    const float scale = 1.0f / 8388608.0f;
                    for (int i = 0; i < count; i++)
                    {
                        int offset = i * 3;
                        int b0 = sourceBytes[offset];
                        int b1 = sourceBytes[offset + 1];
                        int b2 = (sbyte)sourceBytes[offset + 2]; // sign extend top byte
                        int val = (b2 << 16) | (b1 << 8) | b0;
                        destinationFloats[i] = val * scale;
                    }
                    break;
                }
                case AudioSampleFormat.Pcm32:
                {
                    int count = Math.Min(sourceBytes.Length / 4, destinationFloats.Length);
                    ref byte srcRef = ref MemoryMarshal.GetReference(sourceBytes);
                    ref float dstRef = ref MemoryMarshal.GetReference(destinationFloats);

                    const float scale = 1.0f / 2147483648.0f;
                    for (int i = 0; i < count; i++)
                    {
                        int val = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref srcRef, i * 4));
                        Unsafe.Add(ref dstRef, i) = val * scale;
                    }
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported sample format: {sampleFormat} ({bitsPerSample}-bit)");
            }
        }

        /// <summary>
        /// Convenience method to read all samples of a WAV stream into a single float array.
        /// </summary>
        public static float[] ReadAllSamples(Stream stream, out AudioFormat format)
        {
            using var reader = new WavReader(stream, leaveOpen: true);
            format = reader.Format;
            long totalSamples = reader.TotalFrames * format.Channels;
            if (totalSamples > int.MaxValue) throw new InvalidOperationException("WAV file too large for single array.");

            float[] samples = new float[(int)totalSamples];
            int offset = 0;
            while (offset < samples.Length)
            {
                int read = reader.ReadSamples(samples.AsSpan(offset));
                if (read <= 0) break;
                offset += read;
            }
            return samples;
        }

        /// <summary>
        /// Convenience method to read all samples of a WAV file into a single float array.
        /// </summary>
        public static float[] ReadAllSamples(string filePath, out AudioFormat format)
        {
            using var stream = File.OpenRead(filePath);
            return ReadAllSamples(stream, out format);
        }

        private static (AudioFormat format, long dataOffset, long dataLength) ParseWavHeader(Stream stream)
        {
            Span<byte> header = stackalloc byte[12];
            int read = stream.ReadCompat(header);
            if (read < 12) throw new InvalidDataException("Invalid WAV file: stream too short for RIFF header.");

            uint riff = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0, 4));
            if (riff != WavConstants.RiffMagic) throw new InvalidDataException("Not a valid RIFF stream.");

            uint wave = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4));
            if (wave != WavConstants.WaveMagic) throw new InvalidDataException("Not a valid WAVE stream.");

            AudioFormat? parsedFormat = null;
            long dataOffset = -1;
            long dataLength = -1;

            Span<byte> chunkHeader = stackalloc byte[8];
            while (stream.Position < stream.Length)
            {
                read = stream.ReadCompat(chunkHeader);
                if (read < 8) break;

                uint chunkId = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(0, 4));
                uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(4, 4));

                if (chunkId == WavConstants.FmtMagic)
                {
                    byte[] fmtBuffer = ArrayPool<byte>.Shared.Rent((int)chunkSize);
                    try
                    {
                        stream.Read(fmtBuffer, 0, (int)chunkSize);
                        var fmtSpan = fmtBuffer.AsSpan(0, (int)chunkSize);

                        ushort audioFormatCode = BinaryPrimitives.ReadUInt16LittleEndian(fmtSpan.Slice(0, 2));
                        ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(fmtSpan.Slice(2, 2));
                        uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmtSpan.Slice(4, 4));
                        ushort bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmtSpan.Slice(14, 2));

                        AudioSampleFormat sampleFormat = AudioSampleFormat.Pcm16;
                        if (audioFormatCode == WavConstants.WaveFormatIeeeFloat)
                        {
                            sampleFormat = AudioSampleFormat.IeeeFloat32;
                        }
                        else if (audioFormatCode == WavConstants.WaveFormatPcm)
                        {
                            sampleFormat = bitsPerSample switch
                            {
                                8 => AudioSampleFormat.Pcm8,
                                16 => AudioSampleFormat.Pcm16,
                                24 => AudioSampleFormat.Pcm24,
                                32 => AudioSampleFormat.Pcm32,
                                _ => throw new NotSupportedException($"Unsupported PCM bit depth: {bitsPerSample}")
                            };
                        }
                        else if (audioFormatCode == WavConstants.WaveFormatExtensible && chunkSize >= 40)
                        {
                            ushort subFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmtSpan.Slice(24, 2));
                            sampleFormat = (subFormat == WavConstants.WaveFormatIeeeFloat)
                                ? AudioSampleFormat.IeeeFloat32
                                : (bitsPerSample == 24 ? AudioSampleFormat.Pcm24 : AudioSampleFormat.Pcm16);
                        }

                        parsedFormat = new AudioFormat((int)sampleRate, channels, bitsPerSample, sampleFormat);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(fmtBuffer);
                    }
                }
                else if (chunkId == WavConstants.DataMagic)
                {
                    dataOffset = stream.Position;
                    dataLength = chunkSize;
                    break;
                }
                else
                {
                    // Skip unrecognized chunks (JUNK, LIST, fact, etc.)
                    if (stream.CanSeek)
                    {
                        stream.Seek(chunkSize, SeekOrigin.Current);
                    }
                    else
                    {
                        byte[] skip = ArrayPool<byte>.Shared.Rent(4096);
                        long remaining = chunkSize;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, skip.Length);
                            int r = stream.Read(skip, 0, toRead);
                            if (r <= 0) break;
                            remaining -= r;
                        }
                        ArrayPool<byte>.Shared.Return(skip);
                    }
                }
            }

            if (!parsedFormat.HasValue) throw new InvalidDataException("Missing 'fmt ' chunk in WAV file.");
            if (dataOffset < 0) throw new InvalidDataException("Missing 'data' chunk in WAV file.");

            return (parsedFormat.Value, dataOffset, dataLength);
        }

        private byte[] RentBuffer(int minSize)
        {
            if (_rentedBuffer == null || _rentedBuffer.Length < minSize)
            {
                if (_rentedBuffer != null) ArrayPool<byte>.Shared.Return(_rentedBuffer);
                _rentedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(minSize, 4096));
            }
            return _rentedBuffer;
        }

        public void Dispose()
        {
            if (_rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedBuffer);
                _rentedBuffer = null;
            }
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
    }
}
