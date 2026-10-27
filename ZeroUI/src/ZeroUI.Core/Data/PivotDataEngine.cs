using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZeroUI.Core.Data
{
    public class PivotDimension
    {
        public int FieldIndex { get; set; }
        public string Name { get; set; }

        public PivotDimension(int fieldIndex, string name)
        {
            FieldIndex = fieldIndex;
            Name = name;
        }
    }

    public class PivotMeasure
    {
        public int FieldIndex { get; set; }
        public string Name { get; set; }
        public GroupSummaryType SummaryType { get; set; } = GroupSummaryType.Sum;
        public string? FormatString { get; set; }

        public PivotMeasure(int fieldIndex, string name, GroupSummaryType summaryType = GroupSummaryType.Sum, string? formatString = null)
        {
            FieldIndex = fieldIndex;
            Name = name;
            SummaryType = summaryType;
            FormatString = formatString;
        }

        public string FormatValue(double value)
        {
            return string.IsNullOrEmpty(FormatString)
                ? (SummaryType == GroupSummaryType.Count ? ((int)value).ToString("N0") : value.ToString("N2"))
                : string.Format(FormatString, value);
        }
    }

    /// <summary>
    /// High-performance multi-dimensional OLAP pivot aggregation engine.
    /// Slices, dices, and cross-tabulates raw rows into a 2D matrix with sub-totals and grand totals.
    /// </summary>
    public class PivotDataEngine
    {
        public List<PivotDimension> RowDimensions { get; } = new List<PivotDimension>();
        public List<PivotDimension> ColumnDimensions { get; } = new List<PivotDimension>();
        public List<PivotMeasure> Measures { get; } = new List<PivotMeasure>();

        public List<string> RowKeys { get; } = new List<string>();
        public List<string> ColumnKeys { get; } = new List<string>();
        public double[,] Cells { get; private set; } = new double[0, 0];
        public double[] RowGrandTotals { get; private set; } = Array.Empty<double>();
        public double[] ColumnGrandTotals { get; private set; } = Array.Empty<double>();
        public double GrandTotal { get; private set; }

        public void Compute(int totalRows, Func<int, int, string> getCellText, Func<int, int, double> getNumericValue)
        {
            RowKeys.Clear();
            ColumnKeys.Clear();

            if (totalRows <= 0 || Measures.Count == 0)
            {
                Cells = new double[0, 0];
                RowGrandTotals = Array.Empty<double>();
                ColumnGrandTotals = Array.Empty<double>();
                GrandTotal = 0;
                return;
            }

            var rowKeySet = new HashSet<string>(StringComparer.Ordinal);
            var colKeySet = new HashSet<string>(StringComparer.Ordinal);

            // 1. Discover unique row & col tuples
            string[] rowKeysByRow = new string[totalRows];
            string[] colKeysByRow = new string[totalRows];

            for (int r = 0; r < totalRows; r++)
            {
                string rKey = BuildKey(r, RowDimensions, getCellText);
                string cKey = BuildKey(r, ColumnDimensions, getCellText);

                rowKeysByRow[r] = rKey;
                colKeysByRow[r] = cKey;

                if (rowKeySet.Add(rKey)) RowKeys.Add(rKey);
                if (colKeySet.Add(cKey)) ColumnKeys.Add(cKey);
            }

            RowKeys.Sort(StringComparer.OrdinalIgnoreCase);
            ColumnKeys.Sort(StringComparer.OrdinalIgnoreCase);

            var rowIndexMap = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < RowKeys.Count; i++) rowIndexMap[RowKeys[i]] = i;

            var colIndexMap = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < ColumnKeys.Count; i++) colIndexMap[ColumnKeys[i]] = i;

            int rCount = RowKeys.Count;
            int cCount = ColumnKeys.Count;
            Cells = new double[rCount, cCount];
            int[,] countMatrix = new int[rCount, cCount];

            // 2. Aggregate values into matrix
            var measure = Measures[0]; // Primary measure
            for (int r = 0; r < totalRows; r++)
            {
                int rIdx = rowIndexMap[rowKeysByRow[r]];
                int cIdx = colIndexMap[colKeysByRow[r]];
                double val = getNumericValue(r, measure.FieldIndex);

                switch (measure.SummaryType)
                {
                    case GroupSummaryType.Count:
                        Cells[rIdx, cIdx] += 1;
                        break;
                    case GroupSummaryType.Sum:
                        Cells[rIdx, cIdx] += val;
                        break;
                    case GroupSummaryType.Min:
                        if (countMatrix[rIdx, cIdx] == 0 || val < Cells[rIdx, cIdx])
                            Cells[rIdx, cIdx] = val;
                        break;
                    case GroupSummaryType.Max:
                        if (countMatrix[rIdx, cIdx] == 0 || val > Cells[rIdx, cIdx])
                            Cells[rIdx, cIdx] = val;
                        break;
                    case GroupSummaryType.Average:
                        Cells[rIdx, cIdx] += val;
                        break;
                }
                countMatrix[rIdx, cIdx]++;
            }

            if (measure.SummaryType == GroupSummaryType.Average)
            {
                for (int i = 0; i < rCount; i++)
                {
                    for (int j = 0; j < cCount; j++)
                    {
                        if (countMatrix[i, j] > 0)
                            Cells[i, j] /= countMatrix[i, j];
                    }
                }
            }

            // 3. Compute Grand Totals
            RowGrandTotals = new double[rCount];
            ColumnGrandTotals = new double[cCount];
            GrandTotal = 0;

            for (int i = 0; i < rCount; i++)
            {
                double rowSum = 0;
                for (int j = 0; j < cCount; j++)
                {
                    rowSum += Cells[i, j];
                }
                RowGrandTotals[i] = rowSum;
                GrandTotal += rowSum;
            }

            for (int j = 0; j < cCount; j++)
            {
                double colSum = 0;
                for (int i = 0; i < rCount; i++)
                {
                    colSum += Cells[i, j];
                }
                ColumnGrandTotals[j] = colSum;
            }
        }

        private static string BuildKey(int rowIndex, List<PivotDimension> dims, Func<int, int, string> getCellText)
        {
            if (dims.Count == 0) return "All";
            if (dims.Count == 1) return getCellText(rowIndex, dims[0].FieldIndex);

            var parts = new string[dims.Count];
            for (int i = 0; i < dims.Count; i++)
            {
                parts[i] = getCellText(rowIndex, dims[i].FieldIndex);
            }
            return string.Join(" | ", parts);
        }
    }
}
