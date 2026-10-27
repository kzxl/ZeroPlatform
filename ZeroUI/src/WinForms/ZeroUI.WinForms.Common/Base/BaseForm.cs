using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    /// <summary>
    /// Enterprise foundation Form for ZeroUI WinForms applications.
    /// Provides automatic dynamic skin synchronization, asynchronous post-shown initialization pattern (RunAfterShown),
    /// and built-in helpers for loading overlays, toast notifications, and modal dialogs.
    /// </summary>
    public class BaseForm : Form
    {
        private LoadingOverlayHandle? _activeOverlayHandle;
        private bool _hasRunAfterShown = false;

        public BaseForm()
        {
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = ZeroUIConfig.DefaultFont;

            ApplyThemeColors();
            ZeroTheme.ThemeChanged += BaseForm_ThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (!DesignMode)
            {
                float factor = ZeroDpi.GetScaleFactor(this);
                if (Math.Abs(factor - 1.0f) > 0.001f)
                {
                    ZeroDpi.ScaleControlHierarchy(this, factor);
                }
            }
        }

        private const int WM_DPICHANGED = 0x02E0;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_DPICHANGED)
            {
                var suggested = ZeroDpi.GetSuggestedBounds(m.LParam);
                if (!suggested.IsEmpty)
                {
                    Bounds = suggested;
                }

                float factor = ZeroDpi.GetScaleFactor(this);
                ZeroDpi.ScaleControlHierarchy(this, factor);
                OnDpiChangedCore();
            }
        }

        /// <summary>
        /// Invoked when monitor display scaling changes under High-DPI Per-Monitor V2.
        /// </summary>
        protected virtual void OnDpiChangedCore()
        {
            Invalidate(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!_hasRunAfterShown)
            {
                _hasRunAfterShown = true;
                _ = ExecuteRunAfterShownAsync();
            }
        }

        private async Task ExecuteRunAfterShownAsync()
        {
            try
            {
                await RunAfterShown().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                OnRunAfterShownException(ex);
            }
        }

        /// <summary>
        /// Override this asynchronous method to execute initial data queries and background tasks
        /// immediately after the Form has fully rendered on screen, completely preventing UI freezing on startup.
        /// </summary>
        protected virtual Task RunAfterShown()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles unhandled exceptions that occur during <see cref="RunAfterShown"/>.
        /// Default implementation displays an enterprise Error modal dialog.
        /// </summary>
        protected virtual void OnRunAfterShownException(Exception ex)
        {
            ZModalDialog.Error(this, "Initialization Error", ex.Message);
        }

        private void BaseForm_ThemeChanged(object? sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
            {
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(ApplyThemeColors)); } catch { }
                }
                else
                {
                    ApplyThemeColors();
                }
            }
        }

        /// <summary>
        /// Updates standard form surface colors according to <see cref="ZeroTheme.Palette"/>.
        /// </summary>
        protected virtual void ApplyThemeColors()
        {
            var palette = ZeroTheme.Palette;
            BackColor = palette.Surface;
            ForeColor = palette.TextPrimary;
            Invalidate(true);
        }

        #region Built-in Overlay & Notification Helpers

        /// <summary>
        /// Displays an in-form loading overlay directly covering this Form.
        /// </summary>
        public virtual LoadingOverlayHandle ShowLoading(
            string caption = "Loading...",
            string description = "Please wait...",
            bool canCancel = false,
            Action? onCancel = null)
        {
            _activeOverlayHandle?.Dispose();
            _activeOverlayHandle = LoadingOverlay.Show(this, caption, description, canCancel, onCancel);
            return _activeOverlayHandle;
        }

        /// <summary>
        /// Hides any active loading overlay covering this Form.
        /// </summary>
        public virtual void HideLoading()
        {
            _activeOverlayHandle?.Dispose();
            _activeOverlayHandle = null;
            LoadingOverlay.Hide(this);
        }

        /// <summary>
        /// Displays a sleek floating Toast notification anchored to this Form or the active screen.
        /// </summary>
        public virtual void ShowToast(
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null)
        {
            ToastStackManager.Show(this, message, title, type, durationMs, onClick);
        }

        /// <summary>
        /// Displays an enterprise modal dialog over this Form.
        /// </summary>
        public virtual DialogResult ShowModal(
            string title,
            Control content,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int width = 520,
            int height = 340)
        {
            return ZModalDialog.Show(this, title, content, onOk, onCancel, okText, cancelText, showCancel, width, height);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= BaseForm_ThemeChanged;
                _activeOverlayHandle?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="BaseForm"/>.
    /// </summary>
    [Obsolete("Use BaseForm instead.")]
    public class ZeroForm : BaseForm { }
}
