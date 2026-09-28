using System;
using System.Collections.Generic;
using ZeroAsset.Variants;

namespace ZeroAsset.Sorting
{
    /// <summary>
    /// Strict Weak Ordering Hierarchical Comparator for asset trees and filmstrips.
    /// Guarantees that child variants and virtual copies always cluster contiguously
    /// adjacent to their master parent across any sorting mode (Date, Name, Rating, Size, etc.).
    /// </summary>
    /// <typeparam name="T">Type of asset item.</typeparam>
    public sealed class HierarchicalContiguousComparer<T> : IComparer<T>
    {
        private readonly Func<T, string> _pathSelector;
        private readonly Comparison<T> _primaryComparison;

        /// <summary>
        /// Initializes a new hierarchical comparer.
        /// </summary>
        /// <param name="pathSelector">Selector retrieving the logical asset path.</param>
        /// <param name="primaryComparison">Primary sort comparison evaluating two master items.</param>
        public HierarchicalContiguousComparer(Func<T, string> pathSelector, Comparison<T> primaryComparison)
        {
            _pathSelector = pathSelector ?? throw new ArgumentNullException(nameof(pathSelector));
            _primaryComparison = primaryComparison ?? throw new ArgumentNullException(nameof(primaryComparison));
        }

        public int Compare(T? x, T? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string pathX = _pathSelector(x);
            string pathY = _pathSelector(y);

            string masterPathX = VariantIdentifier.GetMasterPath(pathX);
            string masterPathY = VariantIdentifier.GetMasterPath(pathY);

            bool sameMaster = string.Equals(masterPathX, masterPathY, StringComparison.OrdinalIgnoreCase);

            if (sameMaster)
            {
                int copyIdxX = VariantIdentifier.GetVariantIndex(pathX);
                int copyIdxY = VariantIdentifier.GetVariantIndex(pathY);
                return copyIdxX.CompareTo(copyIdxY);
            }

            // Primary master comparison
            int primary = _primaryComparison(x, y);
            if (primary != 0) return primary;

            // Stable fallback on master path string to maintain strict weak ordering
            return string.Compare(masterPathX, masterPathY, StringComparison.OrdinalIgnoreCase);
        }
    }
}
