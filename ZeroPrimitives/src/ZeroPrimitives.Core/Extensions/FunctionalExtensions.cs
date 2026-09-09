using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace ZeroPrimitives.Extensions
{
    /// <summary>
    /// Functional pipeline, Task wrappers, and object sanitation utilities.
    /// </summary>
    public static class FunctionalExtensions
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> NullStringPropsCache =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// Executes a mapping function if the object is not null.
        /// </summary>
        public static TResult? Let<T, TResult>(this T? obj, Func<T, TResult> func)
        {
            if (obj == null) return default;
            return func(obj);
        }

        /// <summary>
        /// Executes an action if the object is not null.
        /// </summary>
        public static void Let<T>(this T? obj, Action<T> action)
        {
            if (obj != null) action(obj);
        }

        /// <summary>
        /// Wraps any value into an already-completed Task.
        /// </summary>
        public static Task<T> AsTask<T>(this T value)
            => Task.FromResult(value);

        /// <summary>
        /// Sets all null public string properties to empty string ("").
        /// Useful prior to SQL database inserts with NOT NULL columns.
        /// </summary>
        public static T? ReplaceNullStrings<T>(this T? obj) where T : class
        {
            if (obj == null) return null;

            var type = obj.GetType();
            if (!NullStringPropsCache.TryGetValue(type, out var props))
            {
                var allProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var list = new System.Collections.Generic.List<PropertyInfo>();
                foreach (var p in allProps)
                {
                    if (p.PropertyType == typeof(string) && p.CanRead && p.CanWrite)
                    {
                        list.Add(p);
                    }
                }
                props = list.ToArray();
                NullStringPropsCache[type] = props;
            }

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                if (prop.GetValue(obj, null) == null)
                {
                    prop.SetValue(obj, string.Empty, null);
                }
            }

            return obj;
        }
    }
}
