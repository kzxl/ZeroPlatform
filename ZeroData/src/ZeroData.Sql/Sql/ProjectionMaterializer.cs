using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Materializes query result rows into projection types that Dapper cannot construct
    /// directly — primarily anonymous types, which have no parameterless constructor.
    /// Also normalizes provider type differences (e.g., SQLite returns Int64/Double for
    /// INTEGER/REAL columns) via <see cref="Convert.ChangeType(object, Type)"/>.
    /// </summary>
    internal static class ProjectionMaterializer
    {
        /// <summary>
        /// Returns true if the type must be materialized manually because it has no usable
        /// public parameterless constructor (the typical case for anonymous types).
        /// </summary>
        public static bool RequiresManualMaterialization(Type type)
        {
            if (type == null) return false;
            // Primitives, string, value types map fine through Dapper.
            if (type.IsPrimitive || type == typeof(string) || type.IsValueType) return false;
            return type.GetConstructor(Type.EmptyTypes) == null;
        }

        /// <summary>
        /// Materializes dynamic rows (Dapper DapperRow / IDictionary&lt;string, object&gt;)
        /// into TResult instances using the type's longest constructor, matching constructor
        /// parameter names to row column names (case-insensitive).
        /// </summary>
        public static List<TResult> Materialize<TResult>(IEnumerable<object> rows)
        {
            var type = typeof(TResult);
            var ctor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null)
                throw new InvalidOperationException(
                    $"Type '{type.Name}' has no public constructor for projection materialization.");

            var parameters = ctor.GetParameters();
            var results = new List<TResult>();

            foreach (var row in rows)
            {
                var lookup = ToCaseInsensitiveLookup(row);
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    lookup.TryGetValue(p.Name, out var value);
                    args[i] = ConvertValue(value, p.ParameterType);
                }
                results.Add((TResult)ctor.Invoke(args));
            }

            return results;
        }

        private static Dictionary<string, object> ToCaseInsensitiveLookup(object row)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (row is IDictionary<string, object> rowDict)
            {
                foreach (var kv in rowDict)
                    dict[kv.Key] = kv.Value;
            }
            return dict;
        }

        /// <summary>
        /// Converts a raw database value to the target CLR type, handling nullable types,
        /// enums, Guid, and provider numeric widening.
        /// </summary>
        internal static object ConvertValue(object value, Type targetType)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            bool isNullable = Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType;

            if (value == null || value is DBNull)
                return isNullable ? null : Activator.CreateInstance(underlying);

            if (underlying.IsInstanceOfType(value))
                return value;

            if (underlying.IsEnum)
                return Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture));

            if (underlying == typeof(Guid))
                return value is Guid g ? g : Guid.Parse(value.ToString());

            if (underlying == typeof(string))
                return value.ToString();

            try
            {
                return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException)
            {
                return value;
            }
        }
    }
}
