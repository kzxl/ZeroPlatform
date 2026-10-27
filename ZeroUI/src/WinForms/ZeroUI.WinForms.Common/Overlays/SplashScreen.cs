using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Specifies the visual template layout style for <see cref="SplashScreen"/>.
    /// </summary>
    public enum SplashStyle
    {
        /// <summary>Modern rounded card with logo badge, title, subtitle, progress bar, and status.</summary>
        Standard,
        /// <summary>Hero banner layout displaying a branded header image with smooth gradient overlay.</summary>
        HeroBanner,
        /// <summary>Enterprise multi-step checklist display tracking initialization tasks in real time.</summary>
        DetailedSteps,
        /// <summary>Compact minimalistic wait dialog layout.</summary>
        CompactWait
    }

    /// <summary>
    /// Execution status for an initialization step in <see cref="SplashStyle.DetailedSteps"/>.
    /// </summary>
    public enum StepStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Represents an initialization step item displayed in <see cref="SplashStyle.DetailedSteps"/>.
    /// </summary>
    public class SplashStepItem
    {
        public string Text { get; set; }
        public StepStatus Status { get; set; }

        public SplashStepItem(string text, StepStatus status = StepStatus.Pending)
        {
            Text = text ?? string.Empty;
            Status = status;
        }
    }

    /// <summary>
    /// Represents progress update data for <see cref="SplashScreen"/>.
    /// </summary>
    public readonly struct SplashProgress
    {
        public string Status { get; }
        public int? ProgressPercentage { get; }
        public string? SubStatus { get; }

        public SplashProgress(string status, int? progressPercentage = null, string? subStatus = null)
        {
            Status = status ?? string.Empty;
            ProgressPercentage = progressPercentage;
            SubStatus = subStatus;
        }

        public static implicit operator SplashProgress(string status) => new SplashProgress(status);
        public static implicit operator SplashProgress((string status, int progress) tuple) => new SplashProgress(tuple.status, tuple.progress);
    }

    /// <summary>
    /// Progress reporter adapter for <see cref="SplashScreen"/> supporting multiple types.
    /// </summary>
    public sealed class SplashProgressReporter : IProgress<SplashProgress>, IProgress<(string, int?)>, IProgress<string>
    {
        public void Report(SplashProgress value) => SplashScreen.SetStatus(value.Status, value.ProgressPercentage);
        public void Report((string, int?) value) => SplashScreen.SetStatus(value.Item1, value.Item2);
        public void Report(string value) => SplashScreen.SetStatus(value);
    }

    /// <summary>
    /// Configuration options for <see cref="SplashScreen"/>.
    /// </summary>
    public class SplashScreenOptions
    {
        public SplashStyle Style { get; set; } = SplashStyle.Standard;
        public string AppTitle { get; set; } = "ZeroUI Application";
        public string Subtitle { get; set; } = "High-Performance Enterprise Suite";
        public string InitialStatus { get; set; } = "Initializing...";

        // Branding
        public Image? LogoImage { get; set; }
        public Icon? LogoIcon { get; set; }
        public string LogoGlyph { get; set; } = "⚡";
        public Image? BannerImage { get; set; }
        public string? FooterText { get; set; } = "ZeroUI Core • Zero-Allocation Engine";
        public bool ShowFooter { get; set; } = true;
        public bool ShowProgressBar { get; set; } = true;
        public bool ShowPercentage { get; set; } = true;

        // Steps Checklist
        public List<SplashStepItem> Steps { get; } = new List<SplashStepItem>();

        // Sizing & Positioning
        public int Width { get; set; } = 520;
        public int Height { get; set; } = 280;
        public Point? TargetLocation { get; set; }
        public Screen? TargetScreen { get; set; }

        // Aesthetics & Motion
        public bool EnableFadeTransitions { get; set; } = true;
        public int FadeDurationMs { get; set; } = 150;
        public bool EnableDropShadow { get; set; } = true;
        public ZeroThemePalette? CustomPalette { get; set; }

        // Cancellation
        public bool EnableCancellation { get; set; } = false;
        public string CancelButtonText { get; set; } = "Cancel";
        public Action? OnCancelRequested { get; set; }
    }

    /// <summary>
    /// Fluent builder for <see cref="SplashScreenOptions"/>.
    /// </summary>
    public sealed class SplashScreenBuilder
    {
        private readonly SplashScreenOptions _options = new SplashScreenOptions();

        public SplashScreenBuilder WithStyle(SplashStyle style)
        {
            _options.Style = style;
            if (style == SplashStyle.CompactWait && _options.Width == 520 && _options.Height == 280)
            {
                _options.Width = 380;
                _options.Height = 130;
            }
            else if (style == SplashStyle.DetailedSteps && _options.Width == 520 && _options.Height == 280)
            {
                _options.Width = 540;
                _options.Height = 360;
            }
            else if (style == SplashStyle.HeroBanner && _options.Width == 520 && _options.Height == 280)
            {
                _options.Width = 560;
                _options.Height = 320;
            }
            return this;
        }

        public SplashScreenBuilder WithTitle(string title) { _options.AppTitle = title; return this; }
        public SplashScreenBuilder WithSubtitle(string subtitle) { _options.Subtitle = subtitle; return this; }
        public SplashScreenBuilder WithStatus(string status) { _options.InitialStatus = status; return this; }
        public SplashScreenBuilder WithLogo(Image logo) { _options.LogoImage = logo; return this; }
        public SplashScreenBuilder WithLogo(Icon icon) { _options.LogoIcon = icon; return this; }
        public SplashScreenBuilder WithLogoGlyph(string glyph) { _options.LogoGlyph = glyph; return this; }
        public SplashScreenBuilder WithBanner(Image banner)
        {
            _options.BannerImage = banner;
            _options.Style = SplashStyle.HeroBanner;
            return this;
        }

        public SplashScreenBuilder AddStep(string stepText, StepStatus status = StepStatus.Pending)
        {
            _options.Steps.Add(new SplashStepItem(stepText, status));
            if (_options.Style == SplashStyle.Standard) _options.Style = SplashStyle.DetailedSteps;
            return this;
        }

        public SplashScreenBuilder WithFooter(string? footer)
        {
            _options.FooterText = footer;
            _options.ShowFooter = !string.IsNullOrEmpty(footer);
            return this;
        }
        public SplashScreenBuilder WithSize(int width, int height) { _options.Width = width; _options.Height = height; return this; }
        public SplashScreenBuilder WithPalette(ZeroThemePalette palette) { _options.CustomPalette = palette; return this; }
        public SplashScreenBuilder EnableFade(bool enable = true, int durationMs = 150)
        {
            _options.EnableFadeTransitions = enable;
            _options.FadeDurationMs = Math.Max(50, durationMs);
            return this;
        }
        public SplashScreenBuilder EnableDropShadow(bool enable = true) { _options.EnableDropShadow = enable; return this; }
        public SplashScreenBuilder EnableCancellation(Action onCancel, string cancelText = "Cancel")
        {
            _options.EnableCancellation = true;
            _options.CancelButtonText = cancelText;
            _options.OnCancelRequested = onCancel;
            return this;
        }
        public SplashScreenBuilder CenterOnCursor()
        {
            _options.TargetScreen = Screen.FromPoint(Cursor.Position);
            return this;
        }
        public SplashScreenBuilder CenterOnScreen(Screen screen)
        {
            _options.TargetScreen = screen;
            return this;
        }

        public SplashScreenOptions Build() => _options;
        public void Show() => SplashScreen.Show(_options);
    }

    /// <summary>
    /// Thread-safe, non-blocking enterprise Splash Screen Manager for ZeroUI.
    /// Runs on an independent background STA thread, guaranteeing smooth animation
    /// and responsive status updates while the main application initializes.
    /// </summary>
    public static class SplashScreen
    {
        private static Thread? _splashThread;
        private static SplashForm? _splashForm;
        private static readonly object _syncLock = new object();
        private static readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);

        public static bool IsShowing => _splashForm != null && !_splashForm.IsDisposed;

        /// <summary>
        /// Creates a fluent configuration builder for SplashScreen.
        /// </summary>
        public static SplashScreenBuilder Configure() => new SplashScreenBuilder();

        /// <summary>
        /// Shows the splash screen with full configuration options.
        /// </summary>
        public static void Show(SplashScreenOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            lock (_syncLock)
            {
                if (IsShowing) return;

                _readyEvent.Reset();
                _splashThread = new Thread(() =>
                {
                    _splashForm = new SplashForm(options);
                    _readyEvent.Set();
                    Application.Run(_splashForm);
                });

                _splashThread.SetApartmentState(ApartmentState.STA);
                _splashThread.IsBackground = true;
                _splashThread.Name = "ZeroSplashScreen_STA_Thread";
                _splashThread.Start();

                _readyEvent.WaitOne(3000);
            }
        }

        /// <summary>
        /// Legacy overload: Shows the splash screen with classic title, subtitle, and initial status.
        /// </summary>
        public static void Show(
            string appTitle = "ZeroUI Application",
            string subtitle = "High-Performance Enterprise Suite",
            string initialStatus = "Initializing...")
        {
            Show(new SplashScreenOptions
            {
                AppTitle = appTitle,
                Subtitle = subtitle,
                InitialStatus = initialStatus
            });
        }

        /// <summary>
        /// Updates the current status and optional progress percentage (0 - 100).
        /// If progressPercentage is null, renders an indeterminate shimmering bar.
        /// </summary>
        public static void SetStatus(string status, int? progressPercentage = null)
        {
            if (_splashForm != null && !_splashForm.IsDisposed)
            {
                try
                {
                    if (_splashForm.InvokeRequired)
                    {
                        _splashForm.BeginInvoke(new Action(() => _splashForm.UpdateStatus(status, progressPercentage)));
                    }
                    else
                    {
                        _splashForm.UpdateStatus(status, progressPercentage);
                    }
                }
                catch
                {
                    // Ignore cross-thread or dispose race conditions during closing
                }
            }
        }

        /// <summary>
        /// Updates the execution status and optional message of a specific checklist step.
        /// </summary>
        public static void SetStepStatus(int stepIndex, StepStatus status, string? newText = null)
        {
            if (_splashForm != null && !_splashForm.IsDisposed)
            {
                try
                {
                    if (_splashForm.InvokeRequired)
                    {
                        _splashForm.BeginInvoke(new Action(() => _splashForm.UpdateStep(stepIndex, status, newText)));
                    }
                    else
                    {
                        _splashForm.UpdateStep(stepIndex, status, newText);
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Dynamically appends a new initialization step to the live checklist.
        /// </summary>
        public static void AddStep(string text, StepStatus status = StepStatus.Running)
        {
            if (_splashForm != null && !_splashForm.IsDisposed)
            {
                try
                {
                    if (_splashForm.InvokeRequired)
                    {
                        _splashForm.BeginInvoke(new Action(() => _splashForm.AppendStep(text, status)));
                    }
                    else
                    {
                        _splashForm.AppendStep(text, status);
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Closes the active splash screen with an optional exit delay or fade-out animation.
        /// </summary>
        public static void Close(int delayMs = 150)
        {
            lock (_syncLock)
            {
                if (_splashForm != null && !_splashForm.IsDisposed)
                {
                    try
                    {
                        _splashForm.BeginInvoke(new Action(() => _splashForm.CloseSmooth(delayMs)));
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                _splashThread = null;
            }
        }

        /// <summary>
        /// Displays an in-app wait form over the parent window (replaces DevExpress WaitForm).
        /// </summary>
        public static WaitOverlayHandle ShowWaitForm(
            IWin32Window? owner = null,
            string caption = "Please wait...",
            string description = "Processing operation...",
            bool showDimmedBackdrop = true,
            bool canCancel = false,
            Action? onCancel = null)
        {
            return WaitOverlay.Show(owner, caption, description, showDimmedBackdrop, canCancel, onCancel);
        }

        /// <summary>
        /// Closes any active in-app wait form.
        /// </summary>
        public static void CloseWaitForm() => WaitOverlay.Close();

        /// <summary>
        /// Returns a thread-safe <see cref="IProgress{SplashProgress}"/> reporter instance.
        /// </summary>
        public static IProgress<SplashProgress> GetProgressReporter() => new SplashProgressReporter();

        /// <summary>
        /// Executes an asynchronous startup action while orchestrating the splash screen lifecycle,
        /// automatic progress reporting, cancellation token handling, and smooth fade-out upon completion.
        /// </summary>
        public static async Task RunAsync(
            Func<IProgress<SplashProgress>, CancellationToken, Task> startupAction,
            Action<SplashScreenBuilder>? configure = null,
            CancellationToken cancellationToken = default)
        {
            if (startupAction == null) throw new ArgumentNullException(nameof(startupAction));

            var builder = Configure();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Wire cancellation callback to CTS
            builder.EnableCancellation(() => cts.Cancel());
            configure?.Invoke(builder);

            var options = builder.Build();
            Show(options);

            var reporter = new SplashProgressReporter();
            try
            {
                await startupAction(reporter, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                Close(options.EnableFadeTransitions ? options.FadeDurationMs : 150);
                cts.Dispose();
            }
        }

        /// <summary>
        /// Internal Form running on the dedicated STA thread.
        /// </summary>
        private class SplashForm : Form
        {
            private readonly SplashScreenOptions _options;
            private string _status;
            private int? _progress;
            private readonly List<SplashStepItem> _steps;
            private readonly System.Windows.Forms.Timer _animationTimer;
            private readonly System.Windows.Forms.Timer? _fadeTimer;
            private float _shimmerPhase = 0f;
            private float _dpiScale = 1.0f;
            private bool _isClosing = false;
            private Rectangle _cancelRect = Rectangle.Empty;
            private bool _isCancelHovered = false;

            public SplashForm(SplashScreenOptions options)
            {
                _options = options;
                _status = options.InitialStatus;
                _steps = new List<SplashStepItem>(options.Steps);

                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;

                // DPI calculation
                using (var tempG = CreateGraphics())
                {
                    _dpiScale = Math.Max(1.0f, tempG.DpiX / 96.0f);
                }

                int scaledWidth = (int)Math.Round(_options.Width * _dpiScale);
                int scaledHeight = (int)Math.Round(_options.Height * _dpiScale);
                Size = new Size(scaledWidth, scaledHeight);

                // Position calculation
                if (_options.TargetLocation.HasValue)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = _options.TargetLocation.Value;
                }
                else if (_options.TargetScreen != null)
                {
                    StartPosition = FormStartPosition.Manual;
                    var bounds = _options.TargetScreen.WorkingArea;
                    Location = new Point(
                        bounds.Left + (bounds.Width - scaledWidth) / 2,
                        bounds.Top + (bounds.Height - scaledHeight) / 2);
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

                DoubleBuffered = true;

                if (_options.EnableFadeTransitions)
                {
                    Opacity = 0.0;
                    _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
                    _fadeTimer.Tick += FadeTimer_Tick;
                }
                else
                {
                    Opacity = 1.0;
                }

                _animationTimer = new System.Windows.Forms.Timer { Interval = 25 }; // ~40 FPS smooth shimmer
                _animationTimer.Tick += (s, e) =>
                {
                    _shimmerPhase = (_shimmerPhase + 0.05f) % 1.0f;
                    Invalidate();
                };
                _animationTimer.Start();
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    const int CS_DROPSHADOW = 0x20000;
                    var cp = base.CreateParams;
                    if (_options.EnableDropShadow)
                    {
                        cp.ClassStyle |= CS_DROPSHADOW;
                    }
                    return cp;
                }
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                if (_options.EnableFadeTransitions && _fadeTimer != null)
                {
                    _fadeTimer.Start();
                }
            }

            private void FadeTimer_Tick(object? sender, EventArgs e)
            {
                if (!_isClosing)
                {
                    // Fade In
                    double step = 16.0 / Math.Max(50, _options.FadeDurationMs);
                    if (Opacity < 1.0)
                    {
                        Opacity = Math.Min(1.0, Opacity + step);
                    }
                    else
                    {
                        _fadeTimer?.Stop();
                    }
                }
                else
                {
                    // Fade Out
                    double step = 16.0 / Math.Max(50, _options.FadeDurationMs);
                    if (Opacity > 0.05)
                    {
                        Opacity = Math.Max(0.0, Opacity - step);
                    }
                    else
                    {
                        _fadeTimer?.Stop();
                        Close();
                        Dispose();
                    }
                }
            }

            public void UpdateStatus(string status, int? progress)
            {
                _status = status;
                _progress = progress.HasValue ? Math.Max(0, Math.Min(100, progress.Value)) : null;
                Invalidate();
            }

            public void UpdateStep(int stepIndex, StepStatus status, string? newText)
            {
                if (stepIndex >= 0 && stepIndex < _steps.Count)
                {
                    _steps[stepIndex].Status = status;
                    if (!string.IsNullOrEmpty(newText))
                    {
                        _steps[stepIndex].Text = newText!;
                    }
                    Invalidate();
                }
            }

            public void AppendStep(string text, StepStatus status)
            {
                _steps.Add(new SplashStepItem(text, status));
                Invalidate();
            }

            public void CloseSmooth(int delayMs)
            {
                if (_isClosing) return;

                if (delayMs > 0 && !_options.EnableFadeTransitions)
                {
                    Thread.Sleep(delayMs);
                }

                if (_options.EnableFadeTransitions && _fadeTimer != null)
                {
                    _isClosing = true;
                    _fadeTimer.Start();
                }
                else
                {
                    Close();
                    Dispose();
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_options.EnableCancellation && !_cancelRect.IsEmpty)
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
                if (_options.EnableCancellation && !_cancelRect.IsEmpty && _cancelRect.Contains(e.Location))
                {
                    _options.OnCancelRequested?.Invoke();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var palette = _options.CustomPalette ?? ZeroTheme.Palette;
                Color cardBg = palette.Surface;

                int cornerRadius = (int)Math.Round(12 * _dpiScale);

                // 1. Base Rounded Card Background
                using (var path = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), cornerRadius))
                {
                    using (var brush = new SolidBrush(cardBg))
                    {
                        g.FillPath(brush, path);
                    }

                    // Hero Banner Background Render
                    if (_options.Style == SplashStyle.HeroBanner && _options.BannerImage != null)
                    {
                        g.SetClip(path);
                        int bannerH = (int)Math.Round(140 * _dpiScale);
                        Rectangle bannerRect = new Rectangle(0, 0, Width, bannerH);
                        g.DrawImage(_options.BannerImage, bannerRect);

                        // Gradient fade down to cardBg
                        using (var fadeBrush = new LinearGradientBrush(
                            new Point(0, bannerH - 40),
                            new Point(0, bannerH),
                            Color.FromArgb(0, cardBg),
                            cardBg))
                        {
                            g.FillRectangle(fadeBrush, 0, bannerH - 40, Width, 40);
                        }
                        g.ResetClip();
                    }

                    using (var pen = new Pen(palette.Border, 1.5f))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                // 2. Decorative Top Accent Bar (only when no hero banner)
                if (_options.Style != SplashStyle.HeroBanner)
                {
                    int accentHeight = (int)Math.Round(4 * _dpiScale);
                    using (var accentBrush = new LinearGradientBrush(new Point(cornerRadius, 0), new Point(Width - cornerRadius, 0), palette.Primary, palette.PrimaryHover))
                    {
                        g.FillRectangle(accentBrush, cornerRadius, 0, Width - (cornerRadius * 2), accentHeight);
                    }
                }

                int padX = (int)Math.Round(32 * _dpiScale);
                int padY = _options.Style == SplashStyle.HeroBanner ? (int)Math.Round(110 * _dpiScale) : (int)Math.Round(36 * _dpiScale);

                // 3. Logo / Badge / Icon
                int logoSize = (int)Math.Round(44 * _dpiScale);
                Rectangle logoRect = new Rectangle(padX, padY, logoSize, logoSize);

                if (_options.LogoImage != null)
                {
                    using (var logoPath = ZeroUIConfig.CreateRoundedRectangle(logoRect, (int)Math.Round(10 * _dpiScale)))
                    {
                        g.SetClip(logoPath);
                        g.DrawImage(_options.LogoImage, logoRect);
                        g.ResetClip();
                    }
                }
                else if (_options.LogoIcon != null)
                {
                    g.DrawIcon(_options.LogoIcon, logoRect);
                }
                else
                {
                    using (var logoPath = ZeroUIConfig.CreateRoundedRectangle(logoRect, (int)Math.Round(10 * _dpiScale)))
                    {
                        using var brush = new SolidBrush(palette.Primary);
                        g.FillPath(brush, logoPath);
                        using var glyphFont = new Font("Segoe UI", 18f * _dpiScale, FontStyle.Bold);
                        TextRenderer.DrawText(g, _options.LogoGlyph, glyphFont, logoRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }

                // 4. App Title & Subtitle
                int textLeft = logoRect.Right + (int)Math.Round(14 * _dpiScale);
                int textWidth = Width - textLeft - padX;

                using (var titleFont = new Font("Segoe UI", 15f * _dpiScale, FontStyle.Bold))
                {
                    Rectangle titleRect = new Rectangle(textLeft, padY, textWidth, (int)Math.Round(26 * _dpiScale));
                    TextRenderer.DrawText(g, _options.AppTitle, titleFont, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                using (var subFont = new Font("Segoe UI", 9.25f * _dpiScale, FontStyle.Regular))
                {
                    Rectangle subRect = new Rectangle(textLeft, padY + (int)Math.Round(26 * _dpiScale), textWidth, (int)Math.Round(18 * _dpiScale));
                    TextRenderer.DrawText(g, _options.Subtitle, subFont, subRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // 5. Steps Checklist (if DetailedSteps style)
                if (_options.Style == SplashStyle.DetailedSteps && _steps.Count > 0)
                {
                    int stepsStartY = padY + logoSize + (int)Math.Round(14 * _dpiScale);
                    int stepLineH = (int)Math.Round(22 * _dpiScale);
                    int maxVisibleSteps = 4;
                    int startIdx = Math.Max(0, _steps.Count - maxVisibleSteps);

                    using (var stepFont = new Font("Segoe UI", 8.5f * _dpiScale, FontStyle.Regular))
                    {
                        for (int i = startIdx; i < _steps.Count; i++)
                        {
                            var step = _steps[i];
                            int curY = stepsStartY + ((i - startIdx) * stepLineH);
                            Rectangle iconRect = new Rectangle(padX + 2, curY + 3, (int)Math.Round(14 * _dpiScale), (int)Math.Round(14 * _dpiScale));
                            Rectangle textRect = new Rectangle(iconRect.Right + 8, curY, Width - iconRect.Right - padX - 8, stepLineH);

                            switch (step.Status)
                            {
                                case StepStatus.Completed:
                                    using (var b = new SolidBrush(palette.Success))
                                        g.FillEllipse(b, iconRect);
                                    TextRenderer.DrawText(g, "✓", stepFont, iconRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                                    break;
                                case StepStatus.Running:
                                    using (var p = new Pen(palette.Primary, 2f))
                                        g.DrawArc(p, iconRect, _shimmerPhase * 360f, 240f);
                                    break;
                                case StepStatus.Failed:
                                    using (var b = new SolidBrush(palette.Danger))
                                        g.FillEllipse(b, iconRect);
                                    TextRenderer.DrawText(g, "✕", stepFont, iconRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                                    break;
                                default: // Pending
                                    using (var p = new Pen(palette.Border, 1.5f))
                                        g.DrawEllipse(p, iconRect);
                                    break;
                            }

                            Color stepTextColor = step.Status == StepStatus.Running ? palette.TextPrimary : palette.TextSecondary;
                            TextRenderer.DrawText(g, step.Text, stepFont, textRect, stepTextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        }
                    }
                }

                // 6. Progress Bar
                int barY = Height - (int)Math.Round(76 * _dpiScale);
                int barH = (int)Math.Round(5 * _dpiScale);
                Rectangle barRect = new Rectangle(padX, barY, Width - (padX * 2), barH);

                if (_options.ShowProgressBar)
                {
                    using (var barPath = ZeroUIConfig.CreateRoundedRectangle(barRect, (int)Math.Round(2.5f * _dpiScale)))
                    {
                        using var trackBrush = new SolidBrush(palette.Background);
                        g.FillPath(trackBrush, barPath);

                        if (_progress.HasValue)
                        {
                            int fillW = (int)((float)_progress.Value / 100f * barRect.Width);
                            if (fillW > 0)
                            {
                                Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, fillW, barRect.Height);
                                using var fillPath = ZeroUIConfig.CreateRoundedRectangle(fillRect, (int)Math.Round(2.5f * _dpiScale));
                                using var fillBrush = new SolidBrush(palette.Primary);
                                g.FillPath(fillBrush, fillPath);
                            }
                        }
                        else
                        {
                            int shimmerW = (int)Math.Round(120 * _dpiScale);
                            int shimmerX = barRect.X + (int)(_shimmerPhase * (barRect.Width + shimmerW)) - shimmerW;
                            Rectangle shimmerRect = Rectangle.Intersect(barRect, new Rectangle(shimmerX, barRect.Y, shimmerW, barRect.Height));
                            if (!shimmerRect.IsEmpty)
                            {
                                using var shimmerPath = ZeroUIConfig.CreateRoundedRectangle(shimmerRect, (int)Math.Round(2.5f * _dpiScale));
                                using var shimmerBrush = new SolidBrush(palette.Primary);
                                g.FillPath(shimmerBrush, shimmerPath);
                            }
                        }
                    }
                }

                // 7. Status Text & Percentage
                int statusY = barY + barH + (int)Math.Round(8 * _dpiScale);
                using (var statusFont = new Font("Segoe UI", 8.75f * _dpiScale, FontStyle.Regular))
                {
                    int pctWidth = _options.ShowPercentage && _progress.HasValue ? (int)Math.Round(50 * _dpiScale) : 0;
                    int statusWidth = Width - (padX * 2) - pctWidth;

                    Rectangle statusRect = new Rectangle(padX, statusY, statusWidth, (int)Math.Round(20 * _dpiScale));
                    TextRenderer.DrawText(g, _status, statusFont, statusRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    if (_options.ShowPercentage && _progress.HasValue)
                    {
                        Rectangle pctRect = new Rectangle(Width - padX - pctWidth, statusY, pctWidth, (int)Math.Round(20 * _dpiScale));
                        TextRenderer.DrawText(g, $"{_progress.Value}%", statusFont, pctRect, palette.TextPrimary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    }
                }

                // 8. Footer Version / Copyright & Optional Cancel Button
                int footerY = Height - (int)Math.Round(30 * _dpiScale);
                if (_options.ShowFooter && !string.IsNullOrEmpty(_options.FooterText))
                {
                    using var footFont = new Font("Segoe UI", 7.25f * _dpiScale, FontStyle.Regular);
                    int cancelAlloc = _options.EnableCancellation ? (int)Math.Round(80 * _dpiScale) : 0;
                    Rectangle footRect = new Rectangle(padX, footerY, Width - (padX * 2) - cancelAlloc, (int)Math.Round(18 * _dpiScale));
                    TextRenderer.DrawText(g, _options.FooterText, footFont, footRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // 9. Cancellation Button (if enabled)
                if (_options.EnableCancellation)
                {
                    int btnW = (int)Math.Round(64 * _dpiScale);
                    int btnH = (int)Math.Round(20 * _dpiScale);
                    _cancelRect = new Rectangle(Width - padX - btnW, footerY - (int)Math.Round(1 * _dpiScale), btnW, btnH);

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

                    using var btnFont = new Font("Segoe UI", 7.75f * _dpiScale, FontStyle.Regular);
                    Color btnTextColor = _isCancelHovered ? palette.Danger : palette.TextSecondary;
                    TextRenderer.DrawText(g, _options.CancelButtonText, btnFont, _cancelRect, btnTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _animationTimer.Stop();
                    _animationTimer.Dispose();
                    _fadeTimer?.Stop();
                    _fadeTimer?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="SplashScreen"/>.
    /// </summary>
    [Obsolete("Use SplashScreen instead.")]
    public static class ZeroSplashScreen
    {
        public static bool IsShowing => SplashScreen.IsShowing;

        public static void Show(
            string appTitle = "ZeroUI Application",
            string subtitle = "High-Performance Enterprise Suite",
            string initialStatus = "Initializing...")
            => SplashScreen.Show(appTitle, subtitle, initialStatus);

        public static void Show(SplashScreenOptions options)
            => SplashScreen.Show(options);

        public static void SetStatus(string status, int? progressPercentage = null)
            => SplashScreen.SetStatus(status, progressPercentage);

        public static void Close(int delayMs = 150)
            => SplashScreen.Close(delayMs);
    }
}

