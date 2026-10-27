using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroData.Sql.Mapping
{
    /// <summary>
    /// Represents the mapping metadata for a single column.
    /// </summary>
    public class ColumnMapping
    {
        public PropertyInfo Property { get; set; }
        public string ColumnName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsDbGenerated { get; set; }
        public bool CanBeNull { get; set; }
        public AutoSync AutoSync { get; set; }

        /// <summary>True if this column is a version/timestamp for optimistic concurrency.</summary>
        public bool IsVersion { get; set; }

        /// <summary>Compiled expression delegate for reading the property value with zero reflection.</summary>
        public Func<object, object> Getter { get; set; }

        /// <summary>Compiled expression delegate for setting the property value with zero reflection.</summary>
        public Action<object, object> Setter { get; set; }
    }

    /// <summary>
    /// Represents a FK association mapping.
    /// </summary>
    public class AssociationMapping
    {
        /// <summary>Navigation property on the entity (e.g. tbSYS_User_Leader)</summary>
        public PropertyInfo Property { get; set; }

        /// <summary>FK column name on this entity (e.g. idLeader)</summary>
        public string ThisKey { get; set; }

        /// <summary>PK column name on the related entity (e.g. id)</summary>
        public string OtherKey { get; set; }

        /// <summary>True if this side holds the FK</summary>
        public bool IsForeignKey { get; set; }

        /// <summary>CLR type of the related entity</summary>
        public Type OtherType { get; set; }
    }

    /// <summary>
    /// Represents the mapping metadata for an entity type,
    /// including table name, column mappings, and FK associations.
    /// </summary>
    public class EntityMapping
    {
        public Type EntityType { get; set; }
        public string TableName { get; set; }
        public IReadOnlyList<ColumnMapping> Columns { get; set; }
        public IReadOnlyList<ColumnMapping> PrimaryKeys { get; set; }
        public IReadOnlyList<ColumnMapping> InsertableColumns { get; set; }
        public IReadOnlyList<ColumnMapping> UpdatableColumns { get; set; }
        public IReadOnlyList<AssociationMapping> Associations { get; set; }

        /// <summary>
        /// Columns flagged with IsVersion=true, used for optimistic concurrency checks.
        /// </summary>
        public IReadOnlyList<ColumnMapping> VersionColumns { get; set; }

        /// <summary>
        /// One-to-many collection navigation (IsForeignKey=false).
        /// The OtherType is the child element type, ThisKey is PK on parent, OtherKey is FK on child.
        /// </summary>
        public IReadOnlyList<AssociationMapping> CollectionAssociations { get; set; }
    }

    /// <summary>
    /// Thread-safe cache for entity mapping metadata.
    /// Reads [Table] and [Column] attributes via reflection and caches results.
    /// </summary>
    public static class MappingCache
    {
        private static readonly ConcurrentDictionary<Type, EntityMapping> Cache
            = new ConcurrentDictionary<Type, EntityMapping>();

        /// <summary>
        /// Gets the mapping metadata for the specified entity type.
        /// Results are cached for subsequent calls.
        /// </summary>
        public static EntityMapping GetMapping<T>() where T : class
        {
            return GetMapping(typeof(T));
        }

        /// <summary>
        /// Gets the mapping metadata for the specified entity type.
        /// Results are cached for subsequent calls.
        /// </summary>
        public static EntityMapping GetMapping(Type entityType)
        {
            return Cache.GetOrAdd(entityType, BuildMapping);
        }

        private static EntityMapping BuildMapping(Type type)
        {
            // Resolve table name from [Table] attribute or class name
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            var tableName = tableAttr?.Name ?? type.Name;

            // Build column mappings from properties with [Column] attribute
            var allColumns = new List<ColumnMapping>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();

                // If entity has any [Column] attributes, only map attributed properties
                // If entity has NO [Column] attributes, map all public properties (convention-based)
                if (colAttr != null)
                {
                    allColumns.Add(new ColumnMapping
                    {
                        Property = prop,
                        ColumnName = colAttr.Name ?? prop.Name,
                        IsPrimaryKey = colAttr.IsPrimaryKey,
                        IsDbGenerated = colAttr.IsDbGenerated,
                        CanBeNull = colAttr.CanBeNull,
                        AutoSync = colAttr.AutoSync,
                        IsVersion = colAttr.IsVersion
                    });
                }
            }

            // Convention-based: if no [Column] attributes found, map all properties
            if (allColumns.Count == 0)
            {
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Skip navigation/association properties
                    if (prop.GetCustomAttribute<AssociationAttribute>() != null)
                        continue;

                    // Skip collection properties (likely navigation)
                    if (prop.PropertyType != typeof(string) &&
                        typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                        continue;

                    allColumns.Add(new ColumnMapping
                    {
                        Property = prop,
                        ColumnName = prop.Name,
                        IsPrimaryKey = false,
                        IsDbGenerated = false,
                        CanBeNull = !prop.PropertyType.IsValueType ||
                                    Nullable.GetUnderlyingType(prop.PropertyType) != null
                    });
                }

                // Convention PK detection: prefer "Id", then "{TypeName}Id" (case-insensitive).
                // An integral PK is assumed DB-generated (identity/auto-increment).
                var pkCol = allColumns.FirstOrDefault(c =>
                                string.Equals(c.Property.Name, "Id", StringComparison.OrdinalIgnoreCase))
                            ?? allColumns.FirstOrDefault(c =>
                                string.Equals(c.Property.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase));

                if (pkCol != null)
                {
                    pkCol.IsPrimaryKey = true;
                    var pkType = Nullable.GetUnderlyingType(pkCol.Property.PropertyType) ?? pkCol.Property.PropertyType;
                    if (pkType == typeof(int) || pkType == typeof(long) || pkType == typeof(short))
                        pkCol.IsDbGenerated = true;
                }
            }

            foreach (var col in allColumns)
            {
                try
                {
                    var param = Expression.Parameter(typeof(object), "entity");
                    var cast = Expression.Convert(param, type);
                    var propAccess = Expression.Property(cast, col.Property);
                    var box = Expression.Convert(propAccess, typeof(object));
                    col.Getter = Expression.Lambda<Func<object, object>>(box, param).Compile();
                }
                catch
                {
                    col.Getter = obj => col.Property.GetValue(obj);
                }

                if (col.Property.CanWrite)
                {
                    try
                    {
                        var targetParam = Expression.Parameter(typeof(object), "target");
                        var valParam = Expression.Parameter(typeof(object), "val");
                        var castTarget = Expression.Convert(targetParam, type);
                        var castVal = Expression.Convert(valParam, col.Property.PropertyType);
                        var assign = Expression.Assign(Expression.Property(castTarget, col.Property), castVal);
                        col.Setter = Expression.Lambda<Action<object, object>>(assign, targetParam, valParam).Compile();
                    }
                    catch
                    {
                        col.Setter = (target, val) => col.Property.SetValue(target, val);
                    }
                }
            }

            var primaryKeys = allColumns.Where(c => c.IsPrimaryKey).ToList();
            var insertable = allColumns.Where(c => !c.IsDbGenerated).ToList();
            var updatable = allColumns.Where(c => !c.IsPrimaryKey && !c.IsDbGenerated).ToList();
            var versionColumns = allColumns.Where(c => c.IsVersion).ToList();

            // Build association mappings from [Association] attributes
            var associations = new List<AssociationMapping>();
            var collectionAssociations = new List<AssociationMapping>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var assocAttr = prop.GetCustomAttribute<AssociationAttribute>();
                if (assocAttr != null && assocAttr.IsForeignKey)
                {
                    associations.Add(new AssociationMapping
                    {
                        Property = prop,
                        ThisKey = assocAttr.ThisKey,
                        OtherKey = assocAttr.OtherKey,
                        IsForeignKey = true,
                        OtherType = prop.PropertyType
                    });
                }
                else if (assocAttr != null && !assocAttr.IsForeignKey)
                {
                    // One-to-many collection navigation
                    var collectionType = prop.PropertyType;
                    Type elementType = null;

                    // Extract element type from List<T>, IList<T>, IEnumerable<T>, EntitySet<T>
                    if (collectionType.IsGenericType)
                    {
                        elementType = collectionType.GetGenericArguments()[0];
                    }

                    if (elementType != null)
                    {
                        collectionAssociations.Add(new AssociationMapping
                        {
                            Property = prop,
                            ThisKey = assocAttr.ThisKey,    // PK on parent
                            OtherKey = assocAttr.OtherKey,  // FK on child
                            IsForeignKey = false,
                            OtherType = elementType
                        });
                    }
                }
            }

            return new EntityMapping
            {
                EntityType = type,
                TableName = tableName,
                Columns = allColumns,
                PrimaryKeys = primaryKeys,
                InsertableColumns = insertable,
                UpdatableColumns = updatable,
                Associations = associations,
                VersionColumns = versionColumns,
                CollectionAssociations = collectionAssociations
            };
        }

        /// <summary>
        /// Clears the mapping cache. Useful for testing.
        /// </summary>
        public static void Clear()
        {
            Cache.Clear();
        }
    }
}
