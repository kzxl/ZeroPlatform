using System;
using System.Globalization;
using System.Text;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Identifies the discrete time segment being edited in a <see cref="TimeSpanModel"/>.
    /// </summary>
    public enum TimeSpanPart
    {
        Days,
        Hours,
        Minutes,
        Seconds,
        Milliseconds
    }

    /// <summary>
    /// Display and formatting styles for <see cref="TimeSpanModel"/>.
    /// </summary>
    public enum TimeSpanFormatMode
    {
        /// <summary>
        /// Digital clock display: [-][d.]hh:mm:ss[.fff]
        /// </summary>
        Clock,

        /// <summary>
        /// Verbose readable format: e.g. "2d 04h 15m 30s"
        /// </summary>
        Verbose,

        /// <summary>
        /// Total seconds display: e.g. "124.500s"
        /// </summary>
        TotalSeconds
    }

    /// <summary>
    /// Headless, zero-allocation calculation and state model for duration editing.
    /// Handles segment stepping, bounds enforcement, custom format rendering, and string parsing.
    /// </summary>
    public class TimeSpanModel
    {
        private TimeSpan _value = TimeSpan.Zero;
        private TimeSpan _minimum = TimeSpan.Zero;
        private TimeSpan _maximum = TimeSpan.FromDays(9999);
        private TimeSpanPart _focusedPart = TimeSpanPart.Hours;
        private bool _allowNegative = false;
        private bool _showDays = true;
        private bool _showMilliseconds = false;
        private TimeSpanFormatMode _formatMode = TimeSpanFormatMode.Clock;

        public event EventHandler? ValueChanged;
        public event EventHandler? PartChanged;

        public TimeSpanModel()
        {
        }

        public TimeSpanModel(TimeSpan initialValue)
        {
            _value = Clamp(initialValue);
        }

        public TimeSpan Value
        {
            get => _value;
            set
            {
                var clamped = Clamp(value);
                if (_value != clamped)
                {
                    _value = clamped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public TimeSpan Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_value < _minimum) Value = _minimum;
            }
        }

        public TimeSpan Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_value > _maximum) Value = _maximum;
            }
        }

        public TimeSpanPart FocusedPart
        {
            get => _focusedPart;
            set
            {
                if (_focusedPart != value)
                {
                    _focusedPart = value;
                    PartChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowNegative
        {
            get => _allowNegative;
            set => _allowNegative = value;
        }

        public bool ShowDays
        {
            get => _showDays;
            set => _showDays = value;
        }

        public bool ShowMilliseconds
        {
            get => _showMilliseconds;
            set => _showMilliseconds = value;
        }

        public TimeSpanFormatMode FormatMode
        {
            get => _formatMode;
            set => _formatMode = value;
        }

        public int Days => _value.Days;
        public int Hours => _value.Hours;
        public int Minutes => _value.Minutes;
        public int Seconds => _value.Seconds;
        public int Milliseconds => _value.Milliseconds;

        private TimeSpan Clamp(TimeSpan val)
        {
            if (!_allowNegative && val < TimeSpan.Zero)
                val = TimeSpan.Zero;

            if (val < _minimum) return _minimum;
            if (val > _maximum) return _maximum;
            return val;
        }

        public void StepUp(TimeSpanPart? part = null)
        {
            var p = part ?? _focusedPart;
            Value = _value.Add(GetStepDelta(p, 1));
        }

        public void StepDown(TimeSpanPart? part = null)
        {
            var p = part ?? _focusedPart;
            Value = _value.Add(GetStepDelta(p, -1));
        }

        private static TimeSpan GetStepDelta(TimeSpanPart part, int direction)
        {
            switch (part)
            {
                case TimeSpanPart.Days:
                    return TimeSpan.FromDays(direction);
                case TimeSpanPart.Hours:
                    return TimeSpan.FromHours(direction);
                case TimeSpanPart.Minutes:
                    return TimeSpan.FromMinutes(direction);
                case TimeSpanPart.Seconds:
                    return TimeSpan.FromSeconds(direction);
                case TimeSpanPart.Milliseconds:
                    return TimeSpan.FromMilliseconds(direction * 50);
                default:
                    return TimeSpan.FromSeconds(direction);
            }
        }

        public void NextPart()
        {
            switch (_focusedPart)
            {
                case TimeSpanPart.Days:
                    FocusedPart = TimeSpanPart.Hours;
                    break;
                case TimeSpanPart.Hours:
                    FocusedPart = TimeSpanPart.Minutes;
                    break;
                case TimeSpanPart.Minutes:
                    FocusedPart = TimeSpanPart.Seconds;
                    break;
                case TimeSpanPart.Seconds:
                    FocusedPart = _showMilliseconds ? TimeSpanPart.Milliseconds : (_showDays ? TimeSpanPart.Days : TimeSpanPart.Hours);
                    break;
                case TimeSpanPart.Milliseconds:
                    FocusedPart = _showDays ? TimeSpanPart.Days : TimeSpanPart.Hours;
                    break;
            }
        }

        public void PreviousPart()
        {
            switch (_focusedPart)
            {
                case TimeSpanPart.Milliseconds:
                    FocusedPart = TimeSpanPart.Seconds;
                    break;
                case TimeSpanPart.Seconds:
                    FocusedPart = TimeSpanPart.Minutes;
                    break;
                case TimeSpanPart.Minutes:
                    FocusedPart = TimeSpanPart.Hours;
                    break;
                case TimeSpanPart.Hours:
                    FocusedPart = _showDays ? TimeSpanPart.Days : (_showMilliseconds ? TimeSpanPart.Milliseconds : TimeSpanPart.Seconds);
                    break;
                case TimeSpanPart.Days:
                    FocusedPart = _showMilliseconds ? TimeSpanPart.Milliseconds : TimeSpanPart.Seconds;
                    break;
            }
        }

        public string GetFormattedText()
        {
            return FormatTimeSpan(_value, _formatMode, _showDays, _showMilliseconds);
        }

        public static string FormatTimeSpan(TimeSpan ts, TimeSpanFormatMode mode, bool showDays = true, bool showMs = false)
        {
            bool isNeg = ts < TimeSpan.Zero;
            var abs = isNeg ? ts.Negate() : ts;
            string prefix = isNeg ? "-" : "";

            switch (mode)
            {
                case TimeSpanFormatMode.Verbose:
                    var sb = new StringBuilder(prefix);
                    if (showDays && abs.Days > 0) sb.Append(abs.Days).Append("d ");
                    sb.Append(abs.Hours).Append("h ");
                    sb.Append(abs.Minutes).Append("m ");
                    sb.Append(abs.Seconds).Append("s");
                    if (showMs && abs.Milliseconds > 0) sb.Append(" ").Append(abs.Milliseconds).Append("ms");
                    return sb.ToString().TrimEnd();

                case TimeSpanFormatMode.TotalSeconds:
                    return $"{prefix}{abs.TotalSeconds:F2}s";

                case TimeSpanFormatMode.Clock:
                default:
                    if (showDays && abs.Days > 0)
                    {
                        return showMs
                            ? $"{prefix}{abs.Days}d {abs.Hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}.{abs.Milliseconds:D3}"
                            : $"{prefix}{abs.Days}d {abs.Hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
                    }
                    else
                    {
                        return showMs
                            ? $"{prefix}{abs.Hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}.{abs.Milliseconds:D3}"
                            : $"{prefix}{abs.Hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
                    }
            }
        }

        public static bool TryParse(string? text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text!.Trim();

            // Try standard TimeSpan parse
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out result))
                return true;

            // Try shorthand notation e.g. "90s", "15m", "2h", "1d"
            if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && double.TryParse(text.Substring(0, text.Length - 2).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double ms))
            {
                result = TimeSpan.FromMilliseconds(ms);
                return true;
            }
            if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase) && double.TryParse(text.Substring(0, text.Length - 1).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double s))
            {
                result = TimeSpan.FromSeconds(s);
                return true;
            }
            if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase) && double.TryParse(text.Substring(0, text.Length - 1).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double m))
            {
                result = TimeSpan.FromMinutes(m);
                return true;
            }
            if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase) && double.TryParse(text.Substring(0, text.Length - 1).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double h))
            {
                result = TimeSpan.FromHours(h);
                return true;
            }
            if (text.EndsWith("d", StringComparison.OrdinalIgnoreCase) && double.TryParse(text.Substring(0, text.Length - 1).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                result = TimeSpan.FromDays(d);
                return true;
            }

            return false;
        }
    }
}
