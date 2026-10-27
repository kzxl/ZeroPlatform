using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Media;
using ZeroUI.Core.Theme;

namespace ZeroUI.WinForms.Media
{
    public partial class ZImageViewer
    {
        #region Paint Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_canvasColor);

            if (_image == null)
            {
                DrawEmptyState(g);
                return;
            }

            // Configure rendering quality
            if (_zoomFactor >= _pixelGridThreshold || _interpolation == ImageInterpolationQuality.NearestNeighbor)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
            }
            else
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
            }

            // 1. Draw Main Image with Transformations
            DrawMainImage(g);

            // 2. Draw Pixel Grid if Zoom is high
            if (_showPixelGrid && _zoomFactor >= _pixelGridThreshold)
            {
                DrawPixelGrid(g);
            }

            // 3. Draw MiniMap
            if (_showMiniMap)
            {
                DrawMiniMap(g);
            }

            // 4. Draw Status HUD
            if (_showStatusBar)
            {
                DrawStatusBar(g);
            }

            // 5. Draw Floating Toolbar
            if (_showToolbar)
            {
                DrawToolbar(g);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            using var font = new Font("Segoe UI", 11f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("⚡ ZeroUI Image Viewer\nDrag & drop image or load a file to begin", font, brush, ClientRectangle, sf);
        }

        private void DrawMainImage(Graphics g)
        {
            if (_image == null) return;

            var state = g.Save();

            g.TranslateTransform(_panOffset.X, _panOffset.Y);
            g.ScaleTransform(
                _zoomFactor * (_flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1),
                _zoomFactor * (_flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1));

            if (_rotation != ImageRotationAngle.Rotate0)
            {
                g.RotateTransform((float)_rotation);
            }

            float imgW = _image.Width;
            float imgH = _image.Height;
            g.DrawImage(_image, -imgW / 2f, -imgH / 2f, imgW, imgH);

            g.Restore(state);
        }

        private void DrawPixelGrid(Graphics g)
        {
            if (_image == null) return;

            PointF tl = ViewportToImage(new Point(0, 0));
            PointF br = ViewportToImage(new Point(Width, Height));

            int startX = Math.Max(0, (int)Math.Floor(Math.Min(tl.X, br.X)));
            int endX = Math.Min(_image.Width, (int)Math.Ceiling(Math.Max(tl.X, br.X)));
            int startY = Math.Max(0, (int)Math.Floor(Math.Min(tl.Y, br.Y)));
            int endY = Math.Min(_image.Height, (int)Math.Ceiling(Math.Max(tl.Y, br.Y)));

            using var gridPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f);

            for (int x = startX; x <= endX; x++)
            {
                PointF p1 = ImageToViewport(new PointF(x, startY));
                PointF p2 = ImageToViewport(new PointF(x, endY));
                g.DrawLine(gridPen, p1, p2);
            }

            for (int y = startY; y <= endY; y++)
            {
                PointF p1 = ImageToViewport(new PointF(startX, y));
                PointF p2 = ImageToViewport(new PointF(endX, y));
                g.DrawLine(gridPen, p1, p2);
            }
        }

        private void DrawMiniMap(Graphics g)
        {
            if (_image == null) return;

            float pad = 14f;
            float mapW = 140f;
            float mapH = 90f;

            float ratio = (float)_image.Width / Math.Max(1, _image.Height);
            if (ratio >= 1.5f) mapH = mapW / ratio;
            else mapW = mapH * ratio;

            RectangleF bounds = new RectangleF(Width - mapW - pad, Height - mapH - pad - (_showToolbar ? 44 : 0), mapW, mapH);

            using var bgBrush = new SolidBrush(Color.FromArgb(220, 20, 24, 34));
            using var borderPen = new Pen(Color.FromArgb(120, 56, 189, 248), 1f);
            g.FillRectangle(bgBrush, bounds);
            g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);

            // Draw thumbnail image
            RectangleF thumbRect = new RectangleF(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            g.DrawImage(_image, thumbRect);

            // Viewport rect
            PointF vTl = ViewportToImage(new Point(0, 0));
            PointF vBr = ViewportToImage(new Point(Width, Height));

            float nx1 = Math.Max(0f, Math.Min(1f, vTl.X / _image.Width));
            float ny1 = Math.Max(0f, Math.Min(1f, vTl.Y / _image.Height));
            float nx2 = Math.Max(0f, Math.Min(1f, vBr.X / _image.Width));
            float ny2 = Math.Max(0f, Math.Min(1f, vBr.Y / _image.Height));

            float vpX = thumbRect.X + Math.Min(nx1, nx2) * thumbRect.Width;
            float vpY = thumbRect.Y + Math.Min(ny1, ny2) * thumbRect.Height;
            float vpW = Math.Max(6f, Math.Abs(nx2 - nx1) * thumbRect.Width);
            float vpH = Math.Max(6f, Math.Abs(ny2 - ny1) * thumbRect.Height);

            using var vpFill = new SolidBrush(Color.FromArgb(40, 0, 229, 255));
            using var vpPen = new Pen(Color.FromArgb(220, 0, 229, 255), 1.5f);
            g.FillRectangle(vpFill, vpX, vpY, vpW, vpH);
            g.DrawRectangle(vpPen, vpX, vpY, vpW, vpH);
        }

        private void DrawStatusBar(Graphics g)
        {
            if (_image == null) return;

            float mp = (_image.Width * _image.Height) / 1_000_000f;
            string probe = _currentPixelCoord.X >= 0
                ? $" | X: {_currentPixelCoord.X} Y: {_currentPixelCoord.Y} | #{_currentHoverColor.R:X2}{_currentHoverColor.G:X2}{_currentHoverColor.B:X2}"
                : string.Empty;

            string text = $"{_image.Width} × {_image.Height} ({mp:F1} MP) | {Math.Round(_zoomFactor * 100):0}% [{_viewMode}]{probe}";

            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            SizeF sz = g.MeasureString(text, font);
            RectangleF bg = new RectangleF(12, 12, sz.Width + 24, sz.Height + 8);

            using var bgBrush = new SolidBrush(Color.FromArgb(200, 20, 24, 34));
            using var borderPen = new Pen(Color.FromArgb(120, 56, 189, 248), 1f);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));

            g.FillRectangle(bgBrush, bg);
            g.DrawRectangle(borderPen, bg.X, bg.Y, bg.Width, bg.Height);
            g.DrawString(text, font, textBrush, bg.X + 8, bg.Y + 4);

            if (_currentPixelCoord.X >= 0)
            {
                using var dotBrush = new SolidBrush(_currentHoverColor);
                g.FillEllipse(dotBrush, bg.Right - 14, bg.Y + bg.Height / 2f - 4, 8, 8);
                g.DrawEllipse(borderPen, bg.Right - 14, bg.Y + bg.Height / 2f - 4, 8, 8);
            }
        }

        private void DrawToolbar(Graphics g)
        {
            float barW = 340f;
            float barH = 34f;
            RectangleF bar = new RectangleF((Width - barW) / 2f, Height - barH - 10f, barW, barH);

            using var bgBrush = new SolidBrush(Color.FromArgb(200, 20, 24, 34));
            using var borderPen = new Pen(Color.FromArgb(120, 56, 189, 248), 1f);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var accentBrush = new SolidBrush(Color.FromArgb(0, 229, 255));
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.FillRectangle(bgBrush, bar);
            g.DrawRectangle(borderPen, bar.X, bar.Y, bar.Width, bar.Height);

            string[] items = { "−", "+", "Fit", "1:1", "↺", "↻", "⇄", "Grid", "Map", "Reset" };
            float itemW = barW / items.Length;

            for (int i = 0; i < items.Length; i++)
            {
                RectangleF btn = new RectangleF(bar.X + i * itemW, bar.Y, itemW, barH);
                bool hovered = btn.Contains(_currentMousePosition);

                if (hovered)
                {
                    using var hoverBrush = new SolidBrush(Color.FromArgb(60, 0, 229, 255));
                    g.FillRectangle(hoverBrush, btn);
                }

                g.DrawString(items[i], font, hovered ? accentBrush : textBrush, btn, sf);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        #endregion
    }
}
