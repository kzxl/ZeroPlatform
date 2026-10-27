using System;
using ZeroData.Sql.Dialects;

namespace ZeroData.Sql.Migrations
{
    /// <summary>
    /// Base class for a schema migration. Implement <see cref="Up"/> (and optionally
    /// <see cref="Down"/>) using the supplied <see cref="SchemaBuilder"/>.
    /// The migration <see cref="Id"/> determines ordering and uniqueness in history.
    /// </summary>
    public abstract class Migration
    {
        /// <summary>
        /// Unique, sortable migration identifier. Convention: a zero-padded timestamp or sequence,
        /// e.g. "20260101_0001_CreateUsers". Defaults to the type name.
        /// </summary>
        public virtual string Id => GetType().Name;

        /// <summary>Applies the migration.</summary>
        public abstract void Up(SchemaBuilder schema);

        /// <summary>Reverts the migration. Override to enable rollback.</summary>
        public virtual void Down(SchemaBuilder schema)
        {
            throw new NotSupportedException($"Migration '{Id}' does not support Down().");
        }
    }
}
