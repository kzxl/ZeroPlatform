using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Compliance;

namespace ZeroUI.Wpf.Compliance
{
    public class ZAuditTrail : FrameworkElement
    {
        public List<AuditEntry> Entries { get; set; } = new List<AuditEntry>();
        public string? Filter { get; set; }
        public bool ShowDiff { get; set; }
        public int MaxEntries { get; set; }

        

        public ZAuditTrail()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged() => InvalidateVisual();

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        
    }

    [Obsolete("AuditTrail is deprecated and will be removed in 5 release cycles. Please migrate to ZAuditTrail instead.")]
    public class AuditTrail : ZAuditTrail { }

    [Obsolete("ZeroAuditTrail is deprecated. Please use ZAuditTrail instead.")]
    public class ZeroAuditTrail : ZAuditTrail { }
}
