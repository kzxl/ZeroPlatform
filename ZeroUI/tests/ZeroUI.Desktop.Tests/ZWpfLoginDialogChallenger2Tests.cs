using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Overlays;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Desktop.Tests
{
    [Collection("WpfThemeTests")]
    public class ZWpfLoginDialogChallenger2Tests
    {
        private readonly ITestOutputHelper _output;

        public ZWpfLoginDialogChallenger2Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static string BrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                return ColorToHex(scb.Color);
            }
            return "#000000";
        }

        private static double GetContrast(Brush fgBrush, Brush bgBrush)
        {
            string hexFg = BrushToHex(fgBrush);
            string hexBg = BrushToHex(bgBrush);
            return ZeroColorUtils.GetContrastRatio(hexFg, hexBg);
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(instance)!;
        }

        #region 1. Static Guard: Prohibited Literals Check

        [Fact]
        public void StaticGuard_ZLoginDialog_ZeroProhibitedLiterals()
        {
            string baseDir = AppContext.BaseDirectory;
            string? repoRoot = baseDir;
            while (repoRoot != null && !File.Exists(Path.Combine(repoRoot, "ZeroUI.slnx")))
            {
                repoRoot = Directory.GetParent(repoRoot)?.FullName;
            }
            Assert.NotNull(repoRoot);
            string sourcePath = Path.Combine(repoRoot!, "src", "Wpf", "ZeroUI.Wpf.Common", "Overlays", "ZLoginDialog.cs");
            Assert.True(File.Exists(sourcePath), $"File not found at {sourcePath}");

            string content = File.ReadAllText(sourcePath);

            // 0 FromArgb
            var fromArgbMatches = Regex.Matches(content, @"\bFromArgb\b");
            Assert.True(fromArgbMatches.Count == 0, $"Expected 0 FromArgb, but found {fromArgbMatches.Count}");

            // 0 FromRgb
            var fromRgbMatches = Regex.Matches(content, @"\bFromRgb\b");
            Assert.True(fromRgbMatches.Count == 0, $"Expected 0 FromRgb, but found {fromRgbMatches.Count}");

            // 0 Hex color string literals (e.g., "#123456" or "#FFF")
            var hexColorMatches = Regex.Matches(content, @"""#[0-9a-fA-F]{3,8}""");
            Assert.True(hexColorMatches.Count == 0, $"Expected 0 hex literals, but found {hexColorMatches.Count}: {string.Join(", ", hexColorMatches.Cast<Match>().Select(m => m.Value))}");
        }

        #endregion

        #region 2. Close Button Contract & Dismissal Semantics

        [Fact]
        public void CloseButton_PropertiesAndLayout_ConformToContract()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();

                Assert.NotNull(dlg.CloseButton);
                Assert.Equal("✕", dlg.CloseButton.Content);
                Assert.Equal(32, dlg.CloseButton.Width);
                Assert.Equal(32, dlg.CloseButton.Height);
                Assert.True(dlg.CloseButton.IsCancel);
                Assert.Equal(Cursors.Hand, dlg.CloseButton.Cursor);
                Assert.Equal(new Thickness(0), dlg.CloseButton.BorderThickness);
            });
        }

        [Fact]
        public void CloseButton_Click_InModalFlow_SafelyCloses()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                bool closedFired = false;
                dlg.Closed += (s, e) => closedFired = true;

                // Simulate clicking close button programmatically
                var clickMethod = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                clickMethod?.Invoke(dlg.CloseButton, Array.Empty<object>());

                Assert.True(closedFired);
            });
        }

        [Fact]
        public void EscapeKey_PreviewKeyDown_SafelyCloses()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                bool closedFired = false;
                dlg.Closed += (s, e) => closedFired = true;

                // Raise PreviewKeyDown with Escape
                var eventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, new MockPresentationSource(), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                dlg.RaiseEvent(eventArgs);

                Assert.True(closedFired);
            });
        }

        private class MockPresentationSource : PresentationSource
        {
            protected override CompositionTarget? GetCompositionTargetCore() => null;
            public override Visual RootVisual { get; set; } = null!;
            public override bool IsDisposed => false;
        }

        #endregion

        #region 3. Lifecycle, Theme Switching Stress & Memory Leak Verification

        [Fact]
        public void Lifecycle_DisposeAndClosed_UnsubscribesFromThemeChanged()
        {
            StaTestRunner.Run(() =>
            {
                var eventField = typeof(ZeroWpfTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
                var initialDelegate = eventField?.GetValue(null) as Action;
                int initialCount = initialDelegate?.GetInvocationList().Length ?? 0;

                // Create instance
                var dlg = new ZLoginDialog();
                var midDelegate = eventField?.GetValue(null) as Action;
                int midCount = midDelegate?.GetInvocationList().Length ?? 0;
                Assert.Equal(initialCount + 1, midCount);

                // Dispose instance
                dlg.Dispose();
                var postDelegate = eventField?.GetValue(null) as Action;
                int postCount = postDelegate?.GetInvocationList().Length ?? 0;
                Assert.Equal(initialCount, postCount);
            });
        }

        [Fact]
        public void StressTest_100Instances_RapidSwitching_ZeroDanglingHandlers()
        {
            StaTestRunner.Run(() =>
            {
                var eventField = typeof(ZeroWpfTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
                var initialDelegate = eventField?.GetValue(null) as Action;
                int initialCount = initialDelegate?.GetInvocationList().Length ?? 0;

                for (int i = 0; i < 100; i++)
                {
                    using (var dlg = new ZLoginDialog())
                    {
                        ZeroWpfTheme.SetTheme(i % 2 == 0);
                    }
                }

                var finalDelegate = eventField?.GetValue(null) as Action;
                int finalCount = finalDelegate?.GetInvocationList().Length ?? 0;
                Assert.Equal(initialCount, finalCount);
            });
        }

        [Fact]
        public void StressTest_OpenDialog_50RapidThemeSwitches_UpdatesColorsFlawlessly()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                var btnSignIn = GetField<Button>(dlg, "_btnSignIn");
                var txtUsername = GetField<TextBox>(dlg, "_txtUsername");

                for (int i = 0; i < 50; i++)
                {
                    bool isDark = (i % 2 == 0);
                    ZeroWpfTheme.SetTheme(isDark);

                    // Ensure properties synchronized immediately
                    Assert.Equal(ZeroWpfTheme.BgPrimary, dlg.Background);
                    Assert.Equal(ZeroWpfTheme.BgInput, txtUsername.Background);
                    Assert.Equal(ZeroWpfTheme.PrimaryAccent, btnSignIn.Background);
                }
            });
        }

        #endregion

        #region 4. Adversarial Contrast Audit Across All 9 Enterprise Skins

        [Fact]
        public void ContrastAudit_All9EnterpriseSkins_ContrastRatiosReported()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();

                var lblTitle = GetField<TextBlock>(dlg, "_lblTitle");
                var lblSubtitle = GetField<TextBlock>(dlg, "_lblSubtitle");
                var btnClose = dlg.CloseButton;
                var tabPass = GetField<Button>(dlg, "_tabPass");
                var tabPin = GetField<Button>(dlg, "_tabPin");
                var lblUsername = GetField<TextBlock>(dlg, "_lblUsername");
                var txtUsername = GetField<TextBox>(dlg, "_txtUsername");
                var btnSignIn = GetField<Button>(dlg, "_btnSignIn");
                var btnCancel = GetField<Button>(dlg, "_btnCancel");
                var lblStatus = GetField<TextBlock>(dlg, "_lblStatus");
                var lblBadgePrompt = GetField<TextBlock>(dlg, "_lblBadgePrompt");

                var skins = ZeroSkinDefaults.GetAllDefaults().ToList();
                Assert.Equal(9, skins.Count);

                int failureCount = 0;

                foreach (var skin in skins)
                {
                    ZeroWpfTheme.ApplyPalette(skin.Tokens, skin.IsDark);
                    dlg.ApplyTheme();

                    _output.WriteLine($"==================================================");
                    _output.WriteLine($"SKIN: {skin.DisplayName} ({skin.Name}) - Dark: {skin.IsDark}");
                    _output.WriteLine($"==================================================");

                    void Check(string element, Brush fg, Brush bg, double minRequired = 4.5)
                    {
                        double ratio = GetContrast(fg, bg);
                        bool pass = ratio >= minRequired;
                        string status = pass ? "PASS" : "FAIL";
                        _output.WriteLine($"[{status}] {element}: {BrushToHex(fg)} on {BrushToHex(bg)} -> {ratio:F2}:1 (min: {minRequired}:1)");
                        if (!pass)
                        {
                            failureCount++;
                        }
                    }

                    // 1. Title
                    Check("Title (_lblTitle)", lblTitle.Foreground, dlg.Background);

                    // 2. Subtitle
                    Check("Subtitle (_lblSubtitle)", lblSubtitle.Foreground, dlg.Background);

                    // 3. Close Button Normal
                    Check("Close Button Normal", btnClose.Foreground, dlg.Background);

                    // 4. Close Button Hover
                    Check("Close Button Hover", ZeroWpfTheme.TextPrimary, ZeroWpfTheme.BgHover);

                    // 5. Inactive Tab
                    Check("Inactive Tab (_tabPin)", tabPin.Foreground, tabPin.Background);

                    // 6. Active Tab
                    Check("Active Tab (_tabPass)", tabPass.Foreground, tabPass.Background);

                    // 7. Labels
                    Check("Username Label", lblUsername.Foreground, dlg.Background);

                    // 8. Text Input
                    Check("Text Input (_txtUsername)", txtUsername.Foreground, txtUsername.Background);

                    // 9. Action Button (Sign In)
                    Check("Sign In Button", btnSignIn.Foreground, btnSignIn.Background);

                    // 10. Action Button (Cancel)
                    Check("Cancel Button", btnCancel.Foreground, btnCancel.Background);

                    // 11. Status Ready
                    Check("Status Ready", lblStatus.Foreground, dlg.Background);

                    // 12. Status Authenticating
                    Check("Status Authenticating", ZeroWpfTheme.PrimaryAccent, dlg.Background);

                    // 13. Status Error
                    Check("Status Error", ZeroWpfTheme.DangerAccent, dlg.Background);

                    // 14. Badge Prompt
                    Check("Badge Prompt", lblBadgePrompt.Foreground, dlg.Background);
                }

                _output.WriteLine($"\nTotal Contrast Failures across all 9 skins: {failureCount}");
            });
        }

        [Theory]
        [InlineData("obsidian_dark")]
        [InlineData("clean_light")]
        [InlineData("nordic_frost")]
        [InlineData("cyberpunk_neon")]
        [InlineData("emerald_enterprise")]
        [InlineData("solar_amber")]
        [InlineData("amethyst_violet")]
        [InlineData("crimson_ruby")]
        [InlineData("oled_midnight")]
        public void ContrastAudit_EnterpriseSkin_MustSatisfyWcagAa(string skinName)
        {
            StaTestRunner.Run(() =>
            {
                var skin = ZeroSkinDefaults.GetAllDefaults().FirstOrDefault(s => s.Name == skinName);
                Assert.NotNull(skin);

                ZeroWpfTheme.ApplyPalette(skin!.Tokens, skin.IsDark);
                using var dlg = new ZLoginDialog();
                dlg.ApplyTheme();

                var lblTitle = GetField<TextBlock>(dlg, "_lblTitle");
                var lblSubtitle = GetField<TextBlock>(dlg, "_lblSubtitle");
                var btnClose = dlg.CloseButton;
                var tabPass = GetField<Button>(dlg, "_tabPass");
                var tabPin = GetField<Button>(dlg, "_tabPin");
                var lblUsername = GetField<TextBlock>(dlg, "_lblUsername");
                var txtUsername = GetField<TextBox>(dlg, "_txtUsername");
                var btnSignIn = GetField<Button>(dlg, "_btnSignIn");
                var btnCancel = GetField<Button>(dlg, "_btnCancel");
                var lblStatus = GetField<TextBlock>(dlg, "_lblStatus");
                var lblBadgePrompt = GetField<TextBlock>(dlg, "_lblBadgePrompt");

                double titleContrast = GetContrast(lblTitle.Foreground, dlg.Background);
                double subContrast = GetContrast(lblSubtitle.Foreground, dlg.Background);
                double closeBtnContrast = GetContrast(btnClose.Foreground, dlg.Background);
                double closeBtnHoverContrast = GetContrast(ZeroWpfTheme.TextPrimary, ZeroWpfTheme.BgHover);
                double inactiveTabContrast = GetContrast(tabPin.Foreground, tabPin.Background);
                double activeTabContrast = GetContrast(tabPass.Foreground, tabPass.Background);
                double labelContrast = GetContrast(lblUsername.Foreground, dlg.Background);
                double inputContrast = GetContrast(txtUsername.Foreground, txtUsername.Background);
                double signInContrast = GetContrast(btnSignIn.Foreground, btnSignIn.Background);
                double cancelContrast = GetContrast(btnCancel.Foreground, btnCancel.Background);
                double statusReadyContrast = GetContrast(lblStatus.Foreground, dlg.Background);
                double statusAuthContrast = GetContrast(ZeroWpfTheme.PrimaryAccent, dlg.Background);
                double statusErrContrast = GetContrast(ZeroWpfTheme.DangerAccent, dlg.Background);
                double badgePromptContrast = GetContrast(lblBadgePrompt.Foreground, dlg.Background);

                Assert.True(titleContrast >= 4.5, $"Title contrast {titleContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(subContrast >= 4.5, $"Subtitle contrast {subContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(closeBtnContrast >= 4.5, $"CloseButton contrast {closeBtnContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(closeBtnHoverContrast >= 4.5, $"CloseButton Hover contrast {closeBtnHoverContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(inactiveTabContrast >= 4.5, $"InactiveTab contrast {inactiveTabContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(labelContrast >= 4.5, $"Label contrast {labelContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(inputContrast >= 4.5, $"Input contrast {inputContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(cancelContrast >= 4.5, $"CancelButton contrast {cancelContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(statusReadyContrast >= 4.5, $"StatusReady contrast {statusReadyContrast:F2}:1 < 4.5 in skin {skinName}");
                Assert.True(statusErrContrast >= 4.5, $"StatusError contrast {statusErrContrast:F2}:1 < 4.5 in skin {skinName}");

                // Critical failure assertions:
                Assert.True(signInContrast >= 4.5, $"SignIn button contrast {signInContrast:F2}:1 < 4.5 in skin {skinName} (Fg: {BrushToHex(btnSignIn.Foreground)}, Bg: {BrushToHex(btnSignIn.Background)})");
                Assert.True(activeTabContrast >= 4.5, $"ActiveTab contrast {activeTabContrast:F2}:1 < 4.5 in skin {skinName} (Fg: {BrushToHex(tabPass.Foreground)}, Bg: {BrushToHex(tabPass.Background)})");
                Assert.True(statusAuthContrast >= 4.5, $"Status Authenticating contrast {statusAuthContrast:F2}:1 < 4.5 in skin {skinName} (Fg: {BrushToHex(ZeroWpfTheme.PrimaryAccent)}, Bg: {BrushToHex(dlg.Background)})");
                Assert.True(badgePromptContrast >= 4.5, $"BadgePrompt contrast {badgePromptContrast:F2}:1 < 4.5 in skin {skinName} (Fg: {BrushToHex(lblBadgePrompt.Foreground)}, Bg: {BrushToHex(dlg.Background)})");
            });
        }

        #endregion
    }
}
