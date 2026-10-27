using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    public partial class ZGrid
    {
        private void UpdateEditorTheme()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)UpdateEditorTheme);
                return;
            }
            _inPlaceEditor.Background = ZeroWpfTheme.BgInput;
            _inPlaceEditor.Foreground = ZeroWpfTheme.TextPrimary;
            _inPlaceEditor.BorderBrush = ZeroWpfTheme.PrimaryAccent;
            _inPlaceEditor.CaretBrush = ZeroWpfTheme.PrimaryAccent;

            _maskedEditor.Background = ZeroWpfTheme.BgInput;
            _maskedEditor.Foreground = ZeroWpfTheme.TextPrimary;
            _maskedEditor.BorderBrush = ZeroWpfTheme.PrimaryAccent;
            _maskedEditor.CaretBrush = ZeroWpfTheme.PrimaryAccent;
        }

        private void ToggleBooleanCell(int visualRow, int colIndex)
        {
            if (_dataSource == null || visualRow < 0 || visualRow >= VisualRowCount || colIndex < 0 || colIndex >= _columns.Count) return;
            var col = _columns[colIndex];
            if (col.ReadOnly || !col.IsVisible) return;

            int modelRow = GetModelRowIndex(visualRow);
            if (modelRow < 0) return;
            if (_dataSource is IZeroEditableSource editable && !editable.IsCellEditable(modelRow, colIndex)) return;

            CellValueBuffer buf = new CellValueBuffer();
            _dataSource.GetCellValue(modelRow, colIndex, ref buf);
            bool isTrue = IsTruthy(buf.Text);
            string newVal = isTrue ? "false" : "true";

            if (_dataSource is IZeroEditableSource editableSrc)
            {
                editableSrc.SetCellValue(modelRow, colIndex, newVal);
                CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(visualRow, modelRow, colIndex, buf.Text.ToString(), newVal));
                InvalidateVisual();
            }
        }

        public Rect GetCellRectangle(int visualRow, int colIndex)
        {
            if (visualRow < 0 || colIndex < 0 || colIndex >= _columns.Count) return Rect.Empty;

            double cellY = TotalTopOffset + (visualRow * _rowHeight) - _scrollY;
            int pinnedW = GetPinnedColumnsWidth();

            double cellX;
            if (_columns[colIndex].IsPinned)
            {
                cellX = 0;
                for (int c = 0; c < colIndex; c++)
                {
                    if (_columns[c].IsVisible && _columns[c].IsPinned) cellX += _columns[c].Width;
                }
            }
            else
            {
                cellX = pinnedW - _scrollX;
                for (int c = 0; c < colIndex; c++)
                {
                    if (_columns[c].IsVisible && !_columns[c].IsPinned) cellX += _columns[c].Width;
                }
            }

            return new Rect(cellX, cellY, _columns[colIndex].Width, _rowHeight);
        }

        public void StartEdit(int visualRow, int colIndex)
        {
            if (_dataSource == null || visualRow < 0 || visualRow >= VisualRowCount ||
                colIndex < 0 || colIndex >= _columns.Count) return;

            if (_groupedMap.HasGrouping && visualRow < _groupedMap.ActiveCount && _groupedMap[visualRow].IsGroup) return;

            var col = _columns[colIndex];
            if (col.ReadOnly || !col.IsVisible) return;

            int modelRow = GetModelRowIndex(visualRow);
            if (modelRow < 0) return;

            if (_dataSource is IZeroEditableSource editable && !editable.IsCellEditable(modelRow, colIndex))
            {
                return;
            }

            if (_isEditing) CommitEdit();

            EnsureRowVisible(visualRow);

            var rect = GetCellRectangle(visualRow, colIndex);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            CellValueBuffer buf = new CellValueBuffer();
            _dataSource.GetCellValue(modelRow, colIndex, ref buf);
            string val = buf.Text.ToString();

            _isEditing = true;
            _editingVisualRow = visualRow;
            _editingColIndex = colIndex;

            FrameworkElement editor;
            if (col.ColumnType == GridColumnType.Masked || !string.IsNullOrEmpty(col.Mask))
            {
                _maskedEditor.Mask = col.Mask ?? "";
                _maskedEditor.Text = val;
                editor = _maskedEditor;
            }
            else if (col.ColumnType == GridColumnType.Numeric)
            {
                if (decimal.TryParse(val.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
                {
                    _numericEditor.Value = numVal;
                }
                else
                {
                    _numericEditor.Value = 0;
                }
                editor = _numericEditor;
            }
            else if (col.ColumnType == GridColumnType.DateTime)
            {
                if (DateTime.TryParse(val, out var dtVal))
                {
                    _dateEditor.SelectedDate = dtVal;
                }
                else
                {
                    _dateEditor.SelectedDate = DateTime.Today;
                }
                editor = _dateEditor;
            }
            else
            {
                _inPlaceEditor.Text = val;
                editor = _inPlaceEditor;
            }

            _activeEditor = editor;
            UpdateEditorTheme();
            editor.ToolTip = null;
            if (editor is Control ctrl) ctrl.BorderBrush = ZeroWpfTheme.PrimaryAccent;
            editor.Visibility = Visibility.Visible;
            editor.Arrange(rect);
            editor.Focus();
            if (editor is TextBox tb) tb.SelectAll();

            CellBeginEdit?.Invoke(this, EventArgs.Empty);
        }

        public void CommitEdit()
        {
            if (!_isEditing || _dataSource == null || _activeEditor == null) return;

            int visualRow = _editingVisualRow;
            int colIndex = _editingColIndex;
            if (visualRow < 0 || visualRow >= VisualRowCount || colIndex < 0 || colIndex >= _columns.Count)
            {
                CancelEdit();
                return;
            }

            int modelRow = GetModelRowIndex(visualRow);
            if (modelRow < 0)
            {
                CancelEdit();
                return;
            }

            var col = _columns[colIndex];
            string newText = string.Empty;
            if (_activeEditor is ZeroMaskedTextBox mtb)
            {
                newText = mtb.Text;
            }
            else if (_activeEditor is TextBox tb)
            {
                newText = tb.Text;
            }
            else if (_activeEditor is ZeroNumericBox nb)
            {
                newText = nb.Value.ToString(CultureInfo.InvariantCulture);
            }
            else if (_activeEditor is ZeroDatePicker dp)
            {
                newText = dp.SelectedDate.ToString(dp.DateFormat);
            }

            if (col.CustomValidator != null)
            {
                var (isValid, errMsg) = col.CustomValidator(newText);
                if (!isValid)
                {
                    if (_activeEditor is Control c)
                    {
                        c.ToolTip = errMsg ?? "Validation failed";
                        c.BorderBrush = ZeroWpfTheme.DangerAccent;
                    }
                    _activeEditor.Focus();
                    return;
                }
            }

            _activeEditor.Visibility = Visibility.Collapsed;
            _activeEditor = null;
            _isEditing = false;
            _editingVisualRow = -1;
            _editingColIndex = -1;

            CellValueBuffer buf = new CellValueBuffer();
            _dataSource.GetCellValue(modelRow, colIndex, ref buf);
            string oldText = buf.Text.ToString();

            if (oldText != newText)
            {
                if (_dataSource is IZeroEditableSource editable)
                {
                    editable.SetCellValue(modelRow, colIndex, newText);
                }
                CellValueChanged?.Invoke(this, new CellValueChangedEventArgs(visualRow, modelRow, colIndex, oldText, newText));
                InvalidateVisual();
            }

            CellEndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void CancelEdit()
        {
            if (!_isEditing) return;

            if (_activeEditor != null)
            {
                _activeEditor.Visibility = Visibility.Collapsed;
                _activeEditor = null;
            }
            _isEditing = false;
            _editingVisualRow = -1;
            _editingColIndex = -1;

            CellEndEdit?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            Focus();
        }

        private void InPlaceEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                if (_selectedVisualRow < VisualRowCount - 1)
                {
                    _selectedVisualRow++;
                    _selectedVisualRows.Clear();
                    _selectedVisualRows.Add(_selectedVisualRow);
                    EnsureRowVisible(_selectedVisualRow);
                    InvalidateVisual();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                int nextCol = _editingColIndex + 1;
                int nextRow = _editingVisualRow;
                CommitEdit();
                while (nextCol < _columns.Count && (_columns[nextCol].ReadOnly || !_columns[nextCol].IsVisible))
                {
                    nextCol++;
                }
                if (nextCol < _columns.Count)
                {
                    StartEdit(nextRow, nextCol);
                }
                e.Handled = true;
            }
        }
    }
}
