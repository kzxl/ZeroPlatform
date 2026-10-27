using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Modern theme-aware container group box with stylish caption bar, rounded borders,
    /// optional interactive collapsible toggle, icon support, and High-DPI Per-Monitor V2 scaling.
    /// Canonical modern replacement for standard <see cref="System.Windows.Forms.GroupBox"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Containers")]
    [Description("Modern container group box with styled header caption bar and theme reactivity")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDefaultControl.bmp")]
    [Designer("ZeroUI.WinForms.Design.Common.ZGroupBoxDesigner, ZeroUI.WinForms.Design")]
    public class ZGroupBox : Panel, IZeroDpiScalable
    {
        private string _caption = "Group Title";
        private Image? _captionImage;
        private bool _showCaption = true;
        private int _baseCaptionHeight = 32;
        private int _captionHeight = 32;
        private int _baseBorderRadius = 6;
        private int _borderRadius = 6;
        private float _borderThickness = 1f;
        private Color _customBorderColor = Color.Empty;
        private Color _customHeaderColor = Color.Empty;
        private Color _customCaptionColor = Color.Empty;
        private Color _customContentColor = Color.Empty;
        private float _currentDpiScale = 1.0f;

        private bool _collapsible = false;
        private bool _isCollapsed = false;
        private int _expandedHeight = 150;
        private bool _isHoveringChevron = false;
        private Rectangle _chevronRect;

        public event EventHandler? CollapsedChanged;

        [Category("Appearance")]
        [DefaultValue("Group Title")]
        [Description("The caption text displayed in the header bar.")]
        public string Caption
        {
            get => _caption;
            set
            {
                if (_caption != value)
                {
                    _caption = value ?? string.Empty;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Optional icon displayed on the left side of the header caption.")]
        public Image? CaptionImage
        {
            get => _captionImage;
            set
            {
                if (_captionImage != value)
                {
                    _captionImage = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Gets or sets whether the top caption header bar is visible.")]
        public bool ShowCaption
        {
            get => _showCaption;
            set
            {
                if (_showCaption != value)
                {
                    _showCaption = value;
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(32)]
        [Description("Base unscaled height of the caption header bar.")]
        public int CaptionHeight
        {
            get => _baseCaptionHeight;
            set
            {
                int clamped = Math.Max(20, value);
                if (_baseCaptionHeight != clamped)
                {
                    _baseCaptionHeight = clamped;
                    _captionHeight = (int)Math.Round(_baseCaptionHeight * _currentDpiScale);
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(6)]
        [Description("Corner radius for smooth rounded group border.")]
        public int BorderRadius
        {
            get => _baseBorderRadius;
            set
            {
                int clamped = Math.Max(0, value);
                if (_baseBorderRadius != clamped)
                {
                    _baseBorderRadius = clamped;
                    _borderRadius = (int)Math.Round(_baseBorderRadius * _currentDpiScale);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(1f)]
        [Description("Thickness of the border outline.")]
        public float BorderThickness
        {
            get => _borderThickness;
            set
            {
                float clamped = Math.Max(0.5f, value);
                if (Math.Abs(_borderThickness - clamped) > 0.01f)
                {
                    _borderThickness = clamped;
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Empty")]
        [Description("Custom border color override. If empty, the active theme border color is used.")]
        public Color BorderColor
        {
            get => _customBorderColor;
            set
            {
                if (_customBorderColor != value)
                {
                    _customBorderColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Empty")]
        [Description("Custom header background color override.")]
        public Color HeaderBackColor
        {
            get => _customHeaderColor;
            set
            {
                if (_customHeaderColor != value)
                {
                    _customHeaderColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Empty")]
        [Description("Custom caption text color override.")]
        public Color CaptionColor
        {
            get => _customCaptionColor;
            set
            {
                if (_customCaptionColor != value)
                {
                    _customCaptionColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Empty")]
        [Description("Custom content area background color override.")]
        public Color ContentBackColor
        {
            get => _customContentColor;
            set
            {
                if (_customContentColor != value)
                {
                    _customContentColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Enables interactive expand/collapse toggle via the caption bar.")]
        public bool Collapsible
        {
            get => _collapsible;
            set
            {
                if (_collapsible != value)
                {
                    _collapsible = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Gets or sets whether the group control is collapsed.")]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    if (_isCollapsed)
                    {
                        _expandedHeight = Height;
                        Height = _captionHeight;
                    }
                    else
                    {
                        Height = Math.Max(_expandedHeight, _captionHeight + 20);
                    }
                    CollapsedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public override Rectangle DisplayRectangle
        {
            get
            {
                int headerH = _showCaption ? _captionHeight : 0;
                int borderW = (int)Math.Ceiling(_borderThickness);
                int padLeft = Padding.Left + borderW + 4;
                int padRight = Padding.Right + borderW + 4;
                int padTop = Padding.Top + headerH + borderW + 2;
                int padBottom = Padding.Bottom + borderW + 4;
                return new Rectangle(padLeft, padTop, Math.Max(0, Width - padLeft - padRight), Math.Max(0, Height - padTop - padBottom));
            }
        }

        public ZGroupBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            Padding = new Padding(6);
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += OnCornerStyleChanged;
        }

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;
            _captionHeight = (int)Math.Round(_baseCaptionHeight * _currentDpiScale);
            _borderRadius = (int)Math.Round(_baseBorderRadius * _currentDpiScale);
            PerformLayout();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float factor = ZeroDpi.GetScaleFactor(this);
            if (Math.Abs(factor - _currentDpiScale) > 0.001f)
            {
                ApplyDpiScaling(factor);
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            Invalidate();
        }

        private void OnCornerStyleChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_collapsible && _showCaption && e.Y <= _captionHeight)
            {
                IsCollapsed = !IsCollapsed;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_collapsible && _showCaption)
            {
                bool wasHovering = _isHoveringChevron;
                _isHoveringChevron = _chevronRect.Contains(e.Location) || e.Y <= _captionHeight;
                if (wasHovering != _isHoveringChevron) Invalidate();
                Cursor = _isHoveringChevron ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHoveringChevron)
            {
                _isHoveringChevron = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            Color effectiveBorder = _customBorderColor != Color.Empty ? _customBorderColor : palette.Border;
            Color effectiveHeader = _customHeaderColor != Color.Empty ? _customHeaderColor : palette.HeaderBackground;
            Color effectiveText = _customCaptionColor != Color.Empty ? _customCaptionColor : palette.TextPrimary;
            Color effectiveBody = _customContentColor != Color.Empty ? _customContentColor : palette.CardBackground;

            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 1. Draw Container Body Background
            if (effRadius > 0)
            {
                using var bodyPath = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius);
                using var bodyBrush = new SolidBrush(effectiveBody);
                g.FillPath(bodyBrush, bodyPath);
            }
            else
            {
                using var bodyBrush = new SolidBrush(effectiveBody);
                g.FillRectangle(bodyBrush, rect);
            }

            // 2. Draw Caption Bar
            if (_showCaption)
            {
                Rectangle headerRect = new Rectangle(0, 0, Width - 1, _captionHeight);
                if (effRadius > 0)
                {
                    // Draw header with top rounded corners
                    using var headerPath = CreateTopRoundedRectangle(headerRect, effRadius);
                    using var headerBrush = new SolidBrush(effectiveHeader);
                    g.FillPath(headerBrush, headerPath);
                }
                else
                {
                    using var headerBrush = new SolidBrush(effectiveHeader);
                    g.FillRectangle(headerBrush, headerRect);
                }

                // Header Bottom Separator Line
                if (!_isCollapsed)
                {
                    using var sepPen = new Pen(effectiveBorder, 1f);
                    g.DrawLine(sepPen, 0, _captionHeight, Width - 1, _captionHeight);
                }

                // Draw Icon & Caption Text
                int textX = (int)Math.Round(12 * _currentDpiScale);
                int centerY = _captionHeight / 2;

                if (_captionImage != null)
                {
                    int iconSize = Math.Min(16, _captionHeight - 8);
                    g.DrawImage(_captionImage, textX, centerY - (iconSize / 2), iconSize, iconSize);
                    textX += iconSize + 6;
                }

                int rightMargin = _collapsible ? (int)Math.Round(32 * _currentDpiScale) : 10;
                Rectangle textRect = new Rectangle(textX, 0, Math.Max(10, Width - textX - rightMargin), _captionHeight);
                using (var textBrush = new SolidBrush(effectiveText))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString(_caption, Font, textBrush, textRect, sf);
                }

                // Draw Collapsible Chevron
                if (_collapsible)
                {
                    int chevronSize = (int)Math.Round(18 * _currentDpiScale);
                    int chevX = Width - chevronSize - (int)Math.Round(10 * _currentDpiScale);
                    int chevY = centerY - (chevronSize / 2);
                    _chevronRect = new Rectangle(chevX, chevY, chevronSize, chevronSize);

                    Color chevColor = _isHoveringChevron ? palette.Primary : palette.TextSecondary;
                    using var chevPen = new Pen(chevColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

                    int midX = chevX + chevronSize / 2;
                    int midY = chevY + chevronSize / 2;

                    if (_isCollapsed)
                    {
                        // Pointing Down
                        g.DrawLine(chevPen, midX - 4, midY - 2, midX, midY + 3);
                        g.DrawLine(chevPen, midX, midY + 3, midX + 4, midY - 2);
                    }
                    else
                    {
                        // Pointing Up
                        g.DrawLine(chevPen, midX - 4, midY + 2, midX, midY - 3);
                        g.DrawLine(chevPen, midX, midY - 3, midX + 4, midY + 2);
                    }
                }
            }

            // 3. Draw Outer Border
            if (_borderThickness > 0)
            {
                using var pen = new Pen(effectiveBorder, _borderThickness) { Alignment = PenAlignment.Inset };
                if (effRadius > 0)
                {
                    using var borderPath = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius);
                    g.DrawPath(pen, borderPath);
                }
                else
                {
                    g.DrawRectangle(pen, rect);
                }
            }
        }

        private static GraphicsPath CreateTopRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                ZeroUIConfig.CornerStyleChanged -= OnCornerStyleChanged;
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZGroupBox"/>.
    /// </summary>
    [Obsolete("GroupControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGroupBox instead.")]
    public class GroupControl : ZGroupBox { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZGroupBox"/>.
    /// </summary>
    [Obsolete("ZeroGroupControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGroupBox instead.")]
    public class ZeroGroupControl : ZGroupBox { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZGroupBox"/>.
    /// </summary>
    [Obsolete("ZeroGroupBox is deprecated and will be removed in 5 release cycles. Please migrate to ZGroupBox instead.")]
    public class ZeroGroupBox : ZGroupBox { }

    /// <summary>
    /// Convenience alias for <see cref="ZGroupBox"/>.
    /// </summary>
    public class ZGroupControl : ZGroupBox { }

    #endregion
}
