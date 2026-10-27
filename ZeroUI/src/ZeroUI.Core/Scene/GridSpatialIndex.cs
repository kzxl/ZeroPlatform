using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// High-performance 2D Uniform Grid spatial partition.
    /// Provides $O(1)$ cell lookups, near-zero allocation viewport frustum culling, and instant mouse hit-testing.
    /// </summary>
    public sealed class GridSpatialIndex : ISpatialIndex
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, List<SceneNode>> _cells = new Dictionary<long, List<SceneNode>>();
        private readonly Dictionary<SceneNode, List<long>> _nodeCells = new Dictionary<SceneNode, List<long>>();
        private uint _queryStamp = 1;

        /// <summary>
        /// Initializes a new instance of GridSpatialIndex.
        /// </summary>
        /// <param name="cellSize">Size in world coordinate units for each partition grid cell (default: 256).</param>
        public GridSpatialIndex(float cellSize = 256f)
        {
            _cellSize = Math.Max(32f, cellSize);
        }

        public void Insert(SceneNode node)
        {
            if (node == null) return;
            Remove(node);

            var bounds = node.WorldBounds;
            int minCx = (int)Math.Floor(bounds.Left / _cellSize);
            int maxCx = (int)Math.Floor(bounds.Right / _cellSize);
            int minCy = (int)Math.Floor(bounds.Top / _cellSize);
            int maxCy = (int)Math.Floor(bounds.Bottom / _cellSize);

            var registeredKeys = new List<long>();

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = GetCellKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<SceneNode>(8);
                        _cells[key] = list;
                    }
                    list.Add(node);
                    registeredKeys.Add(key);
                }
            }

            _nodeCells[node] = registeredKeys;
        }

        public void Remove(SceneNode node)
        {
            if (node == null) return;

            if (_nodeCells.TryGetValue(node, out var keys))
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    long key = keys[i];
                    if (_cells.TryGetValue(key, out var list))
                    {
                        list.Remove(node);
                        if (list.Count == 0)
                        {
                            _cells.Remove(key);
                        }
                    }
                }
                _nodeCells.Remove(node);
            }
        }

        public void Update(SceneNode node)
        {
            Insert(node);
        }

        public void Clear()
        {
            _cells.Clear();
            _nodeCells.Clear();
        }

        public void Query(in SceneRect viewport, List<SceneNode> results)
        {
            if (results == null) return;

            unchecked
            {
                _queryStamp++;
                if (_queryStamp == 0) _queryStamp = 1;
            }

            int minCx = (int)Math.Floor(viewport.Left / _cellSize);
            int maxCx = (int)Math.Floor(viewport.Right / _cellSize);
            int minCy = (int)Math.Floor(viewport.Top / _cellSize);
            int maxCy = (int)Math.Floor(viewport.Bottom / _cellSize);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = GetCellKey(cx, cy);
                    if (_cells.TryGetValue(key, out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var node = list[i];
                            if (node.QueryStamp != _queryStamp)
                            {
                                node.QueryStamp = _queryStamp;
                                if (node.IsVisible && node.WorldBounds.IntersectsWith(viewport))
                                {
                                    results.Add(node);
                                }
                            }
                        }
                    }
                }
            }
        }

        public SceneNode? HitTest(float worldX, float worldY)
        {
            int cx = (int)Math.Floor(worldX / _cellSize);
            int cy = (int)Math.Floor(worldY / _cellSize);
            long key = GetCellKey(cx, cy);

            if (!_cells.TryGetValue(key, out var list))
                return null;

            SceneNode? bestMatch = null;
            int bestZIndex = int.MinValue;

            for (int i = 0; i < list.Count; i++)
            {
                var node = list[i];
                if (!node.IsVisible) continue;

                if (node.HitTest(worldX, worldY))
                {
                    if (node.ZIndex >= bestZIndex)
                    {
                        bestZIndex = node.ZIndex;
                        bestMatch = node;
                    }
                }
            }

            return bestMatch;
        }

        private static long GetCellKey(int cx, int cy)
        {
            return unchecked(((long)cx << 32) | (uint)cy);
        }
    }
}
