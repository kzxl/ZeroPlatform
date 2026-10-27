using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public partial class ZImageViewer
    {
        #region Rendering Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Draw Canvas Background
            Brush bg = BackgroundBrush ?? DefaultCanvasBg;
            dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));

            if (Source == null)
            {
                DrawEmptyState(dc, w, h);
                return;
            }

            // 2. Draw Transformed Main Image
            DrawMainImage(dc, w, h);

            // 3. Draw Pixel Grid if Zoom is high enough
            if (ShowPixelGrid && Zoom >= PixelGridThreshold)
            {
                DrawPixelGrid(dc);
            }

            // 4. Draw Floating Loupe Magnifier
            if (ShowLoupe && _currentMousePosition.X >= 0 && _currentMousePosition.Y >= 0)
            {
                DrawLoupe(dc);
            }

            // 5. Draw Interactive MiniMap
            if (ShowMiniMap && Source != null)
            {
                DrawMiniMap(dc, w, h);
            }

            // 6. Draw Telemetry Status Badge
            if (ShowStatusBar && Source != null)
            {
                DrawStatusBar(dc);
            }

            // 7. Draw Floating HUD Toolbar
            if (ShowToolbar)
            {
                DrawToolbar(dc, w, h);
            }
        }

        private void DrawEmptyState(DrawingContext dc, double w, double h)
        {
            var text = new FormattedText(
                "⚡ ZeroUI Image Viewer\nDrag & drop image or load a file to begin",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _hudTypeface,
                15,
                HudMutedBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            text.TextAlignment = TextAlignment.Center;
            dc.DrawText(text, new Point(w / 2.0, h / 2.0 - 20));
        }

        private void DrawMainImage(DrawingContext dc, double w, double h)
        {
            if (Source == null) return;

            double imgW = Source.PixelWidth;
            double imgH = Source.PixelHeight;

            // Configure rendering quality based on zoom and property
            BitmapScalingMode scaling = (Zoom >= PixelGridThreshold || InterpolationQuality == ImageInterpolationQuality.NearestNeighbor)
                ? BitmapScalingMode.NearestNeighbor
                : BitmapScalingMode.HighQuality;

            RenderOptions.SetBitmapScalingMode(this, scaling);

            dc.PushTransform(new TranslateTransform(PanOffset.X, PanOffset.Y));
            dc.PushTransform(new ScaleTransform(
                Zoom * (Flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1),
                Zoom * (Flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1)));

            if (Rotation != ImageRotationAngle.Rotate0)
            {
                dc.PushTransform(new RotateTransform((double)Rotation));
            }

            Rect drawRect = new Rect(-imgW / 2.0, -imgH / 2.0, imgW, imgH);
            dc.DrawImage(Source, drawRect);

            if (Rotation != ImageRotationAngle.Rotate0) dc.Pop();
            dc.Pop(); // Scale
            dc.Pop(); // Translate
        }

        private void DrawPixelGrid(DrawingContext dc)
        {
            if (Source == null) return;

            double imgW = Source.PixelWidth;
            double imgH = Source.PixelHeight;

            // Transform viewport corners to image coordinates
            Point imgTopLeft = ViewportToImage(new Point(0, 0));
            Point imgBottomRight = ViewportToImage(new Point(ActualWidth, ActualHeight));

            int startX = Math.Max(0, (int)Math.Floor(Math.Min(imgTopLeft.X, imgBottomRight.X)));
            int endX = Math.Min((int)imgW, (int)Math.Ceiling(Math.Max(imgTopLeft.X, imgBottomRight.X)));
            int startY = Math.Max(0, (int)Math.Floor(Math.Min(imgTopLeft.Y, imgBottomRight.Y)));
            int endY = Math.Min((int)imgH, (int)Math.Ceiling(Math.Max(imgTopLeft.Y, imgBottomRight.Y)));

            // Draw vertical grid lines
            for (int x = startX; x <= endX; x++)
            {
                Point p1 = ImageToViewport(new Point(x, startY));
                Point p2 = ImageToViewport(new Point(x, endY));
                dc.DrawLine(PixelGridPen, p1, p2);
            }

            // Draw horizontal grid lines
            for (int y = startY; y <= endY; y++)
            {
                Point p1 = ImageToViewport(new Point(startX, y));
                Point p2 = ImageToViewport(new Point(endX, y));
                dc.DrawLine(PixelGridPen, p1, p2);
            }

            // If zoomed extremely close (>= 24x), draw subtle color / coord hints in each pixel
            if (Zoom >= 24.0 && (endX - startX) * (endY - startY) < 400)
            {
                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                for (int y = startY; y < endY; y++)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Point cellTl = ImageToViewport(new Point(x, y));
                        Point cellBr = ImageToViewport(new Point(x + 1, y + 1));
                        double cellW = cellBr.X - cellTl.X;
                        double cellH = cellBr.Y - cellTl.Y;

                        var text = new FormattedText(
                            $"{x},{y}",
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            _pixelTextTypeface,
                            Math.Max(8, Math.Min(11, cellH * 0.25)),
                            HudMutedBrush,
                            dpi);

                        text.TextAlignment = TextAlignment.Center;
                        dc.DrawText(text, new Point(cellTl.X + cellW / 2.0, cellTl.Y + cellH / 2.0 - text.Height / 2.0));
                    }
                }
            }
        }

        private void DrawMiniMap(DrawingContext dc, double w, double h)
        {
            if (Source == null) return;

            double pad = 16.0;
            double mapW = 150.0;
            double mapH = 100.0;

            double imgRatio = (double)Source.PixelWidth / Math.Max(1, Source.PixelHeight);
            if (imgRatio >= 1.5)
            {
                mapH = mapW / imgRatio;
            }
            else
            {
                mapW = mapH * imgRatio;
            }

            Rect mapBounds = new Rect(w - mapW - pad, h - mapH - pad - (ShowToolbar ? 48 : 0), mapW, mapH);

            // Background card with shadow and border
            dc.DrawRoundedRectangle(HudBgBrush, HudBorderPen, mapBounds, 6, 6);

            // Thumbnail inside
            Rect thumbRect = new Rect(mapBounds.X + 2, mapBounds.Y + 2, mapBounds.Width - 4, mapBounds.Height - 4);
            dc.DrawImage(Source, thumbRect);

            // Compute visible viewport region mapped onto thumbnail
            Point vTopLeft = ViewportToImage(new Point(0, 0));
            Point vBottomRight = ViewportToImage(new Point(w, h));

            double normX1 = Clamp(vTopLeft.X / Source.PixelWidth, 0, 1);
            double normY1 = Clamp(vTopLeft.Y / Source.PixelHeight, 0, 1);
            double normX2 = Clamp(vBottomRight.X / Source.PixelWidth, 0, 1);
            double normY2 = Clamp(vBottomRight.Y / Source.PixelHeight, 0, 1);

            double vpX = thumbRect.X + Math.Min(normX1, normX2) * thumbRect.Width;
            double vpY = thumbRect.Y + Math.Min(normY1, normY2) * thumbRect.Height;
            double vpW = Math.Max(6, Math.Abs(normX2 - normX1) * thumbRect.Width);
            double vpH = Math.Max(6, Math.Abs(normY2 - normY1) * thumbRect.Height);

            Rect viewportIndicator = new Rect(vpX, vpY, vpW, vpH);
            dc.DrawRectangle(MiniMapViewportFill, MiniMapViewportPen, viewportIndicator);
        }

        private void DrawLoupe(DrawingContext dc)
        {
            if (Source == null) return;

            Point center = _currentMousePosition;
            double radius = 64.0;
            double factor = LoupeFactor;

            var clipGeo = new EllipseGeometry(center, radius, radius);
            dc.PushClip(clipGeo);

            // Magnified background
            dc.DrawRectangle(DefaultCanvasBg, null, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));

            // Magnified image fragment
            Point imgCoord = ViewportToImage(center);
            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            dc.PushTransform(new ScaleTransform(Zoom * factor, Zoom * factor));
            dc.DrawImage(Source, new Rect(-imgCoord.X, -imgCoord.Y, Source.PixelWidth, Source.PixelHeight));
            dc.Pop();
            dc.Pop();

            // Crosshair inside loupe
            var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.0);
            dc.DrawLine(crossPen, new Point(center.X - 10, center.Y), new Point(center.X + 10, center.Y));
            dc.DrawLine(crossPen, new Point(center.X, center.Y - 10), new Point(center.X, center.Y + 10));

            dc.Pop(); // Pop clip

            // Loupe Outer Ring Glow & Border
            var ringPen = new Pen(AccentBrush, 2.5);
            dc.DrawEllipse(null, ringPen, center, radius, radius);
        }

        private void DrawStatusBar(DrawingContext dc)
        {
            if (Source == null) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double mp = (Source.PixelWidth * Source.PixelHeight) / 1_000_000.0;
            string probeInfo = _currentPixelCoord.X >= 0
                ? $" | X: {(int)_currentPixelCoord.X} Y: {(int)_currentPixelCoord.Y} | #{_currentHoverColor.R:X2}{_currentHoverColor.G:X2}{_currentHoverColor.B:X2}"
                : string.Empty;

            string info = $"{Source.PixelWidth} × {Source.PixelHeight} ({mp:F1} MP) | {Math.Round(Zoom * 100):0}% [{ViewMode}]{probeInfo}";

            var ft = new FormattedText(
                info,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _hudTypeface,
                11.5,
                HudTextBrush,
                dpi);

            Rect bgRect = new Rect(14, 14, ft.Width + 24, ft.Height + 10);
            dc.DrawRoundedRectangle(HudBgBrush, HudBorderPen, bgRect, 6, 6);

            // Color probe preview dot
            if (_currentPixelCoord.X >= 0)
            {
                var colorDot = new SolidColorBrush(_currentHoverColor);
                colorDot.Freeze();
                dc.DrawEllipse(colorDot, HudBorderPen, new Point(bgRect.Right - 14, bgRect.Top + bgRect.Height / 2.0), 5, 5);
            }

            dc.DrawText(ft, new Point(bgRect.X + 10, bgRect.Y + 5));
        }

        private void DrawToolbar(DrawingContext dc, double w, double h)
        {
            double barW = 380.0;
            double barH = 38.0;
            Rect barRect = new Rect((w - barW) / 2.0, h - barH - 14.0, barW, barH);

            // Floating translucent card
            dc.DrawRoundedRectangle(HudBgBrush, HudBorderPen, barRect, 19, 19);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            string[] buttons = { "−", "+", "Fit", "1:1", "↺", "↻", "⇄", "Grid", "Loupe", "Reset" };
            double btnW = barW / buttons.Length;

            for (int i = 0; i < buttons.Length; i++)
            {
                Rect btnRect = new Rect(barRect.X + i * btnW, barRect.Y, btnW, barH);
                bool hovered = btnRect.Contains(_currentMousePosition);

                if (hovered)
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 229, 255)), null, btnRect, 4, 4);
                }

                var ft = new FormattedText(
                    buttons[i],
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    _hudTypeface,
                    11.5,
                    hovered ? AccentBrush : HudTextBrush,
                    dpi);

                ft.TextAlignment = TextAlignment.Center;
                dc.DrawText(ft, new Point(btnRect.X + btnW / 2.0, btnRect.Y + (barH - ft.Height) / 2.0));
            }
        }

        #endregion
    }
}
