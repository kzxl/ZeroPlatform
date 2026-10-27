using System;
using System.Collections.Generic;
using ZeroData.Sql.Mapping;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Partial implementation of DynamicDatabaseQueryTool providing seamless integration with
    /// ZeroData.Sql EntityMapping and MappingCache metadata for automated schema registration.
    /// </summary>
    public static partial class DynamicDatabaseQueryTool
    {
        /// <summary>
        /// Registers a strongly typed entity class from ZeroData.Sql into the Agent database catalog,
        /// automatically discovering table names, column types, primary keys, and foreign key associations.
        /// </summary>
        public static void RegisterEntity<T>(string description = "") where T : class
        {
            RegisterEntity(typeof(T), description);
        }

        /// <summary>
        /// Registers an entity type using ZeroData.Sql MappingCache into the Agent database catalog.
        /// </summary>
        public static void RegisterEntity(Type entityType, string description = "")
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            var mapping = MappingCache.GetMapping(entityType);
            var tableMeta = new TableMetadata(
                mapping.TableName,
                string.IsNullOrWhiteSpace(description) ? $"ZeroData.Sql Entity: {entityType.Name}" : description
            );

            // 1. Map columns
            foreach (var col in mapping.Columns)
            {
                tableMeta.Columns.Add(new ColumnMetadata(
                    col.ColumnName,
                    col.Property.PropertyType.Name,
                    isPrimaryKey: col.IsPrimaryKey,
                    canBeNull: col.CanBeNull,
                    description: col.IsVersion ? "Optimistic Concurrency Version" : null
                ));
            }

            // 2. Map FK associations
            if (mapping.Associations != null)
            {
                foreach (var assoc in mapping.Associations)
                {
                    try
                    {
                        var targetMapping = MappingCache.GetMapping(assoc.OtherType);
                        tableMeta.ForeignKeys.Add(new ForeignKeyMetadata(
                            assoc.ThisKey,
                            targetMapping.TableName,
                            assoc.OtherKey
                        ));
                    }
                    catch
                    {
                        // Fallback if related entity type mapping cannot be automatically resolved
                        tableMeta.ForeignKeys.Add(new ForeignKeyMetadata(
                            assoc.ThisKey,
                            assoc.OtherType.Name,
                            assoc.OtherKey
                        ));
                    }
                }
            }

            _catalog[tableMeta.TableName] = tableMeta;
        }

        /// <summary>
        /// Batch registers multiple entity types into the Agent database catalog.
        /// </summary>
        public static void RegisterEntities(params Type[] entityTypes)
        {
            if (entityTypes == null) return;
            foreach (var type in entityTypes)
            {
                if (type != null) RegisterEntity(type);
            }
        }
    }
}
