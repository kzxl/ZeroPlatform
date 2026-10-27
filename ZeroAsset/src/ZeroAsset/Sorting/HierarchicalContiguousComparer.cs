using System;
using System.Collections.Generic;
using ZeroAsset.Variants;

namespace ZeroAsset.Sorting
{
    /// <summary>
    /// Strict Weak Ordering Hierarchical Comparator for asset trees and filmstrips.
    /// Guarantees that child variants and virtual copies always cluster contiguously
    /// adjacent to their master parent across any sorting mode (Date, Name, Rating, Size, etc.),
    /// even when child variants possess mutated metadata values that differ from their master.
    /// </summary>
    /// <typeparam name="T">Type of asset item.</typeparam>
    public sealed class HierarchicalContiguousComparer<T> : IComparer<T>
    {
        private readonly Func<T, string> _pathSelector;
        private readonly Comparison<T> _primaryComparison;
        private readonly Func<T, T>? _masterResolver;

        /// <summary>
        /// Initializes a new hierarchical comparer.
        /// </summary>
        /// <param name="pathSelector">Selector retrieving the logical asset path.</param>
        /// <param name="primaryComparison">Primary sort comparison evaluating two master items.</param>
        public HierarchicalContiguousComparer(Func<T, string> pathSelector, Comparison<T> primaryComparison)
            : this(pathSelector, primaryComparison, (Func<T, T>?)null)
        {
        }

        /// <summary>
        /// Initializes a new hierarchical comparer with an explicit master item resolver.
        /// </summary>
        /// <param name="pathSelector">Selector retrieving the logical asset path.</param>
        /// <param name="primaryComparison">Primary sort comparison evaluating two master items.</param>
        /// <param name="masterResolver">Optional resolver mapping any item (master or variant) to its canonical master item.</param>
        public HierarchicalContiguousComparer(
            Func<T, string> pathSelector,
            Comparison<T> primaryComparison,
            Func<T, T>? masterResolver)
        {
            _pathSelector = pathSelector ?? throw new ArgumentNullException(nameof(pathSelector));
            _primaryComparison = primaryComparison ?? throw new ArgumentNullException(nameof(primaryComparison));
            _masterResolver = masterResolver;
        }

        /// <summary>
        /// Initializes a new hierarchical comparer that automatically indexes canonical master items from the dataset,
        /// ensuring strict weak ordering transitivity ($A_0 &lt; A_1 &lt; B_0$) even when variants have divergent sort fields.
        /// </summary>
        /// <param name="dataset">Dataset containing master and variant items to index.</param>
        /// <param name="pathSelector">Selector retrieving the logical asset path.</param>
        /// <param name="primaryComparison">Primary sort comparison evaluating two master items.</param>
        public HierarchicalContiguousComparer(
            IEnumerable<T> dataset,
            Func<T, string> pathSelector,
            Comparison<T> primaryComparison)
        {
            if (dataset == null) throw new ArgumentNullException(nameof(dataset));
            _pathSelector = pathSelector ?? throw new ArgumentNullException(nameof(pathSelector));
            _primaryComparison = primaryComparison ?? throw new ArgumentNullException(nameof(primaryComparison));

            var masterLookup = new Dictionary<string, (T Item, int VariantIndex)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dataset)
            {
                if (item == null) continue;
                string path = _pathSelector(item);
                string masterPath = GetCanonicalMasterPath(path);
                int variantIdx = VariantIdentifier.GetVariantIndex(path);

                if (!masterLookup.TryGetValue(masterPath, out var existing) || variantIdx < existing.VariantIndex)
                {
                    masterLookup[masterPath] = (item, variantIdx);
                }
            }

            _masterResolver = item =>
            {
                string path = _pathSelector(item);
                string masterPath = GetCanonicalMasterPath(path);
                return masterLookup.TryGetValue(masterPath, out var entry) ? entry.Item : item;
            };
        }

        /// <summary>
        /// Sorts the specified list in-place while automatically indexing master items so child variants
        /// always cluster contiguously behind their master item without strict weak ordering violations.
        /// </summary>
        public static void SortContiguous(IList<T> items, Func<T, string> pathSelector, Comparison<T> primaryComparison)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count <= 1) return;

            var comparer = new HierarchicalContiguousComparer<T>(items, pathSelector, primaryComparison);
            if (items is List<T> list)
            {
                list.Sort(comparer);
            }
            else if (items is T[] array)
            {
                Array.Sort(array, comparer);
            }
            else
            {
                T[] temp = new T[items.Count];
                items.CopyTo(temp, 0);
                Array.Sort(temp, comparer);
                for (int i = 0; i < temp.Length; i++)
                {
                    items[i] = temp[i];
                }
            }
        }

        public int Compare(T? x, T? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string pathX = _pathSelector(x);
            string pathY = _pathSelector(y);

            string masterPathX = GetCanonicalMasterPath(pathX);
            string masterPathY = GetCanonicalMasterPath(pathY);

            bool sameMaster = string.Equals(masterPathX, masterPathY, StringComparison.OrdinalIgnoreCase);

            if (sameMaster)
            {
                int copyIdxX = VariantIdentifier.GetVariantIndex(pathX);
                int copyIdxY = VariantIdentifier.GetVariantIndex(pathY);
                int cmpIdx = copyIdxX.CompareTo(copyIdxY);
                if (cmpIdx != 0) return cmpIdx;
                return string.Compare(pathX, pathY, StringComparison.OrdinalIgnoreCase);
            }

            T effectiveMasterX = _masterResolver != null ? (_masterResolver(x) ?? x) : x;
            T effectiveMasterY = _masterResolver != null ? (_masterResolver(y) ?? y) : y;

            // Primary master comparison using canonical master items
            int primary = _primaryComparison(effectiveMasterX, effectiveMasterY);
            if (primary != 0) return primary;

            // Stable fallback on master path string to maintain strict weak ordering
            return string.Compare(masterPathX, masterPathY, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCanonicalMasterPath(string? path)
        {
            string master = VariantIdentifier.GetMasterPath(path);
            return master.IndexOf('\\') >= 0 ? master.Replace('\\', '/') : master;
        }
    }
}
