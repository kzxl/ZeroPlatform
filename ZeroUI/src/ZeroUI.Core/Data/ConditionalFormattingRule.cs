using System;

namespace ZeroUI.Core.Data
{
    public enum ConditionalRuleType
    {
        Highlight,
        ColorScale,
        DataBar,
        IconSet
    }

    public enum ConditionOperator
    {
        None,
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Between,
        Contains
    }

    /// <summary>
    /// High-performance conditional formatting rule evaluated per cell during rendering without GC allocations.
    /// </summary>
    public class ConditionalFormattingRule
    {
        public int ColumnIndex { get; set; } = -1;
        public ConditionalRuleType RuleType { get; set; } = ConditionalRuleType.Highlight;
        public ConditionOperator Operator { get; set; } = ConditionOperator.GreaterThan;

        public double Value1 { get; set; }
        public double Value2 { get; set; }
        public string? TextPattern { get; set; }

        public uint BackColor { get; set; }
        public uint TextColor { get; set; }

        public double MinScaleValue { get; set; }
        public double MaxScaleValue { get; set; }
        public uint MinColor { get; set; } = 0xFF2E7D32; // Greenish
        public uint MaxColor { get; set; } = 0xFFC62828; // Reddish

        public ConditionalFormattingRule()
        {
        }

        public ConditionalFormattingRule(int columnIndex, ConditionOperator op, double value1, uint backColor, uint textColor = 0)
        {
            ColumnIndex = columnIndex;
            RuleType = ConditionalRuleType.Highlight;
            Operator = op;
            Value1 = value1;
            BackColor = backColor;
            TextColor = textColor;
        }

        public bool EvaluateNumeric(double cellValue, out uint appliedBackColor, out uint appliedTextColor)
        {
            appliedBackColor = 0;
            appliedTextColor = 0;

            if (RuleType == ConditionalRuleType.Highlight)
            {
                bool match = false;
                switch (Operator)
                {
                    case ConditionOperator.Equal:
                        match = Math.Abs(cellValue - Value1) < 0.000001;
                        break;
                    case ConditionOperator.NotEqual:
                        match = Math.Abs(cellValue - Value1) >= 0.000001;
                        break;
                    case ConditionOperator.GreaterThan:
                        match = cellValue > Value1;
                        break;
                    case ConditionOperator.GreaterThanOrEqual:
                        match = cellValue >= Value1;
                        break;
                    case ConditionOperator.LessThan:
                        match = cellValue < Value1;
                        break;
                    case ConditionOperator.LessThanOrEqual:
                        match = cellValue <= Value1;
                        break;
                    case ConditionOperator.Between:
                        match = cellValue >= Value1 && cellValue <= Value2;
                        break;
                }

                if (match)
                {
                    appliedBackColor = BackColor;
                    appliedTextColor = TextColor;
                    return true;
                }
            }
            else if (RuleType == ConditionalRuleType.ColorScale)
            {
                if (MaxScaleValue > MinScaleValue)
                {
                    double ratio = (cellValue - MinScaleValue) / (MaxScaleValue - MinScaleValue);
                    if (ratio < 0.0) ratio = 0.0;
                    if (ratio > 1.0) ratio = 1.0;

                    appliedBackColor = InterpolateColor(MinColor, MaxColor, ratio);
                    appliedTextColor = 0xFFFFFFFF; // White text for color scale
                    return true;
                }
            }

            return false;
        }

        public bool EvaluateText(string? text, out uint appliedBackColor, out uint appliedTextColor)
        {
            if (text == null)
            {
                appliedBackColor = 0;
                appliedTextColor = 0;
                return false;
            }
            return EvaluateText(text.AsSpan(), out appliedBackColor, out appliedTextColor);
        }

        public bool EvaluateText(ReadOnlySpan<char> text, out uint appliedBackColor, out uint appliedTextColor)
        {
            appliedBackColor = 0;
            appliedTextColor = 0;

            if (RuleType == ConditionalRuleType.Highlight && Operator == ConditionOperator.Contains && !string.IsNullOrEmpty(TextPattern))
            {
#if NET8_0_OR_GREATER
                if (text.Contains(TextPattern.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    appliedBackColor = BackColor;
                    appliedTextColor = TextColor;
                    return true;
                }
#else
                if (text.ToString().IndexOf(TextPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    appliedBackColor = BackColor;
                    appliedTextColor = TextColor;
                    return true;
                }
#endif
            }

            return false;
        }

        private static uint InterpolateColor(uint colorA, uint colorB, double factor)
        {
            byte a1 = (byte)((colorA >> 24) & 0xFF);
            byte r1 = (byte)((colorA >> 16) & 0xFF);
            byte g1 = (byte)((colorA >> 8) & 0xFF);
            byte b1 = (byte)(colorA & 0xFF);

            byte a2 = (byte)((colorB >> 24) & 0xFF);
            byte r2 = (byte)((colorB >> 16) & 0xFF);
            byte g2 = (byte)((colorB >> 8) & 0xFF);
            byte b2 = (byte)(colorB & 0xFF);

            byte a = (byte)(a1 + (a2 - a1) * factor);
            byte r = (byte)(r1 + (r2 - r1) * factor);
            byte g = (byte)(g1 + (g2 - g1) * factor);
            byte b = (byte)(b1 + (b2 - b1) * factor);

            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }
    }
}
