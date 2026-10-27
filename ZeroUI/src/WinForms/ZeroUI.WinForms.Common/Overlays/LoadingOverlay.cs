using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Represents an active disposable handle for an in-control loading overlay.
    /// Disposing this handle removes the overlay from the target control.
    /// </summary>
    public sealed class LoadingOverlayHandle : IDisposable
    {
        private bool _disposed;
        private readonly Action _closeAction;
        private readonly Control _targetControl;

        internal LoadingOverlayHandle(Control targetControl, Action closeAction)
        {
            _targetControl = targetControl ?? throw new ArgumentNullException(nameof(targetControl));
            _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
        }

        public Control TargetControl => _targetControl;

        public void SetCaption(string caption) => LoadingOverlay.SetCaption(_targetControl, caption);
        public void SetDescription(string description) => LoadingOverlay.SetDescription(_targetControl, description);
        public void SetProgress(int? percentage) => LoadingOverlay.SetProgress(_targetControl, percentage);

        public void Close() => Dispose();

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _closeAction();
            }
        }
    }

    /// <summary>
    /// Non-intrusive loading overlay manager attaching a modern dimmed backdrop,
    /// smooth ring spinner, and status labels directly over any Control, UserControl, or Form.
    /// </summary>
    public static class LoadingOverlay
    {
        private static readonly ConcurrentDictionary<Control, ControlOverlayPanel> _activeOverlays =
            new ConcurrentDictionary<Control, ControlOverlayPanel>();

        /// <summary>
        /// Displays an in-control loading overlay over the specified target control.
        /// </summary>
        public static LoadingOverlayHandle Show(
            Control targetControl,
            string caption = "Loading...",
            string description = "Please wait...",
            bool canCancel = false,
            Action? onCancel = null)
        {
            if (targetControl == null) throw new ArgumentNullException(nameof(targetControl));

            if (targetControl.InvokeRequired)
            {
                return (LoadingOverlayHandle)targetControl.Invoke(new Func<LoadingOverlayHandle>(() =>
                    Show(targetControl, caption, description, canCancel, onCancel)));
            }

            // Remove existing overlay if present
            Hide(targetControl);

            var overlay = new ControlOverlayPanel(targetControl, caption, description, canCancel, onCancel);
            _activeOverlays[targetControl] = overlay;

            targetControl.Controls.Add(overlay);
            overlay.BringToFront();

            return new LoadingOverlayHandle(targetControl, () => Hide(targetControl));
        }

        /// <summary>
        /// Hides and disposes any active loading overlay on the specified control.
        /// </summary>
        public static void Hide(Control targetControl)
        {
            if (targetControl == null) return;

            if (targetControl.InvokeRequired)
            {
                try
                {
                    targetControl.BeginInvoke(new Action(() => Hide(targetControl)));
                }
                catch
                {
                    // Control already disposed or closing
                }
                return;
            }

            if (_activeOverlays.TryRemove(targetControl, out var overlay))
            {
                overlay.Parent?.Controls.Remove(overlay);
                overlay.Dispose();
            }
        }

        /// <summary>
        /// Updates the primary caption of the active overlay on the specified control.
        /// </summary>
        public static void SetCaption(Control targetControl, string caption)
        {
            if (targetControl != null && _activeOverlays.TryGetValue(targetControl, out var overlay))
            {
                overlay.UpdateCaption(caption);
            }
        }

        /// <summary>
        /// Updates the secondary description of the active overlay on the specified control.
        /// </summary>
        public static void SetDescription(Control targetControl, string description)
        {
            if (targetControl != null && _activeOverlays.TryGetValue(targetControl, out var overlay))
            {
                overlay.UpdateDescription(description);
            }
        }

        /// <summary>
        /// Updates progress percentage (0 - 100) or switches back to indeterminate spinner (null).
        /// </summary>
        public static void SetProgress(Control targetControl, int? percentage)
        {
            if (targetControl != null && _activeOverlays.TryGetValue(targetControl, out var overlay))
            {
                overlay.UpdateProgress(percentage);
            }
        }

        /// <summary>
        /// Internal composite control acting as the in-control overlay canvas.
        /// </summary>
        private sealed class ControlOverlayPanel : Control
        {
            private readonly Control _target;
            private string _caption;
            private string _description;
            private int? _progress;
            private readonly bool _canCancel;
            private readonly Action? _onCancel;
            private readonly System.Windows.Forms.Timer _animTimer;
            private float _spinnerAngle = 0f;
            private float _dpiScale = 1.0f;
            private Rectangle _cancelRect = Rectangle.Empty;
            private bool _isCancelHovered = false;

            public ControlOverlayPanel(
                Control target,
                string caption,
                string description,
                bool canCancel,
                Action? onCancel)
            {
                _target = target;
                _caption = caption;
                _description = description;
                _canCancel = canCancel;
                _onCancel = onCancel;

                DoubleBuffered = true;
                Bounds = target.ClientRectangle;
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                using (var g = CreateGraphics())
                {
                    _dpiScale = Math.Max(1.0f, g.DpiX / 96.0f);
                }

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor, true);

                BackColor = Color.Transparent;

                target.SizeChanged += Target_SizeChanged;

                _animTimer = new System.Windows.Forms.Timer { Interval = 25 }; // ~40 FPS smooth spinner
                _animTimer.Tick += (s, e) =>
                {
                    _spinnerAngle = (_spinnerAngle + 12f) % 360f;
                    Invalidate();
                };
                _animTimer.Start();
            }

            private void Target_SizeChanged(object? sender, EventArgs e)
            {
                if (!IsDisposed && _target != null && !_target.IsDisposed)
                {
                    Bounds = _target.ClientRectangle;
                }
            }

            public void UpdateCaption(string caption)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateCaption(caption)));
                    return;
                }
                _caption = caption;
                Invalidate();
            }

            public void UpdateDescription(string description)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateDescription(description)));
                    return;
                }
                _description = description;
                Invalidate();
            }

            public void UpdateProgress(int? percentage)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateProgress(percentage)));
                    return;
                }
                _progress = percentage.HasValue ? Math.Max(0, Math.Min(100, percentage.Value)) : null;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_canCancel && !_cancelRect.IsEmpty)
                {
                    bool hovered = _cancelRect.Contains(e.Location);
                    if (hovered != _isCancelHovered)
                    {
                        _isCancelHovered = hovered;
                        Cursor = hovered ? Cursors.Hand : Cursors.Default;
                        Invalidate(_cancelRect);
                    }
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_isCancelHovered)
                {
                    _isCancelHovered = false;
                    Cursor = Cursors.Default;
                    if (!_cancelRect.IsEmpty) Invalidate(_cancelRect);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (_canCancel && !_cancelRect.IsEmpty && _cancelRect.Contains(e.Location))
                {
                    _onCancel?.Invoke();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = ZeroTheme.Palette;

                // 1. Semi-transparent Backdrop Tint over Target Control
                Color dimColor = Color.FromArgb(140, palette.Surface);
                using (var dimBrush = new SolidBrush(dimColor))
                {
                    g.FillRectangle(dimBrush, ClientRectangle);
                }

                // 2. Central Wait Card
                int cardW = Math.Min(Width - 40, (int)Math.Round((_canCancel ? 360 : 320) * _dpiScale));
                int cardH = (int)Math.Round(90 * _dpiScale);
                int cardX = (Width - cardW) / 2;
                int cardY = (Height - cardH) / 2;
                Rectangle cardRect = new Rectangle(cardX, cardY, cardW, cardH);

                if (cardW > 80 && cardH > 40)
                {
                    int radius = (int)Math.Round(8 * _dpiScale);
                    using (var cardPath = ZeroUIConfig.CreateRoundedRectangle(cardRect, radius))
                    {
                        using (var cardBrush = new SolidBrush(palette.Surface))
                        {
                            g.FillPath(cardBrush, cardPath);
                        }
                        using (var cardPen = new Pen(palette.Border, 1.2f))
                        {
                            g.DrawPath(cardPen, cardPath);
                        }
                    }

                    // Top Accent Line
                    int accentH = (int)Math.Round(3 * _dpiScale);
                    using (var accentBrush = new LinearGradientBrush(
                        new Point(cardX, cardY),
                        new Point(cardX + cardW, cardY),
                        palette.Primary,
                        palette.PrimaryHover))
                    {
                        g.FillRectangle(accentBrush, cardX + radius, cardY, cardW - (radius * 2), accentH);
                    }

                    // Spinner
                    int spinnerSize = (int)Math.Round(32 * _dpiScale);
                    int spinnerX = cardX + (int)Math.Round(18 * _dpiScale);
                    int spinnerY = cardY + (cardH - spinnerSize) / 2;
                    Rectangle spinnerRect = new Rectangle(spinnerX, spinnerY, spinnerSize, spinnerSize);

                    using (var trackPen = new Pen(palette.Background, (int)Math.Round(3f * _dpiScale)))
                    {
                        g.DrawEllipse(trackPen, spinnerRect);
                    }

                    using (var spinPen = new Pen(palette.Primary, (int)Math.Round(3f * _dpiScale)))
                    {
                        spinPen.StartCap = LineCap.Round;
                        spinPen.EndCap = LineCap.Round;
                        float sweep = _progress.HasValue ? (_progress.Value / 100f * 360f) : 100f;
                        g.DrawArc(spinPen, spinnerRect, _spinnerAngle, sweep);
                    }

                    // Text
                    int textLeft = spinnerRect.Right + (int)Math.Round(14 * _dpiScale);
                    int textRightPad = _canCancel ? (int)Math.Round(65 * _dpiScale) : (int)Math.Round(16 * _dpiScale);
                    int textW = cardRect.Right - textLeft - textRightPad;

                    using (var capFont = new Font("Segoe UI", 9.75f * _dpiScale, FontStyle.Bold))
                    {
                        Rectangle capRect = new Rectangle(textLeft, cardY + (int)Math.Round(18 * _dpiScale), textW, (int)Math.Round(22 * _dpiScale));
                        TextRenderer.DrawText(g, _caption, capFont, capRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }

                    using (var descFont = new Font("Segoe UI", 8.25f * _dpiScale, FontStyle.Regular))
                    {
                        string desc = _progress.HasValue ? $"{_description} ({_progress.Value}%)" : _description;
                        Rectangle descRect = new Rectangle(textLeft, cardY + (int)Math.Round(42 * _dpiScale), textW, (int)Math.Round(18 * _dpiScale));
                        TextRenderer.DrawText(g, desc, descFont, descRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }

                    // Optional Cancel Button
                    if (_canCancel)
                    {
                        int btnW = (int)Math.Round(50 * _dpiScale);
                        int btnH = (int)Math.Round(22 * _dpiScale);
                        _cancelRect = new Rectangle(cardRect.Right - (int)Math.Round(14 * _dpiScale) - btnW, cardY + (cardH - btnH) / 2, btnW, btnH);

                        using var btnPath = ZeroUIConfig.CreateRoundedRectangle(_cancelRect, (int)Math.Round(4 * _dpiScale));
                        Color btnBg = _isCancelHovered ? palette.Hover : palette.Background;
                        using (var brush = new SolidBrush(btnBg))
                        {
                            g.FillPath(brush, btnPath);
                        }
                        using (var pen = new Pen(palette.Border, 1f))
                        {
                            g.DrawPath(pen, btnPath);
                        }

                        using var btnFont = new Font("Segoe UI", 7.5f * _dpiScale, FontStyle.Regular);
                        Color btnColor = _isCancelHovered ? palette.Danger : palette.TextSecondary;
                        TextRenderer.DrawText(g, "Cancel", btnFont, _cancelRect, btnColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _target.SizeChanged -= Target_SizeChanged;
                    _animTimer.Stop();
                    _animTimer.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// Extension methods for attaching <see cref="LoadingOverlay"/> easily onto any Control.
    /// </summary>
    public static class LoadingOverlayExtensions
    {
        /// <summary>
        /// Shows an in-control loading overlay over the control with an <see cref="IDisposable"/> handle.
        /// </summary>
        public static LoadingOverlayHandle ShowOverlay(
            this Control control,
            string caption = "Loading...",
            string description = "Please wait...",
            bool canCancel = false,
            Action? onCancel = null)
        {
            return LoadingOverlay.Show(control, caption, description, canCancel, onCancel);
        }

        /// <summary>
        /// Hides and disposes any active loading overlay on the control.
        /// </summary>
        public static void HideOverlay(this Control control)
        {
            LoadingOverlay.Hide(control);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="LoadingOverlay"/>.
    /// </summary>
    [Obsolete("Use LoadingOverlay instead.")]
    public static class ZeroLoadingOverlay
    {
        public static LoadingOverlayHandle Show(
            Control targetControl,
            string caption = "Loading...",
            string description = "Please wait...",
            bool canCancel = false,
            Action? onCancel = null)
            => LoadingOverlay.Show(targetControl, caption, description, canCancel, onCancel);

        public static void Hide(Control targetControl)
            => LoadingOverlay.Hide(targetControl);
    }
}
