using System;

namespace ZeroUI.Core.Validation.Rules
{
    /// <summary>
    /// Validation rule driven by a custom predicate delegate.
    /// </summary>
    public class PredicateRule<T> : IValidationRule<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly string _errorMessage;
        private readonly ValidationSeverity _severity;
        private readonly string? _propertyName;

        public PredicateRule(Func<T, bool> predicate, string errorMessage, ValidationSeverity severity = ValidationSeverity.Error, string? propertyName = null)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _errorMessage = errorMessage ?? "Validation failed";
            _severity = severity;
            _propertyName = propertyName;
        }

        public ValidationResult Validate(T value)
        {
            if (_predicate(value))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(new[]
            {
                new ValidationMessage(_errorMessage, _severity, _propertyName)
            });
        }
    }
}
