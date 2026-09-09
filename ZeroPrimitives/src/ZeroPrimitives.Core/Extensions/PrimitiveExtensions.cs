using System;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Extensions
{
    /// <summary>
    /// Fluent extension methods for fast, zero-allocation type conversions.
    /// Provides drop-in compatibility with legacy ERP ObjectExtension syntax.
    /// </summary>
    public static class PrimitiveExtensions
    {
        public static bool HasValue(this object? value)
        {
            if (value == null || value == DBNull.Value) return false;
            if (value is string str) return !string.IsNullOrWhiteSpace(str);
            if (value is bool || value is int || value is long || value is double || value is decimal) return true;
            return value.ToString()?.Trim().Length > 0;
        }

        public static int AsInt(this object? value, int defaultValue = 0)
            => FastConvert.AsInt(value, defaultValue);

        public static int? AsNullableInt(this object? value, int? defaultValue = null)
            => FastConvert.AsNullableInt(value, defaultValue);

        public static long AsLong(this object? value, long defaultValue = 0)
            => FastConvert.AsLong(value, defaultValue);

        public static long? AsNullableLong(this object? value, long? defaultValue = null)
            => FastConvert.AsNullableLong(value, defaultValue);

        public static decimal AsDecimal(this object? value, decimal defaultValue = 0m)
            => FastConvert.AsDecimal(value, defaultValue);

        public static decimal? AsNullableDecimal(this object? value, decimal? defaultValue = null)
            => FastConvert.AsNullableDecimal(value, defaultValue);

        public static double AsDouble(this object? value, double defaultValue = 0.0)
            => FastConvert.AsDouble(value, defaultValue);

        public static double? AsNullableDouble(this object? value, double? defaultValue = null)
            => FastConvert.AsNullableDouble(value, defaultValue);

        public static bool AsBool(this object? value, bool defaultValue = false)
            => FastConvert.AsBool(value, defaultValue);

        public static bool? AsNullableBool(this object? value, bool? defaultValue = null)
            => FastConvert.AsNullableBool(value, defaultValue);

        public static DateTime AsDate(this object? value, DateTime defaultValue = default)
            => FastConvert.AsDateTime(value, defaultValue);

        public static DateTime? AsNullableDate(this object? value, DateTime? defaultValue = null)
            => FastConvert.AsNullableDateTime(value, defaultValue);

        public static string AsString(this object? value, string defaultValue = "")
            => FastConvert.AsString(value, defaultValue);

        public static Guid AsGuid(this object? value, Guid defaultValue = default)
            => FastConvert.AsGuid(value, defaultValue);

        public static Guid? AsNullableGuid(this object? value, Guid? defaultValue = null)
            => FastConvert.AsNullableGuid(value, defaultValue);

        public static bool AsBoolean(this object? value, bool defaultValue = false)
            => FastConvert.AsBool(value, defaultValue);

        public static bool? AsNullableBoolean(this object? value, bool? defaultValue = null)
            => FastConvert.AsNullableBool(value, defaultValue);

        public static TEnum AsEnum<TEnum>(this object? value, TEnum defaultValue = default) where TEnum : struct, Enum
            => FastConvert.AsEnum(value, defaultValue);

        public static TEnum? AsNullableEnum<TEnum>(this object? value) where TEnum : struct, Enum
            => FastConvert.AsNullableEnum<TEnum>(value);

        public static string AsNumberString(this object? value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            decimal d = FastConvert.AsDecimal(value);
            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts to a SQL Server safe date string (yyyy-MM-dd) clamped between 1753 and 9999.
        /// </summary>
        public static string AsSqlDateString(this object? value)
        {
            if (value == null || value == DBNull.Value) return "NULL";
            var dt = FastConvert.AsNullableDateTime(value);
            if (dt.HasValue)
            {
                var clamped = dt.Value.EnsureSqlDateTime();
                return $"{clamped:yyyy-MM-dd}";
            }
            return "NULL";
        }
    }
}
