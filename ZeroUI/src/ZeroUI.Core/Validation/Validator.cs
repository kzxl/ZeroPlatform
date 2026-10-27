using System;
using System.Collections.Generic;
using ZeroUI.Core.Input.Masking;
using ZeroUI.Core.Validation.Rules;

namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Fluent, high-performance rule aggregator for validating values of type T.
    /// </summary>
    public class Validator<T> : IValidationRule<T>
    {
        private readonly List<IValidationRule<T>> _rules = new List<IValidationRule<T>>();

        public IReadOnlyList<IValidationRule<T>> Rules => _rules;

        public Validator<T> AddRule(IValidationRule<T> rule)
        {
            if (rule != null) _rules.Add(rule);
            return this;
        }

        public Validator<T> Must(Func<T, bool> predicate, string errorMessage, ValidationSeverity severity = ValidationSeverity.Error, string? propertyName = null)
        {
            _rules.Add(new PredicateRule<T>(predicate, errorMessage, severity, propertyName));
            return this;
        }

        public ValidationResult Validate(T value)
        {
            List<ValidationMessage>? messages = null;

            for (int i = 0; i < _rules.Count; i++)
            {
                var res = _rules[i].Validate(value);
                if (!res.IsValid || res.Messages.Count > 0)
                {
                    if (messages == null) messages = new List<ValidationMessage>();
                    for (int j = 0; j < res.Messages.Count; j++)
                    {
                        messages.Add(res.Messages[j]);
                    }
                }
            }

            return messages == null || messages.Count == 0 ? ValidationResult.Success : new ValidationResult(messages);
        }
    }

    /// <summary>
    /// Extension methods providing fluent validation rules for common types.
    /// </summary>
    public static class ValidatorExtensions
    {
        public static Validator<T> InRange<T>(this Validator<T> validator, T minimum, T maximum, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
            where T : IComparable<T>
        {
            return validator.AddRule(new RangeRule<T>(minimum, maximum, errorMessage, severity));
        }

        public static Validator<string?> NotEmpty(this Validator<string?> validator, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            return validator.AddRule(new NotEmptyRule(errorMessage, severity));
        }

        public static Validator<string?> Length(this Validator<string?> validator, int minLength, int maxLength, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            return validator.AddRule(new StringLengthRule(minLength, maxLength, errorMessage, severity));
        }

        public static Validator<string?> MatchesMask(this Validator<string?> validator, MaskDefinition definition, char prompt = '_', string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error)
        {
            return validator.AddRule(new MaskMatchRule(definition, prompt, errorMessage, severity));
        }
    }
}
