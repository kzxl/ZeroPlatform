using System;
using System.Globalization;

namespace ZeroData.Sql.Execution
{
    /// <summary>
    /// High-performance cross-database type conversion bridge powered by ZeroPrimitives.FastConvert.
    /// Handles database-specific discrepancies (e.g. SQLite returning long for int, Oracle returning decimal for numbers)
    /// with zero allocations and register-level unboxing.
    /// </summary>
    public static class FastConvert
    {
        /// <summary>
        /// Converts a database value to the specified target type dynamically.
        /// Returns default(T) if value is null or DBNull.Value.
        /// </summary>
        public static object ChangeType(object value, Type targetType)
        {
            if (value == null || value is DBNull)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    return Activator.CreateInstance(targetType);
                }
                return null;
            }

            var sourceType = value.GetType();
            if (sourceType == targetType)
                return value;

            var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (sourceType == underlyingTarget)
                return value;

            // Direct mapping via ZeroPrimitives.FastConvert
            if (underlyingTarget == typeof(int)) return ZeroPrimitives.FastConvert.AsInt(value);
            if (underlyingTarget == typeof(long)) return ZeroPrimitives.FastConvert.AsLong(value);
            if (underlyingTarget == typeof(decimal)) return ZeroPrimitives.FastConvert.AsDecimal(value);
            if (underlyingTarget == typeof(double)) return ZeroPrimitives.FastConvert.AsDouble(value);
            if (underlyingTarget == typeof(float)) return ZeroPrimitives.FastConvert.ToFloat(value);
            if (underlyingTarget == typeof(bool)) return ZeroPrimitives.FastConvert.AsBool(value);
            if (underlyingTarget == typeof(string)) return ZeroPrimitives.FastConvert.AsString(value);
            if (underlyingTarget == typeof(DateTime)) return ZeroPrimitives.FastConvert.AsDateTime(value);
            if (underlyingTarget == typeof(Guid)) return ZeroPrimitives.FastConvert.AsGuid(value);
            if (underlyingTarget == typeof(short)) return (short)ZeroPrimitives.FastConvert.AsInt(value);
            if (underlyingTarget == typeof(byte)) return (byte)ZeroPrimitives.FastConvert.AsInt(value);

            if (underlyingTarget.IsEnum)
            {
                if (value is string str)
                {
                    return Enum.Parse(underlyingTarget, str, true);
                }
                return Enum.ToObject(underlyingTarget, ZeroPrimitives.FastConvert.AsLong(value));
            }

            if (underlyingTarget == typeof(DateTimeOffset))
            {
                if (value is DateTime dt) return new DateTimeOffset(dt);
                if (value is string s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                    return dto;
            }

            if (underlyingTarget == typeof(TimeSpan))
            {
                if (value is TimeSpan ts) return ts;
                if (value is string s && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var parsedTs))
                    return parsedTs;
                if (value is long ticks) return new TimeSpan(ticks);
            }

            return Convert.ChangeType(value, underlyingTarget, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generic strongly-typed conversion delegate calling ZeroPrimitives.FastConvert.To.
        /// </summary>
        public static T ChangeType<T>(object value)
        {
            if (value == null || value is DBNull)
                return default;

            return ZeroPrimitives.FastConvert.To<T>(value);
        }
    }
}
