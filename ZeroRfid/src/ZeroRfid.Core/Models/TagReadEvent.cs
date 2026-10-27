using System;
using System.Collections.Generic;

namespace ZeroRfid.Core.Models;

/// <summary>
/// Sovereign normalized event representing a single tag detection from an RFID reader.
/// </summary>
public sealed class TagReadEvent
{
    /// <summary>
    /// Gets the Electronic Product Code (EPC) represented as normalized uppercase hexadecimal.
    /// </summary>
    public string Epc { get; }

    /// <summary>
    /// Gets the unique Tag Identifier (TID) if read, or null.
    /// </summary>
    public string? Tid { get; }

    /// <summary>
    /// Gets the 1-based index of the antenna that detected the tag.
    /// </summary>
    public int AntennaId { get; }

    /// <summary>
    /// Gets the Received Signal Strength Indicator (RSSI) in decibels relative to one milliwatt (dBm).
    /// </summary>
    public double Rssi { get; }

    /// <summary>
    /// Gets the number of times this tag was physically read in the aggregate batch.
    /// </summary>
    public int ReadCount { get; }

    /// <summary>
    /// Gets the UTC timestamp when the tag was read.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// Gets the RF carrier phase angle in degrees (0.0 to 360.0), if supported.
    /// </summary>
    public double? PhaseAngle { get; }

    /// <summary>
    /// Gets the carrier channel frequency in megahertz (MHz), if supported.
    /// </summary>
    public double? Frequency { get; }

    /// <summary>
    /// Gets the identifier of the reader hardware that generated this event.
    /// </summary>
    public string ReaderId { get; }

    /// <summary>
    /// Gets optional provider-specific metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    public TagReadEvent(
        string epc,
        int antennaId,
        double rssi,
        string readerId,
        DateTime? timestampUtc = null,
        int readCount = 1,
        string? tid = null,
        double? phaseAngle = null,
        double? frequency = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        Epc = epc ?? throw new ArgumentNullException(nameof(epc));
        AntennaId = antennaId;
        Rssi = rssi;
        ReaderId = readerId ?? string.Empty;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
        ReadCount = readCount > 0 ? readCount : 1;
        Tid = tid;
        PhaseAngle = phaseAngle;
        Frequency = frequency;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a cloned event with an updated read count and latest timestamp.
    /// </summary>
    public TagReadEvent WithAggregate(int additionalReads, double latestRssi, DateTime timestampUtc)
    {
        return new TagReadEvent(
            Epc,
            AntennaId,
            latestRssi,
            ReaderId,
            timestampUtc,
            ReadCount + additionalReads,
            Tid,
            PhaseAngle,
            Frequency,
            Metadata);
    }

    public override string ToString()
    {
        return $"[Tag] EPC={Epc}, Ant={AntennaId}, RSSI={Rssi:F1}dBm, Count={ReadCount}, Reader={ReaderId}";
    }
}
