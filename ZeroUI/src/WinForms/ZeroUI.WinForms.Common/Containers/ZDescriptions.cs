using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    public class DescriptionItem
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public Color? ValueColor { get; set; }
        public bool IsHighlighted { get; set; }

        public DescriptionItem() { }

        public DescriptionItem(string label, string value, Color? valueColor = null, bool isHighlighted = false)
        {
            Label = label;
            Value = value;
            ValueColor = valueColor;
            IsHighlighted = isHighlighted;
        }
    }

    /// <summary>
    /// Modern Key-Value metadata description grid component for ZeroUI.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Key-Value metadata description grid component")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDescriptions.bmp")]
    public class ZDescriptions : Control
    {

        private readonly List<DescriptionItem> _items = new List<DescriptionItem>();
        private int _columns = 2;
        private int _rowHeight = 28;
        private Color _labelColor = Color.FromArgb(107, 114, 128); // Muted gray
        private Color _valueColor = Color.FromArgb(17, 24, 39);     // Dark gray

        public ZDescriptions()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
            Size = new Size(400, 90);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Browsable(false)]
        public List<DescriptionItem> Items => _items;

        [Category("Layout")]
        [DefaultValue(2)]
        public int Columns
        {
            get => _columns;
            set { _columns = Math.Max(1, value); Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(28)]
        public int RowHeight
        {
            get => _rowHeight;
            set { _rowHeight = Math.Max(18, value); Invalidate(); }
        }

        [Category("Appearance")]
        public Color LabelColor
        {
            get => _labelColor;
            set { _labelColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color ValueColor
        {
            get => _valueColor;
            set { _valueColor = value; Invalidate(); }
        }

        public void Add(string label, string value, Color? valueColor = null, bool isHighlighted = false)
        {
            _items.Add(new DescriptionItem(label, value, valueColor, isHighlighted));
            Invalidate();
        }

        public void SetValue(string label, string value, Color? valueColor = null)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Label, label, StringComparison.OrdinalIgnoreCase))
                {
                    _items[i].Value = value;
                    if (valueColor.HasValue) _items[i].ValueColor = valueColor.Value;
                    Invalidate();
                    return;
                }
            }
            Add(label, value, valueColor);
        }

        public void Clear()
        {
            _items.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int count = _items.Count;
            if (count == 0) return;

            int cols = Math.Max(1, _columns);
            int rowsPerCol = (count + cols - 1) / cols;
            int colWidth = Width / cols;

            using var labelFont = new Font(Font.FontFamily, Font.Size, FontStyle.Regular);
            using var valFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);

            for (int i = 0; i < count; i++)
            {
                var item = _items[i];
                int c = i / rowsPerCol;
                int r = i % rowsPerCol;

                int colLeft = c * colWidth;
                int rowTop = r * _rowHeight + 4;

                // Divide label (approx 50%) and value (approx 50%)
                int labelWidth = (int)(colWidth * 0.52f);
                int valWidth = colWidth - labelWidth - 8;

                Rectangle labelRect = new Rectangle(colLeft + 4, rowTop, labelWidth, _rowHeight);
                Rectangle valRect = new Rectangle(colLeft + labelWidth + 4, rowTop, valWidth, _rowHeight);

                // Draw Label
                Color effLabelColor = (_labelColor != Color.Empty && _labelColor != Color.FromArgb(107, 114, 128)) ? _labelColor : ZeroTheme.Colors.TextSecondary;
                TextRenderer.DrawText(
                    g,
                    item.Label + ":",
                    labelFont,
                    labelRect,
                    effLabelColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Draw Value
                Color effValueColor = item.ValueColor ?? ((_valueColor != Color.Empty && _valueColor != Color.FromArgb(17, 24, 39)) ? _valueColor : ZeroTheme.Colors.TextPrimary);
                TextRenderer.DrawText(
                    g,
                    item.Value,
                    valFont,
                    valRect,
                    effValueColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Draw Vertical Column Divider (if multiple columns)
                if (c > 0 && r == 0)
                {
                    using var divPen = new Pen(ZeroTheme.Colors.Border, 1f);
                    g.DrawLine(divPen, colLeft, 4, colLeft, Height - 8);
                }
            }
        }
    
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
    /// Legacy alias for <see cref="DescriptionItem"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDescriptionItem is deprecated. Please use DescriptionItem instead.")]
    public class ZeroDescriptionItem : DescriptionItem
    {
        public ZeroDescriptionItem() : base() { }
        public ZeroDescriptionItem(string label, string value, Color? valueColor = null, bool isHighlighted = false)
            : base(label, value, valueColor, isHighlighted) { }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDescriptions"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDescriptions is deprecated and will be removed in 5 release cycles. Please migrate to ZDescriptions instead.")]
    [ToolboxItem(false)]
    public class ZeroDescriptions : ZDescriptions
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDescriptions"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Descriptions is deprecated and will be removed in 5 release cycles. Please migrate to ZDescriptions instead.")]
    [ToolboxItem(false)]
    public class Descriptions : ZDescriptions
    {
    }

    #endregion
}
