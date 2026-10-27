using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace ZeroData.Sql.Execution
{
    /// <summary>
    /// High-performance parameter binder for ADO.NET IDbCommand.
    /// Binds IDictionary, KeyValuePair collections, anonymous types, and POCO objects.
    /// </summary>
    public static class FastParameterBinder
    {
        private class CachedProperty
        {
            public string ParameterName { get; set; }
            public Func<object, object> Getter { get; set; }
        }

        private static readonly ConcurrentDictionary<Type, CachedProperty[]> PropertyCache
            = new ConcurrentDictionary<Type, CachedProperty[]>();

        /// <summary>
        /// Binds parameters to the specified IDbCommand.
        /// </summary>
        public static void Bind(IDbCommand command, object parameters, ValueConverterCollection converters = null)
        {
            if (parameters == null)
                return;

            if (parameters is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    AddParameter(command, kvp.Key, kvp.Value, converters);
                }
                return;
            }

            if (parameters is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var kvp in pairs)
                {
                    AddParameter(command, kvp.Key, kvp.Value, converters);
                }
                return;
            }

            if (parameters is IDictionary nonGenericDict)
            {
                foreach (DictionaryEntry entry in nonGenericDict)
                {
                    AddParameter(command, entry.Key?.ToString(), entry.Value, converters);
                }
                return;
            }

            // Object / Anonymous type
            var targetType = parameters.GetType();
            if (!PropertyCache.TryGetValue(targetType, out var props))
            {
                props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .Select(p =>
                    {
                        var propInfo = p;
                        return new CachedProperty
                        {
                            ParameterName = p.Name,
                            Getter = obj => propInfo.GetValue(obj, null)
                        };
                    })
                    .ToArray();

                PropertyCache[targetType] = props;
            }

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                var val = p.Getter(parameters);
                AddParameter(command, p.ParameterName, val, converters);
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value, ValueConverterCollection converters)
        {
            if (string.IsNullOrEmpty(name))
                return;

            var param = command.CreateParameter();
            param.ParameterName = name;

            if (value != null && converters != null && converters.HasConverter(value.GetType()))
            {
                value = converters.ConvertToDb(value, value.GetType());
            }

            if (value == null)
            {
                param.Value = DBNull.Value;
            }
            else
            {
                // Enums: if not custom converted, store underlying primitive or string
                if (value is Enum)
                {
                    param.Value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
                }
                else
                {
                    param.Value = value;
                }
            }

            command.Parameters.Add(param);
        }
    }
}
