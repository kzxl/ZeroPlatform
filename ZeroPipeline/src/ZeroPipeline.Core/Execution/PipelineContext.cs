using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ZeroPipeline.Core.Execution
{
    /// <summary>
    /// Execution context passed to pipeline nodes during initialization and execution cycles.
    /// Provides thread-safe metadata exchange, cancellation tokens, and cycle identifiers.
    /// </summary>
    public sealed class PipelineContext
    {
        private readonly ConcurrentDictionary<string, object> _properties;

        public CancellationToken CancellationToken { get; }
        public long CycleId { get; set; }
        public DateTime TimestampUtc { get; set; }

        public PipelineContext(CancellationToken cancellationToken = default)
        {
            _properties = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            CancellationToken = cancellationToken;
            TimestampUtc = DateTime.UtcNow;
            CycleId = 0;
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (value == null)
            {
                _properties.TryRemove(key, out _);
            }
            else
            {
                _properties[key] = value;
            }
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (_properties.TryGetValue(key, out object? obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public T GetOrCreate<T>(string key, Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return (T)_properties.GetOrAdd(key, _ => factory()!);
        }

        public void ClearProperties()
        {
            _properties.Clear();
        }
    }
}
