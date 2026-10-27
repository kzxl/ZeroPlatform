using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroDocuments.Common
{
    /// <summary>
    /// High-performance property accessor cache powered by compiled Expression Trees.
    /// Eliminates reflection overhead (30x-50x faster than PropertyInfo.GetValue/SetValue).
    /// Fully compatible with .NET Standard 2.0, .NET 4.6.2, and .NET 8.0+.
    /// </summary>
    public static class PropertyAccessorCache
    {
        private static readonly ConcurrentDictionary<Type, PropertyAccessorInfo[]> TypeAccessorsCache =
            new ConcurrentDictionary<Type, PropertyAccessorInfo[]>();

        /// <summary>
        /// Represents a fast compiled getter and setter for a specific property.
        /// </summary>
        public sealed class PropertyAccessorInfo
        {
            public string Name { get; }
            public Type PropertyType { get; }
            public Func<object, object?> Getter { get; }
            public Action<object, object?>? Setter { get; }

            public PropertyAccessorInfo(string name, Type propertyType, Func<object, object?> getter, Action<object, object?>? setter)
            {
                Name = name;
                PropertyType = propertyType;
                Getter = getter;
                Setter = setter;
            }
        }

        /// <summary>
        /// Gets all public instance property accessors for the specified type.
        /// Results are compiled once and cached permanently.
        /// </summary>
        public static PropertyAccessorInfo[] GetAccessors(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            return TypeAccessorsCache.GetOrAdd(type, t =>
            {
                var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var list = new List<PropertyAccessorInfo>(properties.Length);

                foreach (var prop in properties)
                {
                    if (prop.GetIndexParameters().Length > 0) continue; // Skip indexed properties

                    var getter = prop.CanRead ? CreateGetter(t, prop) : null;
                    if (getter == null) continue;

                    var setter = prop.CanWrite ? CreateSetter(t, prop) : null;
                    list.Add(new PropertyAccessorInfo(prop.Name, prop.PropertyType, getter, setter));
                }

                return list.ToArray();
            });
        }

        /// <summary>
        /// Creates a compiled getter delegate: (object instance) => (object?)instance.Property
        /// </summary>
        private static Func<object, object?> CreateGetter(Type type, PropertyInfo property)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(instanceParam, type);
            var propertyAccess = Expression.Property(castInstance, property);
            var castResult = Expression.Convert(propertyAccess, typeof(object));

            return Expression.Lambda<Func<object, object?>>(castResult, instanceParam).Compile();
        }

        /// <summary>
        /// Creates a compiled setter delegate: (object instance, object? value) => instance.Property = (PropertyType)value
        /// </summary>
        private static Action<object, object?>? CreateSetter(Type type, PropertyInfo property)
        {
            var setMethod = property.GetSetMethod();
            if (setMethod == null) return null;

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            // Value types: Unbox yields a reference into the boxed instance so the mutation is not lost on a copy.
            Expression castInstance = type.IsValueType
                ? Expression.Unbox(instanceParam, type)
                : Expression.Convert(instanceParam, type);
            var castValue = Expression.Convert(valueParam, property.PropertyType);
            var call = Expression.Call(castInstance, setMethod, castValue);

            return Expression.Lambda<Action<object, object?>>(call, instanceParam, valueParam).Compile();
        }
    }
}
