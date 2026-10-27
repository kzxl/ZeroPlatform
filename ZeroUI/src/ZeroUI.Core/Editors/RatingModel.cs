using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Vector glyph shape for rating controls.
    /// </summary>
    public enum RatingShape
    {
        Star,
        Diamond,
        Heart,
        Shield
    }

    /// <summary>
    /// Headless mathematical state machine and spatial score calculator for <c>RatingControl</c>.
    /// Enforces rating bounds, fractional half-step (0.5) snapping, keyboard stepping,
    /// and spatial hit testing from pointer coordinates.
    /// </summary>
    public class RatingModel
    {
        private decimal _value = 0m;
        private int _maxRating = 5;
        private bool _allowHalf = true;

        public event EventHandler? ValueChanged;
        public event EventHandler? PropertiesChanged;

        public RatingModel(decimal initialValue = 0m, int maxRating = 5, bool allowHalf = true)
        {
            _maxRating = Math.Max(1, maxRating);
            _allowHalf = allowHalf;
            _value = ClampValue(initialValue);
        }

        public decimal Value
        {
            get => _value;
            set
            {
                decimal clamped = ClampValue(value);
                if (_value != clamped)
                {
                    _value = clamped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int MaxRating
        {
            get => _maxRating;
            set
            {
                int val = Math.Max(1, value);
                if (_maxRating != val)
                {
                    _maxRating = val;
                    if (_value > _maxRating)
                    {
                        _value = _maxRating;
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                    PropertiesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowHalf
        {
            get => _allowHalf;
            set
            {
                if (_allowHalf != value)
                {
                    _allowHalf = value;
                    decimal clamped = ClampValue(_value);
                    if (_value != clamped)
                    {
                        _value = clamped;
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                    PropertiesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public decimal Step => _allowHalf ? 0.5m : 1.0m;

        public decimal ClampValue(decimal val)
        {
            decimal clamped = Math.Max(0m, Math.Min(_maxRating, val));
            return _allowHalf
                ? (Math.Round(clamped * 2m, MidpointRounding.AwayFromZero) / 2m)
                : Math.Round(clamped, MidpointRounding.AwayFromZero);
        }

        public decimal CalculateScoreFromPosition(double x, double startX, double itemSize, double itemSpacing)
        {
            int count = Math.Max(1, _maxRating);
            double totalSpan = itemSize + itemSpacing;

            for (int i = 0; i < count; i++)
            {
                double itemLeft = startX + (i * totalSpan);
                double itemRight = itemLeft + itemSize;

                if (x < itemLeft)
                {
                    return i; // Before this glyph
                }

                if (x <= itemRight)
                {
                    if (_allowHalf)
                    {
                        double mid = itemLeft + (itemSize / 2.0);
                        return x < mid ? (i + 0.5m) : (i + 1.0m);
                    }
                    return i + 1.0m;
                }
            }

            return count;
        }

        public void StepUp()
        {
            Value = Math.Min(_maxRating, _value + Step);
        }

        public void StepDown()
        {
            Value = Math.Max(0m, _value - Step);
        }

        public void ToggleOrSet(decimal newScore)
        {
            // Toggle off if clicking the exact current score when value is 1
            if (newScore == _value && newScore == 1m)
            {
                Value = 0m;
            }
            else
            {
                Value = newScore;
            }
        }

        public void Clear()
        {
            Value = 0m;
        }
    }
}
