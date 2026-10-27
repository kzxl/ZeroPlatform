using System;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Text;

namespace ZeroPrimitives
{
    /// <summary>
    /// Sovereign, zero-allocation type conversion engine.
    /// Replaces slow Convert.To* and TryParse(.ToString()) with direct register unboxing and pointer/span parsers.
    /// </summary>
    public static class FastConvert
    {
        #region Integer (32-bit)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AsInt(object? value, int defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            // Direct register unbox - 0 allocation
            if (value is int i) return i;

            // Highly frequent in enterprise applications (DB, JSON, WebAPI, CSV)
            if (value is string str)
            {
                if (FastNumberParser.TryParseInt32(str.AsSpan(), out int res, defaultValue))
                    return res;
                return defaultValue;
            }

            if (value is long l) return (int)l;
            if (value is decimal m) return (int)m;
            if (value is double d) return (int)d;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is float f) return (int)f;
            if (value is bool boolean) return boolean ? 1 : 0;
            if (value is uint ui) return (int)ui;
            if (value is ulong ul) return (int)ul;
            if (value is ushort us) return us;
            if (value is sbyte sb) return sb;

            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int? AsNullableInt(object? value, int? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is int i) return i;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseInt32(span, out int res))
                    return res;
                return defaultValue;
            }

            if (value is long l) return (int)l;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (int)m;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (value is bool boolean) return boolean ? 1 : 0;

            return defaultValue;
        }

        #endregion

        #region Long (64-bit)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AsLong(object? value, long defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is long l) return l;

            if (value is string str)
            {
                if (FastNumberParser.TryParseInt64(str.AsSpan(), out long res, defaultValue))
                    return res;
                return defaultValue;
            }

            if (value is int i) return i;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (long)m;
            if (value is double d) return (long)d;
            if (value is float f) return (long)f;
            if (value is ulong ul) return (long)ul;
            if (value is uint ui) return ui;
            if (value is bool boolean) return boolean ? 1L : 0L;

            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long? AsNullableLong(object? value, long? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is long l) return l;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseInt64(span, out long res))
                    return res;
                return defaultValue;
            }

            if (value is int i) return i;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (long)m;
            if (value is double d) return (long)d;
            if (value is float f) return (long)f;

            return defaultValue;
        }

        #endregion

        #region Decimal

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal AsDecimal(object? value, decimal defaultValue = 0m)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is decimal m) return m;

            if (value is string str)
            {
                if (FastNumberParser.TryParseDecimal(str.AsSpan(), out decimal res, defaultValue))
                    return res;
                return defaultValue;
            }

            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is uint ui) return ui;
            if (value is ulong ul) return ul;

            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal? AsNullableDecimal(object? value, decimal? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is decimal m) return m;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseDecimal(span, out decimal res))
                    return res;
                return defaultValue;
            }

            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is short s) return s;
            if (value is byte b) return b;

            return defaultValue;
        }

        /// <summary>
        /// Safely converts an object to decimal and rounds it to the specified decimal places.
        /// Eliminates string allocation (value.ToString()) by unboxing directly or parsing spans.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Round(object? value, int decimalPlaces, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            if (value == null || value == DBNull.Value) return 0m;
            decimal dec = AsDecimal(value, 0m);
            return Math.Round(dec, decimalPlaces, mode);
        }

        #endregion

        #region Double & Float

        public static double AsDouble(object? value, double defaultValue = 0.0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is double d) return d;
            if (value is float f) return f;
            if (value is decimal m) return (double)m;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is short s) return s;
            if (value is byte b) return b;

            if (value is string str)
            {
                if (FastNumberParser.TryParseDouble(str.AsSpan(), out double res, defaultValue))
                    return res;
                return defaultValue;
            }

            return defaultValue;
        }

        public static double? AsNullableDouble(object? value, double? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is double d) return d;
            if (value is float f) return f;
            if (value is decimal m) return (double)m;
            if (value is int i) return i;
            if (value is long l) return l;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseDouble(span, out double res))
                    return res;
            }

            return defaultValue;
        }

        #endregion

        #region Boolean

        public static bool AsBool(object? value, bool defaultValue = false)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is bool b) return b;
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            if (value is byte by) return by != 0;
            if (value is short s) return s != 0;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;

                if (span.Equals("true".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                    span.Equals("1".AsSpan(), StringComparison.Ordinal) ||
                    span.Equals("yes".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                    span.Equals("y".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (span.Equals("false".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                    span.Equals("0".AsSpan(), StringComparison.Ordinal) ||
                    span.Equals("no".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                    span.Equals("n".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return defaultValue;
        }

        public static bool? AsNullableBool(object? value, bool? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is bool b) return b;
            if (value is int i) return i != 0;
            if (value is byte by) return by != 0;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                return AsBool(str, false);
            }

            return defaultValue;
        }

        #endregion

        #region DateTime

        public static DateTime AsDateTime(object? value, DateTime defaultValue = default)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is DateTime dt) return dt;
            if (value is DateTimeOffset dto) return dto.DateTime;

            if (value is string str)
            {
                if (FastDateParser.TryParse(str.AsSpan(), out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        public static DateTime? AsNullableDateTime(object? value, DateTime? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is DateTime dt) return dt;
            if (value is DateTimeOffset dto) return dto.DateTime;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastDateParser.TryParse(span, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        #endregion

        #region Guid

        public static Guid AsGuid(object? value, Guid defaultValue = default)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is Guid g) return g;
            if (value is byte[] bytes && bytes.Length == 16) return new Guid(bytes);

            if (value is string str)
            {
#if NET8_0_OR_GREATER
                if (Guid.TryParse(str.AsSpan(), out var parsed)) return parsed;
#else
                if (Guid.TryParse(str, out var parsed)) return parsed;
#endif
            }

            return defaultValue;
        }

        public static Guid? AsNullableGuid(object? value, Guid? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is Guid g) return g;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
#if NET8_0_OR_GREATER
                if (Guid.TryParse(span, out var parsed)) return parsed;
#else
                if (Guid.TryParse(span.ToString(), out var parsed)) return parsed;
#endif
            }

            return defaultValue;
        }

        #endregion

        #region String

        public static string AsString(object? value, string defaultValue = "")
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (value is string s) return s.Trim();
            return value.ToString()?.Trim() ?? defaultValue;
        }

        #endregion

        #region Enum & Collections

        /// <summary>
        /// Converts integer or string representation into an Enum value case-insensitively.
        /// </summary>
        public static TEnum AsEnum<TEnum>(object? value, TEnum defaultValue = default) where TEnum : struct, Enum
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is TEnum exact) return exact;

            if (value is int i)
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), i);
            }
            if (value is byte b)
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), b);
            }
            if (value is short s)
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), s);
            }
            if (value is long l)
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), l);
            }

            if (value is string str)
            {
                var trimmed = str.Trim();
                if (string.IsNullOrEmpty(trimmed)) return defaultValue;

                if (Enum.TryParse<TEnum>(trimmed, ignoreCase: true, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        public static TEnum? AsNullableEnum<TEnum>(object? value) where TEnum : struct, Enum
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is TEnum exact) return exact;

            if (value is string str && string.IsNullOrWhiteSpace(str)) return null;

            return AsEnum<TEnum>(value);
        }

        /// <summary>
        /// Parses a delimited string (e.g. "1,2,3,4") into an array of converted values.
        /// </summary>
        public static T[] FromDelimitedString<T>(string? text, char delimiter = ',')
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<T>();

            string[] parts = text!.Split(delimiter);
            var result = new T[parts.Length];
            var targetType = typeof(T);

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (targetType == typeof(int))
                    result[i] = (T)(object)AsInt(part);
                else if (targetType == typeof(long))
                    result[i] = (T)(object)AsLong(part);
                else if (targetType == typeof(decimal))
                    result[i] = (T)(object)AsDecimal(part);
                else if (targetType == typeof(double))
                    result[i] = (T)(object)AsDouble(part);
                else if (targetType == typeof(string))
                    result[i] = (T)(object)part;
                else if (targetType.IsEnum)
                    result[i] = (T)Enum.Parse(targetType, part, true);
                else
                    result[i] = (T)Convert.ChangeType(part, targetType);
            }

            return result;
        }

        /// <summary>
        /// Joins an enumerable collection into a delimited string.
        /// </summary>
        public static string AsDelimitedString<T>(System.Collections.Generic.IEnumerable<T>? items, string delimiter = ",")
        {
            if (items == null) return string.Empty;
            return string.Join(delimiter, items);
        }

        #endregion

        #region Standardized To* Aliases & Generic Conversion

        public static int ToInt(object? value, int defaultValue = 0) => AsInt(value, defaultValue);
        public static int? ToNullableInt(object? value, int? defaultValue = null) => AsNullableInt(value, defaultValue);

        public static long ToLong(object? value, long defaultValue = 0) => AsLong(value, defaultValue);
        public static long? ToNullableLong(object? value, long? defaultValue = null) => AsNullableLong(value, defaultValue);

        public static decimal ToDecimal(object? value, decimal defaultValue = 0m) => AsDecimal(value, defaultValue);
        public static decimal? ToNullableDecimal(object? value, decimal? defaultValue = null) => AsNullableDecimal(value, defaultValue);

        public static double ToDouble(object? value, double defaultValue = 0.0) => AsDouble(value, defaultValue);
        public static double? ToNullableDouble(object? value, double? defaultValue = null) => AsNullableDouble(value, defaultValue);

        public static bool ToBool(object? value, bool defaultValue = false) => AsBool(value, defaultValue);
        public static bool? ToNullableBool(object? value, bool? defaultValue = null) => AsNullableBool(value, defaultValue);

        public static DateTime ToDateTime(object? value, DateTime defaultValue = default) => AsDateTime(value, defaultValue);
        public static DateTime? ToNullableDateTime(object? value, DateTime? defaultValue = null) => AsNullableDateTime(value, defaultValue);

        public static float ToFloat(object? value, float defaultValue = 0.0f)
            => (float)AsDouble(value, defaultValue);
        public static float? ToNullableFloat(object? value, float? defaultValue = null)
        {
            var d = AsNullableDouble(value);
            return d.HasValue ? (float)d.Value : defaultValue;
        }

        public static string ToStringOrDefault(object? value, string defaultValue = "") => AsString(value, defaultValue);
        public static string ToSafeString(object? value, string defaultValue = "") => AsString(value, defaultValue);

        public static string ToDelimitedString<T>(System.Collections.Generic.IEnumerable<T>? items, string delimiter = ",")
            => AsDelimitedString(items, delimiter);

        public static Guid ToGuid(object? value, Guid defaultValue = default) => AsGuid(value, defaultValue);
        public static Guid? ToNullableGuid(object? value, Guid? defaultValue = null) => AsNullableGuid(value, defaultValue);

        public static TEnum ToEnum<TEnum>(object? value, TEnum defaultValue = default) where TEnum : struct, Enum
            => AsEnum(value, defaultValue);
        public static TEnum? ToNullableEnum<TEnum>(object? value) where TEnum : struct, Enum
            => AsNullableEnum<TEnum>(value);

        /// <summary>
        /// Universal, high-performance generic type converter.
        /// Unboxes primitives via CPU register casts with zero heap allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T To<T>(object? value)
        {
            return To<T>(value, default!);
        }

        /// <summary>
        /// Universal, high-performance generic type converter with fallback default value.
        /// Unboxes primitives via CPU register casts with zero heap allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T To<T>(object? value, T defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is T exact) return exact;

            var targetType = typeof(T);

            // Zero-boxing unbox path: JIT folds typeof(T) == typeof(...) into compile-time constant
            if (targetType == typeof(int))
            {
                int val = AsInt(value);
                return Unsafe.As<int, T>(ref val);
            }
            if (targetType == typeof(long))
            {
                long val = AsLong(value);
                return Unsafe.As<long, T>(ref val);
            }
            if (targetType == typeof(decimal))
            {
                decimal val = AsDecimal(value);
                return Unsafe.As<decimal, T>(ref val);
            }
            if (targetType == typeof(double))
            {
                double val = AsDouble(value);
                return Unsafe.As<double, T>(ref val);
            }
            if (targetType == typeof(float))
            {
                float val = (float)AsDouble(value);
                return Unsafe.As<float, T>(ref val);
            }
            if (targetType == typeof(bool))
            {
                bool val = AsBool(value);
                return Unsafe.As<bool, T>(ref val);
            }
            if (targetType == typeof(string))
            {
                string val = AsString(value);
                return Unsafe.As<string, T>(ref val);
            }
            if (targetType == typeof(DateTime))
            {
                DateTime val = AsDateTime(value);
                return Unsafe.As<DateTime, T>(ref val);
            }
            if (targetType == typeof(Guid))
            {
                Guid val = AsGuid(value);
                return Unsafe.As<Guid, T>(ref val);
            }
            if (targetType == typeof(short))
            {
                short val = (short)AsInt(value);
                return Unsafe.As<short, T>(ref val);
            }
            if (targetType == typeof(byte))
            {
                byte val = (byte)AsInt(value);
                return Unsafe.As<byte, T>(ref val);
            }

            if (targetType.IsEnum)
            {
#if NET8_0_OR_GREATER
                if (value is string s && Enum.TryParse(targetType, s, true, out var parsedEnum))
                    return (T)parsedEnum;
#else
                if (value is string s)
                {
                    try
                    {
                        return (T)Enum.Parse(targetType, s, true);
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
#endif
                try
                {
                    return (T)Enum.ToObject(targetType, AsLong(value));
                }
                catch
                {
                    return defaultValue;
                }
            }

            try
            {
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Universal generic nullable type converter.
        /// </summary>
        public static T? ToNullable<T>(object? value) where T : struct
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is string str && string.IsNullOrWhiteSpace(str)) return null;
            return To<T>(value);
        }

        #endregion
    }
}
