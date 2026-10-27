using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;
using ZeroUI.Core.Notification;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Categorizes toast notifications by operational priority and severity.
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
        Alarm
    }

    /// <summary>
    /// Specifies the screen anchor position for stacked toast notifications.
    /// </summary>
    public enum ToastPosition
    {
        TopRight,
        BottomRight,
        TopLeft,
        BottomLeft,
        TopCenter,
        BottomCenter
    }

    /// <summary>
    /// Lightweight non-blocking floating toast notification component for ZeroUI.
    /// Supports title + message, left accent indicator, close button, pause on hover, and smooth stacking animations.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZToastNotification.bmp")]
    public class ZToastNotification : Form
    {
        private readonly Form _ownerForm;
        private readonly ToastType _type;
        private readonly string _message;
        private readonly string _title;
        private readonly Timer _stayTimer;
        private readonly Timer _fadeTimer;
        private readonly Timer _slideTimer;
        private bool _isFadingOut = false;
        private bool _isCloseHovered = false;
        private Rectangle _closeButtonRect;

        internal int TargetX { get; set; }
        internal int TargetY { get; set; }

        public string Title => _title;
        public string Message => _message;
        public ToastType ToastType => _type;
        public Action? ClickAction { get; set; }
        public ToastPosition Position { get; set; } = ToastPosition.TopRight;

        public ZToastNotification(
            Form owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition position = ToastPosition.TopRight)
        {
            _ownerForm = owner ?? throw new ArgumentNullException(nameof(owner));
            _message = message ?? string.Empty;
            _title = title ?? string.Empty;
            _type = type;
            ClickAction = onClick;
            Position = position;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Opacity = 0.0;
            Size = string.IsNullOrEmpty(_title) ? new Size(340, 56) : new Size(360, 68);

            _fadeTimer = new Timer { Interval = 16 };
            _fadeTimer.Tick += FadeTimer_Tick;

            _stayTimer = new Timer { Interval = Math.Max(1000, durationMs) };
            _stayTimer.Tick += (s, e) =>
            {
                _stayTimer.Stop();
                CloseToast();
            };

            _slideTimer = new Timer { Interval = 15 };
            _slideTimer.Tick += SlideTimer_Tick;

            Cursor = Cursors.Hand;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        internal void StartDisplay()
        {
            _fadeTimer.Start();
        }

        internal void AnimateTo(int targetX, int targetY)
        {
            TargetX = targetX;
            TargetY = targetY;
            if (!_slideTimer.Enabled)
            {
                _slideTimer.Start();
            }
        }

        private void SlideTimer_Tick(object? sender, EventArgs e)
        {
            int dx = TargetX - Location.X;
            int dy = TargetY - Location.Y;

            if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2)
            {
                Location = new Point(TargetX, TargetY);
                _slideTimer.Stop();
                return;
            }

            int stepX = dx / 3;
            int stepY = dy / 3;
            if (stepX == 0) stepX = Math.Sign(dx);
            if (stepY == 0) stepY = Math.Sign(dy);

            Location = new Point(Location.X + stepX, Location.Y + stepY);
        }

        private void FadeTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isFadingOut)
            {
                Opacity += 0.14;
                if (Opacity >= 0.98)
                {
                    Opacity = 1.0;
                    _fadeTimer.Stop();
                    _stayTimer.Start();
                }
            }
            else
            {
                Opacity -= 0.14;
                if (Opacity <= 0.05)
                {
                    _fadeTimer.Stop();
                    _slideTimer.Stop();
                    Close();
                }
            }
        }

        public void CloseToast()
        {
            if (_isFadingOut) return;
            _stayTimer.Stop();
            _isFadingOut = true;
            _fadeTimer.Start();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _stayTimer.Stop(); // Pause countdown on hover
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isFadingOut)
            {
                _stayTimer.Start(); // Resume countdown
            }
            if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Invalidate(_closeButtonRect);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hovered = _closeButtonRect.Contains(e.Location);
            if (_isCloseHovered != hovered)
            {
                _isCloseHovered = hovered;
                Invalidate(_closeButtonRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_closeButtonRect.Contains(e.Location))
                {
                    CloseToast();
                }
                else
                {
                    ClickAction?.Invoke();
                    CloseToast();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 1. Draw Card Background & Border
            using (var path = CreateRoundedRectangle(rect, 8))
            {
                using var bgBrush = new SolidBrush(palette.Surface);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(palette.Border, 1.2f);
                g.DrawPath(borderPen, path);
            }

            // 2. Resolve Accent Color & Glyph
            var (accentColor, iconChar) = _type switch
            {
                ToastType.Success => (palette.Success, "✔"),
                ToastType.Warning => (palette.Warning, "⚠"),
                ToastType.Error => (palette.Danger, "✕"),
                ToastType.Alarm => (Color.FromArgb(220, 38, 38), "⚡"),
                _ => (palette.Primary, "ℹ")
            };

            // Left vertical accent strip
            using (var stripBrush = new SolidBrush(accentColor))
            {
                using var stripPath = CreateRoundedRectangle(new Rectangle(2, 2, 5, Height - 5), 3);
                g.FillPath(stripBrush, stripPath);
            }

            // 3. Draw Type Icon
            Rectangle iconRect = new Rectangle(16, 0, 24, Height);
            TextRenderer.DrawText(
                g,
                iconChar,
                ZeroFontCache.Get("Segoe UI", 12f, FontStyle.Bold),
                iconRect,
                accentColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 4. Draw Close Button (✕)
            int closeSize = 16;
            _closeButtonRect = new Rectangle(Width - closeSize - 10, 10, closeSize, closeSize);
            Color closeColor = _isCloseHovered ? palette.TextPrimary : palette.TextSecondary;
            TextRenderer.DrawText(
                g,
                "✕",
                ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold),
                _closeButtonRect,
                closeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 5. Draw Title & Message
            int textX = 46;
            int textW = Width - textX - closeSize - 16;

            if (!string.IsNullOrEmpty(_title))
            {
                // Title (Bold)
                Rectangle titleRect = new Rectangle(textX, 10, textW, 20);
                using (var titleFont = new Font(Font.FontFamily, 9.2f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        g,
                        _title,
                        titleFont,
                        titleRect,
                        palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.WordEllipsis);
                }

                // Message (Regular)
                Rectangle msgRect = new Rectangle(textX, 30, textW, Height - 34);
                using (var msgFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        g,
                        _message,
                        msgFont,
                        msgRect,
                        palette.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.WordEllipsis);
                }
            }
            else
            {
                // Message only (Centered vertically)
                Rectangle textRect = new Rectangle(textX, 0, textW, Height);
                using (var msgFont = new Font(Font.FontFamily, 9f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        g,
                        _message,
                        msgFont,
                        textRect,
                        palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        #region Static Convenience API (Delegating to ToastStackManager)

        public static void Show(
            Form parent,
            string message,
            ToastType type = ToastType.Info,
            int durationMs = 3000)
        {
            ToastStackManager.Show(parent, message, string.Empty, type, durationMs);
        }

        public static void Show(
            Form parent,
            string message,
            string title,
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition position = ToastPosition.TopRight)
        {
            ToastStackManager.Show(parent, message, title, type, durationMs, onClick, position);
        }

        public static void Success(Form parent, string message, string title = "") =>
            ToastStackManager.Success(parent, message, title);

        public static void Info(Form parent, string message, string title = "") =>
            ToastStackManager.Info(parent, message, title);

        public static void Warning(Form parent, string message, string title = "") =>
            ToastStackManager.Warning(parent, message, title);

        public static void Error(Form parent, string message, string title = "") =>
            ToastStackManager.Error(parent, message, title);

        public static void Alarm(Form parent, string message, string title = "") =>
            ToastStackManager.Alarm(parent, message, title);

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stayTimer.Dispose();
                _fadeTimer.Dispose();
                _slideTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Thread-safe central stacking manager for ZeroUI toasts.
    /// Manages active toast slot allocations, multi-corner anchors, overflow queueing, and smooth slide repositioning.
    /// </summary>
    public static class ToastStackManager
    {
        private class PendingToastInfo
        {
            public Form Owner { get; set; } = null!;
            public string Message { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public ToastType Type { get; set; }
            public int DurationMs { get; set; }
            public Action? OnClick { get; set; }
            public ToastPosition Position { get; set; }
        }

        private static readonly List<ZToastNotification> _activeToasts = new List<ZToastNotification>();
        private static readonly Queue<PendingToastInfo> _pendingQueue = new Queue<PendingToastInfo>();
        private static readonly object _syncLock = new object();

        public static int MaxVisibleToasts { get; set; } = 5;
        public static int MarginX { get; set; } = 24;
        public static int MarginY { get; set; } = 24;
        public static int Gap { get; set; } = 10;
        public static ToastPosition DefaultPosition { get; set; } = ToastPosition.TopRight;
        public static ZeroNotificationDeliveryMode DeliveryMode { get; set; } = ZeroNotificationDeliveryMode.Auto;
        public static bool RouteAlarmsToSystem { get; set; } = true;

        public static IReadOnlyList<ZToastNotification> ActiveToasts
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeToasts.ToArray();
                }
            }
        }

        public static int PendingCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _pendingQueue.Count;
                }
            }
        }

        public static void Show(
            Form parent,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition? position = null)
        {
            if (parent == null || parent.IsDisposed) return;

            if (parent.InvokeRequired)
            {
                parent.BeginInvoke(new Action(() => Show(parent, message, title, type, durationMs, onClick, position)));
                return;
            }

            WindowsNotificationBridge.RegisterMainForm(parent);

            bool isAppForeground = Form.ActiveForm == parent || (parent != null && parent.ContainsFocus && parent.WindowState != FormWindowState.Minimized);
            bool shouldShowInApp = DeliveryMode == ZeroNotificationDeliveryMode.InAppOnly ||
                                   DeliveryMode == ZeroNotificationDeliveryMode.Dual ||
                                   (DeliveryMode == ZeroNotificationDeliveryMode.Auto && isAppForeground);

            bool shouldShowSystem = DeliveryMode == ZeroNotificationDeliveryMode.SystemOnly ||
                                    DeliveryMode == ZeroNotificationDeliveryMode.Dual ||
                                    (DeliveryMode == ZeroNotificationDeliveryMode.Auto && !isAppForeground) ||
                                    (RouteAlarmsToSystem && type == ToastType.Alarm);

            if (shouldShowSystem)
            {
                WindowsNotificationBridge.ShowNotification(title, message, type, durationMs, onClick);
            }

            if (!shouldShowInApp) return;

            var pos = position ?? DefaultPosition;

            lock (_syncLock)
            {
                if (_activeToasts.Count < MaxVisibleToasts)
                {
                    SpawnToast(parent!, message, title, type, durationMs, onClick, pos);
                }
                else
                {
                    _pendingQueue.Enqueue(new PendingToastInfo
                    {
                        Owner = parent!,
                        Message = message,
                        Title = title,
                        Type = type,
                        DurationMs = durationMs,
                        OnClick = onClick,
                        Position = pos
                    });
                }
            }
        }

        public static void Success(Form parent, string message, string title = "") =>
            Show(parent, message, title, ToastType.Success);

        public static void Info(Form parent, string message, string title = "") =>
            Show(parent, message, title, ToastType.Info);

        public static void Warning(Form parent, string message, string title = "") =>
            Show(parent, message, title, ToastType.Warning);

        public static void Error(Form parent, string message, string title = "") =>
            Show(parent, message, title, ToastType.Error);

        public static void Alarm(Form parent, string message, string title = "") =>
            Show(parent, message, title, ToastType.Alarm);

        private static void SpawnToast(
            Form parent,
            string message,
            string title,
            ToastType type,
            int durationMs,
            Action? onClick,
            ToastPosition pos)
        {
            var toast = new ZToastNotification(parent, message, title, type, durationMs, onClick, pos);

            // Compute target position based on current active list
            var target = CalculateLocation(parent, toast.Width, toast.Height, _activeToasts.Count, pos);
            toast.TargetX = target.X;
            toast.TargetY = target.Y;
            toast.Location = target;

            toast.FormClosed += (s, e) => OnToastClosed(toast, parent);

            _activeToasts.Add(toast);
            toast.Show(parent);
            toast.StartDisplay();
        }

        private static void OnToastClosed(ZToastNotification toast, Form parent)
        {
            lock (_syncLock)
            {
                _activeToasts.Remove(toast);

                // Activate next pending if available
                if (_pendingQueue.Count > 0 && _activeToasts.Count < MaxVisibleToasts)
                {
                    var next = _pendingQueue.Dequeue();
                    if (!next.Owner.IsDisposed)
                    {
                        SpawnToast(next.Owner, next.Message, next.Title, next.Type, next.DurationMs, next.OnClick, next.Position);
                    }
                }

                // Relocate remaining active toasts
                RestackActiveToasts(parent);
            }
        }

        private static void RestackActiveToasts(Form parent)
        {
            if (parent.IsDisposed) return;

            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var t = _activeToasts[i];
                var target = CalculateLocation(parent, t.Width, t.Height, i, t.Position);
                t.AnimateTo(target.X, target.Y);
            }
        }

        private static Point CalculateLocation(
            Form owner,
            int toastWidth,
            int toastHeight,
            int stackIndex,
            ToastPosition position)
        {
            Point parentScreen = owner.PointToScreen(Point.Empty);
            int clientW = owner.ClientSize.Width;
            int clientH = owner.ClientSize.Height;

            int cumulativeOffset = stackIndex * (toastHeight + Gap);
            int x;
            int y;

            switch (position)
            {
                case ToastPosition.TopRight:
                    x = parentScreen.X + clientW - toastWidth - MarginX;
                    y = parentScreen.Y + MarginY + cumulativeOffset;
                    break;

                case ToastPosition.BottomRight:
                    x = parentScreen.X + clientW - toastWidth - MarginX;
                    y = parentScreen.Y + clientH - MarginY - toastHeight - cumulativeOffset;
                    break;

                case ToastPosition.TopLeft:
                    x = parentScreen.X + MarginX;
                    y = parentScreen.Y + MarginY + cumulativeOffset;
                    break;

                case ToastPosition.BottomLeft:
                    x = parentScreen.X + MarginX;
                    y = parentScreen.Y + clientH - MarginY - toastHeight - cumulativeOffset;
                    break;

                case ToastPosition.TopCenter:
                    x = parentScreen.X + (clientW - toastWidth) / 2;
                    y = parentScreen.Y + MarginY + cumulativeOffset;
                    break;

                case ToastPosition.BottomCenter:
                    x = parentScreen.X + (clientW - toastWidth) / 2;
                    y = parentScreen.Y + clientH - MarginY - toastHeight - cumulativeOffset;
                    break;

                default:
                    x = parentScreen.X + clientW - toastWidth - MarginX;
                    y = parentScreen.Y + MarginY + cumulativeOffset;
                    break;
            }

            return new Point(x, y);
        }

        /// <summary>
        /// Instantly closes and dismisses all active and queued toast notifications.
        /// </summary>
        public static void Clear()
        {
            lock (_syncLock)
            {
                _pendingQueue.Clear();
                var activeCopy = new List<ZToastNotification>(_activeToasts);
                foreach (var toast in activeCopy)
                {
                    try { toast.Close(); } catch { }
                }
                _activeToasts.Clear();
            }
        }
    }

    [Obsolete("Use ToastType instead.")]
    public enum ZeroToastType
    {
        Info = ToastType.Info,
        Success = ToastType.Success,
        Warning = ToastType.Warning,
        Error = ToastType.Error,
        Alarm = ToastType.Alarm
    }

    [Obsolete("Use ToastPosition instead.")]
    public enum ZeroToastPosition
    {
        TopRight = ToastPosition.TopRight,
        BottomRight = ToastPosition.BottomRight,
        TopLeft = ToastPosition.TopLeft,
        BottomLeft = ToastPosition.BottomLeft,
        TopCenter = ToastPosition.TopCenter,
        BottomCenter = ToastPosition.BottomCenter
    }

    [Obsolete("Use ZToastNotification instead.")]
    [ToolboxItem(false)]
    public class ZeroToast : ZToastNotification
    {
        public ZeroToast(
            Form owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition position = ToastPosition.TopRight)
            : base(owner, message, title, type, durationMs, onClick, position)
        {
        }

        public static void Show(Form parent, string message, ZeroToastType type = ZeroToastType.Info, int durationMs = 3000)
            => ZToastNotification.Show(parent, message, (ToastType)type, durationMs);

        public static void Show(Form parent, string message, string title, ZeroToastType type = ZeroToastType.Info, int durationMs = 3000, Action? onClick = null, ZeroToastPosition position = ZeroToastPosition.TopRight)
            => ZToastNotification.Show(parent, message, title, (ToastType)type, durationMs, onClick, (ToastPosition)position);

        public static new void Show(Form parent, string message, ToastType type = ToastType.Info, int durationMs = 3000)
            => ZToastNotification.Show(parent, message, type, durationMs);

        public static new void Show(Form parent, string message, string title, ToastType type = ToastType.Info, int durationMs = 3000, Action? onClick = null, ToastPosition position = ToastPosition.TopRight)
            => ZToastNotification.Show(parent, message, title, type, durationMs, onClick, position);

        public static new void Success(Form parent, string message, string title = "")
            => ZToastNotification.Success(parent, message, title);

        public static new void Info(Form parent, string message, string title = "")
            => ZToastNotification.Info(parent, message, title);

        public static new void Warning(Form parent, string message, string title = "")
            => ZToastNotification.Warning(parent, message, title);

        public static new void Error(Form parent, string message, string title = "")
            => ZToastNotification.Error(parent, message, title);

        public static new void Alarm(Form parent, string message, string title = "")
            => ZToastNotification.Alarm(parent, message, title);
    }

    [Obsolete("Use ToastStackManager instead.")]
    public static class ZeroToastStackManager
    {
        public static int MaxVisibleToasts { get => ToastStackManager.MaxVisibleToasts; set => ToastStackManager.MaxVisibleToasts = value; }
        public static int MarginX { get => ToastStackManager.MarginX; set => ToastStackManager.MarginX = value; }
        public static int MarginY { get => ToastStackManager.MarginY; set => ToastStackManager.MarginY = value; }
        public static int Gap { get => ToastStackManager.Gap; set => ToastStackManager.Gap = value; }
        public static ToastPosition DefaultPosition { get => ToastStackManager.DefaultPosition; set => ToastStackManager.DefaultPosition = value; }
        public static ZeroNotificationDeliveryMode DeliveryMode { get => ToastStackManager.DeliveryMode; set => ToastStackManager.DeliveryMode = value; }
        public static bool RouteAlarmsToSystem { get => ToastStackManager.RouteAlarmsToSystem; set => ToastStackManager.RouteAlarmsToSystem = value; }

        public static void Show(Form parent, string message, string title = "", ZeroToastType type = ZeroToastType.Info, int durationMs = 3000, Action? onClick = null, ZeroToastPosition position = ZeroToastPosition.TopRight)
            => ToastStackManager.Show(parent, message, title, (ToastType)type, durationMs, onClick, (ToastPosition)position);

        public static void Show(Form parent, string message, string title = "", ToastType type = ToastType.Info, int durationMs = 3000, Action? onClick = null, ToastPosition position = ToastPosition.TopRight)
            => ToastStackManager.Show(parent, message, title, type, durationMs, onClick, position);

        public static void Success(Form parent, string message, string title = "") => ToastStackManager.Success(parent, message, title);
        public static void Info(Form parent, string message, string title = "") => ToastStackManager.Info(parent, message, title);
        public static void Warning(Form parent, string message, string title = "") => ToastStackManager.Warning(parent, message, title);
        public static void Error(Form parent, string message, string title = "") => ToastStackManager.Error(parent, message, title);
        public static void Alarm(Form parent, string message, string title = "") => ToastStackManager.Alarm(parent, message, title);
        public static void Clear() => ToastStackManager.Clear();
        public static void CloseAll() => ToastStackManager.Clear();
    }

    /// <summary>
    /// Legacy shim for ToastNotification.
    /// </summary>
    [Obsolete("ToastNotification is deprecated and will be removed in 5 release cycles. Please migrate to ZToastNotification instead.")]
    public class ToastNotification : ZToastNotification
    {
        public ToastNotification(
            Form parent,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition position = ToastPosition.TopRight)
            : base(parent, message, title, type, durationMs, onClick, position) { }
    }

    /// <summary>
    /// Canonical Z-prefixed short alias for <see cref="ZToastNotification"/>.
    /// </summary>
    public class ZToast : ZToastNotification
    {
        public ZToast(
            Form parent,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastPosition position = ToastPosition.TopRight)
            : base(parent, message, title, type, durationMs, onClick, position) { }
    }

    /// <summary>
    /// Canonical Z-prefixed alias for <see cref="ToastStackManager"/>.
    /// </summary>
    public static class ZToastStackManager
    {
        public static int MaxVisibleToasts { get => ToastStackManager.MaxVisibleToasts; set => ToastStackManager.MaxVisibleToasts = value; }
        public static int MarginX { get => ToastStackManager.MarginX; set => ToastStackManager.MarginX = value; }
        public static int MarginY { get => ToastStackManager.MarginY; set => ToastStackManager.MarginY = value; }
        public static int Gap { get => ToastStackManager.Gap; set => ToastStackManager.Gap = value; }
        public static ToastPosition DefaultPosition { get => ToastStackManager.DefaultPosition; set => ToastStackManager.DefaultPosition = value; }
        public static ZeroNotificationDeliveryMode DeliveryMode { get => ToastStackManager.DeliveryMode; set => ToastStackManager.DeliveryMode = value; }
        public static bool RouteAlarmsToSystem { get => ToastStackManager.RouteAlarmsToSystem; set => ToastStackManager.RouteAlarmsToSystem = value; }

        public static void Show(Form parent, string message, string title = "", ToastType type = ToastType.Info, int durationMs = 3000, Action? onClick = null, ToastPosition position = ToastPosition.TopRight)
            => ToastStackManager.Show(parent, message, title, type, durationMs, onClick, position);

        public static void Success(Form parent, string message, string title = "") => ToastStackManager.Success(parent, message, title);
        public static void Info(Form parent, string message, string title = "") => ToastStackManager.Info(parent, message, title);
        public static void Warning(Form parent, string message, string title = "") => ToastStackManager.Warning(parent, message, title);
        public static void Error(Form parent, string message, string title = "") => ToastStackManager.Error(parent, message, title);
        public static void Alarm(Form parent, string message, string title = "") => ToastStackManager.Alarm(parent, message, title);
        public static void Clear() => ToastStackManager.Clear();
        public static void CloseAll() => ToastStackManager.Clear();
    }
}

