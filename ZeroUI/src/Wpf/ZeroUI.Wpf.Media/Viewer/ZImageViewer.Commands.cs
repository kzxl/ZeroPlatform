using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public partial class ZImageViewer
    {
        #region View Navigation Commands

        public void FitToWindow()
        {
            if (Source == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            double imgW = Source.PixelWidth;
            double imgH = Source.PixelHeight;
            if (imgW <= 0 || imgH <= 0) return;

            // Compute rotated bounds
            bool isSwapped = Rotation == ImageRotationAngle.Rotate90 || Rotation == ImageRotationAngle.Rotate270;
            double targetW = isSwapped ? imgH : imgW;
            double targetH = isSwapped ? imgW : imgH;

            double margin = 24.0;
            double availW = Math.Max(10, ActualWidth - margin * 2);
            double availH = Math.Max(10, ActualHeight - margin * 2);

            double scaleX = availW / targetW;
            double scaleY = availH / targetH;
            double fitScale = Math.Min(scaleX, scaleY);

            Zoom = Clamp(fitScale, MinZoom, MaxZoom);
            PanOffset = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            ViewMode = ImageViewMode.FitToWindow;
            InvalidateVisual();
        }

        public void FitWidth()
        {
            if (Source == null || ActualWidth <= 0) return;
            bool isSwapped = Rotation == ImageRotationAngle.Rotate90 || Rotation == ImageRotationAngle.Rotate270;
            double targetW = isSwapped ? Source.PixelHeight : Source.PixelWidth;
            if (targetW <= 0) return;

            Zoom = Clamp((ActualWidth - 32) / targetW, MinZoom, MaxZoom);
            PanOffset = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            ViewMode = ImageViewMode.FitWidth;
            InvalidateVisual();
        }

        public void FitHeight()
        {
            if (Source == null || ActualHeight <= 0) return;
            bool isSwapped = Rotation == ImageRotationAngle.Rotate90 || Rotation == ImageRotationAngle.Rotate270;
            double targetH = isSwapped ? Source.PixelWidth : Source.PixelHeight;
            if (targetH <= 0) return;

            Zoom = Clamp((ActualHeight - 32) / targetH, MinZoom, MaxZoom);
            PanOffset = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            ViewMode = ImageViewMode.FitHeight;
            InvalidateVisual();
        }

        public void ActualSize()
        {
            Zoom = 1.0;
            PanOffset = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            ViewMode = ImageViewMode.OriginalSize;
            InvalidateVisual();
        }

        public void ZoomIn(double factor = 1.25)
        {
            ZoomAroundCenter(Zoom * factor);
        }

        public void ZoomOut(double factor = 0.8)
        {
            ZoomAroundCenter(Zoom * factor);
        }

        public void ZoomAroundCenter(double newZoom)
        {
            SetZoom(newZoom, new Point(ActualWidth / 2.0, ActualHeight / 2.0));
        }

        public void SetZoom(double newZoom, Point? anchorPoint = null)
        {
            double oldZoom = Zoom;
            double clamped = Clamp(newZoom, MinZoom, MaxZoom);
            if (Math.Abs(clamped - oldZoom) < 0.0001) return;

            Point anchor = anchorPoint ?? new Point(ActualWidth / 2.0, ActualHeight / 2.0);

            // Shift PanOffset so anchor position remains fixed on the image
            Point currentPan = PanOffset;
            double scaleRatio = clamped / oldZoom;
            double newPanX = anchor.X - (anchor.X - currentPan.X) * scaleRatio;
            double newPanY = anchor.Y - (anchor.Y - currentPan.Y) * scaleRatio;

            PanOffset = new Point(newPanX, newPanY);
            Zoom = clamped;
            ViewMode = ImageViewMode.Custom;
            ZoomChanged?.Invoke(this, clamped);
            PanChanged?.Invoke(this, PanOffset);
            InvalidateVisual();
        }

        public void RotateClockwise()
        {
            Rotation = Rotation switch
            {
                ImageRotationAngle.Rotate0 => ImageRotationAngle.Rotate90,
                ImageRotationAngle.Rotate90 => ImageRotationAngle.Rotate180,
                ImageRotationAngle.Rotate180 => ImageRotationAngle.Rotate270,
                _ => ImageRotationAngle.Rotate0
            };
            if (ViewMode == ImageViewMode.FitToWindow) FitToWindow();
            InvalidateVisual();
        }

        public void RotateCounterClockwise()
        {
            Rotation = Rotation switch
            {
                ImageRotationAngle.Rotate0 => ImageRotationAngle.Rotate270,
                ImageRotationAngle.Rotate90 => ImageRotationAngle.Rotate0,
                ImageRotationAngle.Rotate180 => ImageRotationAngle.Rotate90,
                _ => ImageRotationAngle.Rotate180
            };
            if (ViewMode == ImageViewMode.FitToWindow) FitToWindow();
            InvalidateVisual();
        }

        public void ToggleFlipHorizontal()
        {
            Flip ^= ImageFlipMode.Horizontal;
            InvalidateVisual();
        }

        public void ToggleFlipVertical()
        {
            Flip ^= ImageFlipMode.Vertical;
            InvalidateVisual();
        }

        public void ResetView()
        {
            Rotation = ImageRotationAngle.Rotate0;
            Flip = ImageFlipMode.None;
            FitToWindow();
        }

        public void LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Path.GetFullPath(path));
                bitmap.EndInit();
                bitmap.Freeze();

                Source = bitmap;
                FilePath = path;
                ImageLoaded?.Invoke(this, EventArgs.Empty);
                FitToWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZImageViewer] Load failed: {ex.Message}");
            }
        }

        public void LoadFromBytes(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;

            using var ms = new MemoryStream(buffer);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            Source = bitmap;
            FilePath = null;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
            FitToWindow();
        }

        #endregion

        #region Helper Methods & DP Callbacks

        internal static double Clamp(double val, double min, double max) => Math.Max(min, Math.Min(max, val));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZImageViewer ctrl && ctrl.Source != null)
            {
                ctrl.FitToWindow();
            }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZImageViewer ctrl && e.NewValue is string path)
            {
                ctrl.LoadFromFile(path);
            }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZImageViewer ctrl && e.NewValue is double zoom)
            {
                ctrl.ZoomChanged?.Invoke(ctrl, zoom);
            }
        }

        private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZImageViewer ctrl && e.NewValue is ImageViewMode mode)
            {
                switch (mode)
                {
                    case ImageViewMode.FitToWindow: ctrl.FitToWindow(); break;
                    case ImageViewMode.FitWidth: ctrl.FitWidth(); break;
                    case ImageViewMode.FitHeight: ctrl.FitHeight(); break;
                    case ImageViewMode.OriginalSize: ctrl.ActualSize(); break;
                }
            }
        }

        #endregion
    }
}
