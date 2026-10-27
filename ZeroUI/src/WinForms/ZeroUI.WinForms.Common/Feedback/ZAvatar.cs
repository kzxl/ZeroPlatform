using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

using ZeroUI.Core.Editors;

namespace ZeroUI.WinForms.Feedback
{

    /// <summary>
    /// Circular user avatar component displaying initials or an image,
    /// complete with online presence indicator, size presets, and auto-generated palette colors.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [DefaultProperty("Initials")]
    [Description("Circular user profile avatar with initials, image, and online presence badge")]
    public class ZAvatar : ControlBase
    {
        private string _initials = "ZU";
        private object? _imageSource;
        private string _sizePreset = "Medium";
        private object? _backgroundColor;
        private bool _showOnlineIndicator = false;
        private AvatarStatus _status = AvatarStatus.Online;

        [Category("Appearance")]
        [DefaultValue("ZU")]
        [Description("User initials displayed when no image is loaded.")]
        public string Initials
        {
            get => _initials;
            set { _initials = (value ?? string.Empty).ToUpperInvariant(); Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Profile image (Image or Bitmap).")]
        public object? ImageSource
        {
            get => _imageSource;
            set { _imageSource = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue("Medium")]
        [Description("Preset dimension: Small (28px), Medium (40px), Large (56px), XLarge (72px) or custom pixel size.")]
        public string Size
        {
            get => _sizePreset;
            set
            {
                _sizePreset = value ?? "Medium";
                UpdateDimensions();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Background color for initials circle. If null, automatically resolves a vibrant hash color.")]
        public object? BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Whether to display the bottom-right online presence dot.")]
        public bool ShowOnlineIndicator
        {
            get => _showOnlineIndicator;
            set { _showOnlineIndicator = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(AvatarStatus.Online)]
        [Description("Presence status for the indicator badge.")]
        public AvatarStatus Status
        {
            get => _status;
            set { _status = value; Invalidate(); }
        }

        public ZAvatar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            UpdateDimensions();
            BackColor = Color.Transparent;
        }

        private void UpdateDimensions()
        {
            int px = _sizePreset.ToLowerInvariant() switch
            {
                "small" => 28,
                "medium" => 40,
                "large" => 56,
                "xlarge" => 72,
                _ => int.TryParse(_sizePreset, out int parsed) ? Math.Max(16, parsed) : 40
            };
            base.Size = new System.Drawing.Size(px, px);
        }

        private Color ResolveBackgroundColor()
        {
            if (_backgroundColor is Color c) return c;
            if (_backgroundColor is string hex && !string.IsNullOrEmpty(hex))
            {
                try { return ColorTranslator.FromHtml(hex); } catch { }
            }

            // Derive deterministic vibrant color from initials hash
            int hash = Math.Abs((_initials ?? "Z").GetHashCode());
            Color[] palette =
            {
                Color.FromArgb(59, 130, 246),   // Blue
                Color.FromArgb(16, 185, 129),   // Green
                Color.FromArgb(245, 158, 11),   // Amber
                Color.FromArgb(239, 68, 68),    // Red
                Color.FromArgb(139, 92, 246),   // Purple
                Color.FromArgb(236, 72, 153),   // Pink
                Color.FromArgb(20, 184, 166),   // Teal
                Color.FromArgb(99, 102, 241)    // Indigo
            };
            return palette[hash % palette.Length];
        }

        private static Color GetStatusColor(AvatarStatus status) => status switch
        {
            AvatarStatus.Online => Color.FromArgb(34, 197, 94),
            AvatarStatus.Away => Color.FromArgb(245, 158, 11),
            AvatarStatus.Busy => Color.FromArgb(239, 68, 68),
            _ => Color.FromArgb(156, 163, 175)
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            int diameter = Math.Min(bounds.Width, bounds.Height) - 2;
            if (diameter <= 4) return;

            var circleRect = new Rectangle(1, 1, diameter, diameter);

            // Circular clipping path
            using var circlePath = new GraphicsPath();
            circlePath.AddEllipse(circleRect);

            if (_imageSource is Image img)
            {
                var state = g.Save();
                g.SetClip(circlePath);
                g.DrawImage(img, circleRect);
                g.Restore(state);
            }
            else
            {
                var bg = ResolveBackgroundColor();
                using var bgBrush = new SolidBrush(bg);
                g.FillEllipse(bgBrush, circleRect);

                if (!string.IsNullOrEmpty(_initials))
                {
                    float fontSize = Math.Max(7f, diameter * 0.38f);
                    var font = ZeroFontCache.Get(fontSize, FontStyle.Bold);
                    using var textBrush = new SolidBrush(Color.White);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(_initials, font, textBrush, circleRect, sf);
                }
            }

            // Outline ring
            using (var ringPen = new Pen(Color.FromArgb(40, Color.White), 1f))
            {
                g.DrawEllipse(ringPen, circleRect);
            }

            // Online Presence Indicator Badge
            if (_showOnlineIndicator)
            {
                int dotSize = Math.Max(6, (int)(diameter * 0.28f));
                int dotX = circleRect.Right - dotSize;
                int dotY = circleRect.Bottom - dotSize;
                var dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);

                // Cutout border around dot
                using (var cutoutPen = new Pen(ZeroTheme.Colors.Background, 2f))
                {
                    g.DrawEllipse(cutoutPen, dotRect);
                }

                using (var dotBrush = new SolidBrush(GetStatusColor(_status)))
                {
                    g.FillEllipse(dotBrush, dotRect);
                }
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("ZeroAvatar is deprecated and will be removed in 5 release cycles. Please migrate to ZAvatar instead.")]
    [ToolboxItem(false)]
    public class ZeroAvatar : ZAvatar { }
}
