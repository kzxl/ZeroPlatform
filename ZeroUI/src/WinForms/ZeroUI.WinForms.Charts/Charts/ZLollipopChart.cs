using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Clean, high-density Lollipop comparison chart (horizontal or vertical stems with dot heads).
    /// Thin WinForms rendering layer consuming ZeroUI.Core.Analytics.LollipopEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ChartControl.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("High-density lollipop comparison chart with thin stems and circular dot heads")]
    public class ZLollipopChart : Control
    {
        private readonly List<LollipopItem> _items = new List<LollipopItem>();
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Category Benchmark Comparison";
        private LollipopOrientation _orientation = LollipopOrientation.Horizontal;
        private Color _stemColor = Color.FromArgb(148, 163, 184);
        private Color _dotColor = Color.FromArgb(59, 130, 246);
        private float _dotRadius = 6.5f;
        private bool _showValueLabels = true;

        private LollipopLayoutResult? _lastLayout;
        private LollipopRenderItem? _hoverItem;

        public List<LollipopItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("Category Benchmark Comparison")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(LollipopOrientation.Horizontal)]
        public LollipopOrientation Orientation
        {
            get => _orientation;
            set { _orientation = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color StemColor
        {
            get => _stemColor;
            set { _stemColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color DotColor
        {
            get => _dotColor;
            set { _dotColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(6.5f)]
        public float DotRadius
        {
            get => _dotRadius;
            set { _dotRadius = Math.Max(2f, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowValueLabels
        {
            get => _showValueLabels;
            set { _showValueLabels = value; Invalidate(); }
        }

        public ZLollipopChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(520, 340);
            Font = new Font("Segoe UI", 9f);
        }

        public void LoadSampleData()
        {
            _items.Clear();
            _items.Add(new LollipopItem("Product Alpha", 84, (uint)Color.FromArgb(59, 130, 246).ToArgb()));
            _items.Add(new LollipopItem("Product Beta", 62, (uint)Color.FromArgb(16, 185, 129).ToArgb()));
            _items.Add(new LollipopItem("Product Gamma", 95, (uint)Color.FromArgb(249, 115, 22).ToArgb()));
            _items.Add(new LollipopItem("Product Delta", 43, (uint)Color.FromArgb(168, 85, 247).ToArgb()));
            _items.Add(new LollipopItem("Product Epsilon", 71, (uint)Color.FromArgb(236, 72, 153).ToArgb()));
            _items.Add(new LollipopItem("Product Zeta", 88, (uint)Color.FromArgb(14, 165, 233).ToArgb()));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            LollipopRenderItem? newHover = null;

            if (_lastLayout != null)
            {
                foreach (var item in _lastLayout.RenderItems)
                {
                    if (item.Contains(e.X, e.Y, 6.0))
                    {
                        newHover = item;
                        break;
                    }
                }
            }

            if (newHover != _hoverItem)
            {
                _hoverItem = newHover;
                if (_hoverItem != null)
                {
                    _toolTip.SetToolTip(this, $"{_hoverItem.SourceItem.Category}\nValue: {_hoverItem.SourceItem.Value:F1}");
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverItem != null)
            {
                _hoverItem = null;
                _toolTip.SetToolTip(this, null);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            var bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(255, 255, 255);
            var textPrimary = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(30, 41, 59);
            var textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            var baselineColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);

            g.Clear(bgColor);

            // Title
            float topMargin = 38f;
            if (!string.IsNullOrEmpty(_title))
            {
                using (var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textPrimary))
                {
                    g.DrawString(_title, titleFont, brush, 16f, 10f);
                }
            }

            var activeItems = _items.Count > 0 ? _items : GetPreviewItems();
            if (activeItems.Count == 0) return;

            float leftMargin = (_orientation == LollipopOrientation.Horizontal) ? 120f : 45f;
            float rightMargin = _showValueLabels ? 50f : 20f;
            float bottomMargin = (_orientation == LollipopOrientation.Vertical) ? 50f : 30f;
            float plotX = leftMargin;
            float plotY = topMargin;
            float plotWidth = Math.Max(10f, Width - leftMargin - rightMargin);
            float plotHeight = Math.Max(10f, Height - topMargin - bottomMargin);

            _lastLayout = LollipopEngine.ComputeLayout(
                activeItems,
                plotX,
                plotY,
                plotWidth,
                plotHeight,
                _orientation,
                _dotRadius
            );

            if (_lastLayout.RenderItems.Count == 0) return;

            // Draw baseline
            using (var basePen = new Pen(baselineColor, 1.5f))
            {
                if (_orientation == LollipopOrientation.Horizontal)
                {
                    float bx = (float)_lastLayout.RenderItems[0].StemStartX;
                    g.DrawLine(basePen, bx, plotY, bx, plotY + plotHeight);
                }
                else
                {
                    float by = (float)_lastLayout.RenderItems[0].StemStartY;
                    g.DrawLine(basePen, plotX, by, plotX + plotWidth, by);
                }
            }

            // Draw each Lollipop
            using (var smallFont = new Font(Font.FontFamily, 8f))
            using (var textBrush = new SolidBrush(textPrimary))
            using (var valBrush = new SolidBrush(textSecondary))
            {
                foreach (var ri in _lastLayout.RenderItems)
                {
                    bool isHover = (_hoverItem == ri);
                    Color curDotColor = ri.SourceItem.ColorRgba.ToColor(_dotColor);
                    Color curStemColor = _stemColor;

                    if (isHover)
                    {
                        curDotColor = Color.FromArgb(Math.Min(255, curDotColor.R + 40), Math.Min(255, curDotColor.G + 40), Math.Min(255, curDotColor.B + 40));
                    }

                    // Draw Stem
                    using (var stemPen = new Pen(curStemColor, isHover ? 2.5f : 1.5f))
                    {
                        g.DrawLine(stemPen, (float)ri.StemStartX, (float)ri.StemStartY, (float)ri.StemEndX, (float)ri.StemEndY);
                    }

                    // Draw Dot Head
                    float r = (float)ri.DotRadius + (isHover ? 2f : 0f);
                    float cx = (float)ri.DotCenterX;
                    float cy = (float)ri.DotCenterY;

                    using (var brush = new SolidBrush(curDotColor))
                    using (var pen = new Pen(isHover ? Color.White : Color.FromArgb(180, isDark ? Color.Black : Color.White), 1.5f))
                    {
                        g.FillEllipse(brush, cx - r, cy - r, r * 2f, r * 2f);
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
                    }

                    // Labels
                    if (_orientation == LollipopOrientation.Horizontal)
                    {
                        // Category Label (Left aligned in left margin)
                        using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString(ri.SourceItem.Category, smallFont, textBrush, (float)ri.StemStartX - 8f, cy, sf);
                        }

                        // Value Label (Right of Dot Head)
                        if (_showValueLabels)
                        {
                            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString(ri.SourceItem.Value.ToString("F0"), smallFont, valBrush, cx + r + 6f, cy, sf);
                            }
                        }
                    }
                    else
                    {
                        // Category Label (Below Stem)
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                        {
                            g.DrawString(ri.SourceItem.Category, smallFont, textBrush, cx, (float)ri.StemStartY + 6f, sf);
                        }

                        // Value Label (Above Dot Head)
                        if (_showValueLabels)
                        {
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far })
                            {
                                g.DrawString(ri.SourceItem.Value.ToString("F0"), smallFont, valBrush, cx, cy - r - 3f, sf);
                            }
                        }
                    }
                }
            }
        }

        private static List<LollipopItem> GetPreviewItems()
        {
            return new List<LollipopItem>
            {
                new LollipopItem("Alpha", 50),
                new LollipopItem("Beta", 75),
                new LollipopItem("Gamma", 40),
                new LollipopItem("Delta", 90)
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLollipopChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LollipopChart is deprecated and will be removed in 5 release cycles. Please migrate to ZLollipopChart instead.")]
    [ToolboxItem(false)]
    public class LollipopChart : ZLollipopChart
    {
    }

    #endregion
}
