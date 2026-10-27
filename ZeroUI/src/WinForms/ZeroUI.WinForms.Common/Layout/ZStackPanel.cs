using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    public enum StackOrientation
    {
        Vertical,
        Horizontal
    }

    public enum StackAlignment
    {
        Start,
        Center,
        End,
        Stretch
    }

    /// <summary>
    /// High-performance, zero-flicker StackPanel for ZeroUI.
    /// Replaces legacy FlowLayoutPanel with predictable single-pass layout and zero GC allocations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Arranges child controls into a single horizontal or vertical line with smooth layout calculation.")]
    public class ZStackPanel : Panel
    {
        private StackOrientation _orientation = StackOrientation.Vertical;
        private StackAlignment _alignment = StackAlignment.Stretch;
        private int _spacing = 8;
        private Color _borderColor = Color.Empty;
        private int _borderWidth = 0;
        private bool _isPerformingLayout = false;

        public ZStackPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            AutoScroll = true;
            Padding = new Padding(8);
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ApplyCurrentTheme();
        }

        [Category("Layout")]
        [DefaultValue(StackOrientation.Vertical)]
        [Description("The direction in which child controls are stacked.")]
        public StackOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    PerformLayout();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(StackAlignment.Stretch)]
        [Description("Alignment of child controls along the cross-axis.")]
        public StackAlignment Alignment
        {
            get => _alignment;
            set
            {
                if (_alignment != value)
                {
                    _alignment = value;
                    PerformLayout();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(8)]
        [Description("Spacing in pixels between adjacent controls.")]
        public int Spacing
        {
            get => _spacing;
            set
            {
                if (_spacing != value)
                {
                    _spacing = Math.Max(0, value);
                    PerformLayout();
                }
            }
        }

        [Category("Appearance")]
        [Description("Optional border color.")]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Optional border thickness in pixels.")]
        public int BorderWidth
        {
            get => _borderWidth;
            set
            {
                _borderWidth = Math.Max(0, value);
                Invalidate();
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            ApplyCurrentTheme();
            Invalidate();
        }

        private void ApplyCurrentTheme()
        {
            var palette = ZeroTheme.Palette;
            if (BackColor == Color.Transparent || BackColor == palette.Surface || BackColor == palette.CardBackground)
            {
                // Sync subtle borders if configured
                if (_borderWidth > 0 && _borderColor == Color.Empty)
                {
                    _borderColor = palette.Border;
                }
            }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null)
            {
                // Reset DockStyle so StackPanel manages positions cleanly without WinForms docking collisions
                if (e.Control.Dock != DockStyle.None)
                {
                    e.Control.Dock = DockStyle.None;
                }
            }
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (_isPerformingLayout) return;

            _isPerformingLayout = true;
            try
            {
                ArrangeChildren();
            }
            finally
            {
                _isPerformingLayout = false;
            }
        }

        private void ArrangeChildren()
        {
            if (Controls.Count == 0)
            {
                AutoScrollMinSize = Size.Empty;
                return;
            }

            // Use ClientRectangle to automatically account for active scrollbars and prevent horizontal scrollbar leak
            int clientW = ClientRectangle.Width;
            int clientH = ClientRectangle.Height;

            int availableWidth = Math.Max(0, clientW - Padding.Horizontal);
            int availableHeight = Math.Max(0, clientH - Padding.Vertical);

            int startX = Padding.Left + AutoScrollPosition.X;
            int startY = Padding.Top + AutoScrollPosition.Y;

            int currentX = startX;
            int currentY = startY;

            for (int i = 0; i < Controls.Count; i++)
            {
                Control child = Controls[i];
                if (!child.Visible) continue;

                if (child.Dock != DockStyle.None)
                {
                    child.Dock = DockStyle.None;
                }

                int childW = child.Width;
                int childH = child.Height;

                if (_orientation == StackOrientation.Vertical)
                {
                    int x = startX;
                    int w = childW;

                    switch (_alignment)
                    {
                        case StackAlignment.Stretch:
                            x = startX;
                            w = availableWidth;
                            break;
                        case StackAlignment.Center:
                            x = startX + Math.Max(0, (availableWidth - childW) / 2);
                            break;
                        case StackAlignment.End:
                            x = startX + Math.Max(0, availableWidth - childW);
                            break;
                        case StackAlignment.Start:
                        default:
                            x = startX;
                            break;
                    }

                    if (child.Location.X != x || child.Location.Y != currentY || child.Width != w || child.Height != childH)
                    {
                        child.SetBounds(x, currentY, w, childH, BoundsSpecified.All);
                    }

                    currentY += childH + _spacing;
                }
                else // Horizontal
                {
                    int y = startY;
                    int h = childH;

                    switch (_alignment)
                    {
                        case StackAlignment.Stretch:
                            y = startY;
                            h = availableHeight;
                            break;
                        case StackAlignment.Center:
                            y = startY + Math.Max(0, (availableHeight - childH) / 2);
                            break;
                        case StackAlignment.End:
                            y = startY + Math.Max(0, availableHeight - childH);
                            break;
                        case StackAlignment.Start:
                        default:
                            y = startY;
                            break;
                    }

                    if (child.Location.X != currentX || child.Location.Y != y || child.Width != childW || child.Height != h)
                    {
                        child.SetBounds(currentX, y, childW, h, BoundsSpecified.All);
                    }

                    currentX += childW + _spacing;
                }
            }

            // Automatically set scroll minimum virtual boundaries
            if (_orientation == StackOrientation.Vertical)
            {
                int totalHeight = (currentY - AutoScrollPosition.Y) - _spacing + Padding.Bottom;
                AutoScrollMinSize = new Size(0, Math.Max(0, totalHeight));
            }
            else
            {
                int totalWidth = (currentX - AutoScrollPosition.X) - _spacing + Padding.Right;
                AutoScrollMinSize = new Size(Math.Max(0, totalWidth), 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_borderWidth > 0 && _borderColor != Color.Empty && _borderColor != Color.Transparent)
            {
                using var pen = new Pen(_borderColor, _borderWidth);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
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

    [Obsolete("Use StackPanel instead.")]
    [ToolboxItem(false)]
    public class ZeroStackPanel : ZStackPanel
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStackPanel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StackPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZStackPanel instead.")]
    [ToolboxItem(false)]
    public class StackPanel : ZStackPanel
    {
    }

    #endregion
}
