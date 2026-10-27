using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Navigation
{
    /// <summary>
    /// Represents a hierarchical tree node in WPF <see cref="ZTreeView"/>.
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
    /// High-performance vector tree view for WPF supporting hierarchical navigation,
    /// checkboxes, chevron expand/collapse, and full ZeroWpfTheme integration.
    /// </summary>
    public class ZTreeView : Control
    {
        private readonly List<ZTreeNode> _nodes = new List<ZTreeNode>();

        #region Dependency Properties

        public static readonly DependencyProperty ShowLinesProperty =
            DependencyProperty.Register(
                nameof(ShowLines),
                typeof(bool),
                typeof(ZTreeView),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCheckboxesProperty =
            DependencyProperty.Register(
                nameof(ShowCheckboxes),
                typeof(bool),
                typeof(ZTreeView),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectedNodeProperty =
            DependencyProperty.Register(
                nameof(SelectedNode),
                typeof(ZTreeNode),
                typeof(ZTreeView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedNodeChanged));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(
                nameof(ItemHeight),
                typeof(int),
                typeof(ZTreeView),
                new FrameworkPropertyMetadata(28, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IndentProperty =
            DependencyProperty.Register(
                nameof(Indent),
                typeof(int),
                typeof(ZTreeView),
                new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowLines
        {
            get => (bool)GetValue(ShowLinesProperty);
            set => SetValue(ShowLinesProperty, value);
        }

        public bool ShowCheckboxes
        {
            get => (bool)GetValue(ShowCheckboxesProperty);
            set => SetValue(ShowCheckboxesProperty, value);
        }

        public ZTreeNode? SelectedNode
        {
            get => (ZTreeNode?)GetValue(SelectedNodeProperty);
            set => SetValue(SelectedNodeProperty, value);
        }

        public int ItemHeight
        {
            get => (int)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, Math.Max(20, value));
        }

        public int Indent
        {
            get => (int)GetValue(IndentProperty);
            set => SetValue(IndentProperty, Math.Max(12, value));
        }

        #endregion

        #region Events

        public event EventHandler? NodeSelected;
        public event EventHandler? NodeExpanded;
        public event EventHandler? NodeCollapsed;
        public event EventHandler<ZTreeNode>? NodeCheckChanged;

        #endregion

        public List<ZTreeNode> Nodes => _nodes;

        public ZTreeView()
        {
            Focusable = true;
            ClipToBounds = true;
            Width = 240;
            Height = 320;

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTreeView tree)
            {
                if (e.OldValue is ZTreeNode oldNode) oldNode.IsSelected = false;
                if (e.NewValue is ZTreeNode newNode) newNode.IsSelected = true;
                tree.NodeSelected?.Invoke(tree, EventArgs.Empty);
            }
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
        }

        public ZTreeNode AddNode(string text)
        {
            var node = new ZTreeNode(text);
            _nodes.Add(node);
            InvalidateVisual();
            return node;
        }

        public void ClearNodes()
        {
            _nodes.Clear();
            SelectedNode = null;
            InvalidateVisual();
        }

        public void ExpandAll()
        {
            SetExpandRecursively(_nodes, true);
            InvalidateVisual();
        }

        public void CollapseAll()
        {
            SetExpandRecursively(_nodes, false);
            InvalidateVisual();
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

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.BorderPen, bounds);

            var visibleNodes = new List<(ZTreeNode node, int level)>();
            FlattenVisibleNodes(_nodes, 0, visibleNodes);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double itemH = ItemHeight;
            double indent = Indent;

            for (int i = 0; i < visibleNodes.Count; i++)
            {
                var (node, level) = visibleNodes[i];
                double nodeY = 4 + (i * itemH);
                if (nodeY + itemH < 0 || nodeY > ActualHeight) continue;

                double nodeX = 8 + (level * indent);
                bool isSelected = (node == SelectedNode || node.IsSelected);

                // Row selection background
                if (isSelected)
                {
                    var selRect = new Rect(nodeX - 4, nodeY + 2, Math.Max(0, ActualWidth - nodeX), itemH - 4);
                    dc.DrawRoundedRectangle(ZeroWpfTheme.SelectionBackground, null, selRect, 3, 3);
                }

                // Chevron
                double chevronSize = 10;
                double chevronX = nodeX;
                double chevronY = nodeY + (itemH - chevronSize) / 2;

                if (node.Nodes.Count > 0)
                {
                    var geom = new StreamGeometry();
                    using (var ctx = geom.Open())
                    {
                        if (node.IsExpanded)
                        {
                            // Downward triangle
                            ctx.BeginFigure(new Point(chevronX, chevronY + 2), true, true);
                            ctx.LineTo(new Point(chevronX + 8, chevronY + 2), true, false);
                            ctx.LineTo(new Point(chevronX + 4, chevronY + 6), true, false);
                        }
                        else
                        {
                            // Rightward triangle
                            ctx.BeginFigure(new Point(chevronX + 2, chevronY), true, true);
                            ctx.LineTo(new Point(chevronX + 6, chevronY + 4), true, false);
                            ctx.LineTo(new Point(chevronX + 2, chevronY + 8), true, false);
                        }
                    }
                    geom.Freeze();
                    dc.DrawGeometry(ZeroWpfTheme.TextSecondary, null, geom);
                }

                double contentX = nodeX + 14;

                // Checkbox
                if (ShowCheckboxes)
                {
                    double cbSize = 14;
                    double cbY = nodeY + (itemH - cbSize) / 2;
                    var cbRect = new Rect(contentX, cbY, cbSize, cbSize);

                    dc.DrawRectangle(null, new Pen(ZeroWpfTheme.BorderDefault, 1.2), cbRect);

                    if (node.Checked)
                    {
                        var tickRect = new Rect(contentX + 2, cbY + 2, cbSize - 4, cbSize - 4);
                        dc.DrawRectangle(ZeroWpfTheme.PrimaryAccent, null, tickRect);
                    }

                    contentX += 18;
                }

                // Text
#pragma warning disable CS0618
                var ft = new FormattedText(
                    node.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12.5,
                    isSelected ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextPrimary,
                    dpi);
#pragma warning restore CS0618

                dc.DrawText(ft, new Point(contentX, nodeY + (itemH - ft.Height) / 2));
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point pt = e.GetPosition(this);
            var visibleNodes = new List<(ZTreeNode node, int level)>();
            FlattenVisibleNodes(_nodes, 0, visibleNodes);

            int index = (int)((pt.Y - 4) / ItemHeight);
            if (index < 0 || index >= visibleNodes.Count) return;

            var (node, level) = visibleNodes[index];
            double nodeX = 8 + (level * Indent);

            // Chevron hit-test
            if (node.Nodes.Count > 0 && pt.X >= nodeX - 2 && pt.X <= nodeX + 14)
            {
                node.Toggle();
                if (node.IsExpanded)
                    NodeExpanded?.Invoke(this, EventArgs.Empty);
                else
                    NodeCollapsed?.Invoke(this, EventArgs.Empty);

                InvalidateVisual();
                return;
            }

            // Checkbox hit-test
            double contentX = nodeX + 14;
            if (ShowCheckboxes && pt.X >= contentX && pt.X <= contentX + 16)
            {
                node.Checked = !node.Checked;
                NodeCheckChanged?.Invoke(this, node);
                InvalidateVisual();
                return;
            }

            // Select node
            SelectedNode = node;
            InvalidateVisual();
        }
    }

    [Obsolete("ZeroTreeView is deprecated and will be removed in 5 release cycles. Please migrate to ZTreeView instead.")]
    public class ZeroTreeView : ZTreeView
    {
    }
}
