using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.Desktop.Tests
{
    [Collection("WinFormsThemeTests")]
    public class ZLoginDialogChallengerTests
    {
        private static double GetContrast(Color fg, Color bg)
        {
            string hexFg = $"#{fg.R:X2}{fg.G:X2}{fg.B:X2}";
            string hexBg = $"#{bg.R:X2}{bg.G:X2}{bg.B:X2}";
            return ZeroColorUtils.GetContrastRatio(hexFg, hexBg);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(instance)!;
        }

        #region Attack Surface 1: Close Button & Cancel / ESC Semantics

        [Fact]
        public void CloseButton_PropertiesAndLayout_ConformToContract()
        {
            using var dlg = new ZLoginDialog();

            Assert.NotNull(dlg.CloseButton);
            Assert.Equal("✕", dlg.CloseButton.Text);
            Assert.Equal(new Size(30, 30), dlg.CloseButton.Size);
            Assert.Equal(new Point(dlg.Width - 42, 12), dlg.CloseButton.Location);
            Assert.Equal(AnchorStyles.Top | AnchorStyles.Right, dlg.CloseButton.Anchor);
            Assert.Equal(FlatStyle.Flat, dlg.CloseButton.FlatStyle);
            Assert.Equal(0, dlg.CloseButton.FlatAppearance.BorderSize);
            Assert.False(dlg.CloseButton.TabStop);
            Assert.True(dlg.Controls.Contains(dlg.CloseButton));
            Assert.Same(dlg.CloseButton, dlg.CancelButton);
        }

        [Fact]
        public void CloseButton_Click_WhenShown_SetsDialogResultCancelAndCloses()
        {
            using var dlg = new ZLoginDialog();
            dlg.Show();

            Assert.True(dlg.Visible);
            Assert.True(dlg.CloseButton.CanSelect);

            dlg.CloseButton.PerformClick();

            Assert.False(dlg.Visible);
            Assert.Equal(DialogResult.Cancel, dlg.DialogResult);
        }

        [Fact]
        public void CloseButton_DirectClickInvocation_SetsDialogResultCancel()
        {
            using var dlg = new ZLoginDialog();

            // Invoke protected OnClick on the Button directly
            var onClickMethod = typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(onClickMethod);

            onClickMethod!.Invoke(dlg.CloseButton, new object[] { EventArgs.Empty });

            Assert.Equal(DialogResult.Cancel, dlg.DialogResult);
        }

        [Fact]
        public void CancelButton_WhenShown_SetsDialogResultCancel()
        {
            using var dlg = new ZLoginDialog();
            dlg.Show();

            var cancelBtn = dlg.CancelButton as Button;
            Assert.NotNull(cancelBtn);
            cancelBtn!.PerformClick();

            Assert.False(dlg.Visible);
            Assert.Equal(DialogResult.Cancel, dlg.DialogResult);
        }

        [Fact]
        public void ActionCancelButton_WhenShown_SetsDialogResultCancel()
        {
            using var dlg = new ZLoginDialog();
            dlg.Show();

            var btnCancel = GetPrivateField<Button>(dlg, "_btnCancel");
            Assert.NotNull(btnCancel);
            btnCancel.PerformClick();

            Assert.False(dlg.Visible);
            Assert.Equal(DialogResult.Cancel, dlg.DialogResult);
        }

        [Fact]
        public void EscapeKey_DismissesDialogWithCancel()
        {
            using var dlg = new ZLoginDialog();
            dlg.Show();

            var processDialogKeyMethod = typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(processDialogKeyMethod);

            bool handled = (bool)processDialogKeyMethod!.Invoke(dlg, new object[] { Keys.Escape })!;

            Assert.True(handled);
            Assert.False(dlg.Visible);
            Assert.Equal(DialogResult.Cancel, dlg.DialogResult);
        }

        #endregion

        #region Attack Surface 2: Dynamic Theme Switching

        [Fact]
        public void DynamicTheming_SwitchingBetweenLightAndDark_UpdatesAllComponents()
        {
            // Reset to Clean Light first
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var dlg = new ZLoginDialog();

            var txtUser = GetPrivateField<TextBox>(dlg, "_txtUsername");
            var txtPass = GetPrivateField<TextBox>(dlg, "_txtPassword");
            var txtPin = GetPrivateField<TextBox>(dlg, "_txtPin");
            var lblUser = GetPrivateField<Label>(dlg, "_lblUser");
            var lblPass = GetPrivateField<Label>(dlg, "_lblPass");
            var btnSubmit = GetPrivateField<Button>(dlg, "_btnSubmit");
            var btnCancel = GetPrivateField<Button>(dlg, "_btnCancel");
            var lblBadgeIcon = GetPrivateField<Label>(dlg, "_lblBadgeIcon");
            var lblStatus = GetPrivateField<Label>(dlg, "_lblStatus");

            // Verify Light Skin values
            var lightColors = ZeroTheme.Light;
            Assert.Equal(lightColors.CardBackground, dlg.BackColor);
            Assert.Equal(lightColors.Background, txtUser.BackColor);
            Assert.Equal(lightColors.TextPrimary, txtUser.ForeColor);
            Assert.Equal(lightColors.Background, txtPass.BackColor);
            Assert.Equal(lightColors.TextPrimary, txtPass.ForeColor);
            Assert.Equal(lightColors.Background, txtPin.BackColor);
            Assert.Equal(lightColors.TextPrimary, txtPin.ForeColor);
            Assert.Equal(lightColors.TextSecondary, lblUser.ForeColor);
            Assert.Equal(lightColors.TextSecondary, lblPass.ForeColor);
            Assert.Equal(lightColors.Primary, btnSubmit.BackColor);
            Assert.Equal(ZLoginDialog.GetAccentTextColor(lightColors), btnSubmit.ForeColor);
            Assert.Equal(lightColors.Hover, btnCancel.BackColor);
            Assert.Equal(lightColors.TextPrimary, btnCancel.ForeColor);
            Assert.Equal(lightColors.TextSecondary, dlg.CloseButton.ForeColor);
            Assert.Equal(lightColors.Hover, dlg.CloseButton.FlatAppearance.MouseOverBackColor);
            Assert.Equal(lightColors.Border, dlg.CloseButton.FlatAppearance.MouseDownBackColor);
            Assert.Equal(lightColors.Primary, lblBadgeIcon.ForeColor);
            Assert.Equal(lightColors.TextSecondary, lblStatus.ForeColor);

            // Switch to Obsidian Dark
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);

            var darkColors = ZeroTheme.Dark;
            Assert.Equal(darkColors.CardBackground, dlg.BackColor);
            Assert.Equal(darkColors.Background, txtUser.BackColor);
            Assert.Equal(darkColors.TextPrimary, txtUser.ForeColor);
            Assert.Equal(darkColors.Background, txtPass.BackColor);
            Assert.Equal(darkColors.TextPrimary, txtPass.ForeColor);
            Assert.Equal(darkColors.Background, txtPin.BackColor);
            Assert.Equal(darkColors.TextPrimary, txtPin.ForeColor);
            Assert.Equal(darkColors.TextSecondary, lblUser.ForeColor);
            Assert.Equal(darkColors.TextSecondary, lblPass.ForeColor);
            Assert.Equal(darkColors.Primary, btnSubmit.BackColor);
            Assert.Equal(ZLoginDialog.GetAccentTextColor(darkColors), btnSubmit.ForeColor);
            Assert.Equal(darkColors.Hover, btnCancel.BackColor);
            Assert.Equal(darkColors.TextPrimary, btnCancel.ForeColor);
            Assert.Equal(darkColors.TextSecondary, dlg.CloseButton.ForeColor);
            Assert.Equal(darkColors.Hover, dlg.CloseButton.FlatAppearance.MouseOverBackColor);
            Assert.Equal(darkColors.Border, dlg.CloseButton.FlatAppearance.MouseDownBackColor);
            Assert.Equal(darkColors.Primary, lblBadgeIcon.ForeColor);
            Assert.Equal(darkColors.TextSecondary, lblStatus.ForeColor);

            // Switch back to Clean Light to verify re-theming in place
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            Assert.Equal(lightColors.CardBackground, dlg.BackColor);
            Assert.Equal(lightColors.Background, txtUser.BackColor);
            Assert.Equal(lightColors.TextPrimary, txtUser.ForeColor);
            Assert.Equal(lightColors.Primary, btnSubmit.BackColor);
            Assert.Equal(lightColors.TextSecondary, dlg.CloseButton.ForeColor);
        }

        [Fact]
        public void DynamicTheming_ZeroTheme_CurrentMode_UpdatesDialog()
        {
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var dlg = new ZLoginDialog();
            Assert.Equal(ZeroTheme.Light.CardBackground, dlg.BackColor);

            ZeroTheme.CurrentMode = ZeroThemeMode.Dark;
            Assert.Equal(ZeroTheme.Dark.CardBackground, dlg.BackColor);

            ZeroTheme.CurrentMode = ZeroThemeMode.Light;
            Assert.Equal(ZeroTheme.Light.CardBackground, dlg.BackColor);
        }

        [Fact]
        public void DynamicTheming_ToggleTheme_UpdatesDialog()
        {
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var dlg = new ZLoginDialog();
            Assert.False(ZeroTheme.IsDark);

            ZeroTheme.ToggleTheme();
            Assert.True(ZeroTheme.IsDark);
            Assert.Equal(ZeroTheme.Dark.CardBackground, dlg.BackColor);

            ZeroTheme.ToggleTheme();
            Assert.False(ZeroTheme.IsDark);
            Assert.Equal(ZeroTheme.Light.CardBackground, dlg.BackColor);
        }

        [Fact]
        public void DualSkin_ContrastRatio_SubmitButtonAndActiveTab_MustMeetWcagThreshold()
        {
            // Clean Light: submit button White text on Primary (#4F46E5)
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            using (var dlg = new ZLoginDialog())
            {
                var btnSubmit = GetPrivateField<Button>(dlg, "_btnSubmit");
                double lightContrast = ZeroColorUtils.GetContrastRatio(
                    $"#{btnSubmit.ForeColor.R:X2}{btnSubmit.ForeColor.G:X2}{btnSubmit.ForeColor.B:X2}",
                    $"#{btnSubmit.BackColor.R:X2}{btnSubmit.BackColor.G:X2}{btnSubmit.BackColor.B:X2}");
                Assert.True(lightContrast >= 4.5, $"CleanLight submit button contrast was {lightContrast:F2} (< 4.5:1)");

                double lightTabContrast = GetContrast(ZLoginDialog.GetAccentTextColor(ZeroTheme.Light), ZeroTheme.Light.Primary);
                Assert.True(lightTabContrast >= 4.5, $"CleanLight active tab contrast was {lightTabContrast:F2} (< 4.5:1)");
            }

            // Obsidian Dark: submit button and active tab contrast on Primary (#818CF8)
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
            using (var dlg = new ZLoginDialog())
            {
                var btnSubmit = GetPrivateField<Button>(dlg, "_btnSubmit");
                double darkContrast = ZeroColorUtils.GetContrastRatio(
                    $"#{btnSubmit.ForeColor.R:X2}{btnSubmit.ForeColor.G:X2}{btnSubmit.ForeColor.B:X2}",
                    $"#{btnSubmit.BackColor.R:X2}{btnSubmit.BackColor.G:X2}{btnSubmit.BackColor.B:X2}");
                Assert.True(darkContrast >= 4.5, $"ObsidianDark submit button contrast was {darkContrast:F2} (< 4.5:1)");

                double darkTabContrast = GetContrast(ZLoginDialog.GetAccentTextColor(ZeroTheme.Dark), ZeroTheme.Dark.Primary);
                Assert.True(darkTabContrast >= 4.5, $"ObsidianDark active tab contrast was {darkTabContrast:F2} (< 4.5:1)");
            }
        }

        #endregion

        #region Attack Surface 3: Disposal & Memory Leak Prevention

        [Fact]
        public void Dispose_UnsubscribesFromThemeChanged_PreventsExceptions()
        {
            var dlg = new ZLoginDialog();
            dlg.Dispose();

            // Triggering theme switch after disposal should NOT throw ObjectDisposedException
            var ex = Record.Exception(() =>
            {
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            });

            Assert.Null(ex);
        }

        #endregion

        #region Attack Surface 4: Rendering & Tab Switching Under Both Skins

        [Fact]
        public void PaintAndTabSwitching_BothSkins_RendersWithoutExceptions()
        {
            foreach (var skin in new[] { ZeroSkinDefaults.CleanLight, ZeroSkinDefaults.ObsidianDark })
            {
                ZeroSkinManager.ApplySkin(skin);

                using var dlg = new ZLoginDialog();
                using var bmp = new Bitmap(dlg.Width, dlg.Height);
                using var g = Graphics.FromImage(bmp);

                // Simulate paint
                var paintMethod = typeof(ZLoginDialog).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(paintMethod);
                paintMethod!.Invoke(dlg, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, dlg.Width, dlg.Height)) });

                // Simulate clicking tabs
                var clickMethod = typeof(ZLoginDialog).GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(clickMethod);

                var tabPinRect = GetPrivateField<Rectangle>(dlg, "_tabPinRect");
                clickMethod!.Invoke(dlg, new object[] { new MouseEventArgs(MouseButtons.Left, 1, tabPinRect.X + 2, tabPinRect.Y + 2, 0) });
                paintMethod!.Invoke(dlg, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, dlg.Width, dlg.Height)) });

                var tabBadgeRect = GetPrivateField<Rectangle>(dlg, "_tabBadgeRect");
                clickMethod!.Invoke(dlg, new object[] { new MouseEventArgs(MouseButtons.Left, 1, tabBadgeRect.X + 2, tabBadgeRect.Y + 2, 0) });
                paintMethod!.Invoke(dlg, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, dlg.Width, dlg.Height)) });

                var tabPassRect = GetPrivateField<Rectangle>(dlg, "_tabPassRect");
                clickMethod!.Invoke(dlg, new object[] { new MouseEventArgs(MouseButtons.Left, 1, tabPassRect.X + 2, tabPassRect.Y + 2, 0) });
                paintMethod!.Invoke(dlg, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, dlg.Width, dlg.Height)) });
            }
        }

        #endregion

        #region Attack Surface 5: Static Code Guard

        [Fact]
        public void StaticColorGuard_ZLoginDialog_ZeroProhibitedLiterals()
        {
            string sourcePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../src/WinForms/ZeroUI.WinForms.Common/Overlays/ZLoginDialog.cs"));

            Assert.True(System.IO.File.Exists(sourcePath), $"Source file not found at: {sourcePath}");

            string[] lines = System.IO.File.ReadAllLines(sourcePath);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                    continue;

                // Check for hardcoded FromArgb
                Assert.DoesNotContain("FromArgb", trimmed);

                // Check for Brushes.
                Assert.DoesNotContain("Brushes.", trimmed);

                // Check for hardcoded hex colors
                Assert.False(
                    System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"#[0-9a-fA-F]{6}"),
                    $"Hardcoded hex color found in line: {line}");
            }
        }

        #endregion
    }
}
