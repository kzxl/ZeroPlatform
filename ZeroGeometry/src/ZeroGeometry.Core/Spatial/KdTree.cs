using System;
using System.Collections.Generic;

namespace ZeroGeometry.Core.Spatial
{
    /// <summary>
    /// High-performance 3D k-d Tree for spatial point indexing, k-nearest neighbors (k-NN),
    /// and radius searches in O(log N) time.
    /// </summary>
    public sealed class KdTree3D
    {
        private sealed class Node
        {
            public Point3D Point;
            public int Index;
            public Node? Left;
            public Node? Right;

            public Node(Point3D point, int index)
            {
                Point = point;
                Index = index;
            }
        }

        private readonly Node? _root;
        public int Count { get; }

        public KdTree3D(IReadOnlyList<Point3D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            Count = points.Count;

            if (Count > 0)
            {
                var indices = new int[Count];
                for (int i = 0; i < Count; i++) indices[i] = i;
                _root = BuildRecursive(points, indices, 0, Count, depth: 0);
            }
        }

        private static Node? BuildRecursive(IReadOnlyList<Point3D> points, int[] indices, int start, int length, int depth)
        {
            if (length <= 0) return null;

            int axis = depth % 3;
            Array.Sort(indices, start, length, Comparer<int>.Create((i1, i2) =>
                GetAxis(points[i1], axis).CompareTo(GetAxis(points[i2], axis))));

            int midOffset = length / 2;
            int midIndex = start + midOffset;

            int medianIdx = indices[midIndex];
            var node = new Node(points[medianIdx], medianIdx);

            node.Left = BuildRecursive(points, indices, start, midOffset, depth + 1);
            node.Right = BuildRecursive(points, indices, midIndex + 1, length - midOffset - 1, depth + 1);

            return node;
        }

        private static double GetAxis(Point3D p, int axis) => axis switch
        {
            0 => p.X,
            1 => p.Y,
            _ => p.Z
        };

        /// <summary>
        /// Finds the single nearest neighbor to target point in O(log N) time.
        /// </summary>
        public bool NearestNeighbor(Point3D target, out Point3D nearest, out double bestDistSq, out int nearestIndex)
        {
            if (_root == null)
            {
                nearest = default;
                bestDistSq = double.PositiveInfinity;
                nearestIndex = -1;
                return false;
            }

            var best = new BestNeighbor { DistSq = double.PositiveInfinity, Node = _root };
            SearchNearest(_root, target, depth: 0, ref best);

            nearest = best.Node!.Point;
            bestDistSq = best.DistSq;
            nearestIndex = best.Node!.Index;
            return true;
        }

        private struct BestNeighbor
        {
            public double DistSq;
            public Node? Node;
        }

        private static void SearchNearest(Node? current, Point3D target, int depth, ref BestNeighbor best)
        {
            if (current == null) return;

            double distSq = current.Point.DistanceSquaredTo(target);
            if (distSq < best.DistSq)
            {
                best.DistSq = distSq;
                best.Node = current;
            }

            int axis = depth % 3;
            double targetAxis = GetAxis(target, axis);
            double currentAxis = GetAxis(current.Point, axis);
            double diff = targetAxis - currentAxis;

            Node? nearChild = diff < 0 ? current.Left : current.Right;
            Node? farChild = diff < 0 ? current.Right : current.Left;

            SearchNearest(nearChild, target, depth + 1, ref best);

            // If plane crosses sphere of current best distance, search far child
            if (diff * diff < best.DistSq)
            {
                SearchNearest(farChild, target, depth + 1, ref best);
            }
        }

        /// <summary>
        /// Finds the K nearest neighbors to the target point, ordered by distance ascending.
        /// </summary>
        public List<(Point3D point, double distSq, int index)> KNearestNeighbors(Point3D target, int k)
        {
            if (k <= 0 || _root == null) return new List<(Point3D, double, int)>();

            var heap = new SortedList<double, (Point3D point, int index)>(new DuplicateKeyComparer());
            SearchKnn(_root, target, depth: 0, k, heap);

            var result = new List<(Point3D, double, int)>(heap.Count);
            foreach (var kvp in heap)
            {
                result.Add((kvp.Value.point, kvp.Key, kvp.Value.index));
            }
            return result;
        }

        private static void SearchKnn(Node? current, Point3D target, int depth, int k, SortedList<double, (Point3D, int)> heap)
        {
            if (current == null) return;

            double distSq = current.Point.DistanceSquaredTo(target);
            if (heap.Count < k)
            {
                heap.Add(distSq, (current.Point, current.Index));
            }
            else if (distSq < heap.Keys[heap.Count - 1])
            {
                heap.RemoveAt(heap.Count - 1);
                heap.Add(distSq, (current.Point, current.Index));
            }

            int axis = depth % 3;
            double diff = GetAxis(target, axis) - GetAxis(current.Point, axis);

            Node? near = diff < 0 ? current.Left : current.Right;
            Node? far = diff < 0 ? current.Right : current.Left;

            SearchKnn(near, target, depth + 1, k, heap);

            double maxDistSq = heap.Count < k ? double.PositiveInfinity : heap.Keys[heap.Count - 1];
            if (diff * diff < maxDistSq)
            {
                SearchKnn(far, target, depth + 1, k, heap);
            }
        }

        private sealed class DuplicateKeyComparer : IComparer<double>
        {
            public int Compare(double x, double y)
            {
                int result = x.CompareTo(y);
                return result == 0 ? 1 : result; // Keep duplicate keys
            }
        }
    }
}
