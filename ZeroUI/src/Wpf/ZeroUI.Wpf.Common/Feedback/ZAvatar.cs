using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Feedback
{

    /// <summary>
    /// Circular user avatar component for WPF displaying initials or an image,
    /// complete with online presence indicator, size presets, and auto-generated palette colors.
    /// </summary>
    public class ZAvatar : Control
    {
        private string _initials = "ZU";
        private object? _imageSource;
        private string _sizePreset = "Medium";
        private object? _backgroundColor;
        private bool _showOnlineIndicator = false;
        private AvatarStatus _status = AvatarStatus.Online;

        public string Initials
        {
            get => _initials;
            set { _initials = (value ?? string.Empty).ToUpperInvariant(); InvalidateVisual(); }
        }

        public object? ImageSource
        {
            get => _imageSource;
            set { _imageSource = value; InvalidateVisual(); }
        }

        public string Size
        {
            get => _sizePreset;
            set
            {
                _sizePreset = value ?? "Medium";
                UpdateDimensions();
                InvalidateVisual();
            }
        }

        public object? BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; InvalidateVisual(); }
        }

        public bool ShowOnlineIndicator
        {
            get => _showOnlineIndicator;
            set { _showOnlineIndicator = value; InvalidateVisual(); }
        }

        public AvatarStatus Status
        {
            get => _status;
            set { _status = value; InvalidateVisual(); }
        }

        static ZAvatar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZAvatar), new FrameworkPropertyMetadata(typeof(ZAvatar)));
        }

        public ZAvatar()
        {
            UpdateDimensions();

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void UpdateDimensions()
        {
            double px = _sizePreset.ToLowerInvariant() switch
            {
                "small" => 28,
                "medium" => 40,
                "large" => 56,
                "xlarge" => 72,
                _ => double.TryParse(_sizePreset, out double parsed) ? Math.Max(16, parsed) : 40
            };
            Width = px;
            Height = px;
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
        }

        private Brush ResolveBackgroundBrush()
        {
            if (_backgroundColor is Brush b) return b;
            if (_backgroundColor is Color c) return new SolidColorBrush(c);
            if (_backgroundColor is string hex && !string.IsNullOrEmpty(hex))
            {
                try
                {
                    var parsed = (Color)ColorConverter.ConvertFromString(hex);
                    return new SolidColorBrush(parsed);
                }
                catch { }
            }

            int hash = Math.Abs((_initials ?? "Z").GetHashCode());
            Color[] palette =
            {
                Color.FromRgb(59, 130, 246),
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(245, 158, 11),
                Color.FromRgb(239, 68, 68),
                Color.FromRgb(139, 92, 246),
                Color.FromRgb(236, 72, 153),
                Color.FromRgb(20, 184, 166),
                Color.FromRgb(99, 102, 241)
            };
            var chosen = new SolidColorBrush(palette[hash % palette.Length]);
            chosen.Freeze();
            return chosen;
        }

        private static Brush GetStatusBrush(AvatarStatus status)
        {
            Color c = status switch
            {
                AvatarStatus.Online => Color.FromRgb(34, 197, 94),
                AvatarStatus.Away => Color.FromRgb(245, 158, 11),
                AvatarStatus.Busy => Color.FromRgb(239, 68, 68),
                _ => Color.FromRgb(156, 163, 175)
            };
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double diameter = Math.Min(ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            if (diameter <= 4) return;

            var center = new Point(diameter / 2, diameter / 2);
            double radius = (diameter - 2) / 2;

            if (_imageSource is System.Windows.Media.ImageSource img)
            {
                var imgBrush = new ImageBrush(img)
                {
                    Stretch = Stretch.UniformToFill
                };
                dc.DrawEllipse(imgBrush, null, center, radius, radius);
            }
            else
            {
                var bgBrush = ResolveBackgroundBrush();
                dc.DrawEllipse(bgBrush, null, center, radius, radius);

                if (!string.IsNullOrEmpty(_initials))
                {
                    double fontSize = Math.Max(8, diameter * 0.38);
                    var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var textFormatted = new FormattedText(
                        _initials,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(textFormatted, new Point(center.X - (textFormatted.Width / 2), center.Y - (textFormatted.Height / 2)));
                }
            }

            // Online indicator dot
            if (_showOnlineIndicator)
            {
                double dotRadius = Math.Max(3, diameter * 0.14);
                var dotCenter = new Point(diameter - dotRadius - 1, diameter - dotRadius - 1);

                // Cutout border
                var cutoutPen = new Pen(ZeroWpfTheme.BgPrimary, 2.0);
                cutoutPen.Freeze();
                dc.DrawEllipse(GetStatusBrush(_status), cutoutPen, dotCenter, dotRadius, dotRadius);
            }
        }
    }

    [Obsolete("ZeroAvatar is deprecated and will be removed in 5 release cycles. Please migrate to ZAvatar instead.")]
    public class ZeroAvatar : ZAvatar { }
}
