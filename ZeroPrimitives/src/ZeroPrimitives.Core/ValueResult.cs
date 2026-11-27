using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives
{
    /// <summary>
    /// Lightweight, zero-allocation discriminated result struct for batch ERP validation and import pipelines without exception unwinding overhead.
    /// </summary>
    public readonly struct ValueResult<T, TError> : IEquatable<ValueResult<T, TError>>
    {
        private readonly T _value;
        private readonly TError _error;
        private readonly bool _isSuccess;

        private ValueResult(bool isSuccess, T value, TError error)
        {
            _isSuccess = isSuccess;
            _value = value;
            _error = error;
        }

        public bool IsSuccess
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isSuccess;
        }

        public bool IsFailure
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !_isSuccess;
        }

        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isSuccess)
                    throw new InvalidOperationException("Cannot access Value on a failed ValueResult.");
                return _value;
            }
        }

        public TError Error
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_isSuccess)
                    throw new InvalidOperationException("Cannot access Error on a successful ValueResult.");
                return _error;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueResult<T, TError> Ok(T value) => new ValueResult<T, TError>(true, value, default!);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueResult<T, TError> Fail(TError error) => new ValueResult<T, TError>(false, default!, error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out T value)
        {
            if (_isSuccess)
            {
                value = _value;
                return true;
            }
            value = default!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetError(out TError error)
        {
            if (!_isSuccess)
            {
                error = _error;
                return true;
            }
            error = default!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetValueOrDefault(T defaultValue = default!)
            => _isSuccess ? _value : defaultValue;

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onFailure)
            => _isSuccess ? onSuccess(_value) : onFailure(_error);

        public ValueResult<TNext, TError> Map<TNext>(Func<T, TNext> mapper)
            => _isSuccess ? ValueResult<TNext, TError>.Ok(mapper(_value)) : ValueResult<TNext, TError>.Fail(_error);

        public bool Equals(ValueResult<T, TError> other)
        {
            if (_isSuccess != other._isSuccess) return false;
            return _isSuccess
                ? EqualityComparer<T>.Default.Equals(_value, other._value)
                : EqualityComparer<TError>.Default.Equals(_error, other._error);
        }

        public override bool Equals(object? obj) => obj is ValueResult<T, TError> other && Equals(other);

        public override int GetHashCode()
        {
            if (_isSuccess)
                return _value == null ? 1 : EqualityComparer<T>.Default.GetHashCode(_value);
            return _error == null ? 0 : EqualityComparer<TError>.Default.GetHashCode(_error);
        }

        public static bool operator ==(ValueResult<T, TError> left, ValueResult<T, TError> right) => left.Equals(right);
        public static bool operator !=(ValueResult<T, TError> left, ValueResult<T, TError> right) => !left.Equals(right);
    }

    /// <summary>
    /// Factory helpers for creating <see cref="ValueResult{T, TError}"/> instances with type inference.
    /// </summary>
    public static class ValueResult
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueResult<T, TError> Ok<T, TError>(T value) => ValueResult<T, TError>.Ok(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueResult<T, TError> Fail<T, TError>(TError error) => ValueResult<T, TError>.Fail(error);
    }
}
