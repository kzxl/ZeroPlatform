using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroConcurrency.Ipc;

namespace ZeroNetwork.PubSub
{
    /// <summary>
    /// Cross-Process (IPC) Pub/Sub event bus powered by <see cref="ZeroMmfRingBuffer"/> (OS Shared Memory).
    /// Enables sub-microsecond frame and telemetry transfer between isolated processes (e.g. C# SCADA and Python/C++ Vision/AI)
    /// without TCP sockets, named pipes, or kernel context-switch overhead.
    /// </summary>
    public sealed class IpcZeroBus : IDisposable
    {
        private readonly ZeroMmfRingBuffer _ringBuffer;
        private readonly InProcessZeroBus _localDispatcher;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Thread? _listenerThread;
        private int _disposed;

        /// <summary>
        /// Gets the local in-process bus used for dispatching messages received via IPC.
        /// </summary>
        public InProcessZeroBus LocalBus => _localDispatcher;

        /// <summary>
        /// Initializes an IPC bus instance over a named shared memory ring buffer.
        /// </summary>
        /// <param name="mapName">Globally unique OS shared memory name.</param>
        /// <param name="capacity">Slot capacity (power of 2, e.g. 1024).</param>
        /// <param name="slotSize">Maximum bytes per slot (e.g. 65536 for 64KB).</param>
        /// <param name="isListener">Whether this instance actively runs a background listener thread to read messages.</param>
        public IpcZeroBus(string mapName, int capacity = 1024, int slotSize = 65536, bool isListener = true)
        {
            _ringBuffer = ZeroMmfRingBuffer.CreateOrOpen(mapName, capacity, slotSize);
            _localDispatcher = new InProcessZeroBus();

            if (isListener)
            {
                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = $"IpcZeroBusListener-{mapName}"
                };
                _listenerThread.Start();
            }
        }

        /// <summary>
        /// Attempts to publish a message with topic to the shared memory ring buffer.
        /// </summary>
        /// <param name="topic">Hierarchical topic name.</param>
        /// <param name="payload">Payload byte span.</param>
        /// <returns><c>true</c> if written to IPC slot; <c>false</c> if slot buffer is full (backpressure).</returns>
        public bool TryPublish(string topic, ReadOnlySpan<byte> payload)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));

            int topicByteCount = Encoding.UTF8.GetByteCount(topic);
            if (topicByteCount > ushort.MaxValue)
                throw new ArgumentException("Topic length exceeds maximum supported size (64KB).", nameof(topic));

            int totalPacketSize = sizeof(ushort) + topicByteCount + payload.Length;
            if (totalPacketSize > _ringBuffer.MaxPayloadSize)
                throw new ArgumentOutOfRangeException(nameof(payload), $"Total packet size ({totalPacketSize} B) exceeds maximum slot payload ({_ringBuffer.MaxPayloadSize} B).");

            byte[]? rented = null;
            Span<byte> packetSpan = totalPacketSize <= 1024
                ? stackalloc byte[totalPacketSize]
                : (rented = ArrayPool<byte>.Shared.Rent(totalPacketSize)).AsSpan(0, totalPacketSize);

            try
            {
                // Write topic length header
                BitConverter.GetBytes((ushort)topicByteCount).AsSpan().CopyTo(packetSpan.Slice(0, 2));

                // Write topic bytes (zero-allocation across net462, netstandard2.0, and net8.0)
                unsafe
                {
                    fixed (char* pTopic = topic)
                    fixed (byte* pPacket = packetSpan)
                    {
                        Encoding.UTF8.GetBytes(pTopic, topic.Length, pPacket + 2, topicByteCount);
                    }
                }

                // Write payload
                if (payload.Length > 0)
                {
                    payload.CopyTo(packetSpan.Slice(2 + topicByteCount));
                }

                return _ringBuffer.TryWrite(packetSpan);
            }
            finally
            {
                if (rented != null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Subscribes to an IPC topic pattern on the local receiver dispatcher.
        /// </summary>
        public IZeroSubscription SubscribeTopic(string topicPattern, Action<string, ReadOnlyMemory<byte>> handler)
        {
            ThrowIfDisposed();
            return _localDispatcher.SubscribeTopic(topicPattern, handler);
        }

        /// <summary>
        /// Subscribes an async handler to an IPC topic pattern on the local receiver dispatcher.
        /// </summary>
        public IZeroSubscription SubscribeTopic(string topicPattern, Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler)
        {
            ThrowIfDisposed();
            return _localDispatcher.SubscribeTopic(topicPattern, handler);
        }

        private void ListenLoop()
        {
            var token = _cts.Token;
            int maxPayload = _ringBuffer.MaxPayloadSize;
            byte[] readBuffer = new byte[maxPayload];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_ringBuffer.TryRead(readBuffer.AsSpan(), out int bytesRead))
                    {
                        if (bytesRead >= sizeof(ushort))
                        {
                            ushort topicLen = BitConverter.ToUInt16(readBuffer, 0);
                            if (bytesRead >= sizeof(ushort) + topicLen)
                            {
                                string topic = Encoding.UTF8.GetString(readBuffer, sizeof(ushort), topicLen);
                                int payloadOffset = sizeof(ushort) + topicLen;
                                int payloadLen = bytesRead - payloadOffset;

                                byte[] payloadCopy = new byte[payloadLen];
                                if (payloadLen > 0)
                                {
                                    Buffer.BlockCopy(readBuffer, payloadOffset, payloadCopy, 0, payloadLen);
                                }

                                _ = _localDispatcher.PublishTopicAsync(topic, payloadCopy, token);
                            }
                        }
                    }
                    else
                    {
                        // Spin / yield backoff when buffer is empty
                        Thread.Sleep(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(IpcZeroBus));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _cts.Cancel();
                _localDispatcher.Dispose();
                _ringBuffer.Dispose();
                _cts.Dispose();
            }
        }
    }
}
