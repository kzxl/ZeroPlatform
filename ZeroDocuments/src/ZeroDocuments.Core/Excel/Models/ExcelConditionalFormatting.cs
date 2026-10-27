using System;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Relational operators for Excel conditional formatting cell rules.
    /// </summary>
    public enum CellRuleOperator
    {
        GreaterThan,
        LessThan,
        Equal,
        NotEqual,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Between,
        NotBetween
    }

    /// <summary>
    /// Base class for an OpenXML conditional formatting rule applied to a cell range.
    /// </summary>
    public abstract class ExcelConditionalFormatRule
    {
        /// <summary>
        /// Gets or sets the target cell range sequence reference (sqref), e.g., "A1:A10" or "B2:F50".
        /// </summary>
        public string Range { get; set; } = string.Empty;
    }

    /// <summary>
    /// Highlights cells matching a specific comparison criteria.
    /// </summary>
    public sealed class ExcelHighlightRule : ExcelConditionalFormatRule
    {
        /// <summary>
        /// Gets or sets the comparison operator.
        /// </summary>
        public CellRuleOperator Operator { get; set; } = CellRuleOperator.GreaterThan;

        /// <summary>
        /// Gets or sets the primary comparison value or formula.
        /// </summary>
        public string Formula1 { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secondary comparison value or formula (used for Between and NotBetween).
        /// </summary>
        public string? Formula2 { get; set; }

        /// <summary>
        /// Gets or sets the background fill color in Hex format (e.g. "FFC7CE" or "FFFFC7CE").
        /// </summary>
        public string? FillColorHex { get; set; }

        /// <summary>
        /// Gets or sets the font color in Hex format (e.g. "9C0006" or "FF9C0006").
        /// </summary>
        public string? FontColorHex { get; set; }

        /// <summary>
        /// Gets or sets whether matching cells should be rendered in bold font.
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelHighlightRule"/>.
        /// </summary>
        public ExcelHighlightRule() { }

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelHighlightRule"/> with specified criteria and styling.
        /// </summary>
        public ExcelHighlightRule(string range, CellRuleOperator op, string formula1, string? formula2 = null, string? fillColorHex = null, string? fontColorHex = null, bool bold = false)
        {
            Range = range ?? throw new ArgumentNullException(nameof(range));
            Operator = op;
            Formula1 = formula1 ?? string.Empty;
            Formula2 = formula2;
            FillColorHex = fillColorHex;
            FontColorHex = fontColorHex;
            Bold = bold;
        }
    }

    /// <summary>
    /// Applies a 2-color or 3-color gradient heatmap across a numeric range.
    /// </summary>
    public sealed class ExcelColorScaleRule : ExcelConditionalFormatRule
    {
        /// <summary>
        /// Gets or sets the minimum endpoint color in Hex format (default: soft red "FFF8696B").
        /// </summary>
        public string MinColorHex { get; set; } = "FFF8696B";

        /// <summary>
        /// Gets or sets the optional midpoint color in Hex format (e.g. soft yellow "FFFFEB84").
        /// When provided, a 3-color scale is created.
        /// </summary>
        public string? MidColorHex { get; set; }

        /// <summary>
        /// Gets or sets the maximum endpoint color in Hex format (default: soft green "FF63BE7B").
        /// </summary>
        public string MaxColorHex { get; set; } = "FF63BE7B";

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelColorScaleRule"/>.
        /// </summary>
        public ExcelColorScaleRule() { }

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelColorScaleRule"/> with specified range and colors.
        /// </summary>
        public ExcelColorScaleRule(string range, string minColorHex = "FFF8696B", string maxColorHex = "FF63BE7B", string? midColorHex = null)
        {
            Range = range ?? throw new ArgumentNullException(nameof(range));
            MinColorHex = minColorHex;
            MaxColorHex = maxColorHex;
            MidColorHex = midColorHex;
        }
    }

    /// <summary>
    /// Renders in-cell horizontal data bars proportional to cell values.
    /// </summary>
    public sealed class ExcelDataBarRule : ExcelConditionalFormatRule
    {
        /// <summary>
        /// Gets or sets the bar color in Hex format (default: soft blue "FF638EC6").
        /// </summary>
        public string ColorHex { get; set; } = "FF638EC6";

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelDataBarRule"/>.
        /// </summary>
        public ExcelDataBarRule() { }

        /// <summary>
        /// Initializes a new instance of <see cref="ExcelDataBarRule"/> with specified range and color.
        /// </summary>
        public ExcelDataBarRule(string range, string colorHex = "FF638EC6")
        {
            Range = range ?? throw new ArgumentNullException(nameof(range));
            ColorHex = colorHex;
        }
    }
}
