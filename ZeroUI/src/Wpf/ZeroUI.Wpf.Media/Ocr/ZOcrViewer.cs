using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Enterprise OCR visualization, token bounding box inspection, confidence analysis, and interactive ROI canvas (WPF).
    /// </summary>
    public class ZOcrViewer : FrameworkElement
    {
        private Point _lastMousePos;
        private bool _isPanning;
        private bool _isDraggingRoi;
        private Point _roiStartPoint;
        private Point _roiCurrentPoint;
        private OcrWord? _hoveredWord;

        private static readonly Typeface OverlayTypeface = new(new FontFamily("Segoe UI, Consolas, sans-serif"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        static ZOcrViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZOcrViewer), new FrameworkPropertyMetadata(typeof(ZOcrViewer)));
            ClipToBoundsProperty.OverrideMetadata(typeof(ZOcrViewer), new FrameworkPropertyMetadata(true));
            FocusableProperty.OverrideMetadata(typeof(ZOcrViewer), new FrameworkPropertyMetadata(true));
        }

        public ZOcrViewer()
        {
            Loaded += (s, e) => InvalidateVisual();
        }

        #region Dependency Properties

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(BitmapSource), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

        public static readonly DependencyProperty ResultProperty =
            DependencyProperty.Register(nameof(Result), typeof(OcrResult), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowBoundingBoxesProperty =
            DependencyProperty.Register(nameof(ShowBoundingBoxes), typeof(bool), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowConfidenceBadgesProperty =
            DependencyProperty.Register(nameof(ShowConfidenceBadges), typeof(bool), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowTextOverlayProperty =
            DependencyProperty.Register(nameof(ShowTextOverlay), typeof(bool), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinConfidenceThresholdProperty =
            DependencyProperty.Register(nameof(MinConfidenceThreshold), typeof(float), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(0.70f, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectedWordProperty =
            DependencyProperty.Register(nameof(SelectedWord), typeof(OcrWord), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedWordChanged));

        public static readonly DependencyProperty SelectedLineProperty =
            DependencyProperty.Register(nameof(SelectedLine), typeof(OcrLine), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectedRoiProperty =
            DependencyProperty.Register(nameof(SelectedRoi), typeof(OcrRect?), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EnableRoiSelectionProperty =
            DependencyProperty.Register(nameof(EnableRoiSelection), typeof(bool), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PanOffsetProperty =
            DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(ZOcrViewer),
                new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

        public BitmapSource? Source
        {
            get => (BitmapSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public OcrResult? Result
        {
            get => (OcrResult?)GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }

        public bool ShowBoundingBoxes
        {
            get => (bool)GetValue(ShowBoundingBoxesProperty);
            set => SetValue(ShowBoundingBoxesProperty, value);
        }

        public bool ShowConfidenceBadges
        {
            get => (bool)GetValue(ShowConfidenceBadgesProperty);
            set => SetValue(ShowConfidenceBadgesProperty, value);
        }

        public bool ShowTextOverlay
        {
            get => (bool)GetValue(ShowTextOverlayProperty);
            set => SetValue(ShowTextOverlayProperty, value);
        }

        public float MinConfidenceThreshold
        {
            get => (float)GetValue(MinConfidenceThresholdProperty);
            set => SetValue(MinConfidenceThresholdProperty, value);
        }

        public OcrWord? SelectedWord
        {
            get => (OcrWord?)GetValue(SelectedWordProperty);
            set => SetValue(SelectedWordProperty, value);
        }

        public OcrLine? SelectedLine
        {
            get => (OcrLine?)GetValue(SelectedLineProperty);
            set => SetValue(SelectedLineProperty, value);
        }

        public OcrRect? SelectedRoi
        {
            get => (OcrRect?)GetValue(SelectedRoiProperty);
            set => SetValue(SelectedRoiProperty, value);
        }

        public bool EnableRoiSelection
        {
            get => (bool)GetValue(EnableRoiSelectionProperty);
            set => SetValue(EnableRoiSelectionProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, Math.Max(0.05, Math.Min(32.0, value)));
        }

        public Point PanOffset
        {
            get => (Point)GetValue(PanOffsetProperty);
            set => SetValue(PanOffsetProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZOcrViewer viewer)
            {
                viewer.ZoomToFit();
            }
        }

        private static void OnSelectedWordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZOcrViewer viewer && e.NewValue is OcrWord word)
            {
                viewer.WordSelected?.Invoke(viewer, word);
            }
        }

        #endregion

        #region Events

        public event EventHandler<OcrWord?>? WordSelected;
        public event EventHandler<OcrLine?>? LineSelected;
        public event EventHandler<OcrRect>? RoiSelected;

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Dark surface background
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Source == null || ActualWidth <= 0 || ActualHeight <= 0)
            {
                DrawEmptyState(dc);
                return;
            }

            var group = new TransformGroup();
            group.Children.Add(new TranslateTransform(PanOffset.X, PanOffset.Y));
            group.Children.Add(new ScaleTransform(Zoom, Zoom, ActualWidth / 2.0, ActualHeight / 2.0));
            dc.PushTransform(group);

            // Draw image centered in unzoomed space
            double imgLeft = (ActualWidth - Source.PixelWidth) / 2.0;
            double imgTop = (ActualHeight - Source.PixelHeight) / 2.0;
            var imgRect = new Rect(imgLeft, imgTop, Source.PixelWidth, Source.PixelHeight);

            dc.DrawImage(Source, imgRect);

            // Render OCR Token Overlay
            if (Result != null && Result.Words.Count > 0)
            {
                RenderOcrOverlay(dc, imgLeft, imgTop);
            }

            // Render Selected ROI Box
            if (SelectedRoi.HasValue && !SelectedRoi.Value.IsEmpty)
            {
                var roi = SelectedRoi.Value;
                var roiScreenRect = new Rect(imgLeft + roi.X, imgTop + roi.Y, roi.Width, roi.Height);
                var roiPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 245, 158, 11)), 2.0 / Zoom)
                {
                    DashStyle = DashStyles.Dash
                };
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)), roiPen, roiScreenRect);
            }

            // Render interactive ROI drag rectangle
            if (_isDraggingRoi)
            {
                var rMinX = Math.Min(_roiStartPoint.X, _roiCurrentPoint.X);
                var rMinY = Math.Min(_roiStartPoint.Y, _roiCurrentPoint.Y);
                var rW = Math.Abs(_roiCurrentPoint.X - _roiStartPoint.X);
                var rH = Math.Abs(_roiCurrentPoint.Y - _roiStartPoint.Y);
                var dragPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 229, 255)), 1.5 / Zoom)
                {
                    DashStyle = DashStyles.Dot
                };
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 0, 229, 255)), dragPen, new Rect(rMinX, rMinY, rW, rH));
            }

            dc.Pop();

            // Render HUD overlay (Zoom level, word count)
            RenderHud(dc);
        }

        private void RenderOcrOverlay(DrawingContext dc, double imgLeft, double imgTop)
        {
            if (Result == null) return;

            var greenBrush = new SolidColorBrush(Color.FromArgb(220, 34, 197, 94));
            var amberBrush = new SolidColorBrush(Color.FromArgb(220, 234, 179, 8));
            var crimsonBrush = new SolidColorBrush(Color.FromArgb(220, 239, 68, 68));
            var hoverBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            var selectedBrush = new SolidColorBrush(Color.FromArgb(100, 0, 229, 255));
            var selectedBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 255)), 2.0 / Zoom);

            foreach (var word in Result.Words)
            {
                var box = word.BoundingBox;
                var wordRect = new Rect(imgLeft + box.X, imgTop + box.Y, box.Width, box.Height);

                // Pick color by confidence
                Brush strokeBrush = word.Confidence switch
                {
                    >= 0.85f => greenBrush,
                    >= 0.70f => amberBrush,
                    _ => crimsonBrush
                };

                bool isSelected = SelectedWord == word;
                bool isHovered = _hoveredWord == word;

                if (ShowBoundingBoxes || isSelected || isHovered)
                {
                    Brush? fillBrush = null;
                    if (isSelected) fillBrush = selectedBrush;
                    else if (isHovered) fillBrush = hoverBrush;

                    var pen = isSelected ? selectedBorderPen : new Pen(strokeBrush, 1.0 / Zoom);
                    dc.DrawRectangle(fillBrush, pen, wordRect);
                }

                // Render confidence badge
                if (ShowConfidenceBadges && Zoom >= 0.5)
                {
                    string badgeText = $"{(int)(word.Confidence * 100)}%";
                    var ft = new FormattedText(
                        badgeText,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        OverlayTypeface,
                        Math.Max(8.0, 10.0 / Math.Sqrt(Zoom)),
                        strokeBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(ft, new Point(wordRect.Left, Math.Max(imgTop, wordRect.Top - ft.Height)));
                }

                // Render text overlay
                if (ShowTextOverlay)
                {
                    var ft = new FormattedText(
                        word.Text,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        OverlayTypeface,
                        Math.Max(9.0, Math.Min(box.Height * 0.8, 14.0)),
                        new SolidColorBrush(Colors.White),
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(ft, new Point(wordRect.Left + 2, wordRect.Top + 1));
                }
            }
        }

        private void RenderHud(DrawingContext dc)
        {
            string hudText = $"Zoom: {(int)(Zoom * 100)}% | Tokens: {Result?.Words.Count ?? 0} | Lines: {Result?.Lines.Count ?? 0}";
            if (SelectedWord != null)
            {
                hudText += $" | Selected: '{SelectedWord.Text}' (Conf: {(int)(SelectedWord.Confidence * 100)}%)";
            }

            var ft = new FormattedText(
                hudText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                OverlayTypeface,
                11.0,
                new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var bgRect = new Rect(10, ActualHeight - 28, ft.Width + 16, 22);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)), null, bgRect, 4, 4);
            dc.DrawText(ft, new Point(18, ActualHeight - 25));
        }

        private void DrawEmptyState(DrawingContext dc)
        {
            var ft = new FormattedText(
                "No OCR Document Loaded",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                OverlayTypeface,
                14.0,
                new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2.0, (ActualHeight - ft.Height) / 2.0));
        }

        #endregion

        #region User Interaction

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            Zoom = Math.Max(0.05, Math.Min(32.0, Zoom * zoomFactor));
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var pos = e.GetPosition(this);
            _lastMousePos = pos;

            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
            {
                _isPanning = true;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                if (EnableRoiSelection)
                {
                    _isDraggingRoi = true;
                    _roiStartPoint = pos;
                    _roiCurrentPoint = pos;
                    CaptureMouse();
                    return;
                }

                // Hit test for word selection
                var word = HitTestWord(pos);
                SelectedWord = word;
                if (word != null)
                {
                    // Find containing line
                    SelectedLine = Result?.Lines.FirstOrDefault(l => l.Words.Contains(word));
                    if (SelectedLine != null)
                    {
                        LineSelected?.Invoke(this, SelectedLine);
                    }
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);

            if (_isPanning)
            {
                var delta = pos - _lastMousePos;
                PanOffset = new Point(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
                _lastMousePos = pos;
                InvalidateVisual();
                return;
            }

            if (_isDraggingRoi)
            {
                _roiCurrentPoint = pos;
                InvalidateVisual();
                return;
            }

            // Word hover hit-testing
            var hit = HitTestWord(pos);
            if (_hoveredWord != hit)
            {
                _hoveredWord = hit;
                Cursor = hit != null ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                return;
            }

            if (_isDraggingRoi)
            {
                _isDraggingRoi = false;
                ReleaseMouseCapture();

                // Compute ROI in image coordinates
                if (Source != null)
                {
                    var imgP1 = ScreenToImagePoint(_roiStartPoint);
                    var imgP2 = ScreenToImagePoint(_roiCurrentPoint);

                    float minX = (float)Math.Max(0, Math.Min(imgP1.X, imgP2.X));
                    float minY = (float)Math.Max(0, Math.Min(imgP1.Y, imgP2.Y));
                    float maxX = (float)Math.Min(Source.PixelWidth, Math.Max(imgP1.X, imgP2.X));
                    float maxY = (float)Math.Min(Source.PixelHeight, Math.Max(imgP1.Y, imgP2.Y));

                    if (maxX > minX + 2 && maxY > minY + 2)
                    {
                        var roi = new OcrRect(minX, minY, maxX - minX, maxY - minY);
                        SelectedRoi = roi;
                        RoiSelected?.Invoke(this, roi);
                    }
                }
                InvalidateVisual();
            }
        }

        private Point ScreenToImagePoint(Point screenPoint)
        {
            if (Source == null) return screenPoint;

            double cx = ActualWidth / 2.0;
            double cy = ActualHeight / 2.0;

            // Invert scale & pan
            double unscaledX = (screenPoint.X - cx) / Zoom + cx - PanOffset.X;
            double unscaledY = (screenPoint.Y - cy) / Zoom + cy - PanOffset.Y;

            double imgLeft = (ActualWidth - Source.PixelWidth) / 2.0;
            double imgTop = (ActualHeight - Source.PixelHeight) / 2.0;

            return new Point(unscaledX - imgLeft, unscaledY - imgTop);
        }

        private OcrWord? HitTestWord(Point screenPoint)
        {
            if (Source == null || Result == null || Result.Words.Count == 0) return null;

            var imgPt = ScreenToImagePoint(screenPoint);
            float px = (float)imgPt.X;
            float py = (float)imgPt.Y;

            foreach (var word in Result.Words)
            {
                if (word.BoundingBox.Contains(px, py))
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
            if (Source == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            PanOffset = new Point(0, 0);
            double fitScale = Math.Min(ActualWidth / Source.PixelWidth, ActualHeight / Source.PixelHeight) * 0.95;
            Zoom = Math.Max(0.05, Math.Min(10.0, fitScale));
            InvalidateVisual();
        }

        /// <summary>
        /// Returns all recognized text across the entire document.
        /// </summary>
        public string GetAllText() => Result?.Text ?? string.Empty;

        /// <summary>
        /// Returns text of the currently selected word or line.
        /// </summary>
        public string? GetSelectedText() => SelectedWord?.Text ?? SelectedLine?.Text;

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
            if (Result == null || Result.Words.Count == 0 || roi.IsEmpty)
            {
                return Array.Empty<OcrWord>();
            }

            return Result.Words.Where(w => roi.IntersectsWith(w.BoundingBox)).ToList();
        }

        /// <summary>
        /// Clears all active token and ROI selections.
        /// </summary>
        public void ClearSelection()
        {
            SelectedWord = null;
            SelectedLine = null;
            SelectedRoi = null;
            InvalidateVisual();
        }

        #endregion
    }

    #region Backward Compatibility Shims

    /// <summary>
    /// Backward compatibility alias for <see cref="ZOcrViewer"/> (OcrRoiBoxControl).
    /// </summary>
    [Obsolete("OcrRoiBoxControl is deprecated. Please migrate to ZOcrViewer instead.")]
    public class OcrRoiBoxControl : ZOcrViewer { }

    /// <summary>
    /// Backward compatibility alias for <see cref="ZOcrViewer"/> (ZeroOcrViewer).
    /// </summary>
    [Obsolete("ZeroOcrViewer is deprecated. Please migrate to ZOcrViewer instead.")]
    public class ZeroOcrViewer : ZOcrViewer { }

    #endregion
}
