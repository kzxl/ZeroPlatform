using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Input;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Dual-thumb range slider for WinForms allowing interactive selection of a [LowerValue, UpperValue] range.
    /// Supports dragging individual thumbs, dragging the middle range bar, step snapping, and dark theme rendering.
    /// </summary>
    [DefaultEvent(nameof(RangeChanged))]
    [DefaultProperty(nameof(LowerValue))]
    public class ZRangeSlider : Control, IZeroEditor
    {
        private readonly RangeSpanModel _model = new RangeSpanModel(0f, 100f, 20f, 80f, 1f, 0f);
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private bool _showRangeText = true;
        private bool _readOnly;
        private bool _isModified;

        private enum DragMode { None, Lower, Upper, RangeBar }
        private DragMode _dragMode = DragMode.None;
        private Point _dragStartPoint;
        private double _dragStartLower;
        private double _dragStartUpper;
        private DragMode _hoverMode = DragMode.None;

        public RangeSpanModel Model => _model;

        [Category("Range")]
        [DefaultValue(0.0)]
        public double Minimum
        {
            get => _model.Minimum;
            set { _model.Minimum = (float)value; Invalidate(); }
        }

        [Category("Range")]
        [DefaultValue(100.0)]
        public double Maximum
        {
            get => _model.Maximum;
            set { _model.Maximum = (float)value; Invalidate(); }
        }

        [Category("Range")]
        [DefaultValue(20.0)]
        public double LowerValue
        {
            get => _model.LowerValue;
            set
            {
                _model.SetLower((float)value);
                _isModified = true;
                OnRangeChanged();
                Invalidate();
            }
        }

        [Category("Range")]
        [DefaultValue(80.0)]
        public double UpperValue
        {
            get => _model.UpperValue;
            set
            {
                _model.SetUpper((float)value);
                _isModified = true;
                OnRangeChanged();
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(1.0)]
        public double Step
        {
            get => _model.Step;
            set => _model.Step = (float)value;
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string Prefix
        {
            get => _prefix;
            set { _prefix = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string Suffix
        {
            get => _suffix;
            set { _suffix = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowRangeText
        {
            get => _showRangeText;
            set { _showRangeText = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set => _readOnly = value;
        }

        [Browsable(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => ((double)_model.LowerValue, (double)_model.UpperValue);
            set
            {
                if (value is ValueTuple<double, double> tuple)
                {
                    SetRange(tuple.Item1, tuple.Item2);
                }
                else if (value is string s && s.Contains("-"))
                {
                    var parts = s.Split('-');
                    if (parts.Length == 2 && double.TryParse(parts[0], out var l) && double.TryParse(parts[1], out var u))
                    {
                        SetRange(l, u);
                    }
                }
            }
        }

        public event EventHandler<(double Lower, double Upper)>? RangeChanged;
        public event EventHandler? EditValueChanged;

        public ZRangeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 36;
            Width = 200;
        }

        public void SetRange(double lower, double upper)
        {
            _model.SetValues((float)lower, (float)upper);
            _isModified = true;
            OnRangeChanged();
            Invalidate();
        }

        public void Reset()
        {
            _model.LowerValue = _model.Minimum;
            _model.UpperValue = _model.Maximum;
            _isModified = false;
            OnRangeChanged();
            Invalidate();
        }

        public void Clear()
        {
            Reset();
        }

        private double SnapToStep(double val)
        {
            return RangeMath.SnapToStep((float)val, _model.Minimum, _model.Maximum, _model.Step);
        }

        private void OnRangeChanged()
        {
            RangeChanged?.Invoke(this, (_model.LowerValue, _model.UpperValue));
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private (Rectangle trackRect, Rectangle activeRect, Rectangle lowerThumb, Rectangle upperThumb) ComputeLayout()
        {
            int top = _showRangeText ? 16 : (Height / 2) - 3;
            int margin = 10;
            int trackWidth = Width - (margin * 2);
            int trackHeight = 6;
            int thumbSize = 16;

            var trackRect = new Rectangle(margin, top, trackWidth, trackHeight);

            double lowerPct = _model.LowerFraction;
            double upperPct = _model.UpperFraction;

            int lowerX = margin + (int)(lowerPct * trackWidth);
            int upperX = margin + (int)(upperPct * trackWidth);

            var lowerThumb = new Rectangle(lowerX - (thumbSize / 2), top + (trackHeight / 2) - (thumbSize / 2), thumbSize, thumbSize);
            var upperThumb = new Rectangle(upperX - (thumbSize / 2), top + (trackHeight / 2) - (thumbSize / 2), thumbSize, thumbSize);
            var activeRect = new Rectangle(lowerX, top - 1, Math.Max(0, upperX - lowerX), trackHeight + 2);

            return (trackRect, activeRect, lowerThumb, upperThumb);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var (trackRect, activeRect, lowerThumb, upperThumb) = ComputeLayout();

            // Background text
            if (_showRangeText)
            {
                string text = $"{_prefix}{_model.LowerValue:F0}{_suffix} — {_prefix}{_model.UpperValue:F0}{_suffix}";
                using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                using var brush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
                g.DrawString(text, font, brush, Width - 8, 1, new StringFormat { Alignment = StringAlignment.Far });
            }

            // Background Groove
            using (var grooveBrush = new SolidBrush(ZeroTheme.Colors.BorderDefault))
            using (var path = CreateRoundedRectanglePath(trackRect, 3))
            {
                g.FillPath(grooveBrush, path);
            }

            // Active Range Span
            if (activeRect.Width > 0)
            {
                using var activeBrush = new SolidBrush(ZeroTheme.Colors.PrimaryAccent);
                using var path = CreateRoundedRectanglePath(activeRect, 3);
                g.FillPath(activeBrush, path);
            }

            // Thumbs
            DrawThumb(g, lowerThumb, _hoverMode == DragMode.Lower || _dragMode == DragMode.Lower);
            DrawThumb(g, upperThumb, _hoverMode == DragMode.Upper || _dragMode == DragMode.Upper);
        }

        private void DrawThumb(Graphics g, Rectangle rect, bool isHot)
        {
            using var fillBrush = new SolidBrush(ZeroTheme.Colors.BgCard);
            using var strokePen = new Pen(isHot ? ZeroTheme.Colors.PrimaryAccentDark : ZeroTheme.Colors.PrimaryAccent, 2.5f);

            g.FillEllipse(fillBrush, rect);
            g.DrawEllipse(strokePen, rect);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_readOnly || e.Button != MouseButtons.Left) return;

            var (_, activeRect, lowerThumb, upperThumb) = ComputeLayout();

            _dragStartPoint = e.Location;
            _dragStartLower = _model.LowerValue;
            _dragStartUpper = _model.UpperValue;

            if (lowerThumb.Contains(e.Location))
            {
                _dragMode = DragMode.Lower;
            }
            else if (upperThumb.Contains(e.Location))
            {
                _dragMode = DragMode.Upper;
            }
            else if (activeRect.Contains(e.Location))
            {
                _dragMode = DragMode.RangeBar;
            }

            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var (trackRect, activeRect, lowerThumb, upperThumb) = ComputeLayout();

            if (_dragMode != DragMode.None && !_readOnly)
            {
                double span = _model.Maximum - _model.Minimum;
                int deltaPixels = e.X - _dragStartPoint.X;
                double deltaVal = ((double)deltaPixels / trackRect.Width) * span;

                if (_dragMode == DragMode.Lower)
                {
                    _model.SetLower((float)(_dragStartLower + deltaVal));
                    _isModified = true;
                    OnRangeChanged();
                    Invalidate();
                }
                else if (_dragMode == DragMode.Upper)
                {
                    _model.SetUpper((float)(_dragStartUpper + deltaVal));
                    _isModified = true;
                    OnRangeChanged();
                    Invalidate();
                }
                else if (_dragMode == DragMode.RangeBar)
                {
                    _model.SetValues((float)_dragStartLower, (float)_dragStartUpper);
                    _model.TranslateSpan((float)deltaVal);
                    _isModified = true;
                    OnRangeChanged();
                    Invalidate();
                }
                return;
            }

            // Hover tracking
            DragMode newHover = DragMode.None;
            if (lowerThumb.Contains(e.Location))
            {
                newHover = DragMode.Lower;
                Cursor = Cursors.Hand;
            }
            else if (upperThumb.Contains(e.Location))
            {
                newHover = DragMode.Upper;
                Cursor = Cursors.Hand;
            }
            else if (activeRect.Contains(e.Location))
            {
                newHover = DragMode.RangeBar;
                Cursor = Cursors.SizeWE;
            }
            else
            {
                Cursor = Cursors.Default;
            }

            if (newHover != _hoverMode)
            {
                _hoverMode = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragMode = DragMode.None;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverMode = DragMode.None;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRangeSlider"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RangeSlider is deprecated and will be removed in 5 release cycles. Please migrate to ZRangeSlider instead.")]
    [ToolboxItem(false)]
    public class RangeSlider : ZRangeSlider
    {
    }

    #endregion
}
