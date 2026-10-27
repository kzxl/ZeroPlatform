using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using ZeroPrimitives;

namespace ZeroData.Sql
{
    /// <summary>
    /// Dynamic parameter container for IDbCommand execution.
    /// Fully compatible drop-in replacement for Dapper's DynamicParameters.
    /// </summary>
    public class DynamicParameters : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public DynamicParameters() { }

        public DynamicParameters(object template)
        {
            if (template != null)
            {
                AddDynamicParams(template);
            }
        }

        public void Add(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _parameters[key] = value;
        }

        public void Add(string name, object value, DbType? dbType, ParameterDirection? direction = null, int? size = null)
        {
            if (string.IsNullOrEmpty(name)) return;
            _parameters[name] = value;
        }

        public void AddDynamicParams(object param)
        {
            if (param == null) return;

            if (param is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                    _parameters[kvp.Key] = kvp.Value;
            }
            else if (param is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var kvp in pairs)
                    _parameters[kvp.Key] = kvp.Value;
            }
            else
            {
                var props = param.GetType().GetProperties();
                foreach (var p in props)
                {
                    if (p.CanRead)
                        _parameters[p.Name] = p.GetValue(param, null);
                }
            }
        }

        public T Get<T>(string name)
        {
            if (_parameters.TryGetValue(name, out var val))
            {
                return FastConvert.To<T>(val);
            }
            return default;
        }

        public object this[string key]
        {
            get => _parameters[key];
            set => _parameters[key] = value;
        }

        public IEnumerable<string> ParameterNames => _parameters.Keys;
        public ICollection<string> Keys => _parameters.Keys;
        public ICollection<object> Values => _parameters.Values;
        public int Count => _parameters.Count;
        public bool IsReadOnly => false;

        public void Add(KeyValuePair<string, object> item) => _parameters.Add(item.Key, item.Value);
        public void Clear() => _parameters.Clear();
        public bool Contains(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_parameters).Contains(item);
        public bool ContainsKey(string key) => _parameters.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object>>)_parameters).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _parameters.GetEnumerator();
        public bool Remove(string key) => _parameters.Remove(key);
        public bool Remove(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_parameters).Remove(item);
        public bool TryGetValue(string key, out object value) => _parameters.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => _parameters.GetEnumerator();
    }
}
