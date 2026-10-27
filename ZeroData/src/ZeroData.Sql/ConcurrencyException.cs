using System;

namespace ZeroData.Sql
{
    /// <summary>
    /// Thrown when an optimistic concurrency conflict is detected.
    /// Indicates that the entity was modified by another process since it was loaded.
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public object Entity { get; }
        public Type EntityType { get; }

        public ConcurrencyException(object entity, Type entityType)
            : base($"Concurrency conflict detected for {entityType.Name}. " +
                   "The entity has been modified by another process since it was loaded.")
        {
            Entity = entity;
            EntityType = entityType;
        }
    }
}
