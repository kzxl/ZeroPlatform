using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    /// <summary>
    /// Represents a single field slot linking a Label and an Editor Control within LayoutControl.
    /// </summary>
    public class LayoutControlItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string LabelText { get; set; } = string.Empty;
        public Control Control { get; set; }
        public bool IsRequired { get; set; } = false;
        public int? CustomLabelWidth { get; set; }
        public int ColumnSpan { get; set; } = 1;
        public bool Visible { get; set; } = true;
        public string? ToolTipText { get; set; }

        internal Label LabelControl { get; }

        public LayoutControlItem(string labelText, Control control, bool isRequired = false, int columnSpan = 1)
        {
            LabelText = labelText;
            Control = control ?? throw new ArgumentNullException(nameof(control));
            IsRequired = isRequired;
            ColumnSpan = Math.Max(1, columnSpan);

            LabelControl = new Label
            {
                Text = labelText + (isRequired ? " *" : ""),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9f),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                BackColor = Color.Transparent
            };
        }
    }

    /// <summary>
    /// Represents a grouped section of fields with collapsible header and configurable column count.
    /// </summary>
    public class LayoutControlGroup
    {
        private readonly List<LayoutControlItem> _items = new List<LayoutControlItem>();
        private bool _isExpanded = true;

        public string Title { get; set; }
        public int ColumnsCount { get; set; } = 2;
        public bool IsCollapsible { get; set; } = true;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    ExpandedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public IReadOnlyList<LayoutControlItem> Items => _items;

        internal event EventHandler? ItemsChanged;
        internal event EventHandler? ExpandedChanged;

        public LayoutControlGroup(string title, int columnsCount = 2, bool isCollapsible = true)
        {
            Title = title;
            ColumnsCount = Math.Max(1, columnsCount);
            IsCollapsible = isCollapsible;
        }

        public LayoutControlItem AddItem(string labelText, Control control, bool isRequired = false, int columnSpan = 1)
        {
            var item = new LayoutControlItem(labelText, control, isRequired, columnSpan);
            _items.Add(item);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }

        public void RemoveItem(LayoutControlItem item)
        {
            if (_items.Remove(item))
            {
                ItemsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            _items.Clear();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Enterprise Form Layout Builder control for complex ERP screens (30-80 fields).
    /// Eliminates manual pixel coordinate calculations by auto-aligning labels and controls,
    /// arranging fields into responsive columns, and providing collapsible groups.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Automated form layout builder with smart label-control binding, responsive columns, and collapsible groups.")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTablePanel.bmp")]
    public class ZLayoutControl : Panel
    {
        private readonly List<LayoutControlGroup> _groups = new List<LayoutControlGroup>();

        private int _defaultLabelWidth = 130;
        private int _itemHeight = 32;
        private int _rowSpacing = 8;
        private int _columnSpacing = 16;
        private int _groupPadding = 12;
        private bool _isLayoutSuspended = false;

        public ZLayoutControl()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
            Padding = new Padding(12);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Properties

        [Category("ZeroUI - Layout")]
        [Description("Default label width in pixels across all fields.")]
        [DefaultValue(130)]
        public int DefaultLabelWidth
        {
            get => _defaultLabelWidth;
            set { _defaultLabelWidth = Math.Max(60, value); PerformSmartLayout(); }
        }

        [Category("ZeroUI - Layout")]
        [Description("Standard vertical height allocated per editor row.")]
        [DefaultValue(32)]
        public int ItemHeight
        {
            get => _itemHeight;
            set { _itemHeight = Math.Max(24, value); PerformSmartLayout(); }
        }

        [Category("ZeroUI - Layout")]
        [Description("Vertical spacing between adjacent field rows.")]
        [DefaultValue(8)]
        public int RowSpacing
        {
            get => _rowSpacing;
            set { _rowSpacing = Math.Max(2, value); PerformSmartLayout(); }
        }

        [Category("ZeroUI - Layout")]
        [Description("Horizontal spacing between adjacent field columns.")]
        [DefaultValue(16)]
        public int ColumnSpacing
        {
            get => _columnSpacing;
            set { _columnSpacing = Math.Max(4, value); PerformSmartLayout(); }
        }

        [Browsable(false)]
        public IReadOnlyList<LayoutControlGroup> Groups => _groups;

        #endregion

        #region Group & Item Management

        public LayoutControlGroup AddGroup(string title, int columnsCount = 2, bool isCollapsible = true)
        {
            var group = new LayoutControlGroup(title, columnsCount, isCollapsible);
            group.ItemsChanged += (s, e) => SyncControlsAndLayout();
            group.ExpandedChanged += (s, e) => PerformSmartLayout();
            _groups.Add(group);
            PerformSmartLayout();
            return group;
        }

        public void RemoveGroup(LayoutControlGroup group)
        {
            if (_groups.Remove(group))
            {
                SyncControlsAndLayout();
            }
        }

        public void ClearGroups()
        {
            _groups.Clear();
            Controls.Clear();
            Invalidate();
        }

        public void BeginLayoutUpdate()
        {
            _isLayoutSuspended = true;
            SuspendLayout();
        }

        public void EndLayoutUpdate()
        {
            _isLayoutSuspended = false;
            ResumeLayout(true);
            SyncControlsAndLayout();
        }

        private void SyncControlsAndLayout()
        {
            if (_isLayoutSuspended) return;

            // Ensure all items' controls are added to this panel
            foreach (var grp in _groups)
            {
                foreach (var itm in grp.Items)
                {
                    if (!Controls.Contains(itm.LabelControl))
                    {
                        Controls.Add(itm.LabelControl);
                    }
                    if (!Controls.Contains(itm.Control))
                    {
                        Controls.Add(itm.Control);
                    }
                }
            }

            PerformSmartLayout();
        }

        #endregion

        #region Layout Calculation Engine

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PerformSmartLayout();
        }

        public void PerformSmartLayout()
        {
            if (_isLayoutSuspended || Width <= 0) return;

            int clientW = ClientRectangle.Width - Padding.Left - Padding.Right;
            if (clientW < 200) return;

            int currentY = Padding.Top;

            foreach (var grp in _groups)
            {
                // Calculate Group Box dimensions
                int groupHeaderH = 34;
                int grpX = Padding.Left;
                int grpW = clientW;

                if (!grp.IsExpanded)
                {
                    // Collapsed: hide all child controls
                    foreach (var itm in grp.Items)
                    {
                        itm.LabelControl.Visible = false;
                        itm.Control.Visible = false;
                    }
                    currentY += groupHeaderH + 12;
                    continue;
                }

                // Expanded: layout items into columns
                int cols = Math.Max(1, grp.ColumnsCount);
                int innerW = grpW - (_groupPadding * 2);
                int colW = (innerW - ((cols - 1) * _columnSpacing)) / cols;

                int itemY = currentY + groupHeaderH + _groupPadding;
                int currentCol = 0;
                int rowMaxY = itemY;

                foreach (var itm in grp.Items)
                {
                    if (!itm.Visible)
                    {
                        itm.LabelControl.Visible = false;
                        itm.Control.Visible = false;
                        continue;
                    }

                    int span = Math.Min(itm.ColumnSpan, cols);
                    if (currentCol + span > cols)
                    {
                        // Wrap to next row
                        currentCol = 0;
                        itemY = rowMaxY + _rowSpacing;
                    }

                    int itemX = grpX + _groupPadding + (currentCol * (colW + _columnSpacing));
                    int totalSlotW = (span * colW) + ((span - 1) * _columnSpacing);

                    int labelW = itm.CustomLabelWidth ?? _defaultLabelWidth;
                    labelW = Math.Min(labelW, totalSlotW - 60);
                    int controlW = Math.Max(40, totalSlotW - labelW - 8);

                    // Position Label
                    itm.LabelControl.Bounds = new Rectangle(itemX, itemY, labelW, _itemHeight);
                    itm.LabelControl.Visible = true;

                    // Position Editor Control
                    itm.Control.Bounds = new Rectangle(itemX + labelW + 8, itemY, controlW, itm.Control.Height > 0 ? itm.Control.Height : _itemHeight);
                    itm.Control.Visible = true;

                    int itemBottom = Math.Max(itm.LabelControl.Bottom, itm.Control.Bottom);
                    if (itemBottom > rowMaxY) rowMaxY = itemBottom;

                    currentCol += span;
                    if (currentCol >= cols)
                    {
                        currentCol = 0;
                        itemY = rowMaxY + _rowSpacing;
                        rowMaxY = itemY;
                    }
                }

                int groupBottom = (grp.Items.Count > 0 ? rowMaxY : itemY) + _groupPadding;
                currentY = groupBottom + 16;
            }

            AutoScrollMinSize = new Size(0, currentY);
            Invalidate();
        }

        private void UpdateThemeColors()
        {
            foreach (var grp in _groups)
            {
                foreach (var itm in grp.Items)
                {
                    itm.LabelControl.ForeColor = itm.IsRequired ? ZeroTheme.Colors.TextPrimary : ZeroTheme.Colors.TextSecondary;
                }
            }
        }

        #endregion

        #region Paint Group Cards

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int clientW = ClientRectangle.Width - Padding.Left - Padding.Right;
            if (clientW < 200) return;

            int currentY = Padding.Top - AutoScrollPosition.Y;

            foreach (var grp in _groups)
            {
                int groupHeaderH = 34;
                int grpX = Padding.Left;
                int grpW = clientW;

                int grpH;
                if (!grp.IsExpanded)
                {
                    grpH = groupHeaderH;
                }
                else
                {
                    int maxItemBottom = currentY + groupHeaderH;
                    foreach (var itm in grp.Items)
                    {
                        if (itm.Visible && itm.Control.Bottom - AutoScrollPosition.Y > maxItemBottom)
                        {
                            maxItemBottom = itm.Control.Bottom - AutoScrollPosition.Y;
                        }
                    }
                    grpH = (maxItemBottom - currentY) + _groupPadding;
                }

                var cardBounds = new Rectangle(grpX, currentY, grpW, grpH);
                int radius = ZeroUIConfig.GetEffectiveRadius(6);

                // Card Background & Border
                using (var bgBrush = new SolidBrush(ZeroTheme.Colors.BgCard))
                using (var path = ZeroUIConfig.CreateRoundedRectangle(cardBounds, radius))
                {
                    g.FillPath(bgBrush, path);
                }

                using (var borderPen = new Pen(ZeroTheme.Colors.BorderDefault, 1f))
                using (var path = ZeroUIConfig.CreateRoundedRectangle(cardBounds, radius))
                {
                    g.DrawPath(borderPen, path);
                }

                // Group Header Bar
                var headerBounds = new Rectangle(grpX, currentY, grpW, groupHeaderH);
                using (var headerBrush = new SolidBrush(Color.FromArgb(16, ZeroTheme.Colors.PrimaryAccent)))
                using (var path = ZeroUIConfig.CreateRoundedRectangle(headerBounds, radius))
                {
                    g.FillPath(headerBrush, path);
                }

                // Header Title
                string toggleGlyph = grp.IsCollapsible ? (grp.IsExpanded ? "▼ " : "► ") : "■ ";
                string fullTitle = toggleGlyph + grp.Title;

                using (var titleBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
                using (var titleFont = new Font("Segoe UI", 9.25f, FontStyle.Bold))
                {
                    g.DrawString(fullTitle, titleFont, titleBrush, grpX + 12, currentY + 8);
                }

                currentY += grpH + 16;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Check if user clicked on a group header to expand/collapse
            int clientW = ClientRectangle.Width - Padding.Left - Padding.Right;
            int currentY = Padding.Top - AutoScrollPosition.Y;

            foreach (var grp in _groups)
            {
                int groupHeaderH = 34;
                var headerRect = new Rectangle(Padding.Left, currentY, clientW, groupHeaderH);

                if (headerRect.Contains(e.Location) && grp.IsCollapsible)
                {
                    grp.IsExpanded = !grp.IsExpanded;
                    return;
                }

                if (!grp.IsExpanded)
                {
                    currentY += groupHeaderH + 16;
                }
                else
                {
                    int maxItemBottom = currentY + groupHeaderH;
                    foreach (var itm in grp.Items)
                    {
                        if (itm.Visible && itm.Control.Bottom - AutoScrollPosition.Y > maxItemBottom)
                        {
                            maxItemBottom = itm.Control.Bottom - AutoScrollPosition.Y;
                        }
                    }
                    int grpH = (maxItemBottom - currentY) + _groupPadding;
                    currentY += grpH + 16;
                }
            }
        }

        #endregion
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                UpdateThemeColors();
                Invalidate();
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLayoutControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LayoutControl is deprecated and will be removed in 5 release cycles. Please migrate to ZLayoutControl instead.")]
    [ToolboxItem(false)]
    public class LayoutControl : ZLayoutControl
    {
    }

    #endregion
}
