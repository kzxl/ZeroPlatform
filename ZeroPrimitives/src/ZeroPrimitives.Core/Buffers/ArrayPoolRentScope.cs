using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Safe scope-based ref struct wrapper around ArrayPool&lt;T&gt;.Shared.
    /// Guarantees that rented memory is automatically returned to the shared pool on exiting the 'using' scope.
    /// Zero heap allocation.
    /// </summary>
    public ref struct ArrayPoolRentScope<T>
    {
        private T[]? _rentedArray;
        private readonly int _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPoolRentScope(int minimumLength)
        {
            if (minimumLength <= 0)
            {
                _rentedArray = null;
                _length = 0;
            }
            else
            {
                _rentedArray = ArrayPool<T>.Shared.Rent(minimumLength);
                _length = minimumLength;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayPoolRentScope<T> Rent(int minimumLength)
            => new ArrayPoolRentScope<T>(minimumLength);

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rentedArray != null ? _rentedArray.AsSpan(0, _length) : Span<T>.Empty;
        }

        public ReadOnlySpan<T> ReadOnlySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rentedArray != null ? _rentedArray.AsSpan(0, _length) : ReadOnlySpan<T>.Empty;
        }

        public T[]? RawArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rentedArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            T[]? array = _rentedArray;
            _rentedArray = null;
            if (array != null)
            {
                ArrayPool<T>.Shared.Return(array, clearArray: false);
            }
        }
    }
}
