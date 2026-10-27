using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    public enum BadgeStatus
    {
        None,
        Primary,
        Success,
        Warning,
        Danger,
        Info
    }

    public enum BadgePlacement
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    /// <summary>
    /// Versatile numeric counter, status indicator pill, and notification badge for ZeroUI WinForms.
    /// Synchronized with WPF Badge for 100% cross-platform parity.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [DefaultProperty("Count")]
    [Description("Versatile numeric counter, status indicator pill, and notification badge")]
    public class ZBadge : ControlBase
    {
        private int? _count = null;
        private int _maxCount = 99;
        private string? _badgeText = null;
        private bool _isDot = false;
        private BadgeStatus _status = BadgeStatus.Danger;
        private BadgePlacement _placement = BadgePlacement.TopRight;

        public ZBadge()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(24, 24);
        }

        [Category("Badge")]
        [DefaultValue(null)]
        public int? Count
        {
            get => _count;
            set { _count = value; Invalidate(); }
        }

        [Category("Badge")]
        [DefaultValue(99)]
        public int MaxCount
        {
            get => _maxCount;
            set { _maxCount = value; Invalidate(); }
        }

        [Category("Badge")]
        [DefaultValue(null)]
        public string? BadgeText
        {
            get => _badgeText;
            set { _badgeText = value; Invalidate(); }
        }

        [Category("Badge")]
        [DefaultValue(false)]
        public bool IsDot
        {
            get => _isDot;
            set { _isDot = value; Invalidate(); }
        }

        [Category("Badge")]
        [DefaultValue(BadgeStatus.Danger)]
        public BadgeStatus Status
        {
            get => _status;
            set { _status = value; Invalidate(); }
        }

        [Category("Badge")]
        [DefaultValue(BadgePlacement.TopRight)]
        public BadgePlacement Placement
        {
            get => _placement;
            set { _placement = value; Invalidate(); }
        }

        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(_badgeText)) return _badgeText!;
                if (_count.HasValue)
                {
                    return _count.Value > _maxCount ? $"{_maxCount}+" : _count.Value.ToString();
                }
                return string.Empty;
            }
        }

        private Color GetStatusColor()
        {
            switch (_status)
            {
                case BadgeStatus.Primary: return Color.FromArgb(79, 70, 229);
                case BadgeStatus.Success: return Color.FromArgb(34, 197, 94);
                case BadgeStatus.Warning: return Color.FromArgb(234, 179, 8);
                case BadgeStatus.Danger: return Color.FromArgb(239, 68, 68);
                case BadgeStatus.Info: return Color.FromArgb(14, 165, 233);
                case BadgeStatus.None:
                default: return Color.FromArgb(100, 116, 139);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            Color bg = GetStatusColor();

            if (_isDot)
            {
                float dotSize = Math.Min(Width, Height) * 0.45f;
                float dx = (Width - dotSize) / 2f;
                float dy = (Height - dotSize) / 2f;
                using var brush = new SolidBrush(bg);
                g.FillEllipse(brush, dx, dy, dotSize, dotSize);
                return;
            }

            string text = DisplayText;
            if (string.IsNullOrEmpty(text))
            {
                text = "0";
            }

            using var font = new Font(Font.FontFamily, Math.Max(7f, 7.5f * scale), FontStyle.Bold);
            SizeF textSize = g.MeasureString(text, font);
            float pillW = Math.Max(Height, textSize.Width + (8f * scale));
            float pillH = Height - (2f * scale);
            float px = (Width - pillW) / 2f;
            float py = 1f * scale;

            using (var path = CreatePillPath(new RectangleF(px, py, pillW, pillH)))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            using var textBrush = new SolidBrush(Color.White);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, font, textBrush, new RectangleF(px, py, pillW, pillH), sf);
        }

        private static GraphicsPath CreatePillPath(RectangleF rect)
        {
            var path = new GraphicsPath();
            float r = rect.Height / 2f;
            path.AddArc(rect.X, rect.Y, rect.Height, rect.Height, 90, 180);
            path.AddArc(rect.Right - rect.Height, rect.Y, rect.Height, rect.Height, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBadge"/>.
    /// </summary>
    [Obsolete("ZeroBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZBadge instead.")]
    [ToolboxItem(false)]
    public class ZeroBadge : ZBadge { }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBadge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Badge is deprecated and will be removed in 5 release cycles. Please migrate to ZBadge instead.")]
    [ToolboxItem(false)]
    public class Badge : ZBadge
    {
    }

    #endregion
}
