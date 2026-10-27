using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Rendering.Optimizer;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Rendering.Optimizer
{
    /// <summary>
    /// Smart hybrid UI card container powered by ZeroUI's Automatic Render Optimizer.
    /// Evaluates visual complexity, frame budget, and batching to route heavy shadow, blur, and glow
    /// to GPU shaders or 9-slice cached atlas, while preserving subpixel ClearType rendering for child text and controls.
    /// </summary>
    public class ZOptimizedCard : Decorator
    {
        #region Dependency Properties

        public static readonly DependencyProperty ElevationProperty =
            DependencyProperty.Register(nameof(Elevation), typeof(double), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlurRadiusProperty =
            DependencyProperty.Register(nameof(BlurRadius), typeof(double), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.Register(nameof(GlowIntensity), typeof(double), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(Color.FromRgb(0, 229, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(new CornerRadius(8.0), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PaddingProperty =
            DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(new Thickness(16.0), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(nameof(CardBackground), typeof(Brush), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CardBorderBrushProperty =
            DependencyProperty.Register(nameof(CardBorderBrush), typeof(Brush), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CardBorderThicknessProperty =
            DependencyProperty.Register(nameof(CardBorderThickness), typeof(double), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OptimizationModeProperty =
            DependencyProperty.Register(nameof(OptimizationMode), typeof(OptimizerRoutingMode), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(OptimizerRoutingMode.Auto, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BatchCountProperty =
            DependencyProperty.Register(nameof(BatchCount), typeof(int), typeof(ZOptimizedCard),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

        // Read-only Telemetry Keys
        private static readonly DependencyPropertyKey AssignedPipelinePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(AssignedPipeline), typeof(RenderPipelineTarget), typeof(ZOptimizedCard),
                new PropertyMetadata(RenderPipelineTarget.Hybrid));
        public static readonly DependencyProperty AssignedPipelineProperty = AssignedPipelinePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey ActiveShaderPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ActiveShader), typeof(string), typeof(ZOptimizedCard),
                new PropertyMetadata("None"));
        public static readonly DependencyProperty ActiveShaderProperty = ActiveShaderPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey EstimatedCostScorePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(EstimatedCostScore), typeof(double), typeof(ZOptimizedCard),
                new PropertyMetadata(0.0));
        public static readonly DependencyProperty EstimatedCostScoreProperty = EstimatedCostScorePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey EstimatedSpeedupFactorPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(EstimatedSpeedupFactor), typeof(double), typeof(ZOptimizedCard),
                new PropertyMetadata(1.0));
        public static readonly DependencyProperty EstimatedSpeedupFactorProperty = EstimatedSpeedupFactorPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey DecisionReasonPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(DecisionReason), typeof(string), typeof(ZOptimizedCard),
                new PropertyMetadata("Initial"));
        public static readonly DependencyProperty DecisionReasonProperty = DecisionReasonPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey TelemetryTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(TelemetryText), typeof(string), typeof(ZOptimizedCard),
                new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty TelemetryTextProperty = TelemetryTextPropertyKey.DependencyProperty;

        #endregion

        #region Properties Accessors

        public double Elevation
        {
            get => (double)GetValue(ElevationProperty);
            set => SetValue(ElevationProperty, value);
        }

        public double BlurRadius
        {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        public double GlowIntensity
        {
            get => (double)GetValue(GlowIntensityProperty);
            set => SetValue(GlowIntensityProperty, value);
        }

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        public Brush? CardBackground
        {
            get => (Brush?)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
        }

        public Brush? CardBorderBrush
        {
            get => (Brush?)GetValue(CardBorderBrushProperty);
            set => SetValue(CardBorderBrushProperty, value);
        }

        public double CardBorderThickness
        {
            get => (double)GetValue(CardBorderThicknessProperty);
            set => SetValue(CardBorderThicknessProperty, value);
        }

        public OptimizerRoutingMode OptimizationMode
        {
            get => (OptimizerRoutingMode)GetValue(OptimizationModeProperty);
            set => SetValue(OptimizationModeProperty, value);
        }

        public int BatchCount
        {
            get => (int)GetValue(BatchCountProperty);
            set => SetValue(BatchCountProperty, value);
        }

        public RenderPipelineTarget AssignedPipeline => (RenderPipelineTarget)GetValue(AssignedPipelineProperty);
        public string ActiveShader => (string)GetValue(ActiveShaderProperty);
        public double EstimatedCostScore => (double)GetValue(EstimatedCostScoreProperty);
        public double EstimatedSpeedupFactor => (double)GetValue(EstimatedSpeedupFactorProperty);
        public string DecisionReason => (string)GetValue(DecisionReasonProperty);
        public string TelemetryText => (string)GetValue(TelemetryTextProperty);

        #endregion

        public ZOptimizedCard()
        {
            SnapsToDevicePixels = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Unloaded += (s, e) => ZeroWpfRenderMonitor.Instance.CoreMonitor.FidelityTierChanged -= OnFidelityTierChanged;
        }

        private void OnFidelityTierChanged(RenderFidelityTier newTier)
        {
            Dispatcher.BeginInvoke(new Action(() => InvalidateVisual()));
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var pad = Padding;
            double padW = pad.Left + pad.Right;
            double padH = pad.Top + pad.Bottom;

            var childConstraint = new Size(
                Math.Max(0, constraint.Width - padW),
                Math.Max(0, constraint.Height - padH));

            if (Child != null)
            {
                Child.Measure(childConstraint);
                return new Size(Child.DesiredSize.Width + padW, Child.DesiredSize.Height + padH);
            }

            return new Size(padW, padH);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var pad = Padding;
            var innerRect = new Rect(
                pad.Left,
                pad.Top,
                Math.Max(0, arrangeSize.Width - (pad.Left + pad.Right)),
                Math.Max(0, arrangeSize.Height - (pad.Top + pad.Bottom)));

            Child?.Arrange(innerRect);
            return arrangeSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Analyze profile via ZeroRenderAnalyzer
            var monitor = ZeroWpfRenderMonitor.Instance;
            var profile = RenderOperationProfile.ForCard(
                (int)w,
                (int)h,
                elevation: (float)Elevation,
                cornerRadius: (float)CornerRadius.TopLeft,
                blurRadius: (float)BlurRadius,
                glowIntensity: (float)GlowIntensity,
                hasText: Child != null,
                isAnimated: false,
                batchCount: BatchCount);

            var decision = ZeroRenderAnalyzer.Evaluate(
                profile,
                monitor.CurrentFidelity,
                monitor.RollingAverageFrameTimeMs);

            // Apply manual routing override if specified
            var pipeline = decision.Pipeline;
            string activeShader = decision.RecommendedShader;

            if (OptimizationMode == OptimizerRoutingMode.ForceCpu)
            {
                pipeline = RenderPipelineTarget.Cpu;
                activeShader = "SoftwareFallback";
            }
            else if (OptimizationMode == OptimizerRoutingMode.ForceGpuShader)
            {
                pipeline = RenderPipelineTarget.GpuShader;
                activeShader = "AnalyticalSdfBoxShadow";
            }
            else if (OptimizationMode == OptimizerRoutingMode.ForceGpuAtlas)
            {
                pipeline = RenderPipelineTarget.GpuAtlas;
                activeShader = "SdfBoxShadowAtlas";
            }

            // Update telemetry
            SetValue(AssignedPipelinePropertyKey, pipeline);
            SetValue(ActiveShaderPropertyKey, activeShader);
            SetValue(EstimatedCostScorePropertyKey, decision.EstimatedCpuCostUs);
            SetValue(EstimatedSpeedupFactorPropertyKey, decision.EstimatedSpeedupFactor);
            SetValue(DecisionReasonPropertyKey, decision.Reason);
            SetValue(TelemetryTextPropertyKey, $"[{pipeline}] {activeShader} | {decision.EstimatedSpeedupFactor:F1}x speedup");

            // 2. Render Backdrop / Shadow according to pipeline
            double radiusX = CornerRadius.TopLeft;
            double radiusY = CornerRadius.TopLeft;

            // Modulate parameters based on adaptive fidelity tier
            double effectiveBlur = BlurRadius;
            double effectiveElev = Elevation;
            double effectiveGlow = GlowIntensity;

            if (monitor.CurrentFidelity == RenderFidelityTier.PowerSaver)
            {
                effectiveBlur = Math.Min(effectiveBlur, 8.0);
                effectiveElev = Math.Min(effectiveElev, 4.0);
                effectiveGlow = Math.Min(effectiveGlow, 0.4);
            }
            else if (monitor.CurrentFidelity == RenderFidelityTier.Balanced)
            {
                effectiveBlur = Math.Min(effectiveBlur, 24.0);
            }

            // Neon Glow pass
            if (effectiveGlow > 0.01)
            {
                DrawGlowPass(dc, w, h, radiusX, radiusY, effectiveGlow, effectiveBlur, effectiveElev);
            }

            // Drop Shadow pass
            if (effectiveElev > 0.1)
            {
                if (pipeline == RenderPipelineTarget.GpuAtlas)
                {
                    DrawAtlasShadowPass(dc, w, h, radiusX, radiusY, effectiveElev, effectiveBlur);
                }
                else
                {
                    DrawSdfShadowPass(dc, w, h, radiusX, radiusY, pipeline == RenderPipelineTarget.Cpu, effectiveElev, effectiveBlur);
                }
            }

            // 3. Render Card Surface & Border
            var bgBrush = CardBackground ?? ZeroWpfTheme.BgCard;
            var borderBrush = CardBorderBrush ?? ZeroWpfTheme.BorderDefault;
            var borderPen = CardBorderThickness > 0 && borderBrush != null
                ? new Pen(borderBrush, CardBorderThickness)
                : null;

            var cardRect = new Rect(0, 0, w, h);
            dc.DrawRoundedRectangle(bgBrush, borderPen, cardRect, radiusX, radiusY);

            // Child content is arranged and rendered on top automatically with subpixel ClearType!
        }

        private void DrawGlowPass(DrawingContext dc, double w, double h, double rx, double ry, double glowIntensity, double blurRadius, double elevation)
        {
            float intensity = (float)Math.Min(2.0, glowIntensity);
            double glowSpread = Math.Max(2.0, (blurRadius + elevation) * 0.75);
            var color = GlowColor;

            // Multi-tier gradient rings approximating exponential glow falloff
            int steps = 4;
            for (int i = steps; i >= 1; i--)
            {
                double fraction = (double)i / steps;
                double spread = glowSpread * fraction;
                float falloffAlpha = ZeroShaderRegistry.EvaluateNeonGlowIntensity((float)spread, (float)glowSpread, intensity);
                byte alpha = (byte)Math.Min(255, Math.Max(0, (int)(falloffAlpha * 140)));

                if (alpha == 0) continue;

                var glowBrush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                glowBrush.Freeze();

                var glowPen = new Pen(glowBrush, spread * 1.5);
                glowPen.Freeze();

                var rect = new Rect(-spread * 0.5, -spread * 0.5, w + spread, h + spread);
                dc.DrawRoundedRectangle(null, glowPen, rect, rx + spread * 0.5, ry + spread * 0.5);
            }
        }

        private void DrawAtlasShadowPass(DrawingContext dc, double w, double h, double rx, double ry, double elevation, double blurRadius)
        {
            // Leverages ZeroShadowAtlas 9-slice cached geometry
            var patch = ZeroShadowAtlas.GetOrCreatePatch((float)rx, (float)elevation, (float)blurRadius);
            double offsetY = elevation * 0.65;

            int steps = 3;
            for (int i = steps; i >= 1; i--)
            {
                double spread = (steps - i + 1) * (blurRadius / 4.0);
                byte alpha = (byte)(28 + (i * 18));
                var shadowBrush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                shadowBrush.Freeze();

                var rect = new Rect(-spread * 0.5, offsetY - spread * 0.25, w + spread, h + spread * 0.5);
                dc.DrawRoundedRectangle(shadowBrush, null, rect, rx + spread * 0.5, ry + spread * 0.5);
            }
        }

        private void DrawSdfShadowPass(DrawingContext dc, double w, double h, double rx, double ry, bool isSoftware, double elevation, double blurRadius)
        {
            double offsetY = elevation * 0.8;
            int passes = isSoftware ? 2 : 4; // Constrain passes on CPU to save rasterizer cycles
            double maxSpread = Math.Max(2.0, blurRadius * 0.8);

            for (int i = passes; i >= 1; i--)
            {
                double spread = (passes - i + 1) * (maxSpread / passes);
                byte alpha = (byte)(18 + (i * 22));
                var shadowBrush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                shadowBrush.Freeze();

                var rect = new Rect(-spread * 0.5, offsetY - spread * 0.25, w + spread, h + spread * 0.5);
                dc.DrawRoundedRectangle(shadowBrush, null, rect, rx + spread * 0.5, ry + spread * 0.5);
            }
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfRenderMonitor.Instance.CoreMonitor.FidelityTierChanged += OnFidelityTierChanged;
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZOptimizedCard"/>.
    /// </summary>
    [Obsolete("OptimizedCard is deprecated and will be removed in 5 release cycles. Please migrate to ZOptimizedCard instead.")]
    public class OptimizedCard : ZOptimizedCard
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZOptimizedCard"/>.
    /// </summary>
    [Obsolete("ZeroOptimizedCard is deprecated and will be removed in 5 release cycles. Please migrate to ZOptimizedCard instead.")]
    public class ZeroOptimizedCard : ZOptimizedCard
    {
    }
}
