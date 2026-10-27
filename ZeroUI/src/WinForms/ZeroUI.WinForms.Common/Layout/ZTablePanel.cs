using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    public enum TableUnitType
    {
        Absolute,
        Percent,
        AutoSize
    }

    public class TableColumnDefinition
    {
        public TableUnitType UnitType { get; set; } = TableUnitType.Percent;
        public float Value { get; set; } = 100f;
        internal int ActualWidth { get; set; }

        public TableColumnDefinition() { }

        public TableColumnDefinition(TableUnitType unitType, float value)
        {
            UnitType = unitType;
            Value = value;
        }

        public static TableColumnDefinition Absolute(int pixels) => new TableColumnDefinition(TableUnitType.Absolute, pixels);
        public static TableColumnDefinition Percent(float percent) => new TableColumnDefinition(TableUnitType.Percent, percent);
        public static TableColumnDefinition Auto() => new TableColumnDefinition(TableUnitType.AutoSize, 0);
    }

    public class TableRowDefinition
    {
        public TableUnitType UnitType { get; set; } = TableUnitType.Percent;
        public float Value { get; set; } = 100f;
        internal int ActualHeight { get; set; }

        public TableRowDefinition() { }

        public TableRowDefinition(TableUnitType unitType, float value)
        {
            UnitType = unitType;
            Value = value;
        }

        public static TableRowDefinition Absolute(int pixels) => new TableRowDefinition(TableUnitType.Absolute, pixels);
        public static TableRowDefinition Percent(float percent) => new TableRowDefinition(TableUnitType.Percent, percent);
        public static TableRowDefinition Auto() => new TableRowDefinition(TableUnitType.AutoSize, 0);
    }

    internal class TableCellPosition
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; } = 1;
        public int ColumnSpan { get; set; } = 1;
    }

    /// <summary>
    /// High-performance responsive TablePanel for ZeroUI.
    /// Provides flexible grid layout with single-pass layout and zero flickering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Positions child controls within a flexible rows and columns grid structure.")]
    public class ZTablePanel : Panel
    {
        private readonly List<TableColumnDefinition> _columns = new List<TableColumnDefinition>();
        private readonly List<TableRowDefinition> _rows = new List<TableRowDefinition>();
        private readonly Dictionary<Control, TableCellPosition> _cellPositions = new Dictionary<Control, TableCellPosition>();

        private int _cellSpacing = 6;
        private bool _showGridLines = false;
        private Color _gridLineColor = Color.FromArgb(46, 52, 78);
        private bool _isPerformingLayout = false;

        public ZTablePanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            Padding = new Padding(8);
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ApplyCurrentTheme();
        }

        [Category("Layout")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<TableColumnDefinition> Columns => _columns;

        [Category("Layout")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<TableRowDefinition> Rows => _rows;

        [Category("Layout")]
        [DefaultValue(6)]
        public int CellSpacing
        {
            get => _cellSpacing;
            set
            {
                if (_cellSpacing != value)
                {
                    _cellSpacing = Math.Max(0, value);
                    PerformLayout();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ShowGridLines
        {
            get => _showGridLines;
            set
            {
                if (_showGridLines != value)
                {
                    _showGridLines = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        public Color GridLineColor
        {
            get => _gridLineColor;
            set
            {
                _gridLineColor = value;
                Invalidate();
            }
        }

        public void SetCell(Control control, int row, int column, int rowSpan = 1, int colSpan = 1)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));

            if (!_cellPositions.TryGetValue(control, out var pos))
            {
                pos = new TableCellPosition();
                _cellPositions[control] = pos;
            }

            pos.Row = Math.Max(0, row);
            pos.Column = Math.Max(0, column);
            pos.RowSpan = Math.Max(1, rowSpan);
            pos.ColumnSpan = Math.Max(1, colSpan);

            if (!Controls.Contains(control))
            {
                Controls.Add(control);
            }
            else
            {
                PerformLayout();
            }
        }

        public void SetRow(Control control, int row) => SetCell(control, row, GetColumn(control));
        public void SetColumn(Control control, int col) => SetCell(control, GetRow(control), col);

        public int GetRow(Control control) => _cellPositions.TryGetValue(control, out var p) ? p.Row : 0;
        public int GetColumn(Control control) => _cellPositions.TryGetValue(control, out var p) ? p.Column : 0;
        public int GetRowSpan(Control control) => _cellPositions.TryGetValue(control, out var p) ? p.RowSpan : 1;
        public int GetColumnSpan(Control control) => _cellPositions.TryGetValue(control, out var p) ? p.ColumnSpan : 1;

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            ApplyCurrentTheme();
            Invalidate();
        }

        private void ApplyCurrentTheme()
        {
            _gridLineColor = ZeroTheme.Palette.Border;
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (e.Control != null)
            {
                _cellPositions.Remove(e.Control);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (_isPerformingLayout) return;

            _isPerformingLayout = true;
            try
            {
                base.OnLayout(levent);
                ComputeAndApplyLayout();
            }
            finally
            {
                _isPerformingLayout = false;
            }
        }

        private void ComputeAndApplyLayout()
        {
            int colCount = Math.Max(1, _columns.Count);
            int rowCount = Math.Max(1, _rows.Count);

            int usableWidth = Math.Max(0, ClientRectangle.Width - Padding.Horizontal - ((colCount - 1) * _cellSpacing));
            int usableHeight = Math.Max(0, ClientRectangle.Height - Padding.Vertical - ((rowCount - 1) * _cellSpacing));

            // 1. Calculate Column Widths
            float totalPercentCols = 0f;
            int takenColWidth = 0;

            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (col.UnitType == TableUnitType.Absolute)
                {
                    col.ActualWidth = (int)col.Value;
                    takenColWidth += col.ActualWidth;
                }
                else if (col.UnitType == TableUnitType.AutoSize)
                {
                    // Compute max preferred width among children in this column
                    int maxPrefW = 0;
                    foreach (var kvp in _cellPositions)
                    {
                        if (kvp.Value.Column == c && kvp.Value.ColumnSpan == 1 && kvp.Key.Visible)
                        {
                            maxPrefW = Math.Max(maxPrefW, kvp.Key.PreferredSize.Width);
                        }
                    }
                    col.ActualWidth = Math.Max(20, maxPrefW);
                    takenColWidth += col.ActualWidth;
                }
                else
                {
                    totalPercentCols += Math.Max(0.001f, col.Value);
                }
            }

            int remainingColWidth = Math.Max(0, usableWidth - takenColWidth);
            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (col.UnitType == TableUnitType.Percent && totalPercentCols > 0)
                {
                    col.ActualWidth = (int)((col.Value / totalPercentCols) * remainingColWidth);
                }
            }

            // 2. Calculate Row Heights
            float totalPercentRows = 0f;
            int takenRowHeight = 0;

            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                if (row.UnitType == TableUnitType.Absolute)
                {
                    row.ActualHeight = (int)row.Value;
                    takenRowHeight += row.ActualHeight;
                }
                else if (row.UnitType == TableUnitType.AutoSize)
                {
                    int maxPrefH = 0;
                    foreach (var kvp in _cellPositions)
                    {
                        if (kvp.Value.Row == r && kvp.Value.RowSpan == 1 && kvp.Key.Visible)
                        {
                            maxPrefH = Math.Max(maxPrefH, kvp.Key.PreferredSize.Height);
                        }
                    }
                    row.ActualHeight = Math.Max(20, maxPrefH);
                    takenRowHeight += row.ActualHeight;
                }
                else
                {
                    totalPercentRows += Math.Max(0.001f, row.Value);
                }
            }

            int remainingRowHeight = Math.Max(0, usableHeight - takenRowHeight);
            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                if (row.UnitType == TableUnitType.Percent && totalPercentRows > 0)
                {
                    row.ActualHeight = (int)((row.Value / totalPercentRows) * remainingRowHeight);
                }
            }

            // 3. Position Controls
            foreach (Control child in Controls)
            {
                if (!child.Visible) continue;

                if (!_cellPositions.TryGetValue(child, out var pos))
                {
                    pos = new TableCellPosition { Row = 0, Column = 0 };
                    _cellPositions[child] = pos;
                }

                int x = Padding.Left;
                for (int c = 0; c < pos.Column && c < _columns.Count; c++)
                {
                    x += _columns[c].ActualWidth + _cellSpacing;
                }

                int y = Padding.Top;
                for (int r = 0; r < pos.Row && r < _rows.Count; r++)
                {
                    y += _rows[r].ActualHeight + _cellSpacing;
                }

                int w = 0;
                for (int c = pos.Column; c < pos.Column + pos.ColumnSpan && c < _columns.Count; c++)
                {
                    w += _columns[c].ActualWidth + (c > pos.Column ? _cellSpacing : 0);
                }
                if (w <= 0) w = Math.Max(20, child.Width);

                int h = 0;
                for (int r = pos.Row; r < pos.Row + pos.RowSpan && r < _rows.Count; r++)
                {
                    h += _rows[r].ActualHeight + (r > pos.Row ? _cellSpacing : 0);
                }
                if (h <= 0) h = Math.Max(20, child.Height);

                child.SetBounds(x, y, w, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_showGridLines && _columns.Count > 0 && _rows.Count > 0)
            {
                using var pen = new Pen(_gridLineColor, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };

                int x = Padding.Left;
                for (int c = 0; c < _columns.Count; c++)
                {
                    x += _columns[c].ActualWidth;
                    e.Graphics.DrawLine(pen, x + (_cellSpacing / 2), Padding.Top, x + (_cellSpacing / 2), Height - Padding.Bottom);
                    x += _cellSpacing;
                }

                int y = Padding.Top;
                for (int r = 0; r < _rows.Count; r++)
                {
                    y += _rows[r].ActualHeight;
                    e.Graphics.DrawLine(pen, Padding.Left, y + (_cellSpacing / 2), Width - Padding.Right, y + (_cellSpacing / 2));
                    y += _cellSpacing;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }

    [Obsolete("Use TablePanel instead.")]
    [ToolboxItem(false)]
    public class ZeroTablePanel : ZTablePanel
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTablePanel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TablePanel is deprecated and will be removed in 5 release cycles. Please migrate to ZTablePanel instead.")]
    [ToolboxItem(false)]
    public class TablePanel : ZTablePanel
    {
    }

    #endregion
}
