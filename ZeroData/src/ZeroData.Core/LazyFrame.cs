using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPrimitives;

namespace ZeroData.Core
{
    internal interface ILazyPlanNode { }

    internal sealed class LazyFilterNode : ILazyPlanNode
    {
        public string ColumnName { get; }
        public Func<DataFrame, int[]> FilterResolver { get; }

        public LazyFilterNode(string columnName, Func<DataFrame, int[]> filterResolver)
        {
            ColumnName = columnName;
            FilterResolver = filterResolver;
        }
    }

    internal sealed class LazyProjectNode : ILazyPlanNode
    {
        public string[] ColumnNames { get; }

        public LazyProjectNode(string[] columnNames)
        {
            ColumnNames = columnNames ?? Array.Empty<string>();
        }
    }

    internal sealed class LazySortNode : ILazyPlanNode
    {
        public string ColumnName { get; }
        public bool Ascending { get; }
        public Func<DataFrame, int[]> Sorter { get; }

        public LazySortNode(string columnName, bool ascending, Func<DataFrame, int[]> sorter)
        {
            ColumnName = columnName;
            Ascending = ascending;
            Sorter = sorter;
        }
    }

    internal sealed class LazySliceNode : ILazyPlanNode
    {
        public int Start { get; }
        public int Length { get; }

        public LazySliceNode(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }

    /// <summary>
    /// Deferred query representation for DataFrames.
    /// Builds a logical query plan (DAG) and executes it with optimizations such as Predicate Pushdown and Projection Pushdown upon calling Collect().
    /// </summary>
    public class LazyFrame
    {
        private readonly DataFrame _source;
        private readonly List<ILazyPlanNode> _planNodes;

        public LazyFrame(DataFrame source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _planNodes = new List<ILazyPlanNode>();
        }

        private LazyFrame(DataFrame source, List<ILazyPlanNode> nodes)
        {
            _source = source;
            _planNodes = nodes;
        }

        /// <summary>
        /// Appends a filtering predicate to the query plan.
        /// </summary>
        public LazyFrame Filter<T>(string columnName, Func<T, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var newNodes = new List<ILazyPlanNode>(_planNodes);

            newNodes.Add(new LazyFilterNode(columnName, df =>
            {
                var col = df.GetColumn(columnName);

                // Fast path: if categorical column and T is string
                if (col is StringDictionaryColumn dictCol && typeof(T) == typeof(string))
                {
                    var matching = new List<int>();
                    var stringPred = (Func<string?, bool>)(object)predicate;
                    var cats = dictCol.Categories;
                    var catMatches = new bool[cats.Count];
                    for (int c = 0; c < cats.Count; c++)
                    {
                        catMatches[c] = stringPred(cats[c]);
                    }
                    bool matchNull = dictCol.HasNulls && stringPred(null);

                    for (int r = 0; r < dictCol.Length; r++)
                    {
                        if (dictCol.IsNull(r))
                        {
                            if (matchNull) matching.Add(r);
                        }
                        else
                        {
                            int catIdx = dictCol.GetCategoryIndex(r);
                            if (catIdx >= 0 && catIdx < catMatches.Length && catMatches[catIdx])
                            {
                                matching.Add(r);
                            }
                        }
                    }
                    return matching.ToArray();
                }

                if (col is DataColumn<T> typedCol)
                {
                    var matching = new List<int>();
                    for (int i = 0; i < typedCol.Length; i++)
                    {
                        if (predicate(typedCol[i])) matching.Add(i);
                    }
                    return matching.ToArray();
                }

                // General fallback
                var fallbackMatches = new List<int>();
                for (int i = 0; i < col.Length; i++)
                {
                    var val = col.GetValue(i);
                    if (val != null && predicate(FastConvert.To<T>(val)))
                    {
                        fallbackMatches.Add(i);
                    }
                }
                return fallbackMatches.ToArray();
            }));

            return new LazyFrame(_source, newNodes);
        }

        /// <summary>
        /// Appends a vectorized SIMD filter operation to the query plan.
        /// </summary>
        public LazyFrame Filter<T>(string columnName, FilterOp op, T threshold)
        {
            var newNodes = new List<ILazyPlanNode>(_planNodes);
            newNodes.Add(new LazyFilterNode(columnName, df =>
            {
                var col = df.GetColumn(columnName);
                if (col is DataColumn<T> typedCol)
                {
                    return typedCol.GetMatchingIndices(op, threshold);
                }

                // Fallback for non-generic or converted types
                var convertedCol = df.Column<T>(columnName);
                return convertedCol.GetMatchingIndices(op, threshold);
            }));

            return new LazyFrame(_source, newNodes);
        }

        public LazyFrame Where<T>(string columnName, FilterOp op, T threshold) => Filter(columnName, op, threshold);

        /// <summary>
        /// Appends a vectorized SIMD range filter to the query plan.
        /// </summary>
        public LazyFrame FilterBetween<T>(string columnName, T low, T high, bool inclusive = true)
        {
            var newNodes = new List<ILazyPlanNode>(_planNodes);
            newNodes.Add(new LazyFilterNode(columnName, df =>
            {
                var col = df.GetColumn(columnName);
                if (col is DataColumn<T> typedCol)
                {
                    return typedCol.GetMatchingIndicesBetween(low, high, inclusive);
                }

                var convertedCol = df.Column<T>(columnName);
                return convertedCol.GetMatchingIndicesBetween(low, high, inclusive);
            }));

            return new LazyFrame(_source, newNodes);
        }

        public LazyFrame WhereBetween<T>(string columnName, T low, T high, bool inclusive = true)
            => FilterBetween(columnName, low, high, inclusive);

