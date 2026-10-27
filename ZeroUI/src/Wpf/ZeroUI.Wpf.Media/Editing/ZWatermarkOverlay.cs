using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Dynamic watermark and security stamp overlay.
    /// Supports dynamic tokens ({User}, {Date}, {Time}), 9-point anchor placement, diagonal tiling, and bitmap burning.
    /// </summary>
    public class ZWatermarkOverlay : FrameworkElement
    {
        private static readonly Typeface DefaultTypeface = new(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        static ZWatermarkOverlay()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZWatermarkOverlay), new FrameworkPropertyMetadata(typeof(ZWatermarkOverlay)));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(ZWatermarkOverlay), new FrameworkPropertyMetadata(false)); // Pass-through clicks
        }

        #region Dependency Properties

        public static readonly DependencyProperty WatermarkTextProperty =
            DependencyProperty.Register(nameof(WatermarkText), typeof(string), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata("CONFIDENTIAL · {User} · {Date}", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LogoSourceProperty =
            DependencyProperty.Register(nameof(LogoSource), typeof(BitmapSource), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(WatermarkPlacement), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(WatermarkPlacement.DiagonalTiled, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WatermarkOpacityProperty =
            DependencyProperty.Register(nameof(WatermarkOpacity), typeof(double), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(0.20, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RotationAngleProperty =
            DependencyProperty.Register(nameof(RotationAngle), typeof(double), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(-30.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WatermarkFontSizeProperty =
            DependencyProperty.Register(nameof(WatermarkFontSize), typeof(double), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(nameof(TextColor), typeof(Color), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TileSpacingProperty =
            DependencyProperty.Register(nameof(TileSpacing), typeof(double), typeof(ZWatermarkOverlay),
                new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public string WatermarkText
        {
            get => (string)GetValue(WatermarkTextProperty);
            set => SetValue(WatermarkTextProperty, value);
        }

        public BitmapSource? LogoSource
        {
            get => (BitmapSource?)GetValue(LogoSourceProperty);
            set => SetValue(LogoSourceProperty, value);
        }

        public WatermarkPlacement Placement
        {
            get => (WatermarkPlacement)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        public double WatermarkOpacity
        {
            get => (double)GetValue(WatermarkOpacityProperty);
            set => SetValue(WatermarkOpacityProperty, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public double RotationAngle
        {
            get => (double)GetValue(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
        }

        public double WatermarkFontSize
        {
            get => (double)GetValue(WatermarkFontSizeProperty);
            set => SetValue(WatermarkFontSizeProperty, Math.Max(8.0, value));
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public double TileSpacing
        {
            get => (double)GetValue(TileSpacingProperty);
            set => SetValue(TileSpacingProperty, Math.Max(50.0, value));
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            string resolvedText = ResolveDynamicTokens(WatermarkText);
            var color = Color.FromArgb((byte)(WatermarkOpacity * 255), TextColor.R, TextColor.G, TextColor.B);
            var textBrush = new SolidColorBrush(color);
            textBrush.Freeze();

            var ft = new FormattedText(
                resolvedText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                WatermarkFontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            if (Placement == WatermarkPlacement.DiagonalTiled)
            {
                RenderTiledWatermark(dc, ft, ActualWidth, ActualHeight);
            }
            else
            {
                RenderSingleAnchorWatermark(dc, ft, ActualWidth, ActualHeight);
            }
        }

        private void RenderTiledWatermark(DrawingContext dc, FormattedText ft, double width, double height)
        {
            double stepX = TileSpacing + ft.Width;
            double stepY = TileSpacing;

            for (double y = -50; y < height + 100; y += stepY)
            {
                for (double x = -50; x < width + 100; x += stepX)
                {
                    dc.PushTransform(new TransformGroup
                    {
                        Children =
                        {
                            new RotateTransform(RotationAngle, x + ft.Width / 2.0, y + ft.Height / 2.0)
                        }
                    });

                    dc.DrawText(ft, new Point(x, y));
                    dc.Pop();
                }
            }
        }

        private void RenderSingleAnchorWatermark(DrawingContext dc, FormattedText ft, double width, double height)
        {
            Point pos = GetAnchorPoint(Placement, width, height, ft.Width, ft.Height);

            dc.PushTransform(new RotateTransform(RotationAngle, pos.X + ft.Width / 2.0, pos.Y + ft.Height / 2.0));
            dc.DrawText(ft, pos);
            dc.Pop();
        }

        private static Point GetAnchorPoint(WatermarkPlacement placement, double canvasW, double canvasH, double itemW, double itemH)
        {
            double margin = 24.0;
            return placement switch
            {
                WatermarkPlacement.TopLeft => new Point(margin, margin),
                WatermarkPlacement.TopCenter => new Point((canvasW - itemW) / 2.0, margin),
                WatermarkPlacement.TopRight => new Point(canvasW - itemW - margin, margin),
                WatermarkPlacement.MiddleLeft => new Point(margin, (canvasH - itemH) / 2.0),
                WatermarkPlacement.Center => new Point((canvasW - itemW) / 2.0, (canvasH - itemH) / 2.0),
                WatermarkPlacement.MiddleRight => new Point(canvasW - itemW - margin, (canvasH - itemH) / 2.0),
                WatermarkPlacement.BottomLeft => new Point(margin, canvasH - itemH - margin),
                WatermarkPlacement.BottomCenter => new Point((canvasW - itemW) / 2.0, canvasH - itemH - margin),
                WatermarkPlacement.BottomRight => new Point(canvasW - itemW - margin, canvasH - itemH - margin),
                _ => new Point((canvasW - itemW) / 2.0, (canvasH - itemH) / 2.0)
            };
        }

        private static string ResolveDynamicTokens(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            var now = DateTime.Now;
            return template
                .Replace("{User}", Environment.UserName)
                .Replace("{Date}", now.ToString("yyyy-MM-dd"))
                .Replace("{Time}", now.ToString("HH:mm:ss"))
                .Replace("{Machine}", Environment.MachineName);
        }

        #endregion

        #region Bitmap Burning Utility

        public BitmapSource BurnWatermarkToBitmap(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                string resolvedText = ResolveDynamicTokens(WatermarkText);
                var color = Color.FromArgb((byte)(WatermarkOpacity * 255), TextColor.R, TextColor.G, TextColor.B);
                var textBrush = new SolidColorBrush(color);
                textBrush.Freeze();

                double scaledFontSize = WatermarkFontSize * (source.PixelWidth / Math.Max(1.0, ActualWidth));
                var ft = new FormattedText(
                    resolvedText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    DefaultTypeface,
                    scaledFontSize,
                    textBrush,
                    source.DpiX / 96.0);

                if (Placement == WatermarkPlacement.DiagonalTiled)
                {
                    RenderTiledWatermark(dc, ft, source.PixelWidth, source.PixelHeight);
                }
                else
                {
                    RenderSingleAnchorWatermark(dc, ft, source.PixelWidth, source.PixelHeight);
                }
            }

            var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        #endregion
    }
}
