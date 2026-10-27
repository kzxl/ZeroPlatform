using System;
using System.Collections.Generic;

namespace ZeroGraphics.Core.Spatial
{
    /// <summary>
    /// High-performance 2D QuadTree spatial index.
    /// Provides O(log N) fast spatial querying, range searching, and frustum culling
    /// for warehouse layouts, industrial plant floor plans, and P&amp;ID diagrams.
    /// </summary>
    /// <typeparam name="T">Spatial item type implementing <see cref="ISpatialItem"/>.</typeparam>
    public sealed class QuadTree<T> where T : class, ISpatialItem
    {
        private readonly int _maxItemsPerNode;
        private readonly int _maxDepth;
        private readonly Node _root;
        private int _count;

        public BoundingBox2D Bounds => _root.Bounds;
        public int Count => _count;

        public QuadTree(BoundingBox2D bounds, int maxItemsPerNode = 16, int maxDepth = 8)
        {
            if (maxItemsPerNode <= 0) throw new ArgumentOutOfRangeException(nameof(maxItemsPerNode));
            if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));

            _maxItemsPerNode = maxItemsPerNode;
            _maxDepth = maxDepth;
            _root = new Node(bounds, 0);
        }

        public void Insert(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_root.Insert(item, _maxItemsPerNode, _maxDepth))
            {
                _count++;
            }
        }

        public bool Remove(T item)
        {
            if (item == null) return false;
            if (_root.Remove(item))
            {
                _count--;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _root.Clear();
            _count = 0;
        }

        /// <summary>
        /// Queries all items intersecting the specified range/viewport, populating the provided list.
        /// </summary>
        public void Query(BoundingBox2D range, List<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            _root.Query(range, results);
        }

        /// <summary>
        /// Queries all items intersecting the specified range/viewport using a zero-allocation visitor delegate.
        /// </summary>
        public void Query(BoundingBox2D range, Action<T> visitor)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            _root.Query(range, visitor);
        }

        private sealed class Node
        {
            public BoundingBox2D Bounds { get; }
            private readonly int _depth;
            private readonly List<T> _items;
            private Node[]? _children;

            public bool IsLeaf => _children == null;

            public Node(BoundingBox2D bounds, int depth)
            {
                Bounds = bounds;
                _depth = depth;
                _items = new List<T>(8);
            }

            public bool Insert(T item, int maxItems, int maxDepth)
            {
                if (!Bounds.Intersects(item.Bounds))
                {
                    return false;
                }

                if (IsLeaf)
                {
                    if (_items.Count < maxItems || _depth >= maxDepth)
                    {
                        _items.Add(item);
                        return true;
                    }

                    Subdivide();
                }

                // If subdivided, attempt insertion into children
                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (_children[i].Bounds.Contains(item.Bounds))
                        {
                            if (_children[i].Insert(item, maxItems, maxDepth))
                            {
                                return true;
                            }
                        }
                    }
                }

                // If the item spans multiple quadrants or wasn't absorbed by a single child, store at this level
                _items.Add(item);
                return true;
            }

            public bool Remove(T item)
            {
                if (!Bounds.Intersects(item.Bounds)) return false;

                if (_items.Remove(item))
                {
                    return true;
                }

                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (_children[i].Remove(item))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public void Clear()
            {
                _items.Clear();
                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        _children[i].Clear();
                    }
                    _children = null;
                }
            }

            public void Query(BoundingBox2D range, List<T> results)
            {
                if (!Bounds.Intersects(range)) return;

                for (int i = 0; i < _items.Count; i++)
                {
                    T item = _items[i];
                    if (range.Intersects(item.Bounds))
                    {
                        results.Add(item);
                    }
                }

                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        _children[i].Query(range, results);
                    }
                }
            }

            public void Query(BoundingBox2D range, Action<T> visitor)
            {
                if (!Bounds.Intersects(range)) return;

                for (int i = 0; i < _items.Count; i++)
                {
                    T item = _items[i];
                    if (range.Intersects(item.Bounds))
                    {
                        visitor(item);
                    }
                }

                if (_children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        _children[i].Query(range, visitor);
                    }
                }
            }

            private void Subdivide()
            {
                float midX = Bounds.CenterX;
                float midY = Bounds.CenterY;

                _children = new Node[4]
                {
                    new Node(new BoundingBox2D(Bounds.MinX, Bounds.MinY, midX, midY), _depth + 1), // NW
                    new Node(new BoundingBox2D(midX, Bounds.MinY, Bounds.MaxX, midY), _depth + 1), // NE
                    new Node(new BoundingBox2D(Bounds.MinX, midY, midX, Bounds.MaxY), _depth + 1), // SW
                    new Node(new BoundingBox2D(midX, midY, Bounds.MaxX, Bounds.MaxY), _depth + 1)  // SE
                };

                // Re-distribute existing items into children if they fit entirely within a child
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    T item = _items[i];
                    for (int c = 0; c < 4; c++)
                    {
                        if (_children[c].Bounds.Contains(item.Bounds))
                        {
                            _children[c].Insert(item, int.MaxValue, _depth + 1);
                            _items.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }
    }
}
