using System;
using System.Collections.Generic;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Validation
{
    public class ValidationEventArgs : EventArgs
    {
        public IZeroEditor Editor { get; }
        public ValidationResult Result { get; }

        public ValidationEventArgs(IZeroEditor editor, ValidationResult result)
        {
            Editor = editor;
            Result = result;
        }
    }

    /// <summary>
    /// Core coordinator for form validation and error notification.
    /// Manages editor rules, evaluation triggers, and error state collections.
    /// </summary>
    public class ValidationProviderBase
    {
        private class EditorBinding
        {
            public IZeroEditor Editor { get; }
            public List<IControlValidationRule> Rules { get; } = new List<IControlValidationRule>();
            public ValidationTrigger Trigger { get; set; } = ValidationTrigger.ValueChanged;

            public EditorBinding(IZeroEditor editor, ValidationTrigger trigger)
            {
                Editor = editor;
                Trigger = trigger;
            }
        }

        private readonly Dictionary<IZeroEditor, EditorBinding> _bindings = new Dictionary<IZeroEditor, EditorBinding>();
        private readonly Dictionary<IZeroEditor, ValidationResult> _errors = new Dictionary<IZeroEditor, ValidationResult>();

        public event EventHandler<ValidationEventArgs>? ValidationFailed;
        public event EventHandler<ValidationEventArgs>? ValidationPassed;
        public event EventHandler? ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;
        public int ErrorCount => _errors.Count;

        public IReadOnlyDictionary<IZeroEditor, ValidationResult> CurrentErrors => _errors;

        /// <summary>
        /// Registers an editor with a validation rule and trigger mode.
        /// </summary>
        public void SetRule(IZeroEditor editor, IControlValidationRule rule, ValidationTrigger trigger = ValidationTrigger.ValueChanged)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            if (!_bindings.TryGetValue(editor, out var binding))
            {
                binding = new EditorBinding(editor, trigger);
                _bindings[editor] = binding;
                editor.EditValueChanged += OnEditorValueChanged;
            }

            binding.Trigger = trigger;
            if (!binding.Rules.Contains(rule))
            {
                binding.Rules.Add(rule);
            }
        }

        /// <summary>
        /// Registers multiple validation rules for an editor.
        /// </summary>
        public void SetRules(IZeroEditor editor, IEnumerable<IControlValidationRule> rules, ValidationTrigger trigger = ValidationTrigger.ValueChanged)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                SetRule(editor, rule, trigger);
            }
        }

        /// <summary>
        /// Removes all validation rules and bindings associated with the specified editor.
        /// </summary>
        public void RemoveRules(IZeroEditor editor)
        {
            if (editor == null) return;
            if (_bindings.TryGetValue(editor, out var binding))
            {
                editor.EditValueChanged -= OnEditorValueChanged;
                _bindings.Remove(editor);
            }
            ClearError(editor);
        }

        /// <summary>
        /// Manually sets an error state on a specific editor.
        /// </summary>
        public void SetError(IZeroEditor editor, string? errorMessage, ValidationSeverity severity = ValidationSeverity.Error)
        {
            if (editor == null) return;

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                ClearError(editor);
            }
            else
            {
                var result = new ValidationResult(new[] { new ValidationMessage(errorMessage!, severity) });
                _errors[editor] = result;
                OnErrorStateChanged(editor, result);
                ValidationFailed?.Invoke(this, new ValidationEventArgs(editor, result));
                ErrorsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Retrieves the current error result for an editor, or null if valid.
        /// </summary>
        public ValidationResult? GetError(IZeroEditor editor)
        {
            if (editor != null && _errors.TryGetValue(editor, out var res))
            {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Clears the error state on a specific editor.
        /// </summary>
        public void ClearError(IZeroEditor editor)
        {
            if (editor != null && _errors.Remove(editor))
            {
                OnErrorStateChanged(editor, ValidationResult.Success);
                ValidationPassed?.Invoke(this, new ValidationEventArgs(editor, ValidationResult.Success));
                ErrorsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clears all active error notifications across all registered editors.
        /// </summary>
        public void ClearErrors()
        {
            if (_errors.Count == 0) return;

            var affectedEditors = new List<IZeroEditor>(_errors.Keys);
            _errors.Clear();

            foreach (var editor in affectedEditors)
            {
                OnErrorStateChanged(editor, ValidationResult.Success);
                ValidationPassed?.Invoke(this, new ValidationEventArgs(editor, ValidationResult.Success));
            }
            ErrorsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Validates a single editor against all its registered rules.
        /// </summary>
        public bool Validate(IZeroEditor editor)
        {
            if (editor == null) return true;

            if (!_bindings.TryGetValue(editor, out var binding) || binding.Rules.Count == 0)
            {
                ClearError(editor);
                return true;
            }

            object? val = editor.EditValue;
            foreach (var rule in binding.Rules)
            {
                var res = rule.Validate(val);
                if (!res.IsValid)
                {
                    _errors[editor] = res;
                    OnErrorStateChanged(editor, res);
                    ValidationFailed?.Invoke(this, new ValidationEventArgs(editor, res));
                    ErrorsChanged?.Invoke(this, EventArgs.Empty);
                    return false;
                }
            }

            ClearError(editor);
            return true;
        }

        /// <summary>
        /// Validates all registered editors across the form/container.
        /// Returns true if all editors are valid; false if any validation failed.
        /// </summary>
        public bool Validate()
        {
            bool allValid = true;
            foreach (var kvp in _bindings)
            {
                var editor = kvp.Key;
                bool valid = Validate(editor);
                if (!valid)
                {
                    allValid = false;
                }
            }
            return allValid;
        }

        /// <summary>
        /// Returns the first editor currently in an invalid state, or null if all are valid.
        /// Useful for focusing the first error field on form submission.
        /// </summary>
        public IZeroEditor? GetFirstInvalid()
        {
            foreach (var kvp in _bindings)
            {
                if (_errors.ContainsKey(kvp.Key))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private void OnEditorValueChanged(object? sender, EventArgs e)
        {
            if (sender is IZeroEditor editor && _bindings.TryGetValue(editor, out var binding))
            {
                if (binding.Trigger == ValidationTrigger.ValueChanged)
                {
                    Validate(editor);
                }
            }
        }

        /// <summary>
        /// Virtual hook invoked when an editor's error state changes, allowing platform-specific visual updates.
        /// </summary>
        protected virtual void OnErrorStateChanged(IZeroEditor editor, ValidationResult result)
        {
            // Platform visual subclasses (WinForms, WPF) override to attach/detach error glyph badges
        }
    }
}
