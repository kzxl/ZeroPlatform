using System;

namespace ZeroData.Sql.Mapping
{
    /// <summary>
    /// Maps a class to a database table.
    /// Compatible with System.Data.Linq.Mapping.TableAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class TableAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the table in the database.
        /// If not specified, the class name is used.
        /// </summary>
        public string Name { get; set; }
    }
}
