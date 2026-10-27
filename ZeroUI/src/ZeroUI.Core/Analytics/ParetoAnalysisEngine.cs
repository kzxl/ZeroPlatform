using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Platform-agnostic data item representing a defect category or frequency occurrence in Pareto analysis.
    /// </summary>
    public class ParetoItem
    {
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public ParetoItem() { }

        public ParetoItem(string category, double value, uint? colorRgba = null)
        {
            Category = category ?? string.Empty;
            Value = value;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Evaluated row containing mathematical ranking, percentage share, and cumulative metrics.
    /// </summary>
    public class ParetoResultRow
    {
        public ParetoItem Item { get; set; } = null!;
        public int Rank { get; set; }
        public double Percentage { get; set; }
        public double CumulativeValue { get; set; }
        public double CumulativePercentage { get; set; }
        public bool IsVitalFew { get; set; }
    }

    /// <summary>
    /// Encapsulates the complete Pareto 80/20 mathematical evaluation.
    /// </summary>
    public class ParetoAnalysisResult
    {
        public IReadOnlyList<ParetoResultRow> Rows { get; }
        public double TotalSum { get; }
        public int VitalFewCount { get; }
        public double CutoffPercentage { get; }

        public ParetoAnalysisResult(IReadOnlyList<ParetoResultRow> rows, double totalSum, int vitalFewCount, double cutoffPercentage)
        {
            Rows = rows;
            TotalSum = totalSum;
            VitalFewCount = vitalFewCount;
            CutoffPercentage = cutoffPercentage;
        }
    }

    /// <summary>
    /// Core calculation engine implementing Juran's Pareto Principle (80/20 rule) with descending sorting,
    /// cumulative frequency curves, and vital-few root cause identification.
    /// </summary>
    public static class ParetoEngine
    {
        public static ParetoAnalysisResult Compute(IEnumerable<ParetoItem> items, double cutoffPercentage = 80.0)
        {
            if (items == null)
            {
                return new ParetoAnalysisResult(Array.Empty<ParetoResultRow>(), 0, 0, cutoffPercentage);
            }

            var validItems = items.Where(x => x != null && x.Value > 0).OrderByDescending(x => x.Value).ToList();
            if (validItems.Count == 0)
            {
                return new ParetoAnalysisResult(Array.Empty<ParetoResultRow>(), 0, 0, cutoffPercentage);
            }

            double totalSum = validItems.Sum(x => x.Value);
            double runningSum = 0;
            var rows = new List<ParetoResultRow>(validItems.Count);

            int vitalFewCount = 0;
            bool cutoffReached = false;

            for (int i = 0; i < validItems.Count; i++)
            {
                var itm = validItems[i];
                runningSum += itm.Value;
                double pct = totalSum > 0 ? (itm.Value / totalSum) * 100.0 : 0;
                double cumPct = totalSum > 0 ? (runningSum / totalSum) * 100.0 : 0;

                bool isVital = false;
                if (!cutoffReached)
                {
                    isVital = true;
                    vitalFewCount++;
                    if (cumPct >= cutoffPercentage)
                    {
                        cutoffReached = true;
                    }
                }

                rows.Add(new ParetoResultRow
                {
                    Item = itm,
                    Rank = i + 1,
                    Percentage = pct,
                    CumulativeValue = runningSum,
                    CumulativePercentage = cumPct,
                    IsVitalFew = isVital
                });
            }

            if (vitalFewCount == 0 && rows.Count > 0)
            {
                vitalFewCount = 1;
                rows[0].IsVitalFew = true;
            }

            return new ParetoAnalysisResult(rows, totalSum, vitalFewCount, cutoffPercentage);
        }
    }
}
