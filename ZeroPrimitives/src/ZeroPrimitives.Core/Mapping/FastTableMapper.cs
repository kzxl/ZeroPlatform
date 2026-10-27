using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroPrimitives.Mapping
{
    /// <summary>
    /// Enterprise-grade, ultra-high-throughput ADO.NET micro-mapper.
    /// Compiles Expression Trees into native IL delegates to project IDataReader, IDataRecord, DataTable, and DataRow
    /// into strongly-typed DTOs/POCOs with zero runtime reflection and automatic type coercion via FastConvert.
    /// </summary>
    public static class FastTableMapper
    {
        private static readonly ConcurrentDictionary<(Type TargetType, string SchemaSignature), Delegate> RecordMapperCache =
            new ConcurrentDictionary<(Type, string), Delegate>();

        private static readonly ConcurrentDictionary<(Type TargetType, string SchemaSignature), Delegate> RowMapperCache =
            new ConcurrentDictionary<(Type, string), Delegate>();

        #region Public Extension Methods

        /// <summary>
        /// Reads all rows from the IDataReader and projects them into a List of T.
        /// Reuses compiled expression delegates matching the reader's schema signature.
        /// </summary>
        public static List<T> ToList<T>(this IDataReader reader) where T : class, new()
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var list = new List<T>();
            if (reader.FieldCount == 0) return list;

            var mapper = GetOrCreateRecordMapper<T>(reader);
            while (reader.Read())
            {
                list.Add(mapper(reader));
            }

            return list;
        }

        /// <summary>
        /// Projects the current row of an IDataRecord into an instance of T.
        /// </summary>
        public static T To<T>(this IDataRecord record) where T : class, new()
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var mapper = GetOrCreateRecordMapper<T>(record);
            return mapper(record);
        }

        /// <summary>
        /// Projects all rows of a DataTable into a List of T.
        /// </summary>
        public static List<T> ToList<T>(this DataTable table) where T : class, new()
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var list = new List<T>(table.Rows.Count);
            if (table.Columns.Count == 0 || table.Rows.Count == 0) return list;

            var mapper = GetOrCreateRowMapper<T>(table);
            for (int i = 0; i < table.Rows.Count; i++)
            {
                list.Add(mapper(table.Rows[i]));
            }

            return list;
        }

        /// <summary>
        /// Projects a DataRow into an instance of T.
        /// </summary>
        public static T To<T>(this DataRow row) where T : class, new()
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var mapper = GetOrCreateRowMapper<T>(row.Table);
            return mapper(row);
        }

        /// <summary>
        /// Projects an IEnumerable of T into a DataTable using compiled expression row writers.
        /// Zero reflection overhead per row.
        /// </summary>
        public static DataTable ToDataTable<T>(this IEnumerable<T>? source, string? tableName = null)
        {
            var table = string.IsNullOrEmpty(tableName) ? new DataTable() : new DataTable(tableName);

            var colNames = TableSchemaCache<T>.ColumnNames;
            var colTypes = TableSchemaCache<T>.ColumnTypes;
            var rowWriter = TableSchemaCache<T>.RowWriter;

            for (int i = 0; i < colNames.Length; i++)
            {
                table.Columns.Add(colNames[i], colTypes[i]);
            }

            if (source == null) return table;

            table.BeginLoadData();
            var values = new object[colNames.Length];

            foreach (var item in source)
            {
                if (item != null)
                {
                    rowWriter(item, values);
                    table.Rows.Add(values);
                }
            }

            table.EndLoadData();
            return table;
        }

        #endregion

        #region Expression Tree Compilation for IDataRecord

        private static Func<IDataRecord, T> GetOrCreateRecordMapper<T>(IDataRecord record) where T : class, new()
        {
            var targetType = typeof(T);
            string sig = BuildRecordSchemaSignature(record);
            var key = (targetType, sig);

            if (!RecordMapperCache.TryGetValue(key, out var del))
            {
                del = CompileRecordMapper<T>(record);
                RecordMapperCache[key] = del;
            }

            return (Func<IDataRecord, T>)del;
        }

        private static string BuildRecordSchemaSignature(IDataRecord record)
        {
            int fieldCount = record.FieldCount;
            var sb = new System.Text.StringBuilder(fieldCount * 16);
            for (int i = 0; i < fieldCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(record.GetName(i));
            }
            return sb.ToString();
        }

        private static Func<IDataRecord, T> CompileRecordMapper<T>(IDataRecord record) where T : class, new()
        {
            var targetType = typeof(T);
            var recordParam = Expression.Parameter(typeof(IDataRecord), "record");
            var itemVar = Expression.Variable(targetType, "item");

            var statements = new List<Expression>
            {
                Expression.Assign(itemVar, Expression.New(targetType))
            };

            // Build map of column names to ordinals
            var colMap = new Dictionary<string, int>(record.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < record.FieldCount; i++)
            {
                colMap[record.GetName(i)] = i;
            }

            var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var isDbNullMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull), new[] { typeof(int) })!;
            var getValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue), new[] { typeof(int) })!;
            var fastConvertMethod = typeof(FastConvert).GetMethod(nameof(FastConvert.To), new[] { typeof(object) })!;

            foreach (var prop in props)
            {
                if (!prop.CanWrite) continue;
                if (!colMap.TryGetValue(prop.Name, out int ordinal)) continue;

                var ordinalExpr = Expression.Constant(ordinal);
                var isDbNullExpr = Expression.Call(recordParam, isDbNullMethod, ordinalExpr);

                var getValueExpr = Expression.Call(recordParam, getValueMethod, ordinalExpr);

                // Build typed value expression
                Expression valueExpr;
                Type propType = prop.PropertyType;

                if (propType == typeof(string))
                {
                    // (string)getValue or val.ToString()
                    var toStringMethod = typeof(object).GetMethod(nameof(object.ToString))!;
                    valueExpr = Expression.Condition(
                        Expression.TypeIs(getValueExpr, typeof(string)),
                        Expression.Convert(getValueExpr, typeof(string)),
                        Expression.Call(getValueExpr, toStringMethod));
                }
                else
                {
                    // Generic FastConvert.To<TProp>(val)
                    var genericConvert = fastConvertMethod.MakeGenericMethod(propType);
                    valueExpr = Expression.Call(genericConvert, getValueExpr);
                }

                var assignExpr = Expression.Assign(Expression.Property(itemVar, prop), valueExpr);

                // if (!record.IsDBNull(ordinal)) { item.Prop = value; }
                var ifNotNullExpr = Expression.IfThen(
                    Expression.IsFalse(isDbNullExpr),
                    assignExpr);

                statements.Add(ifNotNullExpr);
            }

            statements.Add(itemVar);

            var body = Expression.Block(new[] { itemVar }, statements);
            var lambda = Expression.Lambda<Func<IDataRecord, T>>(body, recordParam);
            return lambda.Compile();
        }

        #endregion

        #region Expression Tree Compilation for DataRow

        private static Func<DataRow, T> GetOrCreateRowMapper<T>(DataTable table) where T : class, new()
        {
            var targetType = typeof(T);
            string sig = BuildTableSchemaSignature(table);
            var key = (targetType, sig);

            if (!RowMapperCache.TryGetValue(key, out var del))
            {
                del = CompileRowMapper<T>(table);
                RowMapperCache[key] = del;
            }

            return (Func<DataRow, T>)del;
        }

        private static string BuildTableSchemaSignature(DataTable table)
        {
            int colCount = table.Columns.Count;
            var sb = new System.Text.StringBuilder(colCount * 16);
            for (int i = 0; i < colCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(table.Columns[i].ColumnName);
            }
            return sb.ToString();
        }

        private static Func<DataRow, T> CompileRowMapper<T>(DataTable table) where T : class, new()
        {
            var targetType = typeof(T);
            var rowParam = Expression.Parameter(typeof(DataRow), "row");
            var itemVar = Expression.Variable(targetType, "item");

            var statements = new List<Expression>
            {
                Expression.Assign(itemVar, Expression.New(targetType))
            };

            var colMap = new Dictionary<string, int>(table.Columns.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < table.Columns.Count; i++)
            {
                colMap[table.Columns[i].ColumnName] = i;
            }

            var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var isNullMethod = typeof(DataRow).GetMethod(nameof(DataRow.IsNull), new[] { typeof(int) })!;
            var indexerMethod = typeof(DataRow).GetMethod("get_Item", new[] { typeof(int) })!;
            var fastConvertMethod = typeof(FastConvert).GetMethod(nameof(FastConvert.To), new[] { typeof(object) })!;

            foreach (var prop in props)
            {
                if (!prop.CanWrite) continue;
                if (!colMap.TryGetValue(prop.Name, out int ordinal)) continue;

                var ordinalExpr = Expression.Constant(ordinal);
                var isNullExpr = Expression.Call(rowParam, isNullMethod, ordinalExpr);
                var getValueExpr = Expression.Call(rowParam, indexerMethod, ordinalExpr);

                Expression valueExpr;
                Type propType = prop.PropertyType;

                if (propType == typeof(string))
                {
                    var toStringMethod = typeof(object).GetMethod(nameof(object.ToString))!;
                    valueExpr = Expression.Condition(
                        Expression.TypeIs(getValueExpr, typeof(string)),
                        Expression.Convert(getValueExpr, typeof(string)),
                        Expression.Call(getValueExpr, toStringMethod));
                }
                else
                {
                    var genericConvert = fastConvertMethod.MakeGenericMethod(propType);
                    valueExpr = Expression.Call(genericConvert, getValueExpr);
                }

                var assignExpr = Expression.Assign(Expression.Property(itemVar, prop), valueExpr);

                var ifNotNullExpr = Expression.IfThen(
                    Expression.IsFalse(isNullExpr),
                    assignExpr);

                statements.Add(ifNotNullExpr);
            }

            statements.Add(itemVar);

            var body = Expression.Block(new[] { itemVar }, statements);
            var lambda = Expression.Lambda<Func<DataRow, T>>(body, rowParam);
            return lambda.Compile();
        }

        #endregion

        #region IEnumerable to DataTable Compilation

        private static class TableSchemaCache<T>
        {
            public static readonly string[] ColumnNames;
            public static readonly Type[] ColumnTypes;
            public static readonly Action<T, object[]> RowWriter;

            static TableSchemaCache()
            {
                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var readableProps = new List<PropertyInfo>();
                foreach (var p in props)
                {
                    if (p.CanRead && p.GetIndexParameters().Length == 0)
                    {
                        readableProps.Add(p);
                    }
                }

                int count = readableProps.Count;
                ColumnNames = new string[count];
                ColumnTypes = new Type[count];

                var itemParam = Expression.Parameter(typeof(T), "item");
                var arrayParam = Expression.Parameter(typeof(object[]), "array");
                var statements = new List<Expression>(Math.Max(1, count));

                var dbNullConstant = Expression.Constant(DBNull.Value, typeof(object));

                for (int i = 0; i < count; i++)
                {
                    var prop = readableProps[i];
                    ColumnNames[i] = prop.Name;
                    var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    ColumnTypes[i] = underlying;

                    var indexConst = Expression.Constant(i);
                    var propAccess = Expression.Property(itemParam, prop);

                    Expression valExpr;
                    if (prop.PropertyType.IsValueType)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                        {
                            valExpr = Expression.Condition(
                                Expression.Property(propAccess, "HasValue"),
                                Expression.Convert(Expression.Property(propAccess, "Value"), typeof(object)),
                                dbNullConstant);
                        }
                        else
                        {
                            valExpr = Expression.Convert(propAccess, typeof(object));
                        }
                    }
                    else
                    {
                        valExpr = Expression.Condition(
                            Expression.ReferenceNotEqual(propAccess, Expression.Constant(null, prop.PropertyType)),
                            Expression.Convert(propAccess, typeof(object)),
                            dbNullConstant);
                    }

                    var arrayAssign = Expression.Assign(
                        Expression.ArrayAccess(arrayParam, indexConst),
                        valExpr);

                    statements.Add(arrayAssign);
                }

                if (statements.Count == 0)
                {
                    statements.Add(Expression.Empty());
                }

                var block = Expression.Block(statements);
                RowWriter = Expression.Lambda<Action<T, object[]>>(block, itemParam, arrayParam).Compile();
            }
        }

        #endregion
    }
}
