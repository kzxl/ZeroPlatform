using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Range
{
    /// <summary>
    /// Event arguments for range change notifications.
    /// </summary>
    public sealed class RangeChangedEventArgs : EventArgs
    {
        public double Start { get; }
        public double End { get; }

        public RangeChangedEventArgs(double start, double end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Mathematical engine and state coordinator for RangeControl.
    /// Handles coordinate projections, boundary clamping, pan, zoom, and interval snapping.
    /// </summary>
    public sealed class RangeControlModel
    {
        private double _totalStart = 0.0;
        private double _totalEnd = 100.0;

        private double _visibleStart = 0.0;
        private double _visibleEnd = 100.0;

        private double _selectedStart = 20.0;
        private double _selectedEnd = 80.0;

        private double _minRangeSpan = 1e-6;

        public RangeDataType DataType { get; set; } = RangeDataType.Numeric;
        public RangeInterval Interval { get; set; } = RangeInterval.None;
        public double NumericStep { get; set; } = 1.0;
        public bool SnapToInterval { get; set; } = false;

        public List<RangeDataPoint> DataPoints { get; } = new List<RangeDataPoint>();

        public event EventHandler<RangeChangedEventArgs>? RangeSelectionChanged;
        public event EventHandler<RangeChangedEventArgs>? VisibleRangeChanged;

        public double TotalRangeStart
        {
            get => _totalStart;
            set
            {
                if (value > _totalEnd) _totalEnd = value;
                _totalStart = value;
                ClampRanges();
            }
        }

        public double TotalRangeEnd
        {
            get => _totalEnd;
            set
            {
                if (value < _totalStart) _totalStart = value;
                _totalEnd = value;
                ClampRanges();
            }
        }

        public double VisibleRangeStart
        {
            get => _visibleStart;
            set => SetVisibleRange(value, _visibleEnd);
        }

        public double VisibleRangeEnd
        {
            get => _visibleEnd;
            set => SetVisibleRange(_visibleStart, value);
        }

        public double SelectedRangeStart
        {
            get => _selectedStart;
            set => SetSelectedRange(value, _selectedEnd);
        }

        public double SelectedRangeEnd
        {
            get => _selectedEnd;
            set => SetSelectedRange(_selectedStart, value);
        }

        public double SelectedRangeSpan => _selectedEnd - _selectedStart;
        public double VisibleRangeSpan => _visibleEnd - _visibleStart;
        public double TotalRangeSpan => _totalEnd - _totalStart;

        public double MinRangeSpan
        {
            get => _minRangeSpan;
            set => _minRangeSpan = Math.Max(1e-9, value);
        }

        #region DateTime Helper Properties

        public DateTime TotalStartDate
        {
            get => ToDateTimeSafe(_totalStart);
            set => TotalRangeStart = value.ToOADate();
        }

        public DateTime TotalEndDate
        {
            get => ToDateTimeSafe(_totalEnd);
            set => TotalRangeEnd = value.ToOADate();
        }

        public DateTime SelectedStartDate
        {
            get => ToDateTimeSafe(_selectedStart);
            set => SelectedRangeStart = value.ToOADate();
        }

        public DateTime SelectedEndDate
        {
            get => ToDateTimeSafe(_selectedEnd);
            set => SelectedRangeEnd = value.ToOADate();
        }

        public DateTime VisibleStartDate
        {
            get => ToDateTimeSafe(_visibleStart);
            set => VisibleRangeStart = value.ToOADate();
        }

        public DateTime VisibleEndDate
        {
            get => ToDateTimeSafe(_visibleEnd);
            set => VisibleRangeEnd = value.ToOADate();
        }

        #endregion

        public RangeControlModel()
        {
            _totalStart = 0;
            _totalEnd = 100;
            _visibleStart = 0;
            _visibleEnd = 100;
            _selectedStart = 20;
            _selectedEnd = 80;
        }

        public void SetTotalRange(double start, double end)
        {
            if (end < start)
            {
                double tmp = start;
                start = end;
                end = tmp;
            }

            if (Math.Abs(end - start) < 1e-9)
            {
                end = start + 1.0;
            }

            _totalStart = start;
            _totalEnd = end;
            ClampRanges();
        }

        public void SetVisibleRange(double start, double end)
        {
            if (end < start)
            {
                double tmp = start;
                start = end;
                end = tmp;
            }

            // Clamp into total range
            start = Math.Max(_totalStart, Math.Min(_totalEnd, start));
            end = Math.Max(_totalStart, Math.Min(_totalEnd, end));

            if (end - start < _minRangeSpan)
            {
                end = Math.Min(_totalEnd, start + _minRangeSpan);
            }

            bool changed = Math.Abs(_visibleStart - start) > 1e-9 || Math.Abs(_visibleEnd - end) > 1e-9;
            _visibleStart = start;
            _visibleEnd = end;

            if (changed)
            {
                VisibleRangeChanged?.Invoke(this, new RangeChangedEventArgs(_visibleStart, _visibleEnd));
            }
        }

        public void SetSelectedRange(double start, double end)
        {
            if (SnapToInterval)
            {
                start = Snap(start);
                end = Snap(end);
            }

            if (end < start)
            {
                double tmp = start;
                start = end;
                end = tmp;
            }

            // Clamp to total bounds
            start = Math.Max(_totalStart, Math.Min(_totalEnd, start));
            end = Math.Max(_totalStart, Math.Min(_totalEnd, end));

            if (end - start < _minRangeSpan)
            {
                end = Math.Min(_totalEnd, start + _minRangeSpan);
                if (end - start < _minRangeSpan)
                {
                    start = Math.Max(_totalStart, end - _minRangeSpan);
                }
            }

            bool changed = Math.Abs(_selectedStart - start) > 1e-9 || Math.Abs(_selectedEnd - end) > 1e-9;
            _selectedStart = start;
            _selectedEnd = end;

            if (changed)
            {
                RangeSelectionChanged?.Invoke(this, new RangeChangedEventArgs(_selectedStart, _selectedEnd));
            }
        }

        public void SetSelectedDateRange(DateTime start, DateTime end)
        {
            DataType = RangeDataType.DateTime;
            SetSelectedRange(start.ToOADate(), end.ToOADate());
        }

        public void SetTotalDateRange(DateTime start, DateTime end)
        {
            DataType = RangeDataType.DateTime;
            SetTotalRange(start.ToOADate(), end.ToOADate());
            SetVisibleRange(start.ToOADate(), end.ToOADate());
        }

        /// <summary>
        /// Translates the active selection window by delta value while maintaining span length.
        /// </summary>
        public void PanSelection(double delta)
        {
            double span = _selectedEnd - _selectedStart;
            double newStart = _selectedStart + delta;
            double newEnd = _selectedEnd + delta;

            if (newStart < _totalStart)
            {
                newStart = _totalStart;
                newEnd = newStart + span;
            }
            else if (newEnd > _totalEnd)
            {
                newEnd = _totalEnd;
                newStart = newEnd - span;
            }

            SetSelectedRange(newStart, newEnd);
        }

        /// <summary>
        /// Zooms the visible range window around a focal ratio point (0.0 to 1.0).
        /// Factor > 1.0 zooms in (contracts visible window), Factor &lt; 1.0 zooms out (expands visible window).
        /// </summary>
        public void Zoom(double factor, double centerRatio = 0.5)
        {
            if (factor <= 0) return;

            centerRatio = Math.Max(0.0, Math.Min(1.0, centerRatio));
            double currentSpan = _visibleEnd - _visibleStart;
            double targetSpan = currentSpan / factor;

            if (targetSpan > TotalRangeSpan) targetSpan = TotalRangeSpan;
            if (targetSpan < _minRangeSpan) targetSpan = _minRangeSpan;

            double focalValue = _visibleStart + currentSpan * centerRatio;
            double newStart = focalValue - targetSpan * centerRatio;
            double newEnd = newStart + targetSpan;

            if (newStart < _totalStart)
            {
                newStart = _totalStart;
                newEnd = newStart + targetSpan;
            }
            if (newEnd > _totalEnd)
            {
                newEnd = _totalEnd;
                newStart = newEnd - targetSpan;
            }

            SetVisibleRange(newStart, newEnd);
        }

        /// <summary>
        /// Resets the visible range to cover the entire total range span.
        /// </summary>
        public void ResetVisibleRange()
        {
            SetVisibleRange(_totalStart, _totalEnd);
        }

        /// <summary>
        /// Selects the entire total range span.
        /// </summary>
        public void SelectAll()
        {
            SetSelectedRange(_totalStart, _totalEnd);
        }

        #region Coordinate Mapping Projections

        /// <summary>
        /// Maps a domain value to a ratio between 0.0 and 1.0 relative to the VisibleRange.
        /// </summary>
        public double ValueToRatio(double value)
        {
            double span = _visibleEnd - _visibleStart;
            if (Math.Abs(span) < 1e-12) return 0.0;
            return (value - _visibleStart) / span;
        }

        /// <summary>
        /// Maps a ratio between 0.0 and 1.0 relative to the VisibleRange back to domain value.
        /// </summary>
        public double RatioToValue(double ratio)
        {
            double span = _visibleEnd - _visibleStart;
            return _visibleStart + ratio * span;
        }

        /// <summary>
        /// Projects a domain value to horizontal pixel coordinate on canvas of given width.
        /// </summary>
        public double ValueToPixel(double value, double canvasWidth)
        {
            return ValueToRatio(value) * canvasWidth;
        }

        /// <summary>
        /// Projects a canvas pixel coordinate back to domain value.
        /// </summary>
        public double PixelToValue(double pixelX, double canvasWidth)
        {
            if (canvasWidth <= 0) return _visibleStart;
            double ratio = pixelX / canvasWidth;
            return RatioToValue(ratio);
        }

        #endregion

        #region Snapping Algorithms

        public double Snap(double value)
        {
            if (DataType == RangeDataType.DateTime)
            {
                return SnapDateTime(value);
            }

            if (NumericStep <= 0) return value;
            double steps = Math.Round((value - _totalStart) / NumericStep);
            return _totalStart + steps * NumericStep;
        }

        private double SnapDateTime(double oaDate)
        {
            try
            {
                DateTime dt = DateTime.FromOADate(oaDate);
                switch (Interval)
                {
                    case RangeInterval.Year:
                        return new DateTime(dt.Year, 1, 1).ToOADate();
                    case RangeInterval.Quarter:
                        int qMonth = ((dt.Month - 1) / 3) * 3 + 1;
                        return new DateTime(dt.Year, qMonth, 1).ToOADate();
                    case RangeInterval.Month:
                        return new DateTime(dt.Year, dt.Month, 1).ToOADate();
                    case RangeInterval.Day:
                    case RangeInterval.Auto:
                        return dt.Date.ToOADate();
                    case RangeInterval.Hour:
                        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0).ToOADate();
                    case RangeInterval.Minute:
                        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0).ToOADate();
                    case RangeInterval.Second:
                        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second).ToOADate();
                    default:
                        return dt.Date.ToOADate();
                }
            }
            catch
            {
                return oaDate;
            }
        }

        #endregion

        private void ClampRanges()
        {
            SetVisibleRange(_visibleStart, _visibleEnd);
            SetSelectedRange(_selectedStart, _selectedEnd);
        }

        private static DateTime ToDateTimeSafe(double oaDate)
        {
            try
            {
                if (oaDate < 0 || oaDate > 2958465) return DateTime.Today;
                return DateTime.FromOADate(oaDate);
            }
            catch
            {
                return DateTime.Today;
            }
        }
    }
}
