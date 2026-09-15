using System;

namespace ZeroRfid.Core.Models;

/// <summary>
/// Event representing a state change on an RFID reader's General Purpose Input (GPI) pin.
/// </summary>
public sealed class GpioPinChangedEvent
{
    /// <summary>
    /// Gets the 1-based pin index.
    /// </summary>
    public int PinIndex { get; }

    /// <summary>
    /// Gets whether the pin transitioned to High (True) or Low (False).
    /// </summary>
    public bool IsHigh { get; }

    /// <summary>
    /// Gets the UTC timestamp of the state transition.
    /// </summary>
    public DateTime TimestampUtc { get; }

    public GpioPinChangedEvent(int pinIndex, bool isHigh, DateTime? timestampUtc = null)
    {
        PinIndex = pinIndex;
        IsHigh = isHigh;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public override string ToString() => $"[GPI] Pin {PinIndex} = {(IsHigh ? "HIGH" : "LOW")} at {TimestampUtc:HH:mm:ss.fff}";
}
