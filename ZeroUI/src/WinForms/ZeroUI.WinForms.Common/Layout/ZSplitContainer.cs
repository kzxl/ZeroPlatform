using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    public enum SplitterCollapseMode
    {
        None,
        CollapsePanel1,
        CollapsePanel2
    }

    /// <summary>
    /// Lightweight child panel for SplitContainerControl.
    /// </summary>
    [ToolboxItem(false)]
    public class SplitterPanelControl : Panel
    {
        public SplitterPanelControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }
    }

    /// <summary>
    /// Modern anti-aliased SplitContainer for ZeroUI.
    /// Provides sleek single-pass layout, hover grip feedback, and one-click collapsible panel support.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Splits the control display area into two resizable panels.")]
    [ToolboxBitmap(typeof(ZeroIcons), "SplitContainerControl.bmp")]
    public class SplitContainerControl : Control
    {
        private readonly SplitterPanelControl _panel1;
        private readonly SplitterPanelControl _panel2;

        private Orientation _orientation = Orientation.Vertical;
        private int _splitterDistance = 250;
        private int _splitterWidth = 7;
        private int _minSizePanel1 = 60;
        private int _minSizePanel2 = 60;
        private bool _isSplitterFixed = false;
        private SplitterCollapseMode _collapseMode = SplitterCollapseMode.None;

        private bool _isHovered = false;
        private bool _isDragging = false;
        private int _dragStartPos = 0;
        private int _dragStartDistance = 0;

        private Rectangle _splitterRect;
        private Rectangle _collapseButtonRect;
        private int _savedSplitterDistance = 250;

        public event EventHandler? SplitterMoved;
        public event EventHandler? CollapseStateChanged;

        public SplitContainerControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _panel1 = new SplitterPanelControl();
            _panel2 = new SplitterPanelControl();

            Controls.Add(_panel1);
            Controls.Add(_panel2);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            UpdateLayout();
        }

        [Category("Layout")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public SplitterPanelControl Panel1 => _panel1;

        [Category("Layout")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public SplitterPanelControl Panel2 => _panel2;

        [Category("Layout")]
        [DefaultValue(Orientation.Vertical)]
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    UpdateLayout();
                    Invalidate();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(250)]
        public int SplitterDistance
        {
            get => _splitterDistance;
            set
            {
                int maxDist = _orientation == Orientation.Vertical ? Math.Max(_minSizePanel1, Width - _splitterWidth - _minSizePanel2)
                                                                   : Math.Max(_minSizePanel1, Height - _splitterWidth - _minSizePanel2);
                int clamped = Math.Max(_minSizePanel1, Math.Min(maxDist, value));
                if (_splitterDistance != clamped)
                {
                    _splitterDistance = clamped;
                    UpdateLayout();
                    Invalidate();
                    SplitterMoved?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(7)]
        public int SplitterWidth
        {
            get => _splitterWidth;
            set
            {
                _splitterWidth = Math.Max(3, value);
                UpdateLayout();
                Invalidate();
            }
        }

        [Category("Layout")]
        [DefaultValue(60)]
        public int MinSizePanel1
        {
            get => _minSizePanel1;
            set
            {
                _minSizePanel1 = Math.Max(0, value);
                UpdateLayout();
            }
        }

        [Category("Layout")]
        [DefaultValue(60)]
        public int MinSizePanel2
        {
            get => _minSizePanel2;
            set
            {
                _minSizePanel2 = Math.Max(0, value);
                UpdateLayout();
            }
        }

        [Category("Layout")]
        [DefaultValue(false)]
        public bool IsSplitterFixed
        {
            get => _isSplitterFixed;
            set => _isSplitterFixed = value;
        }

        [Category("Layout")]
        [DefaultValue(SplitterCollapseMode.None)]
        public SplitterCollapseMode CollapseMode
        {
            get => _collapseMode;
            set
            {
                if (_collapseMode != value)
                {
                    _collapseMode = value;
                    UpdateLayout();
                    Invalidate();
                    CollapseStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void ToggleCollapse()
        {
            if (_collapseMode == SplitterCollapseMode.None)
            {
                _savedSplitterDistance = _splitterDistance;
                CollapseMode = SplitterCollapseMode.CollapsePanel1;
            }
            else
            {
                CollapseMode = SplitterCollapseMode.None;
                SplitterDistance = _savedSplitterDistance;
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (Width <= 0 || Height <= 0 || _panel1 == null || _panel2 == null) return;

            SuspendLayout();

            if (_collapseMode == SplitterCollapseMode.CollapsePanel1)
            {
                _panel1.Visible = false;
                _panel2.Visible = true;
                if (_orientation == Orientation.Vertical)
                {
                    _splitterRect = new Rectangle(0, 0, _splitterWidth, Height);
                    _panel2.SetBounds(_splitterWidth, 0, Math.Max(0, Width - _splitterWidth), Height);
                    _collapseButtonRect = new Rectangle(0, (Height - 40) / 2, _splitterWidth, 40);
                }
                else
                {
                    _splitterRect = new Rectangle(0, 0, Width, _splitterWidth);
                    _panel2.SetBounds(0, _splitterWidth, Width, Math.Max(0, Height - _splitterWidth));
                    _collapseButtonRect = new Rectangle((Width - 40) / 2, 0, 40, _splitterWidth);
                }
            }
            else if (_collapseMode == SplitterCollapseMode.CollapsePanel2)
            {
                _panel1.Visible = true;
                _panel2.Visible = false;
                if (_orientation == Orientation.Vertical)
                {
                    int x = Width - _splitterWidth;
                    _splitterRect = new Rectangle(x, 0, _splitterWidth, Height);
                    _panel1.SetBounds(0, 0, Math.Max(0, x), Height);
                    _collapseButtonRect = new Rectangle(x, (Height - 40) / 2, _splitterWidth, 40);
                }
                else
                {
                    int y = Height - _splitterWidth;
                    _splitterRect = new Rectangle(0, y, Width, _splitterWidth);
                    _panel1.SetBounds(0, 0, Width, Math.Max(0, y));
                    _collapseButtonRect = new Rectangle((Width - 40) / 2, y, 40, _splitterWidth);
                }
            }
            else
            {
                _panel1.Visible = true;
                _panel2.Visible = true;

                if (_orientation == Orientation.Vertical)
                {
                    int maxDist = Math.Max(_minSizePanel1, Width - _splitterWidth - _minSizePanel2);
                    _splitterDistance = Math.Max(_minSizePanel1, Math.Min(maxDist, _splitterDistance));

                    _panel1.SetBounds(0, 0, _splitterDistance, Height);
                    _splitterRect = new Rectangle(_splitterDistance, 0, _splitterWidth, Height);
                    _panel2.SetBounds(_splitterDistance + _splitterWidth, 0, Math.Max(0, Width - _splitterDistance - _splitterWidth), Height);
                    _collapseButtonRect = new Rectangle(_splitterDistance, (Height - 40) / 2, _splitterWidth, 40);
                }
                else
                {
                    int maxDist = Math.Max(_minSizePanel1, Height - _splitterWidth - _minSizePanel2);
                    _splitterDistance = Math.Max(_minSizePanel1, Math.Min(maxDist, _splitterDistance));

                    _panel1.SetBounds(0, 0, Width, _splitterDistance);
                    _splitterRect = new Rectangle(0, _splitterDistance, Width, _splitterWidth);
                    _panel2.SetBounds(0, _splitterDistance + _splitterWidth, Width, Math.Max(0, Height - _splitterDistance - _splitterWidth));
                    _collapseButtonRect = new Rectangle((Width - 40) / 2, _splitterDistance, 40, _splitterWidth);
                }
            }

            ResumeLayout(true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && !_isSplitterFixed)
            {
                int delta = _orientation == Orientation.Vertical ? (e.X - _dragStartPos) : (e.Y - _dragStartPos);
                SplitterDistance = _dragStartDistance + delta;
                return;
            }

            bool overSplitter = _splitterRect.Contains(e.Location);
            if (overSplitter != _isHovered)
            {
                _isHovered = overSplitter;
                Invalidate(_splitterRect);
            }

            if (!_isSplitterFixed && overSplitter)
            {
                Cursor = _orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left && _splitterRect.Contains(e.Location))
            {
                if (_collapseButtonRect.Contains(e.Location) && e.Clicks == 2)
                {
                    ToggleCollapse();
                    return;
                }

                if (!_isSplitterFixed && _collapseMode == SplitterCollapseMode.None)
                {
                    _isDragging = true;
                    _dragStartPos = _orientation == Orientation.Vertical ? e.X : e.Y;
                    _dragStartDistance = _splitterDistance;
                    Capture = true;
                    Invalidate(_splitterRect);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                Invalidate(_splitterRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHovered && !_isDragging)
            {
                _isHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_splitterRect);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Palette;
            Color trackColor = palette.Border;
            Color highlightColor = _isDragging ? palette.Primary : (_isHovered ? palette.PrimaryHover : trackColor);

            // 1. Draw Splitter Bar
            using (var brush = new SolidBrush(highlightColor))
            {
                g.FillRectangle(brush, _splitterRect);
            }

            // 2. Draw Subtle Grip Handle (3 small dots in center)
            Color dotColor = _isHovered || _isDragging ? Color.White : palette.TextSecondary;
            using (var dotBrush = new SolidBrush(dotColor))
            {
                if (_orientation == Orientation.Vertical)
                {
                    int cx = _splitterRect.X + (_splitterRect.Width / 2);
                    int cy = _splitterRect.Y + (_splitterRect.Height / 2);
                    g.FillEllipse(dotBrush, cx - 1, cy - 8, 3, 3);
                    g.FillEllipse(dotBrush, cx - 1, cy, 3, 3);
                    g.FillEllipse(dotBrush, cx - 1, cy + 8, 3, 3);
                }
                else
                {
                    int cx = _splitterRect.X + (_splitterRect.Width / 2);
                    int cy = _splitterRect.Y + (_splitterRect.Height / 2);
                    g.FillEllipse(dotBrush, cx - 8, cy - 1, 3, 3);
                    g.FillEllipse(dotBrush, cx, cy - 1, 3, 3);
                    g.FillEllipse(dotBrush, cx + 8, cy - 1, 3, 3);
                }
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

    /// <summary>
    /// Modern anti-aliased SplitContainer for ZeroUI.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.SplitContainer"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Splits the control display area into two resizable panels with Z-prefix standard.")]
    [ToolboxBitmap(typeof(ZeroIcons), "SplitContainerControl.bmp")]
    [Designer("ZeroUI.WinForms.Design.Common.ZSplitContainerDesigner, ZeroUI.WinForms.Design")]
    public class ZSplitContainer : SplitContainerControl
    {
    }

    /// <summary>
    /// Lightweight child panel for <see cref="ZSplitContainer"/>.
    /// </summary>
    [ToolboxItem(false)]
    public class ZSplitterPanel : SplitterPanelControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    [Obsolete("ZeroSplitterPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZSplitterPanel instead.")]
    [ToolboxItem(false)]
    public class ZeroSplitterPanel : SplitterPanelControl
    {
    }

    [Obsolete("ZeroSplitContainer is deprecated and will be removed in 5 release cycles. Please migrate to ZSplitContainer instead.")]
    [ToolboxItem(false)]
    public class ZeroSplitContainer : SplitContainerControl
    {
    }

    #endregion
}
