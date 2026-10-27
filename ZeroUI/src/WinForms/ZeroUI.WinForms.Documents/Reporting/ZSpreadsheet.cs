using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Spreadsheet;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Reporting
{
    /// <summary>
    /// Vector Spreadsheet and Formula Calculation Grid Control for ZeroUI WinForms.
    /// Provides zero-dependency sparse matrix data storage, live formula evaluation (=SUM, AVERAGE, MIN, MAX, IF),
    /// interactive Formula Bar, vector column/row headers, interactive column/row resizing, in-place cell editing,
    /// and reactive ZeroTheme skinning.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Reporting & Documents")]
    [DefaultProperty("Worksheet")]
    [Description("Vector spreadsheet and formula calculation grid control")]
    [ToolboxBitmap(typeof(ZeroIcons), "GridControl.bmp")]
    public class ZSpreadsheet : Control
    {
        private SpreadsheetWorksheet _worksheet;
        private CellAddress _activeCell = new CellAddress(0, 0);
        private CellRange _selectionRange = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0));

        // Layout constants & state
        private int _rowHeaderWidth = 50;
        private int _colHeaderHeight = 26;
        private int _scrollRow = 0;
        private int _scrollCol = 0;
        private int _frozenRows = 0;
        private int _frozenColumns = 0;

        // UI Controls
        private readonly Panel _toolbarPanel;
        private readonly Label _lblActiveCell;
        private readonly Label _lblFx;
        private readonly TextBox _txtFormulaBar;
        private readonly ZButton _btnAutoSum;
        private readonly ZButton _btnRecalc;
        private readonly ZButton _btnFreeze;
        private readonly ZButton _btnCurrency;
        private readonly ZButton _btnPercent;
        private readonly ZButton _btnSampleBom;
        private readonly ZButton _btnSampleQc;

        private readonly GridCanvasPanel _canvas;
        private readonly VScrollBar _vScrollBar;
        private readonly HScrollBar _hScrollBar;
        private readonly TextBox _inPlaceEditor;

        // Header resizing state
        private bool _isResizingCol = false;
        private bool _isResizingRow = false;
        private int _resizingColIndex = -1;
        private int _resizingRowIndex = -1;
        private int _resizeStartPos = 0;
        private double _resizeInitialSize = 0;

        // Mouse selection state
        private bool _isSelecting = false;
        private CellAddress? _dragStartCell = null;

        // Events
        public event EventHandler<CellAddress>? ActiveCellChanged;
        public event EventHandler<CellRange>? SelectionChanged;
        public event EventHandler<CellAddress>? CellValueChanged;

        [Category("Data")]
        [Description("The active spreadsheet worksheet model.")]
        public SpreadsheetWorksheet Worksheet
        {
            get => _worksheet;
            set
            {
                _worksheet = value ?? SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();
                _activeCell = new CellAddress(0, 0);
                _selectionRange = new CellRange(_activeCell, _activeCell);
                _scrollRow = 0;
                _scrollCol = 0;
                _frozenRows = 0;
                _frozenColumns = 0;
                UpdateFreezeButtonText();
                UpdateFormulaBar();
                UpdateScrollBars();
                _canvas.Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Number of rows frozen at the top of the worksheet.")]
        public int FrozenRows
        {
            get => _frozenRows;
            set
            {
                int val = Math.Max(0, value);
                if (_frozenRows != val)
                {
                    _frozenRows = val;
                    if (_scrollRow < _frozenRows)
                        _scrollRow = _frozenRows;
                    UpdateFreezeButtonText();
                    UpdateScrollBars();
                    _canvas.Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Number of columns frozen at the left of the worksheet.")]
        public int FrozenColumns
        {
            get => _frozenColumns;
            set
            {
                int val = Math.Max(0, value);
                if (_frozenColumns != val)
                {
                    _frozenColumns = val;
                    if (_scrollCol < _frozenColumns)
                        _scrollCol = _frozenColumns;
                    UpdateFreezeButtonText();
                    UpdateScrollBars();
                    _canvas.Invalidate();
                }
            }
        }

        public void FreezePanes(int rows, int cols)
        {
            _frozenRows = Math.Max(0, rows);
            _frozenColumns = Math.Max(0, cols);
            if (_scrollRow < _frozenRows) _scrollRow = _frozenRows;
            if (_scrollCol < _frozenColumns) _scrollCol = _frozenColumns;
            UpdateFreezeButtonText();
            UpdateScrollBars();
            _canvas.Invalidate();
        }

        public void UnfreezePanes()
        {
            _frozenRows = 0;
            _frozenColumns = 0;
            UpdateFreezeButtonText();
            UpdateScrollBars();
            _canvas.Invalidate();
        }

        public void ToggleFreezePanes()
        {
            if (_frozenRows > 0 || _frozenColumns > 0)
            {
                UnfreezePanes();
            }
            else
            {
                FreezePanes(_activeCell.Row, _activeCell.Column);
            }
        }

        private int GetFrozenWidth()
        {
            int w = 0;
            int count = Math.Min(_frozenColumns, _worksheet.ColumnCount + 50);
            for (int c = 0; c < count; c++)
            {
                w += (int)_worksheet.GetColumnWidth(c);
            }
            return w;
        }

        private int GetFrozenHeight()
        {
            int h = 0;
            int count = Math.Min(_frozenRows, _worksheet.RowCount + 100);
            for (int r = 0; r < count; r++)
            {
                h += (int)_worksheet.GetRowHeight(r);
            }
            return h;
        }

        private void UpdateFreezeButtonText()
        {
            if (_btnFreeze != null)
            {
                _btnFreeze.Text = (_frozenRows > 0 || _frozenColumns > 0) ? "❄ Unfreeze" : "❄ Freeze";
            }
        }

        [Category("Behavior")]
        [Description("The currently active cell address.")]
        public CellAddress ActiveCell
        {
            get => _activeCell;
            set
            {
                if (!_activeCell.Equals(value))
                {
                    _activeCell = value;
                    _selectionRange = new CellRange(_activeCell, _activeCell);
                    EnsureCellVisible(_activeCell);
                    UpdateFormulaBar();
                    _canvas.Invalidate();
                    ActiveCellChanged?.Invoke(this, _activeCell);
                    SelectionChanged?.Invoke(this, _selectionRange);
                }
            }
        }

        [Category("Behavior")]
        [Description("The currently selected cell range.")]
        public CellRange SelectionRange
        {
            get => _selectionRange;
            set
            {
                _selectionRange = value;
                _canvas.Invalidate();
                SelectionChanged?.Invoke(this, _selectionRange);
            }
        }

        public ZSpreadsheet()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;

            // Default model: Factory Costing BOM
            _worksheet = SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();

            // 1. In-Place Cell Editor & Canvas (initialized first)
            _inPlaceEditor = new TextBox
            {
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };
            _inPlaceEditor.KeyDown += OnInPlaceEditorKeyDown;
            _inPlaceEditor.LostFocus += (s, e) => CommitInPlaceEdit();

            _canvas = new GridCanvasPanel(this)
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Background
            };
            _canvas.Controls.Add(_inPlaceEditor);

            // 2. Formula & Toolbar Panel
            _toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = ZeroTheme.Colors.Surface
            };
            _toolbarPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border);
                e.Graphics.DrawLine(pen, 0, _toolbarPanel.Height - 1, _toolbarPanel.Width, _toolbarPanel.Height - 1);
            };

            _lblActiveCell = new Label
            {
                Location = new Point(8, 7),
                Size = new Size(54, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.Primary,
                BackColor = ZeroTheme.Colors.HeaderBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "A1"
            };

            _lblFx = new Label
            {
                Location = new Point(66, 7),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Georgia", 11f, FontStyle.Italic | FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Text = "fx"
            };

            _txtFormulaBar = new TextBox
            {
                Location = new Point(94, 8),
                Size = new Size(230, 23),
                Font = new Font("Consolas", 10f),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtFormulaBar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitFormulaBar();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _canvas.Focus();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    UpdateFormulaBar();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _canvas.Focus();
                }
            };
            _txtFormulaBar.LostFocus += (s, e) => CommitFormulaBar();

            _btnAutoSum = new ZButton
            {
                Location = new Point(332, 6),
                Size = new Size(64, 26),
                Text = "∑ Sum"
            };
            _btnAutoSum.Click += (s, e) => InsertAutoSum();

            _btnRecalc = new ZButton
            {
                Location = new Point(402, 6),
                Size = new Size(64, 26),
                Text = "⚡ Calc"
            };
            _btnRecalc.Click += (s, e) =>
            {
                _worksheet.RecalculateAllFormulas();
                UpdateFormulaBar();
                _canvas.Invalidate();
            };

            _btnFreeze = new ZButton
            {
                Location = new Point(472, 6),
                Size = new Size(80, 26),
                Text = "❄ Freeze"
            };
            _btnFreeze.Click += (s, e) => ToggleFreezePanes();

            _btnCurrency = new ZButton
            {
                Location = new Point(558, 6),
                Size = new Size(32, 26),
                Text = "$"
            };
            _btnCurrency.Click += (s, e) => SetActiveCellFormat(SpreadsheetFormatType.Currency);

            _btnPercent = new ZButton
            {
                Location = new Point(594, 6),
                Size = new Size(32, 26),
                Text = "%"
            };
            _btnPercent.Click += (s, e) => SetActiveCellFormat(SpreadsheetFormatType.Percentage);

            _btnSampleBom = new ZButton
            {
                Location = new Point(634, 6),
                Size = new Size(122, 26),
                Text = "🏭 Costing BOM"
            };
            _btnSampleBom.Click += (s, e) =>
            {
                Worksheet = SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();
            };

            _btnSampleQc = new ZButton
            {
                Location = new Point(762, 6),
                Size = new Size(128, 26),
                Text = "🔬 QC Inspection"
            };
            _btnSampleQc.Click += (s, e) =>
            {
                Worksheet = SpreadsheetSampleGenerator.CreateQualitySpcWorksheet();
            };

            _toolbarPanel.Controls.Add(_lblActiveCell);
            _toolbarPanel.Controls.Add(_lblFx);
            _toolbarPanel.Controls.Add(_txtFormulaBar);
            _toolbarPanel.Controls.Add(_btnAutoSum);
            _toolbarPanel.Controls.Add(_btnRecalc);
            _toolbarPanel.Controls.Add(_btnFreeze);
            _toolbarPanel.Controls.Add(_btnCurrency);
            _toolbarPanel.Controls.Add(_btnPercent);
            _toolbarPanel.Controls.Add(_btnSampleBom);
            _toolbarPanel.Controls.Add(_btnSampleQc);

            // 3. ScrollBars
            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Width = 16
            };
            _vScrollBar.ValueChanged += (s, e) =>
            {
                _scrollRow = Math.Max(_frozenRows, _vScrollBar.Value);
                _canvas.Invalidate();
            };

            _hScrollBar = new HScrollBar
            {
                Dock = DockStyle.Bottom,
                Height = 16
            };
            _hScrollBar.ValueChanged += (s, e) =>
            {
                _scrollCol = Math.Max(_frozenColumns, _hScrollBar.Value);
                _canvas.Invalidate();
            };

            // Assemble layout
            Controls.Add(_canvas);
            Controls.Add(_vScrollBar);
            Controls.Add(_hScrollBar);
            Controls.Add(_toolbarPanel);

            // Reactive Theme
            ZeroTheme.ThemeChanged += OnThemeChanged;

            Size = new Size(880, 560);
            UpdateFormulaBar();
            UpdateScrollBars();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            BackColor = ZeroTheme.Colors.Background;
            if (_toolbarPanel != null) _toolbarPanel.BackColor = ZeroTheme.Colors.Surface;
            if (_lblActiveCell != null)
            {
                _lblActiveCell.ForeColor = ZeroTheme.Colors.Primary;
                _lblActiveCell.BackColor = ZeroTheme.Colors.HeaderBackground;
            }
            if (_lblFx != null) _lblFx.ForeColor = ZeroTheme.Colors.TextSecondary;
            if (_txtFormulaBar != null)
            {
                _txtFormulaBar.BackColor = ZeroTheme.Colors.Surface;
                _txtFormulaBar.ForeColor = ZeroTheme.Colors.TextPrimary;
            }
            if (_inPlaceEditor != null)
            {
                _inPlaceEditor.BackColor = ZeroTheme.Colors.Surface;
                _inPlaceEditor.ForeColor = ZeroTheme.Colors.TextPrimary;
            }
            if (_canvas != null)
            {
                _canvas.BackColor = ZeroTheme.Colors.Background;
                _canvas.Invalidate();
            }
            _toolbarPanel?.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBars();
        }

        private void UpdateScrollBars()
        {
            if (_vScrollBar == null || _hScrollBar == null || _worksheet == null) return;
            int fRows = Math.Max(0, _frozenRows);
            int fCols = Math.Max(0, _frozenColumns);

            _vScrollBar.Minimum = fRows;
            _vScrollBar.Maximum = Math.Max(100, _worksheet.RowCount + 50);
            _vScrollBar.SmallChange = 1;
            _vScrollBar.LargeChange = 15;
            _vScrollBar.Value = Math.Min(_vScrollBar.Maximum, Math.Max(fRows, _scrollRow));

            _hScrollBar.Minimum = fCols;
            _hScrollBar.Maximum = Math.Max(50, _worksheet.ColumnCount + 20);
            _hScrollBar.SmallChange = 1;
            _hScrollBar.LargeChange = 5;
            _hScrollBar.Value = Math.Min(_hScrollBar.Maximum, Math.Max(fCols, _scrollCol));
        }

        private void EnsureCellVisible(CellAddress cell)
        {
            int fRows = Math.Max(0, _frozenRows);
            int fCols = Math.Max(0, _frozenColumns);

            if (cell.Row >= fRows && cell.Row < _scrollRow)
                _scrollRow = cell.Row;
            if (cell.Column >= fCols && cell.Column < _scrollCol)
                _scrollCol = cell.Column;

            _vScrollBar.Value = Math.Min(_vScrollBar.Maximum, Math.Max(fRows, _scrollRow));
            _hScrollBar.Value = Math.Min(_hScrollBar.Maximum, Math.Max(fCols, _scrollCol));
        }

        private void UpdateFormulaBar()
        {
            _lblActiveCell.Text = _activeCell.Name;
            var cell = _worksheet.GetCell(_activeCell.Row, _activeCell.Column);
            if (cell != null)
            {
                _txtFormulaBar.Text = cell.RawValue ?? string.Empty;
            }
            else
            {
                _txtFormulaBar.Text = string.Empty;
            }
        }

        private void CommitFormulaBar()
        {
            string text = _txtFormulaBar.Text.Trim();
            _worksheet.SetValue(_activeCell.Row, _activeCell.Column, text);
            _worksheet.RecalculateAllFormulas();
            _canvas.Invalidate();
            CellValueChanged?.Invoke(this, _activeCell);
        }

        private void InsertAutoSum()
        {
            int activeRow = _activeCell.Row;
            int activeCol = _activeCell.Column;

            int endRow = activeRow - 1;
            int startRow = endRow;
            while (startRow > 0 && _worksheet.GetCell(startRow - 1, activeCol)?.EvaluatedValue != null)
            {
                startRow--;
            }

            if (endRow >= 0 && startRow <= endRow)
            {
                string rangeRef = $"{CellAddress.ToAddress(startRow, activeCol)}:{CellAddress.ToAddress(endRow, activeCol)}";
                _txtFormulaBar.Text = $"=SUM({rangeRef})";
            }
            else
            {
                _txtFormulaBar.Text = "=SUM(";
            }
            CommitFormulaBar();
        }

        private void SetActiveCellFormat(SpreadsheetFormatType format)
        {
            int r1 = _selectionRange.Start.Row;
            int r2 = _selectionRange.End.Row;
            int c1 = _selectionRange.Start.Column;
            int c2 = _selectionRange.End.Column;

            for (int r = Math.Min(r1, r2); r <= Math.Max(r1, r2); r++)
            {
                for (int c = Math.Min(c1, c2); c <= Math.Max(c1, c2); c++)
                {
                    var cell = _worksheet.GetOrCreateCell(r, c);
                    cell.FormatType = format;
                }
            }
            _canvas.Invalidate();
        }

        private void BeginInPlaceEdit()
        {
            var rect = GetCellRectangle(_activeCell.Row, _activeCell.Column);
            if (!rect.IsEmpty)
            {
                _inPlaceEditor.Location = new Point(rect.X + 1, rect.Y + 1);
                _inPlaceEditor.Size = new Size(Math.Max(50, rect.Width - 2), Math.Max(20, rect.Height - 2));

                var cell = _worksheet.GetCell(_activeCell.Row, _activeCell.Column);
                _inPlaceEditor.Text = cell?.RawValue ?? string.Empty;

                _inPlaceEditor.Visible = true;
                _inPlaceEditor.Focus();
                _inPlaceEditor.SelectAll();
            }
        }

        private void CommitInPlaceEdit()
        {
            if (!_inPlaceEditor.Visible) return;

            string text = _inPlaceEditor.Text.Trim();
            _inPlaceEditor.Visible = false;

            _worksheet.SetValue(_activeCell.Row, _activeCell.Column, text);
            _worksheet.RecalculateAllFormulas();
            UpdateFormulaBar();
            _canvas.Invalidate();
            CellValueChanged?.Invoke(this, _activeCell);
            _canvas.Focus();
        }

        private void OnInPlaceEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitInPlaceEdit();
                ActiveCell = new CellAddress(_activeCell.Row + 1, _activeCell.Column);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                CommitInPlaceEdit();
                ActiveCell = new CellAddress(_activeCell.Row, _activeCell.Column + 1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _inPlaceEditor.Visible = false;
                _canvas.Focus();
                _canvas.Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private Rectangle GetCellRectangle(int row, int col)
        {
            int fCols = Math.Max(0, _frozenColumns);
            int fRows = Math.Max(0, _frozenRows);
            int frozenW = GetFrozenWidth();
            int frozenH = GetFrozenHeight();

            // Column X coordinate
            int x;
            if (col < fCols)
            {
                x = _rowHeaderWidth;
                for (int c = 0; c < col; c++)
                {
                    x += (int)_worksheet.GetColumnWidth(c);
                }
            }
            else
            {
                if (col < _scrollCol)
                    return Rectangle.Empty;

                x = _rowHeaderWidth + frozenW;
                for (int c = Math.Max(fCols, _scrollCol); c < col; c++)
                {
                    x += (int)_worksheet.GetColumnWidth(c);
                    if (x > _canvas.Width) return Rectangle.Empty;
                }
            }

            // Row Y coordinate
            int y;
            if (row < fRows)
            {
                y = _colHeaderHeight;
                for (int r = 0; r < row; r++)
                {
                    y += (int)_worksheet.GetRowHeight(r);
                }
            }
            else
            {
                if (row < _scrollRow)
                    return Rectangle.Empty;

                y = _colHeaderHeight + frozenH;
                for (int r = Math.Max(fRows, _scrollRow); r < row; r++)
                {
                    y += (int)_worksheet.GetRowHeight(r);
                    if (y > _canvas.Height) return Rectangle.Empty;
                }
            }

            int w = (int)_worksheet.GetColumnWidth(col);
            int h = (int)_worksheet.GetRowHeight(row);
            return new Rectangle(x, y, w, h);
        }

        private CellAddress? HitTestCell(Point pt)
        {
            if (pt.X < _rowHeaderWidth || pt.Y < _colHeaderHeight)
                return null;

            int fCols = Math.Max(0, _frozenColumns);
            int fRows = Math.Max(0, _frozenRows);
            int frozenW = GetFrozenWidth();
            int frozenH = GetFrozenHeight();

            int col = -1;
            if (fCols > 0 && pt.X < _rowHeaderWidth + frozenW)
            {
                int x = _rowHeaderWidth;
                for (int c = 0; c < fCols; c++)
                {
                    int cw = (int)_worksheet.GetColumnWidth(c);
                    if (pt.X >= x && pt.X < x + cw)
                    {
                        col = c;
                        break;
                    }
                    x += cw;
                }
            }
            else
            {
                int x = _rowHeaderWidth + frozenW;
                int c = Math.Max(fCols, _scrollCol);
                while (c <= Math.Max(fCols, _scrollCol) + 60)
                {
                    int cw = (int)_worksheet.GetColumnWidth(c);
                    if (pt.X >= x && pt.X < x + cw)
                    {
                        col = c;
                        break;
                    }
                    x += cw;
                    c++;
                }
            }

            int row = -1;
            if (fRows > 0 && pt.Y < _colHeaderHeight + frozenH)
            {
                int y = _colHeaderHeight;
                for (int r = 0; r < fRows; r++)
                {
                    int rh = (int)_worksheet.GetRowHeight(r);
                    if (pt.Y >= y && pt.Y < y + rh)
                    {
                        row = r;
                        break;
                    }
                    y += rh;
                }
            }
            else
            {
                int y = _colHeaderHeight + frozenH;
                int r = Math.Max(fRows, _scrollRow);
                while (r <= Math.Max(fRows, _scrollRow) + 200)
                {
                    int rh = (int)_worksheet.GetRowHeight(r);
                    if (pt.Y >= y && pt.Y < y + rh)
                    {
                        row = r;
                        break;
                    }
                    y += rh;
                    r++;
                }
            }

            if (col < 0 || row < 0) return null;
            return new CellAddress(row, col);
        }

        private int HitTestColumnDivider(Point pt)
        {
            if (pt.Y > _colHeaderHeight) return -1;

            int fCols = Math.Max(0, _frozenColumns);
            int frozenW = GetFrozenWidth();

            int x = _rowHeaderWidth;
            for (int c = 0; c < fCols; c++)
            {
                int cw = (int)_worksheet.GetColumnWidth(c);
                int dividerX = x + cw;
                if (Math.Abs(pt.X - dividerX) <= 4)
                    return c;
                x += cw;
            }

            x = _rowHeaderWidth + frozenW;
            int col = Math.Max(fCols, _scrollCol);
            while (x <= _canvas.Width && col <= Math.Max(fCols, _scrollCol) + 60)
            {
                int cw = (int)_worksheet.GetColumnWidth(col);
                int dividerX = x + cw;
                if (Math.Abs(pt.X - dividerX) <= 4)
                    return col;
                x += cw;
                col++;
            }
            return -1;
        }

        private int HitTestRowDivider(Point pt)
        {
            if (pt.X > _rowHeaderWidth) return -1;

            int fRows = Math.Max(0, _frozenRows);
            int frozenH = GetFrozenHeight();

            int y = _colHeaderHeight;
            for (int r = 0; r < fRows; r++)
            {
                int rh = (int)_worksheet.GetRowHeight(r);
                int dividerY = y + rh;
                if (Math.Abs(pt.Y - dividerY) <= 4)
                    return r;
                y += rh;
            }

            y = _colHeaderHeight + frozenH;
            int row = Math.Max(fRows, _scrollRow);
            while (y <= _canvas.Height && row <= Math.Max(fRows, _scrollRow) + 200)
            {
                int rh = (int)_worksheet.GetRowHeight(row);
                int dividerY = y + rh;
                if (Math.Abs(pt.Y - dividerY) <= 4)
                    return row;
                y += rh;
                row++;
            }
            return -1;
        }

        #region Nested Double-Buffered Canvas Panel
        private class GridCanvasPanel : Panel
        {
            private readonly ZSpreadsheet _owner;

            public GridCanvasPanel(ZSpreadsheet owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.Selectable |
                    ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                TabStop = true;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                base.OnMouseDown(e);

                if (e.Button == MouseButtons.Left)
                {
                    int colDivider = _owner.HitTestColumnDivider(e.Location);
                    if (colDivider >= 0)
                    {
                        _owner._isResizingCol = true;
                        _owner._resizingColIndex = colDivider;
                        _owner._resizeStartPos = e.X;
                        _owner._resizeInitialSize = _owner._worksheet.GetColumnWidth(colDivider);
                        Capture = true;
                        return;
                    }

                    int rowDivider = _owner.HitTestRowDivider(e.Location);
                    if (rowDivider >= 0)
                    {
                        _owner._isResizingRow = true;
                        _owner._resizingRowIndex = rowDivider;
                        _owner._resizeStartPos = e.Y;
                        _owner._resizeInitialSize = _owner._worksheet.GetRowHeight(rowDivider);
                        Capture = true;
                        return;
                    }

                    var cell = _owner.HitTestCell(e.Location);
                    if (cell.HasValue)
                    {
                        _owner.CommitInPlaceEdit();
                        _owner._isSelecting = true;
                        _owner._dragStartCell = cell.Value;
                        _owner.ActiveCell = cell.Value;
                        _owner.SelectionRange = new CellRange(cell.Value, cell.Value);
                        Capture = true;
                    }
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                if (_owner._isResizingCol)
                {
                    int delta = e.X - _owner._resizeStartPos;
                    double newWidth = Math.Max(25.0, _owner._resizeInitialSize + delta);
                    _owner._worksheet.SetColumnWidth(_owner._resizingColIndex, newWidth);
                    Invalidate();
                    return;
                }

                if (_owner._isResizingRow)
                {
                    int delta = e.Y - _owner._resizeStartPos;
                    double newHeight = Math.Max(16.0, _owner._resizeInitialSize + delta);
                    _owner._worksheet.SetRowHeight(_owner._resizingRowIndex, newHeight);
                    Invalidate();
                    return;
                }

                var dragStart = _owner._dragStartCell;
                if (_owner._isSelecting && dragStart.HasValue)
                {
                    var cell = _owner.HitTestCell(e.Location);
                    if (cell.HasValue)
                    {
                        _owner.SelectionRange = new CellRange(dragStart.Value, cell.Value);
                        Invalidate();
                    }
                    return;
                }

                if (_owner.HitTestColumnDivider(e.Location) >= 0)
                {
                    Cursor = Cursors.VSplit;
                }
                else if (_owner.HitTestRowDivider(e.Location) >= 0)
                {
                    Cursor = Cursors.HSplit;
                }
                else
                {
                    Cursor = Cursors.Default;
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_owner._isResizingCol || _owner._isResizingRow || _owner._isSelecting)
                {
                    _owner._isResizingCol = false;
                    _owner._isResizingRow = false;
                    _owner._isSelecting = false;
                    Capture = false;
                    Invalidate();
                }
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);
                if (e.Button == MouseButtons.Left)
                {
                    var cell = _owner.HitTestCell(e.Location);
                    if (cell.HasValue)
                    {
                        _owner.ActiveCell = cell.Value;
                        _owner.BeginInPlaceEdit();
                    }
                }
            }

            protected override bool IsInputKey(Keys keyData)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.Left:
                    case Keys.Right:
                    case Keys.Up:
                    case Keys.Down:
                    case Keys.Tab:
                    case Keys.Enter:
                    case Keys.F2:
                    case Keys.Delete:
                        return true;
                }
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                var active = _owner.ActiveCell;

                if (e.KeyCode == Keys.F2)
                {
                    _owner.BeginInPlaceEdit();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    _owner._worksheet.ClearCell(active.Row, active.Column);
                    _owner._worksheet.RecalculateAllFormulas();
                    _owner.UpdateFormulaBar();
                    Invalidate();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Up && active.Row > 0)
                {
                    _owner.ActiveCell = new CellAddress(active.Row - 1, active.Column);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    _owner.ActiveCell = new CellAddress(active.Row + 1, active.Column);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Left && active.Column > 0)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column - 1);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Right)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column + 1);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    _owner.ActiveCell = new CellAddress(active.Row + 1, active.Column);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Tab)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column + 1);
                    e.Handled = true;
                }
                else if (!e.Control && !e.Alt && e.KeyValue >= 32 && e.KeyValue <= 126)
                {
                    _owner.BeginInPlaceEdit();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var bounds = ClientRectangle;
                var colors = ZeroTheme.Colors;

                using var bgBrush = new SolidBrush(colors.Background);
                g.FillRectangle(bgBrush, bounds);

                int headerW = _owner._rowHeaderWidth;
                int headerH = _owner._colHeaderHeight;

                using var gridPen = new Pen(colors.Border);
                using var headerBgBrush = new SolidBrush(colors.HeaderBackground);
                using var headerTextBrush = new SolidBrush(colors.TextSecondary);
                using var regularFont = new Font("Segoe UI", 9.25f);
                using var boldFont = new Font("Segoe UI", 9.25f, FontStyle.Bold);
                using var headerFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                using var errorBrush = new SolidBrush(colors.Danger);

                // 1. Calculate visible column positions
                var visibleCols = new List<(int Col, int X, int Width)>();
                int fCols = Math.Max(0, _owner.FrozenColumns);
                int frozenWidth = 0;
                int curX = headerW;

                for (int fc = 0; fc < fCols; fc++)
                {
                    int w = (int)_owner._worksheet.GetColumnWidth(fc);
                    visibleCols.Add((fc, curX, w));
                    curX += w;
                    frozenWidth += w;
                    if (curX > bounds.Width) break;
                }

                int startCol = Math.Max(fCols, _owner._scrollCol);
                int c = startCol;
                while (curX < bounds.Width && c <= startCol + 100)
                {
                    int w = (int)_owner._worksheet.GetColumnWidth(c);
                    visibleCols.Add((c, curX, w));
                    curX += w;
                    c++;
                }

                // 2. Calculate visible row positions
                var visibleRows = new List<(int Row, int Y, int Height)>();
                int fRows = Math.Max(0, _owner.FrozenRows);
                int frozenHeight = 0;
                int curY = headerH;

                for (int fr = 0; fr < fRows; fr++)
                {
                    int h = (int)_owner._worksheet.GetRowHeight(fr);
                    visibleRows.Add((fr, curY, h));
                    curY += h;
                    frozenHeight += h;
                    if (curY > bounds.Height) break;
                }

                int startRow = Math.Max(fRows, _owner._scrollRow);
                int r = startRow;
                while (curY < bounds.Height && r <= startRow + 200)
                {
                    int h = (int)_owner._worksheet.GetRowHeight(r);
                    visibleRows.Add((r, curY, h));
                    curY += h;
                    r++;
                }

                // 3. Render Data Cells
                foreach (var rowInfo in visibleRows)
                {
                    foreach (var colInfo in visibleCols)
                    {
                        var cellRect = new Rectangle(colInfo.X, rowInfo.Y, colInfo.Width, rowInfo.Height);
                        var cell = _owner._worksheet.GetCell(rowInfo.Row, colInfo.Col);

                        // Fill cell background if defined (alpha != 0)
                        if (cell != null && (cell.BackgroundColor & 0xFF000000) != 0)
                        {
                            using var cBrush = new SolidBrush(Color.FromArgb((int)cell.BackgroundColor));
                            g.FillRectangle(cBrush, cellRect);
                        }

                        // Draw cell borders
                        g.DrawRectangle(gridPen, cellRect);

                        // Render cell text
                        if (cell != null)
                        {
                            string text = cell.FormattedText;
                            if (!string.IsNullOrEmpty(text))
                            {
                                var font = cell.IsBold ? boldFont : regularFont;
                                Brush textBrush;
                                if (cell.HasError)
                                {
                                    textBrush = errorBrush;
                                }
                                else if ((cell.TextColor & 0xFF000000) != 0)
                                {
                                    textBrush = new SolidBrush(Color.FromArgb((int)cell.TextColor));
                                }
                                else
                                {
                                    textBrush = new SolidBrush(colors.TextPrimary);
                                }

                                var sf = new StringFormat
                                {
                                    LineAlignment = StringAlignment.Center,
                                    Trimming = StringTrimming.EllipsisCharacter,
                                    FormatFlags = StringFormatFlags.NoWrap
                                };

                                switch (cell.Alignment)
                                {
                                    case SpreadsheetAlignment.Center:
                                        sf.Alignment = StringAlignment.Center;
                                        break;
                                    case SpreadsheetAlignment.Right:
                                        sf.Alignment = StringAlignment.Far;
                                        break;
                                    default:
                                        sf.Alignment = StringAlignment.Near;
                                        break;
                                }

                                var textRect = new Rectangle(cellRect.X + 4, cellRect.Y + 1, cellRect.Width - 8, cellRect.Height - 2);
                                g.DrawString(text, font, textBrush, textRect, sf);

                                if (!cell.HasError && (cell.TextColor & 0xFF000000) != 0)
                                    textBrush.Dispose();
                                sf.Dispose();
                            }
                        }
                    }
                }

                // 4. Render Selection Highlight Box
                var sel = _owner._selectionRange;
                int selMinRow = Math.Min(sel.Start.Row, sel.End.Row);
                int selMaxRow = Math.Max(sel.Start.Row, sel.End.Row);
                int selMinCol = Math.Min(sel.Start.Column, sel.End.Column);
                int selMaxCol = Math.Max(sel.Start.Column, sel.End.Column);

                int selX1 = -1, selY1 = -1, selX2 = -1, selY2 = -1;
                foreach (var colInfo in visibleCols)
                {
                    if (colInfo.Col == selMinCol) selX1 = colInfo.X;
                    if (colInfo.Col == selMaxCol) selX2 = colInfo.X + colInfo.Width;
                }
                foreach (var rowInfo in visibleRows)
                {
                    if (rowInfo.Row == selMinRow) selY1 = rowInfo.Y;
                    if (rowInfo.Row == selMaxRow) selY2 = rowInfo.Y + rowInfo.Height;
                }

                if (selX1 >= 0 && selY1 >= 0 && selX2 >= 0 && selY2 >= 0)
                {
                    var selRect = new Rectangle(selX1, selY1, selX2 - selX1, selY2 - selY1);

                    // Translucent fill
                    using var selFill = new SolidBrush(Color.FromArgb(32, colors.Primary));
                    g.FillRectangle(selFill, selRect);

                    // 2px Sapphire / Primary border
                    using var selPen = new Pen(colors.Primary, 2f);
                    g.DrawRectangle(selPen, selRect);

                    // Bottom-right handle square
                    using var handleBrush = new SolidBrush(colors.Primary);
                    g.FillRectangle(handleBrush, selRect.Right - 3, selRect.Bottom - 3, 5, 5);
                }

                // 4.5. Render Freeze Divider Lines
                if (fCols > 0 && frozenWidth > 0)
                {
                    int splitX = headerW + frozenWidth;
                    using var freezePen = new Pen(colors.Primary, 2.5f);
                    g.DrawLine(freezePen, splitX, 0, splitX, bounds.Height);
                }
                if (fRows > 0 && frozenHeight > 0)
                {
                    int splitY = headerH + frozenHeight;
                    using var freezePen = new Pen(colors.Primary, 2.5f);
                    g.DrawLine(freezePen, 0, splitY, bounds.Width, splitY);
                }

                // 5. Render Column Headers (Top Strip)
                using (var colHeaderFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    foreach (var colInfo in visibleCols)
                    {
                        var colRect = new Rectangle(colInfo.X, 0, colInfo.Width, headerH);
                        g.FillRectangle(headerBgBrush, colRect);
                        g.DrawRectangle(gridPen, colRect);

                        string colName = CellAddress.ColumnIndexToLetters(colInfo.Col);
                        g.DrawString(colName, headerFont, headerTextBrush, colRect, colHeaderFormat);
                    }
                }

                // 6. Render Row Headers (Left Strip)
                using (var rowHeaderFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    foreach (var rowInfo in visibleRows)
                    {
                        var rowRect = new Rectangle(0, rowInfo.Y, headerW, rowInfo.Height);
                        g.FillRectangle(headerBgBrush, rowRect);
                        g.DrawRectangle(gridPen, rowRect);

                        // 1-based display for row headers
                        g.DrawString((rowInfo.Row + 1).ToString(CultureInfo.InvariantCulture), headerFont, headerTextBrush, rowRect, rowHeaderFormat);
                    }
                }

                // 7. Top-Left Corner Box
                var cornerRect = new Rectangle(0, 0, headerW, headerH);
                g.FillRectangle(headerBgBrush, cornerRect);
                g.DrawRectangle(gridPen, cornerRect);

                // Small diagonal triangle in corner
                using var cornerPen = new Pen(colors.TextSecondary, 1.5f);
                g.DrawLine(cornerPen, headerW - 8, headerH - 3, headerW - 3, headerH - 8);
                g.DrawLine(cornerPen, headerW - 5, headerH - 3, headerW - 3, headerH - 5);
            }
        }
        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSpreadsheet"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SpreadsheetControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSpreadsheet instead.")]
    [ToolboxItem(false)]
    public class SpreadsheetControl : ZSpreadsheet
    {
    }

    #endregion
}
