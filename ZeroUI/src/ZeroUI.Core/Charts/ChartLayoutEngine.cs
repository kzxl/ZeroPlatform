using System;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Platform-neutral 2D bounding rectangle.
    /// </summary>
    public readonly struct ChartRectF
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public ChartRectF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = Math.Max(0f, width);
            Height = Math.Max(0f, height);
        }

        public bool Contains(float px, float py)
        {
            return px >= Left && px <= Right && py >= Top && py <= Bottom;
        }
    }

    /// <summary>
    /// Layout results computed by the ChartLayoutEngine.
    /// </summary>
    public class ChartLayoutResult
    {
        public ChartRectF ViewportBounds { get; set; }
        public ChartRectF TitleArea { get; set; }
        public ChartRectF LegendArea { get; set; }
        public ChartRectF PlotArea { get; set; }
        public ChartRectF YAxisArea { get; set; }
        public ChartRectF XAxisArea { get; set; }
    }

    /// <summary>
    /// Platform-neutral geometric layout engine for ZeroUI charts.
    /// </summary>
    public static class ChartLayoutEngine
    {
        /// <summary>
        /// Calculates geometric zones (Title, Legend, Axes, PlotArea) with zero allocations.
        /// </summary>
        public static ChartLayoutResult CalculateLayout(
            float containerWidth,
            float containerHeight,
            bool hasTitle,
            float titleHeight,
            ChartLegendPosition legendPosition,
            float legendBreadth,
            float yAxisWidth = 48f,
            float xAxisHeight = 24f,
            float padding = 12f)
        {
            var result = new ChartLayoutResult
            {
                ViewportBounds = new ChartRectF(0, 0, containerWidth, containerHeight)
            };

            float curTop = padding;
            float curLeft = padding;
            float curRight = containerWidth - padding;
            float curBottom = containerHeight - padding;

            // 1. Title Area
            if (hasTitle && titleHeight > 0)
            {
                result.TitleArea = new ChartRectF(curLeft, curTop, curRight - curLeft, titleHeight);
                curTop += titleHeight + 4f;
            }

            // 2. Legend Area
            if (legendPosition == ChartLegendPosition.Top && legendBreadth > 0)
            {
                result.LegendArea = new ChartRectF(curLeft, curTop, curRight - curLeft, legendBreadth);
                curTop += legendBreadth + 4f;
            }
            else if (legendPosition == ChartLegendPosition.Bottom && legendBreadth > 0)
            {
                result.LegendArea = new ChartRectF(curLeft, curBottom - legendBreadth, curRight - curLeft, legendBreadth);
                curBottom -= legendBreadth + 4f;
            }
            else if (legendPosition == ChartLegendPosition.Right && legendBreadth > 0)
            {
                result.LegendArea = new ChartRectF(curRight - legendBreadth, curTop, legendBreadth, curBottom - curTop);
                curRight -= legendBreadth + 8f;
            }
            else if (legendPosition == ChartLegendPosition.Left && legendBreadth > 0)
            {
                result.LegendArea = new ChartRectF(curLeft, curTop, legendBreadth, curBottom - curTop);
                curLeft += legendBreadth + 8f;
            }

            // 3. Axes
            float plotLeft = curLeft + yAxisWidth;
            float plotRight = curRight;
            float plotTop = curTop;
            float plotBottom = curBottom - xAxisHeight;

            float plotWidth = Math.Max(10f, plotRight - plotLeft);
            float plotHeight = Math.Max(10f, plotBottom - plotTop);

            result.YAxisArea = new ChartRectF(curLeft, plotTop, yAxisWidth, plotHeight);
            result.XAxisArea = new ChartRectF(plotLeft, plotBottom, plotWidth, xAxisHeight);
            result.PlotArea = new ChartRectF(plotLeft, plotTop, plotWidth, plotHeight);

            return result;
        }
    }
}
