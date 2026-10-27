using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class BatchRecordEntry { public string Step { get; set; } = string.Empty; public string Parameter { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public DateTime Timestamp { get; set; } public string Operator { get; set; } = string.Empty; public string Signature { get; set; } = string.Empty; }
    /// <summary>
    /// ZBatchRecord WPF control.
    /// </summary>
    public class ZBatchRecord : FrameworkElement
    {        public static readonly DependencyProperty RecordsProperty = DependencyProperty.Register(nameof(Records), typeof(System.Collections.Generic.List<BatchRecordEntry>), typeof(ZBatchRecord));
        public System.Collections.Generic.List<BatchRecordEntry> Records { get => (System.Collections.Generic.List<BatchRecordEntry>)GetValue(RecordsProperty); set => SetValue(RecordsProperty, value); }
        public static readonly DependencyProperty ShowSignaturesProperty = DependencyProperty.Register(nameof(ShowSignatures), typeof(bool), typeof(ZBatchRecord));
        public bool ShowSignatures { get => (bool)GetValue(ShowSignaturesProperty); set => SetValue(ShowSignaturesProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroBatchRecord is deprecated.")]
    public class ZeroBatchRecord : ZBatchRecord { }

    [Obsolete("BatchRecord is deprecated.")]
    public class BatchRecord : ZBatchRecord { }
}
