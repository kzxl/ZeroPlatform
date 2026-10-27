using System;

namespace ZeroRfid.Pipeline.Deduplication;

/// <summary>
/// Method for aggregating RSSI across multiple duplicate reads within a sliding window.
/// </summary>
public enum RssiAggregationMode
{
    /// <summary>Keep the strongest (highest) RSSI value observed.</summary>
    Maximum = 0,

    /// <summary>Keep the most recent RSSI value.</summary>
    Latest = 1,

    /// <summary>Compute the arithmetic mean of all RSSI values in the window.</summary>
    Average = 2
}

/// <summary>
/// Configuration parameters for sliding-window tag deduplication and debounce filtering.
/// </summary>
public sealed class DeduplicationOptions
{
    /// <summary>
    /// Gets or sets the time window within which identical EPC reads are aggregated. Default is 1000ms.
    /// </summary>
    public TimeSpan SlidingWindow { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Gets or sets the minimum number of physical reads required in a window before emitting. Default is 1.
    /// </summary>
    public int MinimumReads { get; set; } = 1;

    /// <summary>
    /// Gets or sets the RSSI calculation strategy within the aggregation window.
    /// </summary>
    public RssiAggregationMode RssiMode { get; set; } = RssiAggregationMode.Maximum;

    /// <summary>
    /// Gets default deduplication settings (1-second sliding window).
    /// </summary>
    public static DeduplicationOptions Default => new();
}
