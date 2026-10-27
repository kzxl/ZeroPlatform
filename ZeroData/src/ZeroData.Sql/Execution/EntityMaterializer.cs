using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Execution
{
    /// <summary>
    /// High-performance entity materializer that transforms IDataReader rows into C# objects.
    /// Uses cached Expression Tree compiled delegates for near-native execution speed.
    /// Thread-safe and resilient against schema differences across queries.
    /// Integrated with ZeroPrimitives.FastConvert for zero-allocation register unboxing.
    /// </summary>
    public static class EntityMaterializer
    {
        private static readonly ConcurrentDictionary<(Type TargetType, string SchemaKey), Func<IDataReader, ValueConverterCollection, object>> DelegateCache
            = new ConcurrentDictionary<(Type TargetType, string SchemaKey), Func<IDataReader, ValueConverterCollection, object>>();

        /// <summary>
        /// Materializes the current row of the IDataReader into an instance of T.
        /// </summary>
        public static T Materialize<T>(IDataReader reader, ValueConverterCollection converters = null)
        {
            var materializer = GetMaterializer<T>(reader);
            return materializer(reader, converters);
        }

        /// <summary>
        /// Materializes the current row of the IDataReader into an instance of the specified target type.
        /// </summary>
        public static object Materialize(IDataReader reader, Type targetType, ValueConverterCollection converters = null)
        {
            var materializer = GetMaterializer(targetType, reader);
            return materializer(reader, converters);
        }

        /// <summary>
        /// Pre-resolves and caches the row materialization delegate for type T based on the reader's schema.
        /// Call this once before reader.Read() loops to avoid computing schema keys and looking up dictionaries per row.
        /// </summary>
        public static Func<IDataReader, ValueConverterCollection, T> GetMaterializer<T>(IDataReader reader)
        {
            var targetType = typeof(T);

            if (IsScalarType(targetType))
            {
                return (r, conv) => (T)MaterializeScalar(r, targetType, conv);
            }

            if (targetType == typeof(object) || targetType == typeof(ZeroRow) || targetType == typeof(IDictionary<string, object>))
            {
                return (r, conv) => (T)(object)MaterializeDynamic(r);
            }

            var schemaKey = ComputeSchemaKey(reader);
            var cacheKey = (targetType, schemaKey);

            if (!DelegateCache.TryGetValue(cacheKey, out var materializer))
            {
                materializer = BuildMaterializer(targetType, reader);
                DelegateCache[cacheKey] = materializer;
            }

            return (r, conv) => (T)materializer(r, conv);
        }

        /// <summary>
        /// Pre-resolves and caches the row materialization delegate for targetType based on the reader's schema.
        /// Call this once before reader.Read() loops to avoid computing schema keys and looking up dictionaries per row.
        /// </summary>
        public static Func<IDataReader, ValueConverterCollection, object> GetMaterializer(Type targetType, IDataReader reader)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (IsScalarType(targetType))
            {
                return (r, conv) => MaterializeScalar(r, targetType, conv);
            }

            if (targetType == typeof(object) || targetType == typeof(ZeroRow) || targetType == typeof(IDictionary<string, object>))
            {
                return (r, conv) => MaterializeDynamic(r);
            }

            var schemaKey = ComputeSchemaKey(reader);
            var cacheKey = (targetType, schemaKey);

            if (!DelegateCache.TryGetValue(cacheKey, out var materializer))
            {
                materializer = BuildMaterializer(targetType, reader);
                DelegateCache[cacheKey] = materializer;
            }

            return materializer;
        }

        private static object MaterializeScalar(IDataReader reader, Type targetType, ValueConverterCollection converters)
        {
            if (reader.IsDBNull(0))
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            var rawValue = reader.GetValue(0);
            if (converters != null && converters.HasConverter(targetType))
            {
                return converters.ConvertFromDb(rawValue, targetType);
            }
            return FastConvert.ChangeType(rawValue, targetType);
        }

        private static ZeroRow MaterializeDynamic(IDataReader reader)
        {
            var row = new ZeroRow(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[name] = val;
            }
            return row;
        }

        private static bool IsScalarType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan)
                || underlying == typeof(Guid)
                || underlying == typeof(byte[]);
        }

        private static string ComputeSchemaKey(IDataReader reader)
        {
            var sb = new StringBuilder(reader.FieldCount * 16);
            sb.Append(reader.FieldCount).Append(';');
            for (int i = 0; i < reader.FieldCount; i++)
            {
                sb.Append(reader.GetName(i)).Append(':')
                  .Append(reader.GetFieldType(i).Name).Append(';');
            }
            return sb.ToString();
        }

        private static readonly MethodInfo _isDbNullMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull), new[] { typeof(int) });
        private static readonly MethodInfo _getValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue), new[] { typeof(int) });
        private static readonly MethodInfo _getInt32Method = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32), new[] { typeof(int) });
        private static readonly MethodInfo _getInt64Method = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64), new[] { typeof(int) });
        private static readonly MethodInfo _getInt16Method = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16), new[] { typeof(int) });
        private static readonly MethodInfo _getByteMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte), new[] { typeof(int) });
        private static readonly MethodInfo _getDoubleMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble), new[] { typeof(int) });
        private static readonly MethodInfo _getFloatMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat), new[] { typeof(int) });
        private static readonly MethodInfo _getDecimalMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal), new[] { typeof(int) });
        private static readonly MethodInfo _getBooleanMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetBoolean), new[] { typeof(int) });
        private static readonly MethodInfo _getDateTimeMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDateTime), new[] { typeof(int) });
        private static readonly MethodInfo _getGuidMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetGuid), new[] { typeof(int) });
        private static readonly MethodInfo _getStringMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString), new[] { typeof(int) });
        private static readonly MethodInfo _convertFromDbMethod = typeof(ValueConverterCollection).GetMethod(nameof(ValueConverterCollection.ConvertFromDb), new[] { typeof(object), typeof(Type) });
        private static readonly MethodInfo _hasConverterMethod = typeof(ValueConverterCollection).GetMethod(nameof(ValueConverterCollection.HasConverter), new[] { typeof(Type) });

        private static Expression TryBuildDirectTypedAccessor(Expression readerParam, ConstantExpression indexConst, Type fieldType, Type propType)
        {
            var nonNullPropType = Nullable.GetUnderlyingType(propType) ?? propType;
            bool isNullable = Nullable.GetUnderlyingType(propType) != null;

            if (fieldType == nonNullPropType)
            {
                Expression typedCall = null;
                if (nonNullPropType == typeof(int))
                    typedCall = Expression.Call(readerParam, _getInt32Method, indexConst);
                else if (nonNullPropType == typeof(long))
                    typedCall = Expression.Call(readerParam, _getInt64Method, indexConst);
                else if (nonNullPropType == typeof(short))
                    typedCall = Expression.Call(readerParam, _getInt16Method, indexConst);
                else if (nonNullPropType == typeof(byte))
                    typedCall = Expression.Call(readerParam, _getByteMethod, indexConst);
                else if (nonNullPropType == typeof(double))
                    typedCall = Expression.Call(readerParam, _getDoubleMethod, indexConst);
                else if (nonNullPropType == typeof(float))
                    typedCall = Expression.Call(readerParam, _getFloatMethod, indexConst);
                else if (nonNullPropType == typeof(decimal))
                    typedCall = Expression.Call(readerParam, _getDecimalMethod, indexConst);
                else if (nonNullPropType == typeof(bool))
                    typedCall = Expression.Call(readerParam, _getBooleanMethod, indexConst);
                else if (nonNullPropType == typeof(DateTime))
                    typedCall = Expression.Call(readerParam, _getDateTimeMethod, indexConst);
                else if (nonNullPropType == typeof(Guid))
                    typedCall = Expression.Call(readerParam, _getGuidMethod, indexConst);
                else if (nonNullPropType == typeof(string))
                    return Expression.Call(readerParam, _getStringMethod, indexConst);

                if (typedCall != null)
                {
                    return isNullable ? Expression.Convert(typedCall, propType) : typedCall;
                }
            }

            return null;
        }

        private static Func<IDataReader, ValueConverterCollection, object> BuildMaterializer(Type targetType, IDataReader reader)
        {
            var ctor = targetType.GetConstructor(Type.EmptyTypes);
            if (ctor == null && targetType.IsValueType)
            {
                return BuildFallbackMaterializer(targetType, reader);
            }

            if (ctor == null)
            {
                throw new InvalidOperationException(
                    $"Type '{targetType.FullName}' must have a parameterless constructor to be materialized from SQL.");
            }

            try
            {
                var readerParam = Expression.Parameter(typeof(IDataReader), "reader");
                var convertersParam = Expression.Parameter(typeof(ValueConverterCollection), "converters");
                var instanceVar = Expression.Variable(targetType, "instance");

                var expressions = new List<Expression>
                {
                    Expression.Assign(instanceVar, Expression.New(ctor))
                };

                EntityMapping mapping = null;
                try
                {
                    mapping = MappingCache.GetMapping(targetType);
                }
                catch
                {
                    // Non-mapped DTO
                }

                var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                    .ToList();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var fieldType = reader.GetFieldType(i);
                    PropertyInfo matchedProp = null;

                    if (mapping != null)
                    {
                        var colMapping = mapping.Columns.FirstOrDefault(c =>
                            string.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Property.Name, colName, StringComparison.OrdinalIgnoreCase));

                        if (colMapping != null)
                            matchedProp = colMapping.Property;
                    }

                    if (matchedProp == null)
                    {
                        matchedProp = properties.FirstOrDefault(p =>
                            string.Equals(p.Name, colName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchedProp == null || !matchedProp.CanWrite)
                        continue;

                    var indexConst = Expression.Constant(i);
                    var propType = matchedProp.PropertyType;
                    var propTypeConst = Expression.Constant(propType);

                    var isDbNullExpr = Expression.Call(readerParam, _isDbNullMethod, indexConst);
                    var rawValExpr = Expression.Call(readerParam, _getValueMethod, indexConst);

                    // Zero-alloc fast path: direct typed accessor if fieldType matches propType
                    var directTypedExpr = TryBuildDirectTypedAccessor(readerParam, indexConst, fieldType, propType);
                    var valueExpr = directTypedExpr ?? BuildDirectConvertExpression(rawValExpr, propType);

                    var hasConverterCheck = Expression.AndAlso(
                        Expression.NotEqual(convertersParam, Expression.Constant(null, typeof(ValueConverterCollection))),
                        Expression.Call(convertersParam, _hasConverterMethod, propTypeConst)
                    );

                    var convertWithConverter = Expression.Convert(
                        Expression.Call(convertersParam, _convertFromDbMethod, rawValExpr, propTypeConst),
                        propType);

                    var resolvedValueExpr = Expression.Condition(hasConverterCheck, convertWithConverter, valueExpr);

                    var assignPropExpr = Expression.Assign(Expression.Property(instanceVar, matchedProp), resolvedValueExpr);
                    var conditionExpr = Expression.IfThen(Expression.Not(isDbNullExpr), assignPropExpr);

                    expressions.Add(conditionExpr);
                }

                expressions.Add(Expression.Convert(instanceVar, typeof(object)));

                var body = Expression.Block(new[] { instanceVar }, expressions);
                var lambda = Expression.Lambda<Func<IDataReader, ValueConverterCollection, object>>(body, readerParam, convertersParam);
                return lambda.Compile();
            }
            catch
            {
                return BuildFallbackMaterializer(targetType, reader);
            }
        }

        private static Expression BuildDirectConvertExpression(Expression rawValExpr, Type targetType)
        {
            if (targetType == typeof(string))
            {
                var toStringMethod = typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes);
                return Expression.Condition(
                    Expression.TypeIs(rawValExpr, typeof(string)),
                    Expression.Convert(rawValExpr, typeof(string)),
                    Expression.Call(rawValExpr, toStringMethod));
            }

            if (targetType == typeof(int))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsInt), new[] { typeof(object), typeof(int) }), rawValExpr, Expression.Constant(0));

            if (targetType == typeof(int?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableInt), new[] { typeof(object), typeof(int?) }), rawValExpr, Expression.Constant(null, typeof(int?)));

            if (targetType == typeof(long))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsLong), new[] { typeof(object), typeof(long) }), rawValExpr, Expression.Constant(0L));

            if (targetType == typeof(long?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableLong), new[] { typeof(object), typeof(long?) }), rawValExpr, Expression.Constant(null, typeof(long?)));

            if (targetType == typeof(decimal))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsDecimal), new[] { typeof(object), typeof(decimal) }), rawValExpr, Expression.Constant(0m));

            if (targetType == typeof(decimal?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableDecimal), new[] { typeof(object), typeof(decimal?) }), rawValExpr, Expression.Constant(null, typeof(decimal?)));

            if (targetType == typeof(double))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsDouble), new[] { typeof(object), typeof(double) }), rawValExpr, Expression.Constant(0.0));

            if (targetType == typeof(double?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableDouble), new[] { typeof(object), typeof(double?) }), rawValExpr, Expression.Constant(null, typeof(double?)));

            if (targetType == typeof(bool))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsBool), new[] { typeof(object), typeof(bool) }), rawValExpr, Expression.Constant(false));

            if (targetType == typeof(bool?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableBool), new[] { typeof(object), typeof(bool?) }), rawValExpr, Expression.Constant(null, typeof(bool?)));

            if (targetType == typeof(DateTime))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsDateTime), new[] { typeof(object), typeof(DateTime) }), rawValExpr, Expression.Constant(default(DateTime)));

            if (targetType == typeof(DateTime?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableDateTime), new[] { typeof(object), typeof(DateTime?) }), rawValExpr, Expression.Constant(null, typeof(DateTime?)));

            if (targetType == typeof(Guid))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsGuid), new[] { typeof(object), typeof(Guid) }), rawValExpr, Expression.Constant(default(Guid)));

            if (targetType == typeof(Guid?))
                return Expression.Call(typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.AsNullableGuid), new[] { typeof(object), typeof(Guid?) }), rawValExpr, Expression.Constant(null, typeof(Guid?)));

            // Universal fallback to generic ZeroPrimitives.FastConvert.To<T>
            var toGenericMethod = typeof(ZeroPrimitives.FastConvert).GetMethod(nameof(ZeroPrimitives.FastConvert.To), new[] { typeof(object) });
            return Expression.Call(toGenericMethod.MakeGenericMethod(targetType), rawValExpr);
        }

        private static Func<IDataReader, ValueConverterCollection, object> BuildFallbackMaterializer(Type targetType, IDataReader reader)
        {
            EntityMapping mapping = null;
            try { mapping = MappingCache.GetMapping(targetType); } catch { }

            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToList();

            var bindings = new List<(int Ordinal, PropertyInfo Prop)>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                PropertyInfo prop = null;

                if (mapping != null)
                {
                    var colMapping = mapping.Columns.FirstOrDefault(c =>
                        string.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Property.Name, colName, StringComparison.OrdinalIgnoreCase));
                    if (colMapping != null) prop = colMapping.Property;
                }

                if (prop == null)
                {
                    prop = properties.FirstOrDefault(p =>
                        string.Equals(p.Name, colName, StringComparison.OrdinalIgnoreCase));
                }

                if (prop != null && prop.CanWrite)
                {
                    bindings.Add((i, prop));
                }
            }

            var bindingsArray = bindings.ToArray();

            return (r, conv) =>
            {
                var entity = Activator.CreateInstance(targetType);
                for (int i = 0; i < bindingsArray.Length; i++)
                {
                    var (ordinal, prop) = bindingsArray[i];
                    if (!r.IsDBNull(ordinal))
                    {
                        var raw = r.GetValue(ordinal);
                        object val;
                        if (conv != null && conv.HasConverter(prop.PropertyType))
                        {
                            val = conv.ConvertFromDb(raw, prop.PropertyType);
                        }
                        else
                        {
                            val = FastConvert.ChangeType(raw, prop.PropertyType);
                        }
                        prop.SetValue(entity, val, null);
                    }
                }
                return entity;
            };
        }

        /// <summary>
        /// Clears all compiled materialization delegates.
        /// </summary>
        public static void ClearCache()
        {
            DelegateCache.Clear();
        }
    }
}
