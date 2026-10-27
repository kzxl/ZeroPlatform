using ZeroData.Sql.Mapping;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ZeroData.Sql.ChangeTracking
{
    /// <summary>
    /// Tracks pending Insert, Delete, and Update operations for SubmitChanges().
    /// Update tracking uses snapshot-based comparison: original values are captured
    /// when entities are first loaded, and compared at SubmitChanges() time.
    /// Uses compiled expression delegates for high performance property access (OPT-1).
    /// </summary>
    public class ChangeTracker
    {
        private readonly List<TrackedEntity> _trackedEntities = new List<TrackedEntity>();
        private readonly Dictionary<object, Dictionary<string, object>> _originalValues
            = new Dictionary<object, Dictionary<string, object>>(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Compiled getter cache: avoids slow PropertyInfo.GetValue() reflection.
        /// Each PropertyInfo is compiled once into a Func&lt;object, object&gt; delegate.
        /// ~3-5x faster than reflection for repeated property access.
        /// </summary>
        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> _getterCache
            = new ConcurrentDictionary<PropertyInfo, Func<object, object>>();

        private static Func<object, object> GetCompiledGetter(PropertyInfo prop)
        {
            return _getterCache.GetOrAdd(prop, p =>
            {
                var param = Expression.Parameter(typeof(object));
                var cast = Expression.Convert(param, p.DeclaringType);
                var access = Expression.Property(cast, p);
                var box = Expression.Convert(access, typeof(object));
                return Expression.Lambda<Func<object, object>>(box, param).Compile();
            });
        }

        /// <summary>
        /// Marks an entity for insertion.
        /// </summary>
        public void TrackInsert<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existing != null)
            {
                if (existing.State == EntityState.Insert) return;
                _trackedEntities.Remove(existing);
            }
            _originalValues.Remove(entity);
            _trackedEntities.Add(new TrackedEntity(entity, typeof(T), EntityState.Insert));
        }

        /// <summary>
        /// Marks an entity for deletion. If the entity was newly added (state=Insert),
        /// deleting it cancels the pending insertion.
        /// </summary>
        public void TrackDelete<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existing != null)
            {
                if (existing.State == EntityState.Insert)
                {
                    // Canceling an uncommitted insert
                    _trackedEntities.Remove(existing);
                    _originalValues.Remove(entity);
                    return;
                }
                if (existing.State == EntityState.Delete) return;
                _trackedEntities.Remove(existing);
            }
            _trackedEntities.Add(new TrackedEntity(entity, typeof(T), EntityState.Delete));
        }

        /// <summary>
        /// Captures the original property values of a loaded entity for later change detection.
        /// Call this for each entity loaded from the database.
        /// </summary>
        public void TrackLoaded(object entity, EntityMapping mapping)
        {
            if (entity == null || _originalValues.ContainsKey(entity))
                return;

            var snapshot = new Dictionary<string, object>();
            foreach (var col in mapping.Columns)
            {
                snapshot[col.Property.Name] = GetCompiledGetter(col.Property)(entity);
            }
            _originalValues[entity] = snapshot;
        }

        /// <summary>
        /// Tracks an entity using another entity's values as the original snapshot.
        /// Used by Attach(entity, original) to set the baseline for change detection.
        /// </summary>
        public void TrackLoadedWithOriginal(object entity, object original, EntityMapping mapping)
        {
            if (entity == null) return;

            var snapshot = new Dictionary<string, object>();
            foreach (var col in mapping.Columns)
            {
                snapshot[col.Property.Name] = GetCompiledGetter(col.Property)(original);
            }
            _originalValues[entity] = snapshot;
        }

        /// <summary>
        /// Explicitly marks an entity as modified (for Attach with asModified=true or Update).
        /// </summary>
        public void TrackUpdate(object entity, Type entityType)
        {
            if (entity == null) return;

            var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existing != null)
            {
                if (existing.State == EntityState.Insert) return; // Keep as Insert so it creates the row
                if (existing.State == EntityState.Update) return; // Avoid duplicate update entry
                _trackedEntities.Remove(existing);
            }
            _trackedEntities.Add(new TrackedEntity(entity, entityType, EntityState.Update));
        }

        /// <summary>
        /// Detaches an entity from change tracking.
        /// </summary>
        public bool Detach(object entity)
        {
            if (entity == null) return false;
            bool removedFromPending = _trackedEntities.RemoveAll(e => ReferenceEquals(e.Entity, entity)) > 0;
            bool removedFromOriginal = _originalValues.Remove(entity);
            return removedFromPending || removedFromOriginal;
        }

        /// <summary>
        /// Detaches any tracked entity matching the specified primary key value.
        /// </summary>
        public void DetachByKey(EntityMapping mapping, object keyValue)
        {
            if (mapping == null || keyValue == null || mapping.PrimaryKeys.Count != 1)
                return;

            var pkProp = mapping.PrimaryKeys[0].Property;
            var getter = GetCompiledGetter(pkProp);

            var toRemovePending = _trackedEntities
                .Where(e => e.EntityType == mapping.EntityType && Equals(getter(e.Entity), keyValue))
                .ToList();

            foreach (var e in toRemovePending)
            {
                _trackedEntities.Remove(e);
            }

            var toRemoveOriginal = _originalValues.Keys
                .Where(e => e.GetType() == mapping.EntityType && Equals(getter(e), keyValue))
                .ToList();

            foreach (var e in toRemoveOriginal)
            {
                _originalValues.Remove(e);
            }
        }

        /// <summary>
        /// Detects modified entities by comparing current values against original snapshots.
        /// Returns tracked entities with state=Update, including ChangedProperties for dirty update.
        /// Skips entities already marked for Delete or Insert.
        /// </summary>
        public List<TrackedEntity> DetectChanges(EntityMapping mapping)
        {
            var updates = new List<TrackedEntity>();

            foreach (var kvp in _originalValues)
            {
                var entity = kvp.Key;
                var originalSnapshot = kvp.Value;

                if (entity.GetType() != mapping.EntityType)
                    continue;

                // Do not detect updates for entities that are already pending deletion or insertion
                var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
                if (existing != null && (existing.State == EntityState.Delete || existing.State == EntityState.Insert))
                    continue;

                var changedProps = new List<string>();

                foreach (var col in mapping.UpdatableColumns)
                {
                    var currentValue = GetCompiledGetter(col.Property)(entity);
                    var originalValue = originalSnapshot.ContainsKey(col.Property.Name)
                        ? originalSnapshot[col.Property.Name]
                        : null;

                    if (!Equals(currentValue, originalValue))
                    {
                        changedProps.Add(col.Property.Name);
                    }
                }

                if (changedProps.Count > 0)
                {
                    if (existing != null && existing.State == EntityState.Update)
                    {
                        if (existing.ChangedProperties == null || existing.ChangedProperties.Count == 0)
                            existing.ChangedProperties = changedProps;
                    }
                    else
                    {
                        updates.Add(new TrackedEntity(entity, mapping.EntityType, EntityState.Update)
                        {
                            ChangedProperties = changedProps
                        });
                    }
                }
            }

            return updates;
        }

        /// <summary>
        /// Detects all modified entities across all tracked types in a single linear pass (O(M)).
        /// Avoids N-table quadratic scans over the snapshot dictionary.
        /// </summary>
        public List<TrackedEntity> DetectAllChanges(Func<Type, EntityMapping> mappingLookup)
        {
            if (mappingLookup == null) throw new ArgumentNullException(nameof(mappingLookup));
            var updates = new List<TrackedEntity>();

            foreach (var kvp in _originalValues)
            {
                var entity = kvp.Key;
                var originalSnapshot = kvp.Value;
                var entityType = entity.GetType();

                // Do not detect updates for entities that are already pending deletion or insertion
                var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
                if (existing != null && (existing.State == EntityState.Delete || existing.State == EntityState.Insert))
                    continue;

                var mapping = mappingLookup(entityType);
                if (mapping == null) continue;

                var changedProps = new List<string>();

                foreach (var col in mapping.UpdatableColumns)
                {
                    var currentValue = col.Getter != null ? col.Getter(entity) : GetCompiledGetter(col.Property)(entity);
                    var originalValue = originalSnapshot.ContainsKey(col.Property.Name)
                        ? originalSnapshot[col.Property.Name]
                        : null;

                    if (!Equals(currentValue, originalValue))
                    {
                        changedProps.Add(col.Property.Name);
                    }
                }

                if (changedProps.Count > 0)
                {
                    if (existing != null && existing.State == EntityState.Update)
                    {
                        if (existing.ChangedProperties == null || existing.ChangedProperties.Count == 0)
                            existing.ChangedProperties = changedProps;
                    }
                    else
                    {
                        updates.Add(new TrackedEntity(entity, mapping.EntityType, EntityState.Update)
                        {
                            ChangedProperties = changedProps
                        });
                    }
                }
            }

            return updates;
        }

        /// <summary>
        /// Returns all pending changes (inserts, deletes, and detected updates).
        /// </summary>
        public IReadOnlyList<TrackedEntity> GetPendingChanges()
        {
            return _trackedEntities.Where(e =>
                e.State == EntityState.Insert ||
                e.State == EntityState.Delete ||
                e.State == EntityState.Update
            ).ToList().AsReadOnly();
        }

        /// <summary>
        /// Clears all tracked changes and refreshes snapshots after a successful SubmitChanges.
        /// Inserted entities become tracked with their new values.
        /// Updated entities refresh their baseline snapshots.
        /// Deleted entities are removed from tracking.
        /// </summary>
        public void AcceptChanges()
        {
            var deletedEntities = new HashSet<object>(
                _trackedEntities.Where(e => e.State == EntityState.Delete).Select(e => e.Entity),
                ReferenceEqualityComparer.Instance);

            var insertedEntities = _trackedEntities.Where(e => e.State == EntityState.Insert).ToList();

            // Remove deleted entities from original values
            foreach (var entity in deletedEntities)
            {
                _originalValues.Remove(entity);
            }

            // Refresh snapshots for remaining tracked original values
            foreach (var kvp in _originalValues.ToList())
            {
                var entity = kvp.Key;
                var snapshot = kvp.Value;
                var mapping = MappingCache.GetMapping(entity.GetType());
                if (mapping != null)
                {
                    foreach (var col in mapping.Columns)
                    {
                        snapshot[col.Property.Name] = GetCompiledGetter(col.Property)(entity);
                    }
                }
            }

            // Add newly inserted entities to original values baseline
            foreach (var tracked in insertedEntities)
            {
                var mapping = MappingCache.GetMapping(tracked.EntityType);
                if (mapping != null)
                {
                    TrackLoaded(tracked.Entity, mapping);
                }
            }

            _trackedEntities.Clear();
        }

        /// <summary>
        /// Returns true if there are any pending changes.
        /// </summary>
        public bool HasChanges => _trackedEntities.Any(e =>
            e.State == EntityState.Insert ||
            e.State == EntityState.Delete ||
            e.State == EntityState.Update);

        /// <summary>
        /// Adds detected updates to the pending changes list.
        /// Called by SqlContext before processing SubmitChanges.
        /// </summary>
        public void AddUpdates(IEnumerable<TrackedEntity> updates)
        {
            if (updates == null) return;
            foreach (var update in updates)
            {
                var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, update.Entity));
                if (existing != null)
                {
                    if (existing.State == EntityState.Delete || existing.State == EntityState.Insert)
                        continue;
                    _trackedEntities.Remove(existing);
                }
                _trackedEntities.Add(update);
            }
        }

        #region ChangeTracker API (Phase 12)

        /// <summary>
        /// Gets the tracked state of an entity.
        /// Returns Unchanged if the entity is tracked (loaded) but not modified.
        /// Throws if the entity is not tracked.
        /// </summary>
        public EntityState GetState(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Check pending changes first
            var tracked = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (tracked != null) return tracked.State;

            // If in original values but not in pending changes, it's unchanged
            if (_originalValues.ContainsKey(entity)) return EntityState.Unchanged;

            throw new InvalidOperationException(
                $"Entity of type '{entity.GetType().Name}' is not tracked by this ChangeTracker.");
        }

        /// <summary>
        /// Returns all tracked entities of type T with their current state.
        /// Includes entities in Insert, Update, Delete, and Unchanged states.
        /// </summary>
        public IReadOnlyList<TrackedEntry<T>> Entries<T>() where T : class
        {
            var result = new List<TrackedEntry<T>>();

            // Add pending changes
            foreach (var tracked in _trackedEntities)
            {
                if (tracked.Entity is T typedEntity)
                {
                    result.Add(new TrackedEntry<T>(typedEntity, tracked.State));
                }
            }

            // Add unchanged (loaded but not in pending changes)
            var pendingEntities = new HashSet<object>(
                _trackedEntities.Select(t => t.Entity), ReferenceEqualityComparer.Instance);

            foreach (var entity in _originalValues.Keys)
            {
                if (entity is T typedUnchanged && !pendingEntities.Contains(entity))
                {
                    result.Add(new TrackedEntry<T>(typedUnchanged, EntityState.Unchanged));
                }
            }

            return result.AsReadOnly();
        }

        /// <summary>
        /// Returns true if an entity is being tracked.
        /// </summary>
        public bool IsTracking(object entity)
        {
            if (entity == null) return false;
            return _originalValues.ContainsKey(entity) ||
                   _trackedEntities.Any(e => ReferenceEquals(e.Entity, entity));
        }

        /// <summary>
        /// Gets the original snapshot property values captured when the entity was first loaded or attached.
        /// Returns an empty dictionary if the entity is not tracked or has no snapshot.
        /// </summary>
        public IReadOnlyDictionary<string, object> GetOriginalValues(object entity)
        {
            if (entity != null && _originalValues.TryGetValue(entity, out var values))
            {
                return new Dictionary<string, object>(values);
            }
            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Updates the original snapshot of a tracked entity with fresh values (used after reload).
        /// </summary>
        public void UpdateSnapshot(object entity, EntityMapping mapping)
        {
            if (entity == null || mapping == null) return;
            var snapshot = new Dictionary<string, object>();
            foreach (var col in mapping.Columns)
            {
                snapshot[col.Property.Name] = GetCompiledGetter(col.Property)(entity);
            }
            _originalValues[entity] = snapshot;

            // Remove any pending Update state since entity is now synchronized
            var existing = _trackedEntities.FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (existing != null && existing.State == EntityState.Update)
            {
                _trackedEntities.Remove(existing);
            }
        }

        #endregion
    }

    /// <summary>
    /// Compares objects by reference identity (not Equals). Required for tracking
    /// different entity instances even if they have equal property values.
    /// </summary>
    internal class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        bool IEqualityComparer<object>.Equals(object x, object y) =>
            ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

