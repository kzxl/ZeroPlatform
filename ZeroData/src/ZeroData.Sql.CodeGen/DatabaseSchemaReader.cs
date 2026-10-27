using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace ZeroData.Sql.CodeGen
{
    /// <summary>
    /// Reads database schema directly from SQL Server using INFORMATION_SCHEMA views.
    /// Returns a DbmlModel so the existing CodeGenerator can be reused.
    /// </summary>
    public class DatabaseSchemaReader
    {
        /// <summary>
        /// Connects to a database and reads all tables, columns, and FK relationships.
        /// Supports SQL Server, MySQL, PostgreSQL (via INFORMATION_SCHEMA) and SQLite (via PRAGMA).
        /// </summary>
        public static DbmlModel ReadSchema(IDbConnection connection, string contextClassName = null,
            DbProvider provider = DbProvider.SqlServer)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            if (provider == DbProvider.Sqlite)
                return ReadSchemaSqlite(connection, contextClassName);

            var dbName = connection.Database;
            var model = new DbmlModel
            {
                DatabaseName = dbName,
                ContextClassName = contextClassName ?? $"{SanitizeName(dbName)}DataContext"
            };

            var defaultSchema = provider == DbProvider.PostgreSql ? "public"
                : provider == DbProvider.MySql ? dbName
                : "dbo";

            // 1. Read all tables
            var tables = ReadTables(connection, provider, dbName);

            // 2. Read columns for each table
            foreach (var table in tables)
            {
                var parts = table.DbTableName.Split('.');
                var schemaName = parts.Length > 1 ? parts[0] : defaultSchema;
                var rawTableName = parts.Length > 1 ? parts[1] : parts[0];
                var columns = ReadColumns(connection, schemaName, rawTableName, provider);
                table.Columns.AddRange(columns);
                model.Tables.Add(table);
            }

            // 3. Read FK relationships
            var fks = ReadForeignKeys(connection, provider);
            foreach (var fk in fks)
            {
                // Add association to parent table (the one with PK)
                var parentTable = model.Tables.FirstOrDefault(t =>
                    t.DbTableName == $"{fk.PkSchema}.{fk.PkTable}" ||
                    t.TypeName == fk.PkTable);
                var childTable = model.Tables.FirstOrDefault(t =>
                    t.DbTableName == $"{fk.FkSchema}.{fk.FkTable}" ||
                    t.TypeName == fk.FkTable);

                if (parentTable != null && childTable != null)
                {
                    // FK side (child → parent): EntityRef
                    childTable.Associations.Add(new DbmlAssociation
                    {
                        Name = fk.ConstraintName,
                        MemberName = parentTable.TypeName,  // e.g. "tbINV_Warehouse"
                        ThisKey = fk.FkColumn,
                        OtherKey = fk.PkColumn,
                        OtherType = parentTable.TypeName,
                        IsForeignKey = true
                    });

                    // PK side (parent → children): EntitySet
                    parentTable.Associations.Add(new DbmlAssociation
                    {
                        Name = fk.ConstraintName,
                        MemberName = childTable.TypeName + "s",  // e.g. "tbINV_StockIns"
                        ThisKey = fk.PkColumn,
                        OtherKey = fk.FkColumn,
                        OtherType = childTable.TypeName,
                        IsForeignKey = false
                    });
                }
            }

            return model;
        }

        private static List<DbmlTable> ReadTables(IDbConnection connection, DbProvider provider, string dbName)
        {
            var tables = new List<DbmlTable>();
            using (var cmd = connection.CreateCommand())
            {
                if (provider == DbProvider.MySql)
                {
                    cmd.CommandText = @"
                        SELECT TABLE_SCHEMA, TABLE_NAME
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()
                        ORDER BY TABLE_NAME";
                }
                else if (provider == DbProvider.PostgreSql)
                {
                    cmd.CommandText = @"
                        SELECT TABLE_SCHEMA, TABLE_NAME
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_TYPE = 'BASE TABLE'
                          AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')
                        ORDER BY TABLE_SCHEMA, TABLE_NAME";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT TABLE_SCHEMA, TABLE_NAME 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_TYPE = 'BASE TABLE'
                        ORDER BY TABLE_SCHEMA, TABLE_NAME";
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var schema = reader.GetString(0);
                        var tableName = reader.GetString(1);
                        tables.Add(new DbmlTable
                        {
                            DbTableName = $"{schema}.{tableName}",
                            TypeName = tableName,
                            MemberName = tableName + "s"
                        });
                    }
                }
            }
            return tables;
        }

        private static List<DbmlColumn> ReadColumns(IDbConnection connection, string schema, string tableName, DbProvider provider)
        {
            var columns = new List<DbmlColumn>();
            var pkColumns = ReadPrimaryKeyColumns(connection, schema, tableName, provider);

            using (var cmd = connection.CreateCommand())
            {
                if (provider == DbProvider.MySql)
                {
                    cmd.CommandText = @"
                        SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                               CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                               CASE WHEN EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END AS IS_IDENTITY
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                        ORDER BY ORDINAL_POSITION";
                }
                else if (provider == DbProvider.PostgreSql)
                {
                    cmd.CommandText = @"
                        SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                               CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                               CASE WHEN is_identity = 'YES'
                                       OR column_default LIKE 'nextval%' THEN 1 ELSE 0 END AS IS_IDENTITY
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                        ORDER BY ORDINAL_POSITION";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT 
                            c.COLUMN_NAME,
                            c.DATA_TYPE,
                            c.IS_NULLABLE,
                            c.CHARACTER_MAXIMUM_LENGTH,
                            c.NUMERIC_PRECISION,
                            c.NUMERIC_SCALE,
                            COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') as IS_IDENTITY
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                        ORDER BY c.ORDINAL_POSITION";
                }

                AddParam(cmd, "@schema", schema);
                AddParam(cmd, "@table", tableName);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader.GetString(0);
                        var dataType = reader.GetString(1);
                        var isNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase);
                        var maxLength = reader.IsDBNull(3) ? (int?)null : Convert.ToInt32(reader.GetValue(3));
                        var isIdentity = !reader.IsDBNull(6) && Convert.ToInt32(reader.GetValue(6)) == 1;
                        var isPk = pkColumns.Contains(columnName);

                        var dbType = BuildDbType(dataType, maxLength, isNullable, isIdentity, isPk);

                        columns.Add(new DbmlColumn
                        {
                            Name = columnName,
                            DbType = dbType,
                            ClrType = MapSqlTypeToCLR(dataType, provider),
                            IsPrimaryKey = isPk,
                            IsDbGenerated = isIdentity,
                            CanBeNull = isNullable
                        });
                    }
                }
            }
            return columns;
        }

        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        private static HashSet<string> ReadPrimaryKeyColumns(IDbConnection connection, string schema, string tableName, DbProvider provider)
        {
            var pks = new HashSet<string>();
            using (var cmd = connection.CreateCommand())
            {
                if (provider == DbProvider.PostgreSql || provider == DbProvider.MySql)
                {
                    // Standard INFORMATION_SCHEMA join via KEY_COLUMN_USAGE (portable).
                    cmd.CommandText = @"
                        SELECT kcu.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                           AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                           AND tc.TABLE_NAME = kcu.TABLE_NAME
                        WHERE tc.TABLE_SCHEMA = @schema
                          AND tc.TABLE_NAME = @table
                          AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT cu.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu
                            ON tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                        WHERE tc.TABLE_SCHEMA = @schema
                          AND tc.TABLE_NAME = @table
                          AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'";
                }

                AddParam(cmd, "@schema", schema);
                AddParam(cmd, "@table", tableName);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        pks.Add(reader.GetString(0));
                }
            }
            return pks;
        }

        private static List<ForeignKeyInfo> ReadForeignKeys(IDbConnection connection, DbProvider provider)
        {
            var fks = new List<ForeignKeyInfo>();
            using (var cmd = connection.CreateCommand())
            {
                if (provider == DbProvider.MySql)
                {
                    cmd.CommandText = @"
                        SELECT
                            kcu.CONSTRAINT_NAME,
                            kcu.REFERENCED_TABLE_SCHEMA AS PkSchema,
                            kcu.REFERENCED_TABLE_NAME   AS PkTable,
                            kcu.REFERENCED_COLUMN_NAME  AS PkColumn,
                            kcu.TABLE_SCHEMA            AS FkSchema,
                            kcu.TABLE_NAME             AS FkTable,
                            kcu.COLUMN_NAME            AS FkColumn
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                        WHERE kcu.REFERENCED_TABLE_NAME IS NOT NULL
                          AND kcu.TABLE_SCHEMA = DATABASE()
                        ORDER BY kcu.CONSTRAINT_NAME";
                }
                else if (provider == DbProvider.PostgreSql)
                {
                    cmd.CommandText = @"
                        SELECT
                            tc.CONSTRAINT_NAME,
                            ccu.TABLE_SCHEMA AS PkSchema,
                            ccu.TABLE_NAME   AS PkTable,
                            ccu.COLUMN_NAME  AS PkColumn,
                            tc.TABLE_SCHEMA  AS FkSchema,
                            tc.TABLE_NAME    AS FkTable,
                            kcu.COLUMN_NAME  AS FkColumn
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                           AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                        JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu
                            ON ccu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                        ORDER BY tc.CONSTRAINT_NAME";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT
                            fk.name AS ConstraintName,
                            SCHEMA_NAME(tp.schema_id) AS PkSchema,
                            tp.name AS PkTable,
                            cp.name AS PkColumn,
                            SCHEMA_NAME(tr.schema_id) AS FkSchema,
                            tr.name AS FkTable,
                            cr.name AS FkColumn
                        FROM sys.foreign_keys fk
                        JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        JOIN sys.tables tp ON fkc.referenced_object_id = tp.object_id
                        JOIN sys.columns cp ON fkc.referenced_object_id = cp.object_id AND fkc.referenced_column_id = cp.column_id
                        JOIN sys.tables tr ON fkc.parent_object_id = tr.object_id
                        JOIN sys.columns cr ON fkc.parent_object_id = cr.object_id AND fkc.parent_column_id = cr.column_id
                        ORDER BY fk.name";
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        fks.Add(new ForeignKeyInfo
                        {
                            ConstraintName = reader.GetString(0),
                            PkSchema = reader.GetString(1),
                            PkTable = reader.GetString(2),
                            PkColumn = reader.GetString(3),
                            FkSchema = reader.GetString(4),
                            FkTable = reader.GetString(5),
                            FkColumn = reader.GetString(6)
                        });
                    }
                }
            }
            return fks;
        }

        #region SQLite schema reading (PRAGMA-based)

        private static DbmlModel ReadSchemaSqlite(IDbConnection connection, string contextClassName)
        {
            var model = new DbmlModel
            {
                DatabaseName = "main",
                ContextClassName = contextClassName ?? "AppDataContext"
            };

            var tableNames = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) tableNames.Add(reader.GetString(0));
            }

            foreach (var tableName in tableNames)
            {
                var table = new DbmlTable
                {
                    DbTableName = tableName,
                    TypeName = tableName,
                    MemberName = tableName + "s"
                };

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\")";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // cols: cid, name, type, notnull, dflt_value, pk
                            var colName = reader.GetString(1);
                            var dataType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
                            var notNull = Convert.ToInt32(reader.GetValue(3)) == 1;
                            var isPk = Convert.ToInt32(reader.GetValue(5)) > 0;
                            var clr = MapSqliteTypeToCLR(dataType);
                            // SQLite INTEGER PRIMARY KEY is an alias for rowid → auto-generated.
                            var isIdentity = isPk && dataType.ToUpperInvariant().Contains("INT");

                            table.Columns.Add(new DbmlColumn
                            {
                                Name = colName,
                                DbType = dataType + (notNull ? " NOT NULL" : "") + (isIdentity ? " IDENTITY" : ""),
                                ClrType = clr,
                                IsPrimaryKey = isPk,
                                IsDbGenerated = isIdentity,
                                CanBeNull = !notNull
                            });
                        }
                    }
                }

                model.Tables.Add(table);
            }

            // Foreign keys via PRAGMA foreign_key_list
            foreach (var childTable in model.Tables)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA foreign_key_list(\"{childTable.TypeName.Replace("\"", "\"\"")}\")";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // cols: id, seq, table, from, to, on_update, on_delete, match
                            var pkTableName = reader.GetString(2);
                            var fkColumn = reader.GetString(3);
                            var pkColumn = reader.IsDBNull(4) ? "Id" : reader.GetString(4);

                            var parentTable = model.Tables.FirstOrDefault(t => t.TypeName == pkTableName);
                            if (parentTable == null) continue;

                            childTable.Associations.Add(new DbmlAssociation
                            {
                                Name = $"FK_{childTable.TypeName}_{pkTableName}",
                                MemberName = parentTable.TypeName,
                                ThisKey = fkColumn,
                                OtherKey = pkColumn,
                                OtherType = parentTable.TypeName,
                                IsForeignKey = true
                            });

                            parentTable.Associations.Add(new DbmlAssociation
                            {
                                Name = $"FK_{childTable.TypeName}_{pkTableName}",
                                MemberName = childTable.TypeName + "s",
                                ThisKey = pkColumn,
                                OtherKey = fkColumn,
                                OtherType = childTable.TypeName,
                                IsForeignKey = false
                            });
                        }
                    }
                }
            }

            return model;
        }

        private static string MapSqliteTypeToCLR(string sqliteType)
        {
            var t = (sqliteType ?? "").ToUpperInvariant();
            if (t.Contains("INT")) return "System.Int64";
            if (t.Contains("CHAR") || t.Contains("CLOB") || t.Contains("TEXT")) return "System.String";
            if (t.Contains("REAL") || t.Contains("FLOA") || t.Contains("DOUB")) return "System.Double";
            if (t.Contains("NUMERIC") || t.Contains("DECIMAL")) return "System.Decimal";
            if (t.Contains("BLOB")) return "System.Byte[]";
            if (t.Contains("DATE") || t.Contains("TIME")) return "System.DateTime";
            if (t.Contains("BOOL")) return "System.Boolean";
            return "System.String";
        }

        #endregion

        #region Type Mapping

        private static string MapSqlTypeToCLR(string sqlType, DbProvider provider)
        {
            var t = sqlType.ToLowerInvariant();

            if (provider == DbProvider.MySql)
            {
                switch (t)
                {
                    case "tinyint": return "System.Boolean"; // MySQL bool is TINYINT(1)
                    case "smallint": return "System.Int16";
                    case "mediumint":
                    case "int":
                    case "integer": return "System.Int32";
                    case "bigint": return "System.Int64";
                    case "decimal":
                    case "numeric": return "System.Decimal";
                    case "float": return "System.Single";
                    case "double": return "System.Double";
                    case "bit": return "System.Boolean";
                    case "date":
                    case "datetime":
                    case "timestamp": return "System.DateTime";
                    case "time": return "System.TimeSpan";
                    case "char":
                    case "varchar":
                    case "text":
                    case "tinytext":
                    case "mediumtext":
                    case "longtext":
                    case "json": return "System.String";
                    case "binary":
                    case "varbinary":
                    case "blob":
                    case "tinyblob":
                    case "mediumblob":
                    case "longblob": return "System.Byte[]";
                    default: return "System.String";
                }
            }

            if (provider == DbProvider.PostgreSql)
            {
                switch (t)
                {
                    case "smallint":
                    case "int2": return "System.Int16";
                    case "integer":
                    case "int":
                    case "int4": return "System.Int32";
                    case "bigint":
                    case "int8": return "System.Int64";
                    case "numeric":
                    case "decimal":
                    case "money": return "System.Decimal";
                    case "real":
                    case "float4": return "System.Single";
                    case "double precision":
                    case "float8": return "System.Double";
                    case "boolean":
                    case "bool": return "System.Boolean";
                    case "date":
                    case "timestamp":
                    case "timestamp without time zone":
                    case "timestamp with time zone": return "System.DateTime";
                    case "time":
                    case "interval": return "System.TimeSpan";
                    case "uuid": return "System.Guid";
                    case "bytea": return "System.Byte[]";
                    default: return "System.String";
                }
            }

            // SQL Server
            switch (t)
            {
                case "bigint": return "System.Int64";
                case "int": return "System.Int32";
                case "smallint": return "System.Int16";
                case "tinyint": return "System.Byte";
                case "bit": return "System.Boolean";
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": return "System.Decimal";
                case "float": return "System.Double";
                case "real": return "System.Single";
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                case "date": return "System.DateTime";
                case "datetimeoffset": return "System.DateTimeOffset";
                case "time": return "System.TimeSpan";
                case "uniqueidentifier": return "System.Guid";
                case "varbinary":
                case "binary":
                case "image":
                case "timestamp": return "System.Data.Linq.Binary";
                default: return "System.String";
            }
        }

        private static string BuildDbType(string dataType, int? maxLength, bool isNullable, bool isIdentity, bool isPk)
        {
            var parts = new List<string>();

            // Type name
            var typeName = dataType.ToUpper();
            switch (dataType.ToLower())
            {
                case "nvarchar":
                case "varchar":
                case "nchar":
                case "char":
                    typeName = maxLength == -1
                        ? $"{typeName}(MAX)"
                        : $"{typeName}({maxLength})";
                    break;
                case "varbinary":
                case "binary":
                    typeName = maxLength == -1
                        ? $"{typeName}(MAX)"
                        : $"{typeName}({maxLength})";
                    break;
            }

            parts.Add(typeName);

            if (!isNullable) parts.Add("NOT NULL");
            if (isIdentity) parts.Add("IDENTITY");

            return string.Join(" ", parts);
        }

        private static string SanitizeName(string name)
        {
            return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        }

        #endregion

        private class ForeignKeyInfo
        {
            public string ConstraintName { get; set; }
            public string PkSchema { get; set; }
            public string PkTable { get; set; }
            public string PkColumn { get; set; }
            public string FkSchema { get; set; }
            public string FkTable { get; set; }
            public string FkColumn { get; set; }
        }
    }

    // Extension to parse schema.table
    internal static class DbmlTableExtensions
    {
        public static string SchemaName(this DbmlTable table)
        {
            var parts = table.DbTableName.Split('.');
            return parts.Length > 1 ? parts[0] : "dbo";
        }
    }
}
