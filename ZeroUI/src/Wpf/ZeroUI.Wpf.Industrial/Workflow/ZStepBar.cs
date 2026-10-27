using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Workflow;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Workflow
{
    public class StepClickEventArgs : EventArgs
    {
        public int Index { get; }
        public StepItem Step { get; }

        public StepClickEventArgs(int index, StepItem step)
        {
            Index = index;
            Step = step;
        }
    }

    /// <summary>
    /// High-performance vector workflow pipeline stepper for WPF.
    /// Provides DPI-independent vector rendering via DrawingContext,
    /// dynamic theme integration, and interactive step navigation.
    /// </summary>
    public class ZStepBar : FrameworkElement
    {
        private readonly StepBarModel _model = new StepBarModel();
        private readonly List<Rect> _nodeBounds = new List<Rect>();
        private readonly List<Rect> _stepHitAreas = new List<Rect>();

        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;

        #region Dependency Properties

        public static readonly DependencyProperty CurrentIndexProperty =
            DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(ZStepBar),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentIndexChangedCallback));

        public static readonly DependencyProperty NodeSizeProperty =
            DependencyProperty.Register(nameof(NodeSize), typeof(double), typeof(ZStepBar),
                new FrameworkPropertyMetadata(32.0, FrameworkPropertyMetadataOptions.AffectsRender, OnNodeSizeChangedCallback));

        public static readonly DependencyProperty AllowClickToNavigateProperty =
            DependencyProperty.Register(nameof(AllowClickToNavigate), typeof(bool), typeof(ZStepBar),
                new FrameworkPropertyMetadata(true));

        private static void OnCurrentIndexChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZStepBar bar && e.NewValue is int idx)
            {
                if (bar._model.CurrentIndex != idx)
                {
                    bar._model.SetCurrentStep(idx);
                }
            }
        }

        private static void OnNodeSizeChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZStepBar bar)
            {
                bar.RecalculateLayout(bar.ActualWidth, bar.ActualHeight);
                bar.InvalidateVisual();
            }
        }

        #endregion

        #region Events & Properties

        public event EventHandler<StepClickEventArgs>? StepClick;
        public event EventHandler<int>? CurrentIndexChanged;

        public StepBarModel Model => _model;
        public IReadOnlyList<StepItem> Steps => _model.Steps;

        public int CurrentIndex
        {
            get => (int)GetValue(CurrentIndexProperty);
            set => SetValue(CurrentIndexProperty, value);
        }

        public double NodeSize
        {
            get => (double)GetValue(NodeSizeProperty);
            set => SetValue(NodeSizeProperty, value);
        }

        public bool AllowClickToNavigate
        {
            get => (bool)GetValue(AllowClickToNavigateProperty);
            set => SetValue(AllowClickToNavigateProperty, value);
        }

        #endregion

        public ZStepBar()
        {
            Height = 72;
            Cursor = Cursors.Arrow;
            Focusable = true;

            _model.ModelChanged += (s, e) =>
            {
                RecalculateLayout(ActualWidth, ActualHeight);
                InvalidateVisual();
            };

            _model.CurrentIndexChanged += (s, idx) =>
            {
                if (CurrentIndex != idx) CurrentIndex = idx;
                CurrentIndexChanged?.Invoke(this, idx);
                InvalidateVisual();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += (s, e) =>
            {
                RecalculateLayout(e.NewSize.Width, e.NewSize.Height);
                InvalidateVisual();
            };
        }

        #region Public Methods

        public void AddStep(string key, string title, string? description = null, StepStatus status = StepStatus.Pending)
        {
            _model.AddStep(key, title, description, status);
        }

        public void SetSteps(IEnumerable<StepItem> steps)
        {
            _model.SetSteps(steps);
        }

        public void SetStepStatus(int index, StepStatus status)
        {
            _model.SetStepStatus(index, status);
        }

        public bool NextStep() => _model.NextStep();

        public bool PrevStep() => _model.PrevStep();

        public void Clear() => _model.Clear();

        #endregion

        #region Layout & Hit-Testing

        private void RecalculateLayout(double w, double h)
        {
            _nodeBounds.Clear();
            _stepHitAreas.Clear();

            int count = _model.Count;
            if (count == 0 || w <= 0 || h <= 0) return;

            double padX = Math.Max(40.0, NodeSize + 10.0);
            double usableWidth = Math.Max(10.0, w - (padX * 2.0));
            double stepSpacing = count > 1 ? usableWidth / (count - 1) : 0;
            double nodeY = 10.0;

            for (int i = 0; i < count; i++)
            {
                double centerX = count == 1 ? w / 2.0 : padX + (i * stepSpacing);
                double nodeX = centerX - (NodeSize / 2.0);
                var nodeRect = new Rect(nodeX, nodeY, NodeSize, NodeSize);
                _nodeBounds.Add(nodeRect);

                double hitLeft = Math.Max(0, centerX - (stepSpacing > 0 ? stepSpacing / 2.0 : padX));
                double hitRight = Math.Min(w, centerX + (stepSpacing > 0 ? stepSpacing / 2.0 : padX));
                _stepHitAreas.Add(new Rect(hitLeft, 0, hitRight - hitLeft, h));
            }
        }

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _stepHitAreas.Count; i++)
            {
                if (_stepHitAreas[i].Contains(pt))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            int hit = HitTest(pt);
            if (hit != _hoveredIndex)
            {
                _hoveredIndex = hit;
                Cursor = (_hoveredIndex >= 0 && AllowClickToNavigate && _model.Steps[_hoveredIndex].Enabled)
                    ? Cursors.Hand
                    : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _pressedIndex = HitTest(e.GetPosition(this));
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            int hit = HitTest(e.GetPosition(this));
            if (hit >= 0 && hit == _pressedIndex)
            {
                var step = _model.Steps[hit];
                if (step.Enabled)
                {
                    StepClick?.Invoke(this, new StepClickEventArgs(hit, step));
                    if (AllowClickToNavigate)
                    {
                        _model.SetCurrentStep(hit);
                    }
                }
            }
            _pressedIndex = -1;
            InvalidateVisual();
        }

        #endregion

        #region Render Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bgBrush = ZeroWpfTheme.BgPrimary;
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            int count = _model.Count;
            if (count == 0 || _nodeBounds.Count != count) return;

            var primaryBrush = ZeroWpfTheme.PrimaryAccent;
            var borderBrush = ZeroWpfTheme.BorderDefault;
            var textPrimary = ZeroWpfTheme.TextPrimary;
            var textSecondary = ZeroWpfTheme.TextSecondary;
            var textMuted = ZeroWpfTheme.TextMuted;
            var successBrush = ZeroWpfTheme.SuccessAccent;
            var dangerBrush = ZeroWpfTheme.DangerAccent;

            // 1. Draw connecting tracks
            for (int i = 0; i < count - 1; i++)
            {
                var n1 = _nodeBounds[i];
                var n2 = _nodeBounds[i + 1];

                bool isCompleted = (i < _model.CurrentIndex);
                var trackBrush = isCompleted ? primaryBrush : borderBrush;
                double trackThickness = isCompleted ? 3.0 : 2.0;

                double y = n1.Y + (n1.Height / 2.0);
                double startX = n1.Right + 4.0;
                double endX = n2.Left - 4.0;

                if (endX > startX)
                {
                    dc.DrawLine(new Pen(trackBrush, trackThickness), new Point(startX, y), new Point(endX, y));
                }
            }

            // 2. Draw nodes and labels
            for (int i = 0; i < count; i++)
            {
                var step = _model.Steps[i];
                var nodeRect = _nodeBounds[i];
                bool isHovered = (i == _hoveredIndex && step.Enabled);
                bool isPressed = (i == _pressedIndex);

                DrawNode(dc, step, nodeRect, i + 1, isHovered, isPressed, primaryBrush, successBrush, dangerBrush, borderBrush, textSecondary);
                DrawText(dc, step, nodeRect, primaryBrush, textPrimary, textSecondary, textMuted, dangerBrush);
            }
        }

        private void DrawNode(DrawingContext dc, StepItem step, Rect rect, int stepNumber, bool isHovered, bool isPressed,
            Brush primary, Brush success, Brush danger, Brush border, Brush textSecondary)
        {
            Brush nodeBg;
            Pen nodePen;
            Brush glyphBrush = Brushes.White;
            string glyphText = stepNumber.ToString();

            switch (step.Status)
            {
                case StepStatus.Completed:
                    nodeBg = success;
                    nodePen = new Pen(success, 2.0);
                    glyphText = "✔";
                    break;
                case StepStatus.Current:
                    nodeBg = primary;
                    nodePen = new Pen(primary, 2.0);
                    break;
                case StepStatus.Error:
                    nodeBg = danger;
                    nodePen = new Pen(danger, 2.0);
                    glyphText = "✖";
                    break;
                case StepStatus.Disabled:
                    nodeBg = ZeroWpfTheme.BgCard;
                    nodePen = new Pen(border, 2.0);
                    glyphBrush = textSecondary;
                    break;
                case StepStatus.Pending:
                default:
                    nodeBg = ZeroWpfTheme.BgPrimary;
                    nodePen = new Pen(isHovered ? primary : border, 2.0);
                    glyphBrush = isHovered ? primary : textSecondary;
                    break;
            }

            Point center = new Point(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));
            double radius = (rect.Width / 2.0);
            if (isPressed) radius -= 1.0;

            if (isHovered && step.Status == StepStatus.Current)
            {
                var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 59, 130, 246)), 4.0);
                dc.DrawEllipse(null, glowPen, center, radius + 2.0, radius + 2.0);
            }

            dc.DrawEllipse(nodeBg, nodePen, center, radius, radius);

            // Draw glyph/number
