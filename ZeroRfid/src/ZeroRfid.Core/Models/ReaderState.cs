namespace ZeroRfid.Core.Models;

/// <summary>
/// Represents the connection and operational lifecycle state of an RFID reader.
/// </summary>
public enum ReaderState
{
    /// <summary>Reader is not connected.</summary>
    Disconnected = 0,

    /// <summary>Reader connection is being established.</summary>
    Connecting = 1,

    /// <summary>Reader is connected and idle.</summary>
    Connected = 2,

    /// <summary>Reader is actively executing continuous tag inventory.</summary>
    InventoryRunning = 3,

    /// <summary>Reader encountered a fatal or communication fault.</summary>
    Faulted = 4
}
