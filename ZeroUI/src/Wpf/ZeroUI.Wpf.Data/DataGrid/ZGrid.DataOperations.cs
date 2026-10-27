using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroData.Core;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Virtualization;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    public partial class ZGrid
    {
        public void GroupBy(params int[] columnIndices)
        {
            if (_dataSource == null || columnIndices == null || columnIndices.Length == 0)
            {
                ClearGrouping();
                return;
            }

            _groupColumnIndices = (int[])columnIndices.Clone();
            int totalRows = _dataSource.TotalRowCount;

            _groupedMap.BuildGroups(totalRows, columnIndices, (modelRow, colIdx) =>
            {
                CellValueBuffer buf = new CellValueBuffer();
                _dataSource.GetCellValue(modelRow, colIdx, ref buf);
                return buf.Text.ToString();
            });

            if (_groupSummaries.Count > 0)
            {
                RecalculateGroupSummaries();
            }

            _scrollY = 0;
            _selectedVisualRow = -1;
            _selectedVisualRows.Clear();
            InvalidateVisual();
        }

        public void RecalculateGroupSummaries()
        {
            if (_dataSource == null || !_groupedMap.HasGrouping) return;

            if (_groupSummaries.Count == 0)
            {
                _groupedMap.CalculateSummaries(_groupSummaries, (r, c) => 0);
            }
            else
            {
                _groupedMap.CalculateSummaries(_groupSummaries, (modelRow, colIdx) =>
                {
                    CellValueBuffer buf = new CellValueBuffer();
                    _dataSource.GetCellValue(modelRow, colIdx, ref buf);
                    var s = buf.Text.ToString();
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out double val2)) return val2;
                    return 0;
                });
            }
            InvalidateVisual();
        }

        public bool IsColumnFiltered(int columnIndex) => _columnDistinctFilters.ContainsKey(columnIndex);


        public List<string> GetDistinctColumnValues(int columnIndex)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_dataSource != null)
            {
                int total = _dataSource.TotalRowCount;
                CellValueBuffer buf = new CellValueBuffer();
                int limit = Math.Min(total, 500);
                int step = (total > limit && limit > 0) ? Math.Max(1, total / limit) : 1;
                for (int r = 0; r < total && set.Count < 500; r += step)
                {
                    _dataSource.GetCellValue(r, columnIndex, ref buf);
                    set.Add(buf.Text.ToString());
                }
            }
            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public void ApplyDistinctColumnFilter(int columnIndex, HashSet<string>? selectedValues)
        {
            if (selectedValues == null || selectedValues.Count == 0)
            {
                _columnDistinctFilters.Remove(columnIndex);
            }
            else
            {
                _columnDistinctFilters[columnIndex] = selectedValues;
            }

            ReapplyAllFilters();
        }

        public void ClearAllFilters()
        {
            _columnDistinctFilters.Clear();
            ReapplyAllFilters();
        }

        public void ReapplyAllFilters()
        {
            if (_dataSource == null) return;
            int total = _dataSource.TotalRowCount;

            if (_columnDistinctFilters.Count == 0)
            {
                _rowIndexMap.ResetIdentity(total);
            }
            else
            {
                _rowIndexMap.Filter(modelRow =>
                {
                    CellValueBuffer buf = new CellValueBuffer();
                    foreach (var kvp in _columnDistinctFilters)
                    {
                        _dataSource.GetCellValue(modelRow, kvp.Key, ref buf);
                        if (!kvp.Value.Contains(buf.Text.ToString())) return false;
                    }
                    return true;
                }, total);
            }

            ClearRowSelection();
            InvalidateSummaries();

            if (_groupedMap.HasGrouping && _groupColumnIndices.Length > 0)
            {
                GroupBy(_groupColumnIndices);
            }
            else
            {
                _scrollY = 0;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Applies an Apache Arrow-compliant SelectionMask directly to the visual row index map.
        /// Accelerates 1,000,000+ row filtering using SIMD 64-bit word skipping and TrailingZeroCount bit extraction. Zero GC allocation.
        /// </summary>
        public void ApplySelectionMask(SelectionMask mask)
        {
            if (_dataSource == null) return;
            if (mask == null)
            {
                _rowIndexMap.ResetIdentity(_dataSource.TotalRowCount);
            }
            else
            {
                _rowIndexMap.ApplySelectionMask(mask);
            }

            ClearRowSelection();
            InvalidateSummaries();

            if (_groupedMap.HasGrouping && _groupColumnIndices.Length > 0)
            {
                GroupBy(_groupColumnIndices);
            }
            else
            {
                _scrollY = 0;
                InvalidateVisual();
            }
        }

        public void ShowColumnFilterPopup(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _columns.Count) return;
            var col = _columns[columnIndex];
            var distinctValues = GetDistinctColumnValues(columnIndex);
            _columnDistinctFilters.TryGetValue(columnIndex, out var currentSelection);

            var popup = new ZeroColumnFilterPopup(this, columnIndex, col.HeaderText, distinctValues, currentSelection, (colIdx, selected) =>
            {
                ApplyDistinctColumnFilter(colIdx, selected);
            });
            if (_columnFilterButtonBounds.TryGetValue(columnIndex, out var buttonRect))
            {
                popup.PlacementRectangle = buttonRect;
            }
            popup.IsOpen = true;
        }

        public void ClearGrouping()
        {
            _groupColumnIndices = Array.Empty<int>();
            if (_dataSource != null)
            {
                _groupedMap.ResetIdentity(_dataSource.TotalRowCount);
                _rowIndexMap.ResetIdentity(_dataSource.TotalRowCount);
            }
            else
            {
                _groupedMap.ResetIdentity(0);
                _rowIndexMap.ActiveCount = 0;
            }
            _scrollY = 0;
            ClearRowSelection();
            InvalidateSummaries();
            InvalidateVisual();
        }

        public void ExpandAllGroups()
        {
            if (_groupedMap.HasGrouping)
            {
                _groupedMap.ExpandAll();
                InvalidateVisual();
            }
        }

        public void CollapseAllGroups()
        {
            if (_groupedMap.HasGrouping)
            {
                _groupedMap.CollapseAll();
                InvalidateVisual();
            }
        }

        public bool ToggleGroup(int visualRowIndex)
        {
            if (_groupedMap.HasGrouping && _groupedMap.ToggleGroup(visualRowIndex))
            {
                InvalidateVisual();
                return true;
            }
            return false;
        }

        public void SetDataSource<T>(IList<T> items, bool autoGenerateColumns = true)
        {
            if (items == null)
            {
                DataSource = null;
                return;
            }

            if (autoGenerateColumns && _columns.Count == 0)
            {
                var src = new ListSource<T>(items);
                foreach (var c in src.GenerateColumns())
                {
                    _columns.Add(c);
                }
                DataSource = src;
            }
            else
            {
                DataSource = new ListSource<T>(items, _columns);
            }
        }

        public void SetDataSource(DataTable? table, bool autoGenerateColumns = true)
        {
            if (table == null)
            {
                DataSource = null;
                return;
            }

            if (autoGenerateColumns && _columns.Count == 0)
            {
                var src = new DataTableSource(table);
                foreach (var c in src.GenerateColumns())
                {
                    _columns.Add(c);
                }
                DataSource = src;
            }
            else
            {
                DataSource = new DataTableSource(table, _columns);
            }
        }

        public void SetDataSource(DataView? view, bool autoGenerateColumns = true)
        {
            if (view == null)
            {
                DataSource = null;
                return;
            }

            if (autoGenerateColumns && _columns.Count == 0)
            {
                var src = new DataTableSource(view);
                foreach (var c in src.GenerateColumns())
                {
                    _columns.Add(c);
                }
                DataSource = src;
            }
            else
            {
                DataSource = new DataTableSource(view, _columns);
            }
        }

        public void SetDataSource(DataFrame? dataFrame, bool autoGenerateColumns = true)
        {
            if (dataFrame == null)
            {
                DataSource = null;
                return;
            }

            if (autoGenerateColumns && _columns.Count == 0)
            {
                var src = new DataFrameSource(dataFrame);
                foreach (var c in src.GenerateColumns())
                {
                    _columns.Add(c);
                }
                DataSource = src;
            }
            else
            {
                DataSource = new DataFrameSource(dataFrame, _columns);
            }
        }

        public void SetDataSource(object? source, bool autoGenerateColumns = true)
        {
            if (source == null)
            {
                DataSource = null;
                return;
            }

            if (source is IZeroVirtualSource virtualSource)
            {
                DataSource = virtualSource;
                return;
            }

            if (source is DataFrame df)
            {
                SetDataSource(df, autoGenerateColumns);
                return;
            }

            if (source is DataTable dt)
            {
                SetDataSource(dt, autoGenerateColumns);
                return;
            }

            if (source is DataView dv)
            {
                SetDataSource(dv, autoGenerateColumns);
                return;
            }

            // Generic IList fallback
            var type = source.GetType();
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    var itemType = iface.GetGenericArguments()[0];
                    var listSourceType = typeof(ListSource<>).MakeGenericType(itemType);
                    if (autoGenerateColumns && _columns.Count == 0)
                    {
                        var listSource = Activator.CreateInstance(listSourceType, source);
                        var genMethod = listSourceType.GetMethod("GenerateColumns");
                        if (genMethod != null)
                        {
                            var genCols = genMethod.Invoke(listSource, null) as IEnumerable<ZeroColumn>;
                            if (genCols != null)
                            {
                                foreach (var c in genCols) _columns.Add(c);
                            }
                        }
                        DataSource = (IZeroVirtualSource)listSource!;
                    }
                    else
                    {
                        var listSource = Activator.CreateInstance(listSourceType, source, _columns);
                        DataSource = (IZeroVirtualSource)listSource!;
                    }
                    return;
                }
            }

            throw new ArgumentException($"Unsupported data source type: {source.GetType().FullName}. Expected DataFrame, DataTable, DataView, IList<T>, or IZeroVirtualSource.", nameof(source));
        }

        public bool HasAnySummaryColumns()
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible && _columns[i].Summary != SummaryType.None) return true;
            }
            return false;
        }

        public void SortByColumn(int colIndex, Comparison<int> comparison)
        {
            _ = SortByColumnAsync(colIndex, comparison);
        }

        public async Task SortByColumnAsync(int colIndex)
        {
            if (_dataSource is IZeroSortableSource sortable)
            {
                await SortByColumnAsync(colIndex, (a, b) => sortable.CompareRows(a, b, colIndex));
            }
            else if (_dataSource != null)
            {
                await SortByColumnAsync(colIndex, (a, b) =>
                {
                    CellValueBuffer bufA = new CellValueBuffer();
                    CellValueBuffer bufB = new CellValueBuffer();
                    _dataSource.GetCellValue(a, colIndex, ref bufA);
                    _dataSource.GetCellValue(b, colIndex, ref bufB);
                    return bufA.Text.CompareTo(bufB.Text, StringComparison.Ordinal);
                });
            }
        }

        public async Task SortByColumnAsync(int colIndex, Comparison<int> comparison)
        {
            if (_dataSource == null || colIndex < 0 || colIndex >= _columns.Count) return;
            if (_isSorting) return;

            var col = _columns[colIndex];
            SortDirection newDirection = (col.SortOrder == SortDirection.Ascending) ? SortDirection.Descending : SortDirection.Ascending;
            col.SortOrder = newDirection;

            // Reset other columns
            for (int i = 0; i < _columns.Count; i++)
            {
                if (i != colIndex) _columns[i].SortOrder = SortDirection.None;
            }

            int count = _rowIndexMap.ActiveCount;
            if (count <= 1)
            {
                InvalidateVisual();
                return;
            }

            _isSorting = true;
            _sortingColumnIndex = colIndex;
            SortingStarted?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();

            _sortCts?.Cancel();
            _sortCts = new System.Threading.CancellationTokenSource();
            var token = _sortCts.Token;

            bool isIdentity = _rowIndexMap.IsIdentity;
            int[]? working = null;
            if (!isIdentity)
            {
                working = new int[count];
                for (int i = 0; i < count; i++)
                {
                    working[i] = _rowIndexMap[i];
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var ds = _dataSource;
                await Task.Run(() =>
                {
                    if (working == null)
                    {
                        working = new int[count];
                        for (int i = 0; i < count; i++) working[i] = i;
                    }

                    if (ds is IZeroSortableSource sortable)
                    {
                        var comparer = new SortableSourceComparer(sortable, colIndex, newDirection);
                        Array.Sort(working, 0, count, comparer);
                    }
                    else
                    {
                        var comparer = new FastComparisonComparer(newDirection == SortDirection.Descending ? (a, b) => comparison(b, a) : comparison);
                        Array.Sort(working, 0, count, comparer);
                    }
                }, token);

                sw.Stop();

                if (!token.IsCancellationRequested)
                {
                    _rowIndexMap.SetUnderlyingBuffer(working!, count);

                    _scrollY = 0;
                    ClearRowSelection();
                    InvalidateSummaries();
                    SortingCompleted?.Invoke(this, sw.Elapsed);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sort error: {ex}");
            }
            finally
            {
                _isSorting = false;
                _sortingColumnIndex = -1;
                InvalidateVisual();
            }
        }

        public void CopySelectionToClipboard()
        {
            if (_dataSource == null) return;

            if (_selectionMode == ZeroGridSelectionMode.Block && !_selectedBlock.IsEmpty)
            {
                int topR = _selectedBlock.TopRow;
                int botR = _selectedBlock.BottomRow;
                int leftC = _selectedBlock.LeftColumn;
                int rightC = _selectedBlock.RightColumn;

                CellValueBuffer blockBuf = new CellValueBuffer();
                var blockSb = new System.Text.StringBuilder();

                bool first = true;
                for (int c = leftC; c <= rightC && c < _columns.Count; c++)
                {
                    if (!_columns[c].IsVisible) continue;
                    if (!first) blockSb.Append('\t');
                    blockSb.Append(_columns[c].HeaderText);
                    first = false;
                }
                blockSb.AppendLine();

                for (int r = topR; r <= botR && r < VisualRowCount; r++)
                {
                    int modelRow = GetModelRowIndex(r);
                    if (modelRow < 0) continue;

                    first = true;
                    for (int c = leftC; c <= rightC && c < _columns.Count; c++)
                    {
                        if (!_columns[c].IsVisible) continue;
                        if (!first) blockSb.Append('\t');
                        blockBuf.Reset();
                        _dataSource.GetCellValue(modelRow, c, ref blockBuf);
                        blockSb.Append(blockBuf.Text.ToString());
                        first = false;
                    }
                    blockSb.AppendLine();
                }

                try { Clipboard.SetText(blockSb.ToString()); } catch { }
                return;
            }

            var rowsToCopy = new List<int>();
            if (_isAllSelected)
            {
                int maxCopy = Math.Min(VisualRowCount, 50_000);
                for (int r = 0; r < maxCopy; r++)
                {
                    if (!_deselectedVisualRows.Contains(r))
                    {
                        rowsToCopy.Add(r);
                    }
                }
            }
            else if (_selectedVisualRows.Count > 0)
            {
                rowsToCopy.AddRange(_selectedVisualRows);
                rowsToCopy.Sort();
            }
            else if (_selectedVisualRow >= 0 && _selectedVisualRow < VisualRowCount)
            {
                rowsToCopy.Add(_selectedVisualRow);
            }

            if (rowsToCopy.Count == 0) return;

            CellValueBuffer buf = new CellValueBuffer();
            var sb = new System.Text.StringBuilder();

            // Header row
            bool firstCol = true;
            for (int c = 0; c < _columns.Count; c++)
            {
                if (!_columns[c].IsVisible) continue;
                if (!firstCol) sb.Append('\t');
                sb.Append(_columns[c].HeaderText);
                firstCol = false;
            }
            sb.AppendLine();

            // Data rows
            foreach (int vRow in rowsToCopy)
            {
                if (vRow < 0 || vRow >= VisualRowCount) continue;
                int modelRow = GetModelRowIndex(vRow);
                if (modelRow < 0) continue;

                firstCol = true;
                for (int c = 0; c < _columns.Count; c++)
                {
                    if (!_columns[c].IsVisible) continue;
                    if (!firstCol) sb.Append('\t');
                    buf.Reset();
                    _dataSource.GetCellValue(modelRow, c, ref buf);
                    sb.Append(buf.Text.ToString());
                    firstCol = false;
                }
                sb.AppendLine();
            }

            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        public string GetColumnSummaryText(int colIndex)
        {
            if (colIndex < 0 || colIndex >= _columns.Count || _dataSource == null) return string.Empty;
            var col = _columns[colIndex];
            if (col.Summary == SummaryType.None) return string.Empty;

            int count = _rowIndexMap.ActiveCount;
            if (col.Summary == SummaryType.Count)
            {
                string countText = !string.IsNullOrEmpty(col.SummaryFormat)
                    ? string.Format(CultureInfo.InvariantCulture, col.SummaryFormat, count)
                    : $"Count: {count:N0}";
                _cachedSummaryTexts[colIndex] = countText;
                return countText;
            }

            if (count == 0) return "-";

            // If clean and cached, return immediately O(1) with 0 allocations
            if (!_summariesDirty && _cachedSummaryTexts.TryGetValue(colIndex, out var cachedVal))
            {
                return cachedVal;
            }

            // For small datasets (<= 50,000 rows), calculate synchronously on-demand
            if (count <= 50_000)
            {
                string syncText = CalculateColumnSummarySync(colIndex, col, count);
                _cachedSummaryTexts[colIndex] = syncText;
                return syncText;
            }

            // For large datasets (> 50,000 rows), trigger background calculation and avoid blocking UI thread
            TriggerBackgroundSummaryCalculation();

            if (_cachedSummaryTexts.TryGetValue(colIndex, out var previousVal))
            {
                return previousVal;
            }

            return "Calculating...";
        }

        private string CalculateColumnSummarySync(int colIndex, ZeroColumn col, int count)
        {
            if (_dataSource == null || count == 0) return "-";

            double sum = 0;
            double min = double.MaxValue;
            double max = double.MinValue;
            int validCount = 0;
            bool isIdentity = _rowIndexMap.IsIdentity;

            CellValueBuffer buf = new CellValueBuffer();
            for (int i = 0; i < count; i++)
            {
                int mRow = isIdentity ? i : _rowIndexMap[i];
                buf.Reset();
                _dataSource.GetCellValue(mRow, colIndex, ref buf);
                string s = buf.Text.ToString();
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) ||
                    double.TryParse(s.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                {
                    sum += val;
                    if (val < min) min = val;
                    if (val > max) max = val;
                    validCount++;
                }
            }

            if (validCount == 0) return "-";

            double result = col.Summary switch
            {
                SummaryType.Sum => sum,
                SummaryType.Average => sum / validCount,
                SummaryType.Min => min,
                SummaryType.Max => max,
                _ => 0
            };

            if (!string.IsNullOrEmpty(col.SummaryFormat))
            {
                return string.Format(CultureInfo.InvariantCulture, col.SummaryFormat, result);
            }

            return col.Summary switch
            {
                SummaryType.Sum => $"Σ {result:N0}",
                SummaryType.Average => $"μ {result:N1}",
                SummaryType.Min => $"Min {result:N0}",
                SummaryType.Max => $"Max {result:N0}",
                _ => result.ToString(CultureInfo.InvariantCulture)
            };
        }

        private void TriggerBackgroundSummaryCalculation()
        {
            if (_isCalculatingSummaries || _dataSource == null) return;
            _isCalculatingSummaries = true;
            _summaryCts?.Cancel();
            _summaryCts = new System.Threading.CancellationTokenSource();
            var token = _summaryCts.Token;

            var source = _dataSource;
            int activeCount = _rowIndexMap.ActiveCount;
            bool isIdentity = _rowIndexMap.IsIdentity;

            var colsToCalculate = new List<(int ColIndex, SummaryType Summary, string? Format)>();
            for (int c = 0; c < _columns.Count; c++)
            {
                if (_columns[c].IsVisible && _columns[c].Summary != SummaryType.None && _columns[c].Summary != SummaryType.Count)
                {
                    colsToCalculate.Add((c, _columns[c].Summary, _columns[c].SummaryFormat));
                }
            }

            if (colsToCalculate.Count == 0)
            {
                _summariesDirty = false;
                _isCalculatingSummaries = false;
                return;
            }

            Task.Run(() =>
            {
                var computed = new Dictionary<int, string>();
                CellValueBuffer buf = new CellValueBuffer();

                foreach (var item in colsToCalculate)
                {
                    if (token.IsCancellationRequested) return;

                    double sum = 0;
                    double min = double.MaxValue;
                    double max = double.MinValue;
                    int validCount = 0;

                    for (int i = 0; i < activeCount; i++)
                    {
                        if ((i & 0xFFFF) == 0 && token.IsCancellationRequested) return;
                        int mRow = isIdentity ? i : _rowIndexMap[i];
                        buf.Reset();
                        source.GetCellValue(mRow, item.ColIndex, ref buf);
                        string s = buf.Text.ToString();
                        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) ||
                            double.TryParse(s.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                        {
                            sum += val;
                            if (val < min) min = val;
                            if (val > max) max = val;
                            validCount++;
                        }
                    }

                    if (validCount > 0)
                    {
                        double result = item.Summary switch
                        {
                            SummaryType.Sum => sum,
                            SummaryType.Average => sum / validCount,
                            SummaryType.Min => min,
                            SummaryType.Max => max,
                            _ => 0
                        };

                        string formatted = !string.IsNullOrEmpty(item.Format)
                            ? string.Format(CultureInfo.InvariantCulture, item.Format, result)
                            : item.Summary switch
                            {
                                SummaryType.Sum => $"Σ {result:N0}",
                                SummaryType.Average => $"μ {result:N1}",
                                SummaryType.Min => $"Min {result:N0}",
                                SummaryType.Max => $"Max {result:N0}",
                                _ => result.ToString(CultureInfo.InvariantCulture)
                            };

                        computed[item.ColIndex] = formatted;
                    }
                    else
                    {
                        computed[item.ColIndex] = "-";
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            foreach (var kvp in computed)
                            {
                                _cachedSummaryTexts[kvp.Key] = kvp.Value;
                            }
                            _summariesDirty = false;
                            _isCalculatingSummaries = false;
                            InvalidateVisual();
                        }));
                    }
                    catch { }
                }
            }, token);
        }

        public void ShowColumnChooser()
        {
            if (_columnChooser == null)
            {
                _columnChooser = new ColumnChooserWindow(this);
            }

            Point screenPt = PointToScreen(new Point(Math.Max(0, ActualWidth - 280), 40));
            _columnChooser.Left = Math.Max(0, screenPt.X);
            _columnChooser.Top = Math.Max(0, screenPt.Y);
            _columnChooser.RefreshColumns();
            _columnChooser.Show();
            _columnChooser.Activate();
        }

        public void HideColumnChooser()
        {
            _columnChooser?.Hide();
        }

        public void AutoFitColumnWidth(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _columns.Count) return;
            var col = _columns[columnIndex];
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var headerFt = CreateFormattedText(col.HeaderText, ZeroWpfTheme.BoldTypeface, 12, Brushes.White, dpi);
            double maxW = headerFt.Width + 36;

            if (_dataSource != null)
            {
                int sampleCount = Math.Min(200, _rowIndexMap.ActiveCount);
                CellValueBuffer buf = new CellValueBuffer();
                for (int i = 0; i < sampleCount; i++)
                {
                    int modelRow = _rowIndexMap[i];
                    _dataSource.GetCellValue(modelRow, columnIndex, ref buf);
                    if (buf.Text.Length > 0)
                    {
                        var cellFt = CreateFormattedText(buf.Text.ToString(), ZeroWpfTheme.RegularTypeface, 12, Brushes.White, dpi);
                        double w = cellFt.Width + 24;
                        if (w > maxW) maxW = w;
                    }
                }
            }

            col.Width = (int)Math.Max(col.MinWidth, Math.Min(col.MaxWidth, Math.Ceiling(maxW)));
            InvalidateVisual();
        }

        public void AutoFitAllColumns()
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible)
                {
                    AutoFitColumnWidth(i);
                }
            }
        }

        public async Task SortColumnAsync(int colIndex, SortDirection direction)
        {
            if (_dataSource == null || colIndex < 0 || colIndex >= _columns.Count) return;
            var col = _columns[colIndex];
            if (direction == SortDirection.None)
            {
                col.SortOrder = SortDirection.None;
                _rowIndexMap.ResetIdentity(_dataSource.TotalRowCount);
                InvalidateVisual();
                return;
            }

            System.Collections.Generic.IComparer<int> comp;
            if (_dataSource is IZeroSortableSource sortable)
            {
                comp = new SortableSourceComparer(sortable, colIndex, direction);
            }
            else
            {
                comp = new FastComparisonComparer((a, b) =>
                {
                    CellValueBuffer bufA = new CellValueBuffer();
                    CellValueBuffer bufB = new CellValueBuffer();
                    _dataSource.GetCellValue(a, colIndex, ref bufA);
                    _dataSource.GetCellValue(b, colIndex, ref bufB);
                    int cmp = bufA.Text.CompareTo(bufB.Text, StringComparison.Ordinal);
                    return direction == SortDirection.Ascending ? cmp : -cmp;
                });
            }

            col.SortOrder = direction;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (i != colIndex) _columns[i].SortOrder = SortDirection.None;
            }

            int count = _rowIndexMap.ActiveCount;
            if (count <= 1)
            {
                InvalidateVisual();
                return;
            }

            _isSorting = true;
            _sortingColumnIndex = colIndex;
            SortingStarted?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();

            _sortCts?.Cancel();
            _sortCts = new System.Threading.CancellationTokenSource();
            var token = _sortCts.Token;

            int[] working = new int[count];
            for (int i = 0; i < count; i++) working[i] = _rowIndexMap[i];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await Task.Run(() =>
                {
                    Array.Sort(working, comp);
                }, token);

                if (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < count; i++)
                    {
                        _rowIndexMap[i] = working[i];
                    }
                    _scrollY = 0;
                    sw.Stop();
                    SortingCompleted?.Invoke(this, sw.Elapsed);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isSorting = false;
                _sortingColumnIndex = -1;
                InvalidateVisual();
            }
        }

        protected virtual void ShowHeaderContextMenu(int columnIndex, Point location)
        {
            if (columnIndex < 0 || columnIndex >= _columns.Count) return;
            var col = _columns[columnIndex];

            var menu = new ContextMenu
            {
                Style = ZeroWpfStyles.ContextMenuStyle
            };

            // 1. Sort Ascending
            var itemAsc = new MenuItem
            {
                Header = "▲  Sort Ascending",
                IsChecked = col.SortOrder == SortDirection.Ascending,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemAsc.Click += (s, e) => _ = SortColumnAsync(columnIndex, SortDirection.Ascending);
            menu.Items.Add(itemAsc);

            // 2. Sort Descending
            var itemDesc = new MenuItem
            {
                Header = "▼  Sort Descending",
                IsChecked = col.SortOrder == SortDirection.Descending,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemDesc.Click += (s, e) => _ = SortColumnAsync(columnIndex, SortDirection.Descending);
            menu.Items.Add(itemDesc);

            // 3. Clear Sorting
            var itemClear = new MenuItem
            {
                Header = "✕  Clear Sorting",
                IsEnabled = col.SortOrder != SortDirection.None,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemClear.Click += (s, e) => _ = SortColumnAsync(columnIndex, SortDirection.None);
            menu.Items.Add(itemClear);

            menu.Items.Add(new Separator { Style = ZeroWpfStyles.SeparatorStyle });

            // 4. Best Fit
            var itemFit = new MenuItem
            {
                Header = "↔  Best Fit Column",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemFit.Click += (s, e) => AutoFitColumnWidth(columnIndex);
            menu.Items.Add(itemFit);

            var itemFitAll = new MenuItem
            {
                Header = "⇹  Best Fit All Columns",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemFitAll.Click += (s, e) => AutoFitAllColumns();
            menu.Items.Add(itemFitAll);

            // 5. Alignment Submenu
            var itemAlign = new MenuItem
            {
                Header = "⬌  Alignment",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            var alignLeft = new MenuItem
            {
                Header = "⬅  Left",
                IsChecked = col.Alignment == CellAlignment.Left,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            alignLeft.Click += (s, e) => { col.Alignment = CellAlignment.Left; InvalidateVisual(); };
            var alignCenter = new MenuItem
            {
                Header = "⬌  Center",
                IsChecked = col.Alignment == CellAlignment.Center,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            alignCenter.Click += (s, e) => { col.Alignment = CellAlignment.Center; InvalidateVisual(); };
            var alignRight = new MenuItem
            {
                Header = "➡  Right",
                IsChecked = col.Alignment == CellAlignment.Right,
                Style = ZeroWpfStyles.MenuItemStyle
            };
            alignRight.Click += (s, e) => { col.Alignment = CellAlignment.Right; InvalidateVisual(); };
            itemAlign.Items.Add(alignLeft);
            itemAlign.Items.Add(alignCenter);
            itemAlign.Items.Add(alignRight);
            menu.Items.Add(itemAlign);

            menu.Items.Add(new Separator { Style = ZeroWpfStyles.SeparatorStyle });

            // 6. Hide Column
            var itemHide = new MenuItem
            {
                Header = $"👁  Hide '{col.HeaderText}'",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemHide.Click += (s, e) =>
            {
                col.IsVisible = false;
                InvalidateVisual();
                _columnChooser?.RefreshColumns();
            };
            menu.Items.Add(itemHide);

            // 7. Show All Columns (if any is hidden)
            bool hasHidden = false;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (!_columns[i].IsVisible) { hasHidden = true; break; }
            }
            if (hasHidden)
            {
                var itemShowAll = new MenuItem
                {
                    Header = "📋  Show All Columns",
                    Style = ZeroWpfStyles.MenuItemStyle
                };
                itemShowAll.Click += (s, e) =>
                {
                    for (int i = 0; i < _columns.Count; i++) _columns[i].IsVisible = true;
                    InvalidateVisual();
                    _columnChooser?.RefreshColumns();
                };
                menu.Items.Add(itemShowAll);
            }

            menu.Items.Add(new Separator { Style = ZeroWpfStyles.SeparatorStyle });

            // 8. Filter
            var itemFilter = new MenuItem
            {
                Header = "🔍  Filter...",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemFilter.Click += (s, e) => ShowColumnFilterPopup(columnIndex);
            menu.Items.Add(itemFilter);

            // 9. Column Chooser
            var itemChooser = new MenuItem
            {
                Header = "⚙️  Column Chooser...",
                Style = ZeroWpfStyles.MenuItemStyle
            };
            itemChooser.Click += (s, e) => ShowColumnChooser();
            menu.Items.Add(itemChooser);

            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            menu.HorizontalOffset = location.X;
            menu.VerticalOffset = location.Y;
            menu.IsOpen = true;
        }

        private sealed class FastComparisonComparer : System.Collections.Generic.IComparer<int>
        {
            private readonly Comparison<int> _comparison;
            public FastComparisonComparer(Comparison<int> comparison) => _comparison = comparison;
            public int Compare(int x, int y) => _comparison(x, y);
        }

        private sealed class SortableSourceComparer : System.Collections.Generic.IComparer<int>
        {
            private readonly IZeroSortableSource _source;
            private readonly int _columnIndex;
            private readonly SortDirection _direction;

            public SortableSourceComparer(IZeroSortableSource source, int columnIndex, SortDirection direction)
            {
                _source = source;
                _columnIndex = columnIndex;
                _direction = direction;
            }

            public int Compare(int x, int y)
            {
                int cmp = _source.CompareRows(x, y, _columnIndex);
                return _direction == SortDirection.Ascending ? cmp : -cmp;
            }
        }
    }
}
