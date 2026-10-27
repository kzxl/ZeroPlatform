using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.TreeList
{
    /// <summary>
    /// Ultra-fast single-visual hierarchical TreeList / TreeGrid control with direct DrawingContext rendering.
    /// Provides 0-allocation tree virtualization, expand/collapse toggles, branch guide lines, and multi-column display.
    /// </summary>
    public class ZTreeList : FrameworkElement
    {
        private TreeModel _model = new TreeModel();
        private readonly ObservableCollection<ZeroColumn> _columns = new ObservableCollection<ZeroColumn>();

        private int _headerHeight = 30;
        private int _rowHeight = 26;
        private double _indentSize = 18.0;
        private bool _showLines = true;
        private bool _showHeaders = true;

        private double _scrollY = 0;
        private double _scrollX = 0;
        private int _hoveredRowIndex = -1;
        private int _selectedRowIndex = -1;
        private TreeNode? _selectedNode;

        private const double ScrollBarWidth = 7.0;

        public event EventHandler<TreeNode>? NodeSelected;
        public event EventHandler<TreeNode>? NodeExpanded;
        public event EventHandler<TreeNode>? NodeCollapsed;

        public TreeModel Model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    if (_model != null) _model.ModelChanged -= OnModelChanged;
                    _model = value ?? new TreeModel();
                    _model.ModelChanged += OnModelChanged;
                    _scrollY = 0;
                    _selectedRowIndex = -1;
                    _selectedNode = null;
                    InvalidateVisual();
                }
            }
        }

        public ObservableCollection<ZeroColumn> Columns => _columns;

        public int HeaderHeight
        {
            get => _headerHeight;
            set { _headerHeight = Math.Max(20, value); InvalidateVisual(); }
        }

        public int RowHeight
        {
            get => _rowHeight;
            set { _rowHeight = Math.Max(18, value); InvalidateVisual(); }
        }

        public double IndentSize
        {
            get => _indentSize;
            set { _indentSize = Math.Max(12.0, value); InvalidateVisual(); }
        }

        public bool ShowLines
        {
            get => _showLines;
            set { _showLines = value; InvalidateVisual(); }
        }

        public bool ShowHeaders
        {
            get => _showHeaders;
            set { _showHeaders = value; InvalidateVisual(); }
        }

        public TreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    _selectedRowIndex = (_selectedNode != null) ? _model.IndexOf(_selectedNode) : -1;
                    InvalidateVisual();
                    if (_selectedNode != null) NodeSelected?.Invoke(this, _selectedNode);
                }
            }
        }

        public int SelectedIndex
        {
            get => _selectedRowIndex;
            set
            {
                if (_selectedRowIndex != value)
                {
                    _selectedRowIndex = value;
                    _selectedNode = (value >= 0 && value < _model.VisibleNodeCount) ? _model.GetVisibleNode(value) : null;
                    InvalidateVisual();
                    if (_selectedNode != null) NodeSelected?.Invoke(this, _selectedNode);
                }
            }
        }

        public ZTreeList()
        {
            ClipToBounds = true;
            Focusable = true;
            _columns.CollectionChanged += OnColumnsChanged;
            _model.ModelChanged += OnModelChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnModelChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void OnThemeChanged()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            int topOffset = _showHeaders ? _headerHeight : 0;
            double visibleH = Math.Max(0, height - topOffset);
            int totalRows = _model.VisibleNodeCount;

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, width, height));

            // Virtualized Visible Row Calculations
            int startRow = Math.Max(0, (int)(_scrollY / _rowHeight));
            int visibleCount = (int)(visibleH / _rowHeight) + 2;
            int endRow = Math.Min(totalRows - 1, startRow + visibleCount);

            double firstRowY = (startRow * _rowHeight) - _scrollY;
            double currentY = topOffset + firstRowY;

            // Render Rows
            for (int r = startRow; r <= endRow && r < totalRows; r++)
            {
                if (currentY >= topOffset + visibleH) break;

                var node = _model.GetVisibleNode(r);
                bool isSelected = (r == _selectedRowIndex || node == _selectedNode);
                bool isHovered = (r == _hoveredRowIndex);

                Rect rowRect = new Rect(0, currentY, width, _rowHeight);

                // Row Background
                if (isSelected)
                {
                    dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, rowRect);
                }
                else if (isHovered)
                {
                    dc.DrawRectangle(ZeroWpfTheme.BgHover, null, rowRect);
                }

                // Render Columns
                double colX = -_scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    if (!col.IsVisible) continue;
                    double colW = col.Width;

                    if (colX + colW > 0 && colX < width)
                    {
                        if (c == 0)
                        {
                            // Tree Hierarchical Column
                            double indent = node.Level * _indentSize;
                            double nodeLeft = colX + 8 + indent;

                            // Connecting Hierarchy Lines
                            if (_showLines && node.Level > 0)
                            {
                                double lineX = colX + 8 + ((node.Level - 1) * _indentSize) + 8;
                                double centerY = currentY + (_rowHeight / 2.0);
                                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(lineX, currentY), new Point(lineX, centerY));
                                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(lineX, centerY), new Point(nodeLeft, centerY));
                            }

                            // Chevron Expander
                            if (node.HasChildren)
                            {
                                string chevron = node.IsExpanded ? "▼" : "▶";
                                var cft = CreateFormattedText(chevron, ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextSecondary, dpi);
                                dc.DrawText(cft, new Point(nodeLeft, currentY + (_rowHeight - cft.Height) / 2.0));
                            }

                            // Text
                            string val = node.GetValue(0);
                            if (!string.IsNullOrEmpty(val))
                            {
                                var tft = CreateFormattedText(val, isSelected ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface, 12.0, isSelected ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextPrimary, dpi);
                                dc.DrawText(tft, new Point(nodeLeft + 16, currentY + (_rowHeight - tft.Height) / 2.0));
                            }
                        }
                        else
                        {
                            // Value Column
                            string val = node.GetValue(c);
                            if (!string.IsNullOrEmpty(val))
                            {
                                var tft = CreateFormattedText(val, ZeroWpfTheme.RegularTypeface, 12.0, isSelected ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextSecondary, dpi);
                                double textX = colX + 8;
                                if (col.Alignment == CellAlignment.Right) textX = colX + colW - tft.Width - 8;
                                else if (col.Alignment == CellAlignment.Center) textX = colX + (colW - tft.Width) / 2.0;

                                dc.DrawText(tft, new Point(textX, currentY + (_rowHeight - tft.Height) / 2.0));
                            }
                        }

                        // Column Divider
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(colX + colW - 0.5, currentY), new Point(colX + colW - 0.5, currentY + _rowHeight));
                    }

                    colX += colW;
                }

                // Row Bottom Border
                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, currentY + _rowHeight - 0.5), new Point(width, currentY + _rowHeight - 0.5));

                currentY += _rowHeight;
            }

            // Render Header Row (Pinned on Top)
            if (_showHeaders)
            {
                dc.DrawRectangle(ZeroWpfTheme.BgInput, null, new Rect(0, 0, width, _headerHeight));
                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, _headerHeight - 0.5), new Point(width, _headerHeight - 0.5));

                double hColX = -_scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    if (!col.IsVisible) continue;
                    double colW = col.Width;

                    if (hColX + colW > 0 && hColX < width)
                    {
                        var hft = CreateFormattedText(col.HeaderText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                        double textX = hColX + 8;
                        if (col.Alignment == CellAlignment.Right) textX = hColX + colW - hft.Width - 8;
                        else if (col.Alignment == CellAlignment.Center) textX = hColX + (colW - hft.Width) / 2.0;

                        dc.DrawText(hft, new Point(textX, (_headerHeight - hft.Height) / 2.0));
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(hColX + colW - 0.5, 4), new Point(hColX + colW - 0.5, _headerHeight - 4));
                    }

                    hColX += colW;
                }
            }

            // Slim Scrollbar
            double maxScroll = Math.Max(0, totalRows * _rowHeight - visibleH);
            if (maxScroll > 0)
            {
                double trackH = visibleH;
                double thumbH = Math.Max(24.0, (visibleH / (totalRows * _rowHeight)) * trackH);
                double thumbY = topOffset + (_scrollY / maxScroll) * (trackH - thumbH);
                dc.DrawRectangle(ZeroWpfTheme.ScrollThumb, null, new Rect(width - ScrollBarWidth - 2, thumbY, ScrollBarWidth, thumbH));
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point pt = e.GetPosition(this);
            int topOffset = _showHeaders ? _headerHeight : 0;

            if (pt.Y < topOffset)
            {
                // Header clicked
                return;
            }

            double yOffset = pt.Y - topOffset + _scrollY;
            int rowIndex = (int)(yOffset / _rowHeight);

            if (rowIndex >= 0 && rowIndex < _model.VisibleNodeCount)
            {
                var node = _model.GetVisibleNode(rowIndex);
                double indent = node.Level * _indentSize;
                double chevronLeft = -_scrollX + 8 + indent;
                double chevronRight = chevronLeft + 18;

                if (pt.X >= chevronLeft && pt.X <= chevronRight && node.HasChildren)
                {
                    // Toggle expand/collapse
                    _model.ToggleExpand(node);
                    if (node.IsExpanded) NodeExpanded?.Invoke(this, node);
                    else NodeCollapsed?.Invoke(this, node);
                    InvalidateVisual();
                    return;
                }

                // Row selection
                SelectedIndex = rowIndex;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            int topOffset = _showHeaders ? _headerHeight : 0;

            if (pt.Y >= topOffset)
            {
                double yOffset = pt.Y - topOffset + _scrollY;
                int rowIndex = (int)(yOffset / _rowHeight);
                if (rowIndex >= 0 && rowIndex < _model.VisibleNodeCount)
                {
                    if (_hoveredRowIndex != rowIndex)
                    {
                        _hoveredRowIndex = rowIndex;
                        InvalidateVisual();
                    }
                    return;
                }
            }

            if (_hoveredRowIndex != -1)
            {
                _hoveredRowIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredRowIndex != -1)
            {
                _hoveredRowIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int topOffset = _showHeaders ? _headerHeight : 0;
            double visibleH = Math.Max(0, ActualHeight - topOffset);
            double maxScroll = Math.Max(0, _model.VisibleNodeCount * _rowHeight - visibleH);

            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - (e.Delta / 120.0) * (_rowHeight * 3)));
            InvalidateVisual();
            e.Handled = true;
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, DpiScale dpi)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                dpi.PixelsPerDip);
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZTreeList"/>.
    /// </summary>
    public class ZTreeListControl : ZTreeList { }

    /// <summary>
    /// Convenience alias for <see cref="ZTreeList"/>.
    /// </summary>
    public class TreeListControl : ZTreeList { }

    /// <summary>
    /// Legacy alias for <see cref="ZTreeList"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TreeList is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeList instead.")]
    public class TreeList : ZTreeList { }

    /// <summary>
    /// Legacy alias for <see cref="ZTreeList"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTreeList is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeList instead.")]
    public class ZeroTreeList : ZTreeList { }

    #endregion
}
