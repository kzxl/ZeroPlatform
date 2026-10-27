using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Represents a disposable handle for an active <see cref="WaitOverlay"/> session.
    /// Disposing this handle closes the wait overlay.
    /// </summary>
    public sealed class WaitOverlayHandle : IDisposable
    {
        private bool _disposed;
        private readonly Action _closeAction;

        internal WaitOverlayHandle(Action closeAction)
        {
            _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
        }

        public void SetCaption(string caption) => WaitOverlay.SetCaption(caption);
        public void SetDescription(string description) => WaitOverlay.SetDescription(description);
        public void SetProgress(int? percentage) => WaitOverlay.SetProgress(percentage);

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
    /// Lightweight, high-performance in-app Wait Overlay &amp; WaitForm for ZeroUI.
    /// Replaces legacy WinForms and DevExpress WaitForms with a modern card design,
    /// smooth animations, optional dimmed backdrop over owner forms, and IDisposable session management.
    /// </summary>
    public static class WaitOverlay
    {
        private static Form? _activeWaitForm;
        private static Form? _activeBackdropForm;
        private static readonly object _syncLock = new object();

        public static bool IsShowing
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeWaitForm != null && !_activeWaitForm.IsDisposed;
                }
            }
        }

        /// <summary>
        /// Displays an in-app wait overlay centered over the specified owner window.
        /// Returns an <see cref="IDisposable"/> handle that automatically closes the overlay when disposed.
        /// </summary>
        public static WaitOverlayHandle Show(
            IWin32Window? owner = null,
            string caption = "Please wait...",
            string description = "Processing operation...",
            bool showDimmedBackdrop = true,
            bool canCancel = false,
            Action? onCancel = null)
        {
            lock (_syncLock)
            {
                CloseInternal();

                Form? ownerForm = owner as Form ?? Form.ActiveForm;

                if (showDimmedBackdrop && ownerForm != null && ownerForm.Visible && !ownerForm.IsDisposed)
                {
                    try
                    {
                        _activeBackdropForm = CreateBackdropForm(ownerForm);
                        _activeBackdropForm.Show(ownerForm);
                    }
                    catch
                    {
                        _activeBackdropForm = null;
                    }
                }

                _activeWaitForm = new InternalWaitCardForm(ownerForm, caption, description, canCancel, onCancel);
                _activeWaitForm.TopMost = true;

                if (_activeBackdropForm != null)
                {
                    _activeWaitForm.Show(ownerForm);
                }
                else if (ownerForm != null && ownerForm.Visible && !ownerForm.IsDisposed)
                {
                    _activeWaitForm.Show(ownerForm);
                }
                else
                {
                    _activeWaitForm.Show();
                }

                try
                {
                    _activeWaitForm.BringToFront();
                }
                catch { }

                return new WaitOverlayHandle(Close);
            }
        }

        /// <summary>
        /// Updates the primary caption of the currently active wait overlay.
        /// </summary>
        public static void SetCaption(string caption)
        {
            lock (_syncLock)
            {
                if (_activeWaitForm is InternalWaitCardForm card && !card.IsDisposed)
                {
                    card.UpdateCaption(caption);
                }
            }
        }

        /// <summary>
        /// Updates the secondary description text of the currently active wait overlay.
        /// </summary>
        public static void SetDescription(string description)
        {
            lock (_syncLock)
            {
                if (_activeWaitForm is InternalWaitCardForm card && !card.IsDisposed)
                {
                    card.UpdateDescription(description);
                }
            }
        }

        /// <summary>
        /// Updates the progress percentage (0 - 100). Passing null switches back to indeterminate animation.
        /// </summary>
        public static void SetProgress(int? percentage)
        {
            lock (_syncLock)
            {
                if (_activeWaitForm is InternalWaitCardForm card && !card.IsDisposed)
                {
                    card.UpdateProgress(percentage);
                }
            }
        }

        /// <summary>
        /// Closes and disposes the active wait overlay and any associated backdrop.
        /// </summary>
        public static void Close()
        {
            lock (_syncLock)
            {
                CloseInternal();
            }
        }

        private static void CloseInternal()
        {
            var waitFormToClose = _activeWaitForm;
            _activeWaitForm = null;

            if (waitFormToClose != null && !waitFormToClose.IsDisposed)
            {
                try
                {
                    if (waitFormToClose.InvokeRequired)
                    {
                        waitFormToClose.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                waitFormToClose.Close();
                                waitFormToClose.Dispose();
                            }
                            catch { }
                        }));
                    }
                    else
                    {
                        waitFormToClose.Close();
                        waitFormToClose.Dispose();
                    }
                }
                catch { }
            }

            var backdropToClose = _activeBackdropForm;
            _activeBackdropForm = null;

            if (backdropToClose != null && !backdropToClose.IsDisposed)
            {
                try
                {
                    if (backdropToClose.InvokeRequired)
                    {
                        backdropToClose.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                backdropToClose.Close();
                                backdropToClose.Dispose();
                            }
                            catch { }
                        }));
                    }
                    else
                    {
                        backdropToClose.Close();
                        backdropToClose.Dispose();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Executes an asynchronous operation while displaying the wait overlay,
        /// automatically handling progress updates, cancellation, and cleanup.
        /// </summary>
        public static async Task RunAsync(
            IWin32Window? owner,
            Func<IProgress<SplashProgress>, CancellationToken, Task> taskAction,
            string caption = "Please wait...",
            string description = "Processing...",
            bool showDimmedBackdrop = true,
            bool canCancel = false,
            CancellationToken cancellationToken = default)
        {
            if (taskAction == null) throw new ArgumentNullException(nameof(taskAction));

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using (Show(owner, caption, description, showDimmedBackdrop, canCancel, () => cts.Cancel()))
            {
                var progress = new SplashProgressReporter();
                try
                {
                    await taskAction(progress, cts.Token).ConfigureAwait(true);
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        private static Form CreateBackdropForm(Form ownerForm)
        {
            var backdrop = new InternalBackdropForm(ownerForm);

            EventHandler locHandler = (s, e) =>
            {
                if (!backdrop.IsDisposed && !ownerForm.IsDisposed)
                    backdrop.Bounds = ownerForm.Bounds;
            };

            EventHandler sizeHandler = (s, e) =>
            {
                if (!backdrop.IsDisposed && !ownerForm.IsDisposed)
                    backdrop.Bounds = ownerForm.Bounds;
            };

            ownerForm.LocationChanged += locHandler;
            ownerForm.SizeChanged += sizeHandler;

            backdrop.Disposed += (s, e) =>
            {
                try
                {
                    ownerForm.LocationChanged -= locHandler;
                    ownerForm.SizeChanged -= sizeHandler;
                }
                catch { }
            };

            return backdrop;
        }

        /// <summary>
        /// Internal dimmed backdrop window covering the owner form with zero-activation overhead.
        /// </summary>
        private sealed class InternalBackdropForm : Form
        {
            public InternalBackdropForm(Form ownerForm)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                BackColor = Color.Black;
                Opacity = 0.35;
                Bounds = ownerForm.Bounds;
                TopMost = true;
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    const int WS_EX_TOPMOST = 0x00000008;
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                    return cp;
                }
            }
        }

        /// <summary>
        /// Internal UI card form rendering the sleek wait box.
        /// </summary>
        private sealed class InternalWaitCardForm : Form
        {
            private readonly Form? _ownerForm;
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

            public InternalWaitCardForm(
                Form? ownerForm,
                string caption,
                string description,
                bool canCancel,
                Action? onCancel)
            {
                _ownerForm = ownerForm;
                _caption = caption;
                _description = description;
                _canCancel = canCancel;
                _onCancel = onCancel;

                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                DoubleBuffered = true;
                TopMost = true;

                using (var g = CreateGraphics())
                {
                    _dpiScale = Math.Max(1.0f, g.DpiX / 96.0f);
                }

                int baseW = _canCancel ? 400 : 360;
                int baseH = 110;
                Size = new Size((int)Math.Round(baseW * _dpiScale), (int)Math.Round(baseH * _dpiScale));

                if (_ownerForm != null && _ownerForm.Visible && !_ownerForm.IsDisposed)
                {
                    StartPosition = FormStartPosition.Manual;
                    var center = new Point(
                        _ownerForm.Left + (_ownerForm.Width - Width) / 2,
                        _ownerForm.Top + (_ownerForm.Height - Height) / 2);
                    Location = center;
                }
                else
                {
                    StartPosition = FormStartPosition.CenterScreen;
                }

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                _animTimer = new System.Windows.Forms.Timer { Interval = 25 }; // ~40 FPS smooth spinner
                _animTimer.Tick += (s, e) =>
                {
                    _spinnerAngle = (_spinnerAngle + 12f) % 360f;
                    Invalidate();
                };
                _animTimer.Start();
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    const int CS_DROPSHADOW = 0x20000;
                    const int WS_EX_TOPMOST = 0x00000008;
                    var cp = base.CreateParams;
                    cp.ClassStyle |= CS_DROPSHADOW;
                    cp.ExStyle |= WS_EX_TOPMOST;
                    return cp;
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
                try { BringToFront(); } catch { }
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
                try { BringToFront(); } catch { }
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
                try { BringToFront(); } catch { }
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
                int cornerRadius = (int)Math.Round(10 * _dpiScale);

                // 1. Background Card & Crisp Border
                using (var path = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), cornerRadius))
                {
                    using (var brush = new SolidBrush(palette.Surface))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(palette.Border, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                // 2. Top Accent Stripe
                int accentH = (int)Math.Round(3 * _dpiScale);
                using (var brush = new LinearGradientBrush(new Point(0, 0), new Point(Width, 0), palette.Primary, palette.PrimaryHover))
                {
                    g.FillRectangle(brush, cornerRadius, 0, Width - (cornerRadius * 2), accentH);
                }

                // 3. Smooth Ring Spinner
                int spinnerSize = (int)Math.Round(38 * _dpiScale);
                int spinnerX = (int)Math.Round(24 * _dpiScale);
                int spinnerY = (Height - spinnerSize) / 2;
                Rectangle spinnerRect = new Rectangle(spinnerX, spinnerY, spinnerSize, spinnerSize);

                using (var trackPen = new Pen(palette.Background, (int)Math.Round(3.5f * _dpiScale)))
                {
                    g.DrawEllipse(trackPen, spinnerRect);
                }

                using (var spinnerPen = new Pen(palette.Primary, (int)Math.Round(3.5f * _dpiScale)))
                {
                    spinnerPen.StartCap = LineCap.Round;
                    spinnerPen.EndCap = LineCap.Round;
                    float sweep = _progress.HasValue ? (_progress.Value / 100f * 360f) : 100f;
                    g.DrawArc(spinnerPen, spinnerRect, _spinnerAngle, sweep);
                }

                // 4. Caption & Description Text
                int textLeft = spinnerRect.Right + (int)Math.Round(16 * _dpiScale);
                int textRightPad = _canCancel ? (int)Math.Round(75 * _dpiScale) : (int)Math.Round(24 * _dpiScale);
                int textWidth = Width - textLeft - textRightPad;

                using (var capFont = new Font("Segoe UI", 10.5f * _dpiScale, FontStyle.Bold))
                {
                    Rectangle capRect = new Rectangle(textLeft, (int)Math.Round(24 * _dpiScale), textWidth, (int)Math.Round(24 * _dpiScale));
                    TextRenderer.DrawText(g, _caption, capFont, capRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                using (var descFont = new Font("Segoe UI", 8.75f * _dpiScale, FontStyle.Regular))
                {
                    string descText = _progress.HasValue ? $"{_description} ({_progress.Value}%)" : _description;
                    Rectangle descRect = new Rectangle(textLeft, (int)Math.Round(50 * _dpiScale), textWidth, (int)Math.Round(20 * _dpiScale));
                    TextRenderer.DrawText(g, descText, descFont, descRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // 5. Optional Cancel Button
                if (_canCancel)
                {
                    int btnW = (int)Math.Round(58 * _dpiScale);
                    int btnH = (int)Math.Round(26 * _dpiScale);
                    _cancelRect = new Rectangle(Width - (int)Math.Round(20 * _dpiScale) - btnW, (Height - btnH) / 2, btnW, btnH);

                    using (var btnPath = ZeroUIConfig.CreateRoundedRectangle(_cancelRect, (int)Math.Round(4 * _dpiScale)))
                    {
                        Color btnBg = _isCancelHovered ? palette.Hover : palette.Background;
                        using var brush = new SolidBrush(btnBg);
                        g.FillPath(brush, btnPath);

                        using var pen = new Pen(palette.Border, 1f);
                        g.DrawPath(pen, btnPath);
                    }

                    using var btnFont = new Font("Segoe UI", 8f * _dpiScale, FontStyle.Regular);
                    Color btnColor = _isCancelHovered ? palette.Danger : palette.TextSecondary;
                    TextRenderer.DrawText(g, "Cancel", btnFont, _cancelRect, btnColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _animTimer.Stop();
                    _animTimer.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
