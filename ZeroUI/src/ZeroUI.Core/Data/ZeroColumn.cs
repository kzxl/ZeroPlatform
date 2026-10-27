using System;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Data
{
    public sealed class ZeroColumn
    {
        public string HeaderText { get; set; } = string.Empty;
        public int Width { get; set; } = 100;
        public int MinWidth { get; set; } = 30;
        public int MaxWidth { get; set; } = 1200;
        public CellAlignment Alignment { get; set; } = CellAlignment.Left;
        public SortDirection SortOrder { get; set; } = SortDirection.None;
        public bool IsVisible { get; set; } = true;

        // Enterprise Metadata
        public string FieldName { get; set; } = string.Empty;
        public string? DisplayFormat { get; set; }
        public bool ReadOnly { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public SummaryType Summary { get; set; } = SummaryType.None;
        public string? SummaryFormat { get; set; }
        public GridColumnType ColumnType { get; set; } = GridColumnType.Text;
        public string? Mask { get; set; }
        public Func<string, (bool IsValid, string? ErrorMessage)>? CustomValidator { get; set; }

        // Advanced Enterprise Extensions
        public int GroupIndex { get; set; } = -1;
        public bool AllowGrouping { get; set; } = true;
        public string? BandTitle { get; set; }
        public bool AllowCellMerge { get; set; } = false;
        public SparklineType Sparkline { get; set; } = SparklineType.None;
        public bool AllowFiltering { get; set; } = true;

        // Multi-Row Record & Repository Properties
        public int BandRow { get; set; } = 0;
        public int BandRowSpan { get; set; } = 1;
        public object? ColumnEdit { get; set; }

        public ZeroColumn() { }

        public ZeroColumn(string headerText, int width = 100, CellAlignment alignment = CellAlignment.Left)
        {
            HeaderText = headerText;
            Width = width;
            Alignment = alignment;
        }

        public ZeroColumn(string fieldName, string headerText, int width = 100, CellAlignment alignment = CellAlignment.Left)
        {
            FieldName = fieldName;
            HeaderText = headerText;
            Width = width;
            Alignment = alignment;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(HeaderText)) return HeaderText;
            if (!string.IsNullOrEmpty(FieldName)) return FieldName;
            return base.ToString() ?? string.Empty;
        }
    }
}
