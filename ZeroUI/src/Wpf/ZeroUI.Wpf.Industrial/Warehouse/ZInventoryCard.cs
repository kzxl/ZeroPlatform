using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Warehouse
{
    /// <summary>
    /// Industrial Inventory Stock Metric Card for Warehouse & MES.
    /// Displays stock telemetry: Available, Waiting, Reserved quantities,
    /// warehouse location, unit of measure, and visual allocation segment distribution.
    /// </summary>
    public class ZInventoryCard : FrameworkElement
    {
        public string ProductCode { get; set; } = "ABC-001";
        public string ProductName { get; set; } = "Cylindrical Bushing φ32 mm";
        public double AvailableQuantity { get; set; } = 1250;
        public double WaitingQuantity { get; set; } = 120;
        public double ReservedQuantity { get; set; } = 300;
        public string WarehouseName { get; set; } = "Main Central WH";
        public string LocationBin { get; set; } = "Zone A - Rack 03";
        public string UnitOfMeasure { get; set; } = "Pcs";

        public ZInventoryCard()
        {
            Height = 190;
            MinWidth = 280;
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Card background & border
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            // Product Code & Location Pill
            var codeFt = CreateFormattedText(ProductCode, ZeroWpfTheme.BoldTypeface, 13.0, ZeroWpfTheme.PrimaryAccent, dpi);
            dc.DrawText(codeFt, new Point(14, 12));

            var locFt = CreateFormattedText(LocationBin, ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
            dc.DrawText(locFt, new Point(w - locFt.Width - 14, 14));

            // Product Name
            var nameFt = CreateFormattedText(ProductName, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(nameFt, new Point(14, 34));

            // Quantities Row: Available (Large Big Metric)
            double total = Math.Max(1, AvailableQuantity + WaitingQuantity + ReservedQuantity);
            var availValFt = CreateFormattedText($"{AvailableQuantity:N0}", ZeroWpfTheme.BoldTypeface, 22.0, ZeroWpfTheme.SuccessAccent, dpi);
            var uomFt = CreateFormattedText(UnitOfMeasure, ZeroWpfTheme.RegularTypeface, 11.0, ZeroWpfTheme.TextMuted, dpi);

            dc.DrawText(availValFt, new Point(14, 62));
            dc.DrawText(uomFt, new Point(16 + availValFt.Width, 70));

            // Metrics Summary
            var availLblFt = CreateFormattedText("Available for SMT", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(availLblFt, new Point(14, 90));

            // Multi-segment Allocation Bar
            double barX = 14;
            double barY = 114;
            double barW = w - 28;
            double barH = 10;

            dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, null, new Rect(barX, barY, barW, barH), 3, 3);

            double availW = (AvailableQuantity / total) * barW;
            double waitW = (WaitingQuantity / total) * barW;
            double resW = (ReservedQuantity / total) * barW;

            if (availW > 0)
            {
                dc.DrawRoundedRectangle(ZeroWpfTheme.SuccessAccent, null, new Rect(barX, barY, availW, barH), 2, 2);
            }
            if (waitW > 0)
            {
                dc.DrawRoundedRectangle(ZeroWpfTheme.WarningAccent, null, new Rect(barX + availW, barY, waitW, barH), 2, 2);
            }
            if (resW > 0)
            {
                dc.DrawRoundedRectangle(ZeroWpfTheme.SecondaryAccent, null, new Rect(barX + availW + waitW, barY, resW, barH), 2, 2);
            }

            // Legend Ticks
            double legendY = 136;
            // Dot 1: Available
            dc.DrawEllipse(ZeroWpfTheme.SuccessAccent, null, new Point(18, legendY + 5), 3, 3);
            var leg1Ft = CreateFormattedText($"Avail: {AvailableQuantity:0}", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(leg1Ft, new Point(26, legendY));

            // Dot 2: Waiting
            double leg2X = 26 + leg1Ft.Width + 14;
            dc.DrawEllipse(ZeroWpfTheme.WarningAccent, null, new Point(leg2X, legendY + 5), 3, 3);
            var leg2Ft = CreateFormattedText($"Wait: {WaitingQuantity:0}", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(leg2Ft, new Point(leg2X + 8, legendY));

            // Dot 3: Reserved
            double leg3X = leg2X + 8 + leg2Ft.Width + 14;
            dc.DrawEllipse(ZeroWpfTheme.SecondaryAccent, null, new Point(leg3X, legendY + 5), 3, 3);
            var leg3Ft = CreateFormattedText($"Res: {ReservedQuantity:0}", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(leg3Ft, new Point(leg3X + 8, legendY));

            // Warehouse bottom caption
            var whFt = CreateFormattedText($"📍 {WarehouseName}", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
            dc.DrawText(whFt, new Point(14, 162));
        }
    
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
    /// Legacy alias for <see cref="ZInventoryCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("InventoryCard is deprecated and will be removed in 5 release cycles. Please migrate to ZInventoryCard instead.")]
    public class InventoryCard : ZInventoryCard { }

    /// <summary>
    /// Legacy alias for <see cref="ZInventoryCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroInventoryCard is deprecated and will be removed in 5 release cycles. Please migrate to ZInventoryCard instead.")]
    public class ZeroInventoryCard : ZInventoryCard { }

    #endregion

}
