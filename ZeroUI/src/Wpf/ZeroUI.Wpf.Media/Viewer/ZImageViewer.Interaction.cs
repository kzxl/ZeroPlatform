using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public partial class ZImageViewer
    {
        #region Coordinate Geometry Transforms

        public Point ViewportToImage(Point vp)
        {
            if (Source == null || Zoom <= 0) return new Point(0, 0);

            // Inverse translation
            double dx = vp.X - PanOffset.X;
            double dy = vp.Y - PanOffset.Y;

            // Inverse scale & flip
            double sx = Zoom * (Flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1);
            double sy = Zoom * (Flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1);
            dx /= sx;
            dy /= sy;

            // Inverse rotation
            if (Rotation != ImageRotationAngle.Rotate0)
            {
                double rad = -(double)Rotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                double rx = dx * cos - dy * sin;
                double ry = dx * sin + dy * cos;
                dx = rx;
                dy = ry;
            }

            return new Point(dx + Source.PixelWidth / 2.0, dy + Source.PixelHeight / 2.0);
        }

        public Point ImageToViewport(Point img)
        {
            if (Source == null) return new Point(0, 0);

            double dx = img.X - Source.PixelWidth / 2.0;
            double dy = img.Y - Source.PixelHeight / 2.0;

            // Rotation
            if (Rotation != ImageRotationAngle.Rotate0)
            {
                double rad = (double)Rotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                double rx = dx * cos - dy * sin;
                double ry = dx * sin + dy * cos;
                dx = rx;
                dy = ry;
            }

            // Scale & flip
            double sx = Zoom * (Flip.HasFlag(ImageFlipMode.Horizontal) ? -1 : 1);
            double sy = Zoom * (Flip.HasFlag(ImageFlipMode.Vertical) ? -1 : 1);
            dx *= sx;
            dy *= sy;

            return new Point(dx + PanOffset.X, dy + PanOffset.Y);
        }

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point mouse = e.GetPosition(this);

            // Check if clicked Toolbar
            if (ShowToolbar && HandleToolbarClick(mouse))
            {
                e.Handled = true;
                return;
            }

            // Check if clicked MiniMap
            if (ShowMiniMap && Source != null && HandleMiniMapClick(mouse))
            {
                _isDraggingMiniMap = true;
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                if (e.ClickCount == 2)
                {
                    // Double click toggles FitToWindow and 1:1
                    if (ViewMode == ImageViewMode.FitToWindow) ActualSize();
                    else FitToWindow();
                }
                else
                {
                    _isPanning = true;
                    _panStartMouse = mouse;
                    _panStartOffset = PanOffset;
                    Cursor = Cursors.SizeAll;
                    CaptureMouse();
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentMousePosition = e.GetPosition(this);

            if (_isPanning)
            {
                Vector delta = _currentMousePosition - _panStartMouse;
                PanOffset = new Point(_panStartOffset.X + delta.X, _panStartOffset.Y + delta.Y);
                ViewMode = ImageViewMode.Custom;
                PanChanged?.Invoke(this, PanOffset);
                InvalidateVisual();
            }
            else if (_isDraggingMiniMap)
            {
                HandleMiniMapDrag(_currentMousePosition);
            }
            else
            {
                // Update color probe under cursor
                UpdatePixelTelemetry(_currentMousePosition);
                if (ShowLoupe || ShowToolbar) InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning || _isDraggingMiniMap)
            {
                _isPanning = false;
                _isDraggingMiniMap = false;
                Cursor = Cursors.Arrow;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            Point mouse = e.GetPosition(this);

            double zoomFactor = e.Delta > 0 ? 1.2 : 0.8333;
            SetZoom(Zoom * zoomFactor, mouse);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.OemPlus:
                case Key.Add:
                    ZoomIn();
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomOut();
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    FitToWindow();
                    e.Handled = true;
                    break;
                case Key.D1:
                case Key.NumPad1:
                    ActualSize();
                    e.Handled = true;
                    break;
                case Key.R:
                    RotateClockwise();
                    e.Handled = true;
                    break;
                case Key.G:
                    ShowPixelGrid = !ShowPixelGrid;
                    e.Handled = true;
                    break;
                case Key.M:
                    ShowMiniMap = !ShowMiniMap;
                    e.Handled = true;
                    break;
                case Key.L:
                    ShowLoupe = !ShowLoupe;
                    e.Handled = true;
                    break;
            }
        }

        private bool HandleToolbarClick(Point mouse)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            double barW = 380.0;
            double barH = 38.0;
            Rect barRect = new Rect((w - barW) / 2.0, h - barH - 14.0, barW, barH);
            if (!barRect.Contains(mouse)) return false;

            double btnW = barW / 10.0;
            int idx = (int)((mouse.X - barRect.X) / btnW);

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
                case 8: ShowLoupe = !ShowLoupe; break;
                case 9: ResetView(); break;
            }
            return true;
        }

        private bool HandleMiniMapClick(Point mouse)
        {
            double pad = 16.0;
            double mapW = 150.0;
            double mapH = 100.0;
            Rect mapBounds = new Rect(ActualWidth - mapW - pad, ActualHeight - mapH - pad - (ShowToolbar ? 48 : 0), mapW, mapH);
            if (mapBounds.Contains(mouse))
            {
                HandleMiniMapDrag(mouse);
                return true;
            }
            return false;
        }

        private void HandleMiniMapDrag(Point mouse)
        {
            if (Source == null) return;

            double pad = 16.0;
            double mapW = 150.0;
            double mapH = 100.0;
            Rect mapBounds = new Rect(ActualWidth - mapW - pad, ActualHeight - mapH - pad - (ShowToolbar ? 48 : 0), mapW, mapH);

            double normX = Clamp((mouse.X - mapBounds.X) / mapBounds.Width, 0, 1);
            double normY = Clamp((mouse.Y - mapBounds.Y) / mapBounds.Height, 0, 1);

            double targetImgX = normX * Source.PixelWidth;
            double targetImgY = normY * Source.PixelHeight;

            // Center view on this coordinate
            Point targetVp = ImageToViewport(new Point(targetImgX, targetImgY));
            PanOffset = new Point(PanOffset.X + (ActualWidth / 2.0 - targetVp.X), PanOffset.Y + (ActualHeight / 2.0 - targetVp.Y));
            ViewMode = ImageViewMode.Custom;
            InvalidateVisual();
        }

        private void UpdatePixelTelemetry(Point mouse)
        {
            if (Source == null) return;

            Point imgPt = ViewportToImage(mouse);
            int px = (int)Math.Floor(imgPt.X);
            int py = (int)Math.Floor(imgPt.Y);

            if (px >= 0 && px < Source.PixelWidth && py >= 0 && py < Source.PixelHeight)
            {
                _currentPixelCoord = new Point(px, py);
                try
                {
                    var cb = new CroppedBitmap(Source, new Int32Rect(px, py, 1, 1));
                    byte[] pixels = new byte[4];
                    cb.CopyPixels(pixels, 4, 0);

                    // Typical BGRA / BGR
                    _currentHoverColor = Color.FromRgb(pixels[2], pixels[1], pixels[0]);
                    HoverPixelChanged?.Invoke(this, (px, py, _currentHoverColor));
                }
                catch
                {
                    _currentHoverColor = Colors.Transparent;
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
