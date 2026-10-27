using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a hierarchical node in a high-performance TreeList or TreeGrid.
    /// Supports parent-child traversal, expand/collapse state, and arbitrary cell values.
    /// </summary>
    public class TreeNode
    {
        private readonly List<TreeNode> _children = new List<TreeNode>();
        private readonly List<string> _cellValues = new List<string>();

        public TreeNode? Parent { get; internal set; }
        public IReadOnlyList<TreeNode> Children => _children;
        public bool IsExpanded { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public object? Tag { get; set; }

        public int Level
        {
            get
            {
                int lvl = 0;
                var p = Parent;
                while (p != null)
                {
                    lvl++;
                    p = p.Parent;
                }
                return lvl;
            }
        }

        public bool HasChildren => _children.Count > 0;

        public TreeNode() { }

        public TreeNode(params string[] cellValues)
        {
            if (cellValues != null)
            {
                _cellValues.AddRange(cellValues);
            }
        }

        public string GetValue(int columnIndex)
        {
            if (columnIndex >= 0 && columnIndex < _cellValues.Count)
            {
                return _cellValues[columnIndex];
            }
            return string.Empty;
        }

        public void SetValue(int columnIndex, string value)
        {
            while (_cellValues.Count <= columnIndex)
            {
                _cellValues.Add(string.Empty);
            }
            _cellValues[columnIndex] = value;
        }

        public TreeNode AddChild(TreeNode child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            child.Parent = this;
            _children.Add(child);
            return child;
        }

        public TreeNode AddChild(params string[] cellValues)
        {
            var node = new TreeNode(cellValues);
            AddChild(node);
            return node;
        }

        public bool RemoveChild(TreeNode child)
        {
            if (child != null && _children.Remove(child))
            {
                child.Parent = null;
                return true;
            }
            return false;
        }

        public void ClearChildren()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Parent = null;
            }
            _children.Clear();
        }
    }

    [Obsolete("ZeroTreeNode is deprecated. Use TreeNode instead.")]
    public class ZeroTreeNode : TreeNode
    {
        public ZeroTreeNode() { }
        public ZeroTreeNode(params string[] cellValues) : base(cellValues) { }
    }
}
