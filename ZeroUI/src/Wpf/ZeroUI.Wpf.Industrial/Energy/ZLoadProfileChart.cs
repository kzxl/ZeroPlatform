using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZLoadProfileChart WPF control.
    /// </summary>
    public class ZLoadProfileChart : FrameworkElement
    {        public static readonly DependencyProperty DemandDataProperty = DependencyProperty.Register(nameof(DemandData), typeof(double[]), typeof(ZLoadProfileChart));
        public double[] DemandData { get => (double[])GetValue(DemandDataProperty); set => SetValue(DemandDataProperty, value); }
        public static readonly DependencyProperty CapacityLimitProperty = DependencyProperty.Register(nameof(CapacityLimit), typeof(double), typeof(ZLoadProfileChart));
        public double CapacityLimit { get => (double)GetValue(CapacityLimitProperty); set => SetValue(CapacityLimitProperty, value); }
        public static readonly DependencyProperty PeakHighlightProperty = DependencyProperty.Register(nameof(PeakHighlight), typeof(bool), typeof(ZLoadProfileChart));
        public bool PeakHighlight { get => (bool)GetValue(PeakHighlightProperty); set => SetValue(PeakHighlightProperty, value); }
        public static readonly DependencyProperty TimeLabelsProperty = DependencyProperty.Register(nameof(TimeLabels), typeof(System.Collections.Generic.List<string>), typeof(ZLoadProfileChart));
        public System.Collections.Generic.List<string> TimeLabels { get => (System.Collections.Generic.List<string>)GetValue(TimeLabelsProperty); set => SetValue(TimeLabelsProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroLoadProfileChart is deprecated.")]
    public class ZeroLoadProfileChart : ZLoadProfileChart { }

    [Obsolete("LoadProfileChart is deprecated.")]
    public class LoadProfileChart : ZLoadProfileChart { }
}
