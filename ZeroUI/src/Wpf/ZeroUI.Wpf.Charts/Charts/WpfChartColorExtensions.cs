using System.Windows.Media;

namespace ZeroUI.Wpf.Charts
{
    internal static class WpfChartColorExtensions
    {
        public static Brush ToBrush(this uint? rgba, Brush fallback)
        {
            if (!rgba.HasValue) return fallback;
            uint val = rgba.Value;
            byte a = (byte)((val >> 24) & 0xFF);
            byte r = (byte)((val >> 16) & 0xFF);
            byte g = (byte)((val >> 8) & 0xFF);
            byte b = (byte)(val & 0xFF);
            if (a == 0 && val != 0) a = 255;
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        public static Color ToColor(this uint? rgba, Color fallback)
        {
            if (!rgba.HasValue) return fallback;
            uint val = rgba.Value;
            byte a = (byte)((val >> 24) & 0xFF);
            byte r = (byte)((val >> 16) & 0xFF);
            byte g = (byte)((val >> 8) & 0xFF);
            byte b = (byte)(val & 0xFF);
            if (a == 0 && val != 0) a = 255;
            return Color.FromArgb(a, r, g, b);
        }
    }
}
