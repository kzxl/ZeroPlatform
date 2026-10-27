using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroData.Core;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Execution;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;

namespace ZeroData.Sql
{
    /// <summary>
    /// Provides bulk operations for high-performance data manipulation.
    /// Uses native SqlBulkCopy when available on SQL Server, and compiled-getter multi-row batching across all database dialects.
    /// </summary>
    public static class BulkOperations
    {
        // SQL Server caps parameters at 2100; stay well under to be safe across providers.
        private const int MaxParametersPerCommand = 2000;

        /// <summary>
        /// Inserts multiple entities using SqlBulkCopy (on SQL Server) or chunked multi-row INSERT with compiled getters.
        /// </summary>
        public static int BulkInsert<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var insertColumns = mapping.InsertableColumns ?? mapping.Columns.Where(c => !c.IsDbGenerated).ToList();
            if (insertColumns.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no insertable columns.");

            // Fast path: SQL Server native SqlBulkCopy
            if (TryGetSqlConnection(connection, out var sqlConn))
            {
                if (TrySqlBulkCopy(sqlConn, entityList, mapping, insertColumns, transaction, out var affected))
                    return affected;
            }

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var quotedCols = insertColumns.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();
            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / insertColumns.Count);
            int totalAffected = 0;

            foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
            {
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunk.Count);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramNames = new List<string>(insertColumns.Count);
                    for (int j = 0; j < insertColumns.Count; j++)
                    {
                        var col = insertColumns[j];
                        var paramName = $"@p{i}_{col.Property.Name}";
                        var val = col.Getter != null ? col.Getter(chunk[i]) : col.Property.GetValue(chunk[i]);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkInsertSql(table, quotedCols, parameterRows);
                totalAffected += connection.Execute(sql, parameters, transaction);
            }

            return totalAffected;
        }

