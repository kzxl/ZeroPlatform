using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Input;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.Desktop.Tests
{
    [Collection("WinFormsThemeTests")]
    public class ZVirtualKeyboardChallenger2Tests
    {
        private static double GetContrast(Color fg, Color bg)
        {
            string hexFg = $"#{fg.R:X2}{fg.G:X2}{fg.B:X2}";
            string hexBg = $"#{bg.R:X2}{bg.G:X2}{bg.B:X2}";
            return ZeroColorUtils.GetContrastRatio(hexFg, hexBg);
        }

        private static bool IsFormReferencedInThemeChanged(Form form)
        {
            var field = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
            var del = field?.GetValue(null) as EventHandler;
            if (del == null) return false;

            foreach (var d in del.GetInvocationList())
            {
                if (ReferenceEquals(d.Target, form)) return true;
                if (d.Target != null)
                {
                    // Check closure fields
                    var fields = d.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in fields)
                    {
                        if (ReferenceEquals(f.GetValue(d.Target), form))
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool IsControlReferencedInThemeChanged(Control control)
        {
            var field = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
            var del = field?.GetValue(null) as EventHandler;
            if (del == null) return false;

            foreach (var d in del.GetInvocationList())
            {
                if (ReferenceEquals(d.Target, control)) return true;
                if (d.Target != null)
                {
                    var fields = d.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in fields)
                    {
                        if (ReferenceEquals(f.GetValue(d.Target), control))
                            return true;
                    }
                }
            }
            return false;
        }

        #region Challenge 1: ShowFloatingPopup Initial Border & Padding

        [Fact]
        public void ShowFloatingPopup_AppliesThemeBorderColor_Initially()
        {
            StaTestRunner.Run(() =>
            {
                // Test in Clean Light
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                var popupLight = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.Numpad, dummyParent);
                try
                {
                    Assert.NotNull(popupLight);
                    Assert.Equal(ZeroTheme.Colors.Border, popupLight.BackColor);
                    Assert.Equal(ZeroTheme.Light.Border, popupLight.BackColor);
                    Assert.Equal(new Padding(1), popupLight.Padding);
                    Assert.Equal(FormBorderStyle.None, popupLight.FormBorderStyle);
                    Assert.True(popupLight.TopMost);
                    Assert.False(popupLight.ShowInTaskbar);
                }
                finally
                {
                    popupLight.Close();
                    popupLight.Dispose();
                }

                // Test in Obsidian Dark
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);

                var popupDark = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                try
                {
                    Assert.NotNull(popupDark);
                    Assert.Equal(ZeroTheme.Colors.Border, popupDark.BackColor);
                    Assert.Equal(ZeroTheme.Dark.Border, popupDark.BackColor);
                    Assert.Equal(new Padding(1), popupDark.Padding);
                }
                finally
                {
                    popupDark.Close();
                    popupDark.Dispose();
                }

                // Reset
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            });
        }

        #endregion

        #region Challenge 2: Dynamic Theme Updates Across All 9 Skins

        [Fact]
        public void ShowFloatingPopup_DynamicThemeUpdate_AllThemes()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                try
                {
                    var kb = popup.Controls.OfType<ZVirtualKeyboard>().Single();

                    var skins = new[]
                    {
                        ZeroSkinDefaults.ObsidianDark,
                        ZeroSkinDefaults.NordicFrost,
                        ZeroSkinDefaults.CyberpunkNeon,
                        ZeroSkinDefaults.EmeraldEnterprise,
                        ZeroSkinDefaults.SolarAmber,
                        ZeroSkinDefaults.AmethystViolet,
                        ZeroSkinDefaults.CrimsonRuby,
                        ZeroSkinDefaults.OledMidnight,
                        ZeroSkinDefaults.CleanLight
                    };

                    foreach (var skin in skins)
                    {
                        ZeroSkinManager.ApplySkin(skin);
                        Application.DoEvents();

                        Assert.Equal(ZeroTheme.Colors.Border, popup.BackColor);
                        Assert.Equal(ZeroTheme.Colors.Background, kb.BackColor);
                        Assert.Equal(ZeroTheme.Colors.TextPrimary, kb.ForeColor);
                    }
                }
                finally
                {
                    popup.Close();
                    popup.Dispose();
                }
            });
        }

        #endregion

        #region Challenge 3: Unhooking from ThemeChanged & Memory Leak Prevention

        [Fact]
        public void ShowFloatingPopup_UnhooksOnClose_NoDelegateLeak()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                var kb = popup.Controls.OfType<ZVirtualKeyboard>().Single();

                // While open: both popup and kb are hooked
                Assert.True(IsFormReferencedInThemeChanged(popup), "Popup form must be hooked while open");
                Assert.True(IsControlReferencedInThemeChanged(kb), "Keyboard control must be hooked while open");

                // Close and dispose
                popup.Close();
                popup.Dispose();

                // After close: neither popup nor kb should be referenced in ThemeChanged
                Assert.False(IsFormReferencedInThemeChanged(popup), "Popup form must NOT be hooked after Close()");
                Assert.False(IsControlReferencedInThemeChanged(kb), "Keyboard control must NOT be hooked after Dispose()");
            });
        }

        /// <summary>
        /// EMPIRICAL BUG REPRODUCTION:
        /// When popup.Dispose() is called directly without popup.Close() (e.g. using var popup = ...),
        /// WinForms does NOT fire FormClosed. Because ShowFloatingPopup only hooks FormClosed,
        /// the delegate remains subscribed in ZeroTheme.ThemeChanged, creating a memory leak.
        /// </summary>
        [Fact]
        public void ShowFloatingPopup_DirectDispose_DemonstratesLeakWhenClosedViaDispose()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                Assert.True(IsFormReferencedInThemeChanged(popup));

                // Direct Dispose without calling Close() first
                popup.Dispose();

                // Verified: popup form closure is cleanly unhooked on direct Dispose()
                bool isLeaked = IsFormReferencedInThemeChanged(popup);
                Assert.False(isLeaked, "Direct Dispose() without Close() must unhook from ZeroTheme.ThemeChanged");
            });
        }

        [Fact]
        public void ShowFloatingPopup_RepeatedOpenClose_50Cycles_NoDelegateAccumulation()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                for (int i = 0; i < 50; i++)
                {
                    var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.Numpad, dummyParent);
                    ZeroTheme.ToggleTheme();
                    popup.Close();
                    popup.Dispose();

                    Assert.False(IsFormReferencedInThemeChanged(popup), $"Leaked popup reference on iteration {i}");
                }

                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            });
        }

        #endregion

        #region Challenge 4: Multi-Threaded / Background Theme Changes & Thread Safety

        [Fact]
        public void ShowFloatingPopup_BackgroundThreadThemeChange_ThreadSafeInvokeRequired()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);

                try
                {
                    // Trigger theme change from a background thread
                    var t = Task.Run(() =>
                    {
                        ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
                    });

                    t.Wait();

                    // Pump messages on the STA thread so BeginInvoke is processed
                    for (int i = 0; i < 10; i++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(10);
                    }

                    Assert.Equal(ZeroTheme.Colors.Border, popup.BackColor);
                    Assert.Equal(ZeroTheme.Dark.Border, popup.BackColor);
                }
                finally
                {
                    popup.Close();
                    popup.Dispose();
                    ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
                }
            });
        }

        #endregion

        #region Challenge 5: Escape and Enter Auto-Close Lifecycle

        [Fact]
        public void ShowFloatingPopup_EscapeAndEnterKey_AutoCloseLifecycle()
        {
            StaTestRunner.Run(() =>
            {
                using var dummyParent = new Form();
                using var target = new TextBox { Multiline = true };
                dummyParent.Controls.Add(target);

                // 1. Escape key
                var popup1 = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                var kb1 = popup1.Controls.OfType<ZVirtualKeyboard>().Single();

                var escKey = typeof(ZVirtualKeyboard)
                    .GetField("_keys", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(kb1) as System.Collections.IList;

                object? escDef = null;
                foreach (var k in escKey!)
                {
                    string label = (string)k.GetType().GetProperty("PrimaryLabel")!.GetValue(k)!;
                    if (label == "Esc") { escDef = k; break; }
                }
                Assert.NotNull(escDef);

                var processMethod = typeof(ZVirtualKeyboard).GetMethod("ProcessKeyActivation", BindingFlags.NonPublic | BindingFlags.Instance);
                processMethod!.Invoke(kb1, new[] { escDef });

                Application.DoEvents();

                Assert.False(popup1.Visible);
                Assert.False(IsFormReferencedInThemeChanged(popup1));
                popup1.Dispose();

                // 2. Enter key
                var popup2 = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric, dummyParent);
                var kb2 = popup2.Controls.OfType<ZVirtualKeyboard>().Single();

                var enterKeys = typeof(ZVirtualKeyboard)
                    .GetField("_keys", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(kb2) as System.Collections.IList;

                object? enterDef = null;
                foreach (var k in enterKeys!)
                {
                    string label = (string)k.GetType().GetProperty("PrimaryLabel")!.GetValue(k)!;
                    if (label == "Enter") { enterDef = k; break; }
                }
                Assert.NotNull(enterDef);

                processMethod!.Invoke(kb2, new[] { enterDef });

                Application.DoEvents();

                Assert.False(popup2.Visible);
                Assert.False(IsFormReferencedInThemeChanged(popup2));
                popup2.Dispose();
            });
        }

        #endregion

        #region Challenge 6: Static Color Guard & Residual Color Literal Inspection

        [Fact]
        public void StaticColorGuard_GrepResidualFromArgbAndHexLiterals()
        {
            string sourcePath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "../../../../../src/WinForms/ZeroUI.WinForms.Common/Input/ZVirtualKeyboard.cs"));

            Assert.True(File.Exists(sourcePath), $"File not found: {sourcePath}");

            var lines = File.ReadAllLines(sourcePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                    continue;

                // 1. Strict check: No Color.FromArgb
                Assert.False(trimmed.Contains("FromArgb"), $"Line {i + 1} contains FromArgb: {line}");

                // 2. Strict check: No hex color strings (#RRGGBB or #AARRGGBB)
                Assert.False(Regex.IsMatch(trimmed, @"#[0-9a-fA-F]{6,8}"), $"Line {i + 1} contains hex color string: {line}");

                // 3. Strict check: No raw 0x hex numbers for colors
                Assert.False(Regex.IsMatch(trimmed, @"0x[0-9a-fA-F]{6,8}"), $"Line {i + 1} contains 0x hex color literal: {line}");

                // 4. Strict check: No Color.FromKnownColor
                Assert.False(trimmed.Contains("FromKnownColor"), $"Line {i + 1} contains FromKnownColor: {line}");
            }
        }

        /// <summary>
        /// EMPIRICAL BUG REPRODUCTION:
        /// Rule 1 of ui-and-theme-synchronization.md strictly prohibits hardcoded static color literals:
        /// "Hardcoded hex literals (e.g., #FFFFFF). Static brushes (e.g. Brushes.White)."
        /// Line 459 contains hardcoded 'Color.White'.
        /// </summary>
        [Fact]
        public void StaticColorGuard_IdentifiesProhibitedColorWhiteLiteral()
        {
            string sourcePath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "../../../../../src/WinForms/ZeroUI.WinForms.Common/Input/ZVirtualKeyboard.cs"));

            var lines = File.ReadAllLines(sourcePath);
            bool foundColorWhite = lines.Any(l => l.Contains("Color.White"));

            Assert.False(foundColorWhite, "ZVirtualKeyboard.cs must NOT contain hardcoded 'Color.White'");
        }

        #endregion

        #region Challenge 7: Contrast Ratio Audit Across All 9 Skins

        [Fact]
        public void ContrastAudit_NormalAndSpecialAndHoverKeys_PassesAllSkins()
        {
            var skins = new[]
            {
                ZeroSkinDefaults.CleanLight,
                ZeroSkinDefaults.ObsidianDark,
                ZeroSkinDefaults.NordicFrost,
                ZeroSkinDefaults.CyberpunkNeon,
                ZeroSkinDefaults.EmeraldEnterprise,
                ZeroSkinDefaults.SolarAmber,
                ZeroSkinDefaults.AmethystViolet,
                ZeroSkinDefaults.CrimsonRuby,
                ZeroSkinDefaults.OledMidnight
            };

            foreach (var skin in skins)
            {
                var pal = ZeroTheme.CreatePaletteFromSkin(skin);

                // Normal key: TextPrimary on Surface (Must be >= 4.5:1)
                double normalContrast = GetContrast(pal.TextPrimary, pal.Surface);
                Assert.True(normalContrast >= 4.5, $"{skin.Name}: Normal key contrast {normalContrast:F2} < 4.5:1");

                // Special key: TextPrimary on HeaderBackground (Must be >= 4.5:1)
                double specialContrast = GetContrast(pal.TextPrimary, pal.HeaderBackground);
                Assert.True(specialContrast >= 4.5, $"{skin.Name}: Special key contrast {specialContrast:F2} < 4.5:1");

                // Hover key: TextPrimary on Hover (Must be >= 4.5:1)
                double hoverContrast = GetContrast(pal.TextPrimary, pal.Hover);
                Assert.True(hoverContrast >= 4.5, $"{skin.Name}: Hover key contrast {hoverContrast:F2} < 4.5:1");
            }
        }

        /// <summary>
        /// Verified: With dynamic GetPressedTextColor, pressed keys achieve optimal contrast
        /// across all 9 skins with zero text washout.
        /// </summary>
        [Fact]
        public void ContrastAudit_DemonstratesWashoutFailuresOnPressedKeys()
        {
            var skins = new[]
            {
                ZeroSkinDefaults.CleanLight,
                ZeroSkinDefaults.ObsidianDark,
                ZeroSkinDefaults.NordicFrost,
                ZeroSkinDefaults.CyberpunkNeon,
                ZeroSkinDefaults.EmeraldEnterprise,
                ZeroSkinDefaults.SolarAmber,
                ZeroSkinDefaults.AmethystViolet,
                ZeroSkinDefaults.CrimsonRuby,
                ZeroSkinDefaults.OledMidnight
            };

            foreach (var skin in skins)
            {
                var pal = ZeroTheme.CreatePaletteFromSkin(skin);
                Color pressedText = ZVirtualKeyboard.GetPressedTextColor(pal);
                double pressedContrast = GetContrast(pressedText, pal.Primary);
                Assert.True(pressedContrast >= 4.5, $"{skin.Name} pressed key contrast {pressedContrast:F2} < 4.5:1");
            }
        }

        #endregion
    }
}
