using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.PropertyGrid
{
    /// <summary>
    /// High-performance categorized PropertyGrid inspector for WPF.
    /// Features collapsible category sections, real-time search filtering, resizable column splitter,
    /// in-place editing, and documentation description callout.
    /// </summary>
    public class ZPropertyGrid : FrameworkElement
    {
        private PropertyModel _model = new PropertyModel();
        private readonly TextBox _searchBox;
        private readonly TextBox _inPlaceEditor;

        private int _headerSearchHeight = 34;
        private int _categoryHeight = 26;
        private int _rowHeight = 24;
        private int _footerHeight = 56;
        private double _splitterX = 140.0;
        private bool _isResizingSplitter = false;

        private double _scrollY = 0;
        private int _hoveredRowIndex = -1;
        private PropertyItem? _selectedItem;
        private bool _isEditing = false;
        private PropertyItem? _editingItem;

        private const double ScrollBarWidth = 7.0;

        public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;
        public event EventHandler<PropertyItem>? PropertySelected;

        public PropertyModel Model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    if (_model != null)
                    {
                        _model.ModelChanged -= OnModelChanged;
                        _model.PropertyValueChanged -= OnPropertyValueChanged;
                    }
                    _model = value ?? new PropertyModel();
                    _model.ModelChanged += OnModelChanged;
                    _model.PropertyValueChanged += OnPropertyValueChanged;
                    _scrollY = 0;
                    _selectedItem = null;
                    InvalidateVisual();
                }
            }
        }

        public object? SelectedObject
        {
            get => _model.SelectedObject;
            set
            {
                _model.SetSelectedObject(value);
                _scrollY = 0;
                _selectedItem = null;
                InvalidateVisual();
            }
        }

        public double SplitterX
        {
            get => _splitterX;
            set { _splitterX = Math.Max(80.0, Math.Min(ActualWidth - 80.0, value)); InvalidateVisual(); }
        }

        public PropertyItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    InvalidateVisual();
                    if (_selectedItem != null) PropertySelected?.Invoke(this, _selectedItem);
                }
            }
        }

        public ZPropertyGrid()
        {
            ClipToBounds = true;
            Focusable = true;

            _model.ModelChanged += OnModelChanged;
            _model.PropertyValueChanged += OnPropertyValueChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // Integrated Search Bar
            _searchBox = new TextBox
            {
                Height = 24,
                FontSize = 12.0,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2)
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _model.SearchFilter = _searchBox.Text;
            };
            AddVisualChild(_searchBox);
            AddLogicalChild(_searchBox);

            // In-place text editor
            _inPlaceEditor = new TextBox
            {
                Height = 22,
                FontSize = 12.0,
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed
            };
            _inPlaceEditor.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitEdit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelEdit();
                    e.Handled = true;
                }
            };
            _inPlaceEditor.LostFocus += (s, e) => CommitEdit();
            AddVisualChild(_inPlaceEditor);
            AddLogicalChild(_inPlaceEditor);

            ApplyThemeStyles();
        }

        private void ApplyThemeStyles()
        {
            _searchBox.Background = ZeroWpfTheme.BgInput;
            _searchBox.Foreground = ZeroWpfTheme.TextPrimary;
            _searchBox.BorderBrush = ZeroWpfTheme.BorderDefault;

            _inPlaceEditor.Background = ZeroWpfTheme.BgInput;
            _inPlaceEditor.Foreground = ZeroWpfTheme.TextPrimary;
            _inPlaceEditor.BorderBrush = ZeroWpfTheme.PrimaryAccent;
        }

        private void OnModelChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            PropertyValueChanged?.Invoke(this, e);
            InvalidateVisual();
        }

        private void OnThemeChanged()
        {
            ApplyThemeStyles();
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index)
        {
            return index switch
            {
                0 => _searchBox,
                1 => _inPlaceEditor,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _searchBox.Arrange(new Rect(8, 5, Math.Max(0, finalSize.Width - 16), 24));
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            var dpi = VisualTreeHelper.GetDpi(this);

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, width, height));

            // Top Search Bar Divider
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, _headerSearchHeight - 0.5), new Point(width, _headerSearchHeight - 0.5));

            double contentTop = _headerSearchHeight;
            double contentHeight = Math.Max(0, height - contentTop - _footerHeight);

            // Compute Flattened Items List
            var visualEntries = BuildVisualEntries();
            double totalItemsHeight = 0;
            for (int i = 0; i < visualEntries.Count; i++)
            {
                totalItemsHeight += visualEntries[i].IsCategory ? _categoryHeight : _rowHeight;
            }

            // Clip content viewport
            dc.PushClip(new RectangleGeometry(new Rect(0, contentTop, width, contentHeight)));

            double curY = contentTop - _scrollY;
            for (int i = 0; i < visualEntries.Count; i++)
            {
                var entry = visualEntries[i];
                double itemH = entry.IsCategory ? _categoryHeight : _rowHeight;

                if (curY + itemH > contentTop && curY < contentTop + contentHeight)
                {
                    if (entry.IsCategory)
                    {
                        // Category Header
                        Rect catRect = new Rect(0, curY, width, _categoryHeight);
                        dc.DrawRectangle(ZeroWpfTheme.BgInput, null, catRect);

                        string chevron = entry.Category!.IsExpanded ? "▼" : "▶";
                        var cft = CreateFormattedText(chevron, ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextSecondary, dpi);
                        dc.DrawText(cft, new Point(8, curY + (_categoryHeight - cft.Height) / 2.0));

                        var tft = CreateFormattedText(entry.Category.Name, ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
                        dc.DrawText(tft, new Point(24, curY + (_categoryHeight - tft.Height) / 2.0));

                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, curY + _categoryHeight - 0.5), new Point(width, curY + _categoryHeight - 0.5));
                    }
                    else
                    {
                        // Property Row
                        var prop = entry.Property!;
                        bool isSelected = (prop == _selectedItem);
                        bool isHovered = (i == _hoveredRowIndex);

                        Rect rowRect = new Rect(0, curY, width, _rowHeight);

                        if (isSelected)
                        {
                            dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, rowRect);
                        }
                        else if (isHovered)
                        {
                            dc.DrawRectangle(ZeroWpfTheme.BgHover, null, rowRect);
                        }

                        // Property Name
                        var nameFt = CreateFormattedText(prop.DisplayName, isSelected ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                        dc.DrawText(nameFt, new Point(16, curY + (_rowHeight - nameFt.Height) / 2.0));

                        // Splitter line
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(_splitterX - 0.5, curY), new Point(_splitterX - 0.5, curY + _rowHeight));

                        // Property Value
                        string valText = prop.Value?.ToString() ?? "(null)";
                        if (prop.PropertyType == typeof(bool))
                        {
                            bool bVal = prop.Value is bool b && b;
                            string boolGlyph = bVal ? "[✓] True" : "[ ] False";
                            var bft = CreateFormattedText(boolGlyph, ZeroWpfTheme.BoldTypeface, 11.5, bVal ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.TextSecondary, dpi);
                            dc.DrawText(bft, new Point(_splitterX + 8, curY + (_rowHeight - bft.Height) / 2.0));
                        }
                        else
                        {
                            var valFt = CreateFormattedText(valText, ZeroWpfTheme.RegularTypeface, 12.0, isSelected ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextSecondary, dpi);
                            dc.DrawText(valFt, new Point(_splitterX + 8, curY + (_rowHeight - valFt.Height) / 2.0));
                        }

                        // Bottom border
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, curY + _rowHeight - 0.5), new Point(width, curY + _rowHeight - 0.5));
                    }
                }

                curY += itemH;
            }

            dc.Pop(); // Pop content viewport clip

            // Footer Description Callout
            double footerTop = height - _footerHeight;
            dc.DrawRectangle(ZeroWpfTheme.BgInput, null, new Rect(0, footerTop, width, _footerHeight));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, footerTop - 0.5), new Point(width, footerTop - 0.5));

            if (_selectedItem != null)
            {
                var titleFt = CreateFormattedText(_selectedItem.DisplayName, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(titleFt, new Point(10, footerTop + 6));

                string desc = !string.IsNullOrEmpty(_selectedItem.Description)
                    ? _selectedItem.Description
                    : $"Type: {_selectedItem.PropertyType?.Name ?? "Object"}";

                var descFt = CreateFormattedText(desc, ZeroWpfTheme.RegularTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(descFt, new Point(10, footerTop + 24));
            }

            // Slim Scrollbar
            double maxScroll = Math.Max(0, totalItemsHeight - contentHeight);
            if (maxScroll > 0)
            {
                double trackH = contentHeight;
                double thumbH = Math.Max(24.0, (contentHeight / totalItemsHeight) * trackH);
                double thumbY = contentTop + (_scrollY / maxScroll) * (trackH - thumbH);
                dc.DrawRectangle(ZeroWpfTheme.ScrollThumb, null, new Rect(width - ScrollBarWidth - 2, thumbY, ScrollBarWidth, thumbH));
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point pt = e.GetPosition(this);

            // Splitter Drag Hit Test
            if (Math.Abs(pt.X - _splitterX) <= 4.0 && pt.Y >= _headerSearchHeight && pt.Y <= ActualHeight - _footerHeight)
            {
                _isResizingSplitter = true;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                return;
            }

            double contentTop = _headerSearchHeight;
            if (pt.Y < contentTop || pt.Y > ActualHeight - _footerHeight) return;

            var visualEntries = BuildVisualEntries();
            double curY = contentTop - _scrollY;

            for (int i = 0; i < visualEntries.Count; i++)
            {
                var entry = visualEntries[i];
                double itemH = entry.IsCategory ? _categoryHeight : _rowHeight;

                if (pt.Y >= curY && pt.Y < curY + itemH)
                {
                    if (entry.IsCategory)
                    {
                        entry.Category!.IsExpanded = !entry.Category.IsExpanded;
                        InvalidateVisual();
                        return;
                    }
                    else
                    {
                        var prop = entry.Property!;
                        SelectedItem = prop;

                        // Boolean toggle on click
                        if (prop.PropertyType == typeof(bool) && !prop.IsReadOnly)
                        {
                            bool cur = prop.Value is bool b && b;
                            prop.Value = !cur;
                            InvalidateVisual();
                            return;
                        }

                        // Start in-place edit on right column click
                        if (pt.X > _splitterX && !prop.IsReadOnly && prop.PropertyType == typeof(string) || prop.PropertyType == typeof(int) || prop.PropertyType == typeof(double))
                        {
                            StartEdit(prop, new Rect(_splitterX + 2, curY + 1, Math.Max(60, ActualWidth - _splitterX - 10), _rowHeight - 2));
                        }
                        return;
                    }
                }

                curY += itemH;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);

            if (_isResizingSplitter && e.LeftButton == MouseButtonState.Pressed)
            {
                SplitterX = pt.X;
                return;
            }

            if (Math.Abs(pt.X - _splitterX) <= 4.0 && pt.Y >= _headerSearchHeight && pt.Y <= ActualHeight - _footerHeight)
            {
                Cursor = Cursors.SizeWE;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isResizingSplitter)
            {
                _isResizingSplitter = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double contentHeight = Math.Max(0, ActualHeight - _headerSearchHeight - _footerHeight);
            var visualEntries = BuildVisualEntries();
            double totalItemsHeight = 0;
            for (int i = 0; i < visualEntries.Count; i++)
            {
                totalItemsHeight += visualEntries[i].IsCategory ? _categoryHeight : _rowHeight;
            }

            double maxScroll = Math.Max(0, totalItemsHeight - contentHeight);
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - (e.Delta / 120.0) * (_rowHeight * 3)));
            InvalidateVisual();
            e.Handled = true;
        }

        private void StartEdit(PropertyItem item, Rect bounds)
        {
            _editingItem = item;
            _isEditing = true;
            _inPlaceEditor.Text = item.Value?.ToString() ?? string.Empty;
            _inPlaceEditor.Arrange(bounds);
            _inPlaceEditor.Visibility = Visibility.Visible;
            _inPlaceEditor.Focus();
            _inPlaceEditor.SelectAll();
        }

        private void CommitEdit()
        {
            if (_isEditing && _editingItem != null)
            {
                try
                {
                    object? newVal = Convert.ChangeType(_inPlaceEditor.Text, _editingItem.PropertyType, CultureInfo.InvariantCulture);
                    _editingItem.Value = newVal;
                }
                catch { }

                _inPlaceEditor.Visibility = Visibility.Collapsed;
                _isEditing = false;
                _editingItem = null;
                InvalidateVisual();
            }
        }

        private void CancelEdit()
        {
            _inPlaceEditor.Visibility = Visibility.Collapsed;
            _isEditing = false;
            _editingItem = null;
        }

        private List<VisualEntry> BuildVisualEntries()
        {
            var list = new List<VisualEntry>();
            var categories = _model.Categories;

            for (int c = 0; c < categories.Count; c++)
            {
                var cat = categories[c];
                list.Add(new VisualEntry { IsCategory = true, Category = cat });
                if (cat.IsExpanded)
                {
                    for (int p = 0; p < cat.Items.Count; p++)
                    {
                        list.Add(new VisualEntry { IsCategory = false, Property = cat.Items[p] });
                    }
                }
            }

            return list;
        }

        private struct VisualEntry
        {
            public bool IsCategory;
            public PropertyCategoryGroup? Category;
            public PropertyItem? Property;
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, DpiScale dpi)
        {
#if NETFRAMEWORK
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush);
#else
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                dpi.PixelsPerDip);
#endif
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
    /// Convenience alias for <see cref="ZPropertyGrid"/>.
    /// </summary>
    public class ZPropertyGridControl : ZPropertyGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZPropertyGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PropertyGridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPropertyGrid instead.")]
    public class PropertyGridControl : ZPropertyGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZPropertyGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPropertyGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZPropertyGrid instead.")]
    public class ZeroPropertyGrid : ZPropertyGrid { }

    #endregion
}
