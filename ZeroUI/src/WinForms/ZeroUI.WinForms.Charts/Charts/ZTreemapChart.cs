using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.Core.Layout;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// High-performance Squarified Treemap chart for nested proportional and hierarchical data visualization.
    /// Thin WinForms View layer rendering tiles computed by ZeroUI.Core.Analytics.TreemapEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "TreemapChart.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Squarified Treemap chart for proportional area analytics")]
    public class ZTreemapChart : Control
    {
        private readonly List<TreemapItem> _items = new List<TreemapItem>();
        private readonly ToolTip _toolTip = new ToolTip();
        private string _title = "Asset Allocation & Proportional Breakdown";
        private string _valuePrefix = "$";
        private string _valueSuffix = "";
        private int _hoverIndex = -1;
        private IReadOnlyList<TreeMapRect<TreemapItem>> _computedLayout = Array.Empty<TreeMapRect<TreemapItem>>();

        public List<TreemapItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("Asset Allocation & Proportional Breakdown")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("$")]
        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? ""; Invalidate(); }
        }

        public ZTreemapChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(540, 360);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.Transparent;

            _toolTip.InitialDelay = 150;
            _toolTip.ReshowDelay = 50;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public TreemapItem AddItem(string label, double value, Color color, string category = "")
        {
            var item = new TreemapItem(label, value, category, (uint)color.ToArgb());
            _items.Add(item);
            Invalidate();
            return item;
        }

        public TreemapItem AddItem(string label, double value, string category = "")
        {
            var item = new TreemapItem(label, value, category);
            _items.Add(item);
            Invalidate();
            return item;
        }

        public void Clear()
        {
            _items.Clear();
            _computedLayout = Array.Empty<TreeMapRect<TreemapItem>>();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            Color bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);

            g.Clear(bgColor);

            int titleHeight = string.IsNullOrEmpty(_title) ? 6 : 30;
            if (!string.IsNullOrEmpty(_title))
            {
                using var titleFont = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
                using var titleBrush = new SolidBrush(textColor);
                g.DrawString(_title, titleFont, titleBrush, 10, 8);
            }

            var plotRect = new Rectangle(10, titleHeight, Width - 20, Height - titleHeight - 10);
            if (plotRect.Width <= 10 || plotRect.Height <= 10 || _items.Count == 0)
            {
                using var muted = new SolidBrush(Color.Gray);
                g.DrawString("No treemap data available.", Font, muted, 12, titleHeight + 10);
                return;
            }

            // Compute squarified layout via Core TreemapEngine
            _computedLayout = TreemapEngine.ComputeLayout(_items, 0, 0, plotRect.Width, plotRect.Height, padding: 3.0);
            double totalWeight = _items.Sum(x => Math.Max(0.0, x.Value));

            using var labelFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var whiteBrush = new SolidBrush(Color.White);

            for (int i = 0; i < _computedLayout.Count; i++)
            {
                var r = _computedLayout[i];
                float rx = (float)(plotRect.X + r.X);
                float ry = (float)(plotRect.Y + r.Y);
                float rw = (float)r.Width;
                float rh = (float)r.Height;

                if (rw < 2 || rh < 2) continue;

                var tileRect = new RectangleF(rx, ry, rw, rh);
                bool isHover = i == _hoverIndex;

                // Tile Background with gradient
                Color baseColor = r.Item.ColorRgba.ToColor(Color.FromArgb(79, 70, 229));
                Color topColor = isHover ? ControlPaint.Light(baseColor, 0.25f) : baseColor;
                Color botColor = ControlPaint.Dark(baseColor, 0.15f);

                using (var fillBrush = new LinearGradientBrush(tileRect, topColor, botColor, 65f))
                {
                    g.FillRectangle(fillBrush, tileRect);
                }

                // Border
                using (var borderPen = new Pen(isHover ? Color.White : Color.FromArgb(40, 255, 255, 255), isHover ? 2f : 1f))
                {
                    g.DrawRectangle(borderPen, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height);
                }

                // Text Labels if tile is large enough
                if (rw >= 40 && rh >= 30)
                {
                    var textClip = g.Clip;
                    g.SetClip(tileRect);

                    double pct = totalWeight > 0 ? (r.Item.Value / totalWeight) * 100.0 : 0.0;
                    string valText = $"{_valuePrefix}{r.Item.Value:N0}{_valueSuffix} ({pct:F1}%)";

                    g.DrawString(r.Item.Label, labelFont, whiteBrush, rx + 6, ry + 5);

                    if (rh >= 45)
                    {
                        using var mutedWhite = new SolidBrush(Color.FromArgb(210, 255, 255, 255));
                        g.DrawString(valText, subFont, mutedWhite, rx + 6, ry + 22);
                    }

                    g.Clip = textClip;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;

            int titleHeight = string.IsNullOrEmpty(_title) ? 6 : 30;
            float px = e.X - 10;
            float py = e.Y - titleHeight;

            for (int i = 0; i < _computedLayout.Count; i++)
            {
                var r = _computedLayout[i];
                if (px >= r.X && px <= r.X + r.Width && py >= r.Y && py <= r.Y + r.Height)
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                Invalidate();

                if (_hoverIndex >= 0 && _hoverIndex < _computedLayout.Count)
                {
                    var item = _computedLayout[_hoverIndex].Item;
                    double totalWeight = _items.Sum(x => Math.Max(0.0, x.Value));
                    double pct = totalWeight > 0 ? (item.Value / totalWeight) * 100.0 : 0.0;

                    string tip = $"{item.Label}\n" +
                                 (string.IsNullOrEmpty(item.Category) ? "" : $"Category: {item.Category}\n") +
                                 $"Value: {_valuePrefix}{item.Value:N2}{_valueSuffix}\n" +
                                 $"Share: {pct:F2}%";

                    _toolTip.SetToolTip(this, tip);
                    Cursor = Cursors.Hand;
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                    Cursor = Cursors.Default;
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _toolTip.SetToolTip(this, null);
                Invalidate();
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTreemapChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TreemapChart is deprecated and will be removed in 5 release cycles. Please migrate to ZTreemapChart instead.")]
    [ToolboxItem(false)]
    public class TreemapChart : ZTreemapChart
    {
    }

    #endregion
}
