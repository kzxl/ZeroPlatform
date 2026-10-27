using System;
using System.Collections;
using System.Text.RegularExpressions;
using ZeroUI.Core.Localization;

namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Contract for evaluating the validity of an editor's loosely-typed EditValue (object?).
    /// </summary>
    public interface IControlValidationRule
    {
        /// <summary>
        /// Evaluates the editor value and returns a <see cref="ValidationResult"/>.
        /// </summary>
        ValidationResult Validate(object? value);
    }

    /// <summary>
    /// Validates that an editor's value is neither null, whitespace, nor empty collection.
    /// </summary>
    public class RequiredControlRule : IControlValidationRule
    {
        public string ErrorMessage { get; set; }
        public ValidationSeverity Severity { get; set; }

        public RequiredControlRule(string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            ErrorMessage = errorMessage ?? Localizer.GetString(StringId.ValRequired);
            Severity = severity;
        }

        public ValidationResult Validate(object? value)
        {
            if (value == null)
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }

            if (value is ICollection coll && coll.Count == 0)
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a numeric, date, or comparable editor value falls within an inclusive [Min, Max] range.
    /// </summary>
    public class RangeControlRule<T> : IControlValidationRule where T : IComparable<T>
    {
        public T? Min { get; set; }
        public T? Max { get; set; }
        public string ErrorMessage { get; set; }
        public ValidationSeverity Severity { get; set; }

        public RangeControlRule(T? min, T? max, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Min = min;
            Max = max;
            Severity = severity;
            ErrorMessage = errorMessage ?? Localizer.GetFormattedString(StringId.ValRangeFormat, min?.ToString() ?? "", max?.ToString() ?? "");
        }

        public ValidationResult Validate(object? value)
        {
            if (value == null) return ValidationResult.Success;

            try
            {
                T compVal;
                if (value is T typed)
                {
                    compVal = typed;
                }
                else
                {
                    compVal = (T)Convert.ChangeType(value, typeof(T));
                }

                if (Min != null && compVal.CompareTo(Min) < 0)
                {
                    return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
                }

                if (Max != null && compVal.CompareTo(Max) > 0)
                {
                    return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
                }
            }
            catch
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that an editor's string value matches a Regular Expression pattern.
    /// </summary>
    public class RegexControlRule : IControlValidationRule
    {
        private readonly Regex _regex;
        public string ErrorMessage { get; set; }
        public ValidationSeverity Severity { get; set; }

        public RegexControlRule(string pattern, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            _regex = new Regex(pattern, RegexOptions.Compiled);
            ErrorMessage = errorMessage ?? Localizer.GetString(StringId.ValInvalidFormat);
            Severity = severity;
        }

        public ValidationResult Validate(object? value)
        {
            if (value == null) return ValidationResult.Success;
            string str = value.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(str)) return ValidationResult.Success;

            return _regex.IsMatch(str) ? ValidationResult.Success : new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
        }
    }

    /// <summary>
    /// Validates character length constraints on an editor's value.
    /// </summary>
    public class StringLengthControlRule : IControlValidationRule
    {
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public string ErrorMessage { get; set; }
        public ValidationSeverity Severity { get; set; }

        public StringLengthControlRule(int minLength, int maxLength, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            MinLength = Math.Max(0, minLength);
            MaxLength = Math.Max(MinLength, maxLength);
            Severity = severity;
            ErrorMessage = errorMessage ?? Localizer.GetFormattedString(StringId.ValStringLengthFormat, MinLength, MaxLength);
        }

        public ValidationResult Validate(object? value)
        {
            if (value == null) return ValidationResult.Success;
            string str = value.ToString() ?? string.Empty;
            if (str.Length < MinLength || str.Length > MaxLength)
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Custom validation rule based on a predicate or evaluator function.
    /// </summary>
    public class CustomControlRule : IControlValidationRule
    {
        private readonly Func<object?, ValidationResult> _evaluator;

        public CustomControlRule(Func<object?, ValidationResult> evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public CustomControlRule(Func<object?, bool> predicate, string errorMessage, ValidationSeverity severity = ValidationSeverity.Error)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            _evaluator = val => predicate(val)
                ? ValidationResult.Success
                : new ValidationResult(new[] { new ValidationMessage(errorMessage, severity) });
        }

        public ValidationResult Validate(object? value) => _evaluator(value);
    }

    /// <summary>
    /// Factory helpers for quickly configuring validation rules for editors.
    /// </summary>
    public static class ControlValidationRules
    {
        public static IControlValidationRule Required(string? message = null, ValidationSeverity severity = ValidationSeverity.Error) =>
            new RequiredControlRule(message, severity);

        public static IControlValidationRule Range<T>(T min, T max, string? message = null, ValidationSeverity severity = ValidationSeverity.Error) where T : IComparable<T> =>
            new RangeControlRule<T>(min, max, message, severity);

        public static IControlValidationRule Email(string? message = null, ValidationSeverity severity = ValidationSeverity.Error) =>
            new RegexControlRule(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", message ?? Localizer.GetString(StringId.ValEmail), severity);

        public static IControlValidationRule Phone(string? message = null, ValidationSeverity severity = ValidationSeverity.Error) =>
            new RegexControlRule(@"^[+0-9\s\-()]{7,20}$", message ?? Localizer.GetString(StringId.ValPhone), severity);

        public static IControlValidationRule StringLength(int min, int max, string? message = null, ValidationSeverity severity = ValidationSeverity.Error) =>
            new StringLengthControlRule(min, max, message, severity);

        public static IControlValidationRule Custom(Func<object?, ValidationResult> evaluator) =>
            new CustomControlRule(evaluator);

        public static IControlValidationRule Custom(Func<object?, bool> predicate, string errorMessage, ValidationSeverity severity = ValidationSeverity.Error) =>
            new CustomControlRule(predicate, errorMessage, severity);
    }
}
