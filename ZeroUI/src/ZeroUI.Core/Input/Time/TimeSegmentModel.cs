using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Input.Time
{
    /// <summary>
    /// Headless, zero-allocation state machine and calculation model for segmented time editing.
    /// Provides segment stepping, keyboard digit entry, 12h/24h conversion, and Span formatting.
    /// </summary>
    public class TimeSegmentModel
    {
        private TimeSpan _time;
        private TimeSegment _focusedSegment = TimeSegment.Hour;
        private bool _showSeconds = false;
        private bool _is24Hour = true;
        private int _stepHours = 1;
        private int _stepMinutes = 1;
        private int _stepSeconds = 1;
        private bool _allowCarryOver = false;

        // Interactive digit input state
        private int _digitBuffer = -1;

        public event EventHandler? TimeChanged;
        public event EventHandler? SegmentChanged;

        public static readonly IReadOnlyList<TimePreset> DefaultShiftPresets = new[]
        {
            new TimePreset("00:00 - Midnight Shift", new TimeSpan(0, 0, 0)),
            new TimePreset("06:00 - Early Morning Shift", new TimeSpan(6, 0, 0)),
            new TimePreset("08:00 - Day Shift Start", new TimeSpan(8, 0, 0)),
            new TimePreset("12:00 - Noon Break", new TimeSpan(12, 0, 0)),
            new TimePreset("14:00 - Afternoon Shift", new TimeSpan(14, 0, 0)),
            new TimePreset("18:00 - Day Shift End", new TimeSpan(18, 0, 0)),
            new TimePreset("22:00 - Night Shift Start", new TimeSpan(22, 0, 0))
        };

        public TimeSegmentModel()
        {
            var now = DateTime.Now.TimeOfDay;
            _time = new TimeSpan(now.Hours, now.Minutes, 0);
        }

        public TimeSegmentModel(TimeSpan initialTime)
        {
            _time = SanitizeTime(initialTime);
        }

        public TimeSpan Time
        {
            get => _time;
            set
            {
                var sanitized = SanitizeTime(value);
                if (_time != sanitized)
                {
                    _time = sanitized;
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public TimeSegment FocusedSegment
        {
            get => _focusedSegment;
            set
            {
                var valid = ValidateSegment(value);
                if (_focusedSegment != valid)
                {
                    _focusedSegment = valid;
                    _digitBuffer = -1;
                    SegmentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool ShowSeconds
        {
            get => _showSeconds;
            set
            {
                if (_showSeconds != value)
                {
                    _showSeconds = value;
                    _focusedSegment = ValidateSegment(_focusedSegment);
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool Is24Hour
        {
            get => _is24Hour;
            set
            {
                if (_is24Hour != value)
                {
                    _is24Hour = value;
                    _focusedSegment = ValidateSegment(_focusedSegment);
                    _digitBuffer = -1;
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int StepHours
        {
            get => _stepHours;
            set => _stepHours = Math.Max(1, Math.Min(12, value));
        }

        public int StepMinutes
        {
            get => _stepMinutes;
            set => _stepMinutes = Math.Max(1, Math.Min(60, value));
        }

        public int StepSeconds
        {
            get => _stepSeconds;
            set => _stepSeconds = Math.Max(1, Math.Min(60, value));
        }

        public bool AllowCarryOver
        {
            get => _allowCarryOver;
            set => _allowCarryOver = value;
        }

        public bool IsAm => _time.Hours < 12;

        public int DisplayHour
        {
            get
            {
                if (_is24Hour) return _time.Hours;
                int h = _time.Hours % 12;
                return h == 0 ? 12 : h;
            }
        }

        public int DisplayMinute => _time.Minutes;
        public int DisplaySecond => _time.Seconds;

        private static TimeSpan SanitizeTime(TimeSpan value)
        {
            int totalSeconds = (int)value.TotalSeconds;
            if (totalSeconds < 0)
            {
                totalSeconds = (totalSeconds % 86400) + 86400;
            }
            else if (totalSeconds >= 86400)
            {
                totalSeconds %= 86400;
            }

            int h = totalSeconds / 3600;
            int rem = totalSeconds % 3600;
            int m = rem / 60;
            int s = rem % 60;

            return new TimeSpan(h, m, s);
        }

        private TimeSegment ValidateSegment(TimeSegment segment)
        {
            if (segment == TimeSegment.Second && !_showSeconds)
            {
                return _is24Hour ? TimeSegment.Minute : TimeSegment.AmPm;
            }
            if (segment == TimeSegment.AmPm && _is24Hour)
            {
                return _showSeconds ? TimeSegment.Second : TimeSegment.Minute;
            }
            return segment;
        }

        public void ResetDigitInput()
        {
            _digitBuffer = -1;
        }

        /// <summary>
        /// Moves focus to the next logical segment. Returns true if segment changed.
        /// </summary>
        public bool MoveNextSegment()
        {
            _digitBuffer = -1;
            TimeSegment next;

            switch (_focusedSegment)
            {
                case TimeSegment.Hour:
                    next = TimeSegment.Minute;
                    break;
                case TimeSegment.Minute:
                    if (_showSeconds) next = TimeSegment.Second;
                    else if (!_is24Hour) next = TimeSegment.AmPm;
                    else return false;
                    break;
                case TimeSegment.Second:
                    if (!_is24Hour) next = TimeSegment.AmPm;
                    else return false;
                    break;
                default:
                    return false;
            }

            FocusedSegment = next;
            return true;
        }

        /// <summary>
        /// Moves focus to the previous logical segment. Returns true if segment changed.
        /// </summary>
        public bool MovePreviousSegment()
        {
            _digitBuffer = -1;
            TimeSegment prev;

            switch (_focusedSegment)
            {
                case TimeSegment.AmPm:
                    prev = _showSeconds ? TimeSegment.Second : TimeSegment.Minute;
                    break;
                case TimeSegment.Second:
                    prev = TimeSegment.Minute;
                    break;
                case TimeSegment.Minute:
                    prev = TimeSegment.Hour;
                    break;
                default:
                    return false;
            }

            FocusedSegment = prev;
            return true;
        }

        /// <summary>
        /// Adjusts the currently focused segment up (+delta) or down (-delta).
        /// </summary>
        public void AdjustCurrentSegment(int delta)
        {
            if (delta == 0) return;
            _digitBuffer = -1;

            int h = _time.Hours;
            int m = _time.Minutes;
            int s = _time.Seconds;

            switch (_focusedSegment)
            {
                case TimeSegment.Hour:
                    h = (h + (delta * _stepHours)) % 24;
                    if (h < 0) h += 24;
                    break;

                case TimeSegment.Minute:
                    int stepM = delta * _stepMinutes;
                    if (_allowCarryOver)
                    {
                        var updated = _time.Add(TimeSpan.FromMinutes(stepM));
                        Time = updated;
                        return;
                    }
                    else
                    {
                        m = (m + stepM) % 60;
                        if (m < 0) m += 60;
                    }
                    break;

                case TimeSegment.Second:
                    int stepS = delta * _stepSeconds;
                    if (_allowCarryOver)
                    {
                        var updated = _time.Add(TimeSpan.FromSeconds(stepS));
                        Time = updated;
                        return;
                    }
                    else
                    {
                        s = (s + stepS) % 60;
                        if (s < 0) s += 60;
                    }
                    break;

                case TimeSegment.AmPm:
                    h = (h + 12) % 24;
                    break;
            }

            Time = new TimeSpan(h, m, s);
        }

        /// <summary>
        /// Applies an interactive digit input (0-9) to the active segment.
        /// Automatically advances to the next segment when entry is complete.
        /// </summary>
        public bool TryApplyDigit(int digit)
        {
            if (digit < 0 || digit > 9) return false;

            int h = _time.Hours;
            int m = _time.Minutes;
            int s = _time.Seconds;

            switch (_focusedSegment)
            {
                case TimeSegment.Hour:
                    if (_digitBuffer < 0)
                    {
                        if (_is24Hour)
                        {
                            if (digit > 2)
                            {
                                Time = new TimeSpan(digit, m, s);
                                _digitBuffer = -1;
                                MoveNextSegment();
                            }
                            else
                            {
                                _digitBuffer = digit;
                                Time = new TimeSpan(digit, m, s);
                            }
                        }
                        else
                        {
                            if (digit > 1)
                            {
                                int hr = IsAm ? digit : (digit + 12) % 24;
                                Time = new TimeSpan(hr, m, s);
                                _digitBuffer = -1;
                                MoveNextSegment();
                            }
                            else
                            {
                                _digitBuffer = digit;
                                int hr = IsAm ? digit : (digit + 12) % 24;
                                Time = new TimeSpan(hr, m, s);
                            }
                        }
                    }
                    else
                    {
                        int combined = (_digitBuffer * 10) + digit;
                        _digitBuffer = -1;

                        if (_is24Hour)
                        {
                            if (combined > 23) combined = 23;
                            Time = new TimeSpan(combined, m, s);
                        }
                        else
                        {
                            if (combined > 12) combined = 12;
                            if (combined == 0) combined = 12;
                            int hr = IsAm ? (combined % 12) : ((combined % 12) + 12);
                            Time = new TimeSpan(hr, m, s);
                        }
                        MoveNextSegment();
                    }
                    return true;

                case TimeSegment.Minute:
                    if (_digitBuffer < 0)
                    {
                        if (digit > 5)
                        {
                            Time = new TimeSpan(h, digit, s);
                            _digitBuffer = -1;
                            MoveNextSegment();
                        }
                        else
                        {
                            _digitBuffer = digit;
                            Time = new TimeSpan(h, digit, s);
                        }
                    }
                    else
                    {
                        int combined = (_digitBuffer * 10) + digit;
                        _digitBuffer = -1;
                        if (combined > 59) combined = 59;
                        Time = new TimeSpan(h, combined, s);
                        MoveNextSegment();
                    }
                    return true;

                case TimeSegment.Second:
                    if (_digitBuffer < 0)
                    {
                        if (digit > 5)
                        {
                            Time = new TimeSpan(h, m, digit);
                            _digitBuffer = -1;
                            MoveNextSegment();
                        }
                        else
                        {
                            _digitBuffer = digit;
                            Time = new TimeSpan(h, m, digit);
                        }
                    }
                    else
                    {
                        int combined = (_digitBuffer * 10) + digit;
                        _digitBuffer = -1;
                        if (combined > 59) combined = 59;
                        Time = new TimeSpan(h, m, combined);
                        MoveNextSegment();
                    }
                    return true;

                default:
                    return false;
            }
        }

        public void ToggleAmPm()
        {
            int h = (_time.Hours + 12) % 24;
            Time = new TimeSpan(h, _time.Minutes, _time.Seconds);
        }

        #region Zero-Allocation Span Formatting

        private static void WriteTwoDigits(int val, Span<char> dest)
        {
            dest[0] = (char)('0' + (val / 10));
            dest[1] = (char)('0' + (val % 10));
        }

        public bool TryFormatHour(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length < 2) return false;
            WriteTwoDigits(DisplayHour, destination);
            charsWritten = 2;
            return true;
        }

        public bool TryFormatMinute(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length < 2) return false;
            WriteTwoDigits(DisplayMinute, destination);
            charsWritten = 2;
            return true;
        }

        public bool TryFormatSecond(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length < 2) return false;
            WriteTwoDigits(DisplaySecond, destination);
            charsWritten = 2;
            return true;
        }

        public bool TryFormatAmPm(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length < 2) return false;
            destination[0] = IsAm ? 'A' : 'P';
            destination[1] = 'M';
            charsWritten = 2;
            return true;
        }

        /// <summary>
        /// Formats full time into destination buffer without allocations.
        /// Pattern is HH:mm, HH:mm:ss, hh:mm tt, or hh:mm:ss tt.
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            int required = 5; // "HH:mm"
            if (_showSeconds) required += 3; // ":ss"
            if (!_is24Hour) required += 3;   // " tt"

            if (destination.Length < required) return false;

            int idx = 0;

            // Hour
            WriteTwoDigits(DisplayHour, destination.Slice(idx, 2));
            idx += 2;

            // :
            destination[idx++] = ':';

            // Minute
            WriteTwoDigits(DisplayMinute, destination.Slice(idx, 2));
            idx += 2;

            // Second
            if (_showSeconds)
            {
                destination[idx++] = ':';
                WriteTwoDigits(DisplaySecond, destination.Slice(idx, 2));
                idx += 2;
            }

            // AM/PM
            if (!_is24Hour)
            {
                destination[idx++] = ' ';
                destination[idx++] = IsAm ? 'A' : 'P';
                destination[idx++] = 'M';
            }

            charsWritten = idx;
            return true;
        }

        public override string ToString()
        {
            Span<char> buf = stackalloc char[16];
            if (TryFormat(buf, out int written))
            {
                return new string(buf.Slice(0, written).ToArray());
            }
            return _time.ToString();
        }

        #endregion
    }
}
