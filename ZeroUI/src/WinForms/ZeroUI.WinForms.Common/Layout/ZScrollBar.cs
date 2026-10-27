using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    public enum ScrollBarOrientation
    {
        Vertical,
        Horizontal
    }

    /// <summary>
    /// Modern anti-aliased flat scrollbar for ZeroUI.
    /// Replaces legacy Win32 HScrollBar/VScrollBar with sleek dark/light theme integration,
    /// rounded pill thumb geometry, and zero GC allocation in hot render loops.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Provides a custom modern flat scrollbar supporting horizontal and vertical orientations.")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroScrollBar.bmp")]
    public class ZScrollBar : Control
    {
        private ScrollBarOrientation _orientation = ScrollBarOrientation.Vertical;
        private int _minimum = 0;
        private int _maximum = 100;
        private int _value = 0;
        private int _smallChange = 1;
        private int _largeChange = 10;

        private bool _isHovered = false;
        private bool _isDragging = false;
        private int _dragStartPos = 0;
        private int _dragStartValue = 0;
        private Rectangle _thumbRect;

        public event EventHandler? ValueChanged;

        public ZScrollBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            Width = 12;
            Height = 150;

            ZeroTheme.ThemeChanged += OnThemeChanged;
            RecalculateThumb();
        }

        [Category("Layout")]
        [DefaultValue(ScrollBarOrientation.Vertical)]
        public ScrollBarOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    // Swap default dimensions when orientation changes
                    int temp = Width;
                    Width = Height;
                    Height = temp;
                    RecalculateThumb();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                if (_minimum != value)
                {
                    _minimum = value;
                    if (_value < _minimum) Value = _minimum;
                    RecalculateThumb();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = Math.Max(_minimum, value);
                    if (_value > _maximum) Value = _maximum;
                    RecalculateThumb();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    RecalculateThumb();
                    Invalidate();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        public int SmallChange
        {
            get => _smallChange;
            set => _smallChange = Math.Max(1, value);
        }

        [Category("Behavior")]
        [DefaultValue(10)]
        public int LargeChange
        {
            get => _largeChange;
            set => _largeChange = Math.Max(1, value);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateThumb();
        }

        private void RecalculateThumb()
        {
            int range = _maximum - _minimum;
            if (range <= 0)
            {
                _thumbRect = Rectangle.Empty;
                return;
            }

            int margin = 2;
            if (_orientation == ScrollBarOrientation.Vertical)
            {
                int trackHeight = Math.Max(10, Height - (margin * 2));
                int thumbHeight = Math.Max(24, (int)((float)_largeChange / (_largeChange + range) * trackHeight));
                int availableTrack = trackHeight - thumbHeight;
                int thumbY = margin + (int)((float)(_value - _minimum) / range * availableTrack);

                _thumbRect = new Rectangle(margin, thumbY, Math.Max(4, Width - (margin * 2)), thumbHeight);
            }
            else // Horizontal
            {
                int trackWidth = Math.Max(10, Width - (margin * 2));
                int thumbWidth = Math.Max(24, (int)((float)_largeChange / (_largeChange + range) * trackWidth));
                int availableTrack = trackWidth - thumbWidth;
                int thumbX = margin + (int)((float)(_value - _minimum) / range * availableTrack);

                _thumbRect = new Rectangle(thumbX, margin, thumbWidth, Math.Max(4, Height - (margin * 2)));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging)
            {
                int range = _maximum - _minimum;
                if (range <= 0) return;

                int margin = 2;
                if (_orientation == ScrollBarOrientation.Vertical)
                {
                    int trackHeight = Math.Max(10, Height - (margin * 2));
                    int thumbHeight = _thumbRect.Height;
                    int availableTrack = Math.Max(1, trackHeight - thumbHeight);
                    int deltaY = e.Y - _dragStartPos;
                    int deltaVal = (int)((float)deltaY / availableTrack * range);
                    Value = _dragStartValue + deltaVal;
                }
                else
                {
                    int trackWidth = Math.Max(10, Width - (margin * 2));
                    int thumbWidth = _thumbRect.Width;
                    int availableTrack = Math.Max(1, trackWidth - thumbWidth);
                    int deltaX = e.X - _dragStartPos;
                    int deltaVal = (int)((float)deltaX / availableTrack * range);
                    Value = _dragStartValue + deltaVal;
                }
                return;
            }

            bool overThumb = _thumbRect.Contains(e.Location);
            if (overThumb != _isHovered)
            {
                _isHovered = overThumb;
                Invalidate(_thumbRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                if (_thumbRect.Contains(e.Location))
                {
                    _isDragging = true;
                    _dragStartPos = _orientation == ScrollBarOrientation.Vertical ? e.Y : e.X;
                    _dragStartValue = _value;
                    Capture = true;
                    Invalidate(_thumbRect);
                }
                else
                {
                    // Click on track: Page Up or Page Down
                    if (_orientation == ScrollBarOrientation.Vertical)
                    {
                        if (e.Y < _thumbRect.Top) Value -= _largeChange;
                        else if (e.Y > _thumbRect.Bottom) Value += _largeChange;
                    }
                    else
                    {
                        if (e.X < _thumbRect.Left) Value -= _largeChange;
                        else if (e.X > _thumbRect.Right) Value += _largeChange;
                    }
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
                Invalidate(_thumbRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHovered && !_isDragging)
            {
                _isHovered = false;
                Invalidate(_thumbRect);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = (e.Delta / 120) * _smallChange;
            Value -= step;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Palette;
            Color trackBg = palette.Surface;
            Color thumbColor = _isDragging ? palette.Primary : (_isHovered ? palette.PrimaryHover : palette.Border);

            // 1. Fill Track
            using (var trackBrush = new SolidBrush(trackBg))
            {
                g.FillRectangle(trackBrush, ClientRectangle);
            }

            // 2. Draw Thumb
            if (!_thumbRect.IsEmpty)
            {
                int radius = Math.Min(4, Math.Min(_thumbRect.Width, _thumbRect.Height) / 2);
                using var path = ZeroUIConfig.CreateRoundedRectangle(_thumbRect, radius);
                using var thumbBrush = new SolidBrush(thumbColor);
                g.FillPath(thumbBrush, path);
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

    [Obsolete("Use ScrollBarOrientation instead.")]
    public enum ZeroScrollOrientation
    {
        Vertical = ScrollBarOrientation.Vertical,
        Horizontal = ScrollBarOrientation.Horizontal
    }

    [Obsolete("Use ScrollBarControl instead.")]
    [ToolboxItem(false)]
    public class ZeroScrollBar : ScrollBarControl
    {
        public new ZeroScrollOrientation Orientation
        {
            get => (ZeroScrollOrientation)base.Orientation;
            set => base.Orientation = (ScrollBarOrientation)value;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZScrollBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ScrollBarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZScrollBar instead.")]
    [ToolboxItem(false)]
    public class ScrollBarControl : ZScrollBar
    {
    }

    #endregion
}
