using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ZeroStorage.Core.Indexing
{
    /// <summary>
    /// Metadata definition for a unique industrial time-series stream.
    /// Combines measurement name and multi-dimensional tag key-value pairs.
    /// </summary>
    public sealed class SeriesDefinition
    {
        public int SeriesId { get; }
        public string Measurement { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public string SeriesKey { get; }

        public SeriesDefinition(int seriesId, string measurement, IReadOnlyDictionary<string, string> tags)
        {
            SeriesId = seriesId;
            Measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
            Tags = tags ?? new Dictionary<string, string>();
            SeriesKey = BuildSeriesKey(measurement, Tags);
        }

        public static string BuildSeriesKey(string measurement, IReadOnlyDictionary<string, string> tags)
        {
            var sb = new StringBuilder(measurement);
            if (tags.Count > 0)
            {
                var sorted = new List<KeyValuePair<string, string>>(tags);
                sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

                for (int i = 0; i < sorted.Count; i++)
                {
                    sb.Append(',');
                    sb.Append(sorted[i].Key);
                    sb.Append('=');
                    sb.Append(sorted[i].Value);
                }
            }
            return sb.ToString();
        }

        public override string ToString() => SeriesKey;
    }

    /// <summary>
    /// High-throughput Inverted Tag Index (TSI) backed by FastRoaringBitmap for multi-dimensional series querying.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public sealed class TagInvertedIndex
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, int> _keyToId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, SeriesDefinition> _idToSeries = new Dictionary<int, SeriesDefinition>();
        private readonly Dictionary<string, FastRoaringBitmap> _tagIndex = new Dictionary<string, FastRoaringBitmap>(StringComparer.Ordinal);
        private readonly Dictionary<string, FastRoaringBitmap> _measurementIndex = new Dictionary<string, FastRoaringBitmap>(StringComparer.Ordinal);
        private int _nextSeriesId = 1;

        public int SeriesCount
        {
            get
            {
                lock (_syncRoot) return _idToSeries.Count;
            }
        }

        /// <summary>
        /// Registers a new time series or retrieves the existing SeriesId.
        /// </summary>
        public int GetOrRegisterSeries(string measurement, IReadOnlyDictionary<string, string> tags)
        {
            if (string.IsNullOrEmpty(measurement)) throw new ArgumentNullException(nameof(measurement));
            tags ??= new Dictionary<string, string>();

            string seriesKey = SeriesDefinition.BuildSeriesKey(measurement, tags);

            lock (_syncRoot)
            {
                if (_keyToId.TryGetValue(seriesKey, out int existingId))
                {
                    return existingId;
                }

                int seriesId = _nextSeriesId++;
                var series = new SeriesDefinition(seriesId, measurement, tags);
                _keyToId[seriesKey] = seriesId;
                _idToSeries[seriesId] = series;

                // Index by measurement
                if (!_measurementIndex.TryGetValue(measurement, out var mBitmap))
                {
                    mBitmap = new FastRoaringBitmap();
                    _measurementIndex[measurement] = mBitmap;
                }
                mBitmap.Add(seriesId);

                // Index by each tag key=value
                foreach (var kvp in tags)
                {
                    string tagToken = $"{kvp.Key}={kvp.Value}";
                    if (!_tagIndex.TryGetValue(tagToken, out var tBitmap))
                    {
                        tBitmap = new FastRoaringBitmap();
                        _tagIndex[tagToken] = tBitmap;
                    }
                    tBitmap.Add(seriesId);
                }

                return seriesId;
            }
        }

        /// <summary>
        /// Retrieves the series definition for a given SeriesId.
        /// </summary>
        public SeriesDefinition? GetSeries(int seriesId)
        {
            lock (_syncRoot)
            {
                _idToSeries.TryGetValue(seriesId, out var def);
                return def;
            }
        }

        /// <summary>
        /// Finds all series matching the specified tag key-value filters via fast bitmap intersections.
        /// </summary>
        public int[] FindSeries(params KeyValuePair<string, string>[] tagFilters)
        {
            return FindSeries(null, tagFilters);
        }

        /// <summary>
        /// Finds all series matching an optional measurement name and tag filters via fast bitmap intersections.
        /// </summary>
        public int[] FindSeries(string? measurement, params KeyValuePair<string, string>[] tagFilters)
        {
            lock (_syncRoot)
            {
                FastRoaringBitmap? resultBitmap = null;

                if (!string.IsNullOrEmpty(measurement))
                {
                    if (!_measurementIndex.TryGetValue(measurement!, out var mBitmap))
                    {
                        return Array.Empty<int>();
                    }
                    resultBitmap = mBitmap;
                }

                if (tagFilters != null && tagFilters.Length > 0)
                {
                    for (int i = 0; i < tagFilters.Length; i++)
                    {
                        string token = $"{tagFilters[i].Key}={tagFilters[i].Value}";
                        if (!_tagIndex.TryGetValue(token, out var tBitmap))
                        {
                            return Array.Empty<int>();
                        }

                        resultBitmap = resultBitmap == null ? tBitmap : resultBitmap.And(tBitmap);
                        if (resultBitmap.IsEmpty)
                        {
                            return Array.Empty<int>();
                        }
                    }
                }

                if (resultBitmap == null)
                {
                    var all = new int[_idToSeries.Count];
                    _idToSeries.Keys.CopyTo(all, 0);
                    Array.Sort(all);
                    return all;
                }

                return resultBitmap.ToArray();
            }
        }
    }
}
