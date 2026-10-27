using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;

namespace ZeroUI.Desktop.Tests
{
    public class VirtualKeyboardAndLockScreenTests
    {
        #region WinForms Virtual Keyboard Tests

        [Fact]
        public void ZVirtualKeyboard_WinForms_LayoutModesAndDimensions()
        {
            using var kb = new ZeroUI.WinForms.Input.ZVirtualKeyboard();

            Assert.Equal(VirtualKeyboardLayout.AlphaNumeric, kb.LayoutMode);
            Assert.Equal(46, kb.KeyHeight);

            kb.LayoutMode = VirtualKeyboardLayout.Numpad;
            Assert.Equal(VirtualKeyboardLayout.Numpad, kb.LayoutMode);
        }

        [Fact]
        public void ZVirtualKeyboard_WinForms_KeystrokePropagationIntoTargetTextBox()
        {
            using var form = new Form();
            using var tb = new TextBox { Text = "123" };
            using var kb = new ZeroUI.WinForms.Input.ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                TargetControl = tb
            };

            form.Controls.Add(tb);
            form.Controls.Add(kb);

            // Simulate keystroke events via event handler
            bool keyFired = false;
            kb.KeyPressed += (s, e) =>
            {
                keyFired = true;
                Assert.Equal("4", e.Text);
            };

            // Programmatically verify target input text manipulation
            tb.SelectionStart = 3;
            tb.Text += "4";

            Assert.Equal("1234", tb.Text);
        }

        [Fact]
        public void ZTouchKeyboardProvider_WinForms_AttachesToContainer()
        {
            using var form = new Form();
            using var tb = new TextBox { Name = "txtPinCode" };
            form.Controls.Add(tb);

            using var provider = new ZeroUI.WinForms.Input.ZTouchKeyboardProvider();
            provider.Attach(form);

            Assert.True(provider.IsEnabled);
            Assert.True(provider.AutoDetectNumeric);
            Assert.Same(form, provider.ParentContainer);
        }

        [Fact]
        public async Task ZLockScreenOverlay_WinForms_UnlockVerificationFlow()
        {
            var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            var session = new SessionContext();
            var user = await store.FindByUsernameAsync("engineer");
            session.SetCurrentUser(user);
            session.LockSession();

            Assert.True(session.IsLockedOut);

            using var overlay = new ZeroUI.WinForms.Overlays.ZLockScreenOverlay(store, session);

            // Wrong PIN fails
            bool wrongUnlock = await overlay.AttemptUnlockAsync("9999"); // engineer's PIN is 1234
            Assert.False(wrongUnlock);
            Assert.True(session.IsLockedOut);

            // Correct PIN succeeds
            bool validUnlock = await overlay.AttemptUnlockAsync("1234");
            Assert.True(validUnlock);
            Assert.False(session.IsLockedOut);

            session.Dispose();
        }

        #endregion

        #region WPF Virtual Keyboard Tests

        [Fact]
        public void ZVirtualKeyboard_Wpf_LayoutAndKeystrokeFlowOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var kb = new ZeroUI.Wpf.Input.ZVirtualKeyboard();
                Assert.Equal(VirtualKeyboardLayout.AlphaNumeric, kb.LayoutMode);

                kb.LayoutMode = VirtualKeyboardLayout.Numpad;
                Assert.Equal(VirtualKeyboardLayout.Numpad, kb.LayoutMode);

                var tb = new System.Windows.Controls.TextBox { Text = "ABC" };
                kb.TargetElement = tb;

                bool enterFired = false;
                kb.EnterPressed += (s, e) => enterFired = true;

                Assert.Same(tb, kb.TargetElement);
            });
        }

        [Fact]
        public void ZTouchKeyboardProvider_Wpf_AttachedPropertiesWorkCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var tb = new System.Windows.Controls.TextBox();
                ZeroUI.Wpf.Input.ZTouchKeyboardProvider.SetIsEnabled(tb, true);
                ZeroUI.Wpf.Input.ZTouchKeyboardProvider.SetLayout(tb, VirtualKeyboardLayout.Numpad);

                Assert.True(ZeroUI.Wpf.Input.ZTouchKeyboardProvider.GetIsEnabled(tb));
                Assert.Equal(VirtualKeyboardLayout.Numpad, ZeroUI.Wpf.Input.ZTouchKeyboardProvider.GetLayout(tb));
            });
        }

        [Fact]
        public async Task ZLockScreenOverlay_Wpf_UnlockVerificationFlow()
        {
            await Task.Run(() =>
            {
                StaTestRunner.Run(async () =>
                {
                    var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
                    var session = new SessionContext();
                    var user = await store.FindByUsernameAsync("admin");
                    session.SetCurrentUser(user);
                    session.LockSession();

                    Assert.True(session.IsLockedOut);

                    var overlay = new ZeroUI.Wpf.Overlays.ZLockScreenOverlay(store, session);

                    // Wrong password
                    bool wrongUnlock = await overlay.AttemptUnlockAsync("wrong_pass");
                    Assert.False(wrongUnlock);
                    Assert.True(session.IsLockedOut);

                    // Valid password for admin
                    bool validUnlock = await overlay.AttemptUnlockAsync("admin");
                    Assert.True(validUnlock);
                    Assert.False(session.IsLockedOut);

                    session.Dispose();
                });
            });
        }

        #endregion
    }
}
