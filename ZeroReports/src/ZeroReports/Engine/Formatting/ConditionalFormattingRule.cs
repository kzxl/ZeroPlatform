using System;
using System.Collections.Generic;
using ZeroPrimitives;
using ZeroReports.Data;

namespace ZeroReports.Engine.Formatting
{
    /// <summary>
    /// Conditional formatting rule equivalent to DevExpress FormattingRules / XRRule.
    /// Dynamically applies foreground colors, background highlights, and bold styles based on record evaluations.
    /// </summary>
    public class ConditionalFormattingRule
    {
        public string Name { get; set; } = "Rule";

        /// <summary>
        /// Direct programmatic condition predicate evaluating (dataSource, recordIndex).
        /// </summary>
        public Func<IReportDataSource, int, bool>? Condition { get; set; }

        /// <summary>
        /// Simple expression rule (e.g. FieldName, Operator, TargetValue).
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        public string Operator { get; set; } = "=="; // "==", "!=", ">", "<", ">=", "<=", "Contains"
        public string Value { get; set; } = string.Empty;

        // Visual Appearance overrides when condition is satisfied
        public (float R, float G, float B)? TextColor { get; set; }
        public (float R, float G, float B)? BackgroundColor { get; set; }
        public bool? IsBold { get; set; }
        public bool? Visible { get; set; }

        public ConditionalFormattingRule() { }

        public ConditionalFormattingRule(string fieldName, string op, string value)
        {
            FieldName = fieldName;
            Operator = op;
            Value = value;
        }

        public ConditionalFormattingRule(Func<IReportDataSource, int, bool> condition)
        {
            Condition = condition;
        }

        /// <summary>
        /// Evaluates whether this rule applies to the specified record using ZeroPrimitives.FastConvert.
        /// </summary>
        public bool Evaluate(IReportDataSource? dataSource, int recordIndex)
        {
            if (dataSource == null || recordIndex < 0 || recordIndex >= dataSource.RecordCount)
            {
                return false;
            }

            if (Condition != null)
            {
                return Condition(dataSource, recordIndex);
            }

            if (string.IsNullOrEmpty(FieldName))
            {
                return false;
            }

            object? rawVal = dataSource.GetValue(recordIndex, FieldName);
            string strVal = FastConvert.AsString(rawVal);

            switch (Operator)
            {
                case "==":
                case "=":
                    return string.Equals(strVal, Value, StringComparison.OrdinalIgnoreCase);

                case "!=":
                case "<>":
                    return !string.Equals(strVal, Value, StringComparison.OrdinalIgnoreCase);

                case ">":
                    return FastConvert.AsDouble(rawVal) > FastConvert.AsDouble(Value);

                case "<":
                    return FastConvert.AsDouble(rawVal) < FastConvert.AsDouble(Value);

                case ">=":
                    return FastConvert.AsDouble(rawVal) >= FastConvert.AsDouble(Value);

                case "<=":
                    return FastConvert.AsDouble(rawVal) <= FastConvert.AsDouble(Value);

                case "Contains":
                    return strVal.IndexOf(Value, StringComparison.OrdinalIgnoreCase) >= 0;

                default:
                    return false;
            }
        }
    }
}
