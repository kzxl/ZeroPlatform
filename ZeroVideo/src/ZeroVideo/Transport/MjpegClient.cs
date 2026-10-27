using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroVideo.Transport
{
    /// <summary>
    /// Event arguments containing a single extracted Motion JPEG frame.
    /// </summary>
    public class MjpegFrameEventArgs : EventArgs
    {
        /// <summary>Raw compressed JPEG frame byte data.</summary>
        public byte[] JpegData { get; }

        /// <summary>Sequential frame sequence number.</summary>
        public long FrameIndex { get; }

        /// <summary>Local reception timestamp.</summary>
        public DateTime Timestamp { get; }

        public MjpegFrameEventArgs(byte[] jpegData, long frameIndex, DateTime timestamp)
        {
            JpegData = jpegData ?? throw new ArgumentNullException(nameof(jpegData));
            FrameIndex = frameIndex;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Pure C# industrial Motion JPEG (MJPEG) streaming client and multipart frame parser.
    /// Ingests HTTP multipart/x-mixed-replace video streams from IP cameras, machine vision sensors,
    /// and industrial web servers without any native external dependencies.
    /// </summary>
    public class MjpegClient : IDisposable
    {
        private const byte JpegSoiFirst = 0xFF;
        private const byte JpegSoiSecond = 0xD8;
        private const byte JpegEoiFirst = 0xFF;
        private const byte JpegEoiSecond = 0xD9;

        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private long _frameCounter;
        private bool _disposed;

        /// <summary>Fired when a complete valid JPEG frame is parsed from the stream.</summary>
        public event EventHandler<MjpegFrameEventArgs>? FrameReceived;

        /// <summary>Fired when a transport or parsing error occurs during stream consumption.</summary>
        public event EventHandler<Exception>? ErrorOccurred;

        /// <summary>Indicates whether the client is currently processing a stream.</summary>
        public bool IsRunning => _readTask != null && !_readTask.IsCompleted;

        /// <summary>Total valid frames successfully received and dispatched.</summary>
        public long TotalFramesReceived => _frameCounter;

        /// <summary>
        /// Starts parsing MJPEG frames asynchronously from the specified input stream.
        /// </summary>
        public void Start(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (IsRunning) throw new InvalidOperationException("MjpegClient is already running.");

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _readTask = Task.Run(() => ProcessStreamLoopAsync(stream, token), token);
        }

        /// <summary>
        /// Stops the background stream reading task.
        /// </summary>
        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private async Task ProcessStreamLoopAsync(Stream stream, CancellationToken token)
        {
            byte[] readChunk = new byte[16384];
            using (var memoryBuffer = new MemoryStream())
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(readChunk, 0, readChunk.Length, token).ConfigureAwait(false);
                        if (bytesRead <= 0)
                        {
                            break; // Stream ended or closed
                        }

                        memoryBuffer.Write(readChunk, 0, bytesRead);

                        // Parse available frames from accumulated buffer
                        ExtractAndDispatchFrames(memoryBuffer);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected cancellation on Stop()
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private void ExtractAndDispatchFrames(MemoryStream memoryBuffer)
        {
            byte[] buffer = memoryBuffer.GetBuffer();
            int length = (int)memoryBuffer.Length;

            int processedOffset = 0;

            while (true)
            {
                int soiIndex = FindMarker(buffer, processedOffset, length, JpegSoiFirst, JpegSoiSecond);
                if (soiIndex < 0)
                {
                    // No Start of Image marker found yet, wait for more data
                    break;
                }

                int eoiIndex = FindMarker(buffer, soiIndex + 2, length, JpegEoiFirst, JpegEoiSecond);
                if (eoiIndex < 0)
                {
                    // SOI found but EOI not yet arrived; keep SOI in buffer
                    processedOffset = soiIndex;
                    break;
                }

                // Complete frame found between [soiIndex, eoiIndex + 2]
                int frameLength = (eoiIndex + 2) - soiIndex;
                byte[] frameBytes = new byte[frameLength];
                Buffer.BlockCopy(buffer, soiIndex, frameBytes, 0, frameLength);

                long frameIndex = Interlocked.Increment(ref _frameCounter);
                FrameReceived?.Invoke(this, new MjpegFrameEventArgs(frameBytes, frameIndex, DateTime.UtcNow));

                processedOffset = eoiIndex + 2;
            }

            // Compact buffer: retain remaining unconsumed bytes
            if (processedOffset > 0)
            {
                int remaining = length - processedOffset;
                if (remaining > 0)
                {
                    Buffer.BlockCopy(buffer, processedOffset, buffer, 0, remaining);
                    memoryBuffer.Position = remaining;
                    memoryBuffer.SetLength(remaining);
                }
                else
                {
                    memoryBuffer.Position = 0;
                    memoryBuffer.SetLength(0);
                }
            }
        }

        /// <summary>
        /// Scans a byte array for a specific 2-byte marker (e.g. 0xFF, 0xD8 for SOI or 0xFF, 0xD9 for EOI).
        /// </summary>
        public static int FindMarker(byte[] buffer, int startIndex, int length, byte b1, byte b2)
        {
            if (buffer == null || startIndex < 0 || length <= startIndex + 1) return -1;

            int searchEnd = length - 1;
            for (int i = startIndex; i < searchEnd; i++)
            {
                if (buffer[i] == b1 && buffer[i + 1] == b2)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Synchronously extracts all standalone JPEG frames contained within a single buffer or stream.
        /// </summary>
        public static List<byte[]> ExtractAllFrames(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var frames = new List<byte[]>();
            int offset = 0;
            int length = data.Length;

            while (offset < length)
            {
                int soi = FindMarker(data, offset, length, JpegSoiFirst, JpegSoiSecond);
                if (soi < 0) break;

                int eoi = FindMarker(data, soi + 2, length, JpegEoiFirst, JpegEoiSecond);
                if (eoi < 0) break;

                int frameLen = (eoi + 2) - soi;
                byte[] frame = new byte[frameLen];
                Buffer.BlockCopy(data, soi, frame, 0, frameLen);
                frames.Add(frame);

                offset = eoi + 2;
            }

            return frames;
        }

        /// <summary>
        /// Extracts the boundary parameter from a standard HTTP Content-Type header string
        /// (e.g. "multipart/x-mixed-replace; boundary=--myboundary" or "boundary=frame").
        /// </summary>
        public static string? ParseBoundary(string contentTypeHeader)
        {
            if (string.IsNullOrEmpty(contentTypeHeader)) return null;

            int boundaryIdx = contentTypeHeader.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (boundaryIdx < 0) return null;

            string raw = contentTypeHeader.Substring(boundaryIdx + 9).Trim();
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
            {
                raw = raw.Substring(1, raw.Length - 2);
            }
            return raw;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Stop();
                _cts?.Dispose();
            }
        }
    }
}
