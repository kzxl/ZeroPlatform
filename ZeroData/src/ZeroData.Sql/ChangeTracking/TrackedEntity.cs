using System;
using System.Collections.Generic;

namespace ZeroData.Sql.ChangeTracking
{
    /// <summary>
    /// Wraps an entity with its tracking state and type information.
    /// For Update state, ChangedProperties contains names of modified properties
    /// to enable dirty (partial) UPDATE SQL generation.
    /// </summary>
    public class TrackedEntity
    {
        public object Entity { get; }
        public Type EntityType { get; }
        public EntityState State { get; internal set; }

        /// <summary>
        /// Names of properties that changed (only for State == Update).
        /// Used by SqlGenerator.GeneratePartialUpdate() to generate SET only for changed columns.
        /// Null means all columns should be updated (full update).
        /// </summary>
        public IReadOnlyList<string> ChangedProperties { get; set; }

        public TrackedEntity(object entity, Type entityType, EntityState state)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            State = state;
        }
    }
}

