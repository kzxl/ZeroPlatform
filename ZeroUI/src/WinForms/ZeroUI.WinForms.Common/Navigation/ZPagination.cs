using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    internal enum PagerButtonKind
    {
        First,
        Prev,
        PageNumber,
        Ellipsis,
        Next,
        Last,
        PageSizeSelector
    }

    internal class PagerButton
    {
        public PagerButtonKind Kind { get; set; }
        public int PageNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsActive { get; set; } = false;
        public string? ToolTip { get; set; }
    }

    /// <summary>
    /// High-performance, single-HWND data pagination control designed for virtual grids and enterprise datasets.
    /// Provides zero-allocation page boundary math, windowed page lists, seamless border rendering, and page-size selection.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "PaginationControl.bmp")]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("PageChanged")]
    [Description("Modern, high-performance single-HWND data pagination control with seamless vector geometry")]
    public class ZPagination : ControlBase
    {
        private readonly PaginationModel _model = new PaginationModel();
        private readonly List<PagerButton> _buttons = new List<PagerButton>();
        private readonly ToolTip _toolTip = new ToolTip();
        private int _hoveredButtonIndex = -1;
        private int _pressedButtonIndex = -1;
        private string? _currentToolTip;

        private int[] _pageSizes = new[] { 10, 20, 50, 100, 200 };
        private bool _showSummary = true;
        private bool _showPageSizeSelector = true;
        private int _buttonHeight = 28;
        private int _buttonRadius = 5;

        public event EventHandler<int>? PageChanged;
        public event EventHandler<int>? PageSizeChanged;

        public ZPagination()
        {
            Dock = DockStyle.Bottom;
            Height = 44;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Default;

            _model.PageChanged += (s, page) =>
            {
                RecalculateLayout();
                Invalidate();
                PageChanged?.Invoke(this, page);
            };

            _model.PageSizeChanged += (s, size) =>
            {
                RecalculateLayout();
                Invalidate();
                PageSizeChanged?.Invoke(this, size);
            };

            _model.StateChanged += (s, e) =>
            {
                RecalculateLayout();
                Invalidate();
            };

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                RecalculateLayout();
                Invalidate();
            };
        }

        #region Properties

        [Browsable(false)]
        public PaginationModel Model => _model;

        [Category("Data")]
        [DefaultValue(0)]
        public int TotalCount
        {
            get => _model.TotalCount;
            set => _model.TotalCount = value;
        }

        [Category("Data")]
        [DefaultValue(20)]
        public int PageSize
        {
            get => _model.PageSize;
            set => _model.PageSize = value;
        }

        [Category("Data")]
        [DefaultValue(1)]
        public int CurrentPage
        {
            get => _model.CurrentPage;
            set => _model.GoToPage(value);
        }

        [Browsable(false)]
        public int TotalPages => _model.TotalPages;

        [Category("Behavior")]
        public int[] PageSizes
        {
            get => _pageSizes;
            set
            {
                _pageSizes = value ?? new[] { 10, 20, 50, 100 };
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowSummary
        {
            get => _showSummary;
            set
            {
                if (_showSummary != value)
                {
                    _showSummary = value;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowPageSizeSelector
        {
            get => _showPageSizeSelector;
            set
            {
                if (_showPageSizeSelector != value)
                {
                    _showPageSizeSelector = value;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        #endregion

        #region Public Methods

        public void GoToPage(int page) => _model.GoToPage(page);
        public void NextPage() => _model.NextPage();
        public void PrevPage() => _model.PrevPage();
        public void FirstPage() => _model.FirstPage();
        public void LastPage() => _model.LastPage();

        #endregion

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
            Invalidate();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        private void RecalculateLayout()
        {
            _buttons.Clear();
            if (Width <= 0 || Height <= 0) return;

            int btnH = _buttonHeight;
            int btnY = (Height - btnH) / 2;
            int currentRight = Width - 14;

            // 1. Page Size Selector Button
            if (_showPageSizeSelector)
            {
                int sizeBtnW = 92;
                currentRight -= sizeBtnW;
                _buttons.Add(new PagerButton
                {
                    Kind = PagerButtonKind.PageSizeSelector,
                    Text = $"{_model.PageSize} / page ▾",
                    Bounds = new Rectangle(currentRight, btnY, sizeBtnW, btnH),
                    ToolTip = "Select items per page"
                });
                currentRight -= 8; // margin
            }

            // 2. Navigation Buttons (Cluster)
            var clusterButtons = new List<PagerButton>();

            // Last
            clusterButtons.Add(new PagerButton
            {
                Kind = PagerButtonKind.Last,
                Text = "⏭",
                IsEnabled = _model.CanLast,
                ToolTip = "Last page"
            });

            // Next
            clusterButtons.Add(new PagerButton
            {
                Kind = PagerButtonKind.Next,
                Text = "▶",
                IsEnabled = _model.CanNext,
                ToolTip = "Next page"
            });

            // Page numbers
            int[] visiblePages = _model.GetVisiblePages(7);
            for (int i = visiblePages.Length - 1; i >= 0; i--)
            {
                int p = visiblePages[i];
                if (p == -1)
                {
                    clusterButtons.Add(new PagerButton
                    {
                        Kind = PagerButtonKind.Ellipsis,
                        Text = "...",
                        IsEnabled = false
                    });
                }
                else
                {
                    clusterButtons.Add(new PagerButton
                    {
                        Kind = PagerButtonKind.PageNumber,
                        PageNumber = p,
                        Text = p.ToString(),
                        IsActive = (p == _model.CurrentPage),
                        ToolTip = $"Go to page {p}"
                    });
                }
            }

            // Prev
            clusterButtons.Add(new PagerButton
            {
                Kind = PagerButtonKind.Prev,
                Text = "◀",
                IsEnabled = _model.CanPrev,
                ToolTip = "Previous page"
            });

            // First
            clusterButtons.Add(new PagerButton
            {
                Kind = PagerButtonKind.First,
                Text = "⏮",
                IsEnabled = _model.CanFirst,
                ToolTip = "First page"
            });

            // Measure & place cluster from right to left
            int btnW = 32;
            for (int i = 0; i < clusterButtons.Count; i++)
            {
                var btn = clusterButtons[i];
                int w = btnW;
                currentRight -= w;
                btn.Bounds = new Rectangle(currentRight, btnY, w, btnH);
                _buttons.Add(btn);
            }
        }

        #region Input Handling

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Bounds.Contains(pt))
                {
                    if (_buttons[i].IsEnabled && _buttons[i].Kind != PagerButtonKind.Ellipsis)
                    {
                        return i;
                    }
                    return -1;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Location);
            if (_hoveredButtonIndex != idx)
            {
                _hoveredButtonIndex = idx;
                Cursor = (_hoveredButtonIndex >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();

                if (_hoveredButtonIndex >= 0)
                {
                    string? tip = _buttons[_hoveredButtonIndex].ToolTip;
                    if (tip != _currentToolTip)
                    {
                        _currentToolTip = tip;
                        _toolTip.SetToolTip(this, tip);
                    }
                }
                else
                {
                    _currentToolTip = null;
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredButtonIndex = -1;
            _pressedButtonIndex = -1;
            _currentToolTip = null;
            _toolTip.SetToolTip(this, null);
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressedButtonIndex = HitTest(e.Location);
                if (_pressedButtonIndex >= 0) Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _pressedButtonIndex >= 0)
            {
                int releasedIdx = HitTest(e.Location);
                if (releasedIdx == _pressedButtonIndex)
                {
                    ExecuteButtonAction(_buttons[releasedIdx]);
                }
                _pressedButtonIndex = -1;
                Invalidate();
            }
        }

        private void ExecuteButtonAction(PagerButton btn)
        {
            switch (btn.Kind)
            {
                case PagerButtonKind.First:
                    _model.FirstPage();
                    break;
                case PagerButtonKind.Prev:
                    _model.PrevPage();
                    break;
                case PagerButtonKind.Next:
                    _model.NextPage();
                    break;
                case PagerButtonKind.Last:
                    _model.LastPage();
                    break;
                case PagerButtonKind.PageNumber:
                    _model.GoToPage(btn.PageNumber);
                    break;
                case PagerButtonKind.PageSizeSelector:
                    ShowPageSizeMenu(btn.Bounds);
                    break;
            }
        }

        private void ShowPageSizeMenu(Rectangle bounds)
        {
            var menu = new ContextMenuStrip();
            foreach (int size in _pageSizes)
            {
                int s = size;
                var itm = new ToolStripMenuItem($"{s} rows / page", null, (sender, e) =>
                {
                    _model.SetPageSize(s);
                })
                {
                    Checked = (s == _model.PageSize)
                };
                menu.Items.Add(itm);
            }
            menu.Show(this, new Point(bounds.Left, bounds.Bottom + 2));
        }

        #endregion

        #region Paint Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;

            // 1. Draw Background
            using (var bgBrush = new SolidBrush(palette.Surface))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Top Border
            using (var topPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(topPen, 0, 0, Width, 0);
            }

            // 2. Draw Left Summary Text
            if (_showSummary)
            {
                string summary = _model.TotalCount == 0
                    ? "No items"
                    : $"Showing {_model.StartItemIndex:N0} - {_model.EndItemIndex:N0} of {_model.TotalCount:N0} items";

                Rectangle textRect = new Rectangle(14, 0, 320, Height);
                TextRenderer.DrawText(
                    g,
                    summary,
                    Font,
                    textRect,
                    palette.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // 3. Draw Buttons
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_buttonRadius);

            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                bool isHovered = (_hoveredButtonIndex == i);
                bool isPressed = (_pressedButtonIndex == i);

                using (var path = ZeroUIConfig.CreateRoundedRectangle(btn.Bounds, effRadius))
                {
                    if (btn.IsActive)
                    {
                        using var activeBrush = new SolidBrush(palette.Primary);
                        g.FillPath(activeBrush, path);

                        TextRenderer.DrawText(
                            g,
                            btn.Text,
                            ZeroFontCache.Get(Font.FontFamily.Name, Font.Size, FontStyle.Bold),
                            btn.Bounds,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                    else if (btn.Kind == PagerButtonKind.Ellipsis)
                    {
                        TextRenderer.DrawText(
                            g,
                            btn.Text,
                            Font,
                            btn.Bounds,
                            palette.TextSecondary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                    else
                    {
                        Color bg = isPressed
                            ? palette.Hover
                            : isHovered
                                ? palette.Hover
                                : Color.Transparent;

                        if (bg != Color.Transparent)
                        {
                            using var brush = new SolidBrush(bg);
                            g.FillPath(brush, path);
                        }

                        Color fg = btn.IsEnabled ? palette.TextPrimary : palette.TextSecondary;
                        if (btn.Kind == PagerButtonKind.PageSizeSelector)
                        {
                            using var borderPen = new Pen(palette.Border, 1f);
                            g.DrawPath(borderPen, path);
                        }

                        TextRenderer.DrawText(
                            g,
                            btn.Text,
                            Font,
                            btn.Bounds,
                            fg,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                }
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PaginationControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPagination instead.")]
    [ToolboxItem(false)]
    public class PaginationControl : ZPagination
    {
    }

    #endregion
}
