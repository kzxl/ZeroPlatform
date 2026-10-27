using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Media;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Enterprise GPU-accelerated interactive image viewer control for WPF.
    /// Provides sub-pixel pan and smooth zoom anchored at the cursor, forensic pixel grid,
    /// interactive MiniMap navigator, floating magnifier loupe, and telemetry color probe HUD.
    /// Supports direct rendering of <see cref="BitmapSource"/>, raw streams, files, and ZeroGraphics <c>ImageBuffer</c>.
    /// </summary>
    public partial class ZImageViewer : FrameworkElement, IZeroEditor, IZeroSkinnable
    {
        private bool _isPanning;
        private Point _panStartMouse;
        private Point _panStartOffset;
        private bool _isDraggingMiniMap;
        private Point _currentMousePosition = new Point(-1, -1);
        private bool _isModified;
        private bool _isReadOnly;
        private Color _currentHoverColor = Colors.Transparent;
        private Point _currentPixelCoord = new Point(-1, -1);

        // Pre-cached render objects for zero-allocation rendering loop
        private readonly Typeface _hudTypeface = new Typeface(new FontFamily("Segoe UI, Consolas, sans-serif"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private readonly Typeface _pixelTextTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Brush DefaultCanvasBg = new SolidColorBrush(Color.FromRgb(15, 18, 24));
        private static readonly Brush HudBgBrush = new SolidColorBrush(Color.FromArgb(200, 20, 24, 34));
        private static readonly Brush HudBorderBrush = new SolidColorBrush(Color.FromArgb(120, 56, 189, 248));
        private static readonly Brush HudTextBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        private static readonly Brush HudMutedBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
        private static readonly Pen HudBorderPen = new Pen(HudBorderBrush, 1.0);
        private static readonly Pen MiniMapViewportPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 229, 255)), 1.5);
        private static readonly Brush MiniMapViewportFill = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255));
        private static readonly Pen PixelGridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.75);

        static ZImageViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZImageViewer), new FrameworkPropertyMetadata(typeof(ZImageViewer)));
            ClipToBoundsProperty.OverrideMetadata(typeof(ZImageViewer), new FrameworkPropertyMetadata(true));
            FocusableProperty.OverrideMetadata(typeof(ZImageViewer), new FrameworkPropertyMetadata(true));

            DefaultCanvasBg.Freeze();
            HudBgBrush.Freeze();
            HudBorderBrush.Freeze();
            HudTextBrush.Freeze();
            HudMutedBrush.Freeze();
            AccentBrush.Freeze();
            HudBorderPen.Freeze();
            MiniMapViewportPen.Freeze();
            MiniMapViewportFill.Freeze();
            PixelGridPen.Freeze();
        }

        public ZImageViewer()
        {
            Cursor = Cursors.Arrow;
            Loaded += (s, e) => { if (Source != null && ViewMode == ImageViewMode.FitToWindow) FitToWindow(); };
            SizeChanged += (s, e) => { if (ViewMode == ImageViewMode.FitToWindow) FitToWindow(); };
        }

        #region Dependency Properties

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(BitmapSource), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(null, OnFilePathChanged));

        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(nameof(ViewMode), typeof(ImageViewMode), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(ImageViewMode.FitToWindow, FrameworkPropertyMetadataOptions.AffectsRender, OnViewModeChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnZoomChanged));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(0.01));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(64.0));

        public static readonly DependencyProperty PanOffsetProperty =
            DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RotationProperty =
            DependencyProperty.Register(nameof(Rotation), typeof(ImageRotationAngle), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(ImageRotationAngle.Rotate0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FlipProperty =
            DependencyProperty.Register(nameof(Flip), typeof(ImageFlipMode), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(ImageFlipMode.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty InterpolationQualityProperty =
            DependencyProperty.Register(nameof(InterpolationQuality), typeof(ImageInterpolationQuality), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(ImageInterpolationQuality.HighQuality, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowPixelGridProperty =
            DependencyProperty.Register(nameof(ShowPixelGrid), typeof(bool), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PixelGridThresholdProperty =
            DependencyProperty.Register(nameof(PixelGridThreshold), typeof(double), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowMiniMapProperty =
            DependencyProperty.Register(nameof(ShowMiniMap), typeof(bool), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowLoupeProperty =
            DependencyProperty.Register(nameof(ShowLoupe), typeof(bool), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LoupeFactorProperty =
            DependencyProperty.Register(nameof(LoupeFactor), typeof(double), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowToolbarProperty =
            DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register(nameof(ShowStatusBar), typeof(bool), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(nameof(BackgroundBrush), typeof(Brush), typeof(ZImageViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region CLR Properties

        public BitmapSource? Source
        {
            get => (BitmapSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public string? FilePath
        {
            get => (string?)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public ImageViewMode ViewMode
        {
            get => (ImageViewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, Clamp(value, MinZoom, MaxZoom));
        }

        public double MinZoom
        {
            get => (double)GetValue(MinZoomProperty);
            set => SetValue(MinZoomProperty, Math.Max(0.001, value));
        }

        public double MaxZoom
        {
            get => (double)GetValue(MaxZoomProperty);
            set => SetValue(MaxZoomProperty, Math.Max(MinZoom, value));
        }

        public Point PanOffset
        {
            get => (Point)GetValue(PanOffsetProperty);
            set => SetValue(PanOffsetProperty, value);
        }

        public ImageRotationAngle Rotation
        {
            get => (ImageRotationAngle)GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
        }

        public ImageFlipMode Flip
        {
            get => (ImageFlipMode)GetValue(FlipProperty);
            set => SetValue(FlipProperty, value);
        }

        public ImageInterpolationQuality InterpolationQuality
        {
            get => (ImageInterpolationQuality)GetValue(InterpolationQualityProperty);
            set => SetValue(InterpolationQualityProperty, value);
        }

        public bool ShowPixelGrid
        {
            get => (bool)GetValue(ShowPixelGridProperty);
            set => SetValue(ShowPixelGridProperty, value);
        }

        public double PixelGridThreshold
        {
            get => (double)GetValue(PixelGridThresholdProperty);
            set => SetValue(PixelGridThresholdProperty, value);
        }

        public bool ShowMiniMap
        {
            get => (bool)GetValue(ShowMiniMapProperty);
            set => SetValue(ShowMiniMapProperty, value);
        }

        public bool ShowLoupe
        {
            get => (bool)GetValue(ShowLoupeProperty);
            set => SetValue(ShowLoupeProperty, value);
        }

        public double LoupeFactor
        {
            get => (double)GetValue(LoupeFactorProperty);
            set => SetValue(LoupeFactorProperty, Clamp(value, 1.5, 16.0));
        }

        public bool ShowToolbar
        {
            get => (bool)GetValue(ShowToolbarProperty);
            set => SetValue(ShowToolbarProperty, value);
        }

        public bool ShowStatusBar
        {
            get => (bool)GetValue(ShowStatusBarProperty);
            set => SetValue(ShowStatusBarProperty, value);
        }

        public Brush? BackgroundBrush
        {
            get => (Brush?)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        public int PixelWidth => Source?.PixelWidth ?? 0;
        public int PixelHeight => Source?.PixelHeight ?? 0;

        #endregion

        #region Events

        public event EventHandler<double>? ZoomChanged;
        public event EventHandler<Point>? PanChanged;
        public event EventHandler<(int X, int Y, Color Color)>? HoverPixelChanged;
        public event EventHandler? ImageLoaded;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => Source;
            set
            {
                if (value is BitmapSource bmp)
                {
                    Source = bmp;
                }
                else if (value is string path && File.Exists(path))
                {
                    LoadFromFile(path);
                }
                else if (value == null)
                {
                    Source = null;
                }
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public bool ReadOnly
        {
            get => _isReadOnly;
            set => _isReadOnly = value;
        }

        public void Reset()
        {
            ResetView();
            IsModified = false;
        }

        public void Clear()
        {
            Source = null;
            FilePath = null;
            PanOffset = new Point(0, 0);
            Zoom = 1.0;
            IsModified = false;
            InvalidateVisual();
        }

        #endregion

        #region IZeroSkinnable Implementation

        private bool _useDefaultSkin = true;
        private ZeroSkin? _customSkin;

        public bool UseDefaultSkin
        {
            get => _useDefaultSkin;
            set
            {
                _useDefaultSkin = value;
                ApplySkin(EffectiveSkin);
            }
        }

        public ZeroSkin? CustomSkin
        {
            get => _customSkin;
            set
            {
                _customSkin = value;
                if (!_useDefaultSkin) ApplySkin(EffectiveSkin);
            }
        }

        public ZeroSkin EffectiveSkin => (!_useDefaultSkin && _customSkin != null)
            ? _customSkin
            : ZeroSkinManager.CurrentSkin;

        public void ApplySkin(ZeroSkin skin)
        {
            InvalidateVisual();
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZImageViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ImageViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZImageViewer instead.")]
    public class ImageViewerControl : ZImageViewer
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZImageViewer"/>.
    /// </summary>
    [Obsolete("ZeroImageViewer is deprecated. Please use ZImageViewer instead.")]
    public class ZeroImageViewer : ZImageViewer
    {
    }

    #endregion
}