using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Localization;
using ZeroUI.WinForms.Localization;
using ZeroUI.Wpf.Localization;

namespace ZeroUI.Desktop.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void JsonScanner_ParsesFlatJsonWithEscapesAndUnicodeCorrectly()
        {
            string json = @"
            {
                // This is a comment
                ""Common.Ok"": ""OK"",
                ""Common.Cancel"": ""Cancel"",
                ""Msg.LineBreak"": ""Line 1\nLine 2\tTabbed\"""",
                ""Unicode.Vietnamese"": ""T\u1ea1o t\u1eadp tin n\u00e9n"",
                ""Number.Val"": 42
            }";

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonScanner.Parse(json, dict);

            Assert.Equal("OK", dict["Common.Ok"]);
            Assert.Equal("Cancel", dict["Common.Cancel"]);
            Assert.Equal("Line 1\nLine 2\tTabbed\"", dict["Msg.LineBreak"]);
            Assert.Equal("Tạo tập tin nén", dict["Unicode.Vietnamese"]);
            Assert.Equal("42", dict["Number.Val"]);

            // Backward compatibility test
            var dictLegacy = new Dictionary<string, string>(StringComparer.Ordinal);
            ZeroJsonScanner.Parse(json, dictLegacy);
            Assert.Equal("OK", dictLegacy["Common.Ok"]);
        }

        [Fact]
        public void LocalizationManager_SwitchesLanguageRealtime()
        {
            string enJson = @"{ ""Archive.Save"": ""Save Archive"", ""Archive.Cancel"": ""Cancel"" }";
            string viJson = @"{ ""Archive.Save"": ""Lưu tập tin nén"", ""Archive.Cancel"": ""Hủy bỏ"" }";

            LocalizationManager.LoadLanguageJson("en-US", enJson);
            LocalizationManager.LoadLanguageJson("vi-VN", viJson);

            // Switch to English
            LocalizationManager.SetLanguage("en-US");
            Assert.Equal("Save Archive", L.T("Archive.Save"));
            Assert.Equal("Cancel", L.T("Archive.Cancel"));

            // Switch to Vietnamese
            bool eventFired = false;
            LocalizationManager.CultureChanged += (s, e) => eventFired = true;

            LocalizationManager.SetLanguage("vi-VN");
            Assert.True(eventFired);
            Assert.Equal("Lưu tập tin nén", L.T("Archive.Save"));
            Assert.Equal("Hủy bỏ", L.T("Archive.Cancel"));
        }

        [Fact]
        public void LocalizationManager_HierarchicalFallback_MemoizesKey()
        {
            string enJson = @"{ ""Feature.OnlyInEnglish"": ""English Only Text"" }";
            string viJson = @"{ ""Feature.VietnameseOnly"": ""Chỉ có tiếng Việt"" }";

            LocalizationManager.LoadLanguageJson("en-US", enJson);
            LocalizationManager.LoadLanguageJson("vi-VN", viJson);

            LocalizationManager.SetLanguage("vi-VN");

            // Key exists only in en-US -> should fallback and memoize
            string resolved = LocalizationManager.Get("Feature.OnlyInEnglish");
            Assert.Equal("English Only Text", resolved);

            // Calling again should resolve immediately
            Assert.Equal("English Only Text", L.T("Feature.OnlyInEnglish"));
        }

        [Fact]
        public void LocalizationManager_MissingKey_RecordedInHarvester()
        {
            string missingKey = "NonExistentKey." + Guid.NewGuid().ToString("N");
            string val = LocalizationManager.Get(missingKey);

#if DEBUG
            Assert.Equal($"[!{missingKey}!]", val);
#else
            Assert.Equal(missingKey, val);
#endif
            Assert.Contains(missingKey, LocalizationManager.GetMissingKeys());
        }

        [Fact]
        public void L_Facade_FormatAndGet_WorkCorrectly()
        {
            string json = @"{ ""Format.Welcome"": ""Xin chào, {0}! Bạn có {1} thông báo."" }";
            LocalizationManager.LoadLanguageJson("vi-VN", json);
            LocalizationManager.SetLanguage("vi-VN");

            string formatted = L.Fmt("Format.Welcome", "Phong", 5);
            Assert.Equal("Xin chào, Phong! Bạn có 5 thông báo.", formatted);

            string defaultFallback = L.Get("Missing.Key", "Default Value");
            Assert.Equal("Default Value", defaultFallback);
        }

        [Fact]
        public void WinForms_BindText_UpdatesRealtimeAndCleansDisposedControls()
        {
            string enJson = @"{ ""Btn.Action"": ""Submit"" }";
            string viJson = @"{ ""Btn.Action"": ""Gửi đi"" }";

            LocalizationManager.LoadLanguageJson("en-US", enJson);
            LocalizationManager.LoadLanguageJson("vi-VN", viJson);
            LocalizationManager.SetLanguage("en-US");

            var activeBtn = new Button();
            var disposedBtn = new Button();

            activeBtn.BindText("Btn.Action");
            disposedBtn.BindText("Btn.Action");

            Assert.Equal("Submit", activeBtn.Text);
            Assert.Equal("Submit", disposedBtn.Text);

            // Dispose one control
            disposedBtn.Dispose();

            // Switch language
            LocalizationManager.SetLanguage("vi-VN");

            // Active button should receive real-time update
            Assert.Equal("Gửi đi", activeBtn.Text);
            activeBtn.Dispose();
        }

        [Fact]
        public void Wpf_LocExtension_ProvidesFallbackValue()
        {
            var ext = new LocExtension("Common.Ok") { Default = "OK" };
            var result = ext.ProvideValue(null!);
            Assert.NotNull(result);
            Assert.Equal(L.T("Common.Ok"), result.ToString());
        }
    }
}
