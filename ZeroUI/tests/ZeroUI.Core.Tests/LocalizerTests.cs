using System;
using System.Globalization;
using Xunit;
using ZeroUI.Core.Data;
using ZeroUI.Core.Localization;

namespace ZeroUI.Core.Tests
{
    public class LocalizerTests : IDisposable
    {
        public LocalizerTests()
        {
            // Reset to English before each test
            Localizer.ResetOverrides();
            Localizer.SetLanguage("en");
        }

        public void Dispose()
        {
            // Ensure clean state after tests
            Localizer.ResetOverrides();
            Localizer.SetLanguage("en");
        }

        [Fact]
        public void DefaultLanguage_IsEnglish()
        {
            Assert.Equal("OK", Localizer.GetString(StringId.Ok));
            Assert.Equal("Cancel", Localizer.GetString(StringId.Cancel));
            Assert.Equal("Select items...", Localizer.GetString(StringId.CheckedComboPlaceholder));
            Assert.Equal("This field is required.", Localizer.GetString(StringId.ValRequired));

            // Legacy backward-compatibility test
            Assert.Equal("OK", ZeroLocalizer.GetString(ZeroStringId.Ok));
            Assert.Equal("Cancel", ZeroLocalizer.GetString(ZeroStringId.Cancel));
        }

        [Fact]
        public void SwitchLanguage_ToVietnamese_ReturnsCorrectTranslations()
        {
            Localizer.SetLanguage("vi");

            Assert.Equal("Đồng ý", Localizer.GetString(StringId.Ok));
            Assert.Equal("Hủy", Localizer.GetString(StringId.Cancel));
            Assert.Equal("Chọn mục...", Localizer.GetString(StringId.CheckedComboPlaceholder));
            Assert.Equal("VÀ", Localizer.GetString(StringId.FilterOpAnd));
            Assert.Equal("HOẶC", Localizer.GetString(StringId.FilterOpOr));
            Assert.Equal("Hoàn tất ✓", Localizer.GetString(StringId.WizardFinish));
            Assert.Equal("Trường này là bắt buộc.", Localizer.GetString(StringId.ValRequired));

            // Legacy backward-compatibility test
            Assert.Equal("Đồng ý", ZeroLocalizer.GetString(ZeroStringId.Ok));
        }

        [Fact]
        public void GetFormattedString_FormatsArgumentsProperly()
        {
            Localizer.SetLanguage("en");
            string pageEn = Localizer.GetFormattedString(StringId.PrintStatusFormat, 2, 5);
            Assert.Equal("Page 2 of 5", pageEn);

            Localizer.SetLanguage("vi");
            string summaryVi = Localizer.GetFormattedString(StringId.CheckedComboSummaryFormat, 4);
            Assert.Equal("Đã chọn 4 mục", summaryVi);

            // Legacy backward-compatibility test
            string summaryViLegacy = ZeroLocalizer.GetFormattedString(ZeroStringId.CheckedComboSummaryFormat, 4);
            Assert.Equal("Đã chọn 4 mục", summaryViLegacy);
        }

        [Fact]
        public void Override_CustomString_OverridesTranslationAndRaisesEvent()
        {
            bool eventFired = false;
            EventHandler handler = (s, e) => eventFired = true;
            Localizer.CultureChanged += handler;

            try
            {
                Localizer.Override(StringId.Ok, "Custom Confirm");
                Assert.True(eventFired);
                Assert.Equal("Custom Confirm", Localizer.GetString(StringId.Ok));

                // Legacy backward-compatibility test
                Assert.Equal("Custom Confirm", ZeroLocalizer.GetString(ZeroStringId.Ok));
            }
            finally
            {
                Localizer.CultureChanged -= handler;
            }
        }

        [Fact]
        public void FilterCriteriaExtensions_ProvideLocalizedStrings()
        {
            Localizer.SetLanguage("vi");

            Assert.Equal("VÀ", FilterGroupOperator.And.GetLocalizedName());
            Assert.Equal("HOẶC", FilterGroupOperator.Or.GetLocalizedName());
            Assert.Equal("KHÔNG VÀ", FilterGroupOperator.NotAnd.GetLocalizedName());
            Assert.Equal("Bằng", FilterComparisonOperator.Equals.GetLocalizedName());
            Assert.Equal("Chứa", FilterComparisonOperator.Contains.GetLocalizedName());
            Assert.Equal("Khác", FilterComparisonOperator.NotEquals.GetLocalizedName());

            Localizer.SetLanguage("en");
            Assert.Equal("AND", FilterGroupOperator.And.GetLocalizedName());
            Assert.Equal("OR", FilterGroupOperator.Or.GetLocalizedName());
            Assert.Equal("Equals", FilterComparisonOperator.Equals.GetLocalizedName());
        }

        [Fact]
        public void Fallback_ForUnsupportedLanguage_DefaultsToEnglish()
        {
            Localizer.SetLanguage(new CultureInfo("fr-FR"));
            Assert.Equal("Cancel", Localizer.GetString(StringId.Cancel));
            Assert.Equal("OK", Localizer.GetString(StringId.Ok));
        }
    }
}
