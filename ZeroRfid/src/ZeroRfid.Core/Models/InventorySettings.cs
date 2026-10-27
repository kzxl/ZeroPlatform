using System.Collections.Generic;

namespace ZeroRfid.Core.Models;

/// <summary>
/// Configuration options for initiating an RFID continuous inventory scan.
/// </summary>
public sealed class InventorySettings
{
    /// <summary>
    /// Gets or sets the target antenna IDs to activate (1-based). If empty, all available antennas are activated.
    /// </summary>
    public IReadOnlyList<int>? AntennaIds { get; set; }

    /// <summary>
    /// Gets or sets an optional hardware RSSI threshold in dBm. Reads weaker than this value are discarded at source.
    /// </summary>
    public double? RssiThresholdDbm { get; set; }

    /// <summary>
    /// Gets or sets an EPC bit or hex prefix filter. Only matching tags are reported.
    /// </summary>
    public string? EpcFilterPrefix { get; set; }

    /// <summary>
    /// Gets or sets whether to also read the TID bank during inventory (if supported by reader mode).
    /// </summary>
    public bool ReadTid { get; set; }

    /// <summary>
    /// Gets or sets optional duration in milliseconds. 0 means continuous until explicitly stopped.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Gets default settings activating all antennas with no filtering.
    /// </summary>
    public static InventorySettings Default => new();
}
