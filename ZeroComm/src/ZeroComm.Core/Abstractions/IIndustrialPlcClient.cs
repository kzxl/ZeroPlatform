using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Abstractions
{
    /// <summary>
    /// Unified, sovereign industrial PLC driver interface providing strongly-typed tag access,
    /// raw memory byte transfers, and batch telemetry reading across heterogeneous PLC vendors.
    /// </summary>
    public interface IIndustrialPlcClient : IDisposable
    {
        /// <summary>
        /// Gets the logical identifier of this PLC client instance.
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Gets the targeted PLC brand/protocol architecture.
        /// </summary>
        PlcVendor Vendor { get; }

        /// <summary>
        /// Gets the current connection lifecycle state.
        /// </summary>
        PlcConnectionState State { get; }

        /// <summary>
        /// Gets the protocol capabilities and constraints of this driver.
        /// </summary>
        PlcDriverCapabilities Capabilities { get; }

        /// <summary>
        /// Event raised whenever the connection state transitions.
        /// </summary>
        event Action<IIndustrialPlcClient, PlcConnectionState>? StateChanged;

        /// <summary>
        /// Establishes the physical connection and negotiates protocol session handshakes.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects gracefully from the PLC.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Reads a strongly-typed unmanaged primitive value (bool, byte, short, int, float, double, etc.)
        /// using unified tag address syntax.
        /// </summary>
        Task<T> ReadAsync<T>(string tagAddress, CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Writes a strongly-typed unmanaged primitive value to the specified PLC tag address.
        /// </summary>
        Task<bool> WriteAsync<T>(string tagAddress, T value, CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Reads a string value from the specified PLC tag address with given maximum length.
        /// </summary>
        Task<string> ReadStringAsync(string tagAddress, int length, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a string value to the specified PLC tag address.
        /// </summary>
        Task<bool> WriteStringAsync(string tagAddress, string value, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a contiguous block of raw bytes starting at the specified PLC tag address.
        /// </summary>
        Task<byte[]> ReadRawBytesAsync(string tagAddress, int length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes raw bytes to the specified PLC tag address.
        /// </summary>
        Task<bool> WriteRawBytesAsync(string tagAddress, byte[] data, CancellationToken cancellationToken = default);
    }
}
