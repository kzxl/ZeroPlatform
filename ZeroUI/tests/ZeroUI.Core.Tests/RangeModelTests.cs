using Xunit;
using ZeroUI.Core.Input;

namespace ZeroUI.Core.Tests
{
    public class RangeModelTests
    {
        [Fact]
        public void RangeMath_Clamp_HandlesNormalAndInvertedRanges()
        {
            Assert.Equal(5f, RangeMath.Clamp(5f, 0f, 10f));
            Assert.Equal(0f, RangeMath.Clamp(-10f, 0f, 10f));
            Assert.Equal(10f, RangeMath.Clamp(15f, 0f, 10f));

            // Inverted range min > max
            Assert.Equal(5f, RangeMath.Clamp(5f, 10f, 0f));
        }

        [Fact]
        public void RangeMath_CalculateFraction_ReturnsNormalized0To1()
        {
            Assert.Equal(0.0f, RangeMath.CalculateFraction(0f, 0f, 100f));
            Assert.Equal(0.5f, RangeMath.CalculateFraction(50f, 0f, 100f));
            Assert.Equal(1.0f, RangeMath.CalculateFraction(100f, 0f, 100f));
            Assert.Equal(0.75f, RangeMath.CalculateFraction(75f, 0f, 100f));

            // Zero range safety
            Assert.Equal(0.0f, RangeMath.CalculateFraction(50f, 50f, 50f));
        }

        [Fact]
        public void RangeMath_CalculateValueFromFraction_ReturnsExpectedValue()
        {
            Assert.Equal(0f, RangeMath.CalculateValueFromFraction(0f, 0f, 100f));
            Assert.Equal(50f, RangeMath.CalculateValueFromFraction(0.5f, 0f, 100f));
            Assert.Equal(100f, RangeMath.CalculateValueFromFraction(1f, 0f, 100f));
            Assert.Equal(25f, RangeMath.CalculateValueFromFraction(0.25f, 0f, 100f));
        }

        [Fact]
        public void RangeMath_SnapToStep_RoundsToNearestDiscreteStep()
        {
            // Step = 5
            Assert.Equal(10f, RangeMath.SnapToStep(11f, 0f, 100f, 5f));
            Assert.Equal(15f, RangeMath.SnapToStep(13f, 0f, 100f, 5f));
            Assert.Equal(15f, RangeMath.SnapToStep(14.9f, 0f, 100f, 5f));

            // Clamping with Step
            Assert.Equal(100f, RangeMath.SnapToStep(105f, 0f, 100f, 5f));
            Assert.Equal(0f, RangeMath.SnapToStep(-5f, 0f, 100f, 5f));
        }

        [Fact]
        public void RangeModel_ValueAndFractionSynchronization()
        {
            var model = new RangeModel(0f, 200f, 0f, 1f);
            Assert.Equal(0f, model.Fraction);

            model.Value = 100f;
            Assert.Equal(0.5f, model.Fraction);

            model.Fraction = 0.25f;
            Assert.Equal(50f, model.Value);
        }

        [Fact]
        public void RangeModel_IncrementAndDecrement_RespectsStep()
        {
            var model = new RangeModel(0f, 100f, 10f, 5f);

            model.Increment();
            Assert.Equal(15f, model.Value);

            model.Increment(2f); // double step
            Assert.Equal(25f, model.Value);

            model.Decrement();
            Assert.Equal(20f, model.Value);
        }

        [Fact]
        public void RangeModel_Events_FireOnValueAndRangeChanges()
        {
            var model = new RangeModel(0f, 100f, 10f, 1f);
            int valueChanges = 0;
            int rangeChanges = 0;

            model.ValueChanged += (s, e) => valueChanges++;
            model.RangeChanged += (s, e) => rangeChanges++;

            model.Value = 20f;
            Assert.Equal(1, valueChanges);
            Assert.Equal(0, rangeChanges);

            model.SetRange(0f, 200f);
            Assert.Equal(1, rangeChanges);
        }
    }
}
