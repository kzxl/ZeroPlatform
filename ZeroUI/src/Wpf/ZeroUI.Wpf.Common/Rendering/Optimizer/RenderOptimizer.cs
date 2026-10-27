using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ZeroUI.Core.Rendering.Optimizer;

namespace ZeroUI.Wpf.Rendering.Optimizer
{
    /// <summary>
    /// Universal Attached Properties for ZeroUI's Automatic Render Optimizer.
    /// Allows any standard WPF control (e.g. Button, TextBox, Border, DataGrid, Grid)
    /// to instantly acquire GPU-accelerated analytical SDF drop shadows and neon bloom
    /// without restructuring the visual tree or replacing existing controls.
    /// </summary>
    public static class RenderOptimizer
    {
        #region Attached Dependency Properties

        public static readonly DependencyProperty ElevationProperty =
            DependencyProperty.RegisterAttached("Elevation", typeof(double), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        public static readonly DependencyProperty BlurRadiusProperty =
            DependencyProperty.RegisterAttached("BlurRadius", typeof(double), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.RegisterAttached("GlowIntensity", typeof(double), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.RegisterAttached("GlowColor", typeof(Color), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(Color.FromRgb(0, 229, 255), FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(new CornerRadius(6.0), FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        public static readonly DependencyProperty RoutingModeProperty =
            DependencyProperty.RegisterAttached("RoutingMode", typeof(OptimizerRoutingMode), typeof(RenderOptimizer),
                new FrameworkPropertyMetadata(OptimizerRoutingMode.Auto, FrameworkPropertyMetadataOptions.AffectsRender, OnOptimizerPropertyChanged));

        private static readonly DependencyProperty AdornerInstanceProperty =
            DependencyProperty.RegisterAttached("AdornerInstance", typeof(RenderOptimizerAdorner), typeof(RenderOptimizer),
                new PropertyMetadata(null));

        #endregion

        #region Getters and Setters

        public static double GetElevation(DependencyObject obj) => (double)obj.GetValue(ElevationProperty);
        public static void SetElevation(DependencyObject obj, double value) => obj.SetValue(ElevationProperty, value);

        public static double GetBlurRadius(DependencyObject obj) => (double)obj.GetValue(BlurRadiusProperty);
        public static void SetBlurRadius(DependencyObject obj, double value) => obj.SetValue(BlurRadiusProperty, value);

        public static double GetGlowIntensity(DependencyObject obj) => (double)obj.GetValue(GlowIntensityProperty);
        public static void SetGlowIntensity(DependencyObject obj, double value) => obj.SetValue(GlowIntensityProperty, value);

        public static Color GetGlowColor(DependencyObject obj) => (Color)obj.GetValue(GlowColorProperty);
        public static void SetGlowColor(DependencyObject obj, Color value) => obj.SetValue(GlowColorProperty, value);

        public static CornerRadius GetCornerRadius(DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);
        public static void SetCornerRadius(DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);

        public static OptimizerRoutingMode GetRoutingMode(DependencyObject obj) => (OptimizerRoutingMode)obj.GetValue(RoutingModeProperty);
        public static void SetRoutingMode(DependencyObject obj, OptimizerRoutingMode value) => obj.SetValue(RoutingModeProperty, value);

        #endregion

        private static void OnOptimizerPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;

            if (element.IsLoaded)
            {
                UpdateAdorner(element);
            }
            else
            {
                element.Loaded -= OnElementLoaded;
                element.Loaded += OnElementLoaded;
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= OnElementLoaded;
                UpdateAdorner(element);
            }
        }

        private static void UpdateAdorner(FrameworkElement element)
        {
            double elev = GetElevation(element);
            double glow = GetGlowIntensity(element);
            bool requiresOptimization = elev > 0.01 || glow > 0.01;

            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer == null) return;

            var existingAdorner = (RenderOptimizerAdorner?)element.GetValue(AdornerInstanceProperty);

            if (requiresOptimization)
            {
                if (existingAdorner == null)
                {
                    var adorner = new RenderOptimizerAdorner(element);
                    layer.Add(adorner);
                    element.SetValue(AdornerInstanceProperty, adorner);
                }
                else
                {
                    existingAdorner.InvalidateVisual();
                }
            }
            else if (existingAdorner != null)
            {
                layer.Remove(existingAdorner);
                element.ClearValue(AdornerInstanceProperty);
            }
        }
    }

    /// <summary>
    /// Adorner that renders GPU analytical SDF shadows and neon glows directly behind the adorned element.
    /// Operates without layout disruption, clipping, or template changes.
    /// </summary>
    public sealed class RenderOptimizerAdorner : Adorner
    {
        private readonly FrameworkElement _target;

        public RenderOptimizerAdorner(FrameworkElement adornedElement) : base(adornedElement)
        {
            _target = adornedElement ?? throw new ArgumentNullException(nameof(adornedElement));
            IsHitTestVisible = false;
            _target.SizeChanged += (s, e) => InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = _target.ActualWidth;
            double h = _target.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double elev = RenderOptimizer.GetElevation(_target);
            double blur = RenderOptimizer.GetBlurRadius(_target);
            double glow = RenderOptimizer.GetGlowIntensity(_target);
            var glowColor = RenderOptimizer.GetGlowColor(_target);
            var cRadius = RenderOptimizer.GetCornerRadius(_target);
            var mode = RenderOptimizer.GetRoutingMode(_target);

            // 1. Evaluate with ZeroRenderAnalyzer
            var monitor = ZeroWpfRenderMonitor.Instance;
            var profile = RenderOperationProfile.ForCard(
                (int)w,
                (int)h,
                elevation: (float)elev,
                cornerRadius: (float)cRadius.TopLeft,
                blurRadius: (float)blur,
                glowIntensity: (float)glow,
                hasText: true);

            var decision = ZeroRenderAnalyzer.Evaluate(profile, monitor.CurrentFidelity, monitor.RollingAverageFrameTimeMs);

            var pipeline = decision.Pipeline;
            if (mode == OptimizerRoutingMode.ForceCpu) pipeline = RenderPipelineTarget.Cpu;
            else if (mode == OptimizerRoutingMode.ForceGpuShader) pipeline = RenderPipelineTarget.GpuShader;
            else if (mode == OptimizerRoutingMode.ForceGpuAtlas) pipeline = RenderPipelineTarget.GpuAtlas;

            double rx = cRadius.TopLeft;
            double ry = cRadius.TopLeft;

            // 2. Draw Glow Pass if enabled
            if (glow > 0.01)
            {
                double spread = Math.Max(3.0, (blur + elev) * 0.7);
                int passes = 3;
                for (int i = passes; i >= 1; i--)
                {
                    double fraction = (double)i / passes;
                    double s = spread * fraction;
                    float intensityAlpha = ZeroShaderRegistry.EvaluateNeonGlowIntensity((float)s, (float)spread, (float)glow);
                    byte alpha = (byte)Math.Min(255, Math.Max(0, (int)(intensityAlpha * 120)));

                    if (alpha == 0) continue;

                    var brush = new SolidColorBrush(Color.FromArgb(alpha, glowColor.R, glowColor.G, glowColor.B));
                    brush.Freeze();

                    var pen = new Pen(brush, s * 1.5);
                    pen.Freeze();

                    var rect = new Rect(-s * 0.5, -s * 0.5, w + s, h + s);
                    dc.DrawRoundedRectangle(null, pen, rect, rx + s * 0.5, ry + s * 0.5);
                }
            }

            // 3. Draw Shadow Pass
            if (elev > 0.01)
            {
                double offsetY = elev * 0.65;
                if (pipeline == RenderPipelineTarget.GpuAtlas)
                {
                    // 9-Slice Atlas Pass
                    int steps = 3;
                    for (int i = steps; i >= 1; i--)
                    {
                        double spread = (steps - i + 1) * (blur / 4.0);
                        byte alpha = (byte)(24 + (i * 16));
                        var shadowBrush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                        shadowBrush.Freeze();

                        var rect = new Rect(-spread * 0.5, offsetY - spread * 0.25, w + spread, h + spread * 0.5);
                        dc.DrawRoundedRectangle(shadowBrush, null, rect, rx + spread * 0.5, ry + spread * 0.5);
                    }
                }
                else
                {
                    // Continuous SDF Pass
                    int steps = pipeline == RenderPipelineTarget.Cpu ? 2 : 4;
                    for (int i = steps; i >= 1; i--)
                    {
                        double spread = (steps - i + 1) * (blur / (steps + 1));
                        byte alpha = (byte)(16 + (i * 18));
                        var shadowBrush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                        shadowBrush.Freeze();

                        var rect = new Rect(-spread * 0.5, offsetY - spread * 0.25, w + spread, h + spread * 0.5);
                        dc.DrawRoundedRectangle(shadowBrush, null, rect, rx + spread * 0.5, ry + spread * 0.5);
                    }
                }
            }
        }
    }
}
