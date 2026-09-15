using System;

namespace ZeroRfid.Core.Models;

/// <summary>
/// Bitwise flags representing hardware capabilities supported by an RFID reader.
/// </summary>
[Flags]
public enum RfidCapabilities
{
    /// <summary>No capabilities reported.</summary>
    None = 0,

    /// <summary>Supports continuous asynchronous tag inventory streaming.</summary>
    InventoryStreaming = 1 << 0,

    /// <summary>Supports reading discrete memory banks (Reserved, EPC, TID, User).</summary>
    MemoryRead = 1 << 1,

    /// <summary>Supports writing to tag memory banks.</summary>
    MemoryWrite = 1 << 2,

    /// <summary>Supports industrial General Purpose Input/Output (GPIO) pins.</summary>
    GpioControl = 1 << 3,

    /// <summary>Supports tag locking and permanent kill commands.</summary>
    LockAndKill = 1 << 4,

    /// <summary>Supports reporting tag RF phase angle for Doppler and motion tracking.</summary>
    PhaseAngle = 1 << 5,

    /// <summary>Supports reporting carrier frequency channel per tag read.</summary>
    CarrierFrequency = 1 << 6,

    /// <summary>Supports reading antenna RSSI signal strength.</summary>
    RssiReporting = 1 << 7
}