        /// <summary>
        /// Asynchronously inserts multiple entities using SqlBulkCopy (on SQL Server) or chunked multi-row INSERT with compiled getters.
        /// </summary>
        public static async Task<int> BulkInsertAsync<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null, CancellationToken cancellationToken = default) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var insertColumns = mapping.InsertableColumns ?? mapping.Columns.Where(c => !c.IsDbGenerated).ToList();
            if (insertColumns.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no insertable columns.");

            // Fast path: SQL Server native SqlBulkCopy
            if (TryGetSqlConnection(connection, out var sqlConn))
            {
                var (success, bulkAffected) = await TrySqlBulkCopyAsync(sqlConn, entityList, mapping, insertColumns, transaction, cancellationToken).ConfigureAwait(false);
                if (success) return bulkAffected;
            }

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var quotedCols = insertColumns.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();
            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / insertColumns.Count);
            int totalAffected = 0;

            foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunk.Count);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramNames = new List<string>(insertColumns.Count);
                    for (int j = 0; j < insertColumns.Count; j++)
                    {
                        var col = insertColumns[j];
                        var paramName = $"@p{i}_{col.Property.Name}";
                        var val = col.Getter != null ? col.Getter(chunk[i]) : col.Property.GetValue(chunk[i]);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkInsertSql(table, quotedCols, parameterRows);
                totalAffected += await connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                    transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            return totalAffected;
        }

        /// <summary>
        /// Inserts data from a columnar DataFrame directly into the database without creating POCO entities.
        /// Uses SqlBulkCopy for SQL Server when available, or batched multi-row INSERTs.
        /// </summary>
        public static int BulkInsert(IDbConnection connection, DataFrame dataFrame, string tableName,
            IDbTransaction transaction = null, ISqlDialect dialect = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (dataFrame.RowCount == 0 || dataFrame.ColumnCount == 0) return 0;

            if (TryGetSqlConnection(connection, out var sqlConn))
            {
                if (TrySqlBulkCopyDataFrame(sqlConn, dataFrame, tableName, transaction, out var affected))
                    return affected;
            }

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var table = SqlGenerator.QuoteTableName(tableName, d);
            var colCount = dataFrame.ColumnCount;
            var colNames = dataFrame.ColumnNames;
            var quotedCols = colNames.Select(c => d.QuoteIdentifier(c)).ToList();
            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / Math.Max(1, colCount));
            int totalAffected = 0;

            for (int start = 0; start < dataFrame.RowCount; start += rowsPerChunk)
            {
                int chunkCount = Math.Min(rowsPerChunk, dataFrame.RowCount - start);
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunkCount);

                for (int i = 0; i < chunkCount; i++)
                {
                    int rowIdx = start + i;
                    var paramNames = new List<string>(colCount);
                    for (int j = 0; j < colCount; j++)
                    {
                        var col = dataFrame.GetColumnByIndex(j);
                        var paramName = $"@df_{i}_{j}";
                        var val = col.IsNull(rowIdx) ? DBNull.Value : col.GetValue(rowIdx);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkInsertSql(table, quotedCols, parameterRows);
                totalAffected += connection.Execute(sql, parameters, transaction);
            }

            return totalAffected;
        }

        /// <summary>
        /// Asynchronously inserts data from a columnar DataFrame directly into the database without creating POCO entities.
        /// </summary>
        public static async Task<int> BulkInsertAsync(IDbConnection connection, DataFrame dataFrame, string tableName,
            IDbTransaction transaction = null, ISqlDialect dialect = null, CancellationToken cancellationToken = default)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (dataFrame.RowCount == 0 || dataFrame.ColumnCount == 0) return 0;

            if (TryGetSqlConnection(connection, out var sqlConn))
            {
                var (success, affected) = await TrySqlBulkCopyDataFrameAsync(sqlConn, dataFrame, tableName, transaction, cancellationToken).ConfigureAwait(false);
                if (success) return affected;
            }

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var table = SqlGenerator.QuoteTableName(tableName, d);
            var colCount = dataFrame.ColumnCount;
            var colNames = dataFrame.ColumnNames;
            var quotedCols = colNames.Select(c => d.QuoteIdentifier(c)).ToList();
            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / Math.Max(1, colCount));
            int totalAffected = 0;

            for (int start = 0; start < dataFrame.RowCount; start += rowsPerChunk)
            {
                int chunkCount = Math.Min(rowsPerChunk, dataFrame.RowCount - start);
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunkCount);

                for (int i = 0; i < chunkCount; i++)
                {
                    int rowIdx = start + i;
                    var paramNames = new List<string>(colCount);
                    for (int j = 0; j < colCount; j++)
                    {
                        var col = dataFrame.GetColumnByIndex(j);
                        var paramName = $"@df_{i}_{j}";
                        var val = col.IsNull(rowIdx) ? DBNull.Value : col.GetValue(rowIdx);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkInsertSql(table, quotedCols, parameterRows);
                totalAffected += await connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                    transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            return totalAffected;
        }

        /// <summary>
        /// Updates multiple entities by primary key(s) in a single parameterized command list.
        /// Supports composite primary keys.
        /// </summary>
        public static int BulkUpdate<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined.");

            var setColumns = (mapping.UpdatableColumns ?? mapping.Columns.Where(c => !c.IsPrimaryKey && !c.IsDbGenerated))
                .Select(c => $"{d.QuoteIdentifier(c.ColumnName)} = @{c.Property.Name}")
                .ToList();

            if (setColumns.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no updatable columns.");

            var whereClause = string.Join(" AND ",
                primaryKeys.Select(pk => $"{d.QuoteIdentifier(pk.ColumnName)} = @{pk.Property.Name}"));

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var sql = $"UPDATE {table} SET {string.Join(", ", setColumns)} WHERE {whereClause}";

            return connection.Execute(sql, entityList, transaction);
        }

        /// <summary>
        /// Asynchronously updates multiple entities by primary key(s) in a single parameterized command list.
        /// </summary>
        public static async Task<int> BulkUpdateAsync<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null, CancellationToken cancellationToken = default) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined.");

            var setColumns = (mapping.UpdatableColumns ?? mapping.Columns.Where(c => !c.IsPrimaryKey && !c.IsDbGenerated))
                .Select(c => $"{d.QuoteIdentifier(c.ColumnName)} = @{c.Property.Name}")
                .ToList();

            if (setColumns.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no updatable columns.");

            var whereClause = string.Join(" AND ",
                primaryKeys.Select(pk => $"{d.QuoteIdentifier(pk.ColumnName)} = @{pk.Property.Name}"));

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var sql = $"UPDATE {table} SET {string.Join(", ", setColumns)} WHERE {whereClause}";

            return await connection.ExecuteAsync(new CommandDefinition(sql, entityList,
                transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes multiple entities by their primary keys.
        /// Single-column PKs use a chunked IN clause; composite PKs use per-row predicates.
        /// </summary>
        public static int BulkDelete<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined.");

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);

            if (primaryKeys.Count == 1)
            {
                var pk = primaryKeys[0];
                var pkCol = d.QuoteIdentifier(pk.ColumnName);
                int affected = 0;

                foreach (var chunk in ChunkFast(entityList, MaxParametersPerCommand))
                {
                    var parameters = new DynamicParameters();
                    var paramNames = new List<string>(chunk.Count);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var name = $"@id{i}";
                        paramNames.Add(name);
                        var val = pk.Getter != null ? pk.Getter(chunk[i]) : pk.Property.GetValue(chunk[i]);
                        parameters.Add(name, val);
                    }
                    var sql = $"DELETE FROM {table} WHERE {pkCol} IN ({string.Join(", ", paramNames)})";
                    affected += connection.Execute(sql, parameters, transaction);
                }

                return affected;
            }
            else
            {
                int affected = 0;
                var paramsPerRow = primaryKeys.Count;
                var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / paramsPerRow);

                foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
                {
                    var parameters = new DynamicParameters();
                    var rowClauses = new List<string>(chunk.Count);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var conds = new List<string>(primaryKeys.Count);
                        for (int k = 0; k < primaryKeys.Count; k++)
                        {
                            var pk = primaryKeys[k];
                            var name = $"@pk{i}_{pk.Property.Name}";
                            conds.Add($"{d.QuoteIdentifier(pk.ColumnName)} = {name}");
                            var val = pk.Getter != null ? pk.Getter(chunk[i]) : pk.Property.GetValue(chunk[i]);
                            parameters.Add(name, val);
                        }
                        rowClauses.Add($"({string.Join(" AND ", conds)})");
                    }
                    var sql = $"DELETE FROM {table} WHERE {string.Join(" OR ", rowClauses)}";
                    affected += connection.Execute(sql, parameters, transaction);
                }

                return affected;
            }
        }

        /// <summary>
        /// Asynchronously deletes multiple entities by their primary keys.
        /// Single-column PKs use a chunked IN clause; composite PKs use per-row predicates.
        /// </summary>
        public static async Task<int> BulkDeleteAsync<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null, CancellationToken cancellationToken = default) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined.");

            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);

            if (primaryKeys.Count == 1)
            {
                var pk = primaryKeys[0];
                var pkCol = d.QuoteIdentifier(pk.ColumnName);
                int affected = 0;

                foreach (var chunk in ChunkFast(entityList, MaxParametersPerCommand))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parameters = new DynamicParameters();
                    var paramNames = new List<string>(chunk.Count);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var name = $"@id{i}";
                        paramNames.Add(name);
                        var val = pk.Getter != null ? pk.Getter(chunk[i]) : pk.Property.GetValue(chunk[i]);
                        parameters.Add(name, val);
                    }
                    var sql = $"DELETE FROM {table} WHERE {pkCol} IN ({string.Join(", ", paramNames)})";
                    affected += await connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                        transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                }

                return affected;
            }
            else
            {
                int affected = 0;
                var paramsPerRow = primaryKeys.Count;
                var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / paramsPerRow);

                foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parameters = new DynamicParameters();
                    var rowClauses = new List<string>(chunk.Count);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var conds = new List<string>(primaryKeys.Count);
                        for (int k = 0; k < primaryKeys.Count; k++)
                        {
                            var pk = primaryKeys[k];
                            var name = $"@pk{i}_{pk.Property.Name}";
                            conds.Add($"{d.QuoteIdentifier(pk.ColumnName)} = {name}");
                            var val = pk.Getter != null ? pk.Getter(chunk[i]) : pk.Property.GetValue(chunk[i]);
                            parameters.Add(name, val);
                        }
                        rowClauses.Add($"({string.Join(" AND ", conds)})");
                    }
                    var sql = $"DELETE FROM {table} WHERE {string.Join(" OR ", rowClauses)}";
                    affected += await connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                        transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                }

                return affected;
            }
        }

        /// <summary>
        /// Upserts (merges) multiple entities: inserts new records or updates existing records matched by primary key.
        /// Uses dialect-native UPSERT (ON CONFLICT DO UPDATE on SQLite/PostgreSQL, ON DUPLICATE KEY UPDATE on MySQL, MERGE on SQL Server).
        /// </summary>
        public static int BulkMerge<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined for merge/upsert.");

            var allCols = mapping.Columns.Where(c => !c.IsDbGenerated || c.IsPrimaryKey).ToList();
            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var quotedCols = allCols.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();
            var quotedPkCols = primaryKeys.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();

            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / allCols.Count);
            int totalAffected = 0;

            foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
            {
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunk.Count);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramNames = new List<string>(allCols.Count);
                    for (int j = 0; j < allCols.Count; j++)
                    {
                        var col = allCols[j];
                        var paramName = $"@p{i}_{col.Property.Name}";
                        var val = col.Getter != null ? col.Getter(chunk[i]) : col.Property.GetValue(chunk[i]);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkMergeSql(table, quotedCols, quotedPkCols, parameterRows);
                totalAffected += connection.Execute(sql, parameters, transaction);
            }

            return totalAffected;
        }

        /// <summary>
        /// Asynchronously upserts (merges) multiple entities: inserts new records or updates existing records matched by primary key.
        /// </summary>
        public static async Task<int> BulkMergeAsync<T>(IDbConnection connection, IEnumerable<T> entities,
            IDbTransaction transaction = null, ISqlDialect dialect = null, CancellationToken cancellationToken = default) where T : class
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var d = dialect ?? SqlGenerator.DefaultDialect;
            var entityList = entities as IReadOnlyList<T> ?? entities.ToList();
            if (entityList.Count == 0) return 0;

            var mapping = MappingCache.GetMapping<T>();
            var primaryKeys = mapping.PrimaryKeys ?? mapping.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (primaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key defined for merge/upsert.");

            var allCols = mapping.Columns.Where(c => !c.IsDbGenerated || c.IsPrimaryKey).ToList();
            var table = SqlGenerator.QuoteTableName(mapping.TableName, d);
            var quotedCols = allCols.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();
            var quotedPkCols = primaryKeys.Select(c => d.QuoteIdentifier(c.ColumnName)).ToList();

            var rowsPerChunk = Math.Max(1, MaxParametersPerCommand / allCols.Count);
            int totalAffected = 0;

            foreach (var chunk in ChunkFast(entityList, rowsPerChunk))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parameters = new DynamicParameters();
                var parameterRows = new List<IReadOnlyList<string>>(chunk.Count);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramNames = new List<string>(allCols.Count);
                    for (int j = 0; j < allCols.Count; j++)
                    {
                        var col = allCols[j];
                        var paramName = $"@p{i}_{col.Property.Name}";
                        var val = col.Getter != null ? col.Getter(chunk[i]) : col.Property.GetValue(chunk[i]);
                        parameters.Add(paramName, val);
                        paramNames.Add(paramName);
                    }
                    parameterRows.Add(paramNames);
                }

                var sql = d.GenerateBulkMergeSql(table, quotedCols, quotedPkCols, parameterRows);
                totalAffected += await connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                    transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            return totalAffected;
        }

        private static IEnumerable<List<T>> ChunkFast<T>(IReadOnlyList<T> source, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            for (int i = 0; i < source.Count; i += size)
            {
                int count = Math.Min(size, source.Count - i);
                var list = new List<T>(count);
                for (int j = 0; j < count; j++)
                {
                    list.Add(source[i + j]);
                }
                yield return list;
            }
        }

        private static bool TryGetSqlConnection(IDbConnection connection, out Microsoft.Data.SqlClient.SqlConnection sqlConn)
        {
            if (connection is Microsoft.Data.SqlClient.SqlConnection direct)
            {
                sqlConn = direct;
                return true;
            }
            sqlConn = null;
            return false;
        }

        private static bool TrySqlBulkCopy<T>(
            Microsoft.Data.SqlClient.SqlConnection sqlConn,
            IReadOnlyList<T> entities,
            EntityMapping mapping,
            IReadOnlyList<ColumnMapping> insertColumns,
            IDbTransaction transaction,
            out int affected) where T : class
        {
            try
            {
                var sqlTx = transaction as Microsoft.Data.SqlClient.SqlTransaction;
                using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.CheckConstraints | Microsoft.Data.SqlClient.SqlBulkCopyOptions.FireTriggers, sqlTx))
                {
                    bulkCopy.DestinationTableName = mapping.TableName;
                    for (int i = 0; i < insertColumns.Count; i++)
                    {
                        var col = insertColumns[i];
                        bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }

                    using (var reader = new EntityDataReader<T>(entities, insertColumns))
                    {
                        bulkCopy.WriteToServer(reader);
                    }
                    affected = entities.Count;
                    return true;
                }
            }
            catch
            {
                affected = 0;
                return false;
            }
        }

        private static async Task<(bool Success, int Affected)> TrySqlBulkCopyAsync<T>(
            Microsoft.Data.SqlClient.SqlConnection sqlConn,
            IReadOnlyList<T> entities,
            EntityMapping mapping,
            IReadOnlyList<ColumnMapping> insertColumns,
            IDbTransaction transaction,
            CancellationToken ct) where T : class
        {
            try
            {
                var sqlTx = transaction as Microsoft.Data.SqlClient.SqlTransaction;
                using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.CheckConstraints | Microsoft.Data.SqlClient.SqlBulkCopyOptions.FireTriggers, sqlTx))
                {
                    bulkCopy.DestinationTableName = mapping.TableName;
                    for (int i = 0; i < insertColumns.Count; i++)
                    {
                        var col = insertColumns[i];
                        bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }

                    using (var reader = new EntityDataReader<T>(entities, insertColumns))
                    {
                        await bulkCopy.WriteToServerAsync(reader, ct).ConfigureAwait(false);
                    }
                    return (true, entities.Count);
                }
            }
            catch
            {
                return (false, 0);
            }
        }

        private static bool TrySqlBulkCopyDataFrame(
            Microsoft.Data.SqlClient.SqlConnection sqlConn,
            DataFrame dataFrame,
            string tableName,
            IDbTransaction transaction,
            out int affected)
        {
            try
            {
                var sqlTx = transaction as Microsoft.Data.SqlClient.SqlTransaction;
                using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.CheckConstraints | Microsoft.Data.SqlClient.SqlBulkCopyOptions.FireTriggers, sqlTx))
                {
                    bulkCopy.DestinationTableName = tableName;
                    foreach (var colName in dataFrame.ColumnNames)
                    {
                        bulkCopy.ColumnMappings.Add(colName, colName);
                    }

                    var dt = dataFrame.ToDataTable(tableName);
                    bulkCopy.WriteToServer(dt);
                    affected = dataFrame.RowCount;
                    return true;
                }
            }
            catch
            {
                affected = 0;
                return false;
            }
        }

        private static async Task<(bool Success, int Affected)> TrySqlBulkCopyDataFrameAsync(
            Microsoft.Data.SqlClient.SqlConnection sqlConn,
            DataFrame dataFrame,
            string tableName,
            IDbTransaction transaction,
            CancellationToken ct)
        {
            try
            {
                var sqlTx = transaction as Microsoft.Data.SqlClient.SqlTransaction;
                using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.CheckConstraints | Microsoft.Data.SqlClient.SqlBulkCopyOptions.FireTriggers, sqlTx))
                {
                    bulkCopy.DestinationTableName = tableName;
                    foreach (var colName in dataFrame.ColumnNames)
                    {
                        bulkCopy.ColumnMappings.Add(colName, colName);
                    }

                    var dt = dataFrame.ToDataTable(tableName);
                    await bulkCopy.WriteToServerAsync(dt, ct).ConfigureAwait(false);
                    return (true, dataFrame.RowCount);
                }
            }
            catch
            {
                return (false, 0);
            }
        }

        private sealed class EntityDataReader<T> : IDataReader where T : class
        {
            private readonly IReadOnlyList<T> _entities;
            private readonly IReadOnlyList<ColumnMapping> _columns;
            private int _index = -1;

            public EntityDataReader(IReadOnlyList<T> entities, IReadOnlyList<ColumnMapping> columns)
            {
                _entities = entities;
                _columns = columns;
            }

            public int FieldCount => _columns.Count;
            public bool Read() => ++_index < _entities.Count;
            public string GetName(int i) => _columns[i].ColumnName;
            public int GetOrdinal(string name)
            {
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (string.Equals(_columns[i].ColumnName, name, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return -1;
            }

            public object GetValue(int i)
            {
                var col = _columns[i];
                var item = _entities[_index];
                var val = col.Getter != null ? col.Getter(item) : col.Property.GetValue(item);
                return val ?? DBNull.Value;
            }

            public Type GetFieldType(int i) => _columns[i].Property.PropertyType;
            public bool IsDBNull(int i) => GetValue(i) == DBNull.Value;

            public object this[int i] => GetValue(i);
            public object this[string name] => GetValue(GetOrdinal(name));

            public void Dispose() { }
            public void Close() { }
            public DataTable GetSchemaTable() => null;
            public bool NextResult() => false;
            public int Depth => 0;
            public bool IsClosed => false;
            public int RecordsAffected => _entities.Count;

            public bool GetBoolean(int i) => (bool)GetValue(i);
            public byte GetByte(int i) => (byte)GetValue(i);
            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => 0;
            public char GetChar(int i) => (char)GetValue(i);
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => 0;
            public IDataReader GetData(int i) => null;
            public string GetDataTypeName(int i) => GetFieldType(i).Name;
            public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
            public decimal GetDecimal(int i) => (decimal)GetValue(i);
            public double GetDouble(int i) => (double)GetValue(i);
            public float GetFloat(int i) => (float)GetValue(i);
            public Guid GetGuid(int i) => (Guid)GetValue(i);
            public short GetInt16(int i) => (short)GetValue(i);
            public int GetInt32(int i) => (int)GetValue(i);
            public long GetInt64(int i) => (long)GetValue(i);
            public string GetString(int i) => (string)GetValue(i);
            public int GetValues(object[] values)
            {
                int count = Math.Min(values.Length, _columns.Count);
                for (int i = 0; i < count; i++) values[i] = GetValue(i);
                return count;
            }
        }
    }
}
