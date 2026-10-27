using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents a value converter between a model type and a database type.
    /// </summary>
    public class ValueConverter
    {
        /// <summary>Model property type (e.g., enum, custom object)</summary>
        public Type ModelType { get; }

        /// <summary>Database column type (e.g., string, int)</summary>
        public Type DbType { get; }

        /// <summary>Converts model value → database value (for INSERT/UPDATE)</summary>
        public Func<object, object> ToDb { get; }

        /// <summary>Converts database value → model value (for SELECT/read)</summary>
        public Func<object, object> FromDb { get; }

        public ValueConverter(Type modelType, Type dbType,
            Func<object, object> toDb, Func<object, object> fromDb)
        {
            ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
            DbType = dbType ?? throw new ArgumentNullException(nameof(dbType));
            ToDb = toDb ?? throw new ArgumentNullException(nameof(toDb));
            FromDb = fromDb ?? throw new ArgumentNullException(nameof(fromDb));
        }
    }

    /// <summary>
    /// Registry of value converters for model⇄database type conversion.
    /// Converters are applied automatically during INSERT/UPDATE (ToDb)
    /// and during SELECT/read queries (FromDb).
    /// Thread-safe.
    /// </summary>
    public class ValueConverterCollection
    {
        /// <summary>
        /// Global shared converter collection for application-wide custom types.
        /// </summary>
        public static ValueConverterCollection Global { get; } = new ValueConverterCollection();

        private readonly ConcurrentDictionary<Type, ValueConverter> _converters
            = new ConcurrentDictionary<Type, ValueConverter>();

        /// <summary>
        /// Registers a value converter for the specified model type.
        /// Example: converters.Add&lt;OrderStatus, string&gt;(
        ///     toDb: v => v.ToString(),
        ///     fromDb: v => (OrderStatus)Enum.Parse(typeof(OrderStatus), (string)v))
        /// </summary>
        public void Add<TModel, TDb>(Func<TModel, TDb> toDb, Func<TDb, TModel> fromDb)
        {
            var converter = new ValueConverter(
                typeof(TModel), typeof(TDb),
                v => toDb((TModel)v),
                v => fromDb((TDb)v));
            _converters[typeof(TModel)] = converter;
        }

        /// <summary>
        /// Gets the converter for a model type, or null if none registered.
        /// </summary>
        public ValueConverter GetConverter(Type modelType)
        {
            if (_converters.TryGetValue(modelType, out var converter))
                return converter;
            return null;
        }

        /// <summary>
        /// Checks if a converter exists for the given type.
        /// </summary>
        public bool HasConverter(Type modelType)
        {
            return _converters.ContainsKey(modelType);
        }

        /// <summary>
        /// True if any converters are registered.
        /// </summary>
        public bool HasAny => !_converters.IsEmpty;

        /// <summary>
        /// Converts a model value to its database representation.
        /// Returns the original value if no converter is registered.
        /// </summary>
        public object ConvertToDb(object value, Type modelType)
        {
            if (value == null) return null;
            var converter = GetConverter(modelType);
            if (converter != null)
                return converter.ToDb(value);
            return value;
        }

        /// <summary>
        /// Converts a database value back to its model representation.
        /// Returns the original value if no converter is registered for the model type.
        /// </summary>
        public object ConvertFromDb(object value, Type modelType)
        {
            if (value == null) return null;
            var converter = GetConverter(modelType);
            if (converter != null)
                return converter.FromDb(value);
            return value;
        }

        /// <summary>
        /// Removes the converter for the specified type.
        /// </summary>
        public void Remove<TModel>()
        {
            _converters.TryRemove(typeof(TModel), out _);
        }

        /// <summary>
        /// Removes all converters.
        /// </summary>
        public void Clear()
        {
            _converters.Clear();
        }
    }
}
