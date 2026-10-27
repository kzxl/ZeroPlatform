using System;
using System.Globalization;

namespace ZeroUI.Core.Spreadsheet
{
    /// <summary>
    /// Text alignment within a spreadsheet cell.
    /// </summary>
    public enum SpreadsheetAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Built-in display number formats.
    /// </summary>
    public enum SpreadsheetFormatType
    {
        General,
        Number,
        Currency,
        Percentage,
        Integer,
        Date
    }

    /// <summary>
    /// Represents an individual cell within a worksheet, storing raw user input, computed value, formula, and visual styling.
    /// </summary>
    public sealed class SpreadsheetCell
    {
        public CellAddress Address { get; }

        public string? RawValue { get; set; }

        public object? EvaluatedValue { get; set; }

        public string? Error { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);

        public bool HasFormula => !string.IsNullOrEmpty(RawValue) && RawValue!.StartsWith("=", StringComparison.Ordinal);

        public string Formula => HasFormula ? RawValue!.Substring(1).Trim() : string.Empty;

        // Visual Styling
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public uint TextColor { get; set; } = 0xFF0F172A; // Slate 900
        public uint BackgroundColor { get; set; } = 0x00000000; // Transparent
        public SpreadsheetAlignment Alignment { get; set; } = SpreadsheetAlignment.Left;
        public SpreadsheetFormatType FormatType { get; set; } = SpreadsheetFormatType.General;
        public string? CustomFormat { get; set; }

        public SpreadsheetCell(CellAddress address, string? rawValue = null)
        {
            Address = address;
            RawValue = rawValue;
            EvaluatedValue = rawValue;
        }

        public SpreadsheetCell(int row, int col, string? rawValue = null)
            : this(new CellAddress(row, col), rawValue)
        {
        }

        /// <summary>
        /// Gets formatted string ready for rendering based on FormatType and EvaluatedValue.
        /// </summary>
        public string FormattedText
        {
            get
            {
                if (HasError)
                    return Error!;

                if (EvaluatedValue == null)
                    return string.Empty;

                if (EvaluatedValue is double d)
                {
                    return FormatNumeric(d);
                }
                if (EvaluatedValue is decimal dec)
                {
                    return FormatNumeric((double)dec);
                }
                if (EvaluatedValue is int i)
                {
                    return FormatNumeric(i);
                }
                if (EvaluatedValue is long l)
                {
                    return FormatNumeric(l);
                }
                if (EvaluatedValue is DateTime dt)
                {
                    return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                return EvaluatedValue.ToString() ?? string.Empty;
            }
        }

        private string FormatNumeric(double val)
        {
            if (!string.IsNullOrEmpty(CustomFormat))
            {
                try
                {
                    return val.ToString(CustomFormat, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // Fall back to general
                }
            }

            switch (FormatType)
            {
                case SpreadsheetFormatType.Currency:
                    return val.ToString("$#,##0.00;($#,##0.00);$0.00", CultureInfo.InvariantCulture);
                case SpreadsheetFormatType.Percentage:
                    return val.ToString("0.0%", CultureInfo.InvariantCulture);
                case SpreadsheetFormatType.Number:
                    return val.ToString("#,##0.00", CultureInfo.InvariantCulture);
                case SpreadsheetFormatType.Integer:
                    return val.ToString("#,##0", CultureInfo.InvariantCulture);
                default:
                    return Math.Abs(val % 1) < 1e-9
                        ? val.ToString("F0", CultureInfo.InvariantCulture)
                        : val.ToString("G6", CultureInfo.InvariantCulture);
            }
        }

        public double AsDouble(double defaultValue = 0.0)
        {
            if (EvaluatedValue is double d) return d;
            if (EvaluatedValue is decimal dec) return (double)dec;
            if (EvaluatedValue is int i) return i;
            if (EvaluatedValue is long l) return l;
            if (EvaluatedValue is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return defaultValue;
        }
    }
}
