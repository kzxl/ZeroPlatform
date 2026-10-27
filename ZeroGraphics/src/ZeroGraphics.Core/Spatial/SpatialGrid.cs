using System;
using System.Collections.Generic;

namespace ZeroGraphics.Core.Spatial
{
    /// <summary>
    /// Uniform 2D Hash Grid spatial index optimized for dynamic entities (AGVs, forklifts, sensor tags)
    /// whose positions update frequently in warehouse logistics and factory floor systems.
    /// Provides O(1) cell hashing and fast proximity queries.
    /// </summary>
    /// <typeparam name="T">Entity type implementing <see cref="ISpatialItem"/>.</typeparam>
    public sealed class SpatialGrid<T> where T : class, ISpatialItem
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;
        private readonly Dictionary<long, List<T>> _cells = new Dictionary<long, List<T>>();
        private int _count;

        public float CellSize => _cellSize;
        public int Count => _count;

        public SpatialGrid(float cellSize)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            _cellSize = cellSize;
            _invCellSize = 1.0f / cellSize;
        }

        private long GetCellKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) | (uint)cellY;
        }

        private void GetCellRange(BoundingBox2D bounds, out int minCellX, out int minCellY, out int maxCellX, out int maxCellY)
        {
            minCellX = (int)System.Math.Floor(bounds.MinX * _invCellSize);
            minCellY = (int)System.Math.Floor(bounds.MinY * _invCellSize);
            maxCellX = (int)System.Math.Floor(bounds.MaxX * _invCellSize);
            maxCellY = (int)System.Math.Floor(bounds.MaxY * _invCellSize);
        }

        public void Add(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            GetCellRange(item.Bounds, out int minX, out int minY, out int maxX, out int maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long key = GetCellKey(x, y);
                    if (!_cells.TryGetValue(key, out var cellList))
                    {
                        cellList = new List<T>(4);
                        _cells[key] = cellList;
                    }
                    cellList.Add(item);
                }
            }

            _count++;
        }

        public bool Remove(T item)
        {
            if (item == null) return false;

            GetCellRange(item.Bounds, out int minX, out int minY, out int maxX, out int maxY);
            bool removedAny = false;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long key = GetCellKey(x, y);
                    if (_cells.TryGetValue(key, out var cellList))
                    {
                        if (cellList.Remove(item))
                        {
                            removedAny = true;
                            if (cellList.Count == 0)
                            {
                                _cells.Remove(key);
                            }
                        }
                    }
                }
            }

            if (removedAny)
            {
                _count--;
            }

            return removedAny;
        }

        public void Update(T item, BoundingBox2D oldBounds)
        {
            if (item == null) return;

            // Remove from old cells
            GetCellRange(oldBounds, out int oldMinX, out int oldMinY, out int oldMaxX, out int oldMaxY);
            for (int y = oldMinY; y <= oldMaxY; y++)
            {
                for (int x = oldMinX; x <= oldMaxX; x++)
                {
                    long key = GetCellKey(x, y);
                    if (_cells.TryGetValue(key, out var cellList))
                    {
                        cellList.Remove(item);
                        if (cellList.Count == 0) _cells.Remove(key);
                    }
                }
            }

            // Insert into new cells
            GetCellRange(item.Bounds, out int newMinX, out int newMinY, out int newMaxX, out int newMaxY);
            for (int y = newMinY; y <= newMaxY; y++)
            {
                for (int x = newMinX; x <= newMaxX; x++)
                {
                    long key = GetCellKey(x, y);
                    if (!_cells.TryGetValue(key, out var cellList))
                    {
                        cellList = new List<T>(4);
                        _cells[key] = cellList;
                    }
                    cellList.Add(item);
                }
            }
        }

        public void Clear()
        {
            _cells.Clear();
            _count = 0;
        }

        public void Query(BoundingBox2D range, HashSet<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            GetCellRange(range, out int minX, out int minY, out int maxX, out int maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long key = GetCellKey(x, y);
                    if (_cells.TryGetValue(key, out var cellList))
                    {
                        for (int i = 0; i < cellList.Count; i++)
                        {
                            T item = cellList[i];
                            if (range.Intersects(item.Bounds))
                            {
                                results.Add(item);
                            }
                        }
                    }
                }
            }
        }
    }
}
