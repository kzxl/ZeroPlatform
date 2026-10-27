using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Data
{
    public class TreeListShowingEditorEventArgs : CancelEventArgs
    {
        public ZeroTreeNode Node { get; }
        public TreeListColumn Column { get; }

        public TreeListShowingEditorEventArgs(ZeroTreeNode node, TreeListColumn column)
        {
            Node = node;
            Column = column;
        }
    }

    public class TreeListCellValueChangedEventArgs : EventArgs
    {
        public ZeroTreeNode Node { get; }
        public TreeListColumn Column { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public TreeListCellValueChangedEventArgs(ZeroTreeNode node, TreeListColumn column, object? oldValue, object? newValue)
        {
            Node = node;
            Column = column;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class TreeListValidatingEditorEventArgs : CancelEventArgs
    {
        public ZeroTreeNode Node { get; }
        public TreeListColumn Column { get; }
        public object? Value { get; set; }
        public string? ErrorText { get; set; }

        public TreeListValidatingEditorEventArgs(ZeroTreeNode node, TreeListColumn column, object? value)
        {
            Node = node;
            Column = column;
            Value = value;
        }
    }

    public partial class ZTreeList
    {
        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Enables in-place cell editing on double-click or F2 key.")]
        public bool AllowInPlaceEditing
        {
            get => _allowInPlaceEditing;
            set
            {
                if (_allowInPlaceEditing != value)
                {
                    _allowInPlaceEditing = value;
                    if (!_allowInPlaceEditing && IsEditing)
                    {
                        CloseEditor(false);
                    }
                }
            }
        }

        [Browsable(false)]
        public bool IsEditing => _activeEditor != null;

        [Browsable(false)]
        public Control? ActiveEditor => _activeEditor;

        [Browsable(false)]
        public ZeroTreeNode? EditingNode => _editingNode;

        [Browsable(false)]
        public TreeListColumn? EditingColumn => _editingColumn;

        /// <summary>
        /// Begins editing the specified node and column cell.
        /// </summary>
        public bool ShowEditor(ZeroTreeNode node, TreeListColumn column)
        {
            if (!_allowInPlaceEditing || node == null || column == null || !column.AllowEdit)
            {
                return false;
            }

            if (IsEditing)
            {
                if (!CloseEditor(true)) return false;
            }

            var showArgs = new TreeListShowingEditorEventArgs(node, column);
            ShowingEditor?.Invoke(this, showArgs);
            if (showArgs.Cancel) return false;

            int colIdx = _columns.IndexOf(column);
            if (colIdx < 0) return false;

            Rectangle cellRect = GetCellBounds(node, colIdx);
            if (cellRect.IsEmpty) return false;

            _editingNode = node;
            _editingColumn = column;

            var tb = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Font = Font,
                BackColor = ZeroTheme.Colors.BgInput,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Bounds = new Rectangle(cellRect.X, cellRect.Y + 2, Math.Max(20, cellRect.Width), Math.Max(20, cellRect.Height - 4)),
                Text = node[column.Name]?.ToString() ?? ""
            };

            tb.KeyDown += OnEditorKeyDown;
            tb.LostFocus += OnEditorLostFocus;

            _activeEditor = tb;
            Controls.Add(tb);
            tb.BringToFront();
            tb.Focus();
            tb.SelectAll();

            return true;
        }

        /// <summary>
        /// Closes the currently active in-place cell editor.
        /// </summary>
        public bool CloseEditor(bool saveChanges)
        {
            if (_activeEditor == null || _editingNode == null || _editingColumn == null)
            {
                return true;
            }

            var editor = _activeEditor;
            var node = _editingNode;
            var col = _editingColumn;

            if (saveChanges)
            {
                string newText = editor.Text;
                object? oldVal = node[col.Name];

                var valArgs = new TreeListValidatingEditorEventArgs(node, col, newText);
                ValidatingEditor?.Invoke(this, valArgs);
                if (valArgs.Cancel)
                {
                    editor.Focus();
                    return false;
                }

                node[col.Name] = valArgs.Value;
                if (col == _columns[0] && (node[col.Name] != null))
                {
                    node.Text = valArgs.Value?.ToString() ?? node.Text;
                }

                CellValueChanged?.Invoke(this, new TreeListCellValueChangedEventArgs(node, col, oldVal, valArgs.Value));

                if (col.RollupMode != TreeListRollupMode.None)
                {
                    RecalculateRollups();
                }
                if (_showFooter && col.SummaryType != TreeListSummaryType.None)
                {
                    RecalculateSummaries();
                }
            }

            editor.KeyDown -= OnEditorKeyDown;
            editor.LostFocus -= OnEditorLostFocus;
            Controls.Remove(editor);
            editor.Dispose();

            _activeEditor = null;
            _editingNode = null;
            _editingColumn = null;

            HiddenEditor?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return true;
        }

        private Rectangle GetCellBounds(ZeroTreeNode node, int columnIndex)
        {
            int nodeVisualIndex = _visibleNodes.IndexOf(node);
            if (nodeVisualIndex < 0 || columnIndex < 0 || columnIndex >= _columns.Count)
            {
                return Rectangle.Empty;
            }

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            int y = headerOffset + (nodeVisualIndex - _scrollOffset) * _rowHeight;

            int x = 0;
            for (int i = 0; i < columnIndex; i++)
            {
                if (_columns[i].Visible) x += _columns[i].Width;
            }

            var targetCol = _columns[columnIndex];
            if (!targetCol.Visible) return Rectangle.Empty;

            int colW = targetCol.Width;
            if (columnIndex == 0)
            {
                // Account for tree chevron and indent in column 0
                int indentX = 12 + (node.Level * _indentWidth) + 18;
                if (_showCheckBoxes) indentX += 22;
                if (!string.IsNullOrEmpty(node.Icon)) indentX += 20;

                int availW = Math.Max(20, (x + colW) - indentX - 4);
                return new Rectangle(indentX, y, availW, _rowHeight);
            }

            return new Rectangle(x + 2, y, Math.Max(20, colW - 4), _rowHeight);
        }

        private void OnEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CloseEditor(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                Focus();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CloseEditor(false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                Focus();
            }
            else if (e.KeyCode == Keys.Tab && _editingNode != null && _editingColumn != null)
            {
                var currNode = _editingNode;
                int currColIdx = _columns.IndexOf(_editingColumn);
                if (CloseEditor(true))
                {
                    // Move to next visible and editable column
                    for (int nextCol = currColIdx + 1; nextCol < _columns.Count; nextCol++)
                    {
                        if (_columns[nextCol].Visible && _columns[nextCol].AllowEdit)
                        {
                            ShowEditor(currNode, _columns[nextCol]);
                            break;
                        }
                    }
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void OnEditorLostFocus(object? sender, EventArgs e)
        {
            // Auto-commit edit when editor loses focus to grid or other controls
            if (IsEditing)
            {
                CloseEditor(true);
            }
        }
    }
}
