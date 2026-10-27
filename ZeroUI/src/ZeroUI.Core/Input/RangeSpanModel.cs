using System;

namespace ZeroUI.Core.Input
{
    /// <summary>
    /// Headless mathematical and state model for dual-thumb interval selectors (<c>RangeSlider</c>).
    /// Enforces minimum/maximum bounds, minimum range separation, step snapping,
    /// normalized fractions, and middle span translation.
    /// </summary>
    public class RangeSpanModel
    {
        private float _minimum = 0f;
        private float _maximum = 100f;
        private float _lowerValue = 20f;
        private float _upperValue = 80f;
        private float _step = 1f;
        private float _minRangeSpan = 0f;

        public event EventHandler? ValuesChanged;
        public event EventHandler? RangeChanged;

        public RangeSpanModel(float minimum = 0f, float maximum = 100f, float lowerValue = 20f, float upperValue = 80f, float step = 1f, float minRangeSpan = 0f)
        {
            _minimum = minimum;
            _maximum = Math.Max(minimum, maximum);
            _step = Math.Max(0.0001f, step);
            _minRangeSpan = Math.Max(0f, minRangeSpan);

            SetValuesInternal(lowerValue, upperValue);
        }

        public float Minimum
        {
            get => _minimum;
            set
            {
                if (Math.Abs(_minimum - value) > 0.0001f)
                {
                    _minimum = value;
                    if (_maximum < _minimum) _maximum = _minimum;
                    SetValuesInternal(_lowerValue, _upperValue);
                    RangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public float Maximum
        {
            get => _maximum;
            set
            {
                if (Math.Abs(_maximum - value) > 0.0001f)
                {
                    _maximum = Math.Max(_minimum, value);
                    SetValuesInternal(_lowerValue, _upperValue);
                    RangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public float Step
        {
            get => _step;
            set
            {
                _step = Math.Max(0.0001f, value);
                SetValuesInternal(_lowerValue, _upperValue);
            }
        }

        public float MinRangeSpan
        {
            get => _minRangeSpan;
            set
            {
                _minRangeSpan = Math.Max(0f, value);
                SetValuesInternal(_lowerValue, _upperValue);
            }
        }

        public float LowerValue
        {
            get => _lowerValue;
            set => SetLower(value);
        }

        public float UpperValue
        {
            get => _upperValue;
            set => SetUpper(value);
        }

        public float Span => _upperValue - _lowerValue;

        public float LowerFraction => RangeMath.CalculateFraction(_lowerValue, _minimum, _maximum);
        public float UpperFraction => RangeMath.CalculateFraction(_upperValue, _minimum, _maximum);
        public float SpanFraction => UpperFraction - LowerFraction;

        public void SetLower(float value)
        {
            float snapped = RangeMath.SnapToStep(value, _minimum, _maximum, _step);
            float maxAllowed = _upperValue - _minRangeSpan;
            if (snapped > maxAllowed) snapped = maxAllowed;
            if (snapped < _minimum) snapped = _minimum;

            if (Math.Abs(_lowerValue - snapped) > 0.0001f)
            {
                _lowerValue = snapped;
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetUpper(float value)
        {
            float snapped = RangeMath.SnapToStep(value, _minimum, _maximum, _step);
            float minAllowed = _lowerValue + _minRangeSpan;
            if (snapped < minAllowed) snapped = minAllowed;
            if (snapped > _maximum) snapped = _maximum;

            if (Math.Abs(_upperValue - snapped) > 0.0001f)
            {
                _upperValue = snapped;
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetLowerFromFraction(float fraction)
        {
            SetLower(RangeMath.CalculateValueFromFraction(fraction, _minimum, _maximum));
        }

        public void SetUpperFromFraction(float fraction)
        {
            SetUpper(RangeMath.CalculateValueFromFraction(fraction, _minimum, _maximum));
        }

        public void SetValues(float lower, float upper)
        {
            if (SetValuesInternal(lower, upper))
            {
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool SetValuesInternal(float lower, float upper)
        {
            float snappedLower = RangeMath.SnapToStep(lower, _minimum, _maximum, _step);
            float snappedUpper = RangeMath.SnapToStep(upper, _minimum, _maximum, _step);

            if (snappedLower < _minimum) snappedLower = _minimum;
            if (snappedUpper > _maximum) snappedUpper = _maximum;

            if (snappedUpper < snappedLower + _minRangeSpan)
            {
                snappedUpper = Math.Min(_maximum, snappedLower + _minRangeSpan);
                snappedLower = Math.Max(_minimum, snappedUpper - _minRangeSpan);
            }

            bool changed = Math.Abs(_lowerValue - snappedLower) > 0.0001f || Math.Abs(_upperValue - snappedUpper) > 0.0001f;
            _lowerValue = snappedLower;
            _upperValue = snappedUpper;
            return changed;
        }

        public void TranslateSpan(float delta)
        {
            float span = Span;
            float newLower = _lowerValue + delta;
            float newUpper = newLower + span;

            if (newLower < _minimum)
            {
                newLower = _minimum;
                newUpper = newLower + span;
            }
            if (newUpper > _maximum)
            {
                newUpper = _maximum;
                newLower = newUpper - span;
            }

            SetValues(newLower, newUpper);
        }

        public void TranslateSpanFromFraction(float deltaFraction)
        {
            float deltaVal = deltaFraction * (_maximum - _minimum);
            TranslateSpan(deltaVal);
        }
    }
}
