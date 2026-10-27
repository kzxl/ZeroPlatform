using System;
using System.Collections.Generic;
using System.Text;
using ZeroUI.Core.Localization;

namespace ZeroUI.Core.Data
{
    public enum FilterGroupOperator
    {
        And,
        Or,
        NotAnd,
        NotOr
    }

    public enum FilterComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        StartsWith,
        EndsWith,
        Between,
        IsNull,
        IsNotNull
    }

    public interface IFilterCriteriaNode
    {
        string ToDisplayString();
        string ToSqlWhere();
        string ToRowFilter();
    }

    public class ConditionFilterNode : IFilterCriteriaNode
    {
        public string FieldName { get; set; } = string.Empty;
        public FilterComparisonOperator Operator { get; set; } = FilterComparisonOperator.Equals;
        public string Value { get; set; } = string.Empty;
        public string Value2 { get; set; } = string.Empty;

        public ConditionFilterNode() { }

        public ConditionFilterNode(string fieldName, FilterComparisonOperator op, string value, string value2 = "")
        {
            FieldName = fieldName;
            Operator = op;
            Value = value;
            Value2 = value2;
        }

        public string ToDisplayString()
        {
            if (Operator == FilterComparisonOperator.Between)
            {
                return $"[{FieldName}] Between '{Value}' And '{Value2}'";
            }
            return $"[{FieldName}] {Operator} '{Value}'";
        }

        public string ToSqlWhere()
        {
            return Operator switch
            {
                FilterComparisonOperator.Equals => $"[{FieldName}] = '{Value}'",
                FilterComparisonOperator.NotEquals => $"[{FieldName}] <> '{Value}'",
                FilterComparisonOperator.GreaterThan => $"[{FieldName}] > '{Value}'",
                FilterComparisonOperator.GreaterThanOrEqual => $"[{FieldName}] >= '{Value}'",
                FilterComparisonOperator.LessThan => $"[{FieldName}] < '{Value}'",
                FilterComparisonOperator.LessThanOrEqual => $"[{FieldName}] <= '{Value}'",
                FilterComparisonOperator.Contains => $"[{FieldName}] LIKE '%{Value}%'",
                FilterComparisonOperator.StartsWith => $"[{FieldName}] LIKE '{Value}%'",
                FilterComparisonOperator.EndsWith => $"[{FieldName}] LIKE '%{Value}'",
                FilterComparisonOperator.Between => $"[{FieldName}] BETWEEN '{Value}' AND '{Value2}'",
                FilterComparisonOperator.IsNull => $"[{FieldName}] IS NULL",
                FilterComparisonOperator.IsNotNull => $"[{FieldName}] IS NOT NULL",
                _ => $"[{FieldName}] = '{Value}'"
            };
        }

        public string ToRowFilter()
        {
            return Operator switch
            {
                FilterComparisonOperator.Equals => $"[{FieldName}] = '{Value}'",
                FilterComparisonOperator.NotEquals => $"[{FieldName}] <> '{Value}'",
                FilterComparisonOperator.GreaterThan => $"[{FieldName}] > '{Value}'",
                FilterComparisonOperator.GreaterThanOrEqual => $"[{FieldName}] >= '{Value}'",
                FilterComparisonOperator.LessThan => $"[{FieldName}] < '{Value}'",
                FilterComparisonOperator.LessThanOrEqual => $"[{FieldName}] <= '{Value}'",
                FilterComparisonOperator.Contains => $"[{FieldName}] LIKE '%{Value}%'",
                FilterComparisonOperator.StartsWith => $"[{FieldName}] LIKE '{Value}%'",
                FilterComparisonOperator.EndsWith => $"[{FieldName}] LIKE '%{Value}'",
                FilterComparisonOperator.Between => $"([{FieldName}] >= '{Value}' AND [{FieldName}] <= '{Value2}')",
                FilterComparisonOperator.IsNull => $"[{FieldName}] IS NULL",
                FilterComparisonOperator.IsNotNull => $"[{FieldName}] IS NOT NULL",
                _ => $"[{FieldName}] = '{Value}'"
            };
        }
    }

    public class GroupFilterNode : IFilterCriteriaNode
    {
        public FilterGroupOperator Operator { get; set; } = FilterGroupOperator.And;
        public List<IFilterCriteriaNode> Children { get; } = new List<IFilterCriteriaNode>();

        public GroupFilterNode() { }

        public GroupFilterNode(FilterGroupOperator op)
        {
            Operator = op;
        }

        public void AddCondition(string fieldName, FilterComparisonOperator op, string value, string value2 = "")
        {
            Children.Add(new ConditionFilterNode(fieldName, op, value, value2));
        }

        public GroupFilterNode AddGroup(FilterGroupOperator op)
        {
            var grp = new GroupFilterNode(op);
            Children.Add(grp);
            return grp;
        }

        public string ToDisplayString()
        {
            var sb = new StringBuilder();
            sb.Append("(");
            string joiner = (Operator == FilterGroupOperator.Or || Operator == FilterGroupOperator.NotOr) ? " OR " : " AND ";
            if (Operator == FilterGroupOperator.NotAnd || Operator == FilterGroupOperator.NotOr)
            {
                sb.Append("NOT ");
            }
            for (int i = 0; i < Children.Count; i++)
            {
                if (i > 0) sb.Append(joiner);
                sb.Append(Children[i].ToDisplayString());
            }
            sb.Append(")");
            return sb.ToString();
        }

        public string ToSqlWhere()
        {
            if (Children.Count == 0) return "1=1";
            var sb = new StringBuilder();
            if (Operator == FilterGroupOperator.NotAnd || Operator == FilterGroupOperator.NotOr)
            {
                sb.Append("NOT ");
            }
            sb.Append("(");
            string joiner = (Operator == FilterGroupOperator.Or || Operator == FilterGroupOperator.NotOr) ? " OR " : " AND ";
            for (int i = 0; i < Children.Count; i++)
            {
                if (i > 0) sb.Append(joiner);
                sb.Append(Children[i].ToSqlWhere());
            }
            sb.Append(")");
            return sb.ToString();
        }

        public string ToRowFilter()
        {
            if (Children.Count == 0) return "";
            var sb = new StringBuilder();
            if (Operator == FilterGroupOperator.NotAnd || Operator == FilterGroupOperator.NotOr)
            {
                sb.Append("NOT ");
            }
            sb.Append("(");
            string joiner = (Operator == FilterGroupOperator.Or || Operator == FilterGroupOperator.NotOr) ? " OR " : " AND ";
            for (int i = 0; i < Children.Count; i++)
            {
                if (i > 0) sb.Append(joiner);
                sb.Append(Children[i].ToRowFilter());
            }
            sb.Append(")");
            return sb.ToString();
        }
    }

    public static class FilterCriteriaExtensions
    {
        public static string GetLocalizedName(this FilterGroupOperator op)
        {
            return op switch
            {
                FilterGroupOperator.And => Localizer.GetString(StringId.FilterOpAnd),
                FilterGroupOperator.Or => Localizer.GetString(StringId.FilterOpOr),
                FilterGroupOperator.NotAnd => Localizer.GetString(StringId.FilterOpNotAnd),
                FilterGroupOperator.NotOr => Localizer.GetString(StringId.FilterOpNotOr),
                _ => op.ToString().ToUpperInvariant()
            };
        }

        public static string GetLocalizedName(this FilterComparisonOperator op)
        {
            return op switch
            {
                FilterComparisonOperator.Equals => Localizer.GetString(StringId.FilterEquals),
                FilterComparisonOperator.NotEquals => Localizer.GetString(StringId.FilterNotEquals),
                FilterComparisonOperator.Contains => Localizer.GetString(StringId.FilterContains),
                FilterComparisonOperator.StartsWith => Localizer.GetString(StringId.FilterStartsWith),
                FilterComparisonOperator.EndsWith => Localizer.GetString(StringId.FilterEndsWith),
                FilterComparisonOperator.GreaterThan => Localizer.GetString(StringId.FilterGreaterThan),
                FilterComparisonOperator.GreaterThanOrEqual => ">=",
                FilterComparisonOperator.LessThan => Localizer.GetString(StringId.FilterLessThan),
                FilterComparisonOperator.LessThanOrEqual => "<=",
                FilterComparisonOperator.Between => "Between",
                FilterComparisonOperator.IsNull => Localizer.GetString(StringId.FilterIsNull),
                FilterComparisonOperator.IsNotNull => Localizer.GetString(StringId.FilterIsNotNull),
                _ => op.ToString()
            };
        }
    }
}
