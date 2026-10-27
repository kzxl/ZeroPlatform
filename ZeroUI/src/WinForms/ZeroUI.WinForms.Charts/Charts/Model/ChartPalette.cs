using System;
using System.Drawing;

namespace ZeroUI.WinForms.Charts.Model
{
    /// <summary>
    /// Color palettes and harmonious palette generators for ZeroUI charts.
    /// </summary>
    public static class ChartPalette
    {
        public static readonly Color[] ModernVibrant = new[]
        {
            Color.FromArgb(79, 70, 229),   // Indigo 600
            Color.FromArgb(16, 185, 129),  // Emerald 500
            Color.FromArgb(245, 158, 11),  // Amber 500
            Color.FromArgb(6, 182, 212),   // Cyan 500
            Color.FromArgb(244, 63, 94),   // Rose 500
            Color.FromArgb(139, 92, 246),  // Violet 500
            Color.FromArgb(14, 165, 233),  // Sky 500
            Color.FromArgb(132, 204, 22),  // Lime 500
            Color.FromArgb(236, 72, 153),  // Pink 500
            Color.FromArgb(100, 116, 139)  // Slate 500
        };

        public static readonly Color[] DarkVibrant = new[]
        {
            Color.FromArgb(129, 140, 248), // Indigo 400
            Color.FromArgb(52, 211, 153),  // Emerald 400
            Color.FromArgb(251, 191, 36),  // Amber 400
            Color.FromArgb(34, 211, 238),  // Cyan 400
            Color.FromArgb(251, 113, 133), // Rose 400
            Color.FromArgb(167, 139, 250), // Violet 400
            Color.FromArgb(56, 189, 248),  // Sky 400
            Color.FromArgb(163, 230, 53)   // Lime 400
        };

        public static readonly Color[] OceanTeal = new[]
        {
            Color.FromArgb(14, 116, 144),  // Cyan 700
            Color.FromArgb(6, 182, 212),   // Cyan 500
            Color.FromArgb(20, 184, 166),  // Teal 500
            Color.FromArgb(45, 212, 191),  // Teal 400
            Color.FromArgb(56, 189, 248),  // Sky 400
            Color.FromArgb(125, 211, 252)  // Sky 300
        };

        public static readonly Color[] SunsetCoral = new[]
        {
            Color.FromArgb(190, 24, 93),   // Pink 700
            Color.FromArgb(225, 29, 72),   // Rose 600
            Color.FromArgb(234, 88, 12),   // Orange 600
            Color.FromArgb(245, 158, 11),  // Amber 500
            Color.FromArgb(251, 191, 36)   // Amber 400
        };

        public static Color GetColor(int index, bool isDark = false)
        {
            var palette = isDark ? DarkVibrant : ModernVibrant;
            return palette[Math.Abs(index) % palette.Length];
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ChartPalette"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartPalette is deprecated. Please use ChartPalette instead.")]
    public static class ZeroChartPalette
    {
        public static Color[] ModernVibrant => ChartPalette.ModernVibrant;
        public static Color[] DarkVibrant => ChartPalette.DarkVibrant;
        public static Color[] OceanTeal => ChartPalette.OceanTeal;
        public static Color[] SunsetCoral => ChartPalette.SunsetCoral;
        public static Color GetColor(int index, bool isDark = false) => ChartPalette.GetColor(index, isDark);
    }
}
