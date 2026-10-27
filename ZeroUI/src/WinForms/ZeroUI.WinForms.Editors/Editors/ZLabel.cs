using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern theme-aware label control featuring automatic text ellipsis trimming (AutoEllipsis),
    /// dynamic tooltip expansion on text truncation, skin-aware ClearType typography, and image alignment.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.Label"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Text")]
    [Description("Modern theme-aware label with automatic ellipsis trimming and tooltip overflow")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLabel.bmp")]
    public class ZLabel : ControlBase
    {
        private bool _autoEllipsis = true;
        private bool _showTooltipWhenTruncated = true;
        private ContentAlignment _textAlign = ContentAlignment.MiddleLeft;
        private bool _wordWrap = false;
        private Image? _image;
        private TextImageRelation _textImageRelation = TextImageRelation.ImageBeforeText;

        private readonly ToolTip _overflowToolTip;
        private bool _isTextTruncated;

        public ZLabel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(120, 24);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            BackColor = Color.Transparent;

            _overflowToolTip = new ToolTip
            {
                ShowAlways = false,
                InitialDelay = 400,
                ReshowDelay = 100
            };
        }

        #region Properties

        [Category("Layout")]
        [DefaultValue(false)]
        [Browsable(true)]
        [Description("Automatically resizes the label based on its text and font content.")]
        public override bool AutoSize
        {
            get => base.AutoSize;
            set
            {
                base.AutoSize = value;
                if (value)
                {
                    Size = GetPreferredSize(Size.Empty);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Indicates whether text that exceeds the label boundary is automatically trimmed with an ellipsis (...).")]
        public bool AutoEllipsis
        {
            get => _autoEllipsis;
            set
            {
                if (_autoEllipsis != value)
                {
                    _autoEllipsis = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Automatically displays a tooltip with the complete text when hovered if the text is truncated by AutoEllipsis.")]
        public bool ShowTooltipWhenTruncated
        {
            get => _showTooltipWhenTruncated;
            set => _showTooltipWhenTruncated = value;
        }

        [Category("Appearance")]
        [DefaultValue(ContentAlignment.MiddleLeft)]
        public ContentAlignment TextAlign
        {
            get => _textAlign;
            set
            {
                if (_textAlign != value)
                {
                    _textAlign = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool WordWrap
        {
            get => _wordWrap;
            set
            {
                if (_wordWrap != value)
                {
                    _wordWrap = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public Image? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(TextImageRelation.ImageBeforeText)]
        public TextImageRelation TextImageRelation
        {
            get => _textImageRelation;
            set
            {
                if (_textImageRelation != value)
                {
                    _textImageRelation = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public bool IsTextTruncated => _isTextTruncated;

        #endregion

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (AutoSize)
            {
                Size = GetPreferredSize(Size.Empty);
            }
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (AutoSize)
            {
                Size = GetPreferredSize(Size.Empty);
            }
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (string.IsNullOrEmpty(Text)) return new Size(16, 16);

            var font = ZeroFontCache.Get(Font.FontFamily.Name, Font.Size, Font.Style);
            Size textSize = TextRenderer.MeasureText(Text, font);

            int w = textSize.Width + Padding.Horizontal + 4;
            int h = Math.Max(textSize.Height, 18) + Padding.Vertical;

            if (_image != null)
            {
                if (_textImageRelation == TextImageRelation.ImageBeforeText || _textImageRelation == TextImageRelation.TextBeforeImage)
                {
                    w += _image.Width + 4;
                    h = Math.Max(h, _image.Height + Padding.Vertical);
                }
                else if (_textImageRelation == TextImageRelation.ImageAboveText || _textImageRelation == TextImageRelation.TextAboveImage)
                {
                    h += _image.Height + 4;
                    w = Math.Max(w, _image.Width + Padding.Horizontal);
                }
            }

            return new Size(w, h);
        }

        #region Mouse & Tooltip Handling

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateOverflowTooltip();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateOverflowTooltip();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _overflowToolTip.SetToolTip(this, null);
        }

        private void UpdateOverflowTooltip()
        {
            if (_showTooltipWhenTruncated && _autoEllipsis && _isTextTruncated && !string.IsNullOrEmpty(Text))
            {
                if (_overflowToolTip.GetToolTip(this) != Text)
                {
                    _overflowToolTip.SetToolTip(this, Text);
                }
            }
            else
            {
                _overflowToolTip.SetToolTip(this, null);
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var client = ClientRectangle;
            if (client.Width <= 0 || client.Height <= 0) return;

            // Draw Background if not transparent
            if (BackColor != Color.Transparent && BackColor.A > 0)
            {
                using (var bgBrush = new SolidBrush(BackColor))
                {
                    g.FillRectangle(bgBrush, client);
                }
            }

            var contentRect = new Rectangle(
                client.X + Padding.Left,
                client.Y + Padding.Top,
                Math.Max(1, client.Width - Padding.Horizontal),
                Math.Max(1, client.Height - Padding.Vertical));

            var textRect = contentRect;

            // 1. Draw Image if present
            if (_image != null)
            {
                DrawImageAndLayout(g, contentRect, out textRect);
            }

            // 2. Measure & Check Truncation
            if (!string.IsNullOrEmpty(Text) && textRect.Width > 0 && textRect.Height > 0)
            {
                var font = ZeroFontCache.Get(Font.FontFamily.Name, Font.Size, Font.Style);
                Size measured = TextRenderer.MeasureText(Text, font);

                _isTextTruncated = measured.Width > textRect.Width || (!_wordWrap && measured.Height > textRect.Height);

                Color textColor = Enabled ? (ForeColor != SystemColors.ControlText && ForeColor != Color.Empty ? ForeColor : ZeroTheme.Colors.TextPrimary)
                                          : ZeroTheme.Colors.TextSecondary;

                using (var sf = CreateStringFormat(_textAlign, _autoEllipsis, _wordWrap))
                using (var textBrush = new SolidBrush(textColor))
                {
                    g.DrawString(Text, font, textBrush, textRect, sf);
                }
            }
            else
            {
                _isTextTruncated = false;
            }
        }

        private void DrawImageAndLayout(Graphics g, Rectangle content, out Rectangle textRect)
        {
            int imgW = _image!.Width;
            int imgH = _image.Height;
            textRect = content;

            switch (_textImageRelation)
            {
                case TextImageRelation.ImageBeforeText:
                    int imgY = content.Y + (content.Height - imgH) / 2;
                    g.DrawImage(_image, content.X, imgY, imgW, imgH);
                    textRect = new Rectangle(content.X + imgW + 4, content.Y, Math.Max(0, content.Width - imgW - 4), content.Height);
                    break;

                case TextImageRelation.TextBeforeImage:
                    int rightImgX = content.Right - imgW;
                    int rightImgY = content.Y + (content.Height - imgH) / 2;
                    g.DrawImage(_image, rightImgX, rightImgY, imgW, imgH);
                    textRect = new Rectangle(content.X, content.Y, Math.Max(0, content.Width - imgW - 4), content.Height);
                    break;

                case TextImageRelation.ImageAboveText:
                    int topImgX = content.X + (content.Width - imgW) / 2;
                    g.DrawImage(_image, topImgX, content.Y, imgW, imgH);
                    textRect = new Rectangle(content.X, content.Y + imgH + 4, content.Width, Math.Max(0, content.Height - imgH - 4));
                    break;

                case TextImageRelation.TextAboveImage:
                    int botImgX = content.X + (content.Width - imgW) / 2;
                    int botImgY = content.Bottom - imgH;
                    g.DrawImage(_image, botImgX, botImgY, imgW, imgH);
                    textRect = new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height - imgH - 4));
                    break;

                default: // Overlay
                    int oX = content.X + (content.Width - imgW) / 2;
                    int oY = content.Y + (content.Height - imgH) / 2;
                    g.DrawImage(_image, oX, oY, imgW, imgH);
                    break;
            }
        }

        private static StringFormat CreateStringFormat(ContentAlignment alignment, bool autoEllipsis, bool wordWrap)
        {
            var sf = new StringFormat
            {
                Trimming = autoEllipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None,
                FormatFlags = wordWrap ? 0 : StringFormatFlags.NoWrap
            };

            switch (alignment)
            {
                case ContentAlignment.TopLeft:
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Near;
                    break;
                case ContentAlignment.TopCenter:
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Near;
                    break;
                case ContentAlignment.TopRight:
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Near;
                    break;
                case ContentAlignment.MiddleLeft:
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    break;
                case ContentAlignment.MiddleCenter:
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    break;
                case ContentAlignment.MiddleRight:
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Center;
                    break;
                case ContentAlignment.BottomLeft:
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Far;
                    break;
                case ContentAlignment.BottomCenter:
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Far;
                    break;
                case ContentAlignment.BottomRight:
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Far;
                    break;
            }

            return sf;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _overflowToolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLabel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LabelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZLabel instead.")]
    [ToolboxItem(false)]
    public class LabelControl : ZLabel
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZLabel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLabel is deprecated and will be removed in 5 release cycles. Please migrate to ZLabel instead.")]
    [ToolboxItem(false)]
    public class ZeroLabel : ZLabel
    {
    }

    #endregion
}
