using System;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Geometric shape presentation mode for conversion pipeline visualizers.
    /// </summary>
    public enum FunnelChartMode
    {
        /// <summary>Top is widest and tapers downward to narrow neck (standard sales/conversion funnel).</summary>
        Funnel = 0,

        /// <summary>Top is narrowest (apex) and expands downward to wide base (hierarchical pyramid chart).</summary>
        Pyramid = 1
    }

    /// <summary>
    /// Mathematical calculations for funnel and pyramid trapezoid geometries and yield analytics.
    /// </summary>
    public static class FunnelMath
    {
        /// <summary>
        /// Calculates top and bottom segment widths for a given stage index.
        /// </summary>
        /// <param name="stageIndex">0-indexed stage position.</param>
        /// <param name="totalStages">Total number of stages.</param>
        /// <param name="maxWidth">Maximum width allowed for the widest tier.</param>
        /// <param name="minWidth">Minimum neck/apex width.</param>
        /// <param name="mode">Funnel or Pyramid presentation mode.</param>
        /// <param name="wTop">Calculated top segment width.</param>
        /// <param name="wBot">Calculated bottom segment width.</param>
        public static void CalculateSegmentWidths(
            int stageIndex,
            int totalStages,
            float maxWidth,
            float minWidth,
            FunnelChartMode mode,
            out float wTop,
            out float wBot)
        {
            if (totalStages <= 0)
            {
                wTop = minWidth;
                wBot = minWidth;
                return;
            }

            maxWidth = Math.Max(minWidth, maxWidth);
            minWidth = Math.Max(10f, minWidth);

            float tRatio = stageIndex / (float)totalStages;
            float bRatio = (stageIndex + 1) / (float)totalStages;

            if (mode == FunnelChartMode.Funnel)
            {
                // Top is widest, bottom is narrowest
                wTop = maxWidth - (maxWidth - minWidth) * tRatio;
                wBot = maxWidth - (maxWidth - minWidth) * bRatio;
            }
            else
            {
                // Pyramid: Top is narrowest, bottom is widest
                wTop = minWidth + (maxWidth - minWidth) * tRatio;
                wBot = minWidth + (maxWidth - minWidth) * bRatio;
            }
        }

        /// <summary>
        /// Calculates the stage-to-stage conversion rate percentage.
        /// </summary>
        /// <param name="currentStageValue">Value of current stage.</param>
        /// <param name="previousStageValue">Value of preceding stage.</param>
        /// <returns>Percentage conversion (e.g. 95.2%). Returns 100.0% if previous value is 0 or negative.</returns>
        public static double CalculateConversionRate(double currentStageValue, double previousStageValue)
        {
            if (previousStageValue <= 0) return 100.0;
            return Math.Max(0.0, (currentStageValue / previousStageValue) * 100.0);
        }

        /// <summary>
        /// Calculates the drop-off or scrap loss rate percentage.
        /// </summary>
        public static double CalculateDropOffRate(double currentStageValue, double previousStageValue)
        {
            double conversion = CalculateConversionRate(currentStageValue, previousStageValue);
            return Math.Max(0.0, 100.0 - conversion);
        }

        /// <summary>
        /// Calculates the cumulative yield percentage relative to the initial intake.
        /// </summary>
        public static double CalculateOverallYield(double currentStageValue, double initialIntakeValue)
        {
            if (initialIntakeValue <= 0) return 100.0;
            return Math.Max(0.0, (currentStageValue / initialIntakeValue) * 100.0);
        }
    }
}
