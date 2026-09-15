using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Pipeline.Deduplication;

/// <summary>
/// High-throughput sliding-window deduplication filter suppressing rapid tag bursts and emitting appearance events.
/// </summary>
public sealed class TagDeduplicationFilter : ITagStreamFilter, IDisposable
{
    private sealed class TagEntry
    {
        public TagReadEvent FirstEvent { get; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public int Count { get; set; }
        public double MaxRssi { get; set; }
        public double LatestRssi { get; set; }
        public double SumRssi { get; set; }

        public TagEntry(TagReadEvent initial)
        {
            FirstEvent = initial;
            FirstSeenUtc = initial.TimestampUtc;
            LastSeenUtc = initial.TimestampUtc;
            Count = initial.ReadCount;
            MaxRssi = initial.Rssi;
            LatestRssi = initial.Rssi;
            SumRssi = initial.Rssi;
        }
    }

    private readonly DeduplicationOptions _options;
    private readonly ConcurrentDictionary<string, TagEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer? _cleanupTimer;

    /// <summary>
    /// Raised when a tag appears for the first time or reappears after the window elapsed.
    /// </summary>
    public event EventHandler<TagReadEvent>? TagAppeared;

    /// <summary>
    /// Raised when a tag has not been detected within the sliding window duration.
    /// </summary>
    public event EventHandler<TagReadEvent>? TagDisappeared;

    public TagDeduplicationFilter(DeduplicationOptions? options = null, bool enableDisappearanceTimer = true)
    {
        _options = options ?? DeduplicationOptions.Default;

        if (enableDisappearanceTimer)
        {
            int intervalMs = Math.Max(200, (int)(_options.SlidingWindow.TotalMilliseconds / 2));
            _cleanupTimer = new Timer(OnCleanupTick, null, intervalMs, intervalMs);
        }
    }

    /// <summary>
    /// Processes a tag read event. Returns the event if it's a first appearance, or null if suppressed by the deduplication window.
    /// </summary>
    public ValueTask<TagReadEvent?> ProcessAsync(TagReadEvent tagEvent, CancellationToken ct = default)
    {
        if (tagEvent == null)
            return new ValueTask<TagReadEvent?>((TagReadEvent?)null);

        string key = tagEvent.Epc;
        DateTime now = tagEvent.TimestampUtc;

        bool isNew = false;
        TagReadEvent? resultToEmit = null;

        _entries.AddOrUpdate(
            key,
            addValueFactory: _ =>
            {
                isNew = true;
                return new TagEntry(tagEvent);
            },
            updateValueFactory: (_, existing) =>
            {
                TimeSpan elapsed = now - existing.LastSeenUtc;
                if (elapsed >= _options.SlidingWindow)
                {
                    // Window expired, treat as re-appearance
                    isNew = true;
                    existing.FirstSeenUtc = now;
                    existing.LastSeenUtc = now;
                    existing.Count = tagEvent.ReadCount;
                    existing.MaxRssi = tagEvent.Rssi;
                    existing.LatestRssi = tagEvent.Rssi;
                    existing.SumRssi = tagEvent.Rssi;
                }
                else
                {
                    // Duplicate within sliding window
                    existing.LastSeenUtc = now;
                    existing.Count += tagEvent.ReadCount;
                    if (tagEvent.Rssi > existing.MaxRssi)
                        existing.MaxRssi = tagEvent.Rssi;
                    existing.LatestRssi = tagEvent.Rssi;
                    existing.SumRssi += tagEvent.Rssi;
                }
                return existing;
            });

        if (isNew)
        {
            resultToEmit = tagEvent;
            try
            {
                TagAppeared?.Invoke(this, tagEvent);
            }
            catch
            {
                // Suppress subscriber exceptions
            }
        }

        return new ValueTask<TagReadEvent?>(resultToEmit);
    }

    /// <summary>
    /// Clears all tracked tag entries in memory.
    /// </summary>
    public void Reset()
    {
        _entries.Clear();
    }

    private void OnCleanupTick(object? state)
    {
        DateTime now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _entries)
        {
            if (now - kvp.Value.LastSeenUtc > _options.SlidingWindow)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_entries.TryRemove(key, out var entry))
            {
                double finalRssi = _options.RssiMode switch
                {
                    RssiAggregationMode.Maximum => entry.MaxRssi,
                    RssiAggregationMode.Average => entry.Count > 0 ? entry.SumRssi / entry.Count : entry.LatestRssi,
                    _ => entry.LatestRssi
                };

                var finalEvent = new TagReadEvent(
                    entry.FirstEvent.Epc,
                    entry.FirstEvent.AntennaId,
                    finalRssi,
                    entry.FirstEvent.ReaderId,
                    entry.LastSeenUtc,
                    readCount: entry.Count,
                    tid: entry.FirstEvent.Tid);

                try
                {
                    TagDisappeared?.Invoke(this, finalEvent);
                }
                catch
                {
                    // Suppress subscriber exceptions
                }
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _entries.Clear();
    }
}
