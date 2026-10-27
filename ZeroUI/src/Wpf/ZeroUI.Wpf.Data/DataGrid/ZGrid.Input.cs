using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    public partial class ZGrid
    {
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            _currentMousePos = pt;
            int footerH = ShowFooter ? _footerHeight : 0;
            int topOffset = TotalTopOffset;
            int groupPanelH = ShowGroupPanel ? _groupPanelHeight : 0;

            if (e.LeftButton == MouseButtonState.Pressed && _draggedHeaderCol >= 0 && !_isDraggingHeader && !_isResizingColumn)
            {
                if (Math.Abs(pt.X - _headerDragStart.X) > 6 || Math.Abs(pt.Y - _headerDragStart.Y) > 6)
                {
                    _isDraggingHeader = true;
                }
            }

            if (_isDraggingHeader)
            {
                if (_allowColumnReordering && pt.Y >= groupPanelH)
                {
                    _reorderDropTargetIndex = HitTestColumnDropTarget(pt.X);
                    Mouse.OverrideCursor = Cursors.SizeWE;
                }
                else
                {
                    _reorderDropTargetIndex = -1;
                    Mouse.OverrideCursor = null;
                }
                InvalidateVisual();
                return;
            }

            if (_isDraggingVThumb)
            {
                double clientH = Math.Max(0, ActualHeight - topOffset - footerH);
                int totalRows = VisualRowCount;
                int totalH = totalRows * _rowHeight;
                double maxScroll = totalH - clientH;
                double trackH = clientH;
                double thumbH = Math.Max(20, (clientH / totalH) * trackH);

                double deltaY = pt.Y - _dragThumbStartY;
                double availableTrack = trackH - thumbH;
                if (availableTrack > 0)
                {
                    double scrollDelta = (deltaY / availableTrack) * maxScroll;
                    _scrollY = (int)Math.Max(0, Math.Min(maxScroll, _dragScrollStartY + scrollDelta));
                    if (_isEditing) InvalidateArrange();
                    InvalidateVisual();
                }
                return;
            }

            if (_isSelectingBlock && _selectionMode == ZeroGridSelectionMode.Block)
            {
                int clickedCol = HitTestColumn(pt.X);
                int vRow = (int)((pt.Y - topOffset + _scrollY) / _rowHeight);
                vRow = Math.Max(0, Math.Min(VisualRowCount - 1, vRow));
                clickedCol = Math.Max(0, Math.Min(_columns.Count - 1, clickedCol));
                if (vRow != _selectedBlock.EndRow || clickedCol != _selectedBlock.EndColumn)
                {
                    _selectedBlock = new CellRange(_selectedBlock.StartRow, _selectedBlock.StartColumn, vRow, clickedCol);
                    InvalidateVisual();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            if (_isResizingColumn)
            {
                double delta = pt.X - _resizeStartX;
                int newW = (int)Math.Max(30, _resizeStartWidth + delta);
                if (_resizingColIndex >= 0 && _resizingColIndex < _columns.Count)
                {
                    _columns[_resizingColIndex].Width = newW;
                    if (_isEditing) InvalidateArrange();
                    InvalidateVisual();
                }
                return;
            }

            // Check scrollbar hover
            bool prevVThumbHover = _isVThumbHovered;
            _isVThumbHovered = (pt.X >= ActualWidth - ScrollBarThickness && pt.Y >= topOffset && pt.Y < ActualHeight - footerH);
            if (prevVThumbHover != _isVThumbHovered) InvalidateVisual();

            // Check header column resize handle
            if (pt.Y >= groupPanelH && pt.Y <= topOffset)
            {
                int colIdx = HitTestColumnDivider(pt.X);
                if (colIdx >= 0)
                {
                    Cursor = Cursors.SizeWE;
                    return;
                }
                Cursor = Cursors.Arrow;
                return;
            }

            Cursor = Cursors.Arrow;

            // Row hover
            int visualRow = (int)((pt.Y - topOffset + _scrollY) / _rowHeight);
            if (visualRow >= 0 && visualRow < VisualRowCount && pt.Y < ActualHeight - footerH)
            {
                if (_hoveredVisualRow != visualRow)
                {
                    _hoveredVisualRow = visualRow;
                    InvalidateVisual();
                }
            }
            else if (_hoveredVisualRow != -1)
            {
                _hoveredVisualRow = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredVisualRow != -1 || _isVThumbHovered)
            {
                _hoveredVisualRow = -1;
                _isVThumbHovered = false;
                InvalidateVisual();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point pt = e.GetPosition(this);
            int footerH = ShowFooter ? _footerHeight : 0;
            int topOffset = TotalTopOffset;
            int groupPanelH = ShowGroupPanel ? _groupPanelHeight : 0;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 1. Group Panel Chip close button click
                if (ShowGroupPanel && pt.Y < groupPanelH)
                {
                    for (int i = 0; i < _groupChipBounds.Count; i++)
                    {
                        var chip = _groupChipBounds[i];
                        if (chip.CloseRect.Contains(pt))
                        {
                            var remaining = new List<int>(_groupColumnIndices);
                            remaining.Remove(chip.ColumnIndex);
                            if (remaining.Count > 0) GroupBy(remaining.ToArray());
                            else ClearGrouping();
                            return;
                        }
                    }
                    return;
                }

                // 2. Filter button click on column headers
                foreach (var kvp in _columnFilterButtonBounds)
                {
                    if (kvp.Value.Contains(pt))
                    {
                        _pendingFilterColumn = kvp.Key;
                        e.Handled = true;
                        return;
                    }
                }

                // 3. Double click cell editing
                if (e.ClickCount == 2 && pt.Y > topOffset && pt.Y < ActualHeight - footerH)
                {
                    int vRow = (int)((pt.Y - topOffset + _scrollY) / _rowHeight);
                    int col = HitTestColumn(pt.X);
                    if (vRow >= 0 && vRow < VisualRowCount && col >= 0)
                    {
                        if (_groupedMap.HasGrouping && vRow < _groupedMap.ActiveCount && _groupedMap[vRow].IsGroup)
                        {
                            _groupedMap.ToggleGroup(vRow);
                            InvalidateVisual();
                            return;
                        }
                        StartEdit(vRow, col);
                        return;
                    }
                }

                // 4. Check ScrollBar thumb click
                if (pt.X >= ActualWidth - ScrollBarThickness && pt.Y >= topOffset && pt.Y < ActualHeight - footerH)
                {
                    _isDraggingVThumb = true;
                    _dragThumbStartY = pt.Y;
                    _dragScrollStartY = _scrollY;
                    CaptureMouse();
                    return;
                }

                // 5. Check Column Header click or resize / drag
                if (pt.Y >= groupPanelH && pt.Y <= topOffset)
                {
                    if (_isEditing) CommitEdit();

                    int dividerCol = HitTestColumnDivider(pt.X);
                    if (dividerCol >= 0)
                    {
                        _isResizingColumn = true;
                        _resizingColIndex = dividerCol;
                        _resizeStartX = pt.X;
                        _resizeStartWidth = _columns[dividerCol].Width;
                        CaptureMouse();
                        return;
                    }

                    int col = HitTestColumn(pt.X);
                    if (col >= 0)
                    {
                        _draggedHeaderCol = col;
                        _headerDragStart = pt;
                        CaptureMouse();
                    }
                    return;
                }

                // 6. Row click selection or Master-Detail toggle
                int visualRow = (int)((pt.Y - topOffset + _scrollY) / _rowHeight);
                int clickedCol = HitTestColumn(pt.X);
                if (visualRow >= 0 && visualRow < VisualRowCount && pt.Y < ActualHeight - footerH)
                {
                    if (_groupedMap.HasGrouping && visualRow < _groupedMap.ActiveCount && _groupedMap[visualRow].IsGroup)
                    {
                        _groupedMap.ToggleGroup(visualRow);
                        InvalidateVisual();
                        return;
                    }

                    int modelRow = GetModelRowIndex(visualRow);
                    if (_allowMasterDetail && pt.X < 24 && modelRow >= 0)
                    {
                        ToggleMasterRow(modelRow);
                        return;
                    }

                    if (clickedCol >= 0 && clickedCol < _columns.Count && _columns[clickedCol].ColumnType == GridColumnType.Boolean)
                    {
                        ToggleBooleanCell(visualRow, clickedCol);
                        _selectedVisualRow = visualRow;
                        _selectedVisualRows.Clear();
                        _selectedVisualRows.Add(visualRow);
                        InvalidateVisual();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    if (_isEditing)
                    {
                        if (visualRow != _editingVisualRow || clickedCol != _editingColIndex)
                        {
                            CommitEdit();
                        }
                    }

                    if (_selectionMode == ZeroGridSelectionMode.Block)
                    {
                        _selectedBlock = new CellRange(visualRow, clickedCol, visualRow, clickedCol);
                        _isSelectingBlock = true;
                        CaptureMouse();
                        InvalidateVisual();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    if (_selectionMode == ZeroGridSelectionMode.MultiRow)
                    {
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        {
                            if (_isAllSelected)
                            {
                                if (_deselectedVisualRows.Contains(visualRow))
                                    _deselectedVisualRows.Remove(visualRow);
                                else
                                    _deselectedVisualRows.Add(visualRow);
                            }
                            else
                            {
                                if (_selectedVisualRows.Contains(visualRow))
                                    _selectedVisualRows.Remove(visualRow);
                                else
                                    _selectedVisualRows.Add(visualRow);
                            }
                            _selectedVisualRow = visualRow;
                        }
                        else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && _selectedVisualRow >= 0)
                        {
                            int start = (_selectedVisualRow >= 0) ? _selectedVisualRow : visualRow;
                            int min = Math.Min(start, visualRow);
                            int max = Math.Max(start, visualRow);
                            long span = (long)max - min + 1;
                            if (span >= VisualRowCount && VisualRowCount > 0)
                            {
                                SelectAllRows();
                            }
                            else
                            {
                                _isAllSelected = false;
                                _deselectedVisualRows.Clear();
                                _selectedVisualRows.Clear();
                                if (span > 100_000)
                                {
                                    _isAllSelected = true;
                                    for (int r = 0; r < min; r++) _deselectedVisualRows.Add(r);
                                    for (int r = max + 1; r < VisualRowCount; r++) _deselectedVisualRows.Add(r);
                                }
                                else
                                {
                                    for (int r = min; r <= max; r++) _selectedVisualRows.Add(r);
                                }
                            }
                        }
                        else
                        {
                            _isAllSelected = false;
                            _deselectedVisualRows.Clear();
                            _selectedVisualRows.Clear();
                            _selectedVisualRows.Add(visualRow);
                            _selectedVisualRow = visualRow;
                        }
                    }
                    else
                    {
                        _isAllSelected = false;
                        _deselectedVisualRows.Clear();
                        _selectedVisualRows.Clear();
                        _selectedVisualRows.Add(visualRow);
                        _selectedVisualRow = visualRow;
                    }

                    InvalidateVisual();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pendingFilterColumn >= 0 && e.ChangedButton == MouseButton.Left)
            {
                int pendingCol = _pendingFilterColumn;
                _pendingFilterColumn = -1;
                ShowColumnFilterPopup(pendingCol);
                e.Handled = true;
                return;
            }
            if (_isDraggingHeader)
            {
                _isDraggingHeader = false;
                Mouse.OverrideCursor = null;
                ReleaseMouseCapture();
                Point pt = e.GetPosition(this);
                int groupPanelH = ShowGroupPanel ? _groupPanelHeight : 0;
                if (ShowGroupPanel && pt.Y < groupPanelH && _draggedHeaderCol >= 0)
                {
                    if (Array.IndexOf(_groupColumnIndices, _draggedHeaderCol) < 0)
                    {
                        int[] newCols = new int[_groupColumnIndices.Length + 1];
                        Array.Copy(_groupColumnIndices, newCols, _groupColumnIndices.Length);
                        newCols[_groupColumnIndices.Length] = _draggedHeaderCol;
                        GroupBy(newCols);
                    }
                }
                else if (_allowColumnReordering && _reorderDropTargetIndex >= 0 && _draggedHeaderCol >= 0 && _draggedHeaderCol != _reorderDropTargetIndex && _draggedHeaderCol < _columns.Count)
                {
                    var col = _columns[_draggedHeaderCol];
                    _columns.RemoveAt(_draggedHeaderCol);
                    int targetIdx = Math.Min(_columns.Count, _reorderDropTargetIndex);
                    _columns.Insert(targetIdx, col);
                }
                _draggedHeaderCol = -1;
                _reorderDropTargetIndex = -1;
                InvalidateVisual();
                return;
            }
            if (_draggedHeaderCol >= 0)
            {
                int col = _draggedHeaderCol;
                _draggedHeaderCol = -1;
                ReleaseMouseCapture();
                ColumnHeaderClicked?.Invoke(this, col);
            }
            if (_isDraggingVThumb)
            {
                _isDraggingVThumb = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
            if (_isSelectingBlock)
            {
                _isSelectingBlock = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
            if (_isResizingColumn)
            {
                _isResizingColumn = false;
                _resizingColIndex = -1;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            Point pt = e.GetPosition(this);
            int topOffset = TotalTopOffset;
            int groupPanelH = ShowGroupPanel ? _groupPanelHeight : 0;

            if (pt.Y >= groupPanelH && pt.Y <= topOffset)
            {
                int col = HitTestColumn(pt.X);
                if (col >= 0)
                {
                    ShowHeaderContextMenu(col, pt);
                    e.Handled = true;
                }
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int scrollAmount = (e.Delta / 120) * _rowHeight * 3;

            if (_enableAdaptiveHighRefresh)
            {
                Interlocked.Add(ref _pendingScrollDeltaY, scrollAmount);
                _hasPendingScroll = true;
                EnsureClockSubscribed();
                return;
            }

            _scrollY = Math.Max(0, Math.Min(GetMaxScrollY(), _scrollY - scrollAmount));
            if (_isEditing) InvalidateArrange();
            InvalidateVisual();
        }


        private void EnsureClockSubscribed()
        {
            if (!_isClockSubscribed)
            {
                _isClockSubscribed = true;
                _idleScrollFrames = 0;
                ZeroAnimationClock.Subscribe((IAnimationFrameListener)this);
            }
        }


        private void RemoveClockSubscribed()
        {
            if (_isClockSubscribed)
            {
                _isClockSubscribed = false;
                ZeroAnimationClock.Unsubscribe((IAnimationFrameListener)this);
            }
        }

        void IAnimationFrameListener.OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            if (!IsLoaded)
            {
                RemoveClockSubscribed();
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(_flushScrollConflationAction);
                return;
            }

            FlushScrollConflation();
        }

        private void FlushScrollConflation()
        {
            if (!_hasPendingScroll)
            {
                _idleScrollFrames++;
                if (_idleScrollFrames > 2)
                {
                    RemoveClockSubscribed();
                }
                return;
            }

            int dy = Interlocked.Exchange(ref _pendingScrollDeltaY, 0);
            _hasPendingScroll = false;
            _idleScrollFrames = 0;

            if (dy != 0)
            {
                int targetY = Math.Max(0, Math.Min(GetMaxScrollY(), _scrollY - dy));
                if (targetY != _scrollY)
                {
                    _scrollY = targetY;
                    if (_isEditing) InvalidateArrange();
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Invalidates visual rendering for the specified cell when data updates.

        public void InvalidateCell(int visualRow, int columnIndex)
        {
            InvalidateVisual();
        }

        /// <summary>
        /// Invalidates visual rendering for the specified row.

        public void InvalidateRow(int visualRow)
        {
            InvalidateVisual();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.A)
            {
                if (_selectionMode == ZeroGridSelectionMode.MultiRow)
                {
                    SelectAllRows();
                    e.Handled = true;
                    return;
                }
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
            {
                CopySelectionToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.F2 || (e.Key == Key.Enter && !_isEditing))
            {
                if (_selectedVisualRow >= 0 && _selectedVisualRow < VisualRowCount)
                {
                    for (int c = 0; c < _columns.Count; c++)
                    {
                        if (_columns[c].IsVisible && !_columns[c].ReadOnly)
                        {
                            StartEdit(_selectedVisualRow, c);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            else if (e.Key == Key.Up && _selectedVisualRow > 0)
            {
                if (_isEditing) CommitEdit();
                _selectedVisualRow--;
                _isAllSelected = false;
                _deselectedVisualRows.Clear();
                _selectedVisualRows.Clear();
                _selectedVisualRows.Add(_selectedVisualRow);
                EnsureRowVisible(_selectedVisualRow);
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _selectedVisualRow < VisualRowCount - 1)
            {
                if (_isEditing) CommitEdit();
                _selectedVisualRow++;
                _isAllSelected = false;
                _deselectedVisualRows.Clear();
                _selectedVisualRows.Clear();
                _selectedVisualRows.Add(_selectedVisualRow);
                EnsureRowVisible(_selectedVisualRow);
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        public void EnsureRowVisible(int visualRowIndex)
        {
            if (visualRowIndex < 0 || visualRowIndex >= VisualRowCount) return;
            int rowTop = visualRowIndex * _rowHeight;
            int rowBottom = rowTop + _rowHeight;
            int footerH = ShowFooter ? _footerHeight : 0;
            int viewH = (int)Math.Max(0, ActualHeight - TotalTopOffset - footerH);

            if (rowTop < _scrollY)
            {
                _scrollY = rowTop;
                InvalidateVisual();
            }
            else if (rowBottom > _scrollY + viewH)
            {
                _scrollY = rowBottom - viewH;
                InvalidateVisual();
            }
        }

        private int HitTestColumnDivider(double mouseX)
        {
            int pinnedW = GetPinnedColumnsWidth();
            if (mouseX < pinnedW)
            {
                double curX = 0;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || !_columns[i].IsPinned) continue;
                    curX += _columns[i].Width;
                    if (Math.Abs(mouseX - curX) <= 4) return i;
                }
            }
            else
            {
                double curX = pinnedW - _scrollX;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || _columns[i].IsPinned) continue;
                    curX += _columns[i].Width;
                    if (Math.Abs(mouseX - curX) <= 4) return i;
                }
            }
            return -1;
        }

        private int HitTestColumn(double mouseX)
        {
            int pinnedW = GetPinnedColumnsWidth();
            if (mouseX < pinnedW)
            {
                double curX = 0;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || !_columns[i].IsPinned) continue;
                    if (mouseX >= curX && mouseX < curX + _columns[i].Width) return i;
                    curX += _columns[i].Width;
                }
            }
            else
            {
                double curX = pinnedW - _scrollX;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || _columns[i].IsPinned) continue;
                    if (mouseX >= curX && mouseX < curX + _columns[i].Width) return i;
                    curX += _columns[i].Width;
                }
            }
            return -1;
        }

        private int HitTestColumnDropTarget(double mouseX)
        {
            int pinnedW = GetPinnedColumnsWidth();
            if (mouseX < pinnedW)
            {
                double curX = 0;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || !_columns[i].IsPinned) continue;
                    double colW = _columns[i].Width;
                    if (mouseX < curX + colW / 2.0) return i;
                    curX += colW;
                }
            }
            else
            {
                double curX = pinnedW - _scrollX;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (!_columns[i].IsVisible || _columns[i].IsPinned) continue;
                    double colW = _columns[i].Width;
                    if (mouseX < curX + colW / 2.0) return i;
                    curX += colW;
                }
            }
            return Math.Max(0, _columns.Count - 1);
        }

        private double GetColumnHeaderScreenX(int colIndex)
        {
            int pinnedW = GetPinnedColumnsWidth();
            if (colIndex < 0 || colIndex >= _columns.Count) return pinnedW;

            if (_columns[colIndex].IsPinned)
            {
                double x = 0;
                for (int i = 0; i < colIndex; i++)
                {
                    if (_columns[i].IsVisible && _columns[i].IsPinned)
                    {
                        x += _columns[i].Width;
                    }
                }
                return x;
            }
            else
            {
                double x = pinnedW - _scrollX;
                for (int i = 0; i < colIndex; i++)
                {
                    if (_columns[i].IsVisible && !_columns[i].IsPinned)
                    {
                        x += _columns[i].Width;
                    }
                }
                return x;
            }
        }
    }
}