        /// <summary>
        /// Synonym for Filter.
        /// </summary>
        public LazyFrame Where<T>(string columnName, Func<T, bool> predicate) => Filter(columnName, predicate);

        /// <summary>
        /// Projects the DataFrame to only contain the specified columns.
        /// Unreferenced columns are dropped early via Projection Pushdown.
        /// </summary>
        public LazyFrame Select(params string[] columnNames)
        {
            if (columnNames == null || columnNames.Length == 0) throw new ArgumentException("Column names cannot be empty.", nameof(columnNames));
            var newNodes = new List<ILazyPlanNode>(_planNodes)
            {
                new LazyProjectNode(columnNames)
            };
            return new LazyFrame(_source, newNodes);
        }

        /// <summary>
        /// Appends an ordering operation to the query plan.
        /// </summary>
        public LazyFrame Sort<TKey>(string columnName, bool ascending = true) where TKey : IComparable<TKey>
        {
            var newNodes = new List<ILazyPlanNode>(_planNodes);
            newNodes.Add(new LazySortNode(columnName, ascending, df =>
            {
                var col = df.Column<TKey>(columnName);
                var indices = Enumerable.Range(0, df.RowCount).ToArray();
                Array.Sort(indices, (a, b) =>
                {
                    int cmp = col[a].CompareTo(col[b]);
                    return ascending ? cmp : -cmp;
                });
                return indices;
            }));

            return new LazyFrame(_source, newNodes);
        }

        public LazyFrame OrderBy<TKey>(string columnName, bool ascending = true) where TKey : IComparable<TKey>
            => Sort<TKey>(columnName, ascending);

        public LazyFrame OrderByDescending<TKey>(string columnName) where TKey : IComparable<TKey>
            => Sort<TKey>(columnName, ascending: false);

        /// <summary>
        /// Appends a slice / limit operation to the query plan.
        /// </summary>
        public LazyFrame Slice(int start, int length)
        {
            var newNodes = new List<ILazyPlanNode>(_planNodes)
            {
                new LazySliceNode(start, length)
            };
            return new LazyFrame(_source, newNodes);
        }

        public LazyFrame Limit(int count) => Slice(0, count);

        /// <summary>
        /// Returns a human-readable string representation of the pending execution plan (DAG).
        /// </summary>
        public string ExplainPlan()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Source] DataFrame (Rows: {_source.RowCount}, Cols: {_source.ColumnCount})");
            for (int i = 0; i < _planNodes.Count; i++)
            {
                var node = _planNodes[i];
                if (node is LazyFilterNode f)
                    sb.AppendLine($"  └─ [{i}] Filter (Column: '{f.ColumnName}')");
                else if (node is LazyProjectNode p)
                    sb.AppendLine($"  └─ [{i}] Project (Columns: [{string.Join(", ", p.ColumnNames)}])");
                else if (node is LazySortNode s)
                    sb.AppendLine($"  └─ [{i}] Sort (Column: '{s.ColumnName}', Asc: {s.Ascending})");
                else if (node is LazySliceNode sl)
                    sb.AppendLine($"  └─ [{i}] Slice (Start: {sl.Start}, Length: {sl.Length})");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compiles, optimizes, and executes the query plan, materializing the resulting DataFrame.
        /// </summary>
        public DataFrame Collect()
        {
            if (_planNodes.Count == 0)
            {
                return _source.Clone();
            }

            // 1. Optimizer: Analyze Projection Pushdown
            var lastProject = _planNodes.OfType<LazyProjectNode>().LastOrDefault();
            HashSet<string>? requiredColumns = null;

            if (lastProject != null)
            {
                requiredColumns = new HashSet<string>(lastProject.ColumnNames, StringComparer.OrdinalIgnoreCase);

                // Add columns needed for filtering or sorting
                foreach (var filter in _planNodes.OfType<LazyFilterNode>())
                {
                    requiredColumns.Add(filter.ColumnName);
                }
                foreach (var sort in _planNodes.OfType<LazySortNode>())
                {
                    requiredColumns.Add(sort.ColumnName);
                }
            }

            // 2. Initial Working Frame (project early if pushdown possible)
            DataFrame current;
            if (requiredColumns != null && requiredColumns.Count < _source.ColumnCount)
            {
                var initialCols = new List<IDataColumn>();
                foreach (var colName in _source.ColumnNames)
                {
                    if (requiredColumns.Contains(colName))
                    {
                        initialCols.Add(_source[colName].Clone());
                    }
                }
                current = new DataFrame(initialCols.ToArray());
            }
            else
            {
                current = _source.Clone();
            }

            // 3. Execute Plan Nodes
            for (int n = 0; n < _planNodes.Count; n++)
            {
                var node = _planNodes[n];

                if (node is LazyFilterNode filterNode)
                {
                    int[] matchingIndices = filterNode.FilterResolver(current);
                    current = current.Filter(matchingIndices);
                }
                else if (node is LazySortNode sortNode)
                {
                    int[] sortedIndices = sortNode.Sorter(current);
                    current = current.Filter(sortedIndices);
                }
                else if (node is LazySliceNode sliceNode)
                {
                    current = current.Slice(sliceNode.Start, Math.Min(sliceNode.Length, current.RowCount - sliceNode.Start));
                }
            }

            // 4. Final Projection if requested
            if (lastProject != null)
            {
                var finalCols = new List<IDataColumn>();
                foreach (var colName in lastProject.ColumnNames)
                {
                    finalCols.Add(current[colName]);
                }
                current = new DataFrame(finalCols.ToArray());
            }

            return current;
        }
    }
}
