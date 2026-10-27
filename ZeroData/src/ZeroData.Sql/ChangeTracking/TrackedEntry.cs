using System;

namespace ZeroData.Sql.ChangeTracking
{
    /// <summary>
    /// Represents a tracked entity with its current state.
    /// Used by ChangeTracker.Entries&lt;T&gt;() to expose tracking information.
    /// </summary>
    public class TrackedEntry<T> where T : class
    {
        public T Entity { get; }
        public EntityState State { get; }

        public TrackedEntry(T entity, EntityState state)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            State = state;
        }
    }
}
