using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Editors;

namespace ZeroUI.WinForms.Media
{

    /// <summary>
    /// Modern anti-aliased image and avatar control for ZeroUI.
    /// Supports rounded borders, circular avatars, initials fallback, operator online/offline status dots,
    /// and click-to-zoom Lightbox modal preview.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Media")]
    [DefaultProperty("Image")]
    [Description("Modern anti-aliased image and avatar control with initials fallback and zoom lightbox")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroImage.bmp")]
    public class ZPictureEdit : ControlBase
    {
        private Image? _image;
        private ImageScaleMode _scaleMode = ImageScaleMode.Cover;
        private bool _isCircle = false;
        private int _borderRadius = 8;
        private float _borderWidth = 1f;
        private Color? _borderColor;
        private string? _fallbackText;
        private Color? _fallbackColor;
        private AvatarStatus _status = AvatarStatus.None;
        private bool _enableZoomPreview = true;
        private bool _isHovered = false;

        public event EventHandler? ImageClicked;

        public ZPictureEdit()
        {
            Size = new Size(64, 64);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public Image? Image
        {
            get => _image;
            set
            {
                _image = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(ImageScaleMode.Cover)]
        public ImageScaleMode ScaleMode
        {
            get => _scaleMode;
            set
            {
                _scaleMode = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsCircle
        {
            get => _isCircle;
            set
            {
                _isCircle = value;
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
                _borderRadius = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(1f)]
        public float BorderWidth
        {
            get => _borderWidth;
            set
            {
                _borderWidth = Math.Max(0f, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor ?? ZeroTheme.Colors.Border;
            set
            {
                _borderColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? FallbackText
        {
            get => _fallbackText;
            set
            {
                _fallbackText = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public Color? FallbackColor
        {
            get => _fallbackColor;
            set
            {
                _fallbackColor = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(AvatarStatus.None)]
        public AvatarStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool EnableZoomPreview
        {
            get => _enableZoomPreview;
            set => _enableZoomPreview = value;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ImageClicked?.Invoke(this, EventArgs.Empty);

            if (_enableZoomPreview && _image != null)
            {
                ShowLightboxPreview();
            }
        }

        public void ShowLightboxPreview()
        {
            if (_image == null) return;
            var previewPanel = new LightboxViewerPanel(_image);
            IWin32Window parentWindow = (IWin32Window?)FindForm() ?? this;
            ModalDialog.Show(
                parentWindow,
                $"Image Preview ({_image.Width} x {_image.Height} px)",
                previewPanel,
                okText: "Close",
                showCancel: false,
                width: 720,
                height: 540);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            Rectangle clientRect = new Rectangle(0, 0, Width, Height);

            // 1. Fill parent background to eliminate black corner clipping artifacts
            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);
            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, clientRect);
            }

            // 2. Calculate Clipping Path
            using (var clipPath = CreateShapePath(clientRect))
            {
                g.SetClip(clipPath);

                if (_image != null)
                {
                    // Draw Image with selected scale mode
                    DrawScaledImage(g, _image, clientRect, _scaleMode);
                }
                else
                {
                    // Draw Fallback Initials
                    DrawFallbackInitials(g, clientRect, palette);
                }

                // 3. Hover Zoom Indicator Overlay
                if (_isHovered && _enableZoomPreview && _image != null)
                {
                    using var hoverBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
                    g.FillPath(hoverBrush, clipPath);

                    int zSize = Math.Min(26, Math.Min(Width, Height) / 2);
                    if (zSize >= 18)
                    {
                        int zX = (Width - zSize) / 2;
                        int zY = (Height - zSize) / 2;
                        var zRect = new Rectangle(zX, zY, zSize, zSize);
                        using var zBg = new SolidBrush(Color.FromArgb(200, 15, 23, 42));
                        g.FillEllipse(zBg, zRect);

                        using var iconFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                        using var iconBrush = new SolidBrush(Color.White);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("🔍", iconFont, iconBrush, zRect, sf);
                    }
                }

                g.ResetClip();

                // Draw Border
                if (_borderWidth > 0f)
                {
                    Color bColor = _isHovered ? palette.Primary : BorderColor;
                    using var pen = new Pen(bColor, _borderWidth);
                    g.DrawPath(pen, clipPath);
                }
            }

            // Draw Operator/Asset Status Badge Dot
            if (_status != AvatarStatus.None)
            {
                DrawStatusDot(g, clientRect, palette);
            }
        }

        private void DrawScaledImage(Graphics g, Image img, Rectangle targetRect, ImageScaleMode mode)
        {
            PictureScaleMath.CalculateDestination(
                targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height,
                img.Width, img.Height, mode,
                out double destX, out double destY, out double destW, out double destH);

            g.DrawImage(img, new Rectangle((int)destX, (int)destY, (int)destW, (int)destH));
        }

        private void DrawFallbackInitials(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            // Background fill with deterministic or custom fallback color
            Color bgColor = _fallbackColor ?? (string.IsNullOrEmpty(_fallbackText)
                ? palette.Primary
                : ColorTranslator.FromHtml(AvatarHelper.GetDeterministicColorHex(_fallbackText!)));

            using (var brushBg = new SolidBrush(bgColor))
            {
                g.FillRectangle(brushBg, rect);
            }

            string initials = AvatarHelper.ExtractInitials(_fallbackText);
            if (!string.IsNullOrEmpty(initials))
            {
                float fontSize = Math.Min(rect.Width, rect.Height) * 0.38f;
                using var font = new Font(Font.FontFamily, fontSize, FontStyle.Bold);
                using var brushText = new SolidBrush(Color.White);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(initials, font, brushText, rect, sf);
            }
            else
            {
                // Draw default user avatar glyph (👤)
                using var iconFont = new Font("Segoe UI Emoji", Math.Min(rect.Width, rect.Height) * 0.45f);
                using var brushIcon = new SolidBrush(Color.White);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("👤", iconFont, brushIcon, rect, sf);
            }
        }

        private void DrawStatusDot(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            Color dotColor = _status switch
            {
                AvatarStatus.Online => palette.Success,
                AvatarStatus.Busy => palette.Danger,
                AvatarStatus.Away => palette.Warning,
                _ => Color.FromArgb(100, 116, 139) // Slate Offline
            };

            int dotSize = Math.Max(10, Math.Min(rect.Width, rect.Height) / 4);
            int dotX = rect.Right - dotSize - 1;
            int dotY = rect.Bottom - dotSize - 1;
            var dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);

            // Cutout halo matching parent background
            Color cutoutBg = Parent?.BackColor ?? palette.Background;
            if (cutoutBg == Color.Transparent) cutoutBg = palette.Surface;

            var cutoutRect = new Rectangle(dotX - 2, dotY - 2, dotSize + 4, dotSize + 4);
            using (var brushCutout = new SolidBrush(cutoutBg))
            {
                g.FillEllipse(brushCutout, cutoutRect);
            }

            using (var brushDot = new SolidBrush(dotColor))
            {
                g.FillEllipse(brushDot, dotRect);
            }
        }

        private GraphicsPath CreateShapePath(Rectangle r)
        {
            var path = new GraphicsPath();
            if (_isCircle)
            {
                path.AddEllipse(r);
                return path;
            }

            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            return ZeroUIConfig.CreateRoundedRectangle(r, effRadius);
        }

        /// <summary>
        /// Inner Lightbox panel displaying high-res zoomable image with pan, rotation, and export studio.
        /// </summary>
        private class LightboxViewerPanel : Control
        {
            private readonly Image _image;
            private float _zoomFactor = 1.0f;
            private int _rotationDegrees = 0;
            private PointF _panOffset = PointF.Empty;
            private bool _isPanning = false;
            private Point _panStartMouse = Point.Empty;
            private PointF _panStartOffset = PointF.Empty;

            public LightboxViewerPanel(Image image)
            {
                _image = image;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                BackColor = Color.FromArgb(15, 23, 42); // Solid dark viewport

                // Top Toolbar for Zoom, Rotation, and Export
                var topTools = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = Color.FromArgb(24, 32, 50),
                    Padding = new Padding(6, 4, 6, 4)
                };

                var btnZoomIn = CreateToolButton("🔍+ Zoom In", 92, () => { _zoomFactor = Math.Min(8.0f, _zoomFactor * 1.25f); Invalidate(); });
                var btnZoomOut = CreateToolButton("🔍- Zoom Out", 96, () => { _zoomFactor = Math.Max(0.2f, _zoomFactor / 1.25f); Invalidate(); });
                var btnReset = CreateToolButton("⟲ Fit Window", 96, () => { _zoomFactor = 1.0f; _rotationDegrees = 0; _panOffset = PointF.Empty; Invalidate(); });
                var btnActual = CreateToolButton("1:1 Actual", 80, () => { _zoomFactor = GetActualScale(); _panOffset = PointF.Empty; Invalidate(); });
                var btnRotate = CreateToolButton("↷ Rotate 90°", 96, () => { _rotationDegrees = (_rotationDegrees + 90) % 360; Invalidate(); });
                var btnCopy = CreateToolButton("📋 Copy", 75, () =>
                {
                    try
                    {
                        Clipboard.SetImage(_image);
                        if (FindForm() is Form f) ToastNotification.Success(f, "Image copied to clipboard!");
                    }
                    catch { }
                });
                var btnSave = CreateToolButton("💾 Save As...", 95, () =>
                {
                    using var sfd = new SaveFileDialog
                    {
                        Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                        FileName = "preview_export.png",
                        Title = "Save Inspected Image"
                    };
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            var fmt = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Jpeg :
                                      sfd.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Bmp :
                                      System.Drawing.Imaging.ImageFormat.Png;
                            _image.Save(sfd.FileName, fmt);
                            if (FindForm() is Form f) ToastNotification.Success(f, "Image saved successfully!");
                        }
                        catch (Exception ex)
                        {
                            if (FindForm() is Form f) ToastNotification.Error(f, $"Failed to save: {ex.Message}");
                        }
                    }
                });

                topTools.Controls.Add(btnSave);
                topTools.Controls.Add(btnCopy);
                topTools.Controls.Add(btnRotate);
                topTools.Controls.Add(btnActual);
                topTools.Controls.Add(btnReset);
                topTools.Controls.Add(btnZoomOut);
                topTools.Controls.Add(btnZoomIn);
                Controls.Add(topTools);
            }

            private float GetActualScale()
            {
                var displayRect = new Rectangle(0, 40, Width, Height - 40);
                float fitRatio = Math.Min((float)displayRect.Width / _image.Width, (float)displayRect.Height / _image.Height);
                return fitRatio > 0 ? 1.0f / fitRatio : 1.0f;
            }

            private ZButton CreateToolButton(string text, int width, Action onClick)
            {
                var btn = new ZButton
                {
                    Text = text,
                    Dock = DockStyle.Left,
                    Width = width,
                    ButtonStyle = ZeroButtonStyle.Secondary,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold)
                };
                btn.Click += (s, e) => onClick();
                return btn;
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                if (e.Delta > 0)
                {
                    _zoomFactor = Math.Min(8.0f, _zoomFactor * 1.2f);
                }
                else
                {
                    _zoomFactor = Math.Max(0.2f, _zoomFactor / 1.2f);
                }
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left && e.Y >= 40)
                {
                    _isPanning = true;
                    _panStartMouse = e.Location;
                    _panStartOffset = _panOffset;
                    Cursor = Cursors.SizeAll;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_isPanning)
                {
                    _panOffset = new PointF(
                        _panStartOffset.X + (e.X - _panStartMouse.X),
                        _panStartOffset.Y + (e.Y - _panStartMouse.Y));
                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_isPanning)
                {
                    _isPanning = false;
                    Cursor = Cursors.Default;
                }
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);
                if (Math.Abs(_zoomFactor - 1.0f) < 0.05f)
                {
                    _zoomFactor = GetActualScale();
                }
                else
                {
                    _zoomFactor = 1.0f;
                    _panOffset = PointF.Empty;
                }
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // 1. CLEAR VIEWPORT SOLID BACKGROUND & DRAW CHECKERBOARD
                g.Clear(Color.FromArgb(15, 23, 42));

                int checkSize = 14;
                using (var darkCheckBrush = new SolidBrush(Color.FromArgb(22, 30, 50)))
                {
                    for (int y = 40; y < Height; y += checkSize * 2)
                    {
                        for (int x = 0; x < Width; x += checkSize * 2)
                        {
                            g.FillRectangle(darkCheckBrush, x, y, checkSize, checkSize);
                            g.FillRectangle(darkCheckBrush, x + checkSize, y + checkSize, checkSize, checkSize);
                        }
                    }
                }

                // 2. RENDER SCALED & ROTATED & PANNED IMAGE
                var displayRect = new Rectangle(0, 40, Width, Height - 40);
                float fitRatio = Math.Min((float)displayRect.Width / _image.Width, (float)displayRect.Height / _image.Height);
                float currentScale = fitRatio * _zoomFactor;

                float imgW = _image.Width * currentScale;
                float imgH = _image.Height * currentScale;

                float cx = displayRect.X + displayRect.Width / 2f + _panOffset.X;
                float cy = displayRect.Y + displayRect.Height / 2f + _panOffset.Y;

                g.TranslateTransform(cx, cy);
                g.RotateTransform(_rotationDegrees);
                g.DrawImage(_image, -imgW / 2f, -imgH / 2f, imgW, imgH);
                g.ResetTransform();

                // 3. DRAW BOTTOM HUD OVERLAY PILL
                string hudText = $"{_image.Width} × {_image.Height} px   |   Zoom: {(int)(_zoomFactor * 100)}%   |   Rotate: {_rotationDegrees}°   |   Drag to pan, wheel to zoom";
                using (var hudFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (var hudTextBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                {
                    var sz = g.MeasureString(hudText, hudFont);
                    float hudW = sz.Width + 24f;
                    float hudH = 26f;
                    float hudX = (Width - hudW) / 2f;
                    float hudY = Height - hudH - 10f;

                    var hudRect = new RectangleF(hudX, hudY, hudW, hudH);
                    using var hudBgBrush = new SolidBrush(Color.FromArgb(200, 15, 23, 42));
                    using var hudBorderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
                    using var path = ZeroUIConfig.CreateRoundedRectangle(new Rectangle((int)hudX, (int)hudY, (int)hudW, (int)hudH), 6);

                    g.FillPath(hudBgBrush, path);
                    g.DrawPath(hudBorderPen, path);

                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(hudText, hudFont, hudTextBrush, hudRect, sf);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Obsolete alias for <see cref="ZPictureEdit"/>.
    /// </summary>
    [Obsolete("ZeroImage is deprecated and will be removed in 5 release cycles. Please migrate to ZPictureEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroImage : ZPictureEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZPictureEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PictureEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZPictureEdit instead.")]
    [ToolboxItem(false)]
    public class PictureEdit : ZPictureEdit
    {
    }

    /// <summary>
    /// Backward-compatible alias for <see cref="ZPictureEdit"/>.
    /// </summary>
    [Obsolete("ZeroPictureEdit is deprecated. Use ZPictureEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroPictureEdit : ZPictureEdit { }

    #endregion
}
