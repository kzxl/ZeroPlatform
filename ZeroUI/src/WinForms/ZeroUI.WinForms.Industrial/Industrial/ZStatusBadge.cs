using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum ZeroStatusType
    {
        Running,    // Green
        Idle,       // Amber
        Alarm,      // Red
        Processing, // Blue
        Offline     // Gray
    }

    /// <summary>
    /// Modern real-time status indicator with smooth expanding pulse ring animation.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Real-time equipment status indicator with animated pulse ring")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroStatusBadge.bmp")]
    public class ZStatusBadge : Control
    {
        private ZeroStatusType _status = ZeroStatusType.Running;
        private int _dotSize = 10;
        private bool _pulseEnabled = true;
        private IDisposable? _clockToken;

        public ZStatusBadge()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(130, 24);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Text = "Active / Running";

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this) && _pulseEnabled)
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (_pulseEnabled && (_status == ZeroStatusType.Running || _status == ZeroStatusType.Alarm || _status == ZeroStatusType.Processing))
            {
                if (IsHandleCreated && Visible)
                {
                    Invalidate();
                }
            }
        }


        [Category("Appearance")]
        [DefaultValue(ZeroStatusType.Running)]
        public ZeroStatusType Status
        {
            get => _status;
            set { _status = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool PulseEnabled
        {
            get => _pulseEnabled;
            set
            {
                _pulseEnabled = value;
                if (value && IsHandleCreated && !ZeroDesignHelper.IsInDesignMode(this))
                {
                    _clockToken ??= ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                }
                else if (!value)
                {
                    _clockToken?.Dispose();
                    _clockToken = null;
                }
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(10)]
        public int DotSize
        {
            get => _dotSize;
            set { _dotSize = Math.Max(6, value); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var (dotColor, pulseColor) = GetColors(_status);

            int centerY = Height / 2;
            int maxPulseRadius = _dotSize + 6;
            int dotCenterY = centerY;
            int dotCenterX = maxPulseRadius + 2;

            // 1. Draw Expanding Pulse Wave (if enabled and status has pulse)
            if (_pulseEnabled && (_status == ZeroStatusType.Running || _status == ZeroStatusType.Alarm || _status == ZeroStatusType.Processing))
            {
                float progress = ZeroAnimationClock.FluidPhase;
                float currentRadius = _dotSize / 2f + (progress * (_dotSize * 0.9f));
                int alpha = (int)((1f - progress) * 180);
                Color ringColor = Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), pulseColor);

                using var ringBrush = new SolidBrush(ringColor);
                g.FillEllipse(ringBrush, dotCenterX - currentRadius, dotCenterY - currentRadius, currentRadius * 2, currentRadius * 2);
            }

            // 2. Draw Solid Center Status Dot
            RectangleF dotRect = new RectangleF(dotCenterX - (_dotSize / 2f), dotCenterY - (_dotSize / 2f), _dotSize, _dotSize);
            using (var dotBrush = new SolidBrush(dotColor))
            {
                g.FillEllipse(dotBrush, dotRect);
            }

            // 3. Draw Text Label
            if (!string.IsNullOrEmpty(Text))
            {
                int textLeft = (int)(dotCenterX + (_dotSize / 2f) + 8);
                Rectangle textRect = new Rectangle(textLeft, 0, Width - textLeft, Height);
                Color textColor = (ForeColor != Color.Empty && ForeColor != Color.FromArgb(31, 41, 55)) ? ForeColor : ZeroTheme.Colors.TextPrimary;
                TextRenderer.DrawText(
                    g,
                    Text,
                    Font,
                    textRect,
                    textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static (Color dot, Color pulse) GetColors(ZeroStatusType type) => type switch
        {
            ZeroStatusType.Running => (Color.FromArgb(16, 185, 129), Color.FromArgb(167, 243, 208)),     // Emerald
            ZeroStatusType.Idle => (Color.FromArgb(245, 158, 11), Color.FromArgb(254, 243, 199)),         // Amber
            ZeroStatusType.Alarm => (Color.FromArgb(239, 68, 68), Color.FromArgb(254, 202, 202)),         // Ruby
            ZeroStatusType.Processing => (Color.FromArgb(59, 130, 246), Color.FromArgb(191, 219, 254)),    // Blue
            _ => (Color.FromArgb(156, 163, 175), Color.FromArgb(229, 231, 235))                           // Slate
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _clockToken?.Dispose();
                _clockToken = null;
            }
            base.Dispose(disposing);
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    /// <summary>
    /// Legacy alias for <see cref="ZStatusBadge"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroStatusBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZStatusBadge instead.")]
    [ToolboxItem(false)]
    public class ZeroStatusBadge : ZStatusBadge
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStatusBadge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StatusBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZStatusBadge instead.")]
    [ToolboxItem(false)]
    public class StatusBadge : ZStatusBadge
    {
    }

    #endregion
}