#pragma warning disable CS0618
            var ft = new FormattedText(
                glyphText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12,
                glyphBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
#pragma warning restore CS0618

            dc.DrawText(ft, new Point(center.X - (ft.Width / 2.0), center.Y - (ft.Height / 2.0)));
        }

        private void DrawText(DrawingContext dc, StepItem step, Rect nodeRect,
            Brush primary, Brush textPrimary, Brush textSecondary, Brush textMuted, Brush danger)
        {
            double textTop = nodeRect.Bottom + 6.0;
            double textWidth = Math.Max(120.0, NodeSize * 3.0);
            double textLeft = (nodeRect.Left + (nodeRect.Width / 2.0)) - (textWidth / 2.0);

            Brush titleBrush = step.Status switch
            {
                StepStatus.Current => primary,
                StepStatus.Completed => textPrimary,
                StepStatus.Error => danger,
                StepStatus.Disabled => textMuted,
                _ => textSecondary
            };

#pragma warning disable CS0618
            var ftTitle = new FormattedText(
                step.Title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                12,
                titleBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = textWidth,
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(ftTitle, new Point(textLeft, textTop));

            if (!string.IsNullOrEmpty(step.Description))
            {
                var ftDesc = new FormattedText(
                    step.Description,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    10.5,
                    textMuted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = textWidth,
                    TextAlignment = TextAlignment.Center
                };
                dc.DrawText(ftDesc, new Point(textLeft, textTop + ftTitle.Height + 2.0));
            }
#pragma warning restore CS0618
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStepBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StepBar is deprecated and will be removed in 5 release cycles. Please migrate to ZStepBar instead.")]
    public class StepBar : ZStepBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZStepBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroStepBar is deprecated and will be removed in 5 release cycles. Please migrate to ZStepBar instead.")]
    public class ZeroStepBar : ZStepBar { }

    #endregion

}
