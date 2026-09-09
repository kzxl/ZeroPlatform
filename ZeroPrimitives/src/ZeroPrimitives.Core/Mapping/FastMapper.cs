using System;
using System.Collections.Concurrent;
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

        private static Action<TSource, TTarget> CreateCopier<TSource, TTarget>()
        {
            var sourceType = typeof(TSource);
            var targetType = typeof(TTarget);

            var sourceParam = Expression.Parameter(sourceType, "source");
            var targetParam = Expression.Parameter(targetType, "target");

            var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var blockExpressions = new System.Collections.Generic.List<Expression>();

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
                    // Same type: target.Prop = source.Prop
                    var assign = Expression.Assign(Expression.Property(targetParam, targetProp), sourceAccess);
                    blockExpressions.Add(assign);
                }
                else if (targetProp.PropertyType.IsAssignableFrom(matchSource.PropertyType))
                {
                    // Assignable: target.Prop = (TargetType)source.Prop
                    var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
                    var assign = Expression.Assign(Expression.Property(targetParam, targetProp), cast);
                    blockExpressions.Add(assign);
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
    }
}
