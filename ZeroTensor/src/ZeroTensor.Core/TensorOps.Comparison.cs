using System;
using System.Collections.Generic;

namespace ZeroTensor.Core
{
    public static partial class TensorOps
    {
        #region Comparison Operators

        /// <summary>
        /// Element-wise greater-than comparison: a > b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> GreaterThan<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = aBroad[coords].CompareTo(bBroad[coords]) > 0;
            });

            return result;
        }

        /// <summary>
        /// Element-wise greater-than comparison with a scalar: a > scalar.
        /// </summary>
        public static Tensor<bool> GreaterThan<T>(Tensor<T> a, T scalar) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            var result = new Tensor<bool>(a.Shape);
            a.ForEachCoordinate(coords => result[coords] = a[coords].CompareTo(scalar) > 0);
            return result;
        }

        /// <summary>
        /// Element-wise less-than comparison: a &lt; b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> LessThan<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = aBroad[coords].CompareTo(bBroad[coords]) < 0;
            });

            return result;
        }

        /// <summary>
        /// Element-wise less-than comparison with a scalar: a &lt; scalar.
        /// </summary>
        public static Tensor<bool> LessThan<T>(Tensor<T> a, T scalar) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            var result = new Tensor<bool>(a.Shape);
            a.ForEachCoordinate(coords => result[coords] = a[coords].CompareTo(scalar) < 0);
            return result;
        }

        /// <summary>
        /// Element-wise greater-than-or-equal comparison: a &gt;= b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> GreaterThanOrEqual<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = aBroad[coords].CompareTo(bBroad[coords]) >= 0;
            });

            return result;
        }

        /// <summary>
        /// Element-wise greater-than-or-equal comparison with a scalar: a &gt;= scalar.
        /// </summary>
        public static Tensor<bool> GreaterThanOrEqual<T>(Tensor<T> a, T scalar) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            var result = new Tensor<bool>(a.Shape);
            a.ForEachCoordinate(coords => result[coords] = a[coords].CompareTo(scalar) >= 0);
            return result;
        }

        /// <summary>
        /// Element-wise less-than-or-equal comparison: a &lt;= b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> LessThanOrEqual<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = aBroad[coords].CompareTo(bBroad[coords]) <= 0;
            });

            return result;
        }

        /// <summary>
        /// Element-wise less-than-or-equal comparison with a scalar: a &lt;= scalar.
        /// </summary>
        public static Tensor<bool> LessThanOrEqual<T>(Tensor<T> a, T scalar) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            var result = new Tensor<bool>(a.Shape);
            a.ForEachCoordinate(coords => result[coords] = a[coords].CompareTo(scalar) <= 0);
            return result;
        }

        /// <summary>
        /// Element-wise equality comparison: a == b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> Equal<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = aBroad[coords].Equals(bBroad[coords]);
            });

            return result;
        }

        /// <summary>
        /// Element-wise inequality comparison: a != b. Supports broadcasting.
        /// </summary>
        public static Tensor<bool> NotEqual<T>(Tensor<T> a, Tensor<T> b) where T : unmanaged, IEquatable<T>
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (!a.Shape.IsCompatibleForBroadcasting(b.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast shapes {a.Shape} and {b.Shape}.");
            }

            var result = new Tensor<bool>(outShape);
            var aBroad = a.BroadcastTo(outShape);
            var bBroad = b.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = !aBroad[coords].Equals(bBroad[coords]);
            });

            return result;
        }

        #endregion

        #region Where Conditional Selection

        /// <summary>
        /// Selects elements from x where condition is true, and from y where false (equivalent to np.where / torch.where).
        /// Supports multidimensional broadcasting across condition, x, and y.
        /// </summary>
        public static Tensor<T> Where<T>(
            Tensor<bool> condition,
            Tensor<T> x,
            Tensor<T> y) where T : unmanaged, IEquatable<T>
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (y == null) throw new ArgumentNullException(nameof(y));

            if (!condition.Shape.IsCompatibleForBroadcasting(x.Shape, out var shapeCX) ||
                !shapeCX.IsCompatibleForBroadcasting(y.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast condition ({condition.Shape}), x ({x.Shape}), and y ({y.Shape}).");
            }

            var result = new Tensor<T>(outShape);
            var condBroad = condition.BroadcastTo(outShape);
            var xBroad = x.BroadcastTo(outShape);
            var yBroad = y.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = condBroad[coords] ? xBroad[coords] : yBroad[coords];
            });

            return result;
        }

        /// <summary>
        /// Selects elements from x where condition is true, and scalar y where false.
        /// </summary>
        public static Tensor<T> Where<T>(
            Tensor<bool> condition,
            Tensor<T> x,
            T yScalar) where T : unmanaged, IEquatable<T>
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (x == null) throw new ArgumentNullException(nameof(x));

            if (!condition.Shape.IsCompatibleForBroadcasting(x.Shape, out var outShape))
            {
                throw new InvalidOperationException($"Cannot broadcast condition ({condition.Shape}) and x ({x.Shape}).");
            }

            var result = new Tensor<T>(outShape);
            var condBroad = condition.BroadcastTo(outShape);
            var xBroad = x.BroadcastTo(outShape);

            outShape.ForEachCoordinate(coords =>
            {
                result[coords] = condBroad[coords] ? xBroad[coords] : yScalar;
            });

            return result;
        }

        #endregion

        #region Masking & Boolean Reductions

        /// <summary>
        /// Filters elements of a tensor using a boolean mask, returning a 1D tensor of matching elements (equivalent to tensor[mask]).
        /// </summary>
        public static Tensor<T> Mask<T>(this Tensor<T> tensor, Tensor<bool> mask) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (mask == null) throw new ArgumentNullException(nameof(mask));

            if (tensor.Shape != mask.Shape)
            {
                throw new ArgumentException($"Mask shape {mask.Shape} must match tensor shape {tensor.Shape}.");
            }

            var matched = new List<T>();
            tensor.ForEachCoordinate(coords =>
            {
                if (mask[coords])
                {
                    matched.Add(tensor[coords]);
                }
            });

            return Tensor.FromArray(matched.ToArray());
        }

        /// <summary>
        /// Returns true if any element in the boolean tensor is true.
        /// </summary>
        public static bool Any(Tensor<bool> t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (t.IsContiguous)
            {
                var span = t.AsReadOnlySpan();
                for (int i = 0; i < span.Length; i++)
                {
                    if (span[i]) return true;
                }
                return false;
            }

            bool found = false;
            t.ForEachCoordinate(coords =>
            {
                if (t[coords]) found = true;
            });
            return found;
        }

        /// <summary>
        /// Returns true if all elements in the boolean tensor are true.
        /// </summary>
        public static bool All(Tensor<bool> t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (t.Length == 0) return true;

            if (t.IsContiguous)
            {
                var span = t.AsReadOnlySpan();
                for (int i = 0; i < span.Length; i++)
                {
                    if (!span[i]) return false;
                }
                return true;
            }

            bool allTrue = true;
            t.ForEachCoordinate(coords =>
            {
                if (!t[coords]) allTrue = false;
            });
            return allTrue;
        }

        #endregion
    }

    public static partial class Tensor
    {
        /// <summary>
        /// Selects elements from x where condition is true, and from y where false (np.where / torch.where).
        /// </summary>
        public static Tensor<T> Where<T>(Tensor<bool> condition, Tensor<T> x, Tensor<T> y) where T : unmanaged, IEquatable<T> =>
            TensorOps.Where(condition, x, y);

        /// <summary>
        /// Selects elements from x where condition is true, and scalar y where false.
        /// </summary>
        public static Tensor<T> Where<T>(Tensor<bool> condition, Tensor<T> x, T yScalar) where T : unmanaged, IEquatable<T> =>
            TensorOps.Where(condition, x, yScalar);

        /// <summary>
        /// Returns true if any element in the boolean tensor is true.
        /// </summary>
        public static bool Any(Tensor<bool> t) => TensorOps.Any(t);

        /// <summary>
        /// Returns true if all elements in the boolean tensor are true.
        /// </summary>
        public static bool All(Tensor<bool> t) => TensorOps.All(t);
    }
}
