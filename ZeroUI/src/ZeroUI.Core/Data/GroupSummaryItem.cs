using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Supported aggregation summary calculation types.
    /// </summary>
    public enum GroupSummaryType
    {
        Sum,
        Count,
        Average,
        Min,
        Max,
        Custom
    }

    /// <summary>
    /// Defines a summary aggregation item displayed on group header rows or footer summaries.
    /// </summary>
    public class GroupSummaryItem
    {
        public int ColumnIndex { get; set; }
        public string? FieldName { get; set; }
        public GroupSummaryType SummaryType { get; set; } = GroupSummaryType.Sum;
        public string? FormatString { get; set; }
        public string? Prefix { get; set; }
        public Func<IReadOnlyList<int>, double>? CustomAggregator { get; set; }

        public GroupSummaryItem()
        {
        }

        public GroupSummaryItem(int columnIndex, GroupSummaryType summaryType, string? formatString = null, string? prefix = null)
        {
            ColumnIndex = columnIndex;
            SummaryType = summaryType;
            FormatString = formatString;
            Prefix = prefix;
        }

        public string FormatValue(double value)
        {
            string formatted = string.IsNullOrEmpty(FormatString)
                ? (SummaryType == GroupSummaryType.Count ? ((int)value).ToString("N0") : value.ToString("N2"))
                : string.Format(FormatString, value);

            return string.IsNullOrEmpty(Prefix) ? formatted : $"{Prefix}: {formatted}";
        }
    }
}
