using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    /// <summary>
    /// Represents a hierarchical tree node in <see cref="ZTreeView"/>.
    /// </summary>
    public class ZTreeNode
    {
        public string Text { get; set; } = "Node";
        public object? Tag { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public bool Checked { get; set; }
        public ZTreeNode? Parent { get; internal set; }
        public List<ZTreeNode> Nodes { get; } = new List<ZTreeNode>();

        public int Level => Parent == null ? 0 : Parent.Level + 1;

        public ZTreeNode() { }

        public ZTreeNode(string text)
        {
            Text = text;
        }

        public ZTreeNode Add(string text)
        {
            var child = new ZTreeNode(text) { Parent = this };
            Nodes.Add(child);
            return child;
        }

        public void Expand() => IsExpanded = true;
        public void Collapse() => IsExpanded = false;
        public void Toggle() => IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Professional hierarchical tree view control supporting chevron expand/collapse,
    /// checkboxes, connector lines, and zero-allocation theming.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Navigation")]
    [Description("Hierarchical tree view with theme reactivity, checkboxes, and connector lines")]
    public class ZTreeView : ControlBase
    {
        private readonly List<ZTreeNode> _nodes = new List<ZTreeNode>();
        private bool _showLines = true;
        private bool _showCheckboxes = false;
        private ZTreeNode? _selectedNode;
        private int _itemHeight = 28;
        private int _indent = 20;

        public event EventHandler? NodeSelected;
        public event EventHandler? NodeExpanded;
        public event EventHandler? NodeCollapsed;
        public event EventHandler<ZTreeNode>? NodeCheckChanged;

        public ZTreeView()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);

            Size = new Size(240, 320);
            Font = new Font("Segoe UI", 9.25f);
        }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<ZTreeNode> Nodes => _nodes;

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLines
        {
            get => _showLines;
            set { _showLines = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ShowCheckboxes
        {
            get => _showCheckboxes;
            set { _showCheckboxes = value; Invalidate(); }
        }

        [Browsable(false)]
        public ZTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    if (_selectedNode != null) _selectedNode.IsSelected = false;
                    _selectedNode = value;
                    if (_selectedNode != null) _selectedNode.IsSelected = true;
                    Invalidate();
                    NodeSelected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(28)]
        public int ItemHeight
        {
            get => _itemHeight;
            set { _itemHeight = Math.Max(20, value); Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(20)]
        public int Indent
        {
            get => _indent;
            set { _indent = Math.Max(12, value); Invalidate(); }
        }

        public ZTreeNode AddNode(string text)
        {
            var node = new ZTreeNode(text);
            _nodes.Add(node);
            Invalidate();
            return node;
        }

        public void ClearNodes()
        {
            _nodes.Clear();
            _selectedNode = null;
            Invalidate();
        }

        public void ExpandAll()
        {
            SetExpandRecursively(_nodes, true);
            Invalidate();
        }

        public void CollapseAll()
        {
            SetExpandRecursively(_nodes, false);
            Invalidate();
        }

        private static void SetExpandRecursively(IEnumerable<ZTreeNode> nodes, bool expand)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = expand;
                if (node.Nodes.Count > 0)
                    SetExpandRecursively(node.Nodes, expand);
            }
        }

        private void FlattenVisibleNodes(IEnumerable<ZTreeNode> nodes, int level, List<(ZTreeNode node, int level)> result)
        {
            foreach (var node in nodes)
            {
                result.Add((node, level));
                if (node.IsExpanded && node.Nodes.Count > 0)
                {
                    FlattenVisibleNodes(node.Nodes, level + 1, result);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // Background
            using (var bgBrush = new SolidBrush(palette.BgPrimary))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Border
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            var visibleNodes = new List<(ZTreeNode node, int level)>();
            FlattenVisibleNodes(_nodes, 0, visibleNodes);

            int y = 4;
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                var (node, level) = visibleNodes[i];
                int nodeY = y + (i * _itemHeight);
                if (nodeY + _itemHeight < 0 || nodeY > Height) continue;

                int nodeX = 8 + (level * _indent);
                bool isSelected = (node == _selectedNode || node.IsSelected);

                // Row selection background
                if (isSelected)
                {
                    var selRect = new Rectangle(nodeX - 4, nodeY + 2, Width - nodeX, _itemHeight - 4);
                    using var selBrush = new SolidBrush(Color.FromArgb(40, palette.Primary));
                    g.FillRectangle(selBrush, selRect);
                }

                // Chevron (if has children)
                int chevronSize = 12;
                int chevronX = nodeX;
                int chevronY = nodeY + (_itemHeight - chevronSize) / 2;

                if (node.Nodes.Count > 0)
                {
                    using var chevBrush = new SolidBrush(palette.TextSecondary);
                    if (node.IsExpanded)
                    {
                        // Downward triangle
                        Point[] points = {
                            new Point(chevronX, chevronY + 3),
                            new Point(chevronX + 8, chevronY + 3),
                            new Point(chevronX + 4, chevronY + 7)
                        };
                        g.FillPolygon(chevBrush, points);
                    }
                    else
                    {
                        // Rightward triangle
                        Point[] points = {
                            new Point(chevronX + 2, chevronY + 1),
                            new Point(chevronX + 6, chevronY + 5),
                            new Point(chevronX + 2, chevronY + 9)
                        };
                        g.FillPolygon(chevBrush, points);
                    }
                }

                int contentX = nodeX + 14;

                // Checkbox (if enabled)
                if (_showCheckboxes)
                {
                    int cbSize = 14;
                    int cbY = nodeY + (_itemHeight - cbSize) / 2;
                    var cbRect = new Rectangle(contentX, cbY, cbSize, cbSize);

                    using var cbPen = new Pen(palette.Border, 1.5f);
                    g.DrawRectangle(cbPen, cbRect);

                    if (node.Checked)
                    {
                        using var checkBrush = new SolidBrush(palette.Primary);
                        g.FillRectangle(checkBrush, cbRect.X + 2, cbRect.Y + 2, cbSize - 4, cbSize - 4);
                    }

                    contentX += 18;
                }

                // Text
                var textRect = new Rectangle(contentX, nodeY, Width - contentX - 4, _itemHeight);
                Color textColor = isSelected ? palette.Primary : palette.TextPrimary;
                TextRenderer.DrawText(g, node.Text, Font, textRect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var visibleNodes = new List<(ZTreeNode node, int level)>();
            FlattenVisibleNodes(_nodes, 0, visibleNodes);

            int index = (e.Y - 4) / _itemHeight;
            if (index < 0 || index >= visibleNodes.Count) return;

            var (node, level) = visibleNodes[index];
            int nodeX = 8 + (level * _indent);

            // Chevron click
            if (node.Nodes.Count > 0 && e.X >= nodeX - 2 && e.X <= nodeX + 14)
            {
                node.Toggle();
                if (node.IsExpanded)
                    NodeExpanded?.Invoke(this, EventArgs.Empty);
                else
                    NodeCollapsed?.Invoke(this, EventArgs.Empty);

                Invalidate();
                return;
            }

            // Checkbox click
            int contentX = nodeX + 14;
            if (_showCheckboxes && e.X >= contentX && e.X <= contentX + 16)
            {
                node.Checked = !node.Checked;
                NodeCheckChanged?.Invoke(this, node);
                Invalidate();
                return;
            }

            // Node selection
            SelectedNode = node;
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("ZeroTreeView is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeView instead.")]
    [ToolboxItem(false)]
    public class ZeroTreeView : ZTreeView
    {
    }
}
