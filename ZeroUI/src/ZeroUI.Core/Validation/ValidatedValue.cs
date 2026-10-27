using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Reactive observable container wrapping a value of type T with an integrated Validator.
    /// Re-evaluates rules on assignment and signals validation state changes to UI listeners.
    /// </summary>
    public class ValidatedValue<T>
    {
        private T _value;
        private ValidationResult _result = ValidationResult.Success;
        private Validator<T>? _validator;

        public event EventHandler<ValidationResult>? Validated;
        public event EventHandler? ValueChanged;

        public ValidatedValue(T initialValue, Validator<T>? validator = null)
        {
            _value = initialValue;
            _validator = validator;
            Validate();
        }

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    Validate();
                }
            }
        }

        public Validator<T>? Validator
        {
            get => _validator;
            set
            {
                if (_validator != value)
                {
                    _validator = value;
                    Validate();
                }
            }
        }

        public ValidationResult Result => _result;
        public bool IsValid => _result.IsValid;
        public bool HasErrors => _result.HasErrors;
        public bool HasWarnings => _result.HasWarnings;

        public ValidationResult Validate()
        {
            _result = _validator != null ? _validator.Validate(_value) : ValidationResult.Success;
            Validated?.Invoke(this, _result);
            return _result;
        }

        public override string ToString() => _value?.ToString() ?? string.Empty;
    }
}
