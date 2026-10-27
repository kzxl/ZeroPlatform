using System;

namespace ZeroUI.Core.Input
{
    /// <summary>
    /// Pure, zero-allocation mathematical utilities for ranged controls (Sliders, ProgressBars, Gauges, Steppers).
    /// </summary>
    public static class RangeMath
    {
        public static float Clamp(float value, float min, float max)
        {
            if (min > max)
            {
                float temp = min;
                min = max;
                max = temp;
            }

            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float CalculateFraction(float value, float min, float max)
        {
            float range = max - min;
            if (Math.Abs(range) < 0.00001f) return 0f;

            float clamped = Clamp(value, min, max);
            return (clamped - min) / range;
        }

        public static float CalculateValueFromFraction(float fraction, float min, float max)
        {
            float clampedFraction = Math.Max(0f, Math.Min(1f, fraction));
            float range = max - min;
            return min + (clampedFraction * range);
        }

        public static float SnapToStep(float value, float min, float max, float step)
        {
            float clamped = Clamp(value, min, max);
            if (step <= 0f) return clamped;

            float stepsFromMin = (float)Math.Round((clamped - min) / step);
            float snapped = min + (stepsFromMin * step);
            return Clamp(snapped, min, max);
        }
    }

    /// <summary>
    /// Framework-independent, observable range state coordinator.
    /// Manages min, max, value, step, and normalized fractions with change notification.
    /// </summary>
    public class RangeModel
    {
        private float _minimum = 0f;
        private float _maximum = 100f;
        private float _value = 0f;
        private float _step = 1f;

        public event EventHandler? ValueChanged;
        public event EventHandler? RangeChanged;

        public RangeModel(float minimum = 0f, float maximum = 100f, float value = 0f, float step = 1f)
        {
            _minimum = minimum;
            _maximum = Math.Max(minimum, maximum);
            _step = Math.Max(0.0001f, step);
            _value = RangeMath.SnapToStep(value, _minimum, _maximum, _step);
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
                    _value = RangeMath.SnapToStep(_value, _minimum, _maximum, _step);
                    RangeChanged?.Invoke(this, EventArgs.Empty);
                    ValueChanged?.Invoke(this, EventArgs.Empty);
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
                    _value = RangeMath.SnapToStep(_value, _minimum, _maximum, _step);
                    RangeChanged?.Invoke(this, EventArgs.Empty);
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public float Step
        {
            get => _step;
            set
            {
                float newStep = Math.Max(0.0001f, value);
                if (Math.Abs(_step - newStep) > 0.0001f)
                {
                    _step = newStep;
                    _value = RangeMath.SnapToStep(_value, _minimum, _maximum, _step);
                    RangeChanged?.Invoke(this, EventArgs.Empty);
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public float Value
        {
            get => _value;
            set
            {
                float snapped = RangeMath.SnapToStep(value, _minimum, _maximum, _step);
                if (Math.Abs(_value - snapped) > 0.0001f)
                {
                    _value = snapped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public float Fraction
        {
            get => RangeMath.CalculateFraction(_value, _minimum, _maximum);
            set => Value = RangeMath.CalculateValueFromFraction(value, _minimum, _maximum);
        }

        public void SetRange(float minimum, float maximum)
        {
            _minimum = minimum;
            _maximum = Math.Max(minimum, maximum);
            _value = RangeMath.SnapToStep(_value, _minimum, _maximum, _step);
            RangeChanged?.Invoke(this, EventArgs.Empty);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Increment(float multiplier = 1f)
        {
            Value += _step * multiplier;
        }

        public void Decrement(float multiplier = 1f)
        {
            Value -= _step * multiplier;
        }
    }
}
