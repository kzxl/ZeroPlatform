using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Encapsulates the outcome of a validation operation, supporting multiple severities.
    /// </summary>
    public class ValidationResult
    {
        public static readonly ValidationResult Success = new ValidationResult(Array.Empty<ValidationMessage>());

        private readonly IReadOnlyList<ValidationMessage> _messages;
        private readonly bool _hasErrors;
        private readonly bool _hasWarnings;
        private readonly bool _hasInfo;

        public IReadOnlyList<ValidationMessage> Messages => _messages;
        public bool IsValid => !_hasErrors;
        public bool HasErrors => _hasErrors;
        public bool HasWarnings => _hasWarnings;
        public bool HasInfo => _hasInfo;

        public string? PrimaryErrorMessage { get; }
        public string? PrimaryMessage { get; }

        public ValidationResult(IReadOnlyList<ValidationMessage> messages)
        {
            _messages = messages ?? Array.Empty<ValidationMessage>();
            for (int i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                if (PrimaryMessage == null) PrimaryMessage = msg.Text;

                switch (msg.Severity)
                {
                    case ValidationSeverity.Error:
                        _hasErrors = true;
                        if (PrimaryErrorMessage == null) PrimaryErrorMessage = msg.Text;
                        break;
                    case ValidationSeverity.Warning:
                        _hasWarnings = true;
                        break;
                    case ValidationSeverity.Information:
                        _hasInfo = true;
                        break;
                }
            }
        }

        public static ValidationResult Error(string message, string? propertyName = null) =>
            new ValidationResult(new[] { new ValidationMessage(message, ValidationSeverity.Error, propertyName) });

        public static ValidationResult Warning(string message, string? propertyName = null) =>
            new ValidationResult(new[] { new ValidationMessage(message, ValidationSeverity.Warning, propertyName) });

        public static ValidationResult Info(string message, string? propertyName = null) =>
            new ValidationResult(new[] { new ValidationMessage(message, ValidationSeverity.Information, propertyName) });
    }
}
