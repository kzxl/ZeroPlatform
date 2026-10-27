using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Validation;

namespace ZeroUI.Core.Tests
{
    public class ValidationProviderTests
    {
        private sealed class MockEditor : IZeroEditor
        {
            private object? _value;

            public object? EditValue
            {
                get => _value;
                set
                {
                    _value = value;
                    IsModified = true;
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public bool IsModified { get; set; }
            public bool ReadOnly { get; set; }

            public event EventHandler? EditValueChanged;

            public void Reset()
            {
                _value = null;
                IsModified = false;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Clear() => Reset();
        }

        [Fact]
        public void RequiredRule_EvaluatesCorrectly()
        {
            var rule = ControlValidationRules.Required("Required text");

            Assert.False(rule.Validate(null).IsValid);
            Assert.False(rule.Validate("").IsValid);
            Assert.False(rule.Validate("   ").IsValid);
            Assert.False(rule.Validate(new List<string>()).IsValid);

            Assert.True(rule.Validate("Valid input").IsValid);
            Assert.True(rule.Validate(123).IsValid);
            Assert.True(rule.Validate(new List<string> { "Item" }).IsValid);
        }

        [Fact]
        public void RangeRule_EvaluatesCorrectly()
        {
            var intRule = ControlValidationRules.Range(10, 50, "Between 10 and 50");

            Assert.True(intRule.Validate(null).IsValid); // null allowed unless combined with Required
            Assert.True(intRule.Validate(10).IsValid);
            Assert.True(intRule.Validate(25).IsValid);
            Assert.True(intRule.Validate(50).IsValid);
            Assert.False(intRule.Validate(9).IsValid);
            Assert.False(intRule.Validate(51).IsValid);
            Assert.False(intRule.Validate("invalid_number").IsValid);

            var dateRule = ControlValidationRules.Range(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            Assert.True(dateRule.Validate(new DateTime(2026, 6, 1)).IsValid);
            Assert.False(dateRule.Validate(new DateTime(2025, 12, 31)).IsValid);
            Assert.False(dateRule.Validate(new DateTime(2027, 1, 1)).IsValid);
        }

        [Fact]
        public void RegexRule_EmailAndPhone_EvaluatesCorrectly()
        {
            var emailRule = ControlValidationRules.Email();

            Assert.True(emailRule.Validate(null).IsValid);
            Assert.True(emailRule.Validate("").IsValid);
            Assert.True(emailRule.Validate("admin@company.com").IsValid);
            Assert.False(emailRule.Validate("invalid_email").IsValid);
            Assert.False(emailRule.Validate("missing@domain").IsValid);

            var phoneRule = ControlValidationRules.Phone();
            Assert.True(phoneRule.Validate("+84 901 234 567").IsValid);
            Assert.True(phoneRule.Validate("0901234567").IsValid);
            Assert.False(phoneRule.Validate("abc").IsValid);
        }

        [Fact]
        public void StringLengthRule_EvaluatesCorrectly()
        {
            var lenRule = ControlValidationRules.StringLength(3, 8);

            Assert.True(lenRule.Validate(null).IsValid);
            Assert.False(lenRule.Validate("ab").IsValid);
            Assert.True(lenRule.Validate("abc").IsValid);
            Assert.True(lenRule.Validate("abcdefgh").IsValid);
            Assert.False(lenRule.Validate("abcdefghi").IsValid);
        }

        [Fact]
        public void CustomRule_EvaluatesCorrectly()
        {
            var rule = ControlValidationRules.Custom(val => val is int n && n % 2 == 0, "Must be even");

            Assert.True(rule.Validate(4).IsValid);
            Assert.False(rule.Validate(5).IsValid);
        }

        [Fact]
        public void ValidationProviderBase_LiveTriggerAndFormValidation_WorksAccurately()
        {
            var provider = new ValidationProviderBase();

            var txtUsername = new MockEditor();
            var spinAge = new MockEditor();

            provider.SetRule(txtUsername, ControlValidationRules.Required("Username is required"));
            provider.SetRule(spinAge, ControlValidationRules.Required("Age is required"));
            provider.SetRule(spinAge, ControlValidationRules.Range(18, 65, "Age must be between 18 and 65"));

            // Initially both are invalid because EditValue is null
            Assert.False(provider.Validate());
            Assert.True(provider.HasErrors);
            Assert.Equal(txtUsername, provider.GetFirstInvalid());

            // Fill txtUsername
            txtUsername.EditValue = "johndoe";
            // txtUsername has ValueChanged trigger, so it re-validates itself automatically!
            Assert.Null(provider.GetError(txtUsername));
            Assert.Equal(spinAge, provider.GetFirstInvalid());

            // Fill spinAge with out-of-range value
            spinAge.EditValue = 16;
            Assert.False(provider.Validate());
            var err = provider.GetError(spinAge);
            Assert.NotNull(err);
            Assert.Equal("Age must be between 18 and 65", err!.PrimaryErrorMessage);

            // Correct spinAge to valid value
            spinAge.EditValue = 28;
            Assert.True(provider.Validate());
            Assert.False(provider.HasErrors);
            Assert.Null(provider.GetFirstInvalid());
        }

        [Fact]
        public void ValidationProviderBase_ManualErrorsAndClear_WorkReliably()
        {
            var provider = new ValidationProviderBase();
            var editor = new MockEditor();

            Assert.False(provider.HasErrors);

            provider.SetError(editor, "Custom server error", ValidationSeverity.Error);
            Assert.True(provider.HasErrors);
            Assert.Equal("Custom server error", provider.GetError(editor)?.PrimaryErrorMessage);

            provider.ClearError(editor);
            Assert.False(provider.HasErrors);
            Assert.Null(provider.GetError(editor));

            provider.SetError(editor, "Warning note", ValidationSeverity.Warning);
            Assert.True(provider.HasErrors);
            Assert.True(provider.GetError(editor)?.HasWarnings);

            provider.ClearErrors();
            Assert.False(provider.HasErrors);
        }
    }
}
