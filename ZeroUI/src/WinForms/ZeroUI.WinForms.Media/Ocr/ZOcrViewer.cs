using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroOcr.Core.Models;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Media
{
    /// <summary>
    /// Enterprise OCR visualization, token bounding box inspection, confidence analysis, and interactive ROI canvas (WinForms).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultProperty("Image")]
    public class ZOcrViewer : Control
    {
        private Bitmap? _image;
        private OcrResult? _result;
        private bool _showBoundingBoxes = true;
        private bool _showConfidenceBadges = true;
        private bool _showTextOverlay = false;
        private float _minConfidenceThreshold = 0.70f;
        private OcrWord? _selectedWord;
        private OcrLine? _selectedLine;
        private OcrRect? _selectedRoi;
        private bool _enableRoiSelection;
        private double _zoom = 1.0;
        private PointF _panOffset = new PointF(0, 0);

        private PointF _lastMousePos;
        private bool _isPanning;
        private bool _isDraggingRoi;
        private PointF _roiStartPoint;
        private PointF _roiCurrentPoint;
        private OcrWord? _hoveredWord;

        public ZOcrViewer()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Properties

        [Category("ZeroUI")]
        [Description("The source document bitmap.")]
        [DefaultValue(null)]
        public Bitmap? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    ZoomToFit();
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Alias for Image property.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap? Source
        {
            get => Image;
            set => Image = value;
        }

        [Category("ZeroUI")]
        [Description("Recognized OCR result model containing document blocks, lines, and words.")]
        [DefaultValue(null)]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OcrResult? Result
        {
            get => _result;
            set
            {
                if (_result != value)
                {
                    _result = value;
                    _selectedWord = null;
                    _selectedLine = null;
                    _selectedRoi = null;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Controls whether bounding boxes are drawn around recognized words.")]
        [DefaultValue(true)]
        public bool ShowBoundingBoxes
        {
            get => _showBoundingBoxes;
            set
            {
                if (_showBoundingBoxes != value)
                {
                    _showBoundingBoxes = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Controls whether confidence percentage badges are displayed.")]
        [DefaultValue(true)]
        public bool ShowConfidenceBadges
        {
            get => _showConfidenceBadges;
            set
            {
                if (_showConfidenceBadges != value)
                {
                    _showConfidenceBadges = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Controls whether recognized text is rendered directly over the tokens.")]
        [DefaultValue(false)]
        public bool ShowTextOverlay
        {
            get => _showTextOverlay;
            set
            {
                if (_showTextOverlay != value)
                {
                    _showTextOverlay = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Minimum confidence threshold for high/medium confidence evaluation (0.0 to 1.0).")]
        [DefaultValue(0.70f)]
        public float MinConfidenceThreshold
        {
            get => _minConfidenceThreshold;
            set
            {
                _minConfidenceThreshold = Math.Max(0.0f, Math.Min(1.0f, value));
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Currently selected OCR word token.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OcrWord? SelectedWord
        {
            get => _selectedWord;
            set
            {
                if (_selectedWord != value)
                {
                    _selectedWord = value;
                    WordSelected?.Invoke(this, _selectedWord);
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Currently selected OCR line.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OcrLine? SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (_selectedLine != value)
                {
                    _selectedLine = value;
                    LineSelected?.Invoke(this, _selectedLine);
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Currently selected Region of Interest (ROI) in image coordinates.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OcrRect? SelectedRoi
        {
            get => _selectedRoi;
            set
            {
                if (_selectedRoi != value)
                {
                    _selectedRoi = value;
                    if (value.HasValue)
                    {
                        RoiSelected?.Invoke(this, value.Value);
                    }
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Enables or disables interactive mouse drag selection of Region of Interest (ROI).")]
        [DefaultValue(false)]
        public bool EnableRoiSelection
        {
            get => _enableRoiSelection;
            set => _enableRoiSelection = value;
        }

        [Category("ZeroUI")]
        [Description("Current zoom ratio (0.05 to 32.0).")]
        [DefaultValue(1.0)]
        public double Zoom
        {
            get => _zoom;
            set
            {
                double clamped = Math.Max(0.05, Math.Min(32.0, value));
                if (Math.Abs(_zoom - clamped) > 0.001)
                {
                    _zoom = clamped;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Current pan translation offset.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PointF PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                Invalidate();
            }
        }

        #endregion

        #region Events

        public event EventHandler<OcrWord?>? WordSelected;
        public event EventHandler<OcrLine?>? LineSelected;
        public event EventHandler<OcrRect>? RoiSelected;

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.Clear(Color.FromArgb(17, 24, 39)); // Deep dark obsidian surface

            if (_image == null || Width <= 0 || Height <= 0)
            {
                DrawEmptyState(g);
                return;
            }

            try
            {
                int imgW = _image.Width;
                int imgH = _image.Height;

                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                var state = g.Save();

                // Center-anchored zoom & pan transform
                float cx = Width / 2.0f;
                float cy = Height / 2.0f;

                g.TranslateTransform(cx + _panOffset.X, cy + _panOffset.Y);
                g.ScaleTransform((float)_zoom, (float)_zoom);
                g.TranslateTransform(-cx, -cy);

                float imgLeft = (Width - imgW) / 2.0f;
                float imgTop = (Height - imgH) / 2.0f;

                g.DrawImage(_image, imgLeft, imgTop, imgW, imgH);

                // Render OCR Token Overlays
                if (_result != null && _result.Words.Count > 0)
                {
                    RenderOcrOverlay(g, imgLeft, imgTop);
                }

                // Render Selected ROI Box (in image space under zoom/pan transform)
                if (!_isDraggingRoi && _selectedRoi.HasValue && !_selectedRoi.Value.IsEmpty)
                {
                    var roi = _selectedRoi.Value;
                    var roiRect = new RectangleF(imgLeft + roi.X, imgTop + roi.Y, roi.Width, roi.Height);

                    using var roiBrush = new SolidBrush(Color.FromArgb(40, 245, 158, 11));
                    using var roiPen = new Pen(Color.FromArgb(230, 245, 158, 11), 2.0f / (float)_zoom)
                    {
                        DashStyle = DashStyle.Dash
                    };
                    g.FillRectangle(roiBrush, roiRect);
                    g.DrawRectangle(roiPen, roiRect.X, roiRect.Y, roiRect.Width, roiRect.Height);
                }

                g.Restore(state);

                // Render interactive ROI drag rectangle in SCREEN coordinates (1:1 with mouse cursor!)
                if (_isDraggingRoi)
                {
                    float rMinX = Math.Min(_roiStartPoint.X, _roiCurrentPoint.X);
                    float rMinY = Math.Min(_roiStartPoint.Y, _roiCurrentPoint.Y);
                    float rW = Math.Abs(_roiCurrentPoint.X - _roiStartPoint.X);
                    float rH = Math.Abs(_roiCurrentPoint.Y - _roiStartPoint.Y);

                    if (rW > 1 && rH > 1)
                    {
                        using var dragBrush = new SolidBrush(Color.FromArgb(35, 0, 229, 255));
                        using var dragPen = new Pen(Color.FromArgb(255, 0, 229, 255), 1.5f)
                        {
                            DashStyle = DashStyle.Dash
                        };
                        g.FillRectangle(dragBrush, rMinX, rMinY, rW, rH);
                        g.DrawRectangle(dragPen, rMinX, rMinY, rW, rH);

                        // Draw real-time size badge near the cursor
                        string sizeText = $"{(int)rW} × {(int)rH} px";
                        using var font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
                        var textSz = g.MeasureString(sizeText, font);
                        float badgeX = Math.Min(Width - textSz.Width - 10, Math.Max(10, rMinX + rW + 6));
                        float badgeY = Math.Min(Height - textSz.Height - 10, Math.Max(10, rMinY + rH + 6));

                        using var badgeBg = new SolidBrush(Color.FromArgb(200, 15, 23, 42));
                        using var textBrush = new SolidBrush(Color.White);
                        g.FillRectangle(badgeBg, badgeX, badgeY, textSz.Width + 6, textSz.Height + 2);
                        g.DrawString(sizeText, font, textBrush, badgeX + 3, badgeY + 1);
                    }
                }

                // Render HUD badge in screen coordinates
                RenderHud(g);
            }
            catch (Exception)
            {
                // Suppress GDI+ rendering anomalies if image was modified/disposed concurrently
            }
        }

        private void RenderOcrOverlay(Graphics g, float imgLeft, float imgTop)
        {
            if (_result == null) return;

            using var greenPen = new Pen(Color.FromArgb(220, 34, 197, 94), 1.0f / (float)_zoom);
            using var amberPen = new Pen(Color.FromArgb(220, 234, 179, 8), 1.0f / (float)_zoom);
            using var crimsonPen = new Pen(Color.FromArgb(220, 239, 68, 68), 1.0f / (float)_zoom);
            using var hoverBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
            using var selectedBrush = new SolidBrush(Color.FromArgb(100, 0, 229, 255));
            using var selectedPen = new Pen(Color.FromArgb(0, 229, 255), 2.0f / (float)_zoom);

            using var badgeFont = new Font("Segoe UI", Math.Max(7.0f, (float)(9.0 / Math.Sqrt(_zoom))), FontStyle.Bold);
            using var badgeBgBrush = new SolidBrush(Color.FromArgb(190, 15, 23, 42));
            using var textBrush = new SolidBrush(Color.White);

            foreach (var word in _result.Words)
            {
                var box = word.BoundingBox;
                var wordRect = new RectangleF(imgLeft + box.X, imgTop + box.Y, box.Width, box.Height);

                Pen strokePen = word.Confidence switch
                {
                    >= 0.85f => greenPen,
                    >= 0.70f => amberPen,
                    _ => crimsonPen
                };

                bool isSelected = _selectedWord == word;
                bool isHovered = _hoveredWord == word;

                if (_showBoundingBoxes || isSelected || isHovered)
                {
                    if (isSelected)
                    {
                        g.FillRectangle(selectedBrush, wordRect);
                        g.DrawRectangle(selectedPen, wordRect.X, wordRect.Y, wordRect.Width, wordRect.Height);
                    }
                    else if (isHovered)
                    {
                        g.FillRectangle(hoverBrush, wordRect);
                        g.DrawRectangle(strokePen, wordRect.X, wordRect.Y, wordRect.Width, wordRect.Height);
                    }
                    else
                    {
                        g.DrawRectangle(strokePen, wordRect.X, wordRect.Y, wordRect.Width, wordRect.Height);
                    }
                }

                // Render confidence badge
                if (_showConfidenceBadges && _zoom >= 0.5)
                {
                    string badge = $"{(int)(word.Confidence * 100)}%";
                    var badgeSize = g.MeasureString(badge, badgeFont);
                    float badgeY = Math.Max(imgTop, wordRect.Top - badgeSize.Height);

                    g.FillRectangle(badgeBgBrush, wordRect.Left, badgeY, badgeSize.Width, badgeSize.Height);
                    using var confTextBrush = new SolidBrush(strokePen.Color);
                    g.DrawString(badge, badgeFont, confTextBrush, wordRect.Left, badgeY);
                }

                // Render text overlay
                if (_showTextOverlay)
                {
                    using var overlayFont = new Font("Segoe UI", Math.Max(8.0f, Math.Min(box.Height * 0.7f, 13.0f)), FontStyle.Regular);
                    g.DrawString(word.Text, overlayFont, textBrush, wordRect.Left + 1, wordRect.Top + 1);
                }
            }
        }

        private void RenderHud(Graphics g)
        {
            string hudText = $"Zoom: {(int)(_zoom * 100)}% | Tokens: {_result?.Words.Count ?? 0} | Lines: {_result?.Lines.Count ?? 0}";
            if (_selectedWord != null)
            {
                hudText += $" | Selected: '{_selectedWord.Text}' (Conf: {(int)(_selectedWord.Confidence * 100)}%)";
            }

            using var hudFont = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            var textSize = g.MeasureString(hudText, hudFont);

            var bgRect = new RectangleF(10, Height - 28, textSize.Width + 16, 22);
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 15, 23, 42));
            using var borderPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1.0f);
            using var textBrush = new SolidBrush(Color.FromArgb(156, 163, 175));

            g.FillRectangle(bgBrush, bgRect);
            g.DrawRectangle(borderPen, bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);
            g.DrawString(hudText, hudFont, textBrush, 18, Height - 25);
        }

        private void DrawEmptyState(Graphics g)
        {
            using var emptyFont = new Font("Segoe UI", 12.0f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.FromArgb(107, 114, 128));
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("No OCR Document Loaded", emptyFont, brush, ClientRectangle, sf);
        }

        #endregion

        #region User Interaction

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            Zoom = Math.Max(0.05, Math.Min(32.0, _zoom * zoomFactor));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Space) != 0))
            {
                _isPanning = true;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (_enableRoiSelection)
                {
                    _isDraggingRoi = true;
                    _roiStartPoint = e.Location;
                    _roiCurrentPoint = e.Location;
                    return;
                }

                // Hit test for word selection
                var word = HitTestWord(e.Location);
                SelectedWord = word;
                if (word != null)
                {
                    SelectedLine = _result?.Lines.FirstOrDefault(l => l.Words.Contains(word));
                    if (SelectedLine != null)
                    {
                        LineSelected?.Invoke(this, SelectedLine);
                    }
                }
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isPanning)
            {
                float dx = e.X - _lastMousePos.X;
                float dy = e.Y - _lastMousePos.Y;
                _panOffset = new PointF(_panOffset.X + dx, _panOffset.Y + dy);
                _lastMousePos = e.Location;
                Invalidate();
                return;
            }

            if (_isDraggingRoi)
            {
                _roiCurrentPoint = e.Location;
                Invalidate();
                return;
            }

            var hit = HitTestWord(e.Location);
            if (_hoveredWord != hit)
            {
                _hoveredWord = hit;
                Cursor = hit != null ? Cursors.Hand : Cursors.Default;
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
                return;
            }

            if (_isDraggingRoi)
            {
                _isDraggingRoi = false;

                if (_image != null)
                {
                    var imgP1 = ScreenToImagePoint(_roiStartPoint);
                    var imgP2 = ScreenToImagePoint(_roiCurrentPoint);

                    float minX = Math.Max(0, Math.Min(imgP1.X, imgP2.X));
                    float minY = Math.Max(0, Math.Min(imgP1.Y, imgP2.Y));
                    float maxX = Math.Min(_image.Width, Math.Max(imgP1.X, imgP2.X));
                    float maxY = Math.Min(_image.Height, Math.Max(imgP1.Y, imgP2.Y));

                    if (maxX > minX + 2 && maxY > minY + 2)
                    {
                        var roi = new OcrRect(minX, minY, maxX - minX, maxY - minY);
                        SelectedRoi = roi;
                        RoiSelected?.Invoke(this, roi);
                    }
                }
                Invalidate();
            }
        }

        private PointF ScreenToImagePoint(PointF screenPoint)
        {
            if (_image == null) return screenPoint;

            float cx = Width / 2.0f;
            float cy = Height / 2.0f;

            float unscaledX = (float)((screenPoint.X - cx - _panOffset.X) / _zoom + cx);
            float unscaledY = (float)((screenPoint.Y - cy - _panOffset.Y) / _zoom + cy);

            float imgLeft = (Width - _image.Width) / 2.0f;
            float imgTop = (Height - _image.Height) / 2.0f;

            return new PointF(unscaledX - imgLeft, unscaledY - imgTop);
        }

        private OcrWord? HitTestWord(PointF screenPoint)
        {
            if (_image == null || _result == null || _result.Words.Count == 0) return null;

            var imgPt = ScreenToImagePoint(screenPoint);

            foreach (var word in _result.Words)
            {
                if (word.BoundingBox.Contains(imgPt.X, imgPt.Y))
                {
                    return word;
                }
            }

            return null;
        }

        #endregion

        #region Public API Methods

        /// <summary>
        /// Resets the pan offset and recalculates zoom to fit the image perfectly within the canvas viewport.
        /// </summary>
        public void ZoomToFit()
        {
            if (_image == null || Width <= 0 || Height <= 0) return;

            _panOffset = new PointF(0, 0);
            double fitScale = Math.Min((double)Width / _image.Width, (double)Height / _image.Height) * 0.95;
            Zoom = Math.Max(0.05, Math.Min(10.0, fitScale));
            Invalidate();
        }

        /// <summary>
        /// Returns all recognized text across the entire document.
        /// </summary>
        public string GetAllText() => _result?.Text ?? string.Empty;

        /// <summary>
        /// Returns text of the currently selected word or line.
        /// </summary>
        public string? GetSelectedText() => _selectedWord?.Text ?? _selectedLine?.Text;

        /// <summary>
        /// Copies the currently selected OCR text to the system clipboard.
        /// </summary>
        public void CopySelectedText()
        {
            var text = GetSelectedText() ?? GetAllText();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        /// <summary>
        /// Filters and returns all OCR words contained within or intersecting the specified Region of Interest.
        /// </summary>
        public IReadOnlyList<OcrWord> GetWordsInRoi(OcrRect roi)
        {
            if (_result == null || _result.Words.Count == 0 || roi.IsEmpty)
            {
                return Array.Empty<OcrWord>();
            }

            return _result.Words.Where(w => roi.IntersectsWith(w.BoundingBox)).ToList();
        }

        /// <summary>
        /// Crops and returns a new Bitmap from the document image based on the specified Region of Interest (or active SelectedRoi).
        /// </summary>
        public Bitmap? GetCroppedBitmap(OcrRect? roi = null)
        {
            var target = roi ?? _selectedRoi;
            if (_image == null || !target.HasValue || target.Value.Width <= 1 || target.Value.Height <= 1)
            {
                return null;
            }

            try
            {
                int imgW = _image.Width;
                int imgH = _image.Height;

                var r = target.Value;
                int x = Math.Max(0, (int)Math.Round(r.X));
                int y = Math.Max(0, (int)Math.Round(r.Y));
                int w = Math.Min(imgW - x, (int)Math.Round(r.Width));
                int h = Math.Min(imgH - y, (int)Math.Round(r.Height));

                if (w <= 0 || h <= 0) return null;

                var cropRect = new Rectangle(x, y, w, h);
                var cropped = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(cropped))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(_image, new Rectangle(0, 0, w, h), cropRect, GraphicsUnit.Pixel);
                }
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZOcrViewer] GetCroppedBitmap error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears all active token and ROI selections.
        /// </summary>
        public void ClearSelection()
        {
            SelectedWord = null;
            SelectedLine = null;
            SelectedRoi = null;
            Invalidate();
        }

        #endregion

        #region Theme & Cleanup

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    #region Backward Compatibility Shims

    /// <summary>
    /// Backward compatibility alias for <see cref="ZOcrViewer"/> (OcrRoiBoxControl).
    /// </summary>
    [Obsolete("OcrRoiBoxControl is deprecated. Please migrate to ZOcrViewer instead.")]
    [ToolboxItem(false)]
    public class OcrRoiBoxControl : ZOcrViewer { }

    /// <summary>
    /// Backward compatibility alias for <see cref="ZOcrViewer"/> (ZeroOcrViewer).
    /// </summary>
    [Obsolete("ZeroOcrViewer is deprecated. Please migrate to ZOcrViewer instead.")]
    [ToolboxItem(false)]
    public class ZeroOcrViewer : ZOcrViewer { }

    #endregion
}
