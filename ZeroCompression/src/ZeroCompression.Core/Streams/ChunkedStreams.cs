using System;
using System.IO;

namespace ZeroCompression.Core.Streams
{
    /// <summary>
    /// Reads seamlessly across multi-part chunked files (.001, .002, ...).
    /// </summary>
    public sealed class ChunkedReadStream : Stream
    {
        private readonly string _baseFilePath;
        private int _currentChunkIndex = 1;
        private FileStream? _currentStream;
        private long _totalPosition = 0;
        private bool _isLegacyNoChunking = false;

        public ChunkedReadStream(string baseFilePath)
        {
            _baseFilePath = baseFilePath;
            if (!OpenNextChunk())
            {
                string legacyPath = $"{_baseFilePath}.bin";
                if (File.Exists(legacyPath))
                {
                    _isLegacyNoChunking = true;
                    _currentStream = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                else
                {
                    throw new FileNotFoundException($"Chunked archive volume not found: {_baseFilePath}.001 or .bin");
                }
            }
        }

        private bool OpenNextChunk()
        {
            if (_isLegacyNoChunking) return false;

            if (_currentStream != null)
            {
                _currentStream.Dispose();
                _currentStream = null;
            }

            string ext = _currentChunkIndex.ToString("D3");
            string nextPath = $"{_baseFilePath}.{ext}";

            if (File.Exists(nextPath))
            {
                _currentStream = new FileStream(nextPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _currentChunkIndex++;
                return true;
            }
            return false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            int currentOffset = offset;
            int bytesToRead = count;

            while (bytesToRead > 0)
            {
                if (_currentStream == null) break;

                int read = _currentStream.Read(buffer, currentOffset, bytesToRead);
                if (read == 0)
                {
                    if (!OpenNextChunk())
                    {
                        break;
                    }
                    continue;
                }

                currentOffset += read;
                bytesRead += read;
                bytesToRead -= read;
                _totalPosition += read;
            }

            return bytesRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _totalPosition;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Writes across multiple volume files (.001, .002, ...) once maximum chunk size is reached.
    /// </summary>
    public sealed class ChunkedWriteStream : Stream
    {
        private readonly string _baseFilePath;
        private readonly long _maxChunkSize;
        private int _currentChunkIndex = 1;
        private FileStream _currentStream = null!;
        private long _bytesWrittenToCurrentChunk = 0;
        private long _totalPosition = 0;

        public ChunkedWriteStream(string baseFilePath, long maxChunkSize)
        {
            _baseFilePath = baseFilePath;
            _maxChunkSize = maxChunkSize;
            OpenNextChunk();
        }

        public int PartCount => _currentChunkIndex - 1;

        private void OpenNextChunk()
        {
            if (_currentStream != null)
            {
                _currentStream.Flush();
                _currentStream.Close();
                _currentStream.Dispose();
            }

            string ext = _currentChunkIndex.ToString("D3");
            string newPath = $"{_baseFilePath}.{ext}";
            if (_maxChunkSize <= 0) newPath = $"{_baseFilePath}.bin";

            _currentStream = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _bytesWrittenToCurrentChunk = 0;
            _currentChunkIndex++;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_maxChunkSize <= 0)
            {
                _currentStream.Write(buffer, offset, count);
                _totalPosition += count;
                return;
            }

            int bytesToWrite = count;
            int currentOffset = offset;

            while (bytesToWrite > 0)
            {
                long spaceLeftInChunk = _maxChunkSize - _bytesWrittenToCurrentChunk;
                if (spaceLeftInChunk == 0)
                {
                    OpenNextChunk();
                    spaceLeftInChunk = _maxChunkSize;
                }

                int toWrite = (int)Math.Min(bytesToWrite, spaceLeftInChunk);
                _currentStream.Write(buffer, currentOffset, toWrite);

                _bytesWrittenToCurrentChunk += toWrite;
                _totalPosition += toWrite;

                currentOffset += toWrite;
                bytesToWrite -= toWrite;
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _totalPosition;
        public override long Position
        {
            get => _totalPosition;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _currentStream?.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
