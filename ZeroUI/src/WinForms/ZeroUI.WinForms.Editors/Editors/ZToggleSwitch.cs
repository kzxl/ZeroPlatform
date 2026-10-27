using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Smooth animated toggle switch control for ZeroUI with keyboard interaction and custom state labels.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSwitch.bmp")]
    [DefaultProperty("Checked")]
    [DefaultEvent("CheckedChanged")]
    [Category("ZeroUI - Editors")]
    [Description("Smooth sliding animated toggle switch")]
    public class ZToggleSwitch : ControlBase, IZeroEditor
    {
        private bool _checked = false;
        private string? _checkedText = "ON";
        private string? _uncheckedText = "OFF";

        private Color _checkedColor = Color.FromArgb(79, 70, 229);     // ZeroUI Indigo Accent
        private bool _customCheckedColor = false;
        private Color _uncheckedColor = Color.FromArgb(203, 213, 225); // Slate 300
        private bool _customUncheckedColor = false;
        private Color _thumbColor = Color.White;
        private IDisposable? _animSub;

        private float _thumbPosition = 0f; // 0.0 (left) to 1.0 (right)
        private bool _isModified = false;
        private bool _readOnly = false;

        public event EventHandler? CheckedChanged;
        public event EventHandler? EditValueChanged;

        [Browsable(false)]
        public object? EditValue
        {
            get => _checked;
            set
            {
                if (value is bool b)
                {
                    Checked = b;
                }
                else if (bool.TryParse(value?.ToString(), out bool parsed))
                {
                    Checked = parsed;
                }
            }
        }

        [Browsable(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set => _readOnly = value;
        }

        public void Reset()
        {
            Checked = false;
            _isModified = false;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear() => Reset();

        public ZToggleSwitch()
        {
            SetStyle(ControlStyles.Selectable, true);

            Size = new Size(52, 26);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    _isModified = true;
                    if (!ZeroDesignHelper.IsInDesignMode(this) && IsHandleCreated)
                    {
                        _animSub ??= ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                    }
                    else
                    {
                        _thumbPosition = value ? 1f : 0f;
                        Invalidate();
                    }
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }


        [Category("Appearance")]
        [DefaultValue("ON")]
        public string? CheckedText
        {
            get => _checkedText;
            set { _checkedText = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("OFF")]
        public string? UncheckedText
        {
            get => _uncheckedText;
            set { _uncheckedText = value; Invalidate(); }
        }


        [Category("Appearance")]
        public Color CheckedColor
        {
            get => _customCheckedColor ? _checkedColor : CurrentPalette.Primary;
            set { _checkedColor = value; _customCheckedColor = true; Invalidate(); }
        }

        [Category("Appearance")]
        public Color UncheckedColor
        {
            get => _customUncheckedColor ? _uncheckedColor : (EffectiveSkin.IsDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(203, 213, 225));
            set { _uncheckedColor = value; _customUncheckedColor = true; Invalidate(); }
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            float target = _checked ? 1.0f : 0.0f;
            float step = (float)(deltaSeconds * 10.0); // Smooth ~100ms transition

            if (Math.Abs(_thumbPosition - target) <= step)
            {
                _thumbPosition = target;
                _animSub?.Dispose();
                _animSub = null;
            }
            else
            {
                _thumbPosition += (_thumbPosition < target) ? step : -step;
            }

            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_readOnly || !Enabled) return;
            Focus();
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && !_readOnly && Enabled)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle trackRect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height / 2;

            // 1. Interpolate Track Color
            Color currentTrackColor = InterpolateColor(UncheckedColor, CheckedColor, _thumbPosition);

            using (var trackPath = CreatePillPath(trackRect, radius))
            {
                using var trackBrush = new SolidBrush(currentTrackColor);
                g.FillPath(trackBrush, trackPath);

                if (Focused)
                {
                    using var focusPen = new Pen(Color.FromArgb(145, 202, 255), 2f);
                    g.DrawPath(focusPen, trackPath);
                }
            }

            // 2. Draw Inside Text (Optional State Label)
            if (_thumbPosition > 0.5f && !string.IsNullOrEmpty(_checkedText))

            {
                Rectangle textRect = new Rectangle(6, 0, Width - Height, Height);
                TextRenderer.DrawText(
                    g,
                    _checkedText,
                    Font,
                    textRect,
                    Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else if (_thumbPosition <= 0.5f && !string.IsNullOrEmpty(_uncheckedText))
            {
                Rectangle textRect = new Rectangle(Height, 0, Width - Height - 6, Height);
                TextRenderer.DrawText(
                    g,
                    _uncheckedText,
                    Font,
                    textRect,
                    Color.FromArgb(220, 220, 220),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // 3. Draw Thumb (Circle)
            int thumbDiameter = Height - 6;
            int minX = 3;
            int maxX = Width - thumbDiameter - 3;
            int thumbX = (int)(minX + (_thumbPosition * (maxX - minX)));
            int thumbY = 3;

            Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbDiameter, thumbDiameter);
            using var thumbBrush = new SolidBrush(_thumbColor);
            g.FillEllipse(thumbBrush, thumbRect);
        }

        private static Color InterpolateColor(Color c1, Color c2, float t)
        {
            int r = (int)(c1.R + ((c2.R - c1.R) * t));
            int g = (int)(c1.G + ((c2.G - c1.G) * t));
            int b = (int)(c1.B + ((c2.B - c1.B) * t));
            return Color.FromArgb(r, g, b);
        }

        private static GraphicsPath CreatePillPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 90, 180);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 180);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animSub?.Dispose();
                _animSub = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZToggleSwitch"/>.
    /// </summary>
    [Obsolete("ZeroSwitch is deprecated and will be removed in 5 release cycles. Please migrate to ZToggleSwitch instead.")]
    [ToolboxItem(false)]
    public class ZeroSwitch : ZToggleSwitch
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZToggleSwitch"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ToggleSwitch is deprecated and will be removed in 5 release cycles. Please migrate to ZToggleSwitch instead.")]
    [ToolboxItem(false)]
    public class ToggleSwitch : ZToggleSwitch
    {
    }

    #endregion
}
