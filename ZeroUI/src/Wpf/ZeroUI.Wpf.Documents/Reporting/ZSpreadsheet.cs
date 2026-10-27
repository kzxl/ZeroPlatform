using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Spreadsheet;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Reporting
{
    /// <summary>
    /// Vector Spreadsheet and Live Formula Calculation Grid Control for ZeroUI WPF.
    /// Provides sparse matrix data storage, live formula evaluation (=SUM, AVERAGE, MIN, MAX, IF),
    /// interactive Formula Bar, vector column/row headers with interactive resizing, in-place cell editing,
    /// and reactive ZeroWpfTheme skinning.
    /// </summary>
    public class ZSpreadsheet : Control
    {
        private SpreadsheetWorksheet _worksheet;
        private CellAddress _activeCell = new CellAddress(0, 0);
        private CellRange _selectionRange = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0));

        // Layout constants & scroll offsets
        private double _rowHeaderWidth = 52.0;
        private double _colHeaderHeight = 26.0;
        private int _scrollRow = 0;
        private int _scrollCol = 0;

        // UI Controls
        private Grid? _rootGrid;
        private Border? _toolbar;
        private TextBlock? _lblActiveCell;
        private TextBox? _txtFormulaBar;
        private SpreadsheetCanvas? _canvas;
        private ScrollBar? _vScrollBar;
        private ScrollBar? _hScrollBar;
        private TextBox? _inPlaceEditor;

        // Header resizing state
        private bool _isResizingCol = false;
        private bool _isResizingRow = false;
        private int _resizingColIndex = -1;
        private int _resizingRowIndex = -1;
        private double _resizeStartPos = 0;
        private double _resizeInitialSize = 0;

        // Mouse selection state
        private bool _isSelecting = false;
        private CellAddress? _dragStartCell = null;

        // Events
        public event EventHandler<CellAddress>? ActiveCellChanged;
        public event EventHandler<CellRange>? SelectionChanged;
        public event EventHandler<CellAddress>? CellValueChanged;

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
                UpdateFormulaBar();
                UpdateScrollBars();
                _canvas?.InvalidateVisual();
            }
        }

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
                    _canvas?.InvalidateVisual();
                    ActiveCellChanged?.Invoke(this, _activeCell);
                    SelectionChanged?.Invoke(this, _selectionRange);
                }
            }
        }

        public CellRange SelectionRange
        {
            get => _selectionRange;
            set
            {
                _selectionRange = value;
                _canvas?.InvalidateVisual();
                SelectionChanged?.Invoke(this, _selectionRange);
            }
        }

        public ZSpreadsheet()
        {
            Background = ZeroWpfTheme.BgPrimary;
            ClipToBounds = true;
            Focusable = true;

            // Default model: Factory Costing BOM
            _worksheet = SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();

            BuildVisualTree();

            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            UpdateFormulaBar();
            UpdateScrollBars();
        }

        private void BuildVisualTree()
        {
            var rootGrid = new Grid();
            _rootGrid = rootGrid;
            AddLogicalChild(rootGrid);
            AddVisualChild(rootGrid);

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Grid

            // 1. Build Toolbar & Formula Bar
            _toolbar = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 5, 8, 5)
            };

            var barPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var cellBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = 54,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center
            };
            _lblActiveCell = new TextBlock
            {
                Text = "A1",
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            cellBorder.Child = _lblActiveCell;
            barPanel.Children.Add(cellBorder);

            var lblFx = new TextBlock
            {
                Text = "fx",
                FontFamily = new FontFamily("Georgia"),
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            barPanel.Children.Add(lblFx);

            _txtFormulaBar = new TextBox
            {
                Width = 260,
                Height = 26,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _txtFormulaBar.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitFormulaBar();
                    e.Handled = true;
                    _canvas?.Focus();
                }
                else if (e.Key == Key.Escape)
                {
                    UpdateFormulaBar();
                    e.Handled = true;
                    _canvas?.Focus();
                }
            };
            _txtFormulaBar.LostFocus += (s, e) => CommitFormulaBar();
            barPanel.Children.Add(_txtFormulaBar);

            barPanel.Children.Add(CreateSeparator());

            barPanel.Children.Add(CreateToolbarButton("∑ Sum", (s, e) => InsertAutoSum()));
            barPanel.Children.Add(CreateToolbarButton("⚡ Calc", (s, e) =>
            {
                _worksheet.RecalculateAllFormulas();
                UpdateFormulaBar();
                _canvas?.InvalidateVisual();
            }));
            barPanel.Children.Add(CreateToolbarButton("$", (s, e) => SetActiveCellFormat(SpreadsheetFormatType.Currency)));
            barPanel.Children.Add(CreateToolbarButton("%", (s, e) => SetActiveCellFormat(SpreadsheetFormatType.Percentage)));

            barPanel.Children.Add(CreateSeparator());

            barPanel.Children.Add(CreateToolbarButton("🏭 Costing BOM", (s, e) =>
            {
                Worksheet = SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();
            }));
            barPanel.Children.Add(CreateToolbarButton("🔬 QC Inspection", (s, e) =>
            {
                Worksheet = SpreadsheetSampleGenerator.CreateQualitySpcWorksheet();
            }));

            _toolbar.Child = barPanel;
            Grid.SetRow(_toolbar, 0);
            rootGrid.Children.Add(_toolbar);

            // 2. Build Grid Canvas Viewport with ScrollBars
            var viewportGrid = new Grid();
            viewportGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            viewportGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // HScrollBar
            viewportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            viewportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // VScrollBar

            _canvas = new SpreadsheetCanvas(this);

            _vScrollBar = new ScrollBar
            {
                Orientation = Orientation.Vertical,
                Width = 14
            };
            _vScrollBar.ValueChanged += (s, e) =>
            {
                _scrollRow = Math.Max(0, (int)_vScrollBar.Value);
                _canvas.InvalidateVisual();
            };
            Grid.SetRow(_vScrollBar, 0);
            Grid.SetColumn(_vScrollBar, 1);
            viewportGrid.Children.Add(_vScrollBar);

            _hScrollBar = new ScrollBar
            {
                Orientation = Orientation.Horizontal,
                Height = 14
            };
            _hScrollBar.ValueChanged += (s, e) =>
            {
                _scrollCol = Math.Max(0, (int)_hScrollBar.Value);
                _canvas.InvalidateVisual();
            };
            Grid.SetRow(_hScrollBar, 1);
            Grid.SetColumn(_hScrollBar, 0);
            viewportGrid.Children.Add(_hScrollBar);

            // In-place editor placed inside canvas
            _inPlaceEditor = new TextBox
            {
                Visibility = Visibility.Collapsed,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.PrimaryAccent,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(3, 1, 3, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _inPlaceEditor.KeyDown += OnInPlaceEditorKeyDown;
            _inPlaceEditor.LostFocus += (s, e) => CommitInPlaceEdit();

            var canvasGrid = new Grid();
            canvasGrid.Children.Add(_canvas);
            canvasGrid.Children.Add(_inPlaceEditor);

            Grid.SetRow(canvasGrid, 0);
            Grid.SetColumn(canvasGrid, 0);
            viewportGrid.Children.Add(canvasGrid);

            Grid.SetRow(viewportGrid, 1);
            rootGrid.Children.Add(viewportGrid);
        }

        private void OnThemeChanged()
        {
            Background = ZeroWpfTheme.BgPrimary;
            if (_toolbar != null)
            {
                _toolbar.Background = ZeroWpfTheme.BgCard;
                _toolbar.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_lblActiveCell != null)
            {
                _lblActiveCell.Foreground = ZeroWpfTheme.PrimaryAccent;
            }
            if (_txtFormulaBar != null)
            {
                _txtFormulaBar.Background = ZeroWpfTheme.BgInput;
                _txtFormulaBar.Foreground = ZeroWpfTheme.TextPrimary;
                _txtFormulaBar.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_inPlaceEditor != null)
            {
                _inPlaceEditor.Background = ZeroWpfTheme.BgInput;
                _inPlaceEditor.Foreground = ZeroWpfTheme.TextPrimary;
                _inPlaceEditor.BorderBrush = ZeroWpfTheme.PrimaryAccent;
            }
            _canvas?.InvalidateVisual();
        }

        private static FrameworkElement CreateSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 18,
                Background = ZeroWpfTheme.BorderDefault,
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Button CreateToolbarButton(string text, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = text,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(8, 2, 8, 2),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5
            };
            btn.Click += onClick;
            return btn;
        }

        protected override int VisualChildrenCount => _rootGrid != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_rootGrid == null || index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _rootGrid;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _rootGrid?.Measure(constraint);
            return _rootGrid?.DesiredSize ?? new Size(880, 560);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootGrid?.Arrange(new Rect(arrangeBounds));
            UpdateScrollBars();
            return arrangeBounds;
        }

        private void UpdateScrollBars()
        {
            if (_vScrollBar != null)
            {
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = Math.Max(100, _worksheet.RowCount + 50);
                _vScrollBar.SmallChange = 1;
                _vScrollBar.LargeChange = 15;
                _vScrollBar.Value = Math.Min(_vScrollBar.Maximum, Math.Max(0, _scrollRow));
            }

            if (_hScrollBar != null)
            {
                _hScrollBar.Minimum = 0;
                _hScrollBar.Maximum = Math.Max(50, _worksheet.ColumnCount + 20);
                _hScrollBar.SmallChange = 1;
                _hScrollBar.LargeChange = 5;
                _hScrollBar.Value = Math.Min(_hScrollBar.Maximum, Math.Max(0, _scrollCol));
            }
        }

        private void EnsureCellVisible(CellAddress cell)
        {
            if (cell.Row < _scrollRow)
                _scrollRow = cell.Row;
            if (cell.Column < _scrollCol)
                _scrollCol = cell.Column;

            if (_vScrollBar != null)
                _vScrollBar.Value = Math.Min(_vScrollBar.Maximum, Math.Max(0, _scrollRow));
            if (_hScrollBar != null)
                _hScrollBar.Value = Math.Min(_hScrollBar.Maximum, Math.Max(0, _scrollCol));
        }

        private void UpdateFormulaBar()
        {
            if (_lblActiveCell != null)
                _lblActiveCell.Text = _activeCell.Name;

            var cell = _worksheet.GetCell(_activeCell.Row, _activeCell.Column);
            if (_txtFormulaBar != null)
            {
                _txtFormulaBar.Text = cell?.RawValue ?? string.Empty;
            }
        }

        private void CommitFormulaBar()
        {
            if (_txtFormulaBar == null) return;
            string text = _txtFormulaBar.Text.Trim();
            _worksheet.SetValue(_activeCell.Row, _activeCell.Column, text);
            _worksheet.RecalculateAllFormulas();
            _canvas?.InvalidateVisual();
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
                if (_txtFormulaBar != null)
                    _txtFormulaBar.Text = $"=SUM({rangeRef})";
            }
            else if (_txtFormulaBar != null)
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
            _canvas?.InvalidateVisual();
        }

        private void BeginInPlaceEdit()
        {
            if (_inPlaceEditor == null) return;
            var rect = GetCellRectangle(_activeCell.Row, _activeCell.Column);
            if (!rect.IsEmpty)
            {
                _inPlaceEditor.Margin = new Thickness(rect.X + 1, rect.Y + 1, 0, 0);
                _inPlaceEditor.Width = Math.Max(50, rect.Width - 2);
                _inPlaceEditor.Height = Math.Max(20, rect.Height - 2);

                var cell = _worksheet.GetCell(_activeCell.Row, _activeCell.Column);
                _inPlaceEditor.Text = cell?.RawValue ?? string.Empty;

                _inPlaceEditor.Visibility = Visibility.Visible;
                _inPlaceEditor.Focus();
                _inPlaceEditor.SelectAll();
            }
        }

        private void CommitInPlaceEdit()
        {
            if (_inPlaceEditor == null || _inPlaceEditor.Visibility != Visibility.Visible) return;

            string text = _inPlaceEditor.Text.Trim();
            _inPlaceEditor.Visibility = Visibility.Collapsed;

            _worksheet.SetValue(_activeCell.Row, _activeCell.Column, text);
            _worksheet.RecalculateAllFormulas();
            UpdateFormulaBar();
            _canvas?.InvalidateVisual();
            CellValueChanged?.Invoke(this, _activeCell);
            _canvas?.Focus();
        }

        private void OnInPlaceEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInPlaceEdit();
                ActiveCell = new CellAddress(_activeCell.Row + 1, _activeCell.Column);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                CommitInPlaceEdit();
                ActiveCell = new CellAddress(_activeCell.Row, _activeCell.Column + 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_inPlaceEditor != null)
                    _inPlaceEditor.Visibility = Visibility.Collapsed;
                _canvas?.Focus();
                _canvas?.InvalidateVisual();
                e.Handled = true;
            }
        }

        private Rect GetCellRectangle(int row, int col)
        {
            if (row < _scrollRow || col < _scrollCol || _canvas == null)
                return Rect.Empty;

            double x = _rowHeaderWidth;
            for (int c = _scrollCol; c < col; c++)
            {
                x += _worksheet.GetColumnWidth(c);
                if (x > _canvas.ActualWidth) return Rect.Empty;
            }

            double y = _colHeaderHeight;
            for (int r = _scrollRow; r < row; r++)
            {
                y += _worksheet.GetRowHeight(r);
                if (y > _canvas.ActualHeight) return Rect.Empty;
            }

            double w = _worksheet.GetColumnWidth(col);
            double h = _worksheet.GetRowHeight(row);
            return new Rect(x, y, w, h);
        }

        private CellAddress? HitTestCell(Point pt)
        {
            if (pt.X < _rowHeaderWidth || pt.Y < _colHeaderHeight)
                return null;

            double x = _rowHeaderWidth;
            int col = _scrollCol;
            while (col <= _scrollCol + 60)
            {
                double cw = _worksheet.GetColumnWidth(col);
                if (pt.X >= x && pt.X < x + cw)
                    break;
                x += cw;
                col++;
            }

            double y = _colHeaderHeight;
            int row = _scrollRow;
            while (row <= _scrollRow + 200)
            {
                double rh = _worksheet.GetRowHeight(row);
                if (pt.Y >= y && pt.Y < y + rh)
                    break;
                y += rh;
                row++;
            }

            return new CellAddress(row, col);
        }

        private int HitTestColumnDivider(Point pt)
        {
            if (pt.Y > _colHeaderHeight || _canvas == null) return -1;

            double x = _rowHeaderWidth;
            int col = _scrollCol;
            while (x <= _canvas.ActualWidth && col <= _scrollCol + 60)
            {
                double cw = _worksheet.GetColumnWidth(col);
                double dividerX = x + cw;
                if (Math.Abs(pt.X - dividerX) <= 4)
                    return col;
                x += cw;
                col++;
            }
            return -1;
        }

        private int HitTestRowDivider(Point pt)
        {
            if (pt.X > _rowHeaderWidth || _canvas == null) return -1;

            double y = _colHeaderHeight;
            int row = _scrollRow;
            while (y <= _canvas.ActualHeight && row <= _scrollRow + 200)
            {
                double rh = _worksheet.GetRowHeight(row);
                double dividerY = y + rh;
                if (Math.Abs(pt.Y - dividerY) <= 4)
                    return row;
                y += rh;
                row++;
            }
            return -1;
        }

        #region Nested SpreadsheetCanvas Element
        private class SpreadsheetCanvas : FrameworkElement
        {
            private readonly ZSpreadsheet _owner;

            public SpreadsheetCanvas(ZSpreadsheet owner)
            {
                _owner = owner;
                Focusable = true;
                ClipToBounds = true;
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                Focus();
                base.OnMouseDown(e);

                if (e.ChangedButton == MouseButton.Left)
                {
                    Point pt = e.GetPosition(this);

                    int colDivider = _owner.HitTestColumnDivider(pt);
                    if (colDivider >= 0)
                    {
                        _owner._isResizingCol = true;
                        _owner._resizingColIndex = colDivider;
                        _owner._resizeStartPos = pt.X;
                        _owner._resizeInitialSize = _owner._worksheet.GetColumnWidth(colDivider);
                        CaptureMouse();
                        return;
                    }

                    int rowDivider = _owner.HitTestRowDivider(pt);
                    if (rowDivider >= 0)
                    {
                        _owner._isResizingRow = true;
                        _owner._resizingRowIndex = rowDivider;
                        _owner._resizeStartPos = pt.Y;
                        _owner._resizeInitialSize = _owner._worksheet.GetRowHeight(rowDivider);
                        CaptureMouse();
                        return;
                    }

                    var cell = _owner.HitTestCell(pt);
                    if (cell.HasValue)
                    {
                        _owner.CommitInPlaceEdit();
                        _owner._isSelecting = true;
                        _owner._dragStartCell = cell.Value;
                        _owner.ActiveCell = cell.Value;
                        _owner.SelectionRange = new CellRange(cell.Value, cell.Value);
                        CaptureMouse();
                    }
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Point pt = e.GetPosition(this);

                if (_owner._isResizingCol)
                {
                    double delta = pt.X - _owner._resizeStartPos;
                    double newWidth = Math.Max(25.0, _owner._resizeInitialSize + delta);
                    _owner._worksheet.SetColumnWidth(_owner._resizingColIndex, newWidth);
                    InvalidateVisual();
                    return;
                }

                if (_owner._isResizingRow)
                {
                    double delta = pt.Y - _owner._resizeStartPos;
                    double newHeight = Math.Max(16.0, _owner._resizeInitialSize + delta);
                    _owner._worksheet.SetRowHeight(_owner._resizingRowIndex, newHeight);
                    InvalidateVisual();
                    return;
                }

                var dragStart = _owner._dragStartCell;
                if (_owner._isSelecting && dragStart.HasValue)
                {
                    var cell = _owner.HitTestCell(pt);
                    if (cell.HasValue)
                    {
                        _owner.SelectionRange = new CellRange(dragStart.Value, cell.Value);
                        InvalidateVisual();
                    }
                    return;
                }

                if (_owner.HitTestColumnDivider(pt) >= 0)
                {
                    Cursor = Cursors.SizeWE;
                }
                else if (_owner.HitTestRowDivider(pt) >= 0)
                {
                    Cursor = Cursors.SizeNS;
                }
                else
                {
                    Cursor = Cursors.Arrow;
                }
            }

            protected override void OnMouseUp(MouseButtonEventArgs e)
            {
                base.OnMouseUp(e);
                if (_owner._isResizingCol || _owner._isResizingRow || _owner._isSelecting)
                {
                    _owner._isResizingCol = false;
                    _owner._isResizingRow = false;
                    _owner._isSelecting = false;
                    ReleaseMouseCapture();
                    InvalidateVisual();
                }
            }

            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    Point pt = e.GetPosition(this);
                    var cell = _owner.HitTestCell(pt);
                    if (cell.HasValue)
                    {
                        _owner.ActiveCell = cell.Value;
                        _owner.BeginInPlaceEdit();
                    }
                }
                base.OnMouseLeftButtonDown(e);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                var active = _owner.ActiveCell;

                if (e.Key == Key.F2)
                {
                    _owner.BeginInPlaceEdit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Delete)
                {
                    _owner._worksheet.ClearCell(active.Row, active.Column);
                    _owner._worksheet.RecalculateAllFormulas();
                    _owner.UpdateFormulaBar();
                    InvalidateVisual();
                    e.Handled = true;
                }
                else if (e.Key == Key.Up && active.Row > 0)
                {
                    _owner.ActiveCell = new CellAddress(active.Row - 1, active.Column);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    _owner.ActiveCell = new CellAddress(active.Row + 1, active.Column);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left && active.Column > 0)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column - 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column + 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    _owner.ActiveCell = new CellAddress(active.Row + 1, active.Column);
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab)
                {
                    _owner.ActiveCell = new CellAddress(active.Row, active.Column + 1);
                    e.Handled = true;
                }
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
                if (bounds.Width <= 0 || bounds.Height <= 0) return;

                dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, bounds);

                double headerW = _owner._rowHeaderWidth;
                double headerH = _owner._colHeaderHeight;
                int startRow = _owner._scrollRow;
                int startCol = _owner._scrollCol;

                // 1. Calculate visible column positions
                var visibleCols = new List<(int Col, double X, double Width)>();
                double curX = headerW;
                int c = startCol;
                while (curX < bounds.Width && c <= startCol + 100)
                {
                    double w = _owner._worksheet.GetColumnWidth(c);
                    visibleCols.Add((c, curX, w));
                    curX += w;
                    c++;
                }

                // 2. Calculate visible row positions
                var visibleRows = new List<(int Row, double Y, double Height)>();
                double curY = headerH;
                int r = startRow;
                while (curY < bounds.Height && r <= startRow + 200)
                {
                    double h = _owner._worksheet.GetRowHeight(r);
                    visibleRows.Add((r, curY, h));
                    curY += h;
                    r++;
                }

                // 3. Render Data Cells
                foreach (var rowInfo in visibleRows)
                {
                    foreach (var colInfo in visibleCols)
                    {
                        var cellRect = new Rect(colInfo.X, rowInfo.Y, colInfo.Width, rowInfo.Height);
                        var cell = _owner._worksheet.GetCell(rowInfo.Row, colInfo.Col);

                        // Fill cell background if specified (alpha != 0)
                        if (cell != null && (cell.BackgroundColor & 0xFF000000) != 0)
                        {
                            byte a = (byte)((cell.BackgroundColor >> 24) & 0xFF);
                            byte red = (byte)((cell.BackgroundColor >> 16) & 0xFF);
                            byte g = (byte)((cell.BackgroundColor >> 8) & 0xFF);
                            byte b = (byte)(cell.BackgroundColor & 0xFF);
                            var cBrush = new SolidColorBrush(Color.FromArgb(a, red, g, b));
                            cBrush.Freeze();
                            dc.DrawRectangle(cBrush, null, cellRect);
                        }

                        // Draw grid lines
                        dc.DrawRectangle(null, ZeroWpfTheme.GridLinePen, cellRect);

                        // Render cell text
                        if (cell != null)
                        {
                            string text = cell.FormattedText;
                            if (!string.IsNullOrEmpty(text))
                            {
                                var typeface = cell.IsBold ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                                Brush textBrush;
                                if (cell.HasError)
                                {
                                    textBrush = ZeroWpfTheme.DangerAccent;
                                }
                                else if ((cell.TextColor & 0xFF000000) != 0)
                                {
                                    byte red = (byte)((cell.TextColor >> 16) & 0xFF);
                                    byte green = (byte)((cell.TextColor >> 8) & 0xFF);
                                    byte blue = (byte)(cell.TextColor & 0xFF);
                                    var customBrush = new SolidColorBrush(Color.FromRgb(red, green, blue));
                                    customBrush.Freeze();
                                    textBrush = customBrush;
                                }
                                else
                                {
                                    textBrush = ZeroWpfTheme.TextPrimary;
                                }

                                var ft = CreateFormattedText(text, typeface, 12.0, textBrush);
                                double tx = cellRect.X + 4.0;
                                if (cell.Alignment == SpreadsheetAlignment.Right)
                                    tx = Math.Max(cellRect.X + 4.0, cellRect.Right - ft.Width - 4.0);
                                else if (cell.Alignment == SpreadsheetAlignment.Center)
                                    tx = cellRect.X + Math.Max(0, (cellRect.Width - ft.Width) / 2.0);

                                double ty = cellRect.Y + Math.Max(0, (cellRect.Height - ft.Height) / 2.0);
                                dc.PushClip(new RectangleGeometry(cellRect));
                                dc.DrawText(ft, new Point(tx, ty));
                                dc.Pop();
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

                double selX1 = -1, selY1 = -1, selX2 = -1, selY2 = -1;
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
                    var selRect = new Rect(selX1, selY1, selX2 - selX1, selY2 - selY1);

                    var selFill = new SolidColorBrush(Color.FromArgb(32, 129, 140, 248));
                    selFill.Freeze();
                    dc.DrawRectangle(selFill, ZeroWpfTheme.AccentPen, selRect);

                    // Corner handle
                    dc.DrawRectangle(ZeroWpfTheme.PrimaryAccent, null, new Rect(selRect.Right - 3, selRect.Bottom - 3, 5, 5));
                }

                // 5. Render Column Headers (Top Strip)
                foreach (var colInfo in visibleCols)
                {
                    var colRect = new Rect(colInfo.X, 0, colInfo.Width, headerH);
                    dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, colRect);

                    string colName = CellAddress.ColumnIndexToLetters(colInfo.Col);
                    var ft = CreateFormattedText(colName, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary);
                    double hx = colRect.X + (colRect.Width - ft.Width) / 2.0;
                    double hy = (headerH - ft.Height) / 2.0;
                    dc.DrawText(ft, new Point(hx, hy));
                }

                // 6. Render Row Headers (Left Strip)
                foreach (var rowInfo in visibleRows)
                {
                    var rowRect = new Rect(0, rowInfo.Y, headerW, rowInfo.Height);
                    dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rowRect);

                    string rowName = (rowInfo.Row + 1).ToString(CultureInfo.InvariantCulture);
                    var ft = CreateFormattedText(rowName, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary);
                    double hx = (headerW - ft.Width) / 2.0;
                    double hy = rowRect.Y + (rowRect.Height - ft.Height) / 2.0;
                    dc.DrawText(ft, new Point(hx, hy));
                }

                // 7. Top-Left Corner Box
                var cornerRect = new Rect(0, 0, headerW, headerH);
                dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, cornerRect);

                // Small diagonal marker in corner
                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(headerW - 8, headerH - 3), new Point(headerW - 3, headerH - 8));
                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(headerW - 5, headerH - 3), new Point(headerW - 3, headerH - 5));
            }

#if NETFRAMEWORK
            private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
            {
                return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
            }
#else
            private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
            {
                return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
            }
#endif
        }
        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZSpreadsheet"/>.
    /// </summary>
    public class ZSpreadsheetControl : ZSpreadsheet { }

    /// <summary>
    /// Legacy alias for <see cref="ZSpreadsheet"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SpreadsheetControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSpreadsheet instead.")]
    public class SpreadsheetControl : ZSpreadsheet { }

    /// <summary>
    /// Legacy alias for <see cref="ZSpreadsheet"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSpreadsheet is deprecated and will be removed in 5 release cycles. Please migrate to ZSpreadsheet instead.")]
    public class ZeroSpreadsheet : ZSpreadsheet { }

    /// <summary>
    /// Legacy alias for <see cref="ZSpreadsheet"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSpreadsheetControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSpreadsheet instead.")]
    public class ZeroSpreadsheetControl : ZSpreadsheet { }

    #endregion

}
