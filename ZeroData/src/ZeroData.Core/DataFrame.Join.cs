using System;
using System.Collections.Generic;

namespace ZeroData.Core
{
    /// <summary>
    /// Supported relational join types between DataFrames.
    /// </summary>
    public enum JoinType
    {
        Inner,
        Left,
        Right,
        FullOuter
    }

    public partial class DataFrame
    {
        /// <summary>
        /// Performs a relational hash join with another DataFrame on a common key column.
        /// </summary>
        public DataFrame Join(
            DataFrame other,
            string onKey,
            JoinType joinType = JoinType.Inner,
            string leftSuffix = "",
            string rightSuffix = "_right")
        {
            return Join(other, onKey, onKey, joinType, leftSuffix, rightSuffix);
        }

        /// <summary>
        /// Performs a relational hash join with another DataFrame matching leftKey to rightKey.
        /// Utilizes unboxed typed hash maps for common primitive and string keys to eliminate GC allocations.
        /// </summary>
        public DataFrame Join(
            DataFrame other,
            string leftKey,
            string rightKey,
            JoinType joinType = JoinType.Inner,
            string leftSuffix = "",
            string rightSuffix = "_right")
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (string.IsNullOrEmpty(leftKey)) throw new ArgumentNullException(nameof(leftKey));
            if (string.IsNullOrEmpty(rightKey)) throw new ArgumentNullException(nameof(rightKey));

            var lCol = GetColumn(leftKey);
            var rCol = other.GetColumn(rightKey);

            List<int> leftIndices;
            List<int> rightIndices;

