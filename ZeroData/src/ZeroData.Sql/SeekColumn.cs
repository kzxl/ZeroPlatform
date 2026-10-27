using System;
using System.Linq;
using System.Linq.Expressions;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql
{
    /// <summary>
    /// Defines an indexed column for constant-time keyset (seek) pagination.
    /// Supports composite keys with distinct sort directions (ASC/DESC).
    /// </summary>
    public sealed class SeekColumn
    {
        /// <summary>
        /// Gets the column name in the database.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the cursor value of the last seen item from the previous page.
        /// </summary>
        public object LastSeenValue { get; }

        /// <summary>
        /// Gets a value indicating whether sorting is ascending.
        /// </summary>
        public bool Ascending { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SeekColumn"/> class.
        /// </summary>
        public SeekColumn(string columnName, object lastSeenValue, bool ascending = true)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            LastSeenValue = lastSeenValue;
            Ascending = ascending;
        }

        /// <summary>
        /// Creates a <see cref="SeekColumn"/> from a lambda property selector.
        /// </summary>
        public static SeekColumn Create<T, TKey>(Expression<Func<T, TKey>> keySelector, TKey lastSeenValue, bool ascending = true) where T : class
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var memberExpr = keySelector.Body as MemberExpression;
            if (memberExpr == null && keySelector.Body is UnaryExpression u && u.Operand is MemberExpression inner)
            {
                memberExpr = inner;
            }

            if (memberExpr == null)
                throw new ArgumentException("Expression must be a member access expression (e.g. x => x.CreatedAt)", nameof(keySelector));

            var mapping = MappingCache.GetMapping<T>();
            var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == memberExpr.Member.Name);
            var colName = col != null ? col.ColumnName : memberExpr.Member.Name;

            return new SeekColumn(colName, lastSeenValue, ascending);
        }
    }
}
