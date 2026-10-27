using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Compliance
{
    /// <summary>
    /// ZComplianceChecklist control for Compliance.
    /// </summary>
    public class ZComplianceChecklist : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.Compliance.ChecklistItem> Items { get; set; } public double CompletionPercent { get { return 0; } } public bool ShowProgress { get; set; } public event EventHandler ItemChecked; public event EventHandler ChecklistCompleted;

        public ZComplianceChecklist()
        {
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var rect = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : 100, ActualHeight > 0 ? ActualHeight : 100);
            drawingContext.DrawRectangle(Brushes.Transparent, null, rect);
            
            var ft = new FormattedText("ZComplianceChecklist rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZComplianceChecklist instead.")]
    public class ZeroComplianceChecklist : ZComplianceChecklist { }
}
