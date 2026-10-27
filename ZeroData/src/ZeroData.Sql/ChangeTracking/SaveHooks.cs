using System;
using System.Collections.Generic;

namespace ZeroData.Sql.ChangeTracking
{
    /// <summary>
    /// Provides BeforeSave/AfterSave hooks for entity lifecycle events.
    /// Register handlers per entity type to auto-fill audit fields, validate, or log.
    /// </summary>
    public class SaveHooks
    {
        private readonly Dictionary<Type, List<Action<object, EntityState>>> _beforeSave
            = new Dictionary<Type, List<Action<object, EntityState>>>();

        private readonly Dictionary<Type, List<Action<object, EntityState>>> _afterSave
            = new Dictionary<Type, List<Action<object, EntityState>>>();

        /// <summary>
        /// Registers a handler called before an entity is saved (insert, update, delete).
        /// </summary>
        public void OnBeforeSave<T>(Action<T, EntityState> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);
            if (!_beforeSave.ContainsKey(type))
                _beforeSave[type] = new List<Action<object, EntityState>>();
            _beforeSave[type].Add((entity, state) => handler((T)entity, state));
        }

        /// <summary>
        /// Registers a handler called after an entity is saved.
        /// </summary>
        public void OnAfterSave<T>(Action<T, EntityState> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);
            if (!_afterSave.ContainsKey(type))
                _afterSave[type] = new List<Action<object, EntityState>>();
            _afterSave[type].Add((entity, state) => handler((T)entity, state));
        }

        /// <summary>
        /// Invokes all BeforeSave hooks for the given entity type and state.
        /// </summary>
        internal void InvokeBeforeSave(object entity, Type entityType, EntityState state)
        {
            if (_beforeSave.TryGetValue(entityType, out var handlers))
            {
                foreach (var handler in handlers)
                    handler(entity, state);
            }
        }

        /// <summary>
        /// Invokes all AfterSave hooks for the given entity type and state.
        /// </summary>
        internal void InvokeAfterSave(object entity, Type entityType, EntityState state)
        {
            if (_afterSave.TryGetValue(entityType, out var handlers))
            {
                foreach (var handler in handlers)
                    handler(entity, state);
            }
        }

        /// <summary>
        /// Returns true if any hooks are registered.
        /// </summary>
        internal bool HasHooks => _beforeSave.Count > 0 || _afterSave.Count > 0;
    }
}