            // Fast path: typed hash join when key columns share the same primitive or string type
            if (lCol is DataColumn<int> lInt && rCol is DataColumn<int> rInt)
            {
                HashJoinTyped(lInt, rInt, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<long> lLng && rCol is DataColumn<long> rLng)
            {
                HashJoinTyped(lLng, rLng, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<string> lStr && rCol is DataColumn<string> rStr)
            {
                HashJoinTyped(lStr, rStr, RowCount, other.RowCount, joinType, StringComparer.Ordinal, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<decimal> lDec && rCol is DataColumn<decimal> rDec)
            {
                HashJoinTyped(lDec, rDec, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<Guid> lGuid && rCol is DataColumn<Guid> rGuid)
            {
                HashJoinTyped(lGuid, rGuid, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<DateTime> lDt && rCol is DataColumn<DateTime> rDt)
            {
                HashJoinTyped(lDt, rDt, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<double> lDbl && rCol is DataColumn<double> rDbl)
            {
                HashJoinTyped(lDbl, rDbl, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else if (lCol is DataColumn<float> lFlt && rCol is DataColumn<float> rFlt)
            {
                HashJoinTyped(lFlt, rFlt, RowCount, other.RowCount, joinType, null, out leftIndices, out rightIndices);
            }
            else
            {
                // General fallback for mixed or custom types
                HashJoinFallback(lCol, rCol, RowCount, other.RowCount, joinType, out leftIndices, out rightIndices);
            }

            // Materialization Phase: Build Resulting Columns with preserved null validity
            var resultCols = new List<IDataColumn>(ColumnCount + other.ColumnCount);

            // Add left columns
            for (int i = 0; i < ColumnCount; i++)
            {
                string colName = _columnOrder[i];
                var srcCol = _columns[colName];

                string finalName = colName;
                if (other._columns.ContainsKey(colName) && colName != leftKey)
                {
                    finalName = colName + leftSuffix;
                }

                resultCols.Add(GatherColumn(srcCol, finalName, leftIndices));
            }

            // Add right columns
            for (int i = 0; i < other.ColumnCount; i++)
            {
                string rColName = other._columnOrder[i];
                // If same key name used for join, omit duplicate key column in output
                if (string.Equals(rColName, rightKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var srcCol = other._columns[rColName];
                string finalName = rColName;
                if (_columns.ContainsKey(rColName))
                {
                    finalName = rColName + rightSuffix;
                }

                resultCols.Add(GatherColumn(srcCol, finalName, rightIndices));
            }

            return new DataFrame(resultCols.ToArray());
        }

        private static void HashJoinTyped<TKey>(
            DataColumn<TKey> lCol,
            DataColumn<TKey> rCol,
            int leftRowCount,
            int rightRowCount,
            JoinType joinType,
            IEqualityComparer<TKey>? comparer,
            out List<int> leftIndices,
            out List<int> rightIndices)
            where TKey : notnull
        {
            // 1. Build Phase: Chained Flat-Array Hash Table (Zero per-key heap allocations)
            int bucketCount = 16;
            while (bucketCount < rightRowCount * 2 && bucketCount > 0)
            {
                bucketCount <<= 1;
            }
            if (bucketCount <= 0) bucketCount = 1 << 24;
            int bucketMask = bucketCount - 1;

            int[] head = new int[bucketCount];
            for (int i = 0; i < bucketCount; i++) head[i] = -1;

            int[] next = new int[rightRowCount];
            for (int i = 0; i < rightRowCount; i++) next[i] = -1;

            var rData = rCol.RawData;
            bool rHasNulls = rCol.HasNulls;

            // Iterate backwards so that probing traverses matching rows in natural ascending order (FIFO)
            for (int r = rightRowCount - 1; r >= 0; r--)
            {
                if (rHasNulls && rCol.IsNull(r)) continue;
                TKey val = rData[r];
                if (val == null) continue;

                int hash = (comparer != null ? comparer.GetHashCode(val) : val.GetHashCode()) & 0x7FFFFFFF;
                int bucket = hash & bucketMask;

                next[r] = head[bucket];
                head[bucket] = r;
            }

            // 2. Probe Phase
            int initialCapacity = Math.Max(leftRowCount, rightRowCount);
            leftIndices = new List<int>(initialCapacity);
            rightIndices = new List<int>(initialCapacity);
            var rightMatched = (joinType == JoinType.Right || joinType == JoinType.FullOuter) ? new bool[rightRowCount] : null;

            var lData = lCol.RawData;
            bool lHasNulls = lCol.HasNulls;

            for (int l = 0; l < leftRowCount; l++)
            {
                if (lHasNulls && lCol.IsNull(l))
                {
                    if (joinType == JoinType.Left || joinType == JoinType.FullOuter)
                    {
                        leftIndices.Add(l);
                        rightIndices.Add(-1);
                    }
                    continue;
                }

                TKey val = lData[l];
                if (val == null)
                {
                    if (joinType == JoinType.Left || joinType == JoinType.FullOuter)
                    {
                        leftIndices.Add(l);
                        rightIndices.Add(-1);
                    }
                    continue;
                }

                int hash = (comparer != null ? comparer.GetHashCode(val) : val.GetHashCode()) & 0x7FFFFFFF;
                int bucket = hash & bucketMask;

                int r = head[bucket];
                bool matched = false;

                while (r >= 0)
                {
                    bool eq = comparer != null
                        ? comparer.Equals(rData[r], val)
                        : EqualityComparer<TKey>.Default.Equals(rData[r], val);

                    if (eq)
                    {
                        leftIndices.Add(l);
                        rightIndices.Add(r);
                        matched = true;
                        if (rightMatched != null) rightMatched[r] = true;
                    }
                    r = next[r];
                }

                if (!matched && (joinType == JoinType.Left || joinType == JoinType.FullOuter))
                {
                    leftIndices.Add(l);
                    rightIndices.Add(-1);
                }
            }

            // 3. Unmatched Right Rows for Right and FullOuter Joins
            if (rightMatched != null)
            {
                for (int r = 0; r < rightRowCount; r++)
                {
                    if (!rightMatched[r])
                    {
                        leftIndices.Add(-1);
                        rightIndices.Add(r);
                    }
                }
            }
        }

        private static void HashJoinFallback(
            IDataColumn lCol,
            IDataColumn rCol,
            int leftRowCount,
            int rightRowCount,
            JoinType joinType,
            out List<int> leftIndices,
            out List<int> rightIndices)
        {
            var rightMap = new Dictionary<object, List<int>>(rightRowCount);
            for (int r = 0; r < rightRowCount; r++)
            {
                if (rCol.IsNull(r)) continue;
                var val = rCol.GetValue(r);
                if (val == null) continue;

                if (!rightMap.TryGetValue(val, out var list))
                {
                    list = new List<int>(1);
                    rightMap[val] = list;
                }
                list.Add(r);
            }

            int initialCapacity = Math.Max(leftRowCount, rightRowCount);
            leftIndices = new List<int>(initialCapacity);
            rightIndices = new List<int>(initialCapacity);
            var rightMatched = (joinType == JoinType.Right || joinType == JoinType.FullOuter) ? new bool[rightRowCount] : null;

            for (int l = 0; l < leftRowCount; l++)
            {
                if (lCol.IsNull(l))
                {
                    if (joinType == JoinType.Left || joinType == JoinType.FullOuter)
                    {
                        leftIndices.Add(l);
                        rightIndices.Add(-1);
                    }
                    continue;
                }

                var val = lCol.GetValue(l);
                bool matched = false;

                if (val != null && rightMap.TryGetValue(val, out var matches))
                {
                    matched = true;
                    for (int m = 0; m < matches.Count; m++)
                    {
                        int rIdx = matches[m];
                        leftIndices.Add(l);
                        rightIndices.Add(rIdx);
                        if (rightMatched != null) rightMatched[rIdx] = true;
                    }
                }

                if (!matched && (joinType == JoinType.Left || joinType == JoinType.FullOuter))
                {
                    leftIndices.Add(l);
                    rightIndices.Add(-1);
                }
            }

            if (rightMatched != null)
            {
                for (int r = 0; r < rightRowCount; r++)
                {
                    if (!rightMatched[r])
                    {
                        leftIndices.Add(-1);
                        rightIndices.Add(r);
                    }
                }
            }
        }

        private static IDataColumn GatherColumn(IDataColumn src, string name, List<int> indices)
        {
            if (src is DataColumn<int> cInt) return GatherColumnTyped(cInt, name, indices);
            if (src is DataColumn<long> cLng) return GatherColumnTyped(cLng, name, indices);
            if (src is DataColumn<double> cDbl) return GatherColumnTyped(cDbl, name, indices);
            if (src is DataColumn<float> cFlt) return GatherColumnTyped(cFlt, name, indices);
            if (src is DataColumn<decimal> cDec) return GatherColumnTyped(cDec, name, indices);
            if (src is DataColumn<string> cStr) return GatherColumnTyped(cStr, name, indices);
            if (src is DataColumn<DateTime> cDt) return GatherColumnTyped(cDt, name, indices);
            if (src is DataColumn<Guid> cGuid) return GatherColumnTyped(cGuid, name, indices);
            if (src is DataColumn<bool> cBool) return GatherColumnTyped(cBool, name, indices);
            if (src is DataColumn<short> cShort) return GatherColumnTyped(cShort, name, indices);
            if (src is DataColumn<byte> cByte) return GatherColumnTyped(cByte, name, indices);

            // Fallback for custom unhandled types
            int count = indices.Count;
            Type colType = typeof(DataColumn<>).MakeGenericType(src.DataType);
            Array arr = Array.CreateInstance(src.DataType, count);
            var col = (IDataColumn)Activator.CreateInstance(colType, name, arr)!;

            bool srcHasNulls = src.HasNulls;
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (idx >= 0)
                {
                    if (srcHasNulls && src.IsNull(idx))
                    {
                        col.SetNull(i);
                    }
                    else
                    {
                        col.SetValue(i, src.GetValue(idx));
                    }
                }
                else
                {
                    col.SetNull(i);
                }
            }
            return col;
        }

        private static IDataColumn GatherColumnTyped<T>(DataColumn<T> src, string name, List<int> indices)
        {
            int count = indices.Count;
            var data = new T[count];
            var srcData = src.RawData;
            bool srcHasNulls = src.HasNulls;

            var col = new DataColumn<T>(name, data);
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (idx >= 0)
                {
                    if (srcHasNulls && src.IsNull(idx))
                    {
                        col.SetNull(i);
                    }
                    else
                    {
                        data[i] = srcData[idx];
                    }
                }
                else
                {
                    col.SetNull(i);
                }
            }
            return col;
        }
    }
}

