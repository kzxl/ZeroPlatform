using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern hierarchical tree dropdown editor for WinForms.
    /// Ideal for selecting organization units, chart of accounts, BOM items, and multi-level categories.
    /// Features fast debounce search, auto-expand, fluent data-binding, and full theme integration.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent(nameof(SelectionChanged))]
    [DefaultProperty(nameof(SelectedText))]
    [Description("Hierarchical multi-level dropdown tree editor with fast search and auto-expand.")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTreeList.bmp")]
    public class ZTreeListLookUpEdit : ControlBase, IZeroEditor
    {
        private readonly DropDownHost _dropdown;
        private readonly Panel _popupPanel;
        private readonly ZTextBox _searchBox;
        private readonly ZButton _btnExpandAll;
        private readonly ZButton _btnCollapseAll;
        private readonly ZTreeList _treeList;
        private readonly Label _lblStatus;
        private readonly ZButton _btnClear;

        private string _placeholder = "Click to select from tree...";
        private string _displayMember = "Name";
        private string _valueMember = "Id";
        private string _keyMember = "Id";
        private string _parentMember = "ParentId";

        private object? _dataSource;
        private ZeroTreeNode? _selectedNode;
        private object? _selectedValue;
        private string _selectedText = string.Empty;

        private bool _isDroppedDown = false;
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isModified = false;
        private bool _readOnly = false;
        private bool _showClearButton = true;
        private bool _autoExpandOnOpen = true;

        private Rectangle _clearButtonRect = Rectangle.Empty;
        private Rectangle _chevronRect = Rectangle.Empty;
        private bool _isHoveredClear = false;

        public event EventHandler? SelectionChanged;
        public event EventHandler? EditValueChanged;
        public event EventHandler? DropDownOpened;
        public event EventHandler? DropDownClosed;

        public ZTreeListLookUpEdit()
        {
            Size = new Size(240, 34);
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;

            // Setup Popup Controls
            _popupPanel = new Panel
            {
                Width = 360,
                Height = 380,
                BackColor = ZeroTheme.Colors.BgCard,
                Padding = new Padding(8)
            };

            // Center TreeList
            _treeList = new ZTreeList
            {
                Dock = DockStyle.Fill,
                ShowCheckBoxes = false,
                ShowLines = true,
                ShowColumnHeaders = false
            };
            _treeList.NodeSelected += OnTreeNodeSelected;

            // Top Header: Search & Expand/Collapse buttons
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 6)
            };

            _searchBox = new ZTextBox
            {
                Placeholder = "Search tree nodes...",
                Dock = DockStyle.Fill,
                Height = 30
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _treeList.FilterText = _searchBox.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(_treeList.FilterText))
                {
                    _treeList.ExpandAll();
                }
                UpdateStatus();
            };

            _btnExpandAll = new ZButton
            {
                Text = "⊞",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Width = 30,
                Height = 30,
                Dock = DockStyle.Right
            };
            _btnExpandAll.Click += (s, e) => _treeList.ExpandAll();

            _btnCollapseAll = new ZButton
            {
                Text = "⊟",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Width = 30,
                Height = 30,
                Dock = DockStyle.Right
            };
            _btnCollapseAll.Click += (s, e) => _treeList.CollapseAll();

            headerPanel.Controls.Add(_searchBox);
            headerPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 4 });
            headerPanel.Controls.Add(_btnCollapseAll);
            headerPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 4 });
            headerPanel.Controls.Add(_btnExpandAll);

            // Bottom Footer: Status & Clear
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 4, 4, 0)
            };

            _lblStatus = new Label
            {
                Text = "Ready",
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 8.25f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnClear = new ZButton
            {
                Text = "Clear",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Width = 60,
                Height = 26,
                Dock = DockStyle.Right
            };
            _btnClear.Click += (s, e) =>
            {
                ClearSelection();
                CloseDropDown();
            };

            footerPanel.Controls.Add(_lblStatus);
            footerPanel.Controls.Add(_btnClear);

            _popupPanel.Controls.Add(_treeList);
            _popupPanel.Controls.Add(headerPanel);
            _popupPanel.Controls.Add(footerPanel);

            _dropdown = new DropDownHost
            {
                Content = _popupPanel
            };
            _dropdown.Opened += (s, e) =>
            {
                _isDroppedDown = true;
                if (_autoExpandOnOpen && string.IsNullOrEmpty(_searchBox.Text))
                {
                    _treeList.ExpandAll();
                }
                _searchBox.Focus();
                _searchBox.SelectAll();
                UpdateStatus();
                Invalidate();
                DropDownOpened?.Invoke(this, EventArgs.Empty);
            };
            _dropdown.Closed += (s, e) =>
            {
                _isDroppedDown = false;
                _isFocused = false;
                Invalidate();
                DropDownClosed?.Invoke(this, EventArgs.Empty);
            };

            
        }

        #region Properties

        [Category("ZeroUI - Appearance")]
        [Description("Placeholder text displayed when no node is selected.")]
        [DefaultValue("Click to select from tree...")]
        public string Placeholder
        {
            get => _placeholder;
            set { _placeholder = value; Invalidate(); }
        }

        [Category("ZeroUI - Data Binding")]
        [Description("Name of the property used as the tree node display text.")]
        [DefaultValue("Name")]
        public string DisplayMember
        {
            get => _displayMember;
            set => _displayMember = value;
        }

        [Category("ZeroUI - Data Binding")]
        [Description("Name of the property used as the selected value.")]
        [DefaultValue("Id")]
        public string ValueMember
        {
            get => _valueMember;
            set => _valueMember = value;
        }

        [Category("ZeroUI - Data Binding")]
        [Description("Name of the unique key property for hierarchy.")]
        [DefaultValue("Id")]
        public string KeyMember
        {
            get => _keyMember;
            set => _keyMember = value;
        }

        [Category("ZeroUI - Data Binding")]
        [Description("Name of the parent key property for hierarchy.")]
        [DefaultValue("ParentId")]
        public string ParentMember
        {
            get => _parentMember;
            set => _parentMember = value;
        }

        [Category("ZeroUI - Behavior")]
        [Description("Automatically expands all nodes when the dropdown opens.")]
        [DefaultValue(true)]
        public bool AutoExpandOnOpen
        {
            get => _autoExpandOnOpen;
            set => _autoExpandOnOpen = value;
        }

        [Category("ZeroUI - Behavior")]
        [Description("Shows an inline clear button when a node is selected.")]
        [DefaultValue(true)]
        public bool ShowClearButton
        {
            get => _showClearButton;
            set { _showClearButton = value; Invalidate(); }
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
                    _selectedText = value != null ? value.Text : string.Empty;
                    _selectedValue = value?.Tag;
                    _isModified = true;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public string SelectedText => _selectedText;

        [Browsable(false)]
        public object? SelectedValue => _selectedValue;

        [Category("ZeroUI - Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set { _readOnly = value; Invalidate(); }
        }

        [Browsable(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => _selectedValue;
            set => SelectByValue(value);
        }

        [Browsable(false)]
        public ZTreeList InnerTree => _treeList;

        [Browsable(false)]
        public List<ZeroTreeNode> Nodes => _treeList.Nodes;

        [Category("ZeroUI - Appearance")]
        [Description("Dropdown popup width.")]
        [DefaultValue(360)]
        public int PopupWidth
        {
            get => _popupPanel.Width;
            set => _popupPanel.Width = Math.Max(260, value);
        }

        [Category("ZeroUI - Appearance")]
        [Description("Dropdown popup height.")]
        [DefaultValue(380)]
        public int PopupHeight
        {
            get => _popupPanel.Height;
            set => _popupPanel.Height = Math.Max(200, value);
        }

        #endregion

        #region Data Binding Engine

        /// <summary>
        /// Binds an IEnumerable dataset into a hierarchical tree structure based on Key and ParentKey properties.
        /// Case-insensitive property lookup ensures robustness across varied DTO naming styles.
        /// </summary>
        public void SetDataSource<T>(IEnumerable<T> data, string keyMember, string parentMember, string displayMember, string? valueMember = null)
        {
            _keyMember = keyMember;
            _parentMember = parentMember;
            _displayMember = displayMember;
            _valueMember = valueMember ?? keyMember;
            _dataSource = data;

            BuildTreeFromCollection(data);
        }

        /// <summary>
        /// Binds a DataTable dataset into a hierarchical tree structure.
        /// </summary>
        public void SetDataSource(DataTable table, string keyMember, string parentMember, string displayMember, string? valueMember = null)
        {
            _keyMember = keyMember;
            _parentMember = parentMember;
            _displayMember = displayMember;
            _valueMember = valueMember ?? keyMember;
            _dataSource = table;

            BuildTreeFromDataTable(table);
        }

        private void BuildTreeFromCollection(IEnumerable items)
        {
            _treeList.ClearNodes();
            if (items == null) return;

            var nodeMap = new Dictionary<string, ZeroTreeNode>(StringComparer.OrdinalIgnoreCase);
            var parentRelations = new List<(ZeroTreeNode Node, string ParentId)>();

            foreach (var item in items)
            {
                if (item == null) continue;

                string id = GetPropertyValueCaseInsensitive(item, _keyMember)?.ToString() ?? string.Empty;
                string parentId = GetPropertyValueCaseInsensitive(item, _parentMember)?.ToString() ?? string.Empty;
                string text = GetPropertyValueCaseInsensitive(item, _displayMember)?.ToString() ?? id;
                object? val = GetPropertyValueCaseInsensitive(item, _valueMember);

                var node = new ZeroTreeNode(text)
                {
                    Id = id,
                    Tag = val ?? item
                };

                if (!string.IsNullOrEmpty(id))
                {
                    nodeMap[id] = node;
                }

                if (string.IsNullOrEmpty(parentId) || parentId == "0" || parentId == id)
                {
                    _treeList.Nodes.Add(node);
                }
                else
                {
                    parentRelations.Add((node, parentId));
                }
            }

            // Link parent-child relations
            foreach (var (child, pId) in parentRelations)
            {
                if (nodeMap.TryGetValue(pId, out var parentNode))
                {
                    parentNode.AddChild(child);
                }
                else
                {
                    // Orphan node treated as root
                    _treeList.Nodes.Add(child);
                }
            }

            UpdateStatus();
        }

        private void BuildTreeFromDataTable(DataTable table)
        {
            _treeList.ClearNodes();
            if (table == null || table.Rows.Count == 0) return;

            var nodeMap = new Dictionary<string, ZeroTreeNode>(StringComparer.OrdinalIgnoreCase);
            var parentRelations = new List<(ZeroTreeNode Node, string ParentId)>();

            foreach (DataRow row in table.Rows)
            {
                string id = GetRowValueCaseInsensitive(row, _keyMember)?.ToString() ?? string.Empty;
                string parentId = GetRowValueCaseInsensitive(row, _parentMember)?.ToString() ?? string.Empty;
                string text = GetRowValueCaseInsensitive(row, _displayMember)?.ToString() ?? id;
                object? val = GetRowValueCaseInsensitive(row, _valueMember);

                var node = new ZeroTreeNode(text)
                {
                    Id = id,
                    Tag = val ?? id
                };

                if (!string.IsNullOrEmpty(id))
                {
                    nodeMap[id] = node;
                }

                if (string.IsNullOrEmpty(parentId) || parentId == "0" || parentId == id)
                {
                    _treeList.Nodes.Add(node);
                }
                else
                {
                    parentRelations.Add((node, parentId));
                }
            }

            foreach (var (child, pId) in parentRelations)
            {
                if (nodeMap.TryGetValue(pId, out var parentNode))
                {
                    parentNode.AddChild(child);
                }
                else
                {
                    _treeList.Nodes.Add(child);
                }
            }

            UpdateStatus();
        }

        private static object? GetPropertyValueCaseInsensitive(object obj, string propName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propName)) return null;

            var type = obj.GetType();
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop?.GetValue(obj);
        }

        private static object? GetRowValueCaseInsensitive(DataRow row, string colName)
        {
            if (row == null || string.IsNullOrWhiteSpace(colName)) return null;
            if (row.Table.Columns.Contains(colName)) return row[colName];

            foreach (DataColumn col in row.Table.Columns)
            {
                if (string.Equals(col.ColumnName, colName, StringComparison.OrdinalIgnoreCase))
                {
                    return row[col];
                }
            }
            return null;
        }

        private void UpdateStatus()
        {
            int total = CountNodes(_treeList.Nodes);
            _lblStatus.Text = $"Nodes: {total} | Selected: {(_selectedNode != null ? _selectedNode.Text : "None")}";
        }

        private static int CountNodes(IEnumerable<ZeroTreeNode> nodes)
        {
            int count = 0;
            foreach (var node in nodes)
            {
                count++;
                if (node.HasChildren)
                {
                    count += CountNodes(node.Children);
                }
            }
            return count;
        }

        #endregion

        #region Selection Logic

        public void SelectByValue(object? value)
        {
            if (value == null)
            {
                ClearSelection();
                return;
            }

            var matched = FindNodeByValue(_treeList.Nodes, value);
            if (matched != null)
            {
                SelectedNode = matched;
            }
            else
            {
                _selectedValue = value;
                _selectedText = value.ToString() ?? string.Empty;
                Invalidate();
            }
        }

        private ZeroTreeNode? FindNodeByValue(IEnumerable<ZeroTreeNode> nodes, object val)
        {
            string valStr = val.ToString() ?? string.Empty;
            foreach (var n in nodes)
            {
                if (Equals(n.Tag, val) || string.Equals(n.Id, valStr, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Tag?.ToString(), valStr, StringComparison.OrdinalIgnoreCase))
                {
                    return n;
                }

                if (n.HasChildren)
                {
                    var childMatch = FindNodeByValue(n.Children, val);
                    if (childMatch != null) return childMatch;
                }
            }
            return null;
        }

        public void ClearSelection()
        {
            _selectedNode = null;
            _selectedValue = null;
            _selectedText = string.Empty;
            _isModified = true;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear() => ClearSelection();

        public void Reset()
        {
            ClearSelection();
            _isModified = false;
        }

        private void OnTreeNodeSelected(object? sender, ZeroTreeNode node)
        {
            if (_readOnly) return;
            SelectedNode = node;
            CloseDropDown();
        }

        public void ShowDropDown()
        {
            if (_readOnly || _isDroppedDown) return;

            int popW = Math.Max(Width, _popupPanel.Width);
            _popupPanel.Width = popW;
            _dropdown.Show(this, 0, Height + 2);
        }

        public void CloseDropDown()
        {
            if (_isDroppedDown)
            {
                _dropdown.Close();
            }
        }

        #endregion

        #region Overrides & Rendering

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isHoveredClear = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overClear = _clearButtonRect.Contains(e.Location);
            if (_isHoveredClear != overClear)
            {
                _isHoveredClear = overClear;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_readOnly) return;

            Focus();
            _isFocused = true;

            if (_showClearButton && !string.IsNullOrEmpty(_selectedText) && _clearButtonRect.Contains(e.Location))
            {
                ClearSelection();
                return;
            }

            if (_isDroppedDown)
            {
                CloseDropDown();
            }
            else
            {
                ShowDropDown();
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Alt | Keys.Down) || keyData == Keys.F4)
                {
                    ShowDropDown();
                    return true;
                }
                if (keyData == Keys.Escape && _isDroppedDown)
                {
                    CloseDropDown();
                    return true;
                }
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = ZeroUIConfig.GetEffectiveRadius(5);

            // Background
            using (var bgBrush = new SolidBrush(ZeroTheme.Colors.BgInput))
            using (var path = ZeroUIConfig.CreateRoundedRectangle(bounds, radius))
            {
                g.FillPath(bgBrush, path);
            }

            // Border (Stateful)
            Color borderColor = _isDroppedDown || _isFocused
                ? ZeroTheme.Colors.PrimaryAccent
                : _isHovered
                    ? Color.FromArgb(160, ZeroTheme.Colors.PrimaryAccent)
                    : ZeroTheme.Colors.BorderDefault;

            using (var borderPen = new Pen(borderColor, (_isDroppedDown || _isFocused) ? 1.5f : 1f))
            using (var path = ZeroUIConfig.CreateRoundedRectangle(bounds, radius))
            {
                g.DrawPath(borderPen, path);
            }

            // Left Icon: Tree glyph
            int leftPad = 10;
            using (var iconBrush = new SolidBrush(ZeroTheme.Colors.PrimaryAccent))
            using (var iconFont = new Font("Segoe UI Symbol", 9.5f))
            {
                g.DrawString("🗂", iconFont, iconBrush, leftPad, (Height - 16) / 2);
            }
            leftPad += 22;

            // Chevron Arrow
            int chevronW = 20;
            _chevronRect = new Rectangle(Width - chevronW - 6, 0, chevronW, Height);
            using (var arrowBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary))
            using (var arrowFont = new Font("Segoe UI", 7.5f))
            {
                g.DrawString("▼", arrowFont, arrowBrush, _chevronRect.X + 4, (Height - 11) / 2);
            }

            // Clear Button (if selected and enabled)
            int rightPad = _chevronRect.Width + 8;
            if (_showClearButton && !string.IsNullOrEmpty(_selectedText))
            {
                int clearSize = 16;
                int clearX = _chevronRect.X - clearSize - 4;
                int clearY = (Height - clearSize) / 2;
                _clearButtonRect = new Rectangle(clearX, clearY, clearSize, clearSize);

                Color clearColor = _isHoveredClear ? ZeroTheme.Colors.Danger : ZeroTheme.Colors.TextSecondary;
                using (var clearBrush = new SolidBrush(clearColor))
                using (var clearFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    g.DrawString("✕", clearFont, clearBrush, clearX + 2, clearY - 1);
                }
                rightPad += clearSize + 4;
            }
            else
            {
                _clearButtonRect = Rectangle.Empty;
            }

            // Text / Placeholder
            int textW = Width - leftPad - rightPad;
            if (textW > 10)
            {
                var textRect = new RectangleF(leftPad, 0, textW, Height);
                using (var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    if (!string.IsNullOrEmpty(_selectedText))
                    {
                        using var textBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary);
                        g.DrawString(_selectedText, Font, textBrush, textRect, sf);
                    }
                    else
                    {
                        using var phBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
                        using var italicFont = new Font(Font, FontStyle.Italic);
                        g.DrawString(_placeholder, italicFont, phBrush, textRect, sf);
                    }
                }
            }
        }

        #endregion
    
    protected override void OnThemeChanged(ZeroSkin skin)
    {
        base.OnThemeChanged(skin);
        _popupPanel.BackColor = ZeroTheme.Colors.BgCard;
                _lblStatus.ForeColor = ZeroTheme.Colors.TextSecondary;
                Invalidate();
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTreeListLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TreeListLookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeListLookUpEdit instead.")]
    [ToolboxItem(false)]
    public class TreeListLookUpEdit : ZTreeListLookUpEdit
    {
    }

    #endregion
}
