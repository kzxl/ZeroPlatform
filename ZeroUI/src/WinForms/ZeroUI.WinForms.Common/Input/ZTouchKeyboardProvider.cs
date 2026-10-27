using System;
using System.ComponentModel;
using System.Windows.Forms;
using ZeroUI.Core.Input;

namespace ZeroUI.WinForms.Input
{
    /// <summary>
    /// Component that automatically provides on-screen virtual keyboard popups
    /// when user clicks or focuses any text/password/numeric input on a touch-screen HMI.
    /// </summary>
    [ToolboxItem(true)]
    public class ZTouchKeyboardProvider : Component
    {
        private Control? _parentContainer;
        private Form? _activePopup;
        private bool _isEnabled = true;
        private bool _autoDetectNumeric = true;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables or disables automatic touch keyboard popups.")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Automatically selects Numpad layout for numeric/PIN fields, and QWERTY for text.")]
        public bool AutoDetectNumeric
        {
            get => _autoDetectNumeric;
            set => _autoDetectNumeric = value;
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        [Description("The form or top-level container whose input controls will be monitored.")]
        public Control? ParentContainer
        {
            get => _parentContainer;
            set
            {
                if (_parentContainer != value)
                {
                    DetachHandlers(_parentContainer);
                    _parentContainer = value;
                    AttachHandlers(_parentContainer);
                }
            }
        }

        public ZTouchKeyboardProvider() { }

        public ZTouchKeyboardProvider(IContainer container)
        {
            container?.Add(this);
        }

        public void Attach(Control container)
        {
            ParentContainer = container;
        }

        private void AttachHandlers(Control? root)
        {
            if (root == null) return;
            HookSubtree(root);
            root.ControlAdded += OnRootControlAdded;
        }

        private void DetachHandlers(Control? root)
        {
            if (root == null) return;
            root.ControlAdded -= OnRootControlAdded;
            UnhookSubtree(root);
        }

        private void OnRootControlAdded(object? sender, ControlEventArgs e)
        {
            if (e.Control != null) HookSubtree(e.Control);
        }

        private void HookSubtree(Control c)
        {
            if (IsInputEditor(c))
            {
                c.Click += OnEditorClicked;
                c.Enter += OnEditorClicked;
            }

            for (int i = 0; i < c.Controls.Count; i++)
            {
                HookSubtree(c.Controls[i]);
            }
        }

        private void UnhookSubtree(Control c)
        {
            if (IsInputEditor(c))
            {
                c.Click -= OnEditorClicked;
                c.Enter -= OnEditorClicked;
            }

            for (int i = 0; i < c.Controls.Count; i++)
            {
                UnhookSubtree(c.Controls[i]);
            }
        }

        private static bool IsInputEditor(Control c)
        {
            if (c is TextBoxBase) return true;
            string typeName = c.GetType().Name;
            return typeName.Contains("TextEdit") ||
                   typeName.Contains("PasswordBox") ||
                   typeName.Contains("SpinEdit") ||
                   typeName.Contains("PinPad");
        }

        private void OnEditorClicked(object? sender, EventArgs e)
        {
            if (!_isEnabled || sender is not Control target) return;

            // Determine optimal layout
            var layout = VirtualKeyboardLayout.AlphaNumeric;
            if (_autoDetectNumeric)
            {
                string name = (target.Name ?? "").ToLowerInvariant();
                string typeName = target.GetType().Name.ToLowerInvariant();
                if (name.Contains("pin") || name.Contains("num") || name.Contains("qty") || name.Contains("price") ||
                    typeName.Contains("spin") || typeName.Contains("numeric"))
                {
                    layout = VirtualKeyboardLayout.Numpad;
                }
            }

            ShowFor(target, layout);
        }

        /// <summary>
        /// Explicitly triggers the virtual keyboard popup for a designated target control.
        /// </summary>
        public void ShowFor(Control target, VirtualKeyboardLayout layout = VirtualKeyboardLayout.AlphaNumeric)
        {
            if (_activePopup != null && !_activePopup.IsDisposed)
            {
                _activePopup.Close();
                _activePopup = null;
            }

            _activePopup = ZVirtualKeyboard.ShowFloatingPopup(target, layout);
        }

        /// <summary>
        /// Closes the active virtual keyboard popup if currently visible.
        /// </summary>
        public void Close()
        {
            if (_activePopup != null && !_activePopup.IsDisposed)
            {
                _activePopup.Close();
                _activePopup = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
                DetachHandlers(_parentContainer);
            }
            base.Dispose(disposing);
        }
    }
}
