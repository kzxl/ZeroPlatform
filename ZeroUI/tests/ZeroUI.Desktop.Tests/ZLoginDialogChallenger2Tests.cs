using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Input;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.Desktop.Tests
{
    [Collection("WinFormsThemeTests")]
    public class ZLoginDialogChallenger2Tests
    {
        private static double GetContrast(Color fg, Color bg)
        {
            string hexFg = $"#{fg.R:X2}{fg.G:X2}{fg.B:X2}";
            string hexBg = $"#{bg.R:X2}{bg.G:X2}{bg.B:X2}";
            return ZeroColorUtils.GetContrastRatio(hexFg, hexBg);
        }

        [Fact]
        public void ZLoginDialog_CloseButton_ContractAndBehavior()
        {
            using var dlg = new ZLoginDialog();
            Assert.NotNull(dlg.CloseButton);
            Assert.Equal("✕", dlg.CloseButton.Text);
            Assert.Same(dlg.CloseButton, dlg.CancelButton);

            // Verify size and position
            Assert.Equal(30, dlg.CloseButton.Width);
            Assert.Equal(30, dlg.CloseButton.Height);
            Assert.Equal(dlg.Width - 42, dlg.CloseButton.Location.X);
            Assert.Equal(12, dlg.CloseButton.Location.Y);

            // Simulate Click in modal dialog flow
            StaTestRunner.Run(() =>
            {
                using var modalDlg = new ZLoginDialog();
                modalDlg.Shown += (s, e) =>
                {
                    modalDlg.CloseButton.PerformClick();
                };
                var result = modalDlg.ShowDialog();
                Assert.Equal(DialogResult.Cancel, result);
            });

            // Simulate ESC key dismiss in modal dialog flow
            StaTestRunner.Run(() =>
            {
                using var modalDlg = new ZLoginDialog();
                modalDlg.Shown += (s, e) =>
                {
                    // Trigger ESC key via ProcessDialogKey
                    var processDialogKey = typeof(ZLoginDialog).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic);
                    processDialogKey?.Invoke(modalDlg, new object[] { Keys.Escape });
                };
                var result = modalDlg.ShowDialog();
                Assert.Equal(DialogResult.Cancel, result);
            });
        }

        [Fact]
        public void ZLoginDialog_Dispose_UnsubscribesFromThemeChanged()
        {
            var themeChangedField = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);

            var dlg = new ZLoginDialog();
            var afterSubDelegate = themeChangedField?.GetValue(null) as EventHandler;
            var afterSubList = afterSubDelegate?.GetInvocationList() ?? Array.Empty<Delegate>();

            // ZLoginDialog subscribes, and its child _pinNumpad (ZVirtualKeyboard) also subscribes
            var dialogSubs = afterSubList.Where(d =>
                d.Target is ZLoginDialog || d.Target is ZeroUI.WinForms.Input.ZVirtualKeyboard).ToList();
            Assert.Equal(2, dialogSubs.Count);

            dlg.Dispose();

            var afterDisposeDelegate = themeChangedField?.GetValue(null) as EventHandler;
            var afterDisposeList = afterDisposeDelegate?.GetInvocationList() ?? Array.Empty<Delegate>();

            // After dispose, no subscribers from ZLoginDialog or ZVirtualKeyboard should remain
            var remainingSubs = afterDisposeList.Where(d =>
                d.Target is ZLoginDialog || d.Target is ZeroUI.WinForms.Input.ZVirtualKeyboard).ToList();
            Assert.Empty(remainingSubs);
        }

        [Fact]
        public void ZLoginDialog_StressTest_100Instances_RapidThemeSwitching()
        {
            for (int i = 0; i < 100; i++)
            {
                using (var dlg = new ZLoginDialog())
                {
                    ZeroTheme.ToggleTheme();
                    Assert.Equal(ZeroTheme.Colors.CardBackground, dlg.BackColor);
                }
            }

            // Force GC to verify no retained instances
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var themeChangedField = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
            var finalDelegate = themeChangedField?.GetValue(null) as EventHandler;
            var finalList = finalDelegate?.GetInvocationList() ?? Array.Empty<Delegate>();

            // No leaked subscribers from ZLoginDialog or ZVirtualKeyboard should remain
            var leakedSubs = finalList.Where(d =>
                d.Target is ZLoginDialog || d.Target is ZeroUI.WinForms.Input.ZVirtualKeyboard).ToList();
            Assert.Empty(leakedSubs);
        }

        [Theory]
        [InlineData("clean_light")]
        [InlineData("obsidian_dark")]
        [InlineData("nordic_frost")]
        [InlineData("cyberpunk_neon")]
        [InlineData("emerald_enterprise")]
        [InlineData("solar_amber")]
        [InlineData("amethyst_violet")]
        [InlineData("crimson_ruby")]
        [InlineData("oled_midnight")]
        public void ZLoginDialog_AdversarialContrastAudit_AllElements(string skinName)
        {
            ZeroTheme.ApplySkin(skinName);
            var colors = ZeroTheme.Colors;

            using var dlg = new ZLoginDialog();

            // 1. Dialog background & outer border
            var cardBg = dlg.BackColor;
            Assert.Equal(colors.CardBackground, cardBg);

            // 2. Title & Subtitle contrast
            double titleContrast = GetContrast(colors.TextPrimary, cardBg);
            double subContrast = GetContrast(colors.TextSecondary, cardBg);

            // 3. Text inputs contrast
            var txtUser = typeof(ZLoginDialog).GetField("_txtUsername", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(dlg) as TextBox;
            Assert.NotNull(txtUser);
            double txtContrast = GetContrast(txtUser.ForeColor, txtUser.BackColor);

            // 4. Close Button contrast
            double closeBtnContrast = GetContrast(dlg.CloseButton.ForeColor, cardBg);

            // 5. Cancel Button contrast
            var btnCancel = typeof(ZLoginDialog).GetField("_btnCancel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(dlg) as Button;
            Assert.NotNull(btnCancel);
            double cancelBtnContrast = GetContrast(btnCancel.ForeColor, btnCancel.BackColor);

            // 6. Submit Button (Sign In) contrast
            var btnSubmit = typeof(ZLoginDialog).GetField("_btnSubmit", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(dlg) as Button;
            Assert.NotNull(btnSubmit);
            double submitBtnContrast = GetContrast(btnSubmit.ForeColor, btnSubmit.BackColor);

            // 7. Active Tab contrast
            double activeTabContrast = GetContrast(ZLoginDialog.GetAccentTextColor(colors), colors.Primary);

            // 8. Inactive Tab contrast (Text: colors.TextSecondary, Back: colors.Surface)
            double inactiveTabContrast = GetContrast(colors.TextSecondary, colors.Surface);

            // 9. Status label normal, in-progress, failure contrast against cardBg
            double statusNormalContrast = GetContrast(colors.TextSecondary, cardBg);
            double statusProgressContrast = GetContrast(colors.Primary, cardBg);
            double statusDangerContrast = GetContrast(colors.Danger, cardBg);

            // Diagnostic output
            Console.WriteLine($"CONTRAST_DATA|{skinName}|Title:{titleContrast:F2}|Sub:{subContrast:F2}|Txt:{txtContrast:F2}|Close:{closeBtnContrast:F2}|Cancel:{cancelBtnContrast:F2}|Submit:{submitBtnContrast:F2}|ActiveTab:{activeTabContrast:F2}|InactiveTab:{inactiveTabContrast:F2}|StatusProg:{statusProgressContrast:F2}|StatusFail:{statusDangerContrast:F2}");

            // Standard requirements: >= 4.5:1
            Assert.True(titleContrast >= 4.5, $"Title contrast {titleContrast:F2}:1 < 4.5 in skin {skinName}");
            Assert.True(subContrast >= 4.5, $"Subtitle contrast {subContrast:F2}:1 < 4.5 in skin {skinName}");
            Assert.True(txtContrast >= 4.5, $"TextBox text contrast {txtContrast:F2}:1 < 4.5 in skin {skinName}");
            Assert.True(closeBtnContrast >= 4.5, $"CloseButton contrast {closeBtnContrast:F2}:1 < 4.5 in skin {skinName}");
            Assert.True(cancelBtnContrast >= 4.5, $"CancelButton contrast {cancelBtnContrast:F2}:1 < 4.5 in skin {skinName}");
            Assert.True(inactiveTabContrast >= 4.5, $"InactiveTab contrast {inactiveTabContrast:F2}:1 < 4.5 in skin {skinName}");

            // Submit button & Active tab must maintain >= 4.5:1
            Assert.True(submitBtnContrast >= 4.5, $"SubmitButton contrast {submitBtnContrast:F2}:1 < 4.5 in skin {skinName} (Text: {btnSubmit.ForeColor}, Back: {btnSubmit.BackColor})");
            Assert.True(activeTabContrast >= 4.5, $"ActiveTab contrast {activeTabContrast:F2}:1 < 4.5 in skin {skinName} (Text: {ZLoginDialog.GetAccentTextColor(colors)}, Back: {colors.Primary})");
        }
    }
}
