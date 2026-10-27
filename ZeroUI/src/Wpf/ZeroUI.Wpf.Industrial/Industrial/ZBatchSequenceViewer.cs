using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class SequenceStep { public string Name { get; set; } = string.Empty; public string State { get; set; } = string.Empty; public TimeSpan Duration { get; set; } }
    /// <summary>
    /// ZBatchSequenceViewer WPF control.
    /// </summary>
    public class ZBatchSequenceViewer : FrameworkElement
    {        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(nameof(Steps), typeof(System.Collections.Generic.List<SequenceStep>), typeof(ZBatchSequenceViewer));
        public System.Collections.Generic.List<SequenceStep> Steps { get => (System.Collections.Generic.List<SequenceStep>)GetValue(StepsProperty); set => SetValue(StepsProperty, value); }
        public static readonly DependencyProperty CurrentStepProperty = DependencyProperty.Register(nameof(CurrentStep), typeof(SequenceStep), typeof(ZBatchSequenceViewer));
        public SequenceStep CurrentStep { get => (SequenceStep)GetValue(CurrentStepProperty); set => SetValue(CurrentStepProperty, value); }
        public static readonly DependencyProperty ShowTimelineProperty = DependencyProperty.Register(nameof(ShowTimeline), typeof(bool), typeof(ZBatchSequenceViewer));
        public bool ShowTimeline { get => (bool)GetValue(ShowTimelineProperty); set => SetValue(ShowTimelineProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroBatchSequenceViewer is deprecated.")]
    public class ZeroBatchSequenceViewer : ZBatchSequenceViewer { }

    [Obsolete("BatchSequenceViewer is deprecated.")]
    public class BatchSequenceViewer : ZBatchSequenceViewer { }
}
