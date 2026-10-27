using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Caches the SQL template generated from expression tree structures.
    /// Two expressions with the same structure but different constant values
    /// will share the same cached SQL template (with parameter placeholders).
    /// 
    /// This avoids re-traversing the expression tree for repeated queries like:
    ///   db.Products.Where(x => x.Active == true)  -- called many times
    ///   db.Products.Where(x => x.Price > someValue)  -- different values, same structure
    /// </summary>
    public static class CompiledQueryCache
    {
        private static readonly ConcurrentDictionary<string, string> _cache
            = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Gets a cached SQL template for the given expression structure key.
        /// Returns null if not cached.
        /// </summary>
        public static string GetCachedSql(string structureKey)
        {
            return _cache.TryGetValue(structureKey, out var sql) ? sql : null;
        }

        /// <summary>
        /// Stores a SQL template for the given expression structure key.
        /// </summary>
        public static void CacheSql(string structureKey, string sql)
        {
            _cache.TryAdd(structureKey, sql);
        }

        /// <summary>
        /// Generates a structural key from an expression tree.
        /// The key captures the tree structure (node types, member names, operators)
        /// but normalizes constant values to placeholders.
        /// Two expressions with the same structure but different constants
        /// will produce the same key.
        /// </summary>
        public static string GetStructureKey(Expression expression)
        {
            if (expression == null) return string.Empty;
            return BuildKey(expression);
        }

        private static string BuildKey(Expression expr)
        {
            switch (expr)
            {
                case LambdaExpression lambda:
                    return $"λ({BuildKey(lambda.Body)})";

                case BinaryExpression binary:
                    return $"({BuildKey(binary.Left)}{binary.NodeType}{BuildKey(binary.Right)})";

                case UnaryExpression unary:
                    return $"{unary.NodeType}({BuildKey(unary.Operand)})";

                case MemberExpression member when member.Expression is ParameterExpression:
                    // Entity property access: x.Name → "M:Name"
                    return $"M:{member.Member.Name}";

                case MemberExpression _:
                    // Closure/constant member access → normalized placeholder
                    return "C:_";

                case ConstantExpression _:
                    return "C:_";

                case MethodCallExpression method:
                    var args = string.Join(",", System.Linq.Enumerable.Select(
                        method.Arguments, a => BuildKey(a)));
                    var obj = method.Object != null ? BuildKey(method.Object) : "static";
                    return $"Call:{method.Method.Name}({obj},{args})";

                case ParameterExpression param:
                    return $"P:{param.Name}";

                default:
                    // Fallback: use node type to avoid cache misses
                    return $"?:{expr.NodeType}";
            }
        }

        /// <summary>
        /// Returns the current cache size. Useful for testing.
        /// </summary>
        public static int CacheSize => _cache.Count;

        /// <summary>
        /// Clears the cache. Useful for testing.
        /// </summary>
        public static void Clear() => _cache.Clear();
    }
}
