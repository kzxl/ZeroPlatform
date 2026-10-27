using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// High-precision numeric stepper and spin box editor for industrial tolerances, setpoints,
    /// and quantities with unit prefixes/suffixes, acceleration on hold, and decimal formatting.
    /// Standardized on EditorBase for unified theme, focus rings, and data binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("Value")]
    [Description("Precision numeric stepper and spin box editor with unit formatting")]
    [ToolboxBitmap(typeof(ZeroIcons), "SpinEdit.bmp")]
    public class ZSpinEdit : EditorBase<decimal>
    {
        private decimal _minValue = 0m;
        private decimal _maxValue = 1000000m;
        private decimal _step = 1m;
        private int _decimalPlaces = 0;
        private string _prefix = "";
        private string _suffix = "";
        private bool _thousandsSeparator = true;

        private readonly TextBox _innerBox;
        private Rectangle _upButtonRect;
        private Rectangle _downButtonRect;
        private bool _hoverUp = false;
        private bool _hoverDown = false;
        private bool _pressUp = false;
        private bool _pressDown = false;

        private readonly Timer _repeatTimer;
        private int _repeatCount = 0;
        private int _direction = 0; // 1 = up, -1 = down

        [Category("Data")]
        [DefaultValue(typeof(decimal), "0")]
        public override decimal Value
        {
            get => base.Value;
            set
            {
                decimal clamped = Math.Max(_minValue, Math.Min(_maxValue, value));
                base.Value = clamped;
                FormatText();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public override bool ReadOnly
        {
            get => base.ReadOnly;
            set
            {
                base.ReadOnly = value;
                if (_innerBox != null)
                {
                    _innerBox.ReadOnly = value;
                    Invalidate();
                }
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "0")]
        public decimal MinValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                if (Value < _minValue) Value = _minValue;
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "1000000")]
        public decimal MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                if (Value > _maxValue) Value = _maxValue;
            }
        }

        [Category("Data")]
        [DefaultValue(typeof(decimal), "1")]
        public decimal Step
        {
            get => _step;
            set => _step = value > 0 ? value : 1m;
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                _decimalPlaces = Math.Max(0, Math.Min(6, value));
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string Prefix
        {
            get => _prefix;
            set
            {
                _prefix = value ?? "";
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string Suffix
        {
            get => _suffix;
            set
            {
                _suffix = value ?? "";
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ThousandsSeparator
        {
            get => _thousandsSeparator;
            set
            {
                _thousandsSeparator = value;
                FormatText();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Allows evaluating in-place arithmetic expressions like '25*4' or '(10+5)*2' when pressing Enter.")]
        public bool EnableMathEvaluation { get; set; } = true;

        public ZSpinEdit()
        {
            Size = new Size(130, 32);
            Cursor = Cursors.IBeam;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _innerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                TextAlign = HorizontalAlignment.Left
            };
            _innerBox.TextChanged += OnInnerBoxTextChanged;
            _innerBox.KeyDown += OnInnerBoxKeyDown;
            _innerBox.LostFocus += (s, e) =>
            {
                OnLostFocus(e);
                CommitText();
            };
            _innerBox.GotFocus += (s, e) =>
            {
                OnGotFocus(e);
                Invalidate();
            };
            Controls.Add(_innerBox);

            _repeatTimer = new Timer();
            _repeatTimer.Tick += OnRepeatTimerTick;

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _innerBox.Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
            UpdateTheme();
            FormatText();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        protected override bool TryConvertEditValue(object? rawValue, out decimal converted)
        {
            if (rawValue == null || rawValue == DBNull.Value)
            {
                converted = _minValue;
                return true;
            }
            if (rawValue is decimal d)
            {
                converted = d;
                return true;
            }
            if (decimal.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
            {
                converted = parsed;
                return true;
            }
            if (decimal.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsedLocal))
            {
                converted = parsedLocal;
                return true;
            }

            converted = _minValue;
            return false;
        }

        public override void Reset()
        {
            Value = MinValue;
            base.Reset();
        }

        public override void Clear()
        {
            Reset();
        }

        private void FormatText()
        {
            string numFormat = _thousandsSeparator ? $"N{_decimalPlaces}" : $"F{_decimalPlaces}";
            string formatted = Value.ToString(numFormat, CultureInfo.InvariantCulture);

            string full = "";
            if (!string.IsNullOrEmpty(_prefix)) full += _prefix + " ";
            full += formatted;
            if (!string.IsNullOrEmpty(_suffix)) full += " " + _suffix;

            if (_innerBox != null && _innerBox.Text != full)
            {
                _innerBox.Text = full;
                _innerBox.SelectionStart = _innerBox.Text.Length;
            }
        }

        private void CommitText()
        {
            if (_innerBox == null) return;
            string clean = _innerBox.Text;
            if (!string.IsNullOrEmpty(_prefix)) clean = clean.Replace(_prefix, "");
            if (!string.IsNullOrEmpty(_suffix)) clean = clean.Replace(_suffix, "");
            clean = clean.Trim().Replace(",", "");

            if (EnableMathEvaluation && MathExpressionParser.TryEvaluate(clean, out decimal mathResult))
            {
                Value = Math.Max(_minValue, Math.Min(_maxValue, mathResult));
            }
            else if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
            {
                Value = Math.Max(_minValue, Math.Min(_maxValue, parsed));
            }
            FormatText();
        }

        private void OnInnerBoxTextChanged(object? sender, EventArgs e)
        {
            if (_innerBox == null || !_innerBox.Focused) return;

            string clean = _innerBox.Text;
            if (!string.IsNullOrEmpty(_prefix)) clean = clean.Replace(_prefix, "");
            if (!string.IsNullOrEmpty(_suffix)) clean = clean.Replace(_suffix, "");
            clean = clean.Trim().Replace(",", "");

            // When math evaluation is enabled, don't parse prematurely if user is typing an expression
            if (EnableMathEvaluation && (clean.Contains("*") || clean.Contains("/") || clean.Contains("+") || clean.Contains("-") || clean.Contains("(") || clean.Contains("^") || clean.Contains("%")))
            {
                return;
            }

            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
            {
                decimal clamped = Math.Max(_minValue, Math.Min(_maxValue, parsed));
                if (Value != clamped)
                {
                    Value = clamped;
                }
            }
        }

        private void OnInnerBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                StepUp();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                StepDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                CommitText();
                e.Handled = true;
            }
        }

        public void StepUp()
        {
            if (ReadOnly || !Enabled) return;
            Value = Math.Min(_maxValue, Value + _step);
        }

        public void StepDown()
        {
            if (ReadOnly || !Enabled) return;
            Value = Math.Max(_minValue, Value - _step);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0) StepUp();
            else StepDown();
        }

        private void UpdateTheme()
        {
            var palette = CurrentPalette;
            BackColor = Color.Transparent;
            if (_innerBox != null)
            {
                _innerBox.BackColor = palette.Surface;
                _innerBox.ForeColor = palette.TextPrimary;
            }
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int btnW = 22;
            int btnH = (Height - 4) / 2;

            _upButtonRect = new Rectangle(Width - btnW - 3, 2, btnW, btnH);
            _downButtonRect = new Rectangle(Width - btnW - 3, 2 + btnH, btnW, btnH);

            if (_innerBox != null)
            {
                _innerBox.Location = new Point(10, (Height - _innerBox.Height) / 2);
                _innerBox.Width = Math.Max(10, Width - btnW - 16);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overUp = _upButtonRect.Contains(e.Location);
            bool overDown = _downButtonRect.Contains(e.Location);

            if (_hoverUp != overUp || _hoverDown != overDown)
            {
                _hoverUp = overUp;
                _hoverDown = overDown;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverUp = false;
            _hoverDown = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_upButtonRect.Contains(e.Location))
            {
                _pressUp = true;
                _direction = 1;
                StepUp();
                StartRepeatTimer();
                Invalidate();
            }
            else if (_downButtonRect.Contains(e.Location))
            {
                _pressDown = true;
                _direction = -1;
                StepDown();
                StartRepeatTimer();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressUp = false;
            _pressDown = false;
            _repeatTimer.Stop();
            Invalidate();
        }

        private void StartRepeatTimer()
        {
            _repeatCount = 0;
            _repeatTimer.Interval = 300;
            _repeatTimer.Start();
        }

        private void OnRepeatTimerTick(object? sender, EventArgs e)
        {
            _repeatCount++;
            if (_repeatCount > 3)
            {
                _repeatTimer.Interval = Math.Max(30, 100 - (_repeatCount * 5));
            }

            if (_direction == 1) StepUp();
            else if (_direction == -1) StepDown();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;

            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);
            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 1. Box Background & Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var brushBg = new SolidBrush(palette.Surface);
                g.FillPath(brushBg, path);

                Color borderCol = HasError ? palette.Danger : (IsEditorFocused || (_innerBox != null && _innerBox.Focused) ? palette.Primary : (IsHovered ? palette.PrimaryHover : palette.Border));
                using var penBorder = new Pen(borderCol, (IsEditorFocused || (_innerBox != null && _innerBox.Focused)) ? 1.5f : 1f);
                g.DrawPath(penBorder, path);
            }

            // 2. Up Button (▲)
            Color upBg = _pressUp ? Color.FromArgb(40, palette.Primary) : (_hoverUp ? Color.FromArgb(20, palette.Primary) : Color.Transparent);
            if (upBg != Color.Transparent)
            {
                using var brushUp = new SolidBrush(upBg);
                using var pathUp = ZeroUIConfig.CreateRoundedRectangle(_upButtonRect, 3);
                g.FillPath(brushUp, pathUp);
            }

            using (var brushUpArrow = new SolidBrush(_hoverUp ? palette.Primary : palette.TextSecondary))
            {
                int cx = _upButtonRect.X + (_upButtonRect.Width / 2);
                int cy = _upButtonRect.Y + (_upButtonRect.Height / 2);
                PointF[] pts = new[]
                {
                    new PointF(cx - 3.5f, cy + 1.5f),
                    new PointF(cx + 3.5f, cy + 1.5f),
                    new PointF(cx, cy - 2.5f)
                };
                g.FillPolygon(brushUpArrow, pts);
            }

            // 3. Down Button (▼)
            Color downBg = _pressDown ? Color.FromArgb(40, palette.Primary) : (_hoverDown ? Color.FromArgb(20, palette.Primary) : Color.Transparent);
            if (downBg != Color.Transparent)
            {
                using var brushDown = new SolidBrush(downBg);
                using var pathDown = ZeroUIConfig.CreateRoundedRectangle(_downButtonRect, 3);
                g.FillPath(brushDown, pathDown);
            }

            using (var brushDownArrow = new SolidBrush(_hoverDown ? palette.Primary : palette.TextSecondary))
            {
                int cx = _downButtonRect.X + (_downButtonRect.Width / 2);
                int cy = _downButtonRect.Y + (_downButtonRect.Height / 2);
                PointF[] pts = new[]
                {
                    new PointF(cx - 3.5f, cy - 1.5f),
                    new PointF(cx + 3.5f, cy - 1.5f),
                    new PointF(cx, cy + 2.5f)
                };
                g.FillPolygon(brushDownArrow, pts);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _repeatTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for SpinEdit.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroNumericBox is deprecated and will be removed in 5 release cycles. Please migrate to ZSpinEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroNumericBox : ZSpinEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSpinEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SpinEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSpinEdit instead.")]
    [ToolboxItem(false)]
    public class SpinEdit : ZSpinEdit
    {
    }

    #endregion
}
