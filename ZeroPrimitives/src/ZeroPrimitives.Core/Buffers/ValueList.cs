using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Ultra-fast, zero-allocation dynamically-sized list backed by an initial stack buffer or rented ArrayPool.
    /// Operates as a ref struct to guarantee 0 B heap allocation on performance-critical execution loops.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    public ref struct ValueList<T>
    {
        private Span<T> _buffer;
        private T[]? _rentedArray;
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueList(Span<T> initialBuffer)
        {
            _buffer = initialBuffer;
            _rentedArray = null;
            _count = 0;
        }

        public readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        public readonly int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        public readonly Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(0, _count);
        }

        public readonly ReadOnlySpan<T> AsReadOnlySpan()
        {
            return _buffer.Slice(0, _count);
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count)
                    ThrowOutOfRange();
                return ref _buffer[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if (_count >= _buffer.Length)
            {
                Grow();
            }

            _buffer[_count++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ReadOnlySpan<T> items)
        {
            if (items.IsEmpty) return;

            if (_count + items.Length > _buffer.Length)
            {
                Grow(_count + items.Length);
            }

            items.CopyTo(_buffer.Slice(_count));
            _count += items.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int minCapacity = 0)
        {
            int newCapacity = Math.Max(_buffer.Length * 2, 16);
            if (newCapacity < minCapacity)
                newCapacity = minCapacity;

            T[] newArray = ArrayPool<T>.Shared.Rent(newCapacity);
            _buffer.Slice(0, _count).CopyTo(newArray);

            if (_rentedArray != null)
            {
                ArrayPool<T>.Shared.Return(_rentedArray);
            }

            _rentedArray = newArray;
            _buffer = newArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_rentedArray != null)
            {
                T[] toReturn = _rentedArray;
                _rentedArray = null;
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOutOfRange()
        {
            throw new IndexOutOfRangeException("Index was outside the bounds of the ValueList.");
        }
    }
}
