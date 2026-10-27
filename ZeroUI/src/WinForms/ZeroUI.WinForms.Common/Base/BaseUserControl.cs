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
    /// Enterprise foundation UserControl for ZeroUI modular feature views and dashboards.
    /// Provides dynamic theme synchronization and localized loading overlay integration.
    /// </summary>
    public class BaseUserControl : UserControl
    {
        private LoadingOverlayHandle? _activeOverlayHandle;
        private bool _hasInitialized = false;

        public BaseUserControl()
        {
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = ZeroUIConfig.DefaultFont;

            ApplyThemeColors();
            ZeroTheme.ThemeChanged += BaseUserControl_ThemeChanged;
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

            if (!_hasInitialized && !DesignMode)
            {
                _hasInitialized = true;
                _ = ExecuteInitializeAsync();
            }
        }

        private async Task ExecuteInitializeAsync()
        {
            try
            {
                await OnInitializeAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                OnInitializeException(ex);
            }
        }

        /// <summary>
        /// Override this method to perform asynchronous initialization queries for this UserControl
        /// once its Win32 handle is fully established.
        /// </summary>
        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles unhandled exceptions during <see cref="OnInitializeAsync"/>.
        /// </summary>
        protected virtual void OnInitializeException(Exception ex)
        {
            // By default, trigger overlay or report via parent Form if available
            var parentForm = FindForm();
            if (parentForm != null)
            {
                ZModalDialog.Error(parentForm, "Component Initialization Error", ex.Message);
            }
        }

        private void BaseUserControl_ThemeChanged(object? sender, EventArgs e)
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
        /// Updates standard control surface colors according to <see cref="ZeroTheme.Palette"/>.
        /// </summary>
        protected virtual void ApplyThemeColors()
        {
            var palette = ZeroTheme.Palette;
            BackColor = palette.Surface;
            ForeColor = palette.TextPrimary;
            Invalidate(true);
        }

        #region Loading Overlay Helpers

        /// <summary>
        /// Displays an in-control loading overlay localized to this UserControl.
        /// Other areas and controls outside this UserControl remain accessible.
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
        /// Hides any active loading overlay currently covering this UserControl.
        /// </summary>
        public virtual void HideLoading()
        {
            _activeOverlayHandle?.Dispose();
            _activeOverlayHandle = null;
            LoadingOverlay.Hide(this);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= BaseUserControl_ThemeChanged;
                _activeOverlayHandle?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="BaseUserControl"/>.
    /// </summary>
    [Obsolete("Use BaseUserControl instead.")]
    public class ZeroUserControl : BaseUserControl { }
}
