using System;
using System.Collections;
using System.Collections.Generic;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Ordered, case-preserving HTTP header collection with case-insensitive lookup.
    /// Header names and values are validated against CR/LF/NUL injection on public mutation paths,
    /// so the collection is safe to serialize by any transport (including raw socket transports).
    /// </summary>
    public sealed class ZeroHttpHeaders : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly List<KeyValuePair<string, string>> _items = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Gets the number of header entries (a header with multiple values counts once per value).
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Appends a header entry. Multiple entries with the same name are preserved in insertion order.
        /// </summary>
        /// <exception cref="ArgumentException">Name or value contains forbidden characters.</exception>
        public void Add(string name, string value)
        {
            ValidateName(name);
            ValidateValue(name, value);
            _items.Add(new KeyValuePair<string, string>(name, value));
        }

        /// <summary>
        /// Replaces all existing entries of <paramref name="name"/> with a single entry.
        /// </summary>
        public void Set(string name, string value)
        {
            ValidateName(name);
            ValidateValue(name, value);
            RemoveAll(name);
            _items.Add(new KeyValuePair<string, string>(name, value));
        }

        /// <summary>
        /// Removes all entries of <paramref name="name"/>.
        /// </summary>
        /// <returns><c>true</c> when at least one entry was removed.</returns>
        public bool Remove(string name)
        {
            return RemoveAll(name) > 0;
        }

        /// <summary>
        /// Determines whether at least one entry of <paramref name="name"/> exists.
        /// </summary>
        public bool Contains(string name)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Key, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the first value of <paramref name="name"/>, or <c>null</c> when absent.
        /// </summary>
        public string? Get(string name)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Key, name, StringComparison.OrdinalIgnoreCase)) return _items[i].Value;
            }
            return null;
        }

        /// <summary>
        /// Gets all values of <paramref name="name"/> in insertion order.
        /// </summary>
        public IEnumerable<string> GetValues(string name)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Key, name, StringComparison.OrdinalIgnoreCase)) yield return _items[i].Value;
            }
        }

        /// <summary>
        /// Appends an entry received from a trusted parser (e.g. BCL response headers) without validation.
        /// </summary>
        internal void AddWithoutValidation(string name, string value)
        {
            _items.Add(new KeyValuePair<string, string>(name, value ?? string.Empty));
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int RemoveAll(string name)
        {
            return _items.RemoveAll(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Header name must not be empty.", nameof(name));

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                // RFC 9110 token: visible ASCII except delimiters. Reject the characters that enable injection or break framing.
                if (c <= 0x20 || c >= 0x7F || c == ':' || c == '"' || c == '(' || c == ')' || c == ',' ||
                    c == '/' || c == ';' || c == '<' || c == '=' || c == '>' || c == '?' || c == '@' ||
                    c == '[' || c == '\\' || c == ']' || c == '{' || c == '}')
                {
                    throw new ArgumentException($"Header name '{name}' contains an invalid character at index {i}.", nameof(name));
                }
            }
        }

        private static void ValidateValue(string name, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\r' || c == '\n' || c == '\0')
                {
                    throw new ArgumentException($"Header '{name}' value contains a forbidden control character (CR, LF or NUL).", nameof(value));
                }
            }
        }
    }
}
