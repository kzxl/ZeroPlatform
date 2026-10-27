using System;

namespace ZeroData.Sql.Mapping
{
    /// <summary>
    /// Marks an entity as supporting soft deletion.
    /// When applied, Delete operations generate UPDATE SET {Column}=1 instead of DELETE.
    /// The specified column should be a boolean/int column (e.g., IsDeleted).
    /// Combine with db.Filters.Add&lt;T&gt;(x => x.IsDeleted == false) for auto-filtering.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SoftDeleteAttribute : Attribute
    {
        /// <summary>
        /// The property name used for soft delete flag (default: "IsDeleted").
        /// </summary>
        public string ColumnName { get; set; } = "IsDeleted";
    }
}
