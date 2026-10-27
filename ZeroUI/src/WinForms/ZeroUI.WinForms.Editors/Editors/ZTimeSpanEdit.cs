using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern segmented duration and cycle-time editor for WinForms.
    /// Supports segment-based arrow stepping, up/down spinner buttons, mouse wheel,
    /// and dark theme palette compliance.
    /// </summary>
    [DefaultEvent(nameof(ValueChanged))]
    public class ZTimeSpanEdit : Control, IZeroEditor
    {
        private readonly TimeSpanModel _model = new TimeSpanModel();
        private bool _isHovered;
        private bool _isFocused;
        private bool _readOnly;
        private bool _isModified;
        private Rectangle _upButtonRect;
        private Rectangle _downButtonRect;
        private bool _isUpHovered;
        private bool _isDownHovered;

        [Category("Data")]
        public TimeSpan Value
        {
            get => _model.Value;
            set
            {
                if (_model.Value != value)
                {
                    _model.Value = value;
                    _isModified = true;
                    ValueChanged?.Invoke(this, _model.Value);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        public TimeSpan Minimum
        {
            get => _model.Minimum;
            set
            {
                _model.Minimum = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        public TimeSpan Maximum
        {
            get => _model.Maximum;
            set
            {
                _model.Maximum = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(TimeSpanFormatMode.Clock)]
        public TimeSpanFormatMode FormatMode
        {
            get => _model.FormatMode;
            set
            {
                _model.FormatMode = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowDays
        {
            get => _model.ShowDays;
            set
            {
                _model.ShowDays = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ShowMilliseconds
        {
            get => _model.ShowMilliseconds;
            set
            {
                _model.ShowMilliseconds = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                Invalidate();
            }
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
            get => Value;
            set
            {
                if (value is TimeSpan ts)
                {
                    Value = ts;
                }
                else if (value is string s && TimeSpanModel.TryParse(s, out var parsed))
                {
                    Value = parsed;
                }
            }
        }

        public TimeSpanModel Model => _model;

        public event EventHandler<TimeSpan>? ValueChanged;
        public event EventHandler? EditValueChanged;

        public ZTimeSpanEdit()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            Height = 32;
            Width = 160;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _model.ValueChanged += (s, e) =>
            {
                _isModified = true;
                ValueChanged?.Invoke(this, _model.Value);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };

            _model.PartChanged += (s, e) => Invalidate();
        }

        public void Reset()
        {
            Value = TimeSpan.Zero;
            _isModified = false;
        }

        public void Clear() => Reset();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_readOnly) return;

            if (e.KeyCode == Keys.Up)
            {
                _model.StepUp();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                _model.StepDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                _model.NextPart();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                _model.PreviousPart();
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_readOnly) return;

            if (e.Delta > 0)
                _model.StepUp();
            else if (e.Delta < 0)
                _model.StepDown();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isUpHovered = false;
            _isDownHovered = false;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool upHover = _upButtonRect.Contains(e.Location);
            bool downHover = _downButtonRect.Contains(e.Location);

            if (upHover != _isUpHovered || downHover != _isDownHovered)
            {
                _isUpHovered = upHover;
                _isDownHovered = downHover;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_readOnly) return;

            if (_upButtonRect.Contains(e.Location))
            {
                _model.StepUp();
            }
            else if (_downButtonRect.Contains(e.Location))
            {
                _model.StepDown();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Container Background & Border
            using (var bgBrush = new SolidBrush(ZeroTheme.Colors.BgInput))
            using (var path = CreateRoundedRectanglePath(bounds, 5))
            {
                g.FillPath(bgBrush, path);
            }

            Color borderColor = _isFocused ? ZeroTheme.Colors.PrimaryAccent : (_isHovered ? ZeroTheme.Colors.PrimaryAccentDark : ZeroTheme.Colors.BorderDefault);
            using (var borderPen = new Pen(borderColor, 1f))
            using (var path = CreateRoundedRectanglePath(bounds, 5))
            {
                g.DrawPath(borderPen, path);
            }

            // Up / Down spin buttons on right
            int btnWidth = 20;
            int btnH = (Height - 2) / 2;
            _upButtonRect = new Rectangle(Width - btnWidth - 1, 1, btnWidth, btnH);
            _downButtonRect = new Rectangle(Width - btnWidth - 1, 1 + btnH, btnWidth, btnH);

            // Button divider line
            using (var divPen = new Pen(ZeroTheme.Colors.BorderDefault, 1f))
            {
                g.DrawLine(divPen, Width - btnWidth - 1, 2, Width - btnWidth - 1, Height - 3);
            }

            // Button hover fills
            if (_isUpHovered)
            {
                using var hBrush = new SolidBrush(Color.FromArgb(40, ZeroTheme.Colors.PrimaryAccent));
                g.FillRectangle(hBrush, _upButtonRect);
            }
            if (_isDownHovered)
            {
                using var hBrush = new SolidBrush(Color.FromArgb(40, ZeroTheme.Colors.PrimaryAccent));
                g.FillRectangle(hBrush, _downButtonRect);
            }

            // Up arrow glyph
            using (var arrowPen = new Pen(ZeroTheme.Colors.TextSecondary, 1.2f))
            {
                int upMidX = _upButtonRect.X + _upButtonRect.Width / 2;
                int upMidY = _upButtonRect.Y + _upButtonRect.Height / 2;
                g.DrawLine(arrowPen, upMidX - 3, upMidY + 1, upMidX, upMidY - 2);
                g.DrawLine(arrowPen, upMidX, upMidY - 2, upMidX + 3, upMidY + 1);

                // Down arrow glyph
                int downMidX = _downButtonRect.X + _downButtonRect.Width / 2;
                int downMidY = _downButtonRect.Y + _downButtonRect.Height / 2;
                g.DrawLine(arrowPen, downMidX - 3, downMidY - 1, downMidX, downMidY + 2);
                g.DrawLine(arrowPen, downMidX, downMidY + 2, downMidX + 3, downMidY - 1);
            }

            // Formatted Text
            string text = _model.GetFormattedText();
            using (var font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular))
            using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near
                };
                var textRect = new Rectangle(10, 0, Width - btnWidth - 14, Height);
                g.DrawString(text, font, brush, textRect, sf);
            }

            // Active underline if focused
            if (_isFocused)
            {
                using var focusPen = new Pen(ZeroTheme.Colors.PrimaryAccent, 2f);
                g.DrawLine(focusPen, 10, Height - 3, Width - btnWidth - 6, Height - 3);
            }
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
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTimeSpanEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TimeSpanEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeSpanEdit instead.")]
    [ToolboxItem(false)]
    public class TimeSpanEdit : ZTimeSpanEdit
    {
    }

    #endregion
}
