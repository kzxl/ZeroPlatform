using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using ZeroPrimitives.Core.Identifiers;
using ZeroPrimitives.Cryptography;

namespace ZeroStorage.Core.Persistence
{
    /// <summary>
    /// Checksum algorithm used for WAL record integrity verification.
    /// </summary>
    public enum WalChecksumMode
    {
        Crc32C_Hardware = 1, // Castagnoli polynomial (Hardware SSE4.2 / ARM64)
        Crc32_Legacy = 2      // IEEE 802.3 Ethernet polynomial
    }

    /// <summary>
    /// Disk synchronization mode for WAL write durability.
    /// </summary>
    public enum WalSyncMode
    {
        Immediate, // Flushes binary writer and forces OS buffer flush to disk (Flush(true))
        Deferred,  // Flushes to FileStream buffer without full hardware fsync
        None       // Keeps in user-space buffer
    }

    /// <summary>
    /// Represents an individual record within a Write-Ahead Log.
    /// Features a unique, time-ordered 128-bit <see cref="Uuid7"/> RecordId.
    /// </summary>
    public sealed class WalRecord
    {
        public Uuid7 RecordId { get; }
        public long Lsn { get; }
        public byte RecordType { get; }
        public byte[] Payload { get; }

        public WalRecord(long lsn, byte recordType, byte[] payload, Uuid7 recordId = default)
        {
            Lsn = lsn;
            RecordType = recordType;
            Payload = payload ?? Array.Empty<byte>();
            RecordId = recordId == default ? Uuid7.NewUuid() : recordId;
        }
    }

    /// <summary>
    /// High-durability append-only Write-Ahead Log (WAL) with hardware-accelerated CRC32C integrity verification and crash recovery.
    /// Pure C# with zero external dependencies, leveraging ZeroPrimitives hardware intrinsics.
    /// </summary>
    public sealed class WriteAheadLog : IDisposable
    {
        private const ushort FileMagic = 0x5741; // "WA"
        private const ushort FileVersion = 2;
        private const ushort RecordMarker = 0xA55A;
        private const int HeaderSize = 4; // Magic (2) + Version (2)

        private readonly string _filePath;
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly object _syncRoot = new object();
        private long _lastLsn;
        private bool _disposed;
        private WalChecksumMode _checksumMode;

        public string FilePath => _filePath;
        public long LastLsn => _lastLsn;
        public long Length => _stream.Length;
        public WalChecksumMode ChecksumMode
        {
            get => _checksumMode;
            set => _checksumMode = value;
        }

        public WriteAheadLog(string filePath, bool autoFlush = true, WalChecksumMode checksumMode = WalChecksumMode.Crc32C_Hardware)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _checksumMode = checksumMode;

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool isNew = !File.Exists(filePath) || new FileInfo(filePath).Length < HeaderSize;
            _stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _writer = new BinaryWriter(_stream);

            if (isNew)
            {
                _writer.Write(FileMagic);
                _writer.Write(FileVersion);
                _writer.Flush();
                _lastLsn = 0;
            }
            else
            {
                // Scan to find last valid LSN
                _lastLsn = RecoverLastLsn();
                _stream.Seek(0, SeekOrigin.End);
            }
        }

        /// <summary>
        /// Computes CRC32 checksum with high performance.
        /// </summary>
        public static uint ComputeCrc32(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return FastCrc.Crc32(buffer.AsSpan(offset, count));
        }

