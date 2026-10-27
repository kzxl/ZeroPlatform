using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Templating
{
    /// <summary>
    /// Context container holding prompt variables and parameters.
    /// </summary>
    public sealed class PromptContext
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, object> Values => _values;

        public PromptContext Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            _values[key] = value;
            return this;
        }

        public bool TryGet(string key, out object value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }
}
