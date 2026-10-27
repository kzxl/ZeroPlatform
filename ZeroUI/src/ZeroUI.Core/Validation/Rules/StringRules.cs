using System;
using ZeroUI.Core.Input.Masking;

namespace ZeroUI.Core.Validation.Rules
{
    /// <summary>
    /// Validates that a string is neither null nor whitespace.
    /// </summary>
    public class NotEmptyRule : IValidationRule<string?>
    {
        public string ErrorMessage { get; }
        public ValidationSeverity Severity { get; }

        public NotEmptyRule(string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            ErrorMessage = errorMessage ?? "Field cannot be empty.";
            Severity = severity;
        }

        public ValidationResult Validate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a string length falls within [min, max].
    /// </summary>
    public class StringLengthRule : IValidationRule<string?>
    {
        public int MinLength { get; }
        public int MaxLength { get; }
        public string ErrorMessage { get; }
        public ValidationSeverity Severity { get; }

        public StringLengthRule(int minLength, int maxLength, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            MinLength = Math.Max(0, minLength);
            MaxLength = Math.Max(MinLength, maxLength);
            Severity = severity;
            ErrorMessage = errorMessage ?? $"Text length must be between {MinLength} and {MaxLength} characters.";
        }

        public ValidationResult Validate(string? value)
        {
            int len = value?.Length ?? 0;
            if (len < MinLength || len > MaxLength)
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a string matches a compiled MaskDefinition and contains no unprompted slots.
    /// </summary>
    public class MaskMatchRule : IValidationRule<string?>
    {
        private readonly MaskDefinition _definition;
        private readonly char _prompt;
        public string ErrorMessage { get; }
        public ValidationSeverity Severity { get; }

        public MaskMatchRule(MaskDefinition definition, char prompt = '_', string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _prompt = prompt;
            Severity = severity;
            ErrorMessage = errorMessage ?? "Input does not match required mask template.";
        }

        public ValidationResult Validate(string? value)
        {
            if (string.IsNullOrEmpty(value) || value!.Length != _definition.Length)
            {
                return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
            }

            for (int i = 0; i < _definition.Length; i++)
            {
                char c = value[i];
                var token = _definition[i];

                if (!token.IsEditable)
                {
                    if (c != token.LiteralChar)
                        return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
                }
                else
                {
                    if (c == _prompt || !token.Matches(c))
                        return new ValidationResult(new[] { new ValidationMessage(ErrorMessage, Severity) });
                }
            }

            return ValidationResult.Success;
        }
    }
}