        public long Append(byte recordType, byte[] payload, bool flush = false)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            return Append(recordType, (ReadOnlySpan<byte>)payload, flush ? WalSyncMode.Immediate : WalSyncMode.Deferred);
        }

        public long Append(byte recordType, ReadOnlySpan<byte> payload, WalSyncMode syncMode = WalSyncMode.Deferred)
        {
            lock (_syncRoot)
            {
                long lsn = ++_lastLsn;
                int recordLength = payload.Length;
                int headerLen = 2 + 8 + 1 + 4; // Marker (2) + LSN (8) + Type (1) + Length (4)
                int totalLen = headerLen + recordLength;

                byte[]? rented = null;
                Span<byte> raw = totalLen <= 1024
                    ? stackalloc byte[totalLen]
                    : (rented = ArrayPool<byte>.Shared.Rent(totalLen)).AsSpan(0, totalLen);

                try
                {
                    raw[0] = (byte)(RecordMarker & 0xFF);
                    raw[1] = (byte)((RecordMarker >> 8) & 0xFF);

                    for (int i = 0; i < 8; i++)
                        raw[2 + i] = (byte)((lsn >> (i * 8)) & 0xFF);

                    raw[10] = recordType;

                    raw[11] = (byte)(recordLength & 0xFF);
                    raw[12] = (byte)((recordLength >> 8) & 0xFF);
                    raw[13] = (byte)((recordLength >> 16) & 0xFF);
                    raw[14] = (byte)((recordLength >> 24) & 0xFF);

                    if (recordLength > 0)
                    {
                        payload.CopyTo(raw.Slice(headerLen));
                    }

                    uint crc = (_checksumMode == WalChecksumMode.Crc32C_Hardware)
                        ? FastCrc.Crc32C(raw)
                        : FastCrc.Crc32(raw);

                    _stream.Seek(0, SeekOrigin.End);

#if NET8_0_OR_GREATER
                    _stream.Write(raw);
#else
                    if (rented != null)
                    {
                        _stream.Write(rented, 0, totalLen);
                    }
                    else
                    {
                        byte[] temp = raw.ToArray();
                        _stream.Write(temp, 0, temp.Length);
                    }
#endif
                    _writer.Write(crc);

                    if (syncMode == WalSyncMode.Immediate)
                    {
                        _writer.Flush();
                        _stream.Flush(true);
                    }
                    else if (syncMode == WalSyncMode.Deferred)
                    {
                        _writer.Flush();
                    }

                    return lsn;
                }
                finally
                {
                    if (rented != null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
        }

        public List<WalRecord> ReadAllRecords()
        {
            lock (_syncRoot)
            {
                var records = new List<WalRecord>();
                _stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    if (_stream.Length < HeaderSize) return records;

                    ushort magic = reader.ReadUInt16();
                    ushort version = reader.ReadUInt16();
                    if (magic != FileMagic || (version != 1 && version != 2))
                        return records;

                    while (_stream.Position + 15 <= _stream.Length) // 2 (marker) + 8 (lsn) + 1 (type) + 4 (len)
                    {
                        ushort marker = reader.ReadUInt16();
                        if (marker != RecordMarker)
                            break; // Corrupted or incomplete record, stop replay

                        long lsn = reader.ReadInt64();
                        byte type = reader.ReadByte();
                        int length = reader.ReadInt32();

                        if (length < 0 || _stream.Position + length + 4 > _stream.Length)
                            break; // Truncated record

                        byte[] payload = reader.ReadBytes(length);
                        uint expectedCrc = reader.ReadUInt32();

                        // Recompute CRC with checkBuf
                        int rawLen = 15 + length;
                        byte[] checkBuf = new byte[rawLen];
                        checkBuf[0] = (byte)(marker & 0xFF);
                        checkBuf[1] = (byte)((marker >> 8) & 0xFF);
                        for (int i = 0; i < 8; i++) checkBuf[2 + i] = (byte)((lsn >> (i * 8)) & 0xFF);
                        checkBuf[10] = type;
                        checkBuf[11] = (byte)(length & 0xFF);
                        checkBuf[12] = (byte)((length >> 8) & 0xFF);
                        checkBuf[13] = (byte)((length >> 16) & 0xFF);
                        checkBuf[14] = (byte)((length >> 24) & 0xFF);
                        if (length > 0) Buffer.BlockCopy(payload, 0, checkBuf, 15, length);

                        bool isValid = (FastCrc.Crc32C(checkBuf) == expectedCrc) || (FastCrc.Crc32(checkBuf) == expectedCrc);
                        if (!isValid)
                            break; // CRC verification failed, stop replay

                        records.Add(new WalRecord(lsn, type, payload));
                    }
                }

                _stream.Seek(0, SeekOrigin.End);
                return records;
            }
        }

        private long RecoverLastLsn()
        {
            var records = ReadAllRecords();
            return records.Count > 0 ? records[records.Count - 1].Lsn : 0;
        }

        public void Truncate()
        {
            lock (_syncRoot)
            {
                _stream.SetLength(0);
                _stream.Seek(0, SeekOrigin.Begin);
                _writer.Write(FileMagic);
                _writer.Write(FileVersion);
                _writer.Flush();
                _stream.Flush(true);
                _lastLsn = 0;
            }
        }

        public void Flush()
        {
            lock (_syncRoot)
            {
                _writer.Flush();
                _stream.Flush(true);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_syncRoot)
                {
                    _writer?.Dispose();
                    _stream?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
