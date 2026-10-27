using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Headless hierarchical model for TreeList / TreeGrid.
    /// Provides depth-first flattening of visible nodes for ultra-fast $O(1)$ virtualized rendering.
    /// </summary>
    public class TreeModel
    {
        private readonly List<TreeNode> _roots = new List<TreeNode>();
        private readonly List<TreeNode> _flattenedVisibleNodes = new List<TreeNode>();
        private bool _isDirty = true;

        public IReadOnlyList<TreeNode> Roots => _roots;

        public event EventHandler? ModelChanged;

        public TreeNode AddRoot(TreeNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            node.Parent = null;
            _roots.Add(node);
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
            return node;
        }

        public TreeNode AddRoot(params string[] cellValues)
        {
            var node = new TreeNode(cellValues);
            AddRoot(node);
            return node;
        }

        public bool RemoveRoot(TreeNode node)
        {
            if (node != null && _roots.Remove(node))
            {
                _isDirty = true;
                ModelChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _roots.Clear();
            _flattenedVisibleNodes.Clear();
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public int VisibleNodeCount
        {
            get
            {
                EnsureFlattened();
                return _flattenedVisibleNodes.Count;
            }
        }

        public TreeNode GetVisibleNode(int visualIndex)
        {
            EnsureFlattened();
            if (visualIndex >= 0 && visualIndex < _flattenedVisibleNodes.Count)
            {
                return _flattenedVisibleNodes[visualIndex];
            }
            throw new ArgumentOutOfRangeException(nameof(visualIndex));
        }

        public int IndexOf(TreeNode node)
        {
            EnsureFlattened();
            return _flattenedVisibleNodes.IndexOf(node);
        }

        public void ToggleExpand(TreeNode node)
        {
            if (node == null || !node.HasChildren) return;
            node.IsExpanded = !node.IsExpanded;
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExpandAll()
        {
            SetExpandRecursive(_roots, true);
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void CollapseAll()
        {
            SetExpandRecursive(_roots, false);
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void SetExpandRecursive(IEnumerable<TreeNode> nodes, bool isExpanded)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = isExpanded;
                if (node.HasChildren)
                {
                    SetExpandRecursive(node.Children, isExpanded);
                }
            }
        }

        public void Invalidate()
        {
            _isDirty = true;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureFlattened()
        {
            if (!_isDirty) return;

            _flattenedVisibleNodes.Clear();
            for (int i = 0; i < _roots.Count; i++)
            {
                FlattenNodeRecursive(_roots[i]);
            }
            _isDirty = false;
        }

        private void FlattenNodeRecursive(TreeNode node)
        {
            if (!node.IsVisible) return;
            _flattenedVisibleNodes.Add(node);

            if (node.IsExpanded && node.HasChildren)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    FlattenNodeRecursive(node.Children[i]);
                }
            }
        }
    }

    [Obsolete("ZeroTreeModel is deprecated. Use TreeModel instead.")]
    public class ZeroTreeModel : TreeModel
    {
    }
}
