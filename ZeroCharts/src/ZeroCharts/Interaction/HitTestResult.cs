using System.Drawing;

namespace ZeroCharts.Interaction
{
    /// <summary>
    /// Encapsulates metadata for a data point under the mouse cursor.
    /// </summary>
    public class HitTestResult
    {
        public string SeriesName { get; }
        public string CategoryOrX { get; }
        public string FormattedValue { get; }
        public PointF ScreenLocation { get; }
        public Color SeriesColor { get; }

        public HitTestResult(
            string seriesName,
            string categoryOrX,
            string formattedValue,
            PointF screenLocation,
            Color seriesColor)
        {
            SeriesName = seriesName ?? string.Empty;
            CategoryOrX = categoryOrX ?? string.Empty;
            FormattedValue = formattedValue ?? string.Empty;
            ScreenLocation = screenLocation;
            SeriesColor = seriesColor;
        }
    }
}
