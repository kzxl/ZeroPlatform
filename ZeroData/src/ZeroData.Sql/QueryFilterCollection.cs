using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ZeroData.Sql
{
    /// <summary>
    /// Registry of global query filters applied automatically to all queries.
    /// Supports per-type predicate filters (e.g., soft delete, multi-tenant).
    /// </summary>
    public class QueryFilterCollection
    {
        private readonly ConcurrentDictionary<Type, List<LambdaExpression>> _filters
            = new ConcurrentDictionary<Type, List<LambdaExpression>>();

        /// <summary>
        /// Registers a global filter for entity type T.
        /// This predicate will be AND-combined with every query on Table&lt;T&gt;.
        /// Multiple filters for the same type are AND-combined.
        /// </summary>
        public void Add<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var list = _filters.GetOrAdd(typeof(T), _ => new List<LambdaExpression>());
            lock (list) { list.Add(predicate); }
        }

        /// <summary>
        /// Gets all registered filters for the specified entity type.
        /// Returns empty list if none registered.
        /// </summary>
        public IReadOnlyList<LambdaExpression> GetFilters(Type entityType)
        {
            if (_filters.TryGetValue(entityType, out var list))
                return list;
            return new List<LambdaExpression>();
        }

        /// <summary>
        /// Removes all filters for entity type T.
        /// </summary>
        public void Remove<T>() where T : class
        {
            _filters.TryRemove(typeof(T), out _);
        }

        /// <summary>
        /// Clears all registered filters.
        /// </summary>
        public void Clear() => _filters.Clear();

        /// <summary>
        /// Returns true if any filters are registered.
        /// </summary>
        public bool HasFilters => !_filters.IsEmpty;
    }
}
