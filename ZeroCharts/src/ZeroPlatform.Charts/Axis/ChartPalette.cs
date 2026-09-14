using System.Drawing;

namespace ZeroPlatform.Charts.Axis
{
    /// <summary>
    /// Industrial UI theme palette conforming to ZeroUI Obsidian Dark (#12151C).
    /// </summary>
    public static class ChartPalette
    {
        public static readonly Color BackgroundDark = Color.FromArgb(18, 21, 28);      // #12151C
        public static readonly Color PlotAreaDark = Color.FromArgb(24, 28, 37);        // #181C25
        public static readonly Color GridLine = Color.FromArgb(42, 48, 60);            // #2A303C
        public static readonly Color AxisText = Color.FromArgb(144, 164, 174);         // #90A4AE
        public static readonly Color CrosshairLine = Color.FromArgb(255, 255, 255);    // #FFFFFF

        // High-contrast accent series colors
        public static readonly Color Sapphire = Color.FromArgb(0, 168, 255);
        public static readonly Color Emerald = Color.FromArgb(0, 230, 118);
        public static readonly Color Amber = Color.FromArgb(255, 214, 0);
        public static readonly Color Crimson = Color.FromArgb(255, 23, 68);
        public static readonly Color Violet = Color.FromArgb(170, 0, 255);
        public static readonly Color Cyan = Color.FromArgb(0, 229, 255);

        public static readonly Color[] SeriesCycle = new[]
        {
            Sapphire,
            Emerald,
            Amber,
            Crimson,
            Violet,
            Cyan
        };
    }
}
