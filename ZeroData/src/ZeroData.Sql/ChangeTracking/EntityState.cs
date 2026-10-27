namespace ZeroData.Sql.ChangeTracking
{
    /// <summary>
    /// Represents the state of a tracked entity.
    /// </summary>
    public enum EntityState
    {
        /// <summary>Entity has not been modified since being loaded.</summary>
        Unchanged,
        /// <summary>Entity is pending insertion into the database.</summary>
        Insert,
        /// <summary>Entity has been modified and is pending update.</summary>
        Update,
        /// <summary>Entity is pending deletion from the database.</summary>
        Delete
    }
}
