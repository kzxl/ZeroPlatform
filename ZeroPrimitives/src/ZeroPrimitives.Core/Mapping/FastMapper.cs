using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroPrimitives.Mapping
{
    /// <summary>
    /// Ultra-fast, expression-tree compiled object-to-object shallow mapper.
    /// Eliminates reflection overhead by generating native IL delegates cached per type-pair.
    /// </summary>
    public static class FastMapper
    {
        private static readonly ConcurrentDictionary<(Type, Type), Delegate> CopierCache =
            new ConcurrentDictionary<(Type, Type), Delegate>();

        private static readonly ConcurrentDictionary<(Type, Type), Action<object, object>> DynamicCopierCache =
            new ConcurrentDictionary<(Type, Type), Action<object, object>>();

        /// <summary>
        /// Copies matching public properties from source to target.
        /// </summary>
        public static TTarget? CopyTo<TSource, TTarget>(TSource? source, TTarget? target)
            where TSource : class
            where TTarget : class
        {
            if (source == null || target == null) return target;

            var key = (typeof(TSource), typeof(TTarget));
            if (!CopierCache.TryGetValue(key, out var del))
            {
                del = CreateCopier<TSource, TTarget>();
                CopierCache[key] = del;
            }

            var copier = (Action<TSource, TTarget>)del;
            copier(source, target);
            return target;
        }

        /// <summary>
        /// Creates a new instance of TTarget and copies matching properties from source.
        /// </summary>
        public static TTarget Map<TSource, TTarget>(TSource source)
            where TSource : class
            where TTarget : class, new()
        {
            var target = new TTarget();
            CopyTo(source, target);
            return target;
        }

        /// <summary>
        /// Maps a collection of TSource to a List of TTarget using compiled IL delegates.
        /// </summary>
        public static List<TTarget> MapList<TSource, TTarget>(IEnumerable<TSource>? sourceList)
            where TSource : class
            where TTarget : class, new()
        {
            var result = new List<TTarget>();
            if (sourceList == null) return result;

            var key = (typeof(TSource), typeof(TTarget));
            if (!CopierCache.TryGetValue(key, out var del))
            {
                del = CreateCopier<TSource, TTarget>();
                CopierCache[key] = del;
            }

            var copier = (Action<TSource, TTarget>)del;
            foreach (var item in sourceList)
            {
                if (item == null) continue;
                var target = new TTarget();
                copier(item, target);
                result.Add(target);
            }

            return result;
        }

        /// <summary>
        /// Dynamically copies matching properties from source (untyped object) to target using cached compiled expressions.
        /// </summary>
        public static TTarget? CopyDynamic<TTarget>(object? source, TTarget? target)
            where TTarget : class
        {
            if (source == null || target == null) return target;

            var sourceType = source.GetType();
            var targetType = typeof(TTarget);
            var key = (sourceType, targetType);

            if (!DynamicCopierCache.TryGetValue(key, out var copier))
            {
                copier = CreateDynamicCopier(sourceType, targetType);
                DynamicCopierCache[key] = copier;
            }

            copier(source, target);
            return target;
        }

        /// <summary>
        /// Dynamically maps untyped object to a new instance of TTarget using cached compiled expressions.
        /// </summary>
        public static TTarget? MapDynamic<TTarget>(object? source)
            where TTarget : class, new()
        {
            if (source == null) return null;
            var target = new TTarget();
            CopyDynamic(source, target);
            return target;
        }

        /// <summary>
        /// Dynamically maps an untyped collection of objects to a List of TTarget using cached compiled expressions.
        /// </summary>
        public static List<TTarget> MapListDynamic<TTarget>(IEnumerable<object>? sourceList)
            where TTarget : class, new()
        {
            var result = new List<TTarget>();
            if (sourceList == null) return result;

            Action<object, object>? cachedCopier = null;
            Type? lastSourceType = null;
            var targetType = typeof(TTarget);

            foreach (var item in sourceList)
            {
                if (item == null) continue;

                var itemType = item.GetType();
                if (itemType != lastSourceType || cachedCopier == null)
                {
                    lastSourceType = itemType;
                    var key = (itemType, targetType);
                    if (!DynamicCopierCache.TryGetValue(key, out cachedCopier))
                    {
                        cachedCopier = CreateDynamicCopier(itemType, targetType);
                        DynamicCopierCache[key] = cachedCopier;
                    }
                }

                var target = new TTarget();
                cachedCopier(item, target);
                result.Add(target);
            }

            return result;
        }

        private static Action<TSource, TTarget> CreateCopier<TSource, TTarget>()
        {
            var sourceType = typeof(TSource);
            var targetType = typeof(TTarget);

            var sourceParam = Expression.Parameter(sourceType, "source");
            var targetParam = Expression.Parameter(targetType, "target");

            var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var blockExpressions = new List<Expression>();

            foreach (var targetProp in targetProps)
            {
                if (!targetProp.CanWrite) continue;

                // Find matching source prop (case-insensitive)
                PropertyInfo? matchSource = null;
                for (int i = 0; i < sourceProps.Length; i++)
                {
                    if (string.Equals(sourceProps[i].Name, targetProp.Name, StringComparison.OrdinalIgnoreCase) && sourceProps[i].CanRead)
                    {
                        matchSource = sourceProps[i];
                        break;
                    }
                }

                if (matchSource == null) continue;

                var sourceAccess = Expression.Property(sourceParam, matchSource);

                if (targetProp.PropertyType == matchSource.PropertyType)
                {
                    var assign = Expression.Assign(Expression.Property(targetParam, targetProp), sourceAccess);
                    blockExpressions.Add(assign);
                }
                else if (targetProp.PropertyType.IsAssignableFrom(matchSource.PropertyType))
                {
                    var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
                    var assign = Expression.Assign(Expression.Property(targetParam, targetProp), cast);
                    blockExpressions.Add(assign);
                }
                else
                {
                    var underlyingSource = Nullable.GetUnderlyingType(matchSource.PropertyType) ?? matchSource.PropertyType;
                    var underlyingTarget = Nullable.GetUnderlyingType(targetProp.PropertyType) ?? targetProp.PropertyType;
                    if (underlyingSource == underlyingTarget)
                    {
                        var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
                        var assign = Expression.Assign(Expression.Property(targetParam, targetProp), cast);
                        blockExpressions.Add(assign);
                    }
                }
            }

            if (blockExpressions.Count == 0)
            {
                return (src, dst) => { };
            }

            var block = Expression.Block(blockExpressions);
            var lambda = Expression.Lambda<Action<TSource, TTarget>>(block, sourceParam, targetParam);
            return lambda.Compile();
        }

        private static Action<object, object> CreateDynamicCopier(Type sourceType, Type targetType)
        {
            var sourceParam = Expression.Parameter(typeof(object), "source");
            var targetParam = Expression.Parameter(typeof(object), "target");

            var typedSource = Expression.Variable(sourceType, "typedSource");
            var typedTarget = Expression.Variable(targetType, "typedTarget");

            var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var blockExpressions = new List<Expression>();
            blockExpressions.Add(Expression.Assign(typedSource, Expression.Convert(sourceParam, sourceType)));
            blockExpressions.Add(Expression.Assign(typedTarget, Expression.Convert(targetParam, targetType)));

            foreach (var targetProp in targetProps)
            {
                if (!targetProp.CanWrite) continue;

                PropertyInfo? matchSource = null;
                for (int i = 0; i < sourceProps.Length; i++)
                {
                    if (string.Equals(sourceProps[i].Name, targetProp.Name, StringComparison.OrdinalIgnoreCase) && sourceProps[i].CanRead)
                    {
                        matchSource = sourceProps[i];
                        break;
                    }
                }

                if (matchSource == null) continue;

                var sourceAccess = Expression.Property(typedSource, matchSource);

                if (targetProp.PropertyType == matchSource.PropertyType)
                {
                    var assign = Expression.Assign(Expression.Property(typedTarget, targetProp), sourceAccess);
                    blockExpressions.Add(assign);
                }
                else if (targetProp.PropertyType.IsAssignableFrom(matchSource.PropertyType))
                {
                    var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
                    var assign = Expression.Assign(Expression.Property(typedTarget, targetProp), cast);
                    blockExpressions.Add(assign);
                }
                else
                {
                    var underlyingSource = Nullable.GetUnderlyingType(matchSource.PropertyType) ?? matchSource.PropertyType;
                    var underlyingTarget = Nullable.GetUnderlyingType(targetProp.PropertyType) ?? targetProp.PropertyType;
                    if (underlyingSource == underlyingTarget)
                    {
                        var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
                        var assign = Expression.Assign(Expression.Property(typedTarget, targetProp), cast);
                        blockExpressions.Add(assign);
                    }
                }
            }

            var block = Expression.Block(new[] { typedSource, typedTarget }, blockExpressions);
            var lambda = Expression.Lambda<Action<object, object>>(block, sourceParam, targetParam);
            return lambda.Compile();
        }
    }
}
