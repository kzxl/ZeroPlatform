using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using ZeroData.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Virtualization;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    /// <summary>
    /// Ultra high-performance Single-Visual Virtual DataGrid for WPF.
    /// Eliminates WPF Visual Tree overhead by rendering cells directly via DrawingContext,
    /// powered by ZeroUI.Core virtualization algorithms and RowIndexMap.
    /// </summary>
    public partial class ZGrid : FrameworkElement, IAnimationFrameListener
    {
        private static readonly Pen WhiteCheckPen;

        static ZGrid()
        {
            WhiteCheckPen = new Pen(Brushes.White, 1.8);
            WhiteCheckPen.Freeze();
        }

        private readonly ObservableCollection<ZeroColumn> _columns = new ObservableCollection<ZeroColumn>();
        private RowIndexMap _rowIndexMap = new RowIndexMap(10000);
        private readonly GroupedRowIndexMap _groupedMap = new GroupedRowIndexMap();
        private int[] _groupColumnIndices = Array.Empty<int>();
        private readonly ObservableCollection<GridBand> _bands = new ObservableCollection<GridBand>();
        private IZeroVirtualSource? _dataSource;

        private int _headerHeight = 32;
        private int _rowHeight = 28;
        private int _scrollX = 0;
        private int _scrollY = 0;

        // High-Frequency Scroll Conflation & Adaptive Display Clock
        private int _pendingScrollDeltaY;
        private int _idleScrollFrames;
        private volatile bool _hasPendingScroll;
        private bool _isClockSubscribed;
        private readonly Action _flushScrollConflationAction;
        private bool _enableAdaptiveHighRefresh = true;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables adaptive display refresh rate synchronization and high-frequency scroll event conflation.")]
        public bool EnableAdaptiveHighRefresh
        {
            get => _enableAdaptiveHighRefresh;
            set => _enableAdaptiveHighRefresh = value;
        }


        public ObservableCollection<GridBand> Bands => _bands;

        public int EffectiveHeaderHeight
        {
            get
            {
                if (_bands.Count == 0) return _headerHeight;
                int maxDepth = 0;
                for (int i = 0; i < _bands.Count; i++)
                {
                    int d = _bands[i].GetMaxDepth();
                    if (d > maxDepth) maxDepth = d;
                }
                return (maxDepth + 1) * _headerHeight;
            }
        }


        // Interactive Group Panel & Summaries
        private bool _showGroupPanel = false;
        private int _groupPanelHeight = 34;
        private readonly ObservableCollection<GroupSummaryItem> _groupSummaries = new ObservableCollection<GroupSummaryItem>();
        private readonly ObservableCollection<ConditionalFormattingRule> _conditionalRules = new ObservableCollection<ConditionalFormattingRule>();
        private readonly Dictionary<int, HashSet<string>> _columnDistinctFilters = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<int, Rect> _columnFilterButtonBounds = new Dictionary<int, Rect>();
        private int _pendingFilterColumn = -1;

        // Master-Detail
        private bool _allowMasterDetail = false;
        private readonly HashSet<int> _expandedMasterRows = new HashSet<int>();
        public event EventHandler<int>? MasterRowExpanded;
        public event EventHandler<int>? MasterRowCollapsed;

        // Group Chip Hit Testing & Dragging
        private struct GroupChipInfo
        {
            public int ColumnIndex;
            public Rect ChipRect;
            public Rect CloseRect;
        }
        private readonly List<GroupChipInfo> _groupChipBounds = new List<GroupChipInfo>();
        private bool _isDraggingHeader = false;
        private int _draggedHeaderCol = -1;
        private Point _headerDragStart;
        private Point _currentMousePos;
        private int _reorderDropTargetIndex = -1;
        private bool _allowColumnReordering = true;
        private ColumnChooserWindow? _columnChooser;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Allows users to reorder columns by dragging column headers.")]
        public bool AllowColumnReordering
        {
            get => _allowColumnReordering;
            set => _allowColumnReordering = value;
        }

        public bool ShowGroupPanel
        {
            get => _showGroupPanel;
            set
            {
                if (_showGroupPanel != value)
                {
                    _showGroupPanel = value;
                    InvalidateVisual();
                }
            }
        }

        public int GroupPanelHeight
        {
            get => _groupPanelHeight;
            set
            {
                if (_groupPanelHeight != value)
                {
                    _groupPanelHeight = value;
                    InvalidateVisual();
                }
            }
        }

        public int TotalTopOffset => (ShowGroupPanel ? _groupPanelHeight : 0) + EffectiveHeaderHeight;
        public ObservableCollection<GroupSummaryItem> GroupSummaries => _groupSummaries;
        public ObservableCollection<ConditionalFormattingRule> ConditionalRules => _conditionalRules;

        public bool AllowMasterDetail
        {
            get => _allowMasterDetail;
            set
            {
                if (_allowMasterDetail != value)
                {
                    _allowMasterDetail = value;
                    InvalidateVisual();
                }
            }
        }

        public bool IsMasterRowExpanded(int modelRowIndex) => _expandedMasterRows.Contains(modelRowIndex);

        public void ToggleMasterRow(int modelRowIndex)
        {
            if (_expandedMasterRows.Contains(modelRowIndex))
            {
                _expandedMasterRows.Remove(modelRowIndex);
                MasterRowCollapsed?.Invoke(this, modelRowIndex);
            }
            else
            {
                _expandedMasterRows.Add(modelRowIndex);
                MasterRowExpanded?.Invoke(this, modelRowIndex);
            }
            InvalidateVisual();
        }

        // Selection & Interaction
        private int _selectedVisualRow = -1;
        private int _hoveredVisualRow = -1;
        private bool _isAllSelected = false;
        private readonly HashSet<int> _selectedVisualRows = new HashSet<int>();
        private readonly HashSet<int> _deselectedVisualRows = new HashSet<int>();
        private ZeroGridSelectionMode _selectionMode = ZeroGridSelectionMode.SingleRow;
        private CellRange _selectedBlock = CellRange.Empty;
        private bool _isSelectingBlock = false;
        private bool _isResizingColumn = false;

        public CellRange SelectedBlock => _selectedBlock;
        private int _resizingColIndex = -1;
        private double _resizeStartX = 0;
        private int _resizeStartWidth = 0;


        // In-Place Floating Editors (Pluggable)
        private readonly VisualCollection _visualChildren;
        private readonly TextBox _inPlaceEditor;
        private readonly ZeroNumericBox _numericEditor;
        private readonly ZeroDatePicker _dateEditor;
        private readonly ZeroMaskedTextBox _maskedEditor;
        private FrameworkElement? _activeEditor;
        private bool _isEditing = false;
        private int _editingVisualRow = -1;
        private int _editingColIndex = -1;

        // Summary Footer & Async Background Calculation Cache
        private bool _showFooter = false;
        private int _footerHeight = 28;
        private readonly Dictionary<int, string> _cachedSummaryTexts = new Dictionary<int, string>();
        private bool _summariesDirty = true;
        private volatile bool _isCalculatingSummaries = false;
        private System.Threading.CancellationTokenSource? _summaryCts;

        // Slim ScrollBar Interaction
        private const int ScrollBarThickness = 8;
        private bool _isDraggingVThumb = false;
        private double _dragThumbStartY = 0;
        private int _dragScrollStartY = 0;
        private bool _isVThumbHovered = false;

        // Asynchronous Sorting
        private bool _isSorting = false;
        private int _sortingColumnIndex = -1;
        private System.Threading.CancellationTokenSource? _sortCts;

        public bool IsSorting => _isSorting;
        public int SortingColumnIndex => _sortingColumnIndex;

        public event EventHandler? SelectionChanged;
        public event EventHandler<int>? ColumnHeaderClicked;
        public event EventHandler? SortingStarted;
        public event EventHandler<TimeSpan>? SortingCompleted;
        public event EventHandler<CellValueChangedEventArgs>? CellValueChanged;
        public event EventHandler? CellBeginEdit;
        public event EventHandler? CellEndEdit;

        public ObservableCollection<ZeroColumn> Columns => _columns;

        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(GridDensity),
                typeof(ZGrid),
                new FrameworkPropertyMetadata(GridDensity.Middle, FrameworkPropertyMetadataOptions.AffectsRender, OnDensityChanged));

        public GridDensity Density
        {
            get => (GridDensity)GetValue(DensityProperty);
            set => SetValue(DensityProperty, value);
        }

        private static void OnDensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZGrid grid)
            {
                grid._rowHeight = (GridDensity)e.NewValue switch
                {
                    GridDensity.Compact => 24,
                    GridDensity.Loose => 36,
                    _ => 28
                };
                grid.InvalidateVisual();
            }
        }

        public IZeroVirtualSource? DataSource
        {
            get => _dataSource;
            set => SetVirtualSourceInternal(value);
        }

        private void SetVirtualSourceInternal(IZeroVirtualSource? value)
        {
            _dataSource = value;
            if (_dataSource != null)
            {
                _rowIndexMap.ResetIdentity(_dataSource.TotalRowCount);
                if (_groupColumnIndices.Length > 0)
                {
                    GroupBy(_groupColumnIndices);
                }
                else
                {
                    _groupedMap.ResetIdentity(_dataSource.TotalRowCount);
                }
            }
            else
            {
                _rowIndexMap.ActiveCount = 0;
                _groupedMap.ResetIdentity(0);
            }
            _scrollY = 0;
            ClearRowSelection();
            InvalidateSummaries();
            InvalidateVisual();
        }

        public int VisualRowCount => _groupedMap.HasGrouping ? _groupedMap.ActiveCount : _rowIndexMap.ActiveCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetModelRowIndex(int visualRowIndex)
        {
            if (_groupedMap.HasGrouping)
            {
                if (visualRowIndex >= 0 && visualRowIndex < _groupedMap.ActiveCount)
                {
                    var entry = _groupedMap[visualRowIndex];
                    return entry.IsData ? entry.ModelRowIndex : -1;
                }
                return -1;
            }
            if (visualRowIndex >= 0 && visualRowIndex < _rowIndexMap.ActiveCount)
            {
                return _rowIndexMap[visualRowIndex];
            }
            return -1;
        }

        public int SelectedIndex
        {
            get => GetModelRowIndex(_selectedVisualRow);
            set
            {
                if (value < 0 || _dataSource == null || value >= _dataSource.TotalRowCount)
                {
                    ClearRowSelection();
                }
                else if (_rowIndexMap.IsIdentity && !_groupedMap.HasGrouping)
                {
                    _selectedVisualRow = value;
                    _isAllSelected = false;
                    _selectedVisualRows.Clear();
                    _deselectedVisualRows.Clear();
                    _selectedVisualRows.Add(value);
                }
                else
                {
                    int total = VisualRowCount;
                    for (int i = 0; i < total; i++)
                    {
                        if (GetModelRowIndex(i) == value)
                        {
                            _selectedVisualRow = i;
                            _isAllSelected = false;
                            _selectedVisualRows.Clear();
                            _deselectedVisualRows.Clear();
                            _selectedVisualRows.Add(i);
                            break;
                        }
                    }
                }
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HasGrouping => _groupedMap.HasGrouping;
        public IReadOnlyList<GroupRowInfo> RootGroups => _groupedMap.RootGroups;
        public GroupedRowIndexMap GroupedMap => _groupedMap;


        public ZeroGridSelectionMode SelectionMode
        {
            get => _selectionMode;
            set
            {
                _selectionMode = value;
                _selectedBlock = CellRange.Empty;
                ClearRowSelection();
            }
        }

        public IReadOnlyCollection<int> SelectedVisualRows => _selectedVisualRows;

        public bool IsVisualRowSelected(int visualRowIndex)
        {
            if (_selectionMode != ZeroGridSelectionMode.MultiRow)
            {
                return visualRowIndex == _selectedVisualRow;
            }
            if (_isAllSelected)
            {
                return !_deselectedVisualRows.Contains(visualRowIndex);
            }
            return _selectedVisualRows.Contains(visualRowIndex);
        }

        public int SelectedRowCount => _isAllSelected
            ? Math.Max(0, VisualRowCount - _deselectedVisualRows.Count)
            : _selectedVisualRows.Count;

        public void SelectAllRows()
        {
            _isAllSelected = true;
            _selectedVisualRows.Clear();
            _deselectedVisualRows.Clear();
            if (VisualRowCount > 0) _selectedVisualRow = 0;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void ClearRowSelection()
        {
            _isAllSelected = false;
            _selectedVisualRows.Clear();
            _deselectedVisualRows.Clear();
            _selectedVisualRow = -1;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void InvalidateSummaries()
        {
            _summariesDirty = true;
            _summaryCts?.Cancel();
        }

        public bool ShowFooter
        {
            get => _showFooter || HasAnySummaryColumns();
            set { _showFooter = value; InvalidateVisual(); }
        }

        public int FooterHeight
        {
            get => _footerHeight;
            set { _footerHeight = Math.Max(20, value); InvalidateVisual(); }
        }

        public bool IsEditing => _isEditing;

        public RowIndexMap IndexMap => _rowIndexMap;

        public ZGrid()
        {
            ClipToBounds = true;
            Focusable = true;
            _visualChildren = new VisualCollection(this);

            _flushScrollConflationAction = FlushScrollConflation;
            ZeroAnimationClock.AutoSynchronizeWithDisplay();
            Unloaded += (s, e) =>
            {
                RemoveClockSubscribed();
                _summaryCts?.Cancel();
                _summaryCts?.Dispose();
                _summaryCts = null;
            };

            _inPlaceEditor = new TextBox
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 12.0
            };
            _inPlaceEditor.KeyDown += InPlaceEditor_KeyDown;
            _inPlaceEditor.LostFocus += (s, e) => CommitEdit();
            _visualChildren.Add(_inPlaceEditor);

            _numericEditor = new ZeroNumericBox
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 12.0
            };
            _numericEditor.KeyDown += InPlaceEditor_KeyDown;
            _numericEditor.LostFocus += (s, e) => CommitEdit();
            _visualChildren.Add(_numericEditor);

            _dateEditor = new ZeroDatePicker
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),
                FontSize = 12.0,
                ShowPresets = false
            };
            _dateEditor.KeyDown += InPlaceEditor_KeyDown;
            _dateEditor.LostFocus += (s, e) =>
            {
                if (_dateEditor.IsDropDownOpen) return;
                CommitEdit();
            };
            _visualChildren.Add(_dateEditor);

            _maskedEditor = new ZeroMaskedTextBox
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 12.0
            };
            _maskedEditor.KeyDown += InPlaceEditor_KeyDown;
            _maskedEditor.LostFocus += (s, e) => CommitEdit();
            _visualChildren.Add(_maskedEditor);

            _columns.CollectionChanged += (s, e) => InvalidateVisual();
            _groupSummaries.CollectionChanged += (s, e) => RecalculateGroupSummaries();
            _conditionalRules.CollectionChanged += (s, e) => InvalidateVisual();
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                }
                return;
            }
            UpdateEditorTheme();
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => _visualChildren.Count;
        protected override Visual GetVisualChild(int index) => _visualChildren[index];

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_isEditing && _activeEditor != null && _activeEditor.Visibility == Visibility.Visible)
            {
                var rect = GetCellRectangle(_editingVisualRow, _editingColIndex);
                int footerH = ShowFooter ? _footerHeight : 0;
                if (rect.Y < TotalTopOffset || rect.Bottom > finalSize.Height - footerH)
                {
                    CommitEdit();
                }
                else
                {
                    _activeEditor.Arrange(rect);
                }
            }
            return base.ArrangeOverride(finalSize);
        }

    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZGrid"/>.
    /// </summary>
    public class ZGridControl : ZGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGrid instead.")]
    public class GridControl : ZGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZGrid instead.")]
    public class ZeroGrid : ZGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGrid instead.")]
    public class ZeroGridControl : ZGrid { }

    #endregion
}
