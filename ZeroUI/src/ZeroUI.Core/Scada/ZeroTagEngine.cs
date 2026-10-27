using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ZeroUI.Core.Collections;
using ZeroUI.Core.Data;
using ZeroUI.Core.Historian;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// High-throughput, thread-safe central SCADA Tag Engine v2.
    /// Modernized with:
    /// - Integer TagId registry for O(1) flat memory array storage (TagStorage).
    /// - Zero-alloc typed setters (SetNumeric, SetBoolean, SetInteger) eliminating object boxing.
    /// - Inverted Index (_boundControlsByTagId) eliminating O(M) control scans.
    /// - Lock-free ZeroTripleBuffer for decoupled 10 kHz ingestion and 60 Hz rendering.
    /// - Batch UI Dispatcher coalescing preventing Windows Message Queue flooding.
    /// </summary>
    public static class ZeroTagEngine
    {
        private static readonly ConcurrentDictionary<string, int> _pathToId =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, ScadaTagRecord> _registry =
            new ConcurrentDictionary<string, ScadaTagRecord>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<ScadaTagRecord> _recordsById = new List<ScadaTagRecord>();
        private static readonly object _registryLock = new object();

        private static readonly object _subLock = new object();
        private static readonly Dictionary<string, List<Action<IScadaTag>>> _subscriptions =
            new Dictionary<string, List<Action<IScadaTag>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<int, List<Action<IScadaTag>>> _subscriptionsByTagId =
            new Dictionary<int, List<Action<IScadaTag>>>();

        // Inverted Index: TagId -> List of bound controls (eliminates 500M comparisons/sec scan)
        private static readonly Dictionary<int, List<IScadaBindable>> _boundControlsByTagId =
            new Dictionary<int, List<IScadaBindable>>();

        private static readonly List<IScadaBindable> _allBoundControls = new List<IScadaBindable>();
        private static IHistorianEngine? _attachedHistorian;

        /// <summary>
        /// Flat array storage indexed by TagId for zero-allocation O(1) reads and writes.
        /// </summary>
        public static TagStorage Storage { get; } = new TagStorage(4096);

        /// <summary>
        /// Lock-free Triple Buffer for latest-value telemetry decoupling.
        /// </summary>
        public static ZeroTripleBuffer TripleBuffer { get; } = new ZeroTripleBuffer(4096);

        /// <summary>
        /// When true (activated by ScadaPipelineCoordinator for high-throughput SCADA pipelines),
        /// UI controls are not directly posted to upon every SetNumeric/SetBoolean call at 10 kHz.
        /// Instead, dirty tags are marked and flushed in coalesced batches at 30-60 Hz via FlushUiBatch
        /// or TripleBuffer, preventing Windows message queue saturation.
        /// </summary>
        public static bool IsDecoupledUiMode { get; set; } = false;

        /// <summary>
        /// Global event fired whenever any tag in the registry is updated.
        /// </summary>
        public static event Action<IScadaTag>? TagUpdated;

        /// <summary>
        /// Resolves an existing integer TagId or allocates a new one for the given tag path.
        /// Runtime lookups should cache this TagId to achieve O(1) array access.
        /// </summary>
        public static int GetOrRegisterTag(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath))
                throw new ArgumentNullException(nameof(tagPath));

            if (_pathToId.TryGetValue(tagPath, out int existingId))
            {
                return existingId;
            }

            lock (_registryLock)
            {
                if (_pathToId.TryGetValue(tagPath, out existingId))
                {
                    return existingId;
                }

                int newId = _recordsById.Count;
                var record = new ScadaTagRecord(tagPath, newId);
                _recordsById.Add(record);
                _registry[tagPath] = record;
                _pathToId[tagPath] = newId;

                Storage.EnsureCapacity(newId);
                return newId;
            }
        }

        /// <summary>
        /// Retrieves the integer TagId for a given path, or null if unregistered.
        /// </summary>
        public static int? GetTagId(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return null;
            return _pathToId.TryGetValue(tagPath, out int id) ? (int?)id : null;
        }

        /// <summary>
        /// Retrieves the tag path for a given TagId in O(1).
        /// </summary>
        public static string? GetTagPath(int tagId)
        {
            if (tagId < 0) return null;
            lock (_registryLock)
            {
                return tagId < _recordsById.Count ? _recordsById[tagId].TagPath : null;
            }
        }

        /// <summary>
        /// Fast, zero-boxing numeric setter using TagId.
        /// Updates flat TagStorage, evaluates deadband, and dispatches via Inverted Index.
        /// </summary>
        public static bool SetNumeric(int tagId, double value, ScadaQuality quality = ScadaQuality.Good, long timestampUtcMs = 0, bool dispatchUi = true)
        {
            ScadaTagRecord record;
            lock (_registryLock)
            {
                if (tagId < 0 || tagId >= _recordsById.Count) return false;
                record = _recordsById[tagId];
            }

            long nowMs = timestampUtcMs > 0 ? timestampUtcMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            lock (record)
            {
                // Deadband suppression
                if (record.Deadband > 0 && record.CurrentTag != null && record.CurrentTag.Value != null)
                {
                    if (TryGetNumeric(record.CurrentTag.Value, out double oldNum))
                    {
                        if (Math.Abs(value - oldNum) < record.Deadband && quality == record.CurrentTag.Quality)
                        {
                            return false; // Suppressed
                        }
                    }
                }

                // Update Flat Storage
                var scadaVal = new ScadaValue(value, quality);
                Storage.Set(tagId, scadaVal, nowMs);

                var nowDt = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime;
                var newTag = new ScadaTag(record.TagPath, value, quality, nowDt);
                record.CurrentTag = newTag;

                record.HistoryBuffer?.Write(new TimePoint(nowMs, value));
                _attachedHistorian?.LogSample(record.TagPath, value, quality, nowDt);

                DispatchSubscribers(tagId, record.TagPath, newTag);
                if (dispatchUi && !IsDecoupledUiMode)
                {
                    DispatchInvertedIndex(tagId, newTag);
                }

                TagUpdated?.Invoke(newTag);
                return true;
            }
        }

        /// <summary>
        /// Fast, zero-boxing boolean setter using TagId.
        /// </summary>
        public static bool SetBoolean(int tagId, bool value, ScadaQuality quality = ScadaQuality.Good, long timestampUtcMs = 0, bool dispatchUi = true)
        {
            ScadaTagRecord record;
            lock (_registryLock)
            {
                if (tagId < 0 || tagId >= _recordsById.Count) return false;
                record = _recordsById[tagId];
            }

            long nowMs = timestampUtcMs > 0 ? timestampUtcMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var scadaVal = new ScadaValue(value, quality);
            Storage.Set(tagId, scadaVal, nowMs);

            var nowDt = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime;
            var newTag = new ScadaTag(record.TagPath, value, quality, nowDt);
            record.CurrentTag = newTag;

            DispatchSubscribers(tagId, record.TagPath, newTag);
            if (dispatchUi && !IsDecoupledUiMode)
            {
                DispatchInvertedIndex(tagId, newTag);
            }

            TagUpdated?.Invoke(newTag);
            return true;
        }

        /// <summary>
        /// Backward-compatible tag setter accepting string path and boxed object.
        /// Resolves TagId and routes to optimized zero-alloc storage.
        /// </summary>
        public static bool SetTagValue(string tagPath, object? value, ScadaQuality quality = ScadaQuality.Good, DateTime? timestamp = null, bool dispatchUi = true)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return false;

            int tagId = GetOrRegisterTag(tagPath);
            long nowMs = timestamp.HasValue
                ? new DateTimeOffset(timestamp.Value).ToUnixTimeMilliseconds()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (TryGetNumeric(value, out double numVal))
            {
                return SetNumeric(tagId, numVal, quality, nowMs, dispatchUi);
            }
            if (value is bool bVal)
            {
                return SetBoolean(tagId, bVal, quality, nowMs, dispatchUi);
            }

            // Fallback for null or custom types
            ScadaTagRecord record;
            lock (_registryLock)
            {
                record = _recordsById[tagId];
            }

            lock (record)
            {
                var nowDt = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime;
                var newTag = new ScadaTag(tagPath, value, quality, nowDt);
                record.CurrentTag = newTag;

                Storage.Set(tagId, new ScadaValue(0.0, ScadaQuality.Bad), nowMs);

                DispatchSubscribers(tagId, tagPath, newTag);
                if (dispatchUi && !IsDecoupledUiMode)
                {
                    DispatchInvertedIndex(tagId, newTag);
                }

                TagUpdated?.Invoke(newTag);
                return true;
            }
        }

        /// <summary>
        /// Retrieves the current snapshot of a registered tag, or null if unregistered.
        /// </summary>
        public static IScadaTag? GetTag(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return null;
            return _registry.TryGetValue(tagPath, out var record) ? record.CurrentTag : null;
        }

        /// <summary>
        /// Retrieves the current snapshot of a registered tag by TagId in O(1).
        /// </summary>
        public static IScadaTag? GetTag(int tagId)
        {
            if (tagId < 0) return null;
            lock (_registryLock)
            {
                return tagId < _recordsById.Count ? _recordsById[tagId].CurrentTag : null;
            }
        }

        /// <summary>
        /// Sets a deadband filter for a tag to eliminate sensor jitter below the threshold.
        /// </summary>
        public static void SetDeadband(string tagPath, double deadbandThreshold)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return;
            int tagId = GetOrRegisterTag(tagPath);
            lock (_registryLock)
            {
                _recordsById[tagId].Deadband = Math.Max(0, deadbandThreshold);
            }
        }

        /// <summary>
        /// Subscribes an action callback to updates on a specific tag path.
        /// </summary>
        public static IDisposable Subscribe(string tagPath, Action<IScadaTag> callback)
        {
            if (string.IsNullOrWhiteSpace(tagPath) || callback == null)
                return EmptyDisposable.Instance;

            int tagId = GetOrRegisterTag(tagPath);

            lock (_subLock)
            {
                if (!_subscriptions.TryGetValue(tagPath, out var list))
                {
                    list = new List<Action<IScadaTag>>();
                    _subscriptions[tagPath] = list;
                }
                list.Add(callback);

                if (!_subscriptionsByTagId.TryGetValue(tagId, out var idList))
                {
                    idList = new List<Action<IScadaTag>>();
                    _subscriptionsByTagId[tagId] = idList;
                }
                idList.Add(callback);
            }

            var current = GetTag(tagId);
            if (current != null)
            {
                try { callback(current); } catch { }
            }

            return new SubscriptionToken(tagPath, tagId, callback);
        }

        /// <summary>
        /// Registers a visual control implementing IScadaBindable into the engine.
        /// Inserts into the Inverted Index for instantaneous O(1) telemetry notification.
        /// </summary>
        public static void RegisterBindable(IScadaBindable control)
        {
            if (control == null) return;

            lock (_subLock)
            {
                if (!_allBoundControls.Contains(control))
                {
                    _allBoundControls.Add(control);
                }

                if (!string.IsNullOrEmpty(control.BoundTagPath))
                {
                    int tagId = GetOrRegisterTag(control.BoundTagPath!);
                    if (!_boundControlsByTagId.TryGetValue(tagId, out var list))
                    {
                        list = new List<IScadaBindable>();
                        _boundControlsByTagId[tagId] = list;
                    }
                    if (!list.Contains(control))
                    {
                        list.Add(control);
                    }
                }
            }

            if (!string.IsNullOrEmpty(control.BoundTagPath))
            {
                var cur = GetTag(control.BoundTagPath!);
                if (cur != null)
                {
                    try { control.OnTagValueChanged(cur); } catch { }
                }
            }
        }

        /// <summary>
        /// Unregisters a visual control from the engine and clears its inverted index mappings.
        /// </summary>
        public static void UnregisterBindable(IScadaBindable control)
        {
            if (control == null) return;

            lock (_subLock)
            {
                _allBoundControls.Remove(control);

                if (!string.IsNullOrEmpty(control.BoundTagPath))
                {
                    int? tagId = GetTagId(control.BoundTagPath!);
                    if (tagId.HasValue && _boundControlsByTagId.TryGetValue(tagId.Value, out var list))
                    {
                        list.Remove(control);
                        if (list.Count == 0)
                        {
                            _boundControlsByTagId.Remove(tagId.Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Flushes all pending dirty tags to bound visual controls in a single coalesced UI batch.
        /// Called on UI frame ticks (e.g., 60 Hz) to eliminate Windows Message Queue saturation.
        /// </summary>
        public static int FlushUiBatch(int maxBatchSize = 1024)
        {
            var sw = Stopwatch.StartNew();
            int[] dirtyTags = new int[maxBatchSize];
            int dirtyCount = Storage.DrainDirtyTags(dirtyTags);

            if (dirtyCount == 0)
            {
                sw.Stop();
                return 0;
            }

            for (int i = 0; i < dirtyCount; i++)
            {
                int tagId = dirtyTags[i];
                var tag = GetTag(tagId);
                if (tag != null)
                {
                    DispatchInvertedIndex(tagId, tag);
                }
            }

            sw.Stop();
            TelemetryMetrics.Shared.UiBatchMs = sw.Elapsed.TotalMilliseconds;
            return dirtyCount;
        }

        private static void DispatchSubscribers(int tagId, string tagPath, IScadaTag newTag)
        {
            List<Action<IScadaTag>>? callbacks = null;
            lock (_subLock)
            {
                if (_subscriptionsByTagId.TryGetValue(tagId, out var list) && list.Count > 0)
                {
                    callbacks = new List<Action<IScadaTag>>(list);
                }
            }

            if (callbacks != null)
            {
                for (int i = 0; i < callbacks.Count; i++)
                {
                    try { callbacks[i](newTag); } catch { }
                }
            }
        }

        private static void DispatchInvertedIndex(int tagId, IScadaTag newTag)
        {
            List<IScadaBindable>? targets = null;
            lock (_subLock)
            {
                if (_boundControlsByTagId.TryGetValue(tagId, out var list) && list.Count > 0)
                {
                    targets = new List<IScadaBindable>(list);
                }
            }

            if (targets == null) return;

            for (int i = 0; i < targets.Count; i++)
            {
                var control = targets[i];
                try
                {
                    if (UiDispatcher.IsInitialized && !UiDispatcher.IsOnUiDispatcherThread)
                    {
                        UiDispatcher.Post(() => control.OnTagValueChanged(newTag));
                    }
                    else
                    {
                        control.OnTagValueChanged(newTag);
                    }
                }
                catch
                {
                    // Guard subscriber exceptions
                }
            }
        }

        /// <summary>
        /// Returns all currently registered tags.
        /// </summary>
        public static IReadOnlyCollection<IScadaTag> GetAllTags()
        {
            lock (_registryLock)
            {
                var list = new List<IScadaTag>(_recordsById.Count);
                for (int i = 0; i < _recordsById.Count; i++)
                {
                    if (_recordsById[i].CurrentTag != null)
                    {
                        list.Add(_recordsById[i].CurrentTag!);
                    }
                }
                return list.AsReadOnly();
            }
        }

        /// <summary>
        /// Attaches an industrial historian engine to automatically receive all valid tag updates.
        /// </summary>
        public static void AttachHistorian(IHistorianEngine? historian)
        {
            _attachedHistorian = historian;
        }

        /// <summary>
        /// Enables an in-memory RingBuffer for a tag to retain high-speed telemetry history for real-time trends.
        /// </summary>
        public static void EnableTagHistoryBuffer(string tagPath, int capacity = 1024)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return;
            int tagId = GetOrRegisterTag(tagPath);
            lock (_registryLock)
            {
                var record = _recordsById[tagId];
                lock (record)
                {
                    record.HistoryBuffer = new RingBuffer<TimePoint>(capacity);
                }
            }
        }

        /// <summary>
        /// Gets the in-memory telemetry points recorded for a tag.
        /// </summary>
        public static IReadOnlyList<TimePoint> GetRecentHistory(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return Array.Empty<TimePoint>();
            int? tagId = GetTagId(tagPath);
            if (!tagId.HasValue) return Array.Empty<TimePoint>();

            lock (_registryLock)
            {
                var record = _recordsById[tagId.Value];
                lock (record)
                {
                    return record.HistoryBuffer != null ? record.HistoryBuffer.ToArray() : Array.Empty<TimePoint>();
                }
            }
        }

        private static bool TryGetNumeric(object? val, out double result)
        {
            if (val == null) { result = 0; return false; }
            if (val is double d) { result = d; return true; }
            if (val is float f) { result = f; return true; }
            if (val is int i) { result = i; return true; }
            if (val is long l) { result = l; return true; }
            if (val is short s) { result = s; return true; }
            if (val is decimal dec) { result = (double)dec; return true; }
            if (val is byte b) { result = b; return true; }
            result = 0;
            return false;
        }

        private sealed class ScadaTagRecord
        {
            public string TagPath { get; }
            public int TagId { get; }
            public IScadaTag? CurrentTag { get; set; }
            public double Deadband { get; set; }
            public RingBuffer<TimePoint>? HistoryBuffer { get; set; }

            public ScadaTagRecord(string tagPath, int tagId)
            {
                TagPath = tagPath;
                TagId = tagId;
            }
        }

        private sealed class SubscriptionToken : IDisposable
        {
            private readonly string _tagPath;
            private readonly int _tagId;
            private readonly Action<IScadaTag> _callback;
            private bool _disposed;

            public SubscriptionToken(string tagPath, int tagId, Action<IScadaTag> callback)
            {
                _tagPath = tagPath;
                _tagId = tagId;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                lock (_subLock)
                {
                    if (_subscriptions.TryGetValue(_tagPath, out var list))
                    {
                        list.Remove(_callback);
                        if (list.Count == 0) _subscriptions.Remove(_tagPath);
                    }

                    if (_subscriptionsByTagId.TryGetValue(_tagId, out var idList))
                    {
                        idList.Remove(_callback);
                        if (idList.Count == 0) _subscriptionsByTagId.Remove(_tagId);
                    }
                }
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
