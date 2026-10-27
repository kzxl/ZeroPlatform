using System;
using System.Collections.Generic;

namespace ZeroLlm.Core.Format
{
    /// <summary>
    /// Key-value metadata table stored in GGUF model files.
    /// </summary>
    public sealed class GgufMetadata
    {
        private readonly Dictionary<string, object> _entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, object> Entries => _entries;

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            _entries[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_entries.TryGetValue(key, out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public T GetValueOrDefault<T>(string key, T defaultValue = default!)
        {
            if (TryGet<T>(key, out var val))
            {
                return val;
            }

            return defaultValue;
        }

        // Common architectural accessors
        public string Architecture => GetValueOrDefault("general.architecture", "llama");

        public uint ContextLength => GetArchValue<uint>("context_length", 2048);

        public uint EmbeddingLength => GetArchValue<uint>("embedding_length", 4096);

        public uint BlockCount => GetArchValue<uint>("block_count", 32);

        public uint FeedForwardLength => GetArchValue<uint>("feed_forward_length", 11008);

        public uint HeadCount => GetArchValue<uint>("attention.head_count", 32);

        public uint HeadCountKv => GetArchValue<uint>("attention.head_count_kv", HeadCount);

        public float RmsNormEpsilon => GetArchValue<float>("attention.layer_norm_rms_epsilon", 1e-5f);

        public float RopeFreqBase => GetArchValue<float>("rope.freq_base", 10000.0f);

        private T GetArchValue<T>(string suffix, T defaultValue)
        {
            var arch = Architecture;
            var key = $"{arch}.{suffix}";
            if (TryGet<T>(key, out var val))
            {
                return val;
            }

            return defaultValue;
        }
    }
}
