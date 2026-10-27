using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Data
{
    /// <summary>
    /// Represents a column definition in the TreeList hierarchical multi-column grid.
    /// </summary>
    public enum TreeListSelectionMode
    {
        Single,
        MultiSelect
    }

    /// <summary>
    /// Represents a column definition in the TreeList hierarchical multi-column grid.
    /// </summary>
    public class TreeListColumn
    {
        public string Name { get; set; } = "";
        public string Caption { get; set; } = "";
        public int Width { get; set; } = 120;
        public int MinWidth { get; set; } = 40;
        public bool Visible { get; set; } = true;
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
        public SortOrder SortOrder { get; set; } = SortOrder.None;
        public bool AllowEdit { get; set; } = true;
        public string? FieldName { get; set; }
        public TreeListSummaryType SummaryType { get; set; } = TreeListSummaryType.None;
        public string SummaryFormat { get; set; } = "{0:N2}";
        public TreeListRollupMode RollupMode { get; set; } = TreeListRollupMode.None;

        internal Rectangle HeaderBounds;

        public TreeListColumn() { }

        public TreeListColumn(string name, string caption, int width = 120)
        {
            Name = name;
            Caption = caption;
            Width = width;
        }

        public TreeListColumn(string name, string caption, int width, HorizontalAlignment alignment)
        {
            Name = name;
            Caption = caption;
            Width = width;
            Alignment = alignment;
        }
    }

    /// <summary>
    /// Represents a hierarchical node within the ZeroTreeList control.
    /// </summary>
    public class ZeroTreeNode
    {
        private readonly Dictionary<string, object?> _cellValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public string SubText { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Badge { get; set; } = "";
        public Color? BadgeColor { get; set; }
        public bool IsExpanded { get; set; } = true;
        public CheckState CheckState { get; set; } = CheckState.Unchecked;
        public object? Tag { get; set; }
        public bool HasLoadedChildren { get; set; } = true;
        public bool IsLazyLoadable { get; set; } = false;

        public ZeroTreeNode? Parent { get; internal set; }
        public List<ZeroTreeNode> Children { get; } = new List<ZeroTreeNode>();

        internal Rectangle RowBounds;
        internal Rectangle ChevronBounds;
        internal Rectangle CheckBounds;

        public object? this[string columnName]
        {
            get => _cellValues.TryGetValue(columnName, out var val) ? val : null;
            set => _cellValues[columnName] = value;
        }

        public void SetValue(string columnName, object? value) => this[columnName] = value;
        public object? GetValue(string columnName) => this[columnName];

        public ZeroTreeNode() { }

        public ZeroTreeNode(string text, string icon = "", string subText = "")
        {
            Text = text;
            Icon = icon;
            SubText = subText;
        }

        public int Level
        {
            get
            {
                int lvl = 0;
                var curr = Parent;
                while (curr != null)
                {
                    lvl++;
                    curr = curr.Parent;
                }
                return lvl;
            }
        }

        public bool HasChildren => Children.Count > 0 || (IsLazyLoadable && !HasLoadedChildren);

        public ZeroTreeNode AddChild(ZeroTreeNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public ZeroTreeNode AddChild(string text, string icon = "", string subText = "")
        {
            var child = new ZeroTreeNode(text, icon, subText) { Parent = this };
            Children.Add(child);
            return child;
        }

        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }

        public void SetCheckState(CheckState state, bool cascade = true)
        {
            CheckState = state;
            if (cascade)
            {
                foreach (var child in Children)
                {
                    child.SetCheckState(state, true);
                }
            }

            if (Parent != null && cascade)
            {
                Parent.UpdateParentCheckState();
            }
        }

        internal void UpdateParentCheckState()
        {
            if (Children.Count == 0) return;

            int checkedCount = 0;
            int indeterminateCount = 0;

            foreach (var child in Children)
            {
                if (child.CheckState == CheckState.Checked) checkedCount++;
                else if (child.CheckState == CheckState.Indeterminate) indeterminateCount++;
            }

            if (checkedCount == Children.Count)
            {
                CheckState = CheckState.Checked;
            }
            else if (checkedCount == 0 && indeterminateCount == 0)
            {
                CheckState = CheckState.Unchecked;
            }
            else
            {
                CheckState = CheckState.Indeterminate;
            }

            Parent?.UpdateParentCheckState();
        }
    }

    /// <summary>
    /// High-performance, virtualized hierarchical Tree and Multi-Level BOM TreeList control for ZeroUI.
    /// Supports expand/collapse, tri-state cascading checkboxes, hierarchy connection lines, search filtering,
    /// and theme reactivity (Clean Light / Obsidian Dark).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("NodeSelected")]
    [Description("High-performance virtualized hierarchical Tree and BOM TreeList control")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTreeList.bmp")]
    [Designer("ZeroUI.WinForms.Design.Data.ZTreeListDesigner, ZeroUI.WinForms.Design")]
    public partial class ZTreeList : Control
    {
        private readonly List<ZeroTreeNode> _nodes = new List<ZeroTreeNode>();
        private readonly List<ZeroTreeNode> _visibleNodes = new List<ZeroTreeNode>();
        private readonly List<TreeListColumn> _columns = new List<TreeListColumn>();

        private int _rowHeight = 34;
        private int _headerHeight = 28;
        private bool _showColumnHeaders = true;
        private int _indentWidth = 24;
        private bool _showCheckBoxes = true;
        private bool _showLines = true;
        private string _filterText = "";

        private int _resizingColumnIndex = -1;
        private int _resizeStartX;
        private int _resizeStartWidth;
        private int _hoveredColumnIndex = -1;
        private bool _hoveredOnDivider = false;

        private ZeroTreeNode? _selectedNode;
        private ZeroTreeNode? _hoveredNode;
        private bool _hoveredOnChevron = false;
        private bool _hoveredOnCheck = false;

        private TreeListSelectionMode _selectionMode = TreeListSelectionMode.Single;
        private readonly List<ZeroTreeNode> _selectedNodes = new List<ZeroTreeNode>();

        private bool _allowInPlaceEditing = false;
        private ZeroTreeNode? _editingNode;
        private TreeListColumn? _editingColumn;
        private Control? _activeEditor;

        private bool _allowDragDrop = false;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private ZeroTreeNode? _dragCandidateNode;
        private ZeroTreeNode? _dropTargetNode;
        private TreeListDropPosition _dropPosition = TreeListDropPosition.None;

        private int _scrollOffset = 0;
        private readonly VScrollBar _vScrollBar;

        public event EventHandler<ZeroTreeNode>? NodeSelected;
        public event EventHandler<ZeroTreeNode>? NodeCheckChanged;
        public event EventHandler<ZeroTreeNode>? NodeExpandedChanged;
        public event EventHandler<TreeListBeforeExpandEventArgs>? BeforeExpand;
        public event EventHandler<TreeListVirtualLoadEventArgs>? VirtualLoadChildren;
        public event EventHandler<TreeListShowingEditorEventArgs>? ShowingEditor;
        public event EventHandler<TreeListCellValueChangedEventArgs>? CellValueChanged;
        public event EventHandler<TreeListValidatingEditorEventArgs>? ValidatingEditor;
        public event EventHandler? HiddenEditor;
        public event EventHandler<TreeListBeforeDragEventArgs>? BeforeDragNode;
        public event EventHandler<TreeListDragOverEventArgs>? DragOverNode;
        public event EventHandler<TreeListAfterDropEventArgs>? AfterDropNode;
        public event EventHandler? SummariesRecalculated;

        public ZTreeList()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Font = new Font("Segoe UI", 9f);
            BackColor = Color.Transparent;

            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Width = 14,
                Visible = false
            };
            _vScrollBar.Scroll += (s, e) =>
            {
                _scrollOffset = _vScrollBar.Value;
                Invalidate();
            };
            Controls.Add(_vScrollBar);

            Size = new Size(380, 420);

            MouseWheel += OnMouseWheelScroll;
            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
        }

        [Browsable(false)]
        public List<ZeroTreeNode> Nodes => _nodes;

        [Category("Columns")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<TreeListColumn> Columns => _columns;

        [Category("Appearance")]
        [DefaultValue(28)]
        public int HeaderHeight
        {
            get => _headerHeight;
            set
            {
                if (_headerHeight != value && value >= 18)
                {
                    _headerHeight = value;
                    UpdateScrollBar();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowColumnHeaders
        {
            get => _showColumnHeaders;
            set
            {
                if (_showColumnHeaders != value)
                {
                    _showColumnHeaders = value;
                    UpdateScrollBar();
                    Invalidate();
                }
            }
        }

        public TreeListColumn AddColumn(string name, string caption, int width = 120, HorizontalAlignment alignment = HorizontalAlignment.Left)
        {
            var col = new TreeListColumn(name, caption, width) { Alignment = alignment };
            _columns.Add(col);
            UpdateScrollBar();
            Invalidate();
            return col;
        }

        public void SortByColumn(TreeListColumn col)
        {
            var next = col.SortOrder switch
            {
                SortOrder.Ascending => SortOrder.Descending,
                SortOrder.Descending => SortOrder.None,
                _ => SortOrder.Ascending
            };

            foreach (var c in _columns) c.SortOrder = SortOrder.None;
            col.SortOrder = next;

            if (next != SortOrder.None)
            {
                SortNodesRecursive(_nodes, col.Name, next == SortOrder.Ascending);
            }
            UpdateVisibleNodes();
            Invalidate();
        }

        private void SortNodesRecursive(List<ZeroTreeNode> list, string colName, bool ascending)
        {
            list.Sort((a, b) =>
            {
                object? valA = a[colName] ?? a.Text;
                object? valB = b[colName] ?? b.Text;
                string sA = valA?.ToString() ?? "";
                string sB = valB?.ToString() ?? "";
                int cmp = string.Compare(sA, sB, StringComparison.OrdinalIgnoreCase);
                return ascending ? cmp : -cmp;
            });

            foreach (var n in list)
            {
                if (n.HasChildren)
                {
                    SortNodesRecursive(n.Children, colName, ascending);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(30)]
        public int RowHeight
        {
            get => _rowHeight;
            set
            {
                if (_rowHeight != value && value >= 18)
                {
                    _rowHeight = value;
                    UpdateVisibleNodes();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(24)]
        public int IndentWidth
        {
            get => _indentWidth;
            set
            {
                if (_indentWidth != value && value >= 12)
                {
                    _indentWidth = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowCheckBoxes
        {
            get => _showCheckBoxes;
            set
            {
                if (_showCheckBoxes != value)
                {
                    _showCheckBoxes = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLines
        {
            get => _showLines;
            set
            {
                if (_showLines != value)
                {
                    _showLines = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        public string FilterText
        {
            get => _filterText;
            set
            {
                var val = value?.Trim() ?? "";
                if (_filterText != val)
                {
                    _filterText = val;
                    UpdateVisibleNodes();
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public ZeroTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    _selectedNodes.Clear();
                    if (_selectedNode != null)
                    {
                        _selectedNodes.Add(_selectedNode);
                    }
                    Invalidate();
                    if (_selectedNode != null)
                    {
                        NodeSelected?.Invoke(this, _selectedNode);
                    }
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(TreeListSelectionMode.Single)]
        [Description("Controls whether single or multiple nodes can be selected simultaneously.")]
        public TreeListSelectionMode SelectionMode
        {
            get => _selectionMode;
            set
            {
                if (_selectionMode != value)
                {
                    _selectionMode = value;
                    if (_selectionMode == TreeListSelectionMode.Single && _selectedNodes.Count > 1)
                    {
                        var primary = _selectedNode ?? _selectedNodes[0];
                        _selectedNodes.Clear();
                        _selectedNodes.Add(primary);
                        _selectedNode = primary;
                    }
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public IReadOnlyList<ZeroTreeNode> SelectedNodes => _selectedNodes;

        public void SelectNode(ZeroTreeNode node, bool addToSelection = false)
        {
            if (node == null) return;
            if (!addToSelection || _selectionMode == TreeListSelectionMode.Single)
            {
                _selectedNodes.Clear();
                _selectedNodes.Add(node);
                _selectedNode = node;
                Invalidate();
                NodeSelected?.Invoke(this, node);
            }
            else
            {
                if (!_selectedNodes.Contains(node))
                {
                    _selectedNodes.Add(node);
                }
                _selectedNode = node;
                Invalidate();
                NodeSelected?.Invoke(this, node);
            }
        }

        public void DeselectNode(ZeroTreeNode node)
        {
            if (node == null) return;
            if (_selectedNodes.Remove(node))
            {
                if (_selectedNode == node)
                {
                    _selectedNode = _selectedNodes.Count > 0 ? _selectedNodes[_selectedNodes.Count - 1] : null;
                }
                Invalidate();
            }
        }

        public void ClearSelection()
        {
            _selectedNodes.Clear();
            _selectedNode = null;
            Invalidate();
        }

        public bool IsNodeSelected(ZeroTreeNode node)
        {
            if (node == null) return false;
            return _selectionMode == TreeListSelectionMode.MultiSelect
                ? _selectedNodes.Contains(node)
                : _selectedNode == node;
        }

        public void AddNode(ZeroTreeNode node)
        {
            _nodes.Add(node);
            UpdateVisibleNodes();
            Invalidate();
        }

        public void ClearNodes()
        {
            _nodes.Clear();
            _visibleNodes.Clear();
            _selectedNodes.Clear();
            _selectedNode = null;
            _hoveredNode = null;
            UpdateScrollBar();
            Invalidate();
        }

        public void ExpandAll()
        {
            foreach (var node in _nodes)
            {
                node.ExpandAll();
            }
            UpdateVisibleNodes();
            Invalidate();
        }

        public void CollapseAll()
        {
            foreach (var node in _nodes)
            {
                node.CollapseAll();
            }
            UpdateVisibleNodes();
            Invalidate();
        }

        public void UpdateVisibleNodes()
        {
            _visibleNodes.Clear();
            bool hasFilter = !string.IsNullOrEmpty(_filterText);

            foreach (var root in _nodes)
            {
                CollectVisible(root, hasFilter);
            }

            UpdateScrollBar();
        }

        private bool CollectVisible(ZeroTreeNode node, bool hasFilter)
        {
            if (!hasFilter)
            {
                _visibleNodes.Add(node);
                if (node.IsExpanded && node.HasChildren)
                {
                    foreach (var child in node.Children)
                    {
                        CollectVisible(child, false);
                    }
                }
                return true;
            }

            // In filter mode, match self or any descendants
            bool selfMatches = node.Text.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               node.SubText.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0;

            var matchingChildren = new List<ZeroTreeNode>();
            foreach (var child in node.Children)
            {
                if (CollectVisibleChildCheck(child))
                {
                    matchingChildren.Add(child);
                }
            }

            if (selfMatches || matchingChildren.Count > 0)
            {
                _visibleNodes.Add(node);
                node.IsExpanded = true; // Auto-expand when filtering
                foreach (var child in matchingChildren)
                {
                    CollectVisible(child, true);
                }
                return true;
            }

            return false;
        }

        private bool CollectVisibleChildCheck(ZeroTreeNode node)
        {
            if (node.Text.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                node.SubText.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (var child in node.Children)
            {
                if (CollectVisibleChildCheck(child)) return true;
            }

            return false;
        }

        private void UpdateScrollBar()
        {
            if (_vScrollBar == null || _visibleNodes == null) return;
            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            int footerOffset = (_showFooter && _columns.Count > 0) ? _footerHeight : 0;
            int totalHeight = _visibleNodes.Count * _rowHeight;
            int viewHeight = Height - headerOffset - footerOffset;

            if (totalHeight > viewHeight && viewHeight > 0)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Maximum = Math.Max(0, _visibleNodes.Count - (viewHeight / _rowHeight) + 1);
                _vScrollBar.LargeChange = Math.Max(1, viewHeight / _rowHeight);
            }
            else
            {
                _vScrollBar.Visible = false;
                _scrollOffset = 0;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
        }

        private void OnMouseWheelScroll(object? sender, MouseEventArgs e)
        {
            if (!_vScrollBar.Visible) return;
            int delta = e.Delta > 0 ? -2 : 2;
            int newVal = Math.Max(0, Math.Min(_vScrollBar.Maximum, _scrollOffset + delta));
            if (newVal != _scrollOffset)
            {
                _scrollOffset = newVal;
                _vScrollBar.Value = _scrollOffset;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int clientWidth = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;

            // 1. Column Resizing Drag
            if (_resizingColumnIndex >= 0 && _resizingColumnIndex < _columns.Count)
            {
                int delta = e.X - _resizeStartX;
                var col = _columns[_resizingColumnIndex];
                col.Width = Math.Max(col.MinWidth, _resizeStartWidth + delta);
                Invalidate();
                return;
            }

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;

            // 2. Column Header Hover & Divider VSplit detection
            if (headerOffset > 0 && e.Y < headerOffset)
            {
                _hoveredNode = null;
                int colIdx = -1;
                bool nearDivider = false;
                int curX = 0;

                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    if (!col.Visible) continue;
                    int rightEdge = curX + col.Width;

                    if (Math.Abs(e.X - rightEdge) <= 4)
                    {
                        colIdx = c;
                        nearDivider = true;
                        break;
                    }
                    else if (e.X >= curX && e.X < rightEdge)
                    {
                        colIdx = c;
                        break;
                    }
                    curX = rightEdge;
                }

                _hoveredColumnIndex = colIdx;
                _hoveredOnDivider = nearDivider;
                Cursor = nearDivider ? Cursors.VSplit : Cursors.Default;
                Invalidate();
                return;
            }

            _hoveredColumnIndex = -1;
            _hoveredOnDivider = false;

            if (e.X > clientWidth) return;

            // 2.5 Drag & Drop Candidate Detection
            if (_allowDragDrop && (e.Button & MouseButtons.Left) == MouseButtons.Left && _dragCandidateNode != null && !_isDragging)
            {
                int dx = Math.Abs(e.X - _dragStartPoint.X);
                int dy = Math.Abs(e.Y - _dragStartPoint.Y);
                if (dx > 4 || dy > 4)
                {
                    _isDragging = true;
                    var beforeArgs = new TreeListBeforeDragEventArgs(_dragCandidateNode);
                    BeforeDragNode?.Invoke(this, beforeArgs);
                    if (!beforeArgs.Cancel)
                    {
                        DoDragDrop(_dragCandidateNode, DragDropEffects.Move);
                    }
                    _isDragging = false;
                    _dragCandidateNode = null;
                    _dropTargetNode = null;
                    _dropPosition = TreeListDropPosition.None;
                    Invalidate();
                    return;
                }
            }

            // 3. Tree Rows Hover
            int index = ((e.Y - headerOffset) / _rowHeight) + _scrollOffset;
            if (index >= 0 && index < _visibleNodes.Count)
            {
                var node = _visibleNodes[index];
                bool onChevron = node.ChevronBounds.Contains(e.Location);
                bool onCheck = node.CheckBounds.Contains(e.Location);

                if (_hoveredNode != node || _hoveredOnChevron != onChevron || _hoveredOnCheck != onCheck)
                {
                    _hoveredNode = node;
                    _hoveredOnChevron = onChevron;
                    _hoveredOnCheck = onCheck;
                    Cursor = (onChevron || onCheck) ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }
            else if (_hoveredNode != null)
            {
                _hoveredNode = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredNode = null;
            _hoveredColumnIndex = -1;
            _hoveredOnDivider = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            if (headerOffset > 0 && e.Y < headerOffset)
            {
                int curX = 0;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    if (!col.Visible) continue;
                    int rightEdge = curX + col.Width;

                    if (Math.Abs(e.X - rightEdge) <= 4)
                    {
                        _resizingColumnIndex = c;
                        _resizeStartX = e.X;
                        _resizeStartWidth = col.Width;
                        Capture = true;
                        return;
                    }
                    else if (e.X >= curX && e.X < rightEdge)
                    {
                        SortByColumn(col);
                        return;
                    }
                    curX = rightEdge;
                }
                return;
            }

            int clientWidth = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;
            if (e.X > clientWidth) return;

            int index = ((e.Y - headerOffset) / _rowHeight) + _scrollOffset;
            if (index < 0 || index >= _visibleNodes.Count) return;

            var node = _visibleNodes[index];

            if (node.ChevronBounds.Contains(e.Location) && node.HasChildren)
            {
                if (node.IsLazyLoadable && !node.HasLoadedChildren)
                {
                    TriggerLazyLoad(node);
                }
                node.IsExpanded = !node.IsExpanded;
                UpdateVisibleNodes();
                NodeExpandedChanged?.Invoke(this, node);
                Invalidate();
                return;
            }

            if (_showCheckBoxes && node.CheckBounds.Contains(e.Location))
            {
                var nextState = (node.CheckState == CheckState.Checked) ? CheckState.Unchecked : CheckState.Checked;
                node.SetCheckState(nextState, true);
                NodeCheckChanged?.Invoke(this, node);
                Invalidate();
                return;
            }

            if (_allowDragDrop)
            {
                _dragStartPoint = e.Location;
                _dragCandidateNode = node;
            }

            if (_selectionMode == TreeListSelectionMode.MultiSelect)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    if (_selectedNodes.Contains(node))
                        DeselectNode(node);
                    else
                        SelectNode(node, addToSelection: true);
                }
                else if ((ModifierKeys & Keys.Shift) == Keys.Shift && _selectedNode != null)
                {
                    int fromIdx = _visibleNodes.IndexOf(_selectedNode);
                    int toIdx = _visibleNodes.IndexOf(node);
                    if (fromIdx >= 0 && toIdx >= 0)
                    {
                        _selectedNodes.Clear();
                        int start = Math.Min(fromIdx, toIdx);
                        int end = Math.Max(fromIdx, toIdx);
                        for (int k = start; k <= end; k++)
                        {
                            _selectedNodes.Add(_visibleNodes[k]);
                        }
                        _selectedNode = node;
                        Invalidate();
                        NodeSelected?.Invoke(this, node);
                    }
                }
                else
                {
                    SelectNode(node, addToSelection: false);
                }
            }
            else
            {
                SelectedNode = node;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizingColumnIndex >= 0)
            {
                _resizingColumnIndex = -1;
                Capture = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            g.Clear(palette.Background);

            int clientWidth = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;
            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;

            // 1. Draw Column Headers
            if (headerOffset > 0)
            {
                int curColX = 0;
                using var headerBgBrush = new SolidBrush(palette.HeaderBackground);
                using var headerBorderPen = new Pen(palette.Border, 1f);
                using var headerTextBrush = new SolidBrush(palette.TextPrimary);
                using var headerFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

                g.FillRectangle(headerBgBrush, 0, 0, clientWidth, _headerHeight);
                g.DrawLine(headerBorderPen, 0, _headerHeight - 1, clientWidth, _headerHeight - 1);

                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    if (!col.Visible) continue;
                    col.HeaderBounds = new Rectangle(curColX, 0, col.Width, _headerHeight);

                    // Hover highlight on header
                    if (_hoveredColumnIndex == c && !_hoveredOnDivider)
                    {
                        using var hovBrush = new SolidBrush(Color.FromArgb(20, palette.Primary));
                        g.FillRectangle(hovBrush, col.HeaderBounds);
                    }

                    // Divider line on right
                    g.DrawLine(headerBorderPen, curColX + col.Width - 1, 0, curColX + col.Width - 1, _headerHeight);

                    // Header Text
                    int textRight = curColX + col.Width - 16;
                    var textRect = new Rectangle(curColX + 8, 0, Math.Max(10, textRight - curColX - 8), _headerHeight);

                    var sf = new StringFormat
                    {
                        Alignment = col.Alignment switch
                        {
                            HorizontalAlignment.Center => StringAlignment.Center,
                            HorizontalAlignment.Right => StringAlignment.Far,
                            _ => StringAlignment.Near
                        },
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(col.Caption, headerFont, headerTextBrush, textRect, sf);

                    // Sort glyph
                    if (col.SortOrder != SortOrder.None)
                    {
                        int glyphX = curColX + col.Width - 12;
                        int glyphY = _headerHeight / 2;
                        using var sortBrush = new SolidBrush(palette.Primary);
                        PointF[] arrow = col.SortOrder == SortOrder.Ascending
                            ? new[] { new PointF(glyphX - 3.5f, glyphY + 2f), new PointF(glyphX + 3.5f, glyphY + 2f), new PointF(glyphX, glyphY - 2.5f) }
                            : new[] { new PointF(glyphX - 3.5f, glyphY - 2f), new PointF(glyphX + 3.5f, glyphY - 2f), new PointF(glyphX, glyphY + 2.5f) };
                        g.FillPolygon(sortBrush, arrow);
                    }

                    curColX += col.Width;
                }
            }

            int footerOffset = (_showFooter && _columns.Count > 0) ? _footerHeight : 0;
            int startIdx = _scrollOffset;
            int maxVisible = ((Height - headerOffset - footerOffset) / _rowHeight) + 2;
            int endIdx = Math.Min(_visibleNodes.Count, startIdx + maxVisible);

            using var penGuide = new Pen(palette.Border, 1f) { DashStyle = DashStyle.Dot };
            using var fontText = new Font(Font.FontFamily, 9.2f, FontStyle.Regular);
            using var fontBold = new Font(Font.FontFamily, 9.2f, FontStyle.Bold);
            using var fontSub = new Font(Font.FontFamily, 8f, FontStyle.Regular);
            using var fontBadge = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
            using var penColDivider = new Pen(Color.FromArgb(20, palette.Border), 1f);

            for (int i = startIdx; i < endIdx; i++)
            {
                var node = _visibleNodes[i];
                int y = headerOffset + (i - startIdx) * _rowHeight;
                node.RowBounds = new Rectangle(0, y, clientWidth, _rowHeight);

                bool isSelected = IsNodeSelected(node);
                bool isHovered = node == _hoveredNode;

                // 1. Draw Row Background
                if (isSelected)
                {
                    using var brushSel = new SolidBrush(Color.FromArgb(40, palette.Primary));
                    g.FillRectangle(brushSel, node.RowBounds);
                    using var penLeft = new SolidBrush(palette.Primary);
                    g.FillRectangle(penLeft, new Rectangle(0, y, 3, _rowHeight));
                }
                else if (isHovered)
                {
                    using var brushHov = new SolidBrush(Color.FromArgb(15, palette.Primary));
                    g.FillRectangle(brushHov, node.RowBounds);
                }

                if (_columns.Count > 0)
                {
                    // MULTI-COLUMN RENDERING MODE
                    int curColX = 0;
                    for (int c = 0; c < _columns.Count; c++)
                    {
                        var col = _columns[c];
                        if (!col.Visible) continue;
                        int colW = col.Width;

                        if (c == 0)
                        {
                            // Column 0 renders hierarchy tree
                            var prevClip = g.Clip;
                            g.SetClip(new Rectangle(curColX, y, colW, _rowHeight));

                            int indentX = curColX + 12 + (node.Level * _indentWidth);

                            if (_showLines && node.Level > 0)
                            {
                                int parentLineX = indentX - (_indentWidth / 2);
                                int midY = y + (_rowHeight / 2);
                                g.DrawLine(penGuide, parentLineX, y, parentLineX, midY);
                                g.DrawLine(penGuide, parentLineX, midY, indentX - 4, midY);
                            }

                            node.ChevronBounds = new Rectangle(indentX, y + ((_rowHeight - 16) / 2), 16, 16);
                            if (node.HasChildren)
                            {
                                Color chevColor = (_hoveredNode == node && _hoveredOnChevron) ? palette.Primary : palette.TextSecondary;
                                DrawChevron(g, node.ChevronBounds, node.IsExpanded, chevColor);
                            }

                            int curX = indentX + 18;
                            if (_showCheckBoxes)
                            {
                                node.CheckBounds = new Rectangle(curX, y + ((_rowHeight - 16) / 2), 16, 16);
                                DrawCheckBox(g, node.CheckBounds, node.CheckState, palette);
                                curX += 22;
                            }
                            else
                            {
                                node.CheckBounds = Rectangle.Empty;
                            }

                            if (!string.IsNullOrEmpty(node.Icon))
                            {
                                using var iconFont = new Font("Segoe UI Emoji", 10f);
                                using var brushIcon = new SolidBrush(palette.TextPrimary);
                                g.DrawString(node.Icon, iconFont, brushIcon, curX, y + ((_rowHeight - 18) / 2));
                                curX += 20;
                            }

                            int col0TextW = Math.Max(10, (curColX + colW) - curX - 6);
                            var col0TextRect = new Rectangle(curX, y, col0TextW, _rowHeight);
                            string cell0Text = node[col.Name]?.ToString() ?? node.Text;
                            Color text0Color = isSelected ? palette.Primary : palette.TextPrimary;
                            var font0 = node.HasChildren ? fontBold : fontText;
                            TextRenderer.DrawText(g, cell0Text, font0, col0TextRect, text0Color,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

                            g.Clip = prevClip;
                        }
                        else
                        {
                            // Columns 1..N render tabular cell values
                            string cellText = node[col.Name]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(cellText))
                            {
                                var cellTextRect = new Rectangle(curColX + 6, y, Math.Max(10, colW - 12), _rowHeight);
                                TextFormatFlags tff = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
                                tff |= col.Alignment switch
                                {
                                    HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
                                    HorizontalAlignment.Right => TextFormatFlags.Right,
                                    _ => TextFormatFlags.Left
                                };
                                TextRenderer.DrawText(g, cellText, fontText, cellTextRect, palette.TextPrimary, tff);
                            }
                        }

                        // Column vertical divider line
                        g.DrawLine(penColDivider, curColX + colW - 1, y, curColX + colW - 1, y + _rowHeight);
                        curColX += colW;
                    }
                }
                else
                {
                    // SINGLE-COLUMN BADGE & SUBTEXT MODE (100% Backward Compatible)
                    int indentX = 12 + (node.Level * _indentWidth);

                    if (_showLines && node.Level > 0)
                    {
                        int parentLineX = indentX - (_indentWidth / 2);
                        int midY = y + (_rowHeight / 2);
                        g.DrawLine(penGuide, parentLineX, y, parentLineX, midY);
                        g.DrawLine(penGuide, parentLineX, midY, indentX - 4, midY);
                    }

                    node.ChevronBounds = new Rectangle(indentX, y + ((_rowHeight - 16) / 2), 16, 16);
                    if (node.HasChildren)
                    {
                        Color chevColor = (_hoveredNode == node && _hoveredOnChevron) ? palette.Primary : palette.TextSecondary;
                        DrawChevron(g, node.ChevronBounds, node.IsExpanded, chevColor);
                    }

                    int curX = indentX + 18;

                    if (_showCheckBoxes)
                    {
                        node.CheckBounds = new Rectangle(curX, y + ((_rowHeight - 16) / 2), 16, 16);
                        DrawCheckBox(g, node.CheckBounds, node.CheckState, palette);
                        curX += 22;
                    }
                    else
                    {
                        node.CheckBounds = Rectangle.Empty;
                    }

                    if (!string.IsNullOrEmpty(node.Icon))
                    {
                        using var iconFont = new Font("Segoe UI Emoji", 10f);
                        using var brushIcon = new SolidBrush(palette.TextPrimary);
                        g.DrawString(node.Icon, iconFont, brushIcon, curX, y + ((_rowHeight - 18) / 2));
                        curX += 20;
                    }

                    int badgeReserved = 0;
                    Rectangle badgeRect = Rectangle.Empty;
                    if (!string.IsNullOrEmpty(node.Badge))
                    {
                        var bColor = node.BadgeColor ?? palette.Primary;
                        var bTextSz = g.MeasureString(node.Badge, fontBadge);
                        int badgeW = (int)bTextSz.Width + 12;
                        int badgeH = 20;
                        int badgeX = clientWidth - badgeW - 10;
                        int badgeY = y + ((_rowHeight - badgeH) / 2);
                        badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                        badgeReserved = badgeW + 16;
                    }

                    int rightLimit = clientWidth - badgeReserved - 6;
                    int availableW = Math.Max(20, rightLimit - curX);

                    Color textColor = isSelected ? palette.Primary : palette.TextPrimary;
                    var activeFont = node.HasChildren ? fontBold : fontText;

                    bool hasSub = !string.IsNullOrEmpty(node.SubText) && availableW >= 180;

                    if (hasSub)
                    {
                        var titleSz = g.MeasureString(node.Text, activeFont);
                        int desiredTitleW = (int)titleSz.Width + 6;
                        int titleW = Math.Min(desiredTitleW, (int)(availableW * 0.55f));
                        if (titleW < 100 && availableW > 140) titleW = Math.Min(desiredTitleW, availableW - 70);

                        var titleRect = new Rectangle(curX, y, titleW, _rowHeight);
                        TextRenderer.DrawText(g, node.Text, activeFont, titleRect, textColor,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

                        int subX = curX + titleW + 8;
                        int subW = Math.Max(0, rightLimit - subX);
                        if (subW > 25)
                        {
                            var subRect = new Rectangle(subX, y, subW, _rowHeight);
                            TextRenderer.DrawText(g, node.SubText, fontSub, subRect, palette.TextSecondary,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                        }
                    }
                    else
                    {
                        var titleRect = new Rectangle(curX, y, availableW, _rowHeight);
                        TextRenderer.DrawText(g, node.Text, activeFont, titleRect, textColor,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    }

                    if (!badgeRect.IsEmpty)
                    {
                        var bColor = node.BadgeColor ?? palette.Primary;
                        using var bBrush = new SolidBrush(Color.FromArgb(35, bColor));
                        using var bPen = new Pen(bColor, 1f);
                        int effBadgeRadius = ZeroUIConfig.GetEffectiveRadius(4);
                        using var bPath = CreateRoundedRect(badgeRect, effBadgeRadius);
                        g.FillPath(bBrush, bPath);
                        g.DrawPath(bPen, bPath);

                        using var bTextBrush = new SolidBrush(bColor);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(node.Badge, fontBadge, bTextBrush, badgeRect, sf);
                    }
                }

                // Separator bottom line
                using var penSep = new Pen(Color.FromArgb(12, palette.Border));
                g.DrawLine(penSep, 0, y + _rowHeight - 1, clientWidth, y + _rowHeight - 1);
            }

            // Render Drop Feedback Indicator (Drag & Drop)
            DrawDragDropIndicator(g, palette, clientWidth);

            // Render Footer Summary Bar
            if (_showFooter && _columns.Count > 0)
            {
                DrawFooter(g, palette, clientWidth, headerOffset);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!_allowInPlaceEditing) return;

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            int clientWidth = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;
            if (e.X > clientWidth || e.Y < headerOffset) return;

            int index = ((e.Y - headerOffset) / _rowHeight) + _scrollOffset;
            if (index < 0 || index >= _visibleNodes.Count) return;

            var node = _visibleNodes[index];
            if (node.ChevronBounds.Contains(e.Location) || node.CheckBounds.Contains(e.Location)) return;

            // Determine which column was clicked
            int curX = 0;
            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (!col.Visible) continue;
                if (e.X >= curX && e.X < curX + col.Width)
                {
                    ShowEditor(node, col);
                    break;
                }
                curX += col.Width;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.F2 && _allowInPlaceEditing && _selectedNode != null && _columns.Count > 0)
            {
                ShowEditor(_selectedNode, _columns[0]);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.A && e.Control && _selectionMode == TreeListSelectionMode.MultiSelect)
            {
                _selectedNodes.Clear();
                _selectedNodes.AddRange(_visibleNodes);
                Invalidate();
                e.Handled = true;
            }
        }

        private void DrawChevron(Graphics g, Rectangle bounds, bool isExpanded, Color color)
        {
            using var brush = new SolidBrush(color);
            var center = new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
            PointF[] points;

            if (isExpanded)
            {
                // Down arrow (▼)
                points = new PointF[]
                {
                    new PointF(center.X - 4f, center.Y - 2.5f),
                    new PointF(center.X + 4f, center.Y - 2.5f),
                    new PointF(center.X, center.Y + 3.5f)
                };
            }
            else
            {
                // Right arrow (▶)
                points = new PointF[]
                {
                    new PointF(center.X - 2.5f, center.Y - 4f),
                    new PointF(center.X + 3.5f, center.Y),
                    new PointF(center.X - 2.5f, center.Y + 4f)
                };
            }

            g.FillPolygon(brush, points);
        }

        private void DrawCheckBox(Graphics g, Rectangle bounds, CheckState state, ZeroThemePalette palette)
        {
            using var path = CreateRoundedRect(bounds, 3);

            if (state == CheckState.Checked)
            {
                using var brush = new SolidBrush(palette.Primary);
                g.FillPath(brush, path);

                using var penCheck = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                PointF p1 = new PointF(bounds.X + 3.5f, bounds.Y + 8f);
                PointF p2 = new PointF(bounds.X + 6.5f, bounds.Y + 11.5f);
                PointF p3 = new PointF(bounds.X + 12.5f, bounds.Y + 4.5f);
                g.DrawLines(penCheck, new[] { p1, p2, p3 });
            }
            else if (state == CheckState.Indeterminate)
            {
                using var brush = new SolidBrush(palette.Primary);
                g.FillPath(brush, path);

                using var brushBar = new SolidBrush(Color.White);
                g.FillRectangle(brushBar, new Rectangle(bounds.X + 3, bounds.Y + 7, 10, 2));
            }
            else
            {
                using var brushBg = new SolidBrush(palette.Surface);
                using var penBorder = new Pen(palette.Border, 1.2f);
                g.FillPath(brushBg, path);
                g.DrawPath(penBorder, path);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(r, radius);
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for TreeList.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroTreeList is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeList instead.")]
    [ToolboxItem(false)]
    public class ZeroTreeList : ZTreeList
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTreeList"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TreeList is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeList instead.")]
    [ToolboxItem(false)]
    public class TreeList : ZTreeList
    {
    }

    #endregion
}
