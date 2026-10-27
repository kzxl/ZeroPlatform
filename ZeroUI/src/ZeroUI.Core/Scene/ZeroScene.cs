using System;
using System.Collections.Generic;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Industrial 2D Scene Graph manager.
    /// Manages hierarchical nodes, spatial indexing, viewport culling, tag binding, and pointer interaction.
    /// </summary>
    public sealed class ZeroScene
    {
        private readonly List<SceneNode> _rootNodes = new List<SceneNode>();
        private readonly ISpatialIndex _spatialIndex;
        private readonly Dictionary<string, List<SceneNode>> _nodesByTagPath = new Dictionary<string, List<SceneNode>>(StringComparer.OrdinalIgnoreCase);
        private SceneNode? _selectedNode;
        private SceneNode? _hoveredNode;

        public event EventHandler? SceneDirty;
        public event EventHandler<SceneNode?>? SelectionChanged;
        public event EventHandler<SceneNode?>? HoverChanged;

        public ISpatialIndex SpatialIndex => _spatialIndex;
        public IReadOnlyList<SceneNode> RootNodes => _rootNodes;

        public SceneNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    if (_selectedNode != null) _selectedNode.IsSelected = false;
                    _selectedNode = value;
                    if (_selectedNode != null) _selectedNode.IsSelected = true;
                    SelectionChanged?.Invoke(this, _selectedNode);
                    Invalidate();
                }
            }
        }

        public SceneNode? HoveredNode
        {
            get => _hoveredNode;
            set
            {
                if (_hoveredNode != value)
                {
                    if (_hoveredNode != null) _hoveredNode.IsHovered = false;
                    _hoveredNode = value;
                    if (_hoveredNode != null) _hoveredNode.IsHovered = true;
                    HoverChanged?.Invoke(this, _hoveredNode);
                    Invalidate();
                }
            }
        }

        public ZeroScene(ISpatialIndex? spatialIndex = null)
        {
            _spatialIndex = spatialIndex ?? new GridSpatialIndex(256f);
        }

        #region Node Management

        public void AddNode(SceneNode node)
        {
            if (node == null || _rootNodes.Contains(node)) return;

            _rootNodes.Add(node);
            RegisterNodeRecursive(node);
            Invalidate();
        }

        public bool RemoveNode(SceneNode node)
        {
            if (node == null) return false;

            if (_rootNodes.Remove(node))
            {
                UnregisterNodeRecursive(node);
                if (_selectedNode == node) SelectedNode = null;
                if (_hoveredNode == node) HoveredNode = null;
                Invalidate();
                return true;
            }
            return false;
        }

        public void Clear()
        {
            for (int i = 0; i < _rootNodes.Count; i++)
            {
                UnregisterNodeRecursive(_rootNodes[i]);
            }
            _rootNodes.Clear();
            _spatialIndex.Clear();
            _nodesByTagPath.Clear();
            _selectedNode = null;
            _hoveredNode = null;
            Invalidate();
        }

        private void RegisterNodeRecursive(SceneNode node)
        {
            _spatialIndex.Insert(node);
            node.Dirty += OnNodeDirty;

            var tagPath = node.TagPath;
            if (!string.IsNullOrWhiteSpace(tagPath))
            {
                if (!_nodesByTagPath.TryGetValue(tagPath!, out var list))
                {
                    list = new List<SceneNode>(2);
                    _nodesByTagPath[tagPath!] = list;
                }
                if (!list.Contains(node)) list.Add(node);
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                RegisterNodeRecursive(node.Children[i]);
            }
        }

        private void UnregisterNodeRecursive(SceneNode node)
        {
            _spatialIndex.Remove(node);
            node.Dirty -= OnNodeDirty;

            var tagPath = node.TagPath;
            if (!string.IsNullOrWhiteSpace(tagPath) && _nodesByTagPath.TryGetValue(tagPath!, out var list))
            {
                list.Remove(node);
                if (list.Count == 0) _nodesByTagPath.Remove(tagPath!);
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                UnregisterNodeRecursive(node.Children[i]);
            }
        }

        private void OnNodeDirty(object? sender, EventArgs e)
        {
            if (sender is SceneNode node)
            {
                _spatialIndex.Update(node);
            }
            Invalidate();
        }

        #endregion

        #region Spatial Querying & Viewport Culling

        /// <summary>
        /// Queries the scene for visible nodes intersecting the viewport and sorts them by ZIndex.
        /// </summary>
        public void QueryVisibleNodes(in SceneRect viewportRect, List<SceneNode> visibleOutput)
        {
            if (visibleOutput == null) return;
            visibleOutput.Clear();

            _spatialIndex.Query(viewportRect, visibleOutput);

            if (visibleOutput.Count > 1)
            {
                // Stable sort by ZIndex
                visibleOutput.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            }
        }

        /// <summary>
        /// Performs a spatial hit-test to find the topmost node under the pointer.
        /// </summary>
        public SceneNode? HitTest(float worldX, float worldY)
        {
            return _spatialIndex.HitTest(worldX, worldY);
        }

        #endregion

        #region SCADA Telemetry Dispatching

        /// <summary>
        /// Dispatches a telemetry tag update directly to bound nodes using the scene's tag index.
        /// </summary>
        public void DispatchTagUpdate(string tagPath, in ScadaValue value)
        {
            if (string.IsNullOrWhiteSpace(tagPath)) return;

            if (_nodesByTagPath.TryGetValue(tagPath, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].OnTagValueChanged(value);
                }
            }
        }

        #endregion

        public void Invalidate()
        {
            SceneDirty?.Invoke(this, EventArgs.Empty);
        }
    }
}
