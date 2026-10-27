using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Overlays;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Desktop.Tests
{
    [Collection("WpfThemeTests")]
    public class ZWpfLoginDialogChallengerTests
    {
        private static void ResetThemeSubscribers()
        {
            var field = typeof(ZeroWpfTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            field?.SetValue(null, null);
        }

        private static double GetContrast(Color fg, Color bg)
        {
            string hexFg = $"#{fg.R:X2}{fg.G:X2}{fg.B:X2}";
            string hexBg = $"#{bg.R:X2}{bg.G:X2}{bg.B:X2}";
            return ZeroColorUtils.GetContrastRatio(hexFg, hexBg);
        }

        private static double GetContrast(SolidColorBrush fg, SolidColorBrush bg)
        {
            return GetContrast(fg.Color, bg.Color);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(instance)!;
        }

        #region Attack Surface 1: Close Button, Cancel, ESC, and DialogResult Semantics

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
        public void CloseButton_Click_Unshown_DoesNotThrowInvalidOperationException()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();

                // Directly raise click on CloseButton without showing dialog
                var ex = Record.Exception(() =>
                {
                    dlg.CloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });

                Assert.Null(ex);
            });
        }

        [Fact]
        public void CloseButton_Click_NonModal_ClosesWithoutException()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                dlg.Show();
                Assert.True(dlg.IsVisible);

                dlg.CloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.False(dlg.IsVisible);
            });
        }

        [Fact]
        public void CloseButton_Click_ModalDialog_SetsDialogResultFalseAndCloses()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                dlg.Loaded += (s, e) =>
                {
                    dlg.CloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };

                bool? result = dlg.ShowDialog();

                Assert.False(result);
                Assert.False(dlg.IsVisible);
            });
        }

        [Fact]
        public void CancelButton_Click_ModalDialog_SetsDialogResultFalseAndCloses()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                var btnCancel = GetPrivateField<Button>(dlg, "_btnCancel");
                Assert.NotNull(btnCancel);

                dlg.Loaded += (s, e) =>
                {
                    btnCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };

                bool? result = dlg.ShowDialog();

                Assert.False(result);
                Assert.False(dlg.IsVisible);
            });
        }

        [Fact]
        public void EscapeKey_ModalDialog_SetsDialogResultFalseAndCloses()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                dlg.Loaded += (s, e) =>
                {
                    var keyEventArgs = new KeyEventArgs(
                        Keyboard.PrimaryDevice,
                        PresentationSource.FromVisual(dlg)!,
                        0,
                        Key.Escape)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent
                    };
                    dlg.RaiseEvent(keyEventArgs);
                };

                bool? result = dlg.ShowDialog();

                Assert.False(result);
                Assert.False(dlg.IsVisible);
            });
        }

        [Fact]
        public void EscapeKey_NonModal_ClosesWithoutException()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZLoginDialog();
                dlg.Show();
                Assert.True(dlg.IsVisible);

                var keyEventArgs = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(dlg)!,
                    0,
                    Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                dlg.RaiseEvent(keyEventArgs);

                Assert.False(dlg.IsVisible);
            });
        }

        #endregion

        #region Attack Surface 2: Dynamic Theme Switching

        [Fact]
        public void DynamicTheming_SwitchingBetweenLightAndDark_UpdatesAllComponents()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();

                // Start in Clean Light
                ZeroWpfTheme.SetTheme(false);
                Assert.False(ZeroWpfTheme.IsDark);

                using var dlg = new ZLoginDialog();

                var rootBorder = GetPrivateField<Border>(dlg, "_rootBorder");
                var lblTitle = GetPrivateField<TextBlock>(dlg, "_lblTitle");
                var lblSubtitle = GetPrivateField<TextBlock>(dlg, "_lblSubtitle");
                var txtUser = GetPrivateField<TextBox>(dlg, "_txtUsername");
                var txtPass = GetPrivateField<PasswordBox>(dlg, "_txtPassword");
                var txtPin = GetPrivateField<PasswordBox>(dlg, "_txtPin");
                var btnSignIn = GetPrivateField<Button>(dlg, "_btnSignIn");
                var btnCancel = GetPrivateField<Button>(dlg, "_btnCancel");
                var tabPass = GetPrivateField<Button>(dlg, "_tabPass");
                var tabPin = GetPrivateField<Button>(dlg, "_tabPin");
                var tabBadge = GetPrivateField<Button>(dlg, "_tabBadge");
                var lblStatus = GetPrivateField<TextBlock>(dlg, "_lblStatus");

                // Verify Light Theme applied
                Assert.Equal(ZeroWpfTheme.BgPrimary.Color, ((SolidColorBrush)dlg.Background).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)rootBorder.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)lblTitle.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)lblSubtitle.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)txtUser.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)txtUser.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)txtUser.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)txtPass.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)txtPass.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)txtPass.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)btnSignIn.Background).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)btnCancel.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)btnCancel.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)dlg.CloseButton.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)tabPass.Background).Color); // active
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)tabPin.Background).Color); // inactive
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)lblStatus.Foreground).Color);

                // Switch to Obsidian Dark dynamically
                ZeroWpfTheme.SetTheme(true);
                Assert.True(ZeroWpfTheme.IsDark);

                // Verify Dark Theme applied in-place
                Assert.Equal(ZeroWpfTheme.BgPrimary.Color, ((SolidColorBrush)dlg.Background).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)rootBorder.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)lblTitle.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)lblSubtitle.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)txtUser.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)txtUser.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)txtUser.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)txtPass.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)txtPass.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.BorderDefault.Color, ((SolidColorBrush)txtPass.BorderBrush).Color);
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)btnSignIn.Background).Color);
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)btnCancel.Background).Color);
                Assert.Equal(ZeroWpfTheme.TextPrimary.Color, ((SolidColorBrush)btnCancel.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)dlg.CloseButton.Foreground).Color);
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)tabPass.Background).Color); // active
                Assert.Equal(ZeroWpfTheme.BgInput.Color, ((SolidColorBrush)tabPin.Background).Color); // inactive
                Assert.Equal(ZeroWpfTheme.TextSecondary.Color, ((SolidColorBrush)lblStatus.Foreground).Color);

                // Switch back to Clean Light
                ZeroWpfTheme.SetTheme(false);
                Assert.Equal(ZeroWpfTheme.BgPrimary.Color, ((SolidColorBrush)dlg.Background).Color);
            });
        }

        [Fact]
        public void DynamicTheming_PreservesStatusState_AcrossThemeSwitches()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();
                ZeroWpfTheme.SetTheme(false);
                using var dlg = new ZLoginDialog();
                var lblStatus = GetPrivateField<TextBlock>(dlg, "_lblStatus");

                var setStatusMethod = typeof(ZLoginDialog).GetMethod("SetStatus", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(setStatusMethod);
                setStatusMethod!.Invoke(dlg, new object[] { "Invalid credentials", ZLoginDialog.StatusState.Error });

                Assert.Equal(ZLoginDialog.StatusState.Error, dlg.CurrentStatusState);
                Assert.Equal(ZeroWpfTheme.DangerAccent.Color, ((SolidColorBrush)lblStatus.Foreground).Color);

                // Switch theme to Dark
                ZeroWpfTheme.SetTheme(true);

                // Status should still reflect DangerAccent (new dark DangerAccent)
                Assert.Equal(ZLoginDialog.StatusState.Error, dlg.CurrentStatusState);
                Assert.Equal(ZeroWpfTheme.DangerAccent.Color, ((SolidColorBrush)lblStatus.Foreground).Color);

                // Set status to Authenticating
                setStatusMethod!.Invoke(dlg, new object[] { "Authenticating...", ZLoginDialog.StatusState.Authenticating });
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)lblStatus.Foreground).Color);

                // Switch theme back to Light
                ZeroWpfTheme.SetTheme(false);
                Assert.Equal(ZeroWpfTheme.PrimaryAccent.Color, ((SolidColorBrush)lblStatus.Foreground).Color);
            });
        }

        #endregion

        #region Attack Surface 3: Dual-Skin Contrast Ratio & Text Washout Audit

        [Fact]
        public void DualSkin_ContrastRatio_SubmitButtonAndActiveTab_MustMeetWcagThreshold()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();

                // Test Clean Light
                ZeroWpfTheme.SetTheme(false);
                using (var dlg = new ZLoginDialog())
                {
                    var btnSignIn = GetPrivateField<Button>(dlg, "_btnSignIn");
                    var tabPass = GetPrivateField<Button>(dlg, "_tabPass");

                    var fgBrush = (SolidColorBrush)btnSignIn.Foreground;
                    var bgBrush = (SolidColorBrush)btnSignIn.Background;
                    double lightBtnContrast = GetContrast(fgBrush, bgBrush);

                    var tabFgBrush = (SolidColorBrush)tabPass.Foreground;
                    var tabBgBrush = (SolidColorBrush)tabPass.Background;
                    double lightTabContrast = GetContrast(tabFgBrush, tabBgBrush);

                    Assert.True(lightBtnContrast >= 4.5,
                        $"CleanLight submit button contrast was {lightBtnContrast:F2} (< 4.5:1). FG: #{fgBrush.Color.R:X2}{fgBrush.Color.G:X2}{fgBrush.Color.B:X2}, BG: #{bgBrush.Color.R:X2}{bgBrush.Color.G:X2}{bgBrush.Color.B:X2}");
                    Assert.True(lightTabContrast >= 4.5,
                        $"CleanLight active tab contrast was {lightTabContrast:F2} (< 4.5:1). FG: #{tabFgBrush.Color.R:X2}{tabFgBrush.Color.G:X2}{tabFgBrush.Color.B:X2}, BG: #{tabBgBrush.Color.R:X2}{tabBgBrush.Color.G:X2}{tabBgBrush.Color.B:X2}");
                }

                // Test Obsidian Dark
                ZeroWpfTheme.SetTheme(true);
                using (var dlg = new ZLoginDialog())
                {
                    var btnSignIn = GetPrivateField<Button>(dlg, "_btnSignIn");
                    var tabPass = GetPrivateField<Button>(dlg, "_tabPass");

                    var fgBrush = (SolidColorBrush)btnSignIn.Foreground;
                    var bgBrush = (SolidColorBrush)btnSignIn.Background;
                    double darkBtnContrast = GetContrast(fgBrush, bgBrush);

                    var tabFgBrush = (SolidColorBrush)tabPass.Foreground;
                    var tabBgBrush = (SolidColorBrush)tabPass.Background;
                    double darkTabContrast = GetContrast(tabFgBrush, tabBgBrush);

                    Assert.True(darkBtnContrast >= 4.5,
                        $"ObsidianDark submit button contrast was {darkBtnContrast:F2} (< 4.5:1). FG: #{fgBrush.Color.R:X2}{fgBrush.Color.G:X2}{fgBrush.Color.B:X2}, BG: #{bgBrush.Color.R:X2}{bgBrush.Color.G:X2}{bgBrush.Color.B:X2}");
                    Assert.True(darkTabContrast >= 4.5,
                        $"ObsidianDark active tab contrast was {darkTabContrast:F2} (< 4.5:1). FG: #{tabFgBrush.Color.R:X2}{tabFgBrush.Color.G:X2}{tabFgBrush.Color.B:X2}, BG: #{tabBgBrush.Color.R:X2}{tabBgBrush.Color.G:X2}{tabBgBrush.Color.B:X2}");
                }
            });
        }

        [Fact]
        public void DualSkin_ContrastRatio_AllElements_MeetWcagThreshold()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();

                foreach (bool isDark in new[] { false, true })
                {
                    ZeroWpfTheme.SetTheme(isDark);
                    string skinName = isDark ? "ObsidianDark" : "CleanLight";

                    using var dlg = new ZLoginDialog();
                    var dialogBg = ((SolidColorBrush)dlg.Background).Color;

                    var lblTitle = GetPrivateField<TextBlock>(dlg, "_lblTitle");
                    var lblSubtitle = GetPrivateField<TextBlock>(dlg, "_lblSubtitle");
                    var txtUser = GetPrivateField<TextBox>(dlg, "_txtUsername");
                    var btnCancel = GetPrivateField<Button>(dlg, "_btnCancel");
                    var closeBtn = dlg.CloseButton;

                    double titleContrast = GetContrast(((SolidColorBrush)lblTitle.Foreground).Color, dialogBg);
                    double subContrast = GetContrast(((SolidColorBrush)lblSubtitle.Foreground).Color, dialogBg);
                    double txtContrast = GetContrast(((SolidColorBrush)txtUser.Foreground).Color, ((SolidColorBrush)txtUser.Background).Color);
                    double closeContrast = GetContrast(((SolidColorBrush)closeBtn.Foreground).Color, dialogBg);
                    double cancelContrast = GetContrast(((SolidColorBrush)btnCancel.Foreground).Color, ((SolidColorBrush)btnCancel.Background).Color);

                    Assert.True(titleContrast >= 4.5, $"Title contrast was {titleContrast:F2} in {skinName}");
                    Assert.True(subContrast >= 4.5, $"Subtitle contrast was {subContrast:F2} in {skinName}");
                    Assert.True(txtContrast >= 4.5, $"TextBox contrast was {txtContrast:F2} in {skinName}");
                    Assert.True(closeContrast >= 4.5, $"CloseButton contrast was {closeContrast:F2} in {skinName}");
                    Assert.True(cancelContrast >= 4.5, $"CancelButton contrast was {cancelContrast:F2} in {skinName}");
                }
            });
        }

        #endregion

        #region Attack Surface 4: Disposal & Memory Leak Prevention

        [Fact]
        public void Dispose_UnsubscribesFromThemeChanged_PreventsExceptions()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();
                var dlg = new ZLoginDialog();
                dlg.Dispose();

                var ex = Record.Exception(() =>
                {
                    ZeroWpfTheme.SetTheme(true);
                    ZeroWpfTheme.SetTheme(false);
                });

                Assert.Null(ex);
            });
        }

        [Fact]
        public void ClosedEvent_UnsubscribesFromThemeChanged()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();
                var themeChangedField = typeof(ZeroWpfTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var initialDelegate = themeChangedField?.GetValue(null) as Action;
                int initialCount = initialDelegate?.GetInvocationList().Length ?? 0;

                var dlg = new ZLoginDialog();
                var afterSubDelegate = themeChangedField?.GetValue(null) as Action;
                int afterSubCount = afterSubDelegate?.GetInvocationList().Length ?? 0;
                Assert.Equal(initialCount + 1, afterSubCount);

                dlg.Close();

                var afterCloseDelegate = themeChangedField?.GetValue(null) as Action;
                int afterCloseCount = afterCloseDelegate?.GetInvocationList().Length ?? 0;
                Assert.Equal(initialCount, afterCloseCount);
            });
        }

        [Fact]
        public void StressTest_100Instances_RapidThemeSwitching()
        {
            StaTestRunner.Run(() =>
            {
                ResetThemeSubscribers();
                for (int i = 0; i < 50; i++)
                {
                    using (var dlg = new ZLoginDialog())
                    {
                        ZeroWpfTheme.SetTheme(i % 2 == 0);
                        Assert.Equal(ZeroWpfTheme.BgPrimary.Color, ((SolidColorBrush)dlg.Background).Color);
                    }
                }
            });
        }

        #endregion

        #region Attack Surface 5: Static Code Guard

        [Fact]
        public void StaticColorGuard_ZLoginDialog_ZeroProhibitedLiterals()
        {
            string sourcePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../src/Wpf/ZeroUI.Wpf.Common/Overlays/ZLoginDialog.cs"));

            Assert.True(System.IO.File.Exists(sourcePath), $"Source file not found at: {sourcePath}");

            string[] lines = System.IO.File.ReadAllLines(sourcePath);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                    continue;

                // Check for hardcoded FromRgb / FromArgb
                Assert.DoesNotContain("FromRgb", trimmed);
                Assert.DoesNotContain("FromArgb", trimmed);

                // Check for hardcoded hex colors
                Assert.False(
                    System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"#[0-9a-fA-F]{6}"),
                    $"Hardcoded hex color found in line: {line}");
            }
        }

        #endregion
    }
}
