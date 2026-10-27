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
    /// High-performance encoder and writer for RIFF/WAVE audio streams and files.
    /// Supports writing 16-bit PCM and 32-bit IEEE Float with automatic header size finalization.
    /// </summary>
    public sealed class WavWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly long _dataChunkPosition;
        private long _dataBytesWritten;
        private byte[]? _rentedBuffer;
        private bool _isDisposed;

        public AudioFormat Format { get; }
        public long TotalFramesWritten => Format.BytesToFrames(_dataBytesWritten);
        public TimeSpan DurationWritten => Format.CalculateDuration(_dataBytesWritten);

        public WavWriter(Stream stream, AudioFormat format, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Format = format;
            _leaveOpen = leaveOpen;

            WriteHeader();
            _dataChunkPosition = _stream.Position;
        }

        public WavWriter(string filePath, AudioFormat format)
            : this(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None), format, leaveOpen: false)
        {
        }

        /// <summary>
        /// Writes normalized float samples (-1.0f to +1.0f) into the WAV stream, automatically encoding them to the target format.
        /// </summary>
        public void WriteSamples(ReadOnlySpan<float> samples)
        {
            if (samples.IsEmpty) return;

            int bytesPerSample = Format.BytesPerSample;
            int totalBytesNeeded = samples.Length * bytesPerSample;
            byte[] buffer = RentBuffer(totalBytesNeeded);

            EncodeFloatToPcm(samples, buffer.AsSpan(0, totalBytesNeeded), Format.SampleFormat, Format.BitsPerSample);

            _stream.Write(buffer, 0, totalBytesNeeded);
            _dataBytesWritten += totalBytesNeeded;
        }

        /// <summary>
        /// Writes raw PCM encoded bytes directly into the data chunk.
        /// </summary>
        public void WriteRaw(ReadOnlySpan<byte> rawBytes)
        {
            if (rawBytes.IsEmpty) return;

            byte[] buffer = RentBuffer(rawBytes.Length);
            rawBytes.CopyTo(buffer);
            _stream.Write(buffer, 0, rawBytes.Length);
            _dataBytesWritten += rawBytes.Length;
        }

        /// <summary>
        /// Encodes normalized float samples (-1.0f to +1.0f) into raw audio bytes according to the specified format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EncodeFloatToPcm(ReadOnlySpan<float> sourceFloats, Span<byte> destinationBytes, AudioSampleFormat sampleFormat, int bitsPerSample)
        {
            switch (sampleFormat)
            {
                case AudioSampleFormat.Pcm16:
                {
                    int count = Math.Min(sourceFloats.Length, destinationBytes.Length / 2);
                    ref float srcRef = ref MemoryMarshal.GetReference(sourceFloats);
                    ref byte dstRef = ref MemoryMarshal.GetReference(destinationBytes);

                    for (int i = 0; i < count; i++)
                    {
                        float sample = Unsafe.Add(ref srcRef, i);
                        // Hard clamp to [-1.0f, +1.0f]
                        if (sample > 1.0f) sample = 1.0f;
                        else if (sample < -1.0f) sample = -1.0f;

                        short val = (short)Math.Round(sample * 32767.0f);
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, i * 2), val);
                    }
                    break;
                }
                case AudioSampleFormat.IeeeFloat32:
                {
                    int count = Math.Min(sourceFloats.Length, destinationBytes.Length / 4);
                    MemoryMarshal.Cast<float, byte>(sourceFloats.Slice(0, count)).CopyTo(destinationBytes);
                    break;
                }
                case AudioSampleFormat.Pcm8:
                {
                    int count = Math.Min(sourceFloats.Length, destinationBytes.Length);
                    for (int i = 0; i < count; i++)
                    {
                        float sample = Math.Max(-1.0f, Math.Min(1.0f, sourceFloats[i]));
                        byte val = (byte)Math.Round((sample + 1.0f) * 127.5f);
                        destinationBytes[i] = val;
                    }
                    break;
                }
                case AudioSampleFormat.Pcm24:
                {
                    int count = Math.Min(sourceFloats.Length, destinationBytes.Length / 3);
                    for (int i = 0; i < count; i++)
                    {
                        float sample = Math.Max(-1.0f, Math.Min(1.0f, sourceFloats[i]));
                        int val = (int)Math.Round(sample * 8388607.0f);
                        int offset = i * 3;
                        destinationBytes[offset] = (byte)(val & 0xFF);
                        destinationBytes[offset + 1] = (byte)((val >> 8) & 0xFF);
                        destinationBytes[offset + 2] = (byte)((val >> 16) & 0xFF);
                    }
                    break;
                }
                case AudioSampleFormat.Pcm32:
                {
                    int count = Math.Min(sourceFloats.Length, destinationBytes.Length / 4);
                    ref float srcRef = ref MemoryMarshal.GetReference(sourceFloats);
                    ref byte dstRef = ref MemoryMarshal.GetReference(destinationBytes);

                    for (int i = 0; i < count; i++)
                    {
                        float sample = Math.Max(-1.0f, Math.Min(1.0f, Unsafe.Add(ref srcRef, i)));
                        int val = (int)Math.Round(sample * 2147483647.0f);
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, i * 4), val);
                    }
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported target sample format: {sampleFormat} ({bitsPerSample}-bit)");
            }
        }

        /// <summary>
        /// Convenience method to write all normalized float samples to a WAV file.
        /// </summary>
        public static void WriteAllSamples(string filePath, ReadOnlySpan<float> samples, AudioFormat format)
        {
            using var writer = new WavWriter(filePath, format);
            writer.WriteSamples(samples);
        }

        private void WriteHeader()
        {
            // Standard 44-byte WAV header for PCM, or 56-byte for IEEE Float (with 12-byte fact chunk)
            bool isFloat = Format.SampleFormat == AudioSampleFormat.IeeeFloat32;
            int headerSize = isFloat ? 56 : 44;

            Span<byte> header = stackalloc byte[headerSize];

            // RIFF chunk descriptor
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), WavConstants.RiffMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), 0); // Placeholder for file size - 8
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), WavConstants.WaveMagic);

            // 'fmt ' sub-chunk
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), WavConstants.FmtMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), 16); // fmt chunk size
            ushort formatCode = isFloat ? WavConstants.WaveFormatIeeeFloat : WavConstants.WaveFormatPcm;
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(20, 2), formatCode);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(22, 2), (ushort)Format.Channels);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), (uint)Format.SampleRate);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(28, 4), (uint)Format.BytesPerSecond);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(32, 2), (ushort)Format.BytesPerFrame);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(34, 2), (ushort)Format.BitsPerSample);

            int dataOffset = 36;
            if (isFloat)
            {
                // 'fact' chunk required for non-PCM formats
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(36, 4), WavConstants.FactMagic);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(40, 4), 4); // fact chunk size
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(44, 4), 0); // Placeholder for sample frame count
                dataOffset = 48;
            }

            // 'data' sub-chunk
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(dataOffset, 4), WavConstants.DataMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(dataOffset + 4, 4), 0); // Placeholder for data length

            _stream.WriteCompat(header);
        }

        public void FinalizeHeader()
        {
            if (!_stream.CanSeek) return;

            long currentPos = _stream.Position;
            bool isFloat = Format.SampleFormat == AudioSampleFormat.IeeeFloat32;
            int dataChunkHeaderOffset = isFloat ? 48 : 36;

            // 1. Update RIFF chunk size at offset 4: (TotalFileSize - 8)
            uint totalRiffLength = (uint)(currentPos - 8);
            _stream.Position = 4;
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, totalRiffLength);
            _stream.WriteCompat(buf);

            // 2. If Float, update 'fact' chunk total samples at offset 44
            if (isFloat)
            {
                _stream.Position = 44;
                uint totalSampleFrames = (uint)TotalFramesWritten;
                BinaryPrimitives.WriteUInt32LittleEndian(buf, totalSampleFrames);
                _stream.WriteCompat(buf);
            }

            // 3. Update 'data' chunk size at (dataChunkHeaderOffset + 4)
            _stream.Position = dataChunkHeaderOffset + 4;
            uint dataChunkSize = (uint)_dataBytesWritten;
            BinaryPrimitives.WriteUInt32LittleEndian(buf, dataChunkSize);
            _stream.WriteCompat(buf);

            _stream.Position = currentPos;
            _stream.Flush();
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
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                FinalizeHeader();
            }
            catch
            {
                // Ignore seek/finalize errors if stream was aborted
            }

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
