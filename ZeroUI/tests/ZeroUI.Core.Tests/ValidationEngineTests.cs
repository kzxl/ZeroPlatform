using System;
using Xunit;
using ZeroUI.Core.Input.Masking;
using ZeroUI.Core.Validation;

namespace ZeroUI.Core.Tests
{
    public class ValidationEngineTests
    {
        [Fact]
        public void Validator_NotEmpty_FailsOnNullOrWhitespace()
        {
            var validator = new Validator<string?>()
                .NotEmpty("Value is required");

            var resNull = validator.Validate(null);
            Assert.False(resNull.IsValid);
            Assert.Equal("Value is required", resNull.PrimaryErrorMessage);

            var resWhite = validator.Validate("   ");
            Assert.False(resWhite.IsValid);

            var resOk = validator.Validate("Valid input");
            Assert.True(resOk.IsValid);
            Assert.Empty(resOk.Messages);
        }

        [Fact]
        public void Validator_InRange_ValidatesNumericBounds()
        {
            var validator = new Validator<double>()
                .InRange(0.0, 100.0, "Value must be between 0 and 100");

            var resLow = validator.Validate(-0.5);
            Assert.False(resLow.IsValid);
            Assert.Equal("Value must be between 0 and 100", resLow.PrimaryErrorMessage);

            var resHigh = validator.Validate(100.1);
            Assert.False(resHigh.IsValid);

            var resOk = validator.Validate(75.5);
            Assert.True(resOk.IsValid);
        }

        [Fact]
        public void Validator_MatchesMask_ValidatesCompleteTemplate()
        {
            var validator = new Validator<string?>()
                .MatchesMask(MaskDefinition.IpV4, prompt: '_', errorMessage: "Invalid IP format");

            var resIncomplete = validator.Validate("192.168.001.00_");
            Assert.False(resIncomplete.IsValid);
            Assert.Equal("Invalid IP format", resIncomplete.PrimaryErrorMessage);

            var resComplete = validator.Validate("192.168.001.001");
            Assert.True(resComplete.IsValid);
        }

        [Fact]
        public void Validator_Must_EvaluatesCustomPredicate()
        {
            var validator = new Validator<int>()
                .Must(x => x % 2 == 0, "Number must be even");

            var resOdd = validator.Validate(7);
            Assert.False(resOdd.IsValid);
            Assert.Equal("Number must be even", resOdd.PrimaryErrorMessage);

            var resEven = validator.Validate(8);
            Assert.True(resEven.IsValid);
        }

        [Fact]
        public void Validator_MultiRule_AggregatesErrorsAndWarnings()
        {
            var validator = new Validator<double>()
                .InRange(0.0, 100.0, "Out of system range", ValidationSeverity.Error)
                .Must(x => x <= 85.0, "Setpoint approaching high safety limit", ValidationSeverity.Warning);

            // Value in normal range
            var resOk = validator.Validate(50.0);
            Assert.True(resOk.IsValid);
            Assert.False(resOk.HasWarnings);

            // Value in warning zone (90.0) -> Valid (no Errors), but HasWarnings is true!
            var resWarn = validator.Validate(90.0);
            Assert.True(resWarn.IsValid);
            Assert.True(resWarn.HasWarnings);
            Assert.Equal("Setpoint approaching high safety limit", resWarn.PrimaryMessage);

            // Value exceeding max (105.0) -> HasErrors is true (and warning is also present)
            var resErr = validator.Validate(105.0);
            Assert.False(resErr.IsValid);
            Assert.True(resErr.HasErrors);
            Assert.True(resErr.HasWarnings);
            Assert.Equal(2, resErr.Messages.Count);
        }

        [Fact]
        public void ValidatedValue_TriggersValidatedEvent_OnValueChange()
        {
            var validator = new Validator<int>()
                .InRange(1, 10, "Must be 1..10");

            var reactive = new ValidatedValue<int>(5, validator);
            Assert.True(reactive.IsValid);

            ValidationResult? eventResult = null;
            reactive.Validated += (s, r) => eventResult = r;

            // Trigger failure
            reactive.Value = 20;
            Assert.False(reactive.IsValid);
            Assert.NotNull(eventResult);
            Assert.False(eventResult!.IsValid);

            // Trigger recovery
            reactive.Value = 8;
            Assert.True(reactive.IsValid);
            Assert.True(eventResult!.IsValid);
        }
    }
}
