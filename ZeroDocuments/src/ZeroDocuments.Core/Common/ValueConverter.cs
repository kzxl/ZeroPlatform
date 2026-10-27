using System;
using System.Globalization;
using ZeroDocuments.Excel.Internal;

namespace ZeroDocuments.Common
{
    /// <summary>
    /// Culture-invariant conversion of raw spreadsheet text into CLR property types.
    /// Tolerates Excel numeric artifacts: integral values stored as "1.0" / "1E3", dates stored as OLE serials.
    /// </summary>
    internal static class ValueConverter
    {
        private const NumberStyles FloatStyle = NumberStyles.Float;

        /// <summary>
        /// Converts a raw cell value. Returns null for null/empty input.
        /// Throws <see cref="FormatException"/> / <see cref="OverflowException"/> when the value cannot be converted.
        /// </summary>
        public static object? Convert(string? raw, Type targetType)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            Type t = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var inv = CultureInfo.InvariantCulture;

            if (t == typeof(string)) return raw;

            if (t == typeof(int)) return int.TryParse(raw, NumberStyles.Integer, inv, out var i) ? i : checked((int)ParseIntegral(raw!));
            if (t == typeof(long)) return long.TryParse(raw, NumberStyles.Integer, inv, out var l) ? l : checked((long)ParseIntegral(raw!));
            if (t == typeof(short)) return checked((short)ParseIntegral(raw!));
            if (t == typeof(byte)) return checked((byte)ParseIntegral(raw!));
            if (t == typeof(sbyte)) return checked((sbyte)ParseIntegral(raw!));
            if (t == typeof(ushort)) return checked((ushort)ParseIntegral(raw!));
            if (t == typeof(uint)) return checked((uint)ParseIntegral(raw!));
            if (t == typeof(ulong)) return ulong.TryParse(raw, NumberStyles.Integer, inv, out var ul) ? ul : checked((ulong)ParseIntegral(raw!));

            if (t == typeof(double)) return double.Parse(raw, FloatStyle, inv);
            if (t == typeof(float)) return float.Parse(raw, FloatStyle, inv);
            // NumberStyles.Float accepts exponent notation (e.g. "1E-3") which Excel emits for very small/large values.
            if (t == typeof(decimal)) return decimal.Parse(raw, FloatStyle, inv);

            if (t == typeof(bool))
            {
                if (raw == "1" || raw!.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                return bool.Parse(raw);
            }

            if (t == typeof(DateTime))
            {
                if (DateTime.TryParse(raw, inv, DateTimeStyles.None, out var dt)) return dt;
                if (double.TryParse(raw, FloatStyle, inv, out var serial) && OADateFormatter.TryFromSerial(serial, false, out var fromSerial)) return fromSerial;
                throw new FormatException($"'{raw}' is not a valid DateTime.");
            }

            if (t == typeof(DateTimeOffset))
            {
                if (DateTimeOffset.TryParse(raw, inv, DateTimeStyles.None, out var dto)) return dto;
                if (double.TryParse(raw, FloatStyle, inv, out var serial) && OADateFormatter.TryFromSerial(serial, false, out var fromSerial)) return new DateTimeOffset(fromSerial);
                throw new FormatException($"'{raw}' is not a valid DateTimeOffset.");
            }

            if (t == typeof(TimeSpan))
            {
                if (TimeSpan.TryParse(raw, inv, out var ts)) return ts;
                // Excel stores durations/time-of-day as fractions of a day.
                if (double.TryParse(raw, FloatStyle, inv, out var days)) return OADateFormatter.FromDayFraction(days);
                throw new FormatException($"'{raw}' is not a valid TimeSpan.");
            }

            if (t == typeof(Guid)) return Guid.Parse(raw);
            if (t.IsEnum) return Enum.Parse(t, raw!.Trim(), true);

            return System.Convert.ChangeType(raw, t, inv);
        }

        /// <summary>
        /// Parses numeric text that must represent a whole number (e.g. "15.0", "1E3").
        /// </summary>
        private static decimal ParseIntegral(string raw)
        {
            decimal value = decimal.Parse(raw, FloatStyle, CultureInfo.InvariantCulture);
            if (value != decimal.Truncate(value))
            {
                throw new FormatException($"'{raw}' is not an integral value.");
            }
            return value;
        }
    }
}
