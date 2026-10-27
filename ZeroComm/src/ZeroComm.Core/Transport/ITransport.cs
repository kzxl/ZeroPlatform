using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Transport
{
    /// <summary>
    /// Unified asynchronous transport abstraction for industrial fieldbus communications (TCP, Serial, UDP).
    /// Pure C# interface with zero external dependencies.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// Gets whether the underlying transport is currently connected and active.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects asynchronously to the remote endpoint.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects gracefully from the remote endpoint.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Sends raw bytes asynchronously through the transport.
        /// </summary>
        Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a read-only memory buffer asynchronously through the transport.
        /// Enables zero-copy slicing and stack-allocated buffer transmission.
        /// </summary>
        Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when new stream data is received from the transport.
        /// Arguments: (byte[] buffer, int offset, int count).
        /// </summary>
        event Action<byte[], int, int>? DataReceived;

        /// <summary>
        /// Event raised when the transport is connected or re-connected.
        /// </summary>
        event Action? OnConnected;

        /// <summary>
        /// Event raised when an unhandled transport error occurs.
        /// </summary>
        event Action<Exception>? OnError;

        /// <summary>
        /// Event raised when the transport is disconnected.
        /// </summary>
        event Action? OnDisconnected;
    }
}
