using System;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Extensions
{
    /// <summary>
    /// Fluent extension methods for fast, zero-allocation type conversions.
    /// Follows .NET Framework Design Guidelines (To* naming) while preserving legacy As* aliases.
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

        #region Universal Generic Conversion

        /// <summary>
        /// Universal high-performance generic type converter.
        /// Unboxes primitives via CPU register casts with zero heap allocations.
        /// </summary>
        public static T To<T>(this object? value, T defaultValue = default!)
            => FastConvert.To<T>(value, defaultValue);

        /// <summary>
        /// Universal generic nullable type converter.
        /// </summary>
        public static T? ToNullable<T>(this object? value) where T : struct
            => FastConvert.ToNullable<T>(value);

        #endregion

        #region Standardized To* Conversions

        public static int ToInt(this object? value, int defaultValue = 0)
            => FastConvert.AsInt(value, defaultValue);

        public static int? ToNullableInt(this object? value, int? defaultValue = null)
            => FastConvert.AsNullableInt(value, defaultValue);

        public static long ToLong(this object? value, long defaultValue = 0)
            => FastConvert.AsLong(value, defaultValue);

        public static long? ToNullableLong(this object? value, long? defaultValue = null)
            => FastConvert.AsNullableLong(value, defaultValue);

        public static decimal ToDecimal(this object? value, decimal defaultValue = 0m)
            => FastConvert.AsDecimal(value, defaultValue);

        public static decimal? ToNullableDecimal(this object? value, decimal? defaultValue = null)
            => FastConvert.AsNullableDecimal(value, defaultValue);

        public static double ToDouble(this object? value, double defaultValue = 0.0)
            => FastConvert.AsDouble(value, defaultValue);

        public static double? ToNullableDouble(this object? value, double? defaultValue = null)
            => FastConvert.AsNullableDouble(value, defaultValue);

        public static float ToFloat(this object? value, float defaultValue = 0.0f)
            => (float)FastConvert.AsDouble(value, defaultValue);

        public static float? ToNullableFloat(this object? value, float? defaultValue = null)
        {
            var d = FastConvert.AsNullableDouble(value);
            return d.HasValue ? (float)d.Value : defaultValue;
        }

        public static bool ToBool(this object? value, bool defaultValue = false)
            => FastConvert.AsBool(value, defaultValue);

        public static bool? ToNullableBool(this object? value, bool? defaultValue = null)
            => FastConvert.AsNullableBool(value, defaultValue);

        public static DateTime ToDateTime(this object? value, DateTime defaultValue = default)
            => FastConvert.AsDateTime(value, defaultValue);

        public static DateTime? ToNullableDateTime(this object? value, DateTime? defaultValue = null)
            => FastConvert.AsNullableDateTime(value, defaultValue);

        public static DateTime ToDate(this object? value, DateTime defaultValue = default)
            => FastConvert.AsDateTime(value, defaultValue);

        public static DateTime? ToNullableDate(this object? value, DateTime? defaultValue = null)
            => FastConvert.AsNullableDateTime(value, defaultValue);

        public static string ToStringOrDefault(this object? value, string defaultValue = "")
            => FastConvert.AsString(value, defaultValue);

        public static string ToSafeString(this object? value, string defaultValue = "")
            => FastConvert.AsString(value, defaultValue);

        public static Guid ToGuid(this object? value, Guid defaultValue = default)
            => FastConvert.AsGuid(value, defaultValue);

        public static Guid? ToNullableGuid(this object? value, Guid? defaultValue = null)
            => FastConvert.AsNullableGuid(value, defaultValue);

        public static TEnum ToEnum<TEnum>(this object? value, TEnum defaultValue = default) where TEnum : struct, Enum
            => FastConvert.AsEnum(value, defaultValue);

        public static TEnum? ToNullableEnum<TEnum>(this object? value) where TEnum : struct, Enum
            => FastConvert.AsNullableEnum<TEnum>(value);

        public static string ToNumberString(this object? value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            decimal d = FastConvert.AsDecimal(value);
            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts to a SQL Server safe date string (yyyy-MM-dd) clamped between 1753 and 9999.
        /// </summary>
        public static string ToSqlDateString(this object? value)
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

        /// <summary>
        /// Formats date object to Vietnamese format: dd/MM/yyyy.
        /// </summary>
        public static string ToVnDateString(this object? value)
        {
            var dt = FastConvert.AsNullableDateTime(value);
            return dt.HasValue ? dt.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>
        /// Formats date object to Vietnamese format with time: dd/MM/yyyy HH:mm:ss.
        /// </summary>
        public static string ToVnDateTimeString(this object? value)
        {
            var dt = FastConvert.AsNullableDateTime(value);
            return dt.HasValue ? dt.Value.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        #endregion

        #region Legacy As* Aliases (100% Backward Compatibility)

        public static int AsInt(this object? value, int defaultValue = 0) => ToInt(value, defaultValue);
        public static int? AsNullableInt(this object? value, int? defaultValue = null) => ToNullableInt(value, defaultValue);

        public static long AsLong(this object? value, long defaultValue = 0) => ToLong(value, defaultValue);
        public static long? AsNullableLong(this object? value, long? defaultValue = null) => ToNullableLong(value, defaultValue);

        public static decimal AsDecimal(this object? value, decimal defaultValue = 0m) => ToDecimal(value, defaultValue);
        public static decimal? AsNullableDecimal(this object? value, decimal? defaultValue = null) => ToNullableDecimal(value, defaultValue);

        public static double AsDouble(this object? value, double defaultValue = 0.0) => ToDouble(value, defaultValue);
        public static double? AsNullableDouble(this object? value, double? defaultValue = null) => ToNullableDouble(value, defaultValue);

        public static bool AsBool(this object? value, bool defaultValue = false) => ToBool(value, defaultValue);
        public static bool? AsNullableBool(this object? value, bool? defaultValue = null) => ToNullableBool(value, defaultValue);

        public static bool AsBoolean(this object? value, bool defaultValue = false) => ToBool(value, defaultValue);
        public static bool? AsNullableBoolean(this object? value, bool? defaultValue = null) => ToNullableBool(value, defaultValue);

        public static DateTime AsDate(this object? value, DateTime defaultValue = default) => ToDateTime(value, defaultValue);
        public static DateTime? AsNullableDate(this object? value, DateTime? defaultValue = null) => ToNullableDateTime(value, defaultValue);

        public static string AsString(this object? value, string defaultValue = "") => ToStringOrDefault(value, defaultValue);

        public static Guid AsGuid(this object? value, Guid defaultValue = default) => ToGuid(value, defaultValue);
        public static Guid? AsNullableGuid(this object? value, Guid? defaultValue = null) => ToNullableGuid(value, defaultValue);

        public static TEnum AsEnum<TEnum>(this object? value, TEnum defaultValue = default) where TEnum : struct, Enum
            => ToEnum(value, defaultValue);
        public static TEnum? AsNullableEnum<TEnum>(this object? value) where TEnum : struct, Enum
            => ToNullableEnum<TEnum>(value);

        public static string AsNumberString(this object? value) => ToNumberString(value);
        public static string AsSqlDateString(this object? value) => ToSqlDateString(value);

        #endregion
    }
}
