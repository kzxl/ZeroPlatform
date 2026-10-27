using System;
using System.Collections.Generic;

namespace ZeroVector.Core.Indices
{
    /// <summary>
    /// Lightweight binary min-heap priority queue compatible with .NET Standard 2.0, .NET Framework 4.6.2, and modern .NET.
    /// </summary>
    internal sealed class ZeroPriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private (TElement Element, TPriority Priority)[] _nodes;
        private int _count;

        public int Count => _count;

        public ZeroPriorityQueue(int initialCapacity = 16)
        {
            _nodes = new (TElement, TPriority)[Math.Max(initialCapacity, 4)];
            _count = 0;
        }

        public void Enqueue(TElement element, TPriority priority)
        {
            if (_count == _nodes.Length)
            {
                Array.Resize(ref _nodes, _nodes.Length * 2);
            }

            _nodes[_count] = (element, priority);
            MoveUp(_count);
            _count++;
        }

        public TElement Dequeue()
        {
            if (_count == 0) throw new InvalidOperationException("Queue is empty.");

            TElement result = _nodes[0].Element;
            _count--;
            if (_count > 0)
            {
                _nodes[0] = _nodes[_count];
                MoveDown(0);
            }

            _nodes[_count] = default;
            return result;
        }

        public (TElement Element, TPriority Priority) Peek()
        {
            if (_count == 0) throw new InvalidOperationException("Queue is empty.");
            return _nodes[0];
        }

        public void Clear()
        {
            Array.Clear(_nodes, 0, _count);
            _count = 0;
        }

        private void MoveUp(int index)
        {
            var item = _nodes[index];
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_nodes[parent].Priority.CompareTo(item.Priority) <= 0)
                    break;

                _nodes[index] = _nodes[parent];
                index = parent;
            }
            _nodes[index] = item;
        }

        private void MoveDown(int index)
        {
            var item = _nodes[index];
            int half = _count / 2;

            while (index < half)
            {
                int child = 2 * index + 1;
                int right = child + 1;

                if (right < _count && _nodes[right].Priority.CompareTo(_nodes[child].Priority) < 0)
                {
                    child = right;
                }

                if (item.Priority.CompareTo(_nodes[child].Priority) <= 0)
                    break;

                _nodes[index] = _nodes[child];
                index = child;
            }
            _nodes[index] = item;
        }
    }
}
