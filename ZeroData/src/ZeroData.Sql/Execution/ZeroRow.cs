using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

namespace ZeroData.Sql.Execution
{
    /// <summary>
    /// Lightweight dynamic row representation for untyped SQL queries.
    /// Implements DynamicObject and IDictionary&lt;string, object&gt; for flexible access.
    /// </summary>
    public class ZeroRow : DynamicObject, IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _values;

        public ZeroRow()
        {
            _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public ZeroRow(int capacity)
        {
            _values = new Dictionary<string, object>(capacity, StringComparer.OrdinalIgnoreCase);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _values.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _values[binder.Name] = value;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames() => _values.Keys;

        #region IDictionary<string, object> Implementation

        public object this[string key]
        {
            get => _values.TryGetValue(key, out var val) ? val : null;
            set => _values[key] = value;
        }

        public ICollection<string> Keys => _values.Keys;
        public ICollection<object> Values => _values.Values;
        public int Count => _values.Count;
        public bool IsReadOnly => false;

        public void Add(string key, object value) => _values.Add(key, value);
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public bool Remove(string key) => _values.Remove(key);
        public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value);
        public void Clear() => _values.Clear();

        public void Add(KeyValuePair<string, object> item) => _values.Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_values).Contains(item);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object>>)_values).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_values).Remove(item);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

        #endregion
    }
}
