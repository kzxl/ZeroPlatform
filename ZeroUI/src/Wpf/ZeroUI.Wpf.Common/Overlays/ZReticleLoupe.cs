using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// High-performance dynamic reticle loupe overlay for precision image inspection and pixel peeping.
    /// Renders floating circular magnifying optics, crosshair reticle rulers, and status badges directly via <see cref="DrawingContext"/>.
    /// </summary>
    public class ZReticleLoupe : FrameworkElement, IZeroEditor
    {
        private bool _isModified;
        private bool _readOnly;

        #region Dependency Properties

        public static readonly DependencyProperty CenterProperty =
            DependencyProperty.Register(
                nameof(Center),
                typeof(Point),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnCenterChanged));

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register(
                nameof(Radius),
                typeof(double),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(90.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReticleRadiusProperty =
            DependencyProperty.Register(
                nameof(ReticleRadius),
                typeof(double),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive),
                typeof(bool),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReticleBrushProperty =
            DependencyProperty.Register(
                nameof(ReticleBrush),
                typeof(Brush),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LensBackgroundProperty =
            DependencyProperty.Register(
                nameof(LensBackground),
                typeof(Brush),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModeBadgeTextProperty =
            DependencyProperty.Register(
                nameof(ModeBadgeText),
                typeof(string),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata("100% LOUPE", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BadgeBrushProperty =
            DependencyProperty.Register(
                nameof(BadgeBrush),
                typeof(Brush),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCrosshairProperty =
            DependencyProperty.Register(
                nameof(ShowCrosshair),
                typeof(bool),
                typeof(ZReticleLoupe),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties

        public Point Center
        {
            get => (Point)GetValue(CenterProperty);
            set => SetValue(CenterProperty, value);
        }

        public double Radius
        {
            get => (double)GetValue(RadiusProperty);
            set => SetValue(RadiusProperty, value);
        }

        public double ReticleRadius
        {
            get => (double)GetValue(ReticleRadiusProperty);
            set => SetValue(ReticleRadiusProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public Brush? ReticleBrush
        {
            get => (Brush?)GetValue(ReticleBrushProperty);
            set => SetValue(ReticleBrushProperty, value);
        }

        public Brush? LensBackground
        {
            get => (Brush?)GetValue(LensBackgroundProperty);
            set => SetValue(LensBackgroundProperty, value);
        }

        public string ModeBadgeText
        {
            get => (string)GetValue(ModeBadgeTextProperty);
            set => SetValue(ModeBadgeTextProperty, value);
        }

        public Brush? BadgeBrush
        {
            get => (Brush?)GetValue(BadgeBrushProperty);
            set => SetValue(BadgeBrushProperty, value);
        }

        public bool ShowCrosshair
        {
            get => (bool)GetValue(ShowCrosshairProperty);
            set => SetValue(ShowCrosshairProperty, value);
        }

        #endregion

        #region IZeroEditor

        public object? EditValue
        {
            get => Center;
            set
            {
                if (value is Point p)
                {
                    Center = p;
                }
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
            get => _readOnly;
            set => _readOnly = value;
        }

        public void Reset()
        {
            Center = new Point(0, 0);
            Radius = 90.0;
            IsActive = true;
            _isModified = false;
        }

        public void Clear()
        {
            Center = new Point(0, 0);
            _isModified = false;
        }

        #endregion

        public ZReticleLoupe()
        {
            IsHitTestVisible = false;
        }

        private static void OnCenterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZReticleLoupe ctrl)
            {
                ctrl._isModified = true;
                ctrl.EditValueChanged?.Invoke(ctrl, EventArgs.Empty);
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (!IsActive || Radius <= 0) return;

            var reticleColor = (ReticleBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x00, 0xE5, 0xFF);
            var lensBg = LensBackground ?? new SolidColorBrush(Color.FromArgb(0x88, 0x00, 0x00, 0x00));
            var badgeBg = BadgeBrush ?? new SolidColorBrush(Color.FromArgb(0xCC, 0x02, 0x84, 0xC7));

            // Outer Lens Circle
            var outerPen = new Pen(new SolidColorBrush(reticleColor), 2.0);
            outerPen.Freeze();
            dc.DrawEllipse(lensBg, outerPen, Center, Radius, Radius);

            // Crosshair
            if (ShowCrosshair)
            {
                var crosshairColor = Color.FromArgb(0x66, reticleColor.R, reticleColor.G, reticleColor.B);
                var dashPen = new Pen(new SolidColorBrush(crosshairColor), 1.0)
                {
                    DashStyle = new DashStyle(new double[] { 3, 3 }, 0)
                };
                dashPen.Freeze();

                dc.DrawLine(dashPen, new Point(Center.X - Radius, Center.Y), new Point(Center.X + Radius, Center.Y));
                dc.DrawLine(dashPen, new Point(Center.X, Center.Y - Radius), new Point(Center.X, Center.Y + Radius));
            }

            // Inner Target Ring
            if (ReticleRadius > 0)
            {
                var ringColor = Color.FromArgb(0xAA, reticleColor.R, reticleColor.G, reticleColor.B);
                var ringPen = new Pen(new SolidColorBrush(ringColor), 1.0);
                ringPen.Freeze();
                dc.DrawEllipse(null, ringPen, Center, ReticleRadius, ReticleRadius);
            }

            // Mode Badge Pill
            if (!string.IsNullOrEmpty(ModeBadgeText))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                var typeface = new Typeface(new FontFamily("Segoe UI, Inter, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var formattedText = new FormattedText(
                    ModeBadgeText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    9.5,
                    Brushes.White,
                    dpi.PixelsPerDip);

                double pillW = formattedText.Width + 12;
                double pillH = formattedText.Height + 6;
                double pillX = Center.X - pillW * 0.5;
                double pillY = Center.Y - Radius + 14;

                var pillRect = new Rect(pillX, pillY, pillW, pillH);
                dc.DrawRoundedRectangle(badgeBg, null, pillRect, 4, 4);
                dc.DrawText(formattedText, new Point(pillX + 6, pillY + 3));
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZReticleLoupe"/>.
    /// </summary>
    [Obsolete("ReticleLoupeControl is deprecated and will be removed in 5 release cycles. Please migrate to ZReticleLoupe instead.")]
    public class ReticleLoupeControl : ZReticleLoupe
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZReticleLoupe"/>.
    /// </summary>
    [Obsolete("ZeroReticleLoupe is deprecated and will be removed in 5 release cycles. Please migrate to ZReticleLoupe instead.")]
    public class ZeroReticleLoupe : ZReticleLoupe
    {
    }
}
