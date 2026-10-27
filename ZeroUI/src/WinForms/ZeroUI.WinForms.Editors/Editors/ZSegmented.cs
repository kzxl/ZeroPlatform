using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern Segmented Control (Pill switcher) for ZeroUI providing clean, compact view and filter switching.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("SelectedIndex")]
    [Description("Segmented pill switcher for view and filter options")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSegmented.bmp")]
    public class ZSegmented : ControlBase
    {
        private string[] _items = new[] { "All", "Daily", "Weekly", "Monthly" };
        private readonly SelectionModel<string> _selection = new SelectionModel<string> { WrapAround = false };
        private int _hoveredIndex = -1;

        public event EventHandler? SelectedIndexChanged;

        public ZSegmented()
        {
            Size = new Size(320, 34);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Cursor = Cursors.Hand;

            _selection.SetSource(() => _items.Length, idx => _items[idx]);
            _selection.SelectIndex(0);
            _selection.SelectionChanged += (s, e) =>
            {
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };

            ZeroUIConfig.CornerStyleChanged += (s, e) =>
            {
                UpdateRegion();
                Invalidate();
            };
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9f, FontStyle.Regular);
                UpdateRegion();
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        [Browsable(false)]
        public SelectionModel<string> Selection => _selection;

        [Category("Data")]
        public string[] Items
        {
            get => _items;
            set
            {
                _items = value ?? Array.Empty<string>();
                if (_selection.SelectedIndex >= _items.Length)
                {
                    _selection.SelectIndex(Math.Max(0, _items.Length - 1));
                }
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int SelectedIndex
        {
            get => _selection.SelectedIndex;
            set => _selection.SelectIndex(value);
        }

        [Browsable(false)]
        public string? SelectedItem => _selection.SelectedItem;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = GetIndexAt(e.X);
            if (_hoveredIndex != idx)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int idx = GetIndexAt(e.X);
            if (idx >= 0 && idx < _items.Length)
            {
                SelectedIndex = idx;
            }
        }

        private int GetIndexAt(int x)
        {
            if (_items.Length == 0) return -1;
            int itemW = (Width - 4) / _items.Length;
            if (itemW <= 0) return -1;
            int idx = (x - 2) / itemW;
            return Math.Max(0, Math.Min(_items.Length - 1, idx));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
            Invalidate();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius);
                Region = new Region(path);
            }
            else
            {
                Region = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;

            Rectangle trackRect = new Rectangle(0, 0, Width, Height);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 1. Draw Track Background
            if (effRadius > 0)
            {
                using var trackPath = CreateRoundedRectangle(trackRect, effRadius);
                using var trackBrush = new SolidBrush(palette.HeaderBackground);
                g.FillPath(trackBrush, trackPath);
                using var trackBorderPen = new Pen(palette.Border, 1f) { Alignment = PenAlignment.Inset };
                g.DrawPath(trackBorderPen, trackPath);
            }
            else
            {
                using var trackBrush = new SolidBrush(palette.HeaderBackground);
                g.FillRectangle(trackBrush, trackRect);
                using var trackBorderPen = new Pen(palette.Border, 1f) { Alignment = PenAlignment.Inset };
                g.DrawRectangle(trackBorderPen, 0, 0, Width - 1, Height - 1);
            }

            if (_items.Length == 0) return;

            float itemW = (float)(Width - 4) / _items.Length;
            float itemH = Height - 4;

            // 3. Draw Active Pill
            if (SelectedIndex >= 0 && SelectedIndex < _items.Length)
            {
                RectangleF pillRect = new RectangleF(2 + (SelectedIndex * itemW), 2, itemW, itemH);
                int effPillRadius = ZeroUIConfig.GetEffectiveRadius(5);
                using (var pillPath = CreateRoundedRectangleF(pillRect, effPillRadius))
                {
                    using var pillBrush = new SolidBrush(palette.Surface);
                    g.FillPath(pillBrush, pillPath);

                    using var shadowPen = new Pen(palette.Border, 1f);
                    g.DrawPath(shadowPen, pillPath);
                }
            }

            // 3. Draw Item Texts
            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle itemRect = new Rectangle((int)(2 + (i * itemW)), 2, (int)itemW, (int)itemH);
                bool isSelected = (i == SelectedIndex);
                Color textColor = isSelected ? palette.TextPrimary : palette.TextSecondary;
                Font itemFont = isSelected ? new Font(Font, FontStyle.Bold) : Font;

                TextRenderer.DrawText(
                    g,
                    _items[i],
                    itemFont,
                    itemRect,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        private static GraphicsPath CreateRoundedRectangleF(RectangleF rect, float radius) =>
            ZeroUIConfig.CreateRoundedRectangleF(rect, radius);
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZSegmented"/>.
    /// </summary>
    [Obsolete("ZeroSegmented is deprecated and will be removed in 5 release cycles. Please migrate to ZSegmented instead.")]
    [ToolboxItem(false)]
    public class ZeroSegmented : ZSegmented
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSegmented"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SegmentedControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSegmented instead.")]
    [ToolboxItem(false)]
    public class SegmentedControl : ZSegmented
    {
    }

    #endregion
}
