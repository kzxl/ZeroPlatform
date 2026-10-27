using System;
using System.Collections.Generic;

namespace ZeroData.Core
{
    public partial class DataFrame
    {
        /// <summary>
        /// Gets the strongly-typed VectorColumn with the specified name.
        /// </summary>
        public VectorColumn VectorColumn(string name)
        {
            var col = GetColumn(name);
            if (col is VectorColumn vecCol)
            {
                return vecCol;
            }

            throw new InvalidCastException($"Column '{name}' is of type {col.DataType.Name}, not VectorColumn.");
        }

        /// <summary>
        /// Adds a new VectorColumn to the DataFrame.
        /// </summary>
        public VectorColumn AddVectorColumn(string name, int dimension)
        {
            var col = new VectorColumn(name, dimension, _rowCount);
            AddColumn(col);
            return col;
        }

        /// <summary>
        /// Executes fused hybrid vector similarity search across this DataFrame.
        /// Directly integrates Apache Arrow SelectionMask metadata filtering with 1-Bit BQ POPCNT acceleration.
        /// </summary>
        public VectorMatch[] SearchVector(
            string columnName,
            ReadOnlySpan<float> query,
            int topK,
            SelectionMask? mask = null,
            int oversampleFactor = 4)
        {
            var col = VectorColumn(columnName);
            return col.SearchNearest(query, topK, mask, oversampleFactor);
        }

        /// <summary>
        /// Executes fused hybrid vector similarity search with a predicate-based row filter.
        /// Automatically generates a SelectionMask before running the SIMD vector scoring.
        /// </summary>
        public VectorMatch[] SearchVector(
            string columnName,
            ReadOnlySpan<float> query,
            int topK,
            RowPredicate rowFilter,
            int oversampleFactor = 4)
        {
            if (rowFilter == null) throw new ArgumentNullException(nameof(rowFilter));

            var mask = new SelectionMask(_rowCount);
            for (int r = 0; r < _rowCount; r++)
            {
                var rowView = new RowView(this, r);
                if (rowFilter(rowView))
                {
                    mask.SetSelected(r, true);
                }
            }

            return SearchVector(columnName, query, topK, mask, oversampleFactor);
        }

        /// <summary>
        /// Executes vector search and materializes a new ranked DataFrame containing only the top-K matching rows.
        /// </summary>
        public DataFrame FilterByVector(
            string columnName,
            ReadOnlySpan<float> query,
            int topK,
            SelectionMask? mask = null,
            int oversampleFactor = 4)
        {
            var matches = SearchVector(columnName, query, topK, mask, oversampleFactor);
            if (matches.Length == 0)
            {
                return Filter(Array.Empty<int>());
            }

            int[] indices = new int[matches.Length];
            for (int i = 0; i < matches.Length; i++)
            {
                indices[i] = matches[i].RowIndex;
            }

            return Filter(indices);
        }
    }
}
