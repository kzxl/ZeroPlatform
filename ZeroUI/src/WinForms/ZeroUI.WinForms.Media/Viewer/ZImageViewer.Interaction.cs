using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Media;

namespace ZeroUI.WinForms.Media
{
    public partial class ZImageViewer
    {
        #region Geometry Coordinates

        public PointF ViewportToImage(Point vp)
        {
            if (_image == null || _zoomFactor <= 0) return PointF.Empty;

            float dx = vp.X - _panOffset.X;
            float dy = vp.Y - _panOffset.Y;

            float sx = _zoomFactor * (_flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1);
            float sy = _zoomFactor * (_flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1);
            dx /= sx;
            dy /= sy;

            if (_rotation != ImageRotationAngle.Rotate0)
            {
                double rad = -(double)_rotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                double rx = dx * cos - dy * sin;
                double ry = dx * sin + dy * cos;
                dx = (float)rx;
                dy = (float)ry;
            }

            return new PointF(dx + _image.Width / 2f, dy + _image.Height / 2f);
        }

        public PointF ImageToViewport(PointF img)
        {
            if (_image == null) return PointF.Empty;

            float dx = img.X - _image.Width / 2f;
            float dy = img.Y - _image.Height / 2f;

            if (_rotation != ImageRotationAngle.Rotate0)
            {
                double rad = (double)_rotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                double rx = dx * cos - dy * sin;
                double ry = dx * sin + dy * cos;
                dx = (float)rx;
                dy = (float)ry;
            }

            float sx = _zoomFactor * (_flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1);
            float sy = _zoomFactor * (_flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1);
            dx *= sx;
            dy *= sy;

            return new PointF(dx + _panOffset.X, dy + _panOffset.Y);
        }

        #endregion

        #region Mouse & Keyboard Events

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_showToolbar && HandleToolbarClick(e.Location)) return;
            if (_showMiniMap && _image != null && HandleMiniMapClick(e.Location))
            {
                _isDraggingMiniMap = true;
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
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
            _currentMousePosition = e.Location;

            if (_isPanning)
            {
                float dx = e.X - _panStartMouse.X;
                float dy = e.Y - _panStartMouse.Y;
                PanOffset = new PointF(_panStartOffset.X + dx, _panStartOffset.Y + dy);
                _viewMode = ImageViewMode.Custom;
            }
            else if (_isDraggingMiniMap)
            {
                HandleMiniMapDrag(e.Location);
            }
            else
            {
                UpdatePixelTelemetry(e.Location);
                if (_showToolbar) Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning || _isDraggingMiniMap)
            {
                _isPanning = false;
                _isDraggingMiniMap = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (_viewMode == ImageViewMode.FitToWindow) ActualSize();
            else FitToWindow();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float factor = e.Delta > 0 ? 1.2f : 0.8333f;
            SetZoom(_zoomFactor * factor, e.Location);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Add:
                case Keys.Oemplus:
                    ZoomIn();
                    break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    ZoomOut();
                    break;
                case Keys.D0:
                case Keys.NumPad0:
                    FitToWindow();
                    break;
                case Keys.D1:
                case Keys.NumPad1:
                    ActualSize();
                    break;
                case Keys.R:
                    RotateClockwise();
                    break;
                case Keys.G:
                    ShowPixelGrid = !ShowPixelGrid;
                    break;
                case Keys.M:
                    ShowMiniMap = !ShowMiniMap;
                    break;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_viewMode == ImageViewMode.FitToWindow) FitToWindow();
        }

        private bool HandleToolbarClick(Point mouse)
        {
            float barW = 340f;
            float barH = 34f;
            RectangleF bar = new RectangleF((Width - barW) / 2f, Height - barH - 10f, barW, barH);
            if (!bar.Contains(mouse)) return false;

            float itemW = barW / 10f;
            int idx = (int)((mouse.X - bar.X) / itemW);

            switch (idx)
            {
                case 0: ZoomOut(); break;
                case 1: ZoomIn(); break;
                case 2: FitToWindow(); break;
                case 3: ActualSize(); break;
                case 4: RotateCounterClockwise(); break;
                case 5: RotateClockwise(); break;
                case 6: ToggleFlipHorizontal(); break;
                case 7: ShowPixelGrid = !ShowPixelGrid; break;
                case 8: ShowMiniMap = !ShowMiniMap; break;
                case 9: ResetView(); break;
            }
            return true;
        }

        private bool HandleMiniMapClick(Point mouse)
        {
            float pad = 14f;
            float mapW = 140f;
            float mapH = 90f;
            RectangleF bounds = new RectangleF(Width - mapW - pad, Height - mapH - pad - (_showToolbar ? 44 : 0), mapW, mapH);
            if (bounds.Contains(mouse))
            {
                HandleMiniMapDrag(mouse);
                return true;
            }
            return false;
        }

        private void HandleMiniMapDrag(Point mouse)
        {
            if (_image == null) return;

            float pad = 14f;
            float mapW = 140f;
            float mapH = 90f;
            RectangleF bounds = new RectangleF(Width - mapW - pad, Height - mapH - pad - (_showToolbar ? 44 : 0), mapW, mapH);

            float nx = Math.Max(0f, Math.Min(1f, (mouse.X - bounds.X) / bounds.Width));
            float ny = Math.Max(0f, Math.Min(1f, (mouse.Y - bounds.Y) / bounds.Height));

            float targetX = nx * _image.Width;
            float targetY = ny * _image.Height;

            PointF targetVp = ImageToViewport(new PointF(targetX, targetY));
            PanOffset = new PointF(_panOffset.X + (Width / 2f - targetVp.X), _panOffset.Y + (Height / 2f - targetVp.Y));
            _viewMode = ImageViewMode.Custom;
            Invalidate();
        }

        private void UpdatePixelTelemetry(Point mouse)
        {
            if (_image == null || !(_image is Bitmap bmp)) return;

            PointF imgPt = ViewportToImage(mouse);
            int px = (int)Math.Floor(imgPt.X);
            int py = (int)Math.Floor(imgPt.Y);

            if (px >= 0 && px < _image.Width && py >= 0 && py < _image.Height)
            {
                _currentPixelCoord = new Point(px, py);
                try
                {
                    _currentHoverColor = bmp.GetPixel(px, py);
                    HoverPixelChanged?.Invoke(this, (px, py, _currentHoverColor));
                }
                catch
                {
                    _currentHoverColor = Color.Transparent;
                }
            }
            else
            {
                _currentPixelCoord = new Point(-1, -1);
            }
        }

        #endregion
    }
}
