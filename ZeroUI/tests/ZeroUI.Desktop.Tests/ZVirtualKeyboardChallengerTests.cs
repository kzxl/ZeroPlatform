using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    public class ZVirtualKeyboardChallengerTests
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

        private static object InvokePrivateMethod(object instance, string methodName, params object[] parameters)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return method!.Invoke(instance, parameters)!;
        }

        #region Attack Surface 1: Static Color Guard & Zero Prohibited Literals

        [Fact]
        public void StaticColorGuard_ZVirtualKeyboard_ZeroFromArgbAndZeroHexLiterals()
        {
            string sourcePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../src/WinForms/ZeroUI.WinForms.Common/Input/ZVirtualKeyboard.cs"));

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
                    Regex.IsMatch(trimmed, @"#[0-9a-fA-F]{6}"),
                    $"Hardcoded hex color found in line: {line}");
            }
        }

        #endregion

        #region Attack Surface 2: Dynamic Theme Switching on ZVirtualKeyboard

        [Fact]
        public void DynamicTheming_SwitchingBetweenLightAndDark_UpdatesKeyboardImmediately()
        {
            // Reset to Clean Light first
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var kb = new ZVirtualKeyboard();

            Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);
            Assert.Equal(ZeroTheme.Light.TextPrimary, kb.ForeColor);

            // Switch to Obsidian Dark
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);

            Assert.Equal(ZeroTheme.Dark.Background, kb.BackColor);
            Assert.Equal(ZeroTheme.Dark.TextPrimary, kb.ForeColor);

            // Switch back to Clean Light
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);
            Assert.Equal(ZeroTheme.Light.TextPrimary, kb.ForeColor);
        }

        [Fact]
        public void DynamicTheming_ZeroTheme_CurrentMode_UpdatesKeyboardImmediately()
        {
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var kb = new ZVirtualKeyboard();
            Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);

            ZeroTheme.CurrentMode = ZeroThemeMode.Dark;
            Assert.Equal(ZeroTheme.Dark.Background, kb.BackColor);
            Assert.Equal(ZeroTheme.Dark.TextPrimary, kb.ForeColor);

            ZeroTheme.CurrentMode = ZeroThemeMode.Light;
            Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);
            Assert.Equal(ZeroTheme.Light.TextPrimary, kb.ForeColor);
        }

        [Fact]
        public void DynamicTheming_ToggleTheme_UpdatesKeyboardImmediately()
        {
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

            using var kb = new ZVirtualKeyboard();
            Assert.False(ZeroTheme.IsDark);

            ZeroTheme.ToggleTheme();
            Assert.True(ZeroTheme.IsDark);
            Assert.Equal(ZeroTheme.Dark.Background, kb.BackColor);

            ZeroTheme.ToggleTheme();
            Assert.False(ZeroTheme.IsDark);
            Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);
        }

        #endregion

        #region Attack Surface 3: Floating Popup Dynamic Theming & Lifecycle

        [Fact]
        public void ShowFloatingPopup_ThemeSynchronizationAndBorderPadding()
        {
            StaTestRunner.Run(() =>
            {
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);

                using var dummyParent = new Form();
                using var target = new TextBox();
                dummyParent.Controls.Add(target);

                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.Numpad, dummyParent);
                try
                {
                    Assert.NotNull(popup);
                    Assert.Equal(FormBorderStyle.None, popup.FormBorderStyle);
                    Assert.True(popup.TopMost);
                    Assert.Equal(new Padding(1), popup.Padding);
                    Assert.Equal(ZeroTheme.Light.Border, popup.BackColor);

                    // Verify embedded keyboard
                    Assert.Single(popup.Controls);
                    var kb = popup.Controls[0] as ZVirtualKeyboard;
                    Assert.NotNull(kb);
                    Assert.Equal(DockStyle.Fill, kb!.Dock);
                    Assert.Equal(VirtualKeyboardLayout.Numpad, kb.LayoutMode);
                    Assert.Same(target, kb.TargetControl);

                    // Switch to Dark Theme at runtime
                    ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
                    Application.DoEvents();

                    Assert.Equal(ZeroTheme.Dark.Border, popup.BackColor);
                    Assert.Equal(ZeroTheme.Dark.Background, kb.BackColor);

                    // Switch back to Light Theme
                    ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
                    Application.DoEvents();

                    Assert.Equal(ZeroTheme.Light.Border, popup.BackColor);
                    Assert.Equal(ZeroTheme.Light.Background, kb.BackColor);
                }
                finally
                {
                    popup.Close();
                    popup.Dispose();
                }
            });
        }

        [Fact]
        public void ShowFloatingPopup_FormClosed_UnsubscribesFromThemeChanged_PreventsLeak()
        {
            StaTestRunner.Run(() =>
            {
                var themeChangedField = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
                var initialDelegate = themeChangedField?.GetValue(null) as EventHandler;
                int initialCount = initialDelegate?.GetInvocationList().Length ?? 0;

                using var target = new TextBox();
                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric);

                var afterSubDelegate = themeChangedField?.GetValue(null) as EventHandler;
                int afterSubCount = afterSubDelegate?.GetInvocationList().Length ?? 0;

                // Popup form and embedded ZVirtualKeyboard (ControlBase) both subscribe
                Assert.True(afterSubCount > initialCount);

                popup.Close();
                popup.Dispose();

                var afterDisposeDelegate = themeChangedField?.GetValue(null) as EventHandler;
                int afterDisposeCount = afterDisposeDelegate?.GetInvocationList().Length ?? 0;

                // All handlers from popup and kb must be unsubscribed
                Assert.Equal(initialCount, afterDisposeCount);
            });
        }

        [Fact]
        public void ShowFloatingPopup_DirectDispose_UnhooksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var themeChangedField = typeof(ZeroTheme).GetField("ThemeChanged", BindingFlags.Static | BindingFlags.NonPublic);
                var initialDelegate = themeChangedField?.GetValue(null) as EventHandler;
                int initialCount = initialDelegate?.GetInvocationList().Length ?? 0;

                using var target = new TextBox();
                var popup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric);

                // Call Dispose directly without calling Close() first
                popup.Dispose();

                var afterDisposeDelegate = themeChangedField?.GetValue(null) as EventHandler;
                int afterDisposeCount = afterDisposeDelegate?.GetInvocationList().Length ?? 0;

                Assert.Equal(initialCount, afterDisposeCount);
            });
        }

        #endregion

        #region Attack Surface 4: WCAG AA Contrast Audit across Dual Skins

        [Fact]
        public void DualSkin_ContrastAudit_KeySurfacesAndLabels_MustMeetWcagThreshold()
        {
            // Clean Light Theme
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            var light = ZeroTheme.Light;

            // Normal key label on normal key surface: TextPrimary on Surface
            double lightNormalKeyContrast = GetContrast(light.TextPrimary, light.Surface);
            Assert.True(lightNormalKeyContrast >= 4.5, $"CleanLight normal key contrast was {lightNormalKeyContrast:F2} (< 4.5:1)");

            // Special key label on special key surface: TextPrimary on HeaderBackground
            double lightSpecialKeyContrast = GetContrast(light.TextPrimary, light.HeaderBackground);
            Assert.True(lightSpecialKeyContrast >= 4.5, $"CleanLight special key contrast was {lightSpecialKeyContrast:F2} (< 4.5:1)");

            // Key label on hover surface: TextPrimary on Hover
            double lightHoverKeyContrast = GetContrast(light.TextPrimary, light.Hover);
            Assert.True(lightHoverKeyContrast >= 4.5, $"CleanLight hover key contrast was {lightHoverKeyContrast:F2} (< 4.5:1)");

            // Pressed key label on Primary
            double lightPressedKeyContrast = GetContrast(ZVirtualKeyboard.GetPressedTextColor(light), light.Primary);
            Assert.True(lightPressedKeyContrast >= 4.5, $"CleanLight pressed key contrast was {lightPressedKeyContrast:F2} (< 4.5:1)");

            // Obsidian Dark Theme
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
            var dark = ZeroTheme.Dark;

            // Normal key label on normal key surface: TextPrimary on Surface
            double darkNormalKeyContrast = GetContrast(dark.TextPrimary, dark.Surface);
            Assert.True(darkNormalKeyContrast >= 4.5, $"ObsidianDark normal key contrast was {darkNormalKeyContrast:F2} (< 4.5:1)");

            // Special key label on special key surface: TextPrimary on HeaderBackground
            double darkSpecialKeyContrast = GetContrast(dark.TextPrimary, dark.HeaderBackground);
            Assert.True(darkSpecialKeyContrast >= 4.5, $"ObsidianDark special key contrast was {darkSpecialKeyContrast:F2} (< 4.5:1)");

            // Key label on hover surface: TextPrimary on Hover
            double darkHoverKeyContrast = GetContrast(dark.TextPrimary, dark.Hover);
            Assert.True(darkHoverKeyContrast >= 4.5, $"ObsidianDark hover key contrast was {darkHoverKeyContrast:F2} (< 4.5:1)");

            // Pressed key label on Primary: dynamic GetPressedTextColor achieves optimal theme contrast
            double darkPressedKeyContrast = GetContrast(ZVirtualKeyboard.GetPressedTextColor(dark), dark.Primary);
            Assert.True(darkPressedKeyContrast >= 4.5, $"ObsidianDark pressed key contrast was {darkPressedKeyContrast:F2} (< 4.5:1)");

            // Reset back to Clean Light for other test isolation
            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
        }

        #endregion

        #region Attack Surface 5: Hit-Testing & Layout Integrity

        [Fact]
        public void HitTesting_AlphaNumericLayout_AllKeysHitTestedAtCenter()
        {
            using var kb = new ZVirtualKeyboard { Size = new Size(680, 240) };
            var keys = GetPrivateField<System.Collections.IList>(kb, "_keys");
            Assert.NotEmpty(keys);

            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i]!;
                var boundsProp = k.GetType().GetProperty("Bounds");
                var bounds = (Rectangle)boundsProp!.GetValue(k)!;

                Assert.True(bounds.Width > 0, $"Key {i} has non-positive width: {bounds.Width}");
                Assert.True(bounds.Height > 0, $"Key {i} has non-positive height: {bounds.Height}");

                // Center of the key should hit-test to index i
                var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                int hitIndex = (int)InvokePrivateMethod(kb, "HitTestKey", center);
                Assert.Equal(i, hitIndex);
            }
        }

        [Fact]
        public void HitTesting_NumpadLayout_AllKeysHitTestedAtCenter()
        {
            using var kb = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                Size = new Size(320, 300)
            };

            var keys = GetPrivateField<System.Collections.IList>(kb, "_keys");
            Assert.Equal(16, keys.Count);

            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i]!;
                var boundsProp = k.GetType().GetProperty("Bounds");
                var bounds = (Rectangle)boundsProp!.GetValue(k)!;

                Assert.True(bounds.Width > 0);
                Assert.True(bounds.Height > 0);

                var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                int hitIndex = (int)InvokePrivateMethod(kb, "HitTestKey", center);
                Assert.Equal(i, hitIndex);
            }
        }

        [Fact]
        public void HitTesting_EmptySpaceAndOutOfBounds_ReturnsNegativeOne()
        {
            using var kb = new ZVirtualKeyboard { Size = new Size(680, 240) };

            // Negative coordinates
            Assert.Equal(-1, (int)InvokePrivateMethod(kb, "HitTestKey", new Point(-10, -10)));

            // Outside client bounds
            Assert.Equal(-1, (int)InvokePrivateMethod(kb, "HitTestKey", new Point(1000, 1000)));

            // Gaps between row 0 and key spacing
            // Top margin spacing is 0..3 (spacing is 4)
            Assert.Equal(-1, (int)InvokePrivateMethod(kb, "HitTestKey", new Point(2, 2)));
        }

        [Fact]
        public void Layout_Resize_RecomputesKeyBoundsDynamically()
        {
            using var kb = new ZVirtualKeyboard { Size = new Size(680, 240) };
            var keys = GetPrivateField<System.Collections.IList>(kb, "_keys");
            var key0 = keys[0]!;
            var bounds0 = (Rectangle)key0.GetType().GetProperty("Bounds")!.GetValue(key0)!;

            // Resize to larger dimensions
            kb.Size = new Size(1000, 400);

            var key0Resized = keys[0]!;
            var bounds0Resized = (Rectangle)key0Resized.GetType().GetProperty("Bounds")!.GetValue(key0Resized)!;

            Assert.True(bounds0Resized.Width > bounds0.Width, "Resized key width should increase");
            Assert.True(bounds0Resized.Height > bounds0.Height, "Resized key height should increase");
        }

        #endregion

        #region Attack Surface 6: Keystroke Execution & Target TextBox State Machine

        [Fact]
        public void Keystroke_TypingLetters_ShiftAndCapsLockStateMachine()
        {
            using var tb = new TextBox { Text = "" };
            using var kb = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.AlphaNumeric,
                TargetControl = tb,
                Size = new Size(680, 240)
            };

            var keys = GetPrivateField<System.Collections.IList>(kb, "_keys");

            object FindKey(string label)
            {
                foreach (var k in keys)
                {
                    string l = (string)k!.GetType().GetProperty("PrimaryLabel")!.GetValue(k)!;
                    if (l == label) return k;
                }
                throw new InvalidOperationException($"Key '{label}' not found");
            }

            void PressKey(string label)
            {
                var k = FindKey(label);
                InvokePrivateMethod(kb, "ProcessKeyActivation", k);
            }

            // 1. Lowercase typing
            PressKey("A");
            Assert.Equal("a", tb.Text);

            // 2. Shift mode: one uppercase letter, then automatically reverts to lowercase
            PressKey("Shift");
            PressKey("B");
            Assert.Equal("aB", tb.Text);

            PressKey("C");
            Assert.Equal("aBc", tb.Text); // Reverted to lower

            // 3. CapsLock mode: latches uppercase until toggled off
            PressKey("Caps");
            PressKey("D");
            PressKey("E");
            Assert.Equal("aBcDE", tb.Text);

            // Shift while CapsLock is on inverts case (produces lowercase)
            PressKey("Shift");
            PressKey("F");
            Assert.Equal("aBcDEf", tb.Text);

            // Turn off Caps
            PressKey("Caps");
            PressKey("G");
            Assert.Equal("aBcDEfg", tb.Text);

            // 4. Space
            PressKey("Space");
            Assert.Equal("aBcDEfg ", tb.Text);

            // 5. Backspace
            PressKey("Bksp");
            Assert.Equal("aBcDEfg", tb.Text);

            // 6. Selection replacement
            tb.SelectionStart = 1;
            tb.SelectionLength = 2; // selects "Bc"
            PressKey("Z");
            Assert.Equal("azDEfg", tb.Text);

            // 7. Clear
            PressKey("Clear");
            Assert.Equal("", tb.Text);
        }

        [Fact]
        public void Keystroke_EnterAndEscapeEvents_FiredCorrectly()
        {
            using var kb = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.AlphaNumeric,
                Size = new Size(680, 240)
            };

            var keys = GetPrivateField<System.Collections.IList>(kb, "_keys");

            object FindKey(string label)
            {
                foreach (var k in keys)
                {
                    string l = (string)k!.GetType().GetProperty("PrimaryLabel")!.GetValue(k)!;
                    if (l == label) return k;
                }
                throw new InvalidOperationException($"Key '{label}' not found");
            }

            bool enterFired = false;
            bool escapeFired = false;

            kb.EnterPressed += (s, e) => enterFired = true;
            kb.EscapePressed += (s, e) => escapeFired = true;

            InvokePrivateMethod(kb, "ProcessKeyActivation", FindKey("Enter"));
            Assert.True(enterFired);

            InvokePrivateMethod(kb, "ProcessKeyActivation", FindKey("Esc"));
            Assert.True(escapeFired);
        }

        #endregion

        #region Attack Surface 7: OnPaint Stress & Memory Leak Prevention

        [Fact]
        public void Paint_BothSkins_RendersWithoutGdiExceptions()
        {
            foreach (var skin in new[] { ZeroSkinDefaults.CleanLight, ZeroSkinDefaults.ObsidianDark })
            {
                ZeroSkinManager.ApplySkin(skin);

                using var kb = new ZVirtualKeyboard { Size = new Size(680, 240) };
                using var bmp = new Bitmap(kb.Width, kb.Height);
                using var g = Graphics.FromImage(bmp);

                var paintMethod = typeof(ZVirtualKeyboard).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(paintMethod);

                // Normal paint
                paintMethod!.Invoke(kb, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, kb.Width, kb.Height)) });

                // Hover state paint
                var hoverField = typeof(ZVirtualKeyboard).GetField("_hoveredKeyIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                hoverField!.SetValue(kb, 5);
                paintMethod.Invoke(kb, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, kb.Width, kb.Height)) });

                // Pressed state paint
                var pressedField = typeof(ZVirtualKeyboard).GetField("_pressedKeyIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                pressedField!.SetValue(kb, 5);
                paintMethod.Invoke(kb, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, kb.Width, kb.Height)) });
            }

            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
        }

        [Fact]
        public void Dispose_UnsubscribesFromThemeChanged_PreventsObjectDisposedException()
        {
            var kb = new ZVirtualKeyboard();
            kb.Dispose();

            var ex = Record.Exception(() =>
            {
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.ObsidianDark);
                ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
            });

            Assert.Null(ex);
        }

        [Fact]
        public void StressTest_100Instances_RapidThemeSwitching_NoExceptionsOrLeaks()
        {
            for (int i = 0; i < 100; i++)
            {
                using var kb = new ZVirtualKeyboard();
                ZeroTheme.ToggleTheme();
            }

            ZeroSkinManager.ApplySkin(ZeroSkinDefaults.CleanLight);
        }

        #endregion
    }
}
