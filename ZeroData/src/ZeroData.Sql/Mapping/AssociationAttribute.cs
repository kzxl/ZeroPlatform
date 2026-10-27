using System;

namespace ZeroData.Sql.Mapping
{
    /// <summary>
    /// Designates a property to represent a database association (foreign key relationship).
    /// Compatible with System.Data.Linq.Mapping.AssociationAttribute.
    /// Note: Association navigation is not implemented in Phase 1, 
    /// but the attribute is provided for compatibility with L2S-generated model classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AssociationAttribute : Attribute
    {
        public string Name { get; set; }
        public string Storage { get; set; }
        public string ThisKey { get; set; }
        public string OtherKey { get; set; }
        public bool IsForeignKey { get; set; }
        public bool IsUnique { get; set; }
        public string DeleteRule { get; set; }
        public bool DeleteOnNull { get; set; }
    }
}
