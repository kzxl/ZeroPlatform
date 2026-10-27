using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Validation.Rules
{
    /// <summary>
    /// Validates that a value falls within an inclusive [min, max] range.
    /// </summary>
    public class RangeRule<T> : IValidationRule<T> where T : IComparable<T>
    {
        public T Minimum { get; }
        public T Maximum { get; }
        public string ErrorMessage { get; }
        public ValidationSeverity Severity { get; }

        public RangeRule(T minimum, T maximum, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            if (minimum.CompareTo(maximum) > 0)
                throw new ArgumentException("Minimum must be less than or equal to Maximum.");

            Minimum = minimum;
            Maximum = maximum;
            Severity = severity;
            ErrorMessage = errorMessage ?? $"Value must be between {minimum} and {maximum}.";
        }

        public ValidationResult Validate(T value)
        {
            if (Comparer<T>.Default.Compare(value, Minimum) < 0 || Comparer<T>.Default.Compare(value, Maximum) > 0)
            {
                return new ValidationResult(new[]
                {
                    new ValidationMessage(ErrorMessage, Severity)
                });
            }

            return ValidationResult.Success;
        }
    }
}
