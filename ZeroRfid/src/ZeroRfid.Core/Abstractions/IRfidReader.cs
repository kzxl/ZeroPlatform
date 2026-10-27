using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Core.Abstractions;

/// <summary>
/// Sovereign interface for RFID reader hardware communication and tag stream ingestion.
/// </summary>
public interface IRfidReader : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier or network address of this reader instance.
    /// </summary>
    string ReaderId { get; }

    /// <summary>
    /// Gets the current operational lifecycle state of the reader.
    /// </summary>
    ReaderState State { get; }

    /// <summary>
    /// Gets the hardware and firmware capabilities supported by this reader.
    /// </summary>
    RfidCapabilities Capabilities { get; }

    /// <summary>
    /// Establishes the physical connection (TCP, Serial, or Sdk session) to the reader.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Gracefully closes connection to the reader and stops ongoing inventory if running.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Instructs the reader to begin continuous autonomous tag inventory scanning.
    /// </summary>
    Task StartInventoryAsync(InventorySettings? settings = null, CancellationToken ct = default);

    /// <summary>
    /// Stops continuous tag inventory scanning.
    /// </summary>
    Task StopInventoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Asynchronous streaming enumeration of real-time tag read events.
    /// </summary>
    IAsyncEnumerable<TagReadEvent> ReadTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Raised whenever an individual tag detection is processed.
    /// </summary>
    event EventHandler<TagReadEvent>? TagRead;

    /// <summary>
    /// Raised whenever the reader transitions between lifecycle states.
    /// </summary>
    event EventHandler<ReaderState>? StateChanged;
}
