using System;
using System.Drawing;
using System.IO;
using ZeroUI.Core.Media;

namespace ZeroUI.WinForms.Media
{
    public partial class ZImageViewer
    {
        #region Navigation Commands

        public void FitToWindow()
        {
            if (_image == null || Width <= 0 || Height <= 0) return;

            bool isSwapped = _rotation == ImageRotationAngle.Rotate90 || _rotation == ImageRotationAngle.Rotate270;
            float imgW = isSwapped ? _image.Height : _image.Width;
            float imgH = isSwapped ? _image.Width : _image.Height;
            if (imgW <= 0 || imgH <= 0) return;

            float margin = 24f;
            float availW = Math.Max(10f, Width - margin * 2);
            float availH = Math.Max(10f, Height - margin * 2);

            float scale = Math.Min(availW / imgW, availH / imgH);
            ZoomFactor = Math.Max(_minZoom, Math.Min(_maxZoom, scale));
            PanOffset = new PointF(Width / 2f, Height / 2f);
            _viewMode = ImageViewMode.FitToWindow;
            Invalidate();
        }

        public void FitWidth()
        {
            if (_image == null || Width <= 0) return;
            bool isSwapped = _rotation == ImageRotationAngle.Rotate90 || _rotation == ImageRotationAngle.Rotate270;
            float imgW = isSwapped ? _image.Height : _image.Width;
            if (imgW <= 0) return;

            ZoomFactor = Math.Max(_minZoom, Math.Min(_maxZoom, (Width - 32f) / imgW));
            PanOffset = new PointF(Width / 2f, Height / 2f);
            _viewMode = ImageViewMode.FitWidth;
            Invalidate();
        }

        public void FitHeight()
        {
            if (_image == null || Height <= 0) return;
            bool isSwapped = _rotation == ImageRotationAngle.Rotate90 || _rotation == ImageRotationAngle.Rotate270;
            float imgH = isSwapped ? _image.Width : _image.Height;
            if (imgH <= 0) return;

            ZoomFactor = Math.Max(_minZoom, Math.Min(_maxZoom, (Height - 32f) / imgH));
            PanOffset = new PointF(Width / 2f, Height / 2f);
            _viewMode = ImageViewMode.FitHeight;
            Invalidate();
        }

        public void ActualSize()
        {
            ZoomFactor = 1.0f;
            PanOffset = new PointF(Width / 2f, Height / 2f);
            _viewMode = ImageViewMode.OriginalSize;
            Invalidate();
        }

        public void ZoomIn(float factor = 1.25f)
        {
            SetZoom(_zoomFactor * factor, new Point(Width / 2, Height / 2));
        }

        public void ZoomOut(float factor = 0.8f)
        {
            SetZoom(_zoomFactor * factor, new Point(Width / 2, Height / 2));
        }

        public void SetZoom(float newZoom, Point? anchorPoint = null)
        {
            float oldZoom = _zoomFactor;
            float clamped = Math.Max(_minZoom, Math.Min(_maxZoom, newZoom));
            if (Math.Abs(clamped - oldZoom) < 0.0001f) return;

            Point anchor = anchorPoint ?? new Point(Width / 2, Height / 2);
            float ratio = clamped / oldZoom;

            float newPanX = anchor.X - (anchor.X - _panOffset.X) * ratio;
            float newPanY = anchor.Y - (anchor.Y - _panOffset.Y) * ratio;

            _panOffset = new PointF(newPanX, newPanY);
            _zoomFactor = clamped;
            _viewMode = ImageViewMode.Custom;
            ZoomChanged?.Invoke(this, clamped);
            PanChanged?.Invoke(this, _panOffset);
            Invalidate();
        }

        public void RotateClockwise()
        {
            _rotation = _rotation switch
            {
                ImageRotationAngle.Rotate0 => ImageRotationAngle.Rotate90,
                ImageRotationAngle.Rotate90 => ImageRotationAngle.Rotate180,
                ImageRotationAngle.Rotate180 => ImageRotationAngle.Rotate270,
                _ => ImageRotationAngle.Rotate0
            };
            if (_viewMode == ImageViewMode.FitToWindow) FitToWindow();
            Invalidate();
        }

        public void RotateCounterClockwise()
        {
            _rotation = _rotation switch
            {
                ImageRotationAngle.Rotate0 => ImageRotationAngle.Rotate270,
                ImageRotationAngle.Rotate90 => ImageRotationAngle.Rotate0,
                ImageRotationAngle.Rotate180 => ImageRotationAngle.Rotate90,
                _ => ImageRotationAngle.Rotate180
            };
            if (_viewMode == ImageViewMode.FitToWindow) FitToWindow();
            Invalidate();
        }

        public void ToggleFlipHorizontal()
        {
            _flip ^= ImageFlipMode.Horizontal;
            Invalidate();
        }

        public void ToggleFlipVertical()
        {
            _flip ^= ImageFlipMode.Vertical;
            Invalidate();
        }

        public void ResetView()
        {
            _rotation = ImageRotationAngle.Rotate0;
            _flip = ImageFlipMode.None;
            FitToWindow();
        }

        public void LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var tempImg = Image.FromStream(fs);
                var bmp = new Bitmap(tempImg); // Copy into un-locked memory
                Image = bmp;
                _filePath = path;
                FitToWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZImageViewer] Load failed: {ex.Message}");
            }
        }

        #endregion
    }
}
