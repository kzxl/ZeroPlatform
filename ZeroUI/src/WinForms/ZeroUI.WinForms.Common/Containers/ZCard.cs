using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{

    /// <summary>
    /// Modern container card for ZeroUI with rounded corners, optional Step Badge, Title, Subtitle, and Action Link.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("Title")]
    [Description("Modern container card with rounded corners, optional Step Badge, and Title")]
    [Designer("ZeroUI.WinForms.Design.Common.ZCardDesigner, ZeroUI.WinForms.Design")]
    public class ZCard : Panel, IZeroDpiScalable
    {
        private int? _stepNumber = 1;
        private Color _badgeColor = Color.FromArgb(79, 70, 229); // Indigo Accent
        private string _title = "Card Title";
        private string? _subtitle;
        private string? _actionText;
        private Color _actionColor = Color.FromArgb(22, 119, 255);
        private int _baseBorderRadius = 8;
        private int _borderRadius = 8;
        private Color _borderColor = Color.FromArgb(229, 231, 235);
        private int _baseHeaderHeight = 44;
        private int _headerHeight = 44;
        private float _currentDpiScale = 1.0f;

        private readonly Panel _contentPanel;
        private Rectangle _actionRect;
        private bool _isActionHovered = false;
        private bool _autoFitContent = true;
        private bool _isAdjustingHeight = false;
        private bool _isMovingChild = false;
        private int _lastCalculatedWidth = -1;

        public event EventHandler? ActionClicked;

        /// <summary>
        /// Gets the active High-DPI Per-Monitor V2 scale factor applied to this Card.
        /// </summary>
        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        /// <summary>
        /// Applies High-DPI Per-Monitor V2 scaling to Card metrics (HeaderHeight, BorderRadius, Padding, and ContentPanel).
        /// </summary>
        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            _headerHeight = (int)Math.Round(_baseHeaderHeight * scaleFactor);
            _borderRadius = (int)Math.Round(_baseBorderRadius * scaleFactor);
            Padding = new Padding((int)Math.Round(12 * scaleFactor));

            UpdateContentLayout();
            UpdateRegion();
            if (_autoFitContent) AdjustHeightToContent();
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
            if (_autoFitContent)
            {
                AdjustHeightToContent();
            }
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            float effectiveFactor = factor.Height > 0f ? factor.Height : factor.Width;
            if (effectiveFactor > 0f && Math.Abs(effectiveFactor - 1.0f) > 0.001f)
            {
                ApplyDpiScaling(_currentDpiScale * effectiveFactor);
            }
        }

        public ZCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = ZeroTheme.Colors.CardBackground;
            _borderColor = ZeroTheme.Colors.Border;
            _badgeColor = ZeroTheme.Colors.Primary;
            Padding = new Padding(12);

            _contentPanel = new Panel
            {
                BackColor = Color.Transparent,
                Location = new Point(12, _headerHeight),
                Size = new Size(Math.Max(10, Width - 24), Math.Max(10, Height - _headerHeight - 12)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = false
            };
            _contentPanel.ControlAdded += OnContentControlAdded;
            _contentPanel.ControlRemoved += OnContentControlRemoved;
            Controls.Add(_contentPanel);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += (s, e) =>
            {
                UpdateRegion();
                Invalidate();
            };
        }

        [Category("Layout")]
        [DefaultValue(true)]
        [Description("Automatically adjusts the Card height to fully enclose its inner content without clipping or inner scrollbars.")]
        public bool AutoFitContent
        {
            get => _autoFitContent;
            set
            {
                if (_autoFitContent != value)
                {
                    _autoFitContent = value;
                    if (_autoFitContent)
                    {
                        AdjustHeightToContent();
                    }
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(1)]
        public int? StepNumber
        {
            get => _stepNumber;
            set { _stepNumber = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BadgeColor
        {
            get => _badgeColor;
            set { _badgeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Card Title")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? Subtitle
        {
            get => _subtitle;
            set
            {
                _subtitle = value;
                _baseHeaderHeight = string.IsNullOrEmpty(value) ? 44 : 58;
                _headerHeight = (int)Math.Round(_baseHeaderHeight * _currentDpiScale);
                UpdateContentLayout();
                if (_autoFitContent) AdjustHeightToContent();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? ActionText
        {
            get => _actionText;
            set
            {
                _actionText = value;
                UpdateContentLayout();
                if (_autoFitContent) AdjustHeightToContent();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(8)]
        public int BorderRadius
        {
            get => _borderRadius;
            set
            {
                _baseBorderRadius = (int)Math.Round(value / _currentDpiScale);
                _borderRadius = Math.Max(0, value);
                UpdateRegion();
                Invalidate();
            }
        }

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [Browsable(false)]
        public Panel ContentPanel => _contentPanel;

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null && e.Control != _contentPanel && _contentPanel != null && !_isMovingChild)
            {
                Control c = e.Control;
                _isMovingChild = true;
                try
                {
                    Controls.Remove(c);
                    _contentPanel.Controls.Add(c);
                }
                finally
                {
                    _isMovingChild = false;
                }
            }
        }

        private void OnContentControlAdded(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                HookChildLayoutEvents(e.Control);
                AdjustHeightToContent();
            }
        }

        private void OnContentControlRemoved(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                UnhookChildLayoutEvents(e.Control);
                AdjustHeightToContent();
            }
        }

        private void HookChildLayoutEvents(Control c)
        {
            c.SizeChanged += OnInnerControlLayoutChanged;
            c.LocationChanged += OnInnerControlLayoutChanged;
            c.VisibleChanged += OnInnerControlLayoutChanged;

            if (c is Panel pnl)
            {
                pnl.ControlAdded += OnInnerPanelChildAdded;
                pnl.ControlRemoved += OnInnerPanelChildRemoved;
                foreach (Control sub in pnl.Controls)
                {
                    sub.SizeChanged += OnInnerControlLayoutChanged;
                    sub.LocationChanged += OnInnerControlLayoutChanged;
                    sub.VisibleChanged += OnInnerControlLayoutChanged;
                }
            }
        }

        private void UnhookChildLayoutEvents(Control c)
        {
            c.SizeChanged -= OnInnerControlLayoutChanged;
            c.LocationChanged -= OnInnerControlLayoutChanged;
            c.VisibleChanged -= OnInnerControlLayoutChanged;

            if (c is Panel pnl)
            {
                pnl.ControlAdded -= OnInnerPanelChildAdded;
                pnl.ControlRemoved -= OnInnerPanelChildRemoved;
                foreach (Control sub in pnl.Controls)
                {
                    sub.SizeChanged -= OnInnerControlLayoutChanged;
                    sub.LocationChanged -= OnInnerControlLayoutChanged;
                    sub.VisibleChanged -= OnInnerControlLayoutChanged;
                }
            }
        }

        private void OnInnerPanelChildAdded(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                e.Control.SizeChanged += OnInnerControlLayoutChanged;
                e.Control.LocationChanged += OnInnerControlLayoutChanged;
                e.Control.VisibleChanged += OnInnerControlLayoutChanged;
                AdjustHeightToContent();
            }
        }

        private void OnInnerPanelChildRemoved(object? sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                e.Control.SizeChanged -= OnInnerControlLayoutChanged;
                e.Control.LocationChanged -= OnInnerControlLayoutChanged;
                e.Control.VisibleChanged -= OnInnerControlLayoutChanged;
                AdjustHeightToContent();
            }
        }

        private void OnInnerControlLayoutChanged(object? sender, EventArgs e)
        {
            AdjustHeightToContent();
        }


        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateContentLayout();
            UpdateRegion();
            if (_autoFitContent && Width != _lastCalculatedWidth)
            {
                AdjustHeightToContent();
            }
            Invalidate();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius);
                Region = new Region(path);
            }
            else
            {
                Region = null;
            }
        }

        private void UpdateContentLayout()
        {
            if (_contentPanel != null)
            {
                int sidePad = (int)Math.Round(12 * _currentDpiScale);
                int bottomPadding = (int)Math.Round((string.IsNullOrEmpty(_actionText) ? 12 : 30) * _currentDpiScale);
                _contentPanel.Location = new Point(sidePad, _headerHeight);
                _contentPanel.Size = new Size(Math.Max(10, Width - (sidePad * 2)), Math.Max(10, Height - _headerHeight - bottomPadding));
            }
        }

        public void AutoFit()
        {
            AdjustHeightToContent();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int w = proposedSize.Width > 0 ? proposedSize.Width : Width;
            int h = _autoFitContent ? CalculatePreferredHeight(w) : Height;
            return new Size(w, h);
        }

        public int CalculatePreferredHeight(int proposedWidth = 0)
        {
            int w = proposedWidth > 0 ? proposedWidth : Width;
            int contentWidth = Math.Max(10, w - 24);
            int bottomPadding = string.IsNullOrEmpty(_actionText) ? 12 : 30;

            if (_contentPanel == null || _contentPanel.Controls.Count == 0)
            {
                return _headerHeight + bottomPadding + 30;
            }

            int maxContentBottom = 0;
            int dockedHeight = 0;

            foreach (Control c in _contentPanel.Controls)
            {
                if (!c.Visible) continue;

                if (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom)
                {
                    int childH = MeasureControlHeight(c, contentWidth);
                    dockedHeight += childH + c.Margin.Vertical;
                }
                else if (c.Dock == DockStyle.Fill)
                {
                    int fillH = MeasureControlHeight(c, contentWidth);
                    maxContentBottom = Math.Max(maxContentBottom, fillH);
                }
                else
                {
                    int childH = MeasureControlHeight(c, contentWidth);
                    int b = c.Top + childH + c.Margin.Bottom;
                    maxContentBottom = Math.Max(maxContentBottom, b);
                }
            }

            int contentHeight = dockedHeight + maxContentBottom;
            if (contentHeight <= 0) contentHeight = 30;

            return _headerHeight + contentHeight + bottomPadding;
        }

        private static int MeasureControlHeight(Control c, int availableWidth)
        {
            if (c is FlowLayoutPanel flp)
            {
                return MeasureFlowLayoutPanelHeight(flp, availableWidth);
            }
            if (c is Panel pnl)
            {
                int maxInner = 0;
                foreach (Control inner in pnl.Controls)
                {
                    if (!inner.Visible) continue;
                    int innerH = MeasureControlHeight(inner, Math.Max(10, availableWidth - inner.Left - pnl.Padding.Horizontal));
                    maxInner = Math.Max(maxInner, inner.Top + innerH + inner.Margin.Bottom);
                }
                return Math.Max(c.Height, maxInner + pnl.Padding.Bottom);
            }

            Size pref = c.GetPreferredSize(new Size(availableWidth, 0));
            if (pref.Height > 0)
            {
                return Math.Max(c.Height, pref.Height);
            }
            return c.Height;
        }

        private static int MeasureFlowLayoutPanelHeight(FlowLayoutPanel flp, int availableWidth)
        {
            if (flp.Controls.Count == 0) return flp.Padding.Vertical;

            int curX = flp.Padding.Left;
            int curY = flp.Padding.Top;
            int rowHeight = 0;
            int maxRight = Math.Max(50, availableWidth - flp.Padding.Right);

            foreach (Control item in flp.Controls)
            {
                if (!item.Visible) continue;

                int itemW = item.Width + item.Margin.Horizontal;
                int itemH = item.Height + item.Margin.Vertical;

                if (curX + itemW > maxRight && curX > flp.Padding.Left)
                {
                    curX = flp.Padding.Left;
                    curY += rowHeight;
                    rowHeight = 0;
                }

                curX += itemW;
                rowHeight = Math.Max(rowHeight, itemH);
            }

            int simulatedH = curY + rowHeight + flp.Padding.Bottom;
            Size pref = flp.GetPreferredSize(new Size(availableWidth, 0));
            return Math.Max(pref.Height, simulatedH);
        }

        private void AdjustHeightToContent()
        {
            if (!_autoFitContent || _isAdjustingHeight || Disposing || IsDisposed) return;

            try
            {
                _isAdjustingHeight = true;
                _lastCalculatedWidth = Width;
                int reqHeight = CalculatePreferredHeight(Width);
                if (reqHeight > 0 && Math.Abs(Height - reqHeight) > 1)
                {
                    Height = reqHeight;
                    UpdateContentLayout();
                    UpdateRegion();
                }
            }
            finally
            {
                _isAdjustingHeight = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width, Height);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);

            // 1. Draw Card Background & Rounded Border
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(rect, effRadius);
                using var bgBrush = new SolidBrush(BackColor);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(_borderColor, 1f) { Alignment = PenAlignment.Inset };
                g.DrawPath(borderPen, path);
            }
            else
            {
                using var bgBrush = new SolidBrush(BackColor);
                g.FillRectangle(bgBrush, rect);

                using var borderPen = new Pen(_borderColor, 1f) { Alignment = PenAlignment.Inset };
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // 2. Draw Header Area
            int currentX = (int)Math.Round(14 * _currentDpiScale);
            int headerCenterY = string.IsNullOrEmpty(_subtitle) ? _headerHeight / 2 : (int)Math.Round(20 * _currentDpiScale);

            // Draw Step Badge
            if (_stepNumber.HasValue)
            {
                int badgeSize = (int)Math.Round(22 * _currentDpiScale);
                Rectangle badgeRect = new Rectangle(currentX, headerCenterY - (badgeSize / 2), badgeSize, badgeSize);
                using (var badgePath = CreateRoundedRectangle(badgeRect, (int)Math.Round(4 * _currentDpiScale)))
                {
                    using var badgeBrush = new SolidBrush(_badgeColor);
                    g.FillPath(badgeBrush, badgePath);
                }

                TextRenderer.DrawText(
                    g,
                    _stepNumber.Value.ToString(),
                    ZeroFontCache.Get("Segoe UI", 9f * _currentDpiScale, FontStyle.Bold),
                    badgeRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                currentX += badgeSize + (int)Math.Round(8 * _currentDpiScale);
            }

            // Draw Title
            using var titleFont = new Font("Segoe UI", 10.5f * _currentDpiScale, FontStyle.Bold);
            Size titleSize = TextRenderer.MeasureText(g, _title, titleFont);
            Rectangle titleRect = new Rectangle(currentX, headerCenterY - (titleSize.Height / 2), Math.Max(10, Width - currentX - (int)Math.Round(16 * _currentDpiScale)), titleSize.Height);
            TextRenderer.DrawText(g, _title, titleFont, titleRect, ZeroTheme.Colors.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Draw Subtitle (if available)
            if (!string.IsNullOrEmpty(_subtitle))
            {
                using var subFont = new Font("Segoe UI", 8.5f * _currentDpiScale, FontStyle.Regular);
                Rectangle subRect = new Rectangle(currentX, titleRect.Bottom + (int)Math.Round(2 * _currentDpiScale), Math.Max(10, Width - currentX - (int)Math.Round(16 * _currentDpiScale)), (int)Math.Round(20 * _currentDpiScale));
                TextRenderer.DrawText(g, _subtitle, subFont, subRect, ZeroTheme.Colors.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // 3. Draw Action Link (Bottom-left or footer)
            if (!string.IsNullOrEmpty(_actionText))
            {
                using var actionFont = new Font("Segoe UI", 9f, _isActionHovered ? FontStyle.Underline : FontStyle.Regular);
                Size actSize = TextRenderer.MeasureText(g, _actionText, actionFont);
                _actionRect = new Rectangle(14, Height - actSize.Height - 8, actSize.Width + 4, actSize.Height + 2);
                Color actCol = _actionColor != Color.FromArgb(22, 119, 255) ? _actionColor : ZeroTheme.Colors.Primary;
                TextRenderer.DrawText(g, _actionText, actionFont, _actionRect, actCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                _actionRect = Rectangle.Empty;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_actionRect.IsEmpty && _actionRect.Contains(e.Location))
            {
                if (!_isActionHovered)
                {
                    _isActionHovered = true;
                    Cursor = Cursors.Hand;
                    Invalidate(_actionRect);
                }
            }
            else if (_isActionHovered)
            {
                _isActionHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_actionRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isActionHovered)
            {
                _isActionHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_actionRect);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && !_actionRect.IsEmpty && _actionRect.Contains(e.Location))
            {
                ActionClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.CardBackground;
                _borderColor = ZeroTheme.Colors.Border;
                Invalidate();
            }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for <see cref="ZCard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroCard is deprecated and will be removed in 5 release cycles. Please migrate to ZCard instead.")]
    [ToolboxItem(false)]
    public class ZeroCard : ZCard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Card is deprecated and will be removed in 5 release cycles. Please migrate to ZCard instead.")]
    [ToolboxItem(false)]
    public class Card : ZCard
    {
    }

    #endregion
}
