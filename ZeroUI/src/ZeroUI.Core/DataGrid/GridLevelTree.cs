using System;
using System.Collections.Generic;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.DataGrid
{
    /// <summary>
    /// Virtual data source contract supporting master-detail hierarchical relations.
    /// Enables in-place detail rendering without creating nested child control HWNDs.
    /// </summary>
    public interface IMasterDetailVirtualSource : IZeroVirtualSource
    {
        /// <summary>
        /// Checks whether the specified master row contains child detail records.
        /// </summary>
        bool HasDetails(int masterModelRow, string relationName);

        /// <summary>
        /// Retrieves the virtual data source representing detail records for the master row.
        /// </summary>
        IZeroVirtualSource? GetDetailSource(int masterModelRow, string relationName);
    }

    /// <summary>
    /// Represents a hierarchical relation node within a master-detail grid structure.
    /// </summary>
    public sealed class GridLevelNode
    {
        /// <summary>
        /// Gets or sets the name of the relation linking master and detail records.
        /// </summary>
        public string RelationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the caption displayed on the detail tab or expander.
        /// </summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional tag or template descriptor associated with this level.
        /// </summary>
        public object? LevelTemplate { get; set; }

        /// <summary>
        /// Child relationship nodes for multi-tier hierarchical trees.
        /// </summary>
        public List<GridLevelNode> Nodes { get; } = new List<GridLevelNode>();

        public GridLevelNode() { }

        public GridLevelNode(string relationName, string caption = "")
        {
            RelationName = relationName;
            Caption = string.IsNullOrEmpty(caption) ? relationName : caption;
        }

        public GridLevelNode AddChild(string relationName, string caption = "")
        {
            var child = new GridLevelNode(relationName, caption);
            Nodes.Add(child);
            return child;
        }
    }

    /// <summary>
    /// Master-detail relation hierarchy tree definition for GridControl.
    /// </summary>
    public sealed class GridLevelTree
    {
        public List<GridLevelNode> Nodes { get; } = new List<GridLevelNode>();

        public GridLevelNode Add(string relationName, string caption = "")
        {
            var node = new GridLevelNode(relationName, caption);
            Nodes.Add(node);
            return node;
        }

        public GridLevelNode? FindNode(string relationName)
        {
            return FindRecursive(Nodes, relationName);
        }

        private static GridLevelNode? FindRecursive(List<GridLevelNode> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                if (string.Equals(n.RelationName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return n;
                }
                var found = FindRecursive(n.Nodes, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
