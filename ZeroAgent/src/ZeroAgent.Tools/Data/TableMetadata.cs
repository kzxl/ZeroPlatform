using System;
using System.Collections.Generic;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Metadata descriptor for a foreign key association between tables in the catalog.
    /// </summary>
    public sealed class ForeignKeyMetadata
    {
        public string FromColumn { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string ToColumn { get; set; } = string.Empty;

        public ForeignKeyMetadata() { }

        public ForeignKeyMetadata(string fromColumn, string toTable, string toColumn)
        {
            FromColumn = fromColumn;
            ToTable = toTable;
            ToColumn = toColumn;
        }
    }

    /// <summary>
    /// Metadata descriptor for a column in the database/DataFrame catalog.
    /// </summary>
    public sealed class ColumnMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; }
        public bool IsVector { get; set; }
        public int VectorDimension { get; set; }
        public bool CanBeNull { get; set; } = true;
        public string? Description { get; set; }

        public ColumnMetadata() { }

        public ColumnMetadata(
            string name,
            string dataType,
            bool isPrimaryKey = false,
            bool isVector = false,
            int vectorDimension = 0,
            bool canBeNull = true,
            string? description = null)
        {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
            IsVector = isVector;
            VectorDimension = vectorDimension;
            CanBeNull = canBeNull;
            Description = description;
        }
    }

    /// <summary>
    /// Metadata descriptor for a table in the database/DataFrame catalog.
    /// </summary>
    public sealed class TableMetadata
    {
        public string TableName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ColumnMetadata> Columns { get; set; } = new List<ColumnMetadata>();
        public List<ForeignKeyMetadata> ForeignKeys { get; set; } = new List<ForeignKeyMetadata>();
        public int EstimatedRowCount { get; set; }

        public TableMetadata() { }

        public TableMetadata(string tableName, string description, int estimatedRowCount = 0)
        {
            TableName = tableName;
            Description = description;
            EstimatedRowCount = estimatedRowCount;
        }
    }
}
