using System;
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

        public static int AsInt(object? value, int defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            // Direct register unbox - 0 allocation
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (int)m;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (value is bool boolean) return boolean ? 1 : 0;
            if (value is uint ui) return (int)ui;
            if (value is ulong ul) return (int)ul;
            if (value is ushort us) return us;
            if (value is sbyte sb) return sb;

            if (value is string str)
            {
                if (FastNumberParser.TryParseInt32(str.AsSpan(), out int res, defaultValue))
                    return res;
                return defaultValue;
            }

            return defaultValue;
        }

        public static int? AsNullableInt(object? value, int? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (int)m;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (value is bool boolean) return boolean ? 1 : 0;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseInt32(span, out int res))
                    return res;
            }

            return defaultValue;
        }

        #endregion

        #region Long (64-bit)

        public static long AsLong(object? value, long defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is long l) return l;
            if (value is int i) return i;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (long)m;
            if (value is double d) return (long)d;
            if (value is float f) return (long)f;
            if (value is ulong ul) return (long)ul;
            if (value is uint ui) return ui;
            if (value is bool boolean) return boolean ? 1L : 0L;

            if (value is string str)
            {
                if (FastNumberParser.TryParseInt64(str.AsSpan(), out long res, defaultValue))
                    return res;
                return defaultValue;
            }

            return defaultValue;
        }

        public static long? AsNullableLong(object? value, long? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is long l) return l;
            if (value is int i) return i;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is decimal m) return (long)m;
            if (value is double d) return (long)d;
            if (value is float f) return (long)f;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseInt64(span, out long res))
                    return res;
            }

            return defaultValue;
        }

        #endregion

        #region Decimal

        public static decimal AsDecimal(object? value, decimal defaultValue = 0m)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is decimal m) return m;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is uint ui) return ui;
            if (value is ulong ul) return ul;

            if (value is string str)
            {
                if (FastNumberParser.TryParseDecimal(str.AsSpan(), out decimal res, defaultValue))
                    return res;
                return defaultValue;
            }

            return defaultValue;
        }

        public static decimal? AsNullableDecimal(object? value, decimal? defaultValue = null)
        {
            if (value == null || value == DBNull.Value) return defaultValue;

            if (value is decimal m) return m;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is short s) return s;
            if (value is byte b) return b;

            if (value is string str)
            {
                var span = SpanTextOps.TrimAsciiWhitespace(str.AsSpan());
                if (span.IsEmpty) return defaultValue;
                if (FastNumberParser.TryParseDecimal(span, out decimal res))
                    return res;
            }

            return defaultValue;
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
    }
}
