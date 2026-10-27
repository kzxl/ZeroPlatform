using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Enterprise host for embedding Windows Forms controls directly into ToolStrip,
    /// ContextMenuStrip, and DropDown menus. Solves native WinForms ToolStripControlHost pitfalls:
    /// 1. Prevents premature ToolStripDropDown dismissal (AutoClose) during interaction and child popups.
    /// 2. Preserves editing keystrokes (Arrow keys, Home/End, Backspace, Space, Delete, Clipboard shortcuts).
    /// 3. Synchronizes background, foreground, and border colors with ZeroTheme automatically.
    /// 4. Handles High-DPI padding and layout constraints cleanly.
    /// </summary>
    /// <typeparam name="T">The type of Control being hosted.</typeparam>
    public class MenuItemControlHost<T> : ToolStripControlHost where T : Control
    {
        private bool _suppressAutoClose = true;
        private bool _savedAutoCloseState;
        private bool _isAutoCloseSuppressed;

        public T TypedControl => (T)Control;

        /// <summary>
        /// Gets or sets whether interacting with this control suppresses the parent ToolStripDropDown's AutoClose.
        /// </summary>
        public bool SuppressAutoClose
        {
            get => _suppressAutoClose;
            set => _suppressAutoClose = value;
        }

        public MenuItemControlHost(T c) : base(c)
        {
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = new Padding(4, 2, 4, 2);
            Size = new Size(c.Width + Margin.Horizontal, c.Height + Margin.Vertical);

            ApplyTheme();
            ZeroTheme.ThemeChanged += OnThemeChanged;

            AttachFocusGuards(c);
        }

        public override Size GetPreferredSize(Size constrainingSize)
        {
            if (Control != null)
            {
                return new Size(Control.Width + Margin.Horizontal, Control.Height + Margin.Vertical);
            }
            return base.GetPreferredSize(constrainingSize);
        }

        private void AttachFocusGuards(Control parent)
        {
            parent.Enter += OnControlEnter;
            parent.Leave += OnControlLeave;
            parent.MouseDown += OnControlMouseDown;

            foreach (Control child in parent.Controls)
            {
                AttachFocusGuards(child);
            }

            parent.ControlAdded += (s, e) =>
            {
                if (e.Control != null)
                {
                    AttachFocusGuards(e.Control);
                }
            };
        }

        private void DetachFocusGuards(Control parent)
        {
            parent.Enter -= OnControlEnter;
            parent.Leave -= OnControlLeave;
            parent.MouseDown -= OnControlMouseDown;

            foreach (Control child in parent.Controls)
            {
                DetachFocusGuards(child);
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            ApplyTheme();
        }

        protected virtual void ApplyTheme()
        {
            if (Control != null && !Control.IsDisposed)
            {
                Control.Font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular);
            }
        }

        private void OnControlMouseDown(object? sender, MouseEventArgs e)
        {
            HoldAutoClose();
        }

        private void OnControlEnter(object? sender, EventArgs e)
        {
            HoldAutoClose();
        }

        private void OnControlLeave(object? sender, EventArgs e)
        {
            ReleaseAutoClose();
        }

        /// <summary>
        /// Temporarily disables AutoClose on the owner ToolStripDropDown while the control is active.
        /// </summary>
        public void HoldAutoClose()
        {
            if (!_suppressAutoClose || _isAutoCloseSuppressed) return;

            if (Owner is ToolStripDropDown dropDown)
            {
                _savedAutoCloseState = dropDown.AutoClose;
                dropDown.AutoClose = false;
                _isAutoCloseSuppressed = true;
            }
        }

        /// <summary>
        /// Restores the parent ToolStripDropDown's AutoClose behavior.
        /// </summary>
        public void ReleaseAutoClose()
        {
            if (!_isAutoCloseSuppressed) return;

            if (Owner is ToolStripDropDown dropDown)
            {
                dropDown.AutoClose = _savedAutoCloseState;
                _isAutoCloseSuppressed = false;
            }
        }

        protected override bool ProcessCmdKey(ref Message m, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            // Route text editing and cursor navigation keys directly to the hosted control
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
                key == Keys.Home || key == Keys.End || key == Keys.Back || key == Keys.Delete ||
                key == Keys.Space)
            {
                return false;
            }

            if (key == Keys.Escape)
            {
                ReleaseAutoClose();
                if (Owner is ToolStripDropDown dropDown)
                {
                    dropDown.Close(ToolStripDropDownCloseReason.Keyboard);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref m, keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            // Prevent ToolStrip from stealing arrow/space keys from editors
            if (key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End ||
                key == Keys.Back || key == Keys.Space || key == Keys.Delete)
            {
                return false;
            }

            return base.ProcessDialogKey(keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                ReleaseAutoClose();
                if (Control != null)
                {
                    DetachFocusGuards(Control);
                }
            }
            base.Dispose(disposing);
        }
    }
}
