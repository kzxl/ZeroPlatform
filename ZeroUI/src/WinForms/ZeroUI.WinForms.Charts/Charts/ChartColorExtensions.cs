using System.Drawing;

namespace ZeroUI.WinForms.Charts
{
    internal static class ChartColorExtensions
    {
        public static Color ToColor(this uint? rgba, Color fallback)
        {
            return rgba.HasValue ? Color.FromArgb((int)rgba.Value) : fallback;
        }

        public static uint ToRgba(this Color color)
        {
            return (uint)color.ToArgb();
        }
    }
}
