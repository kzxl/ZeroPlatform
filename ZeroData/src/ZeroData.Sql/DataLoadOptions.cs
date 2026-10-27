using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroData.Sql
{
    /// <summary>
    /// Specifies which navigation properties to eagerly load.
    /// Compatible with System.Data.Linq.DataLoadOptions pattern.
    /// </summary>
    public class DataLoadOptions
    {
        private readonly Dictionary<Type, List<PropertyInfo>> _loadRules
            = new Dictionary<Type, List<PropertyInfo>>();

        /// <summary>
        /// Registers a navigation property to be automatically loaded when querying entities of type T.
        /// Example: options.LoadWith&lt;Order&gt;(o => o.Customer);
        /// </summary>
        public void LoadWith<T>(Expression<Func<T, object>> expression)
        {
            var prop = ExtractProperty(expression);
            if (prop == null)
                throw new ArgumentException("Expression must be a member access expression (e.g. o => o.Customer).");

            var entityType = typeof(T);
            if (!_loadRules.ContainsKey(entityType))
                _loadRules[entityType] = new List<PropertyInfo>();

            // Avoid duplicates
            if (!_loadRules[entityType].Contains(prop))
                _loadRules[entityType].Add(prop);
        }

        /// <summary>
        /// Gets the list of navigation properties to load for the given entity type.
        /// Returns empty list if no rules registered.
        /// </summary>
        internal List<PropertyInfo> GetLoadRules(Type entityType)
        {
            return _loadRules.TryGetValue(entityType, out var rules) ? rules : new List<PropertyInfo>();
        }

        private static PropertyInfo ExtractProperty<T>(Expression<Func<T, object>> expression)
        {
            var body = expression.Body;

            // Handle boxing (value type wrapped in Convert)
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member && member.Member is PropertyInfo prop)
                return prop;

            return null;
        }
    }
}
