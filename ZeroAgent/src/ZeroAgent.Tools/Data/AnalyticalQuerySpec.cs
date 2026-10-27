using System;
using System.Collections.Generic;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Structured analytical query specification produced by NL-to-SQL intent or agent tool calls.
    /// Supports multi-table joins, complex boolean filtering, aggregations, grouping, and pagination.
    /// </summary>
    public class AnalyticalQuerySpec
    {
        public string Table { get; set; } = string.Empty;
        public List<string> SelectColumns { get; set; } = new List<string>();
        public List<JoinConditionSpec> Joins { get; set; } = new List<JoinConditionSpec>();
        public List<WhereConditionSpec> Filters { get; set; } = new List<WhereConditionSpec>();
        public List<string> GroupBy { get; set; } = new List<string>();
        public List<WhereConditionSpec> Having { get; set; } = new List<WhereConditionSpec>();
        public List<OrderBySpec> OrderBy { get; set; } = new List<OrderBySpec>();
        public int? Limit { get; set; }
        public int? Offset { get; set; }
    }

    /// <summary>
    /// Specification for joining related tables based on foreign keys or explicit predicates.
    /// </summary>
    public class JoinConditionSpec
    {
        public string Table { get; set; } = string.Empty;
        public string LeftColumn { get; set; } = string.Empty;
        public string RightColumn { get; set; } = string.Empty;
        public string JoinType { get; set; } = "INNER"; // INNER, LEFT, RIGHT
    }

    /// <summary>
    /// Specification for a single WHERE or HAVING condition.
    /// </summary>
    public class WhereConditionSpec
    {
        public string Column { get; set; } = string.Empty;
        public string Operator { get; set; } = "="; // =, !=, <, <=, >, >=, LIKE, IN, NOT IN, IS NULL, IS NOT NULL, BETWEEN
        public object? Value { get; set; }
        public object? SecondValue { get; set; }
        public string LogicalOp { get; set; } = "AND"; // AND, OR
    }

    /// <summary>
    /// Specification for sorting query results.
    /// </summary>
    public class OrderBySpec
    {
        public string Column { get; set; } = string.Empty;
        public bool Descending { get; set; }

        public OrderBySpec() { }

        public OrderBySpec(string column, bool descending = false)
        {
            Column = column;
            Descending = descending;
        }
    }

    /// <summary>
    /// Output result of the SQL generation process.
    /// </summary>
    public sealed class SqlBuildResult
    {
        public string Sql { get; set; } = string.Empty;
        public Dictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
