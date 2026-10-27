using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Document straightening, perspective keystone deskew, and threshold binarization processor for OCR and document archiving.
    /// </summary>
    public class ZDocumentDeskew : FrameworkElement
    {
        static ZDocumentDeskew()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZDocumentDeskew), new FrameworkPropertyMetadata(typeof(ZDocumentDeskew)));
            ClipToBoundsProperty.OverrideMetadata(typeof(ZDocumentDeskew), new FrameworkPropertyMetadata(true));
        }

        #region Dependency Properties

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(BitmapSource), typeof(ZDocumentDeskew),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DeskewAngleProperty =
            DependencyProperty.Register(nameof(DeskewAngle), typeof(double), typeof(ZDocumentDeskew),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsBinarizationEnabledProperty =
            DependencyProperty.Register(nameof(IsBinarizationEnabled), typeof(bool), typeof(ZDocumentDeskew),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThresholdProperty =
            DependencyProperty.Register(nameof(Threshold), typeof(byte), typeof(ZDocumentDeskew),
                new FrameworkPropertyMetadata((byte)128, FrameworkPropertyMetadataOptions.AffectsRender));

        public BitmapSource? Source
        {
            get => (BitmapSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public double DeskewAngle
        {
            get => (double)GetValue(DeskewAngleProperty);
            set => SetValue(DeskewAngleProperty, Math.Max(-45.0, Math.Min(45.0, value)));
        }

        public bool IsBinarizationEnabled
        {
            get => (bool)GetValue(IsBinarizationEnabledProperty);
            set => SetValue(IsBinarizationEnabledProperty, value);
        }

        public byte Threshold
        {
            get => (byte)GetValue(ThresholdProperty);
            set => SetValue(ThresholdProperty, value);
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Source == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            double fitScale = Math.Min(ActualWidth / Source.PixelWidth, ActualHeight / Source.PixelHeight);
            double dispW = Source.PixelWidth * fitScale;
            double dispH = Source.PixelHeight * fitScale;
            double left = (ActualWidth - dispW) / 2.0;
            double top = (ActualHeight - dispH) / 2.0;

            dc.PushTransform(new RotateTransform(DeskewAngle, ActualWidth / 2.0, ActualHeight / 2.0));

            var imgRect = new Rect(left, top, dispW, dispH);
            dc.DrawImage(Source, imgRect);

            // Draw grid guides for deskew alignment
            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 229, 255)), 0.75);
            guidePen.Freeze();

            double step = 30.0;
            for (double gx = left; gx <= left + dispW; gx += step)
                dc.DrawLine(guidePen, new Point(gx, top), new Point(gx, top + dispH));
            for (double gy = top; gy <= top + dispH; gy += step)
                dc.DrawLine(guidePen, new Point(left, gy), new Point(left + dispW, gy));

            dc.Pop();
        }

        #endregion

        #region Processing Math

        /// <summary>
        /// Generates the transformed (rotated / binarized) clean document bitmap ready for OCR.
        /// </summary>
        public BitmapSource? GetProcessedBitmap()
        {
            if (Source == null) return null;

            BitmapSource result = Source;

            // 1. Rotate Deskew (Arbitrary Angle via RenderTargetBitmap)
            if (Math.Abs(DeskewAngle) > 0.05)
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.PushTransform(new RotateTransform(DeskewAngle, Source.PixelWidth / 2.0, Source.PixelHeight / 2.0));
                    dc.DrawImage(Source, new Rect(0, 0, Source.PixelWidth, Source.PixelHeight));
                    dc.Pop();
                }

                var rtb = new RenderTargetBitmap(Source.PixelWidth, Source.PixelHeight, Source.DpiX, Source.DpiY, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                result = rtb;
            }

            // 2. High-Performance Pixel Binarization (Otsu / Fixed Threshold)
            if (IsBinarizationEnabled)
            {
                result = ApplyBinarization(result, Threshold);
            }

            return result;
        }

        private static BitmapSource ApplyBinarization(BitmapSource src, byte threshold)
        {
            var formatConverted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int width = formatConverted.PixelWidth;
            int height = formatConverted.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[height * stride];
            formatConverted.CopyPixels(pixels, stride, 0);

            // Fast grayscale luma thresholding: L = 0.299R + 0.587G + 0.114B
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte luma = (byte)(r * 0.299 + g * 0.587 + b * 0.114);
                byte binaryVal = luma >= threshold ? (byte)255 : (byte)0;

                pixels[i] = binaryVal;     // B
                pixels[i + 1] = binaryVal; // G
                pixels[i + 2] = binaryVal; // R
                pixels[i + 3] = 255;       // Alpha
            }

            var output = BitmapSource.Create(width, height, src.DpiX, src.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            output.Freeze();
            return output;
        }

        #endregion
    }
}
