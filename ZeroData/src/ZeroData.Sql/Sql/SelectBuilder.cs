using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Translates a LINQ Select expression into a SQL column list.
    /// Supports:
    /// - Anonymous types: x => new { x.Id, x.Name }
    /// - DTO/MemberInit: x => new DTO { Id = x.Id, Name = x.Name }
    /// - Scalar: x => x.Name
    /// </summary>
    public class SelectBuilder
    {
        private readonly EntityMapping _mapping;
        private readonly ISqlDialect _dialect;

        public SelectBuilder(EntityMapping mapping, ISqlDialect dialect = null)
        {
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
        }

        /// <summary>
        /// Extracts column names from a Select expression and returns the SQL column list.
        /// </summary>
        /// <param name="selector">The select expression (e.g., x => new { x.Id, x.Name })</param>
        /// <returns>Comma-separated column list with aliases, e.g., "[Id] AS [Id], [Name] AS [Name]"</returns>
        public string Build(LambdaExpression selector)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var columns = ExtractColumns(selector.Body);
            if (columns.Count == 0)
                throw new InvalidOperationException("Select expression must project at least one column.");

            return string.Join(", ", columns.Select(c =>
                c.Alias != null && c.Alias != c.ColumnName
                    ? $"{_dialect.QuoteIdentifier(c.ColumnName)} AS {_dialect.QuoteIdentifier(c.Alias)}"
                    : $"{_dialect.QuoteIdentifier(c.ColumnName)}"));
        }

        private List<SelectColumn> ExtractColumns(Expression body)
        {
            switch (body)
            {
                // new { x.Id, x.Name } — anonymous type (NewExpression)
                case NewExpression newExpr:
                    return ExtractFromNew(newExpr);

                // new DTO { Id = x.Id, Name = x.Name } — MemberInitExpression
                case MemberInitExpression initExpr:
                    return ExtractFromMemberInit(initExpr);

                // x.Name — simple member access (scalar projection)
                case MemberExpression memberExpr:
                    return ExtractFromMember(memberExpr);

                // (object)x.Id — conversion/cast wrapping
                case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Convert:
                    return ExtractColumns(unaryExpr.Operand);

                default:
                    throw new NotSupportedException(
                        $"Select expression type '{body.NodeType}' is not supported. " +
                        $"Use anonymous types (new {{ }}), DTOs (new DTO {{ }}), or simple properties (x => x.Name).");
            }
        }

        /// <summary>
        /// Extracts columns from a NewExpression (anonymous types).
        /// new { x.Id, x.Name } → constructor arguments mapped to members.
        /// </summary>
        private List<SelectColumn> ExtractFromNew(NewExpression expr)
        {
            var columns = new List<SelectColumn>();

            for (int i = 0; i < expr.Arguments.Count; i++)
            {
                var arg = expr.Arguments[i];
                var memberName = expr.Members?[i]?.Name;

                var propName = GetPropertyName(arg);
                if (propName == null)
                    throw new NotSupportedException(
                        $"Select projection argument '{arg}' is not a simple property access.");

                var col = FindColumn(propName);
                columns.Add(new SelectColumn(col.ColumnName, memberName ?? propName));
            }

            return columns;
        }

        /// <summary>
        /// Extracts columns from a MemberInitExpression (DTO projection).
        /// new DTO { Id = x.Id, Name = x.Name }
        /// </summary>
        private List<SelectColumn> ExtractFromMemberInit(MemberInitExpression expr)
        {
            var columns = new List<SelectColumn>();

            foreach (var binding in expr.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    var propName = GetPropertyName(assignment.Expression);
                    if (propName == null)
                        throw new NotSupportedException(
                            $"Select binding '{binding.Member.Name}' is not a simple property access.");

                    var col = FindColumn(propName);
                    columns.Add(new SelectColumn(col.ColumnName, binding.Member.Name));
                }
            }

            return columns;
        }

        /// <summary>
        /// Extracts a single column from a scalar MemberExpression.
        /// x => x.Name
        /// </summary>
        private List<SelectColumn> ExtractFromMember(MemberExpression expr)
        {
            var propName = GetPropertyName(expr);
            if (propName == null)
                throw new NotSupportedException($"Select member '{expr}' is not a property of the entity.");

            var col = FindColumn(propName);
            return new List<SelectColumn> { new SelectColumn(col.ColumnName, propName) };
        }

        /// <summary>
        /// Gets the property name from an expression, unwrapping Convert if needed.
        /// </summary>
        private string GetPropertyName(Expression expr)
        {
            // Unwrap Convert (e.g., (object)x.Id)
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Expression is ParameterExpression)
                return member.Member.Name;

            return null;
        }

        /// <summary>
        /// Finds the column mapping for a given property name.
        /// </summary>
        private ColumnMapping FindColumn(string propertyName)
        {
            var col = _mapping.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
            if (col == null)
                throw new InvalidOperationException(
                    $"Property '{propertyName}' is not a mapped column of '{_mapping.EntityType.Name}'.");
            return col;
        }

        private struct SelectColumn
        {
            public string ColumnName;
            public string Alias;

            public SelectColumn(string columnName, string alias)
            {
                ColumnName = columnName;
                Alias = alias;
            }
        }
    }
}
