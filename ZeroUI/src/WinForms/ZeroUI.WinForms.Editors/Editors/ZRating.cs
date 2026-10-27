using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Precision rating editor supporting half-star increments (0.5), custom vector glyphs,
    /// smooth hover previews, and bidirectional IZeroEditor data binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Value")]
    [DefaultEvent("ValueChanged")]
    [Description("Precision half-star rating and defect severity editor")]
    [ToolboxBitmap(typeof(ZeroIcons), "RatingControl.bmp")]
    public class ZRating : ControlBase, IZeroEditor
    {
        private readonly RatingModel _model = new RatingModel();
        private RatingShape _shape = RatingShape.Star;
        private int _itemSize = 22;
        private int _itemSpacing = 6;
        private Color _ratedColor = Color.FromArgb(245, 158, 11); // Amber Gold #F59E0B
        private Color _unratedColor = Color.FromArgb(203, 213, 225); // Slate 300
        private Color _hoverColor = Color.FromArgb(251, 191, 36); // Amber 400

        private bool _isReadOnly = false;
        private bool _isModified = false;
        private decimal? _hoverValue = null;

        public RatingModel Model => _model;

        public event EventHandler? ValueChanged;
        public event EventHandler? EditValueChanged;

        public ZRating()
        {
            Size = new Size(140, 32);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        #region Properties

        [Category("ZeroUI")]
        [Description("The current rating score.")]
        [DefaultValue(typeof(decimal), "0")]
        public decimal Value
        {
            get => _model.Value;
            set
            {
                decimal clamped = _model.ClampValue(value);
                if (_model.Value != clamped)
                {
                    _model.Value = clamped;
                    Invalidate();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Maximum score (number of glyph items).")]
        [DefaultValue(5)]
        public int MaxRating
        {
            get => _model.MaxRating;
            set
            {
                if (_model.MaxRating != value)
                {
                    _model.MaxRating = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Enables fractional 0.5 half-step precision.")]
        [DefaultValue(true)]
        public bool AllowHalf
        {
            get => _model.AllowHalf;
            set
            {
                _model.AllowHalf = value;
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Vector glyph shape to render.")]
        [DefaultValue(RatingShape.Star)]
        public RatingShape Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Diameter or size of each glyph item in pixels.")]
        [DefaultValue(22)]
        public int ItemSize
        {
            get => _itemSize;
            set
            {
                _itemSize = Math.Max(12, value);
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Horizontal spacing between adjacent glyph items.")]
        [DefaultValue(6)]
        public int ItemSpacing
        {
            get => _itemSpacing;
            set
            {
                _itemSpacing = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Fill color for active rated items.")]
        public Color RatedColor
        {
            get => _ratedColor;
            set { _ratedColor = value; Invalidate(); }
        }

        [Category("ZeroUI")]
        [Description("Color for unrated/empty glyphs.")]
        public Color UnratedColor
        {
            get => _unratedColor;
            set { _unratedColor = value; Invalidate(); }
        }

        [Category("ZeroUI")]
        [Description("Fill color during mouse hover preview.")]
        public Color HoverColor
        {
            get => _hoverColor;
            set { _hoverColor = value; Invalidate(); }
        }

        #endregion

        #region IZeroEditor Implementation

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => Value;
            set
            {
                if (value == null || value == DBNull.Value)
                {
                    Value = 0m;
                }
                else if (decimal.TryParse(value.ToString(), out decimal decVal))
                {
                    Value = decVal;
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Tracks whether user has modified the initial value.")]
        [DefaultValue(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Category("ZeroUI")]
        [Description("Prevents user interaction when in read-only mode.")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _isReadOnly;
            set
            {
                _isReadOnly = value;
                Cursor = value ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        public void Reset()
        {
            Value = 0m;
            _isModified = false;
        }

        public void Clear()
        {
            Value = 0m;
            _isModified = false;
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isReadOnly) return;

            decimal newHover = CalculateScoreFromPoint(e.Location);
            if (_hoverValue != newHover)
            {
                _hoverValue = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverValue != null)
            {
                _hoverValue = null;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_isReadOnly || e.Button != MouseButtons.Left) return;

            decimal newScore = CalculateScoreFromPoint(e.Location);
            _model.ToggleOrSet(newScore);
            _isModified = true;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private decimal CalculateScoreFromPoint(Point pt)
        {
            return _model.CalculateScoreFromPosition(pt.X, Padding.Left + 2, _itemSize, _itemSpacing);
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            decimal displayScore = _hoverValue ?? _model.Value;
            Color activeColor = (_hoverValue != null && !_isReadOnly) ? _hoverColor : _ratedColor;
            Color inactiveColor = EffectiveSkin.IsDark ? Color.FromArgb(71, 85, 105) : _unratedColor;

            int startX = Padding.Left + 2;
            int startY = (Height - _itemSize) / 2;
            int totalItemSpan = _itemSize + _itemSpacing;

            for (int i = 0; i < _model.MaxRating; i++)
            {
                var itemRect = new Rectangle(startX + (i * totalItemSpan), startY, _itemSize, _itemSize);
                decimal itemThreshold = i + 1m;
                decimal itemHalfThreshold = i + 0.5m;

                if (displayScore >= itemThreshold)
                {
                    // Fully rated
                    DrawGlyph(g, itemRect, activeColor, activeColor);
                }
                else if (displayScore >= itemHalfThreshold)
                {
                    // Half rated: draw empty base, then clip left 50% and draw filled
                    DrawGlyph(g, itemRect, inactiveColor, inactiveColor);

                    var state = g.Save();
                    var clipRect = new Rectangle(itemRect.X, itemRect.Y, itemRect.Width / 2, itemRect.Height);
                    g.SetClip(clipRect);
                    DrawGlyph(g, itemRect, activeColor, activeColor);
                    g.Restore(state);
                }
                else
                {
                    // Unrated
                    DrawGlyph(g, itemRect, inactiveColor, inactiveColor);
                }
            }
        }

        private void DrawGlyph(Graphics g, Rectangle rect, Color fillColor, Color strokeColor)
        {
            using var path = CreateGlyphPath(rect, _shape);
            using var brush = new SolidBrush(fillColor);
            using var pen = new Pen(strokeColor, 1f);

            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath CreateGlyphPath(Rectangle rect, RatingShape shape)
        {
            var path = new GraphicsPath();
            float cx = rect.X + (rect.Width / 2f);
            float cy = rect.Y + (rect.Height / 2f);
            float r = Math.Min(rect.Width, rect.Height) / 2f - 1f;

            switch (shape)
            {
                case RatingShape.Star:
                    float rInner = r * 0.42f;
                    PointF[] starPts = new PointF[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float rad = (float)(i * Math.PI / 5.0 - Math.PI / 2.0);
                        float cr = (i % 2 == 0) ? r : rInner;
                        starPts[i] = new PointF(cx + (float)Math.Cos(rad) * cr, cy + (float)Math.Sin(rad) * cr);
                    }
                    path.AddPolygon(starPts);
                    break;

                case RatingShape.Diamond:
                    path.AddPolygon(new[]
                    {
                        new PointF(cx, cy - r),
                        new PointF(cx + r, cy),
                        new PointF(cx, cy + r),
                        new PointF(cx - r, cy)
                    });
                    break;

                case RatingShape.Heart:
                    float hr = r * 0.85f;
                    path.AddArc(cx - hr, cy - hr, hr, hr, 135, 225);
                    path.AddArc(cx, cy - hr, hr, hr, 180, 225);
                    path.AddLine(cx + (float)(hr * Math.Cos(Math.PI / 4)), cy + hr * 0.2f, cx, cy + hr);
                    path.CloseFigure();
                    break;

                case RatingShape.Shield:
                    path.AddLine(cx - r, cy - r * 0.8f, cx + r, cy - r * 0.8f);
                    path.AddLine(cx + r, cy - r * 0.8f, cx + r, cy + r * 0.1f);
                    path.AddBezier(
                        cx + r, cy + r * 0.1f,
                        cx + r * 0.8f, cy + r * 0.8f,
                        cx, cy + r,
                        cx, cy + r);
                    path.AddBezier(
                        cx, cy + r,
                        cx - r * 0.8f, cy + r * 0.8f,
                        cx - r, cy + r * 0.1f,
                        cx - r, cy + r * 0.1f);
                    path.CloseFigure();
                    break;
            }

            return path;
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRating"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RatingControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRating instead.")]
    [ToolboxItem(false)]
    public class RatingControl : ZRating
    {
    }

    #endregion
}
