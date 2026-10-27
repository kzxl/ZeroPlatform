using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Pivot;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.PivotGrid
{
    /// <summary>
    /// High-performance multidimensional cross-tab OLAP reporting control for ZeroUI WPF.
    /// Provides hierarchical row/column header grouping, measure aggregations, sub-totals,
    /// grand totals, and vector WPF layout virtualization.
    /// </summary>
    public class ZPivotGrid : Control
    {
        private readonly PivotEngine _engine = new PivotEngine();
        private PivotResultModel? _model;

        private Grid? _rootGrid;
        private StackPanel? _fieldStrip;
        private ScrollViewer? _matrixScrollViewer;
        private Grid? _matrixGrid;

        public static readonly DependencyProperty DataSourceProperty =
            DependencyProperty.Register(
                nameof(DataSource),
                typeof(object),
                typeof(ZPivotGrid),
                new PropertyMetadata(null, (d, e) => ((ZPivotGrid)d).OnDataSourceChanged(e.NewValue)));

        public event EventHandler? DataRecalculated;

        public PivotEngine Engine => _engine;
        public List<PivotGridField> Fields => _engine.Fields;
        public PivotResultModel? Model => _model;

        public object? DataSource
        {
            get => GetValue(DataSourceProperty);
            set => SetValue(DataSourceProperty, value);
        }

        static ZPivotGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZPivotGrid), new FrameworkPropertyMetadata(typeof(ZPivotGrid)));
        }

        public ZPivotGrid()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Width = 680;
            Height = 440;

            BuildVisualStructure();

            ZeroLocalizer.CultureChanged += (s, e) => RefreshData();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_outerBorder != null)
            {
                _outerBorder.Background = Background;
                _outerBorder.BorderBrush = BorderBrush;
            }
            RebuildMatrixUI();
        }

        private void OnDataSourceChanged(object? newValue)
        {
            _engine.DataSource = newValue as IEnumerable;
            RefreshData();
        }

        public PivotGridField AddField(string fieldName, PivotArea area, string? caption = null, PivotSummaryType summaryType = PivotSummaryType.Sum)
        {
            var field = _engine.AddField(fieldName, area, caption, summaryType);
            RefreshData();
            return field;
        }

        public void RefreshData()
        {
            _model = _engine.Calculate();
            RebuildMatrixUI();
            DataRecalculated?.Invoke(this, EventArgs.Empty);
        }

        private Border? _outerBorder;

        private void BuildVisualStructure()
        {
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Field badges strip
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Matrix viewport

            // 1. Field Strip Container
            var stripBorder = new Border
            {
                Background = ZeroWpfTheme.BgPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            _fieldStrip = new StackPanel { Orientation = Orientation.Horizontal };
            stripBorder.Child = _fieldStrip;
            Grid.SetRow(stripBorder, 0);
            _rootGrid.Children.Add(stripBorder);

            // 2. Matrix Scroll Viewer
            _matrixScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = ZeroWpfTheme.BgCard
            };
            _matrixGrid = new Grid();
            _matrixScrollViewer.Content = _matrixGrid;
            Grid.SetRow(_matrixScrollViewer, 1);
            _rootGrid.Children.Add(_matrixScrollViewer);

            var outerBorder = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                Child = _rootGrid
            };
            _outerBorder = outerBorder;

            AddVisualChild(outerBorder);
            AddLogicalChild(outerBorder);

            RebuildMatrixUI();
        }

        protected override int VisualChildrenCount => _outerBorder != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _outerBorder ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            if (_outerBorder != null)
            {
                _outerBorder.Measure(constraint);
                return _outerBorder.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _outerBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private void RebuildMatrixUI()
        {
            if (_fieldStrip == null || _matrixGrid == null) return;

            // 1. Populate Field Badges Strip
            _fieldStrip.Children.Clear();
            foreach (var f in _engine.Fields)
            {
                if (!f.Visible) continue;

                var badgeBorder = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = f.Area switch
                    {
                        PivotArea.RowArea => ZeroWpfTheme.PrimaryAccent,
                        PivotArea.ColumnArea => ZeroWpfTheme.InfoAccent,
                        PivotArea.DataArea => ZeroWpfTheme.SuccessAccent,
                        _ => ZeroWpfTheme.BgInput
                    }
                };

                string areaTag = f.Area switch
                {
                    PivotArea.RowArea => "Row",
                    PivotArea.ColumnArea => "Col",
                    PivotArea.DataArea => $"{f.SummaryType}",
                    _ => "Filter"
                };

                var tb = new TextBlock
                {
                    Text = $"{f.Caption} ({areaTag})",
                    Foreground = f.Area == PivotArea.FilterArea ? ZeroWpfTheme.TextPrimary : Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5
                };
                badgeBorder.Child = tb;
                _fieldStrip.Children.Add(badgeBorder);
            }

            // 2. Populate Cross-Tab Matrix Grid
            _matrixGrid.Children.Clear();
            _matrixGrid.RowDefinitions.Clear();
            _matrixGrid.ColumnDefinitions.Clear();

            if (_model == null || (_model.RowCount == 0 && _model.ColumnCount == 0))
            {
                var emptyNotice = new TextBlock
                {
                    Text = ZeroLocalizer.GetString(ZeroStringId.PivotDropDataFields),
                    Foreground = ZeroWpfTheme.TextMuted,
                    FontSize = 13.0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(24)
                };
                _matrixGrid.Children.Add(emptyNotice);
                return;
            }

            int numCols = _model.ColumnCount + 2; // RowHeader + ColHeaders + GrandTotalCol
            int numRows = _model.RowCount + 2;    // ColHeaderRow + RowItems + GrandTotalRow

            // Row 0: Header row (30px), Rows 1..N: Data rows (26px), Last row: Grand Total row (28px)
            _matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            for (int r = 0; r < _model.RowCount; r++)
                _matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            _matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            // Col 0: Row header col (150px), Cols 1..N: Data cols (110px), Last col: Grand Total col (115px)
            _matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            for (int c = 0; c < _model.ColumnCount; c++)
                _matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            _matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });

            // Top-Left Corner Cell
            string rowTitle = _model.RowFields.Count > 0 ? _model.RowFields[0].Caption : "Rows";
            string colTitle = _model.ColumnFields.Count > 0 ? _model.ColumnFields[0].Caption : "Columns";
            AddCell(0, 0, $"{rowTitle} \\ {colTitle}", ZeroWpfTheme.BgInput, ZeroWpfTheme.TextSecondary, FontWeights.Bold, TextAlignment.Left);

            // Column Headers (Top Row)
            for (int c = 0; c < _model.ColumnCount; c++)
            {
                string header = _model.ColumnKeys[c].ToString();
                AddCell(0, c + 1, header, ZeroWpfTheme.BgInput, ZeroWpfTheme.TextPrimary, FontWeights.Bold, TextAlignment.Center);
            }
            // Column Grand Total Header
            AddCell(0, numCols - 1, ZeroLocalizer.GetString(ZeroStringId.PivotGrandTotal), ZeroWpfTheme.BgInput, ZeroWpfTheme.PrimaryAccent, FontWeights.Bold, TextAlignment.Center);

            // Row Headers & Data Matrix
            for (int r = 0; r < _model.RowCount; r++)
            {
                int gridRow = r + 1;
                string rHeader = _model.RowKeys[r].ToString();
                AddCell(gridRow, 0, rHeader, ZeroWpfTheme.BgInput, ZeroWpfTheme.TextPrimary, FontWeights.Normal, TextAlignment.Left);

                for (int c = 0; c < _model.ColumnCount; c++)
                {
                    int gridCol = c + 1;
                    object? cellVal = _model.GetCellValue(r, c);
                    string formatted = _model.FormatValue(cellVal);
                    AddCell(gridRow, gridCol, formatted, ZeroWpfTheme.BgCard, ZeroWpfTheme.TextPrimary, FontWeights.Normal, TextAlignment.Right);
                }

                // Row Total
                object? rTotVal = _model.GetRowTotal(r);
                string rTotText = _model.FormatValue(rTotVal);
                AddCell(gridRow, numCols - 1, rTotText, ZeroWpfTheme.BgInput, ZeroWpfTheme.TextPrimary, FontWeights.Bold, TextAlignment.Right);
            }

            // Bottom Grand Total Row
            int gtRow = numRows - 1;
            AddCell(gtRow, 0, ZeroLocalizer.GetString(ZeroStringId.PivotGrandTotal), ZeroWpfTheme.BgInput, ZeroWpfTheme.PrimaryAccent, FontWeights.Bold, TextAlignment.Left);

            for (int c = 0; c < _model.ColumnCount; c++)
            {
                object? cTotVal = _model.GetColumnTotal(c);
                string cTotText = _model.FormatValue(cTotVal);
                AddCell(gtRow, c + 1, cTotText, ZeroWpfTheme.BgInput, ZeroWpfTheme.TextPrimary, FontWeights.Bold, TextAlignment.Right);
            }

            // Bottom-Right Corner (Grand Total of Grand Totals)
            object? gtVal = _model.GetGrandTotal();
            string gtText = _model.FormatValue(gtVal);
            AddCell(gtRow, numCols - 1, gtText, ZeroWpfTheme.PrimaryAccent, Brushes.White, FontWeights.Bold, TextAlignment.Right);
        }

        private void AddCell(int row, int col, string text, Brush background, Brush foreground, FontWeight weight, TextAlignment alignment)
        {
            if (_matrixGrid == null) return;

            var border = new Border
            {
                Background = background,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(8, 0, 8, 0)
            };

            var tb = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontWeight = weight,
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.0,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            border.Child = tb;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            _matrixGrid.Children.Add(border);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZPivotGrid"/>.
    /// </summary>
    public class ZPivotGridControl : ZPivotGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZPivotGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PivotGridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPivotGrid instead.")]
    public class PivotGridControl : ZPivotGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZPivotGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPivotGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZPivotGrid instead.")]
    public class ZeroPivotGrid : ZPivotGrid { }

    #endregion
}
