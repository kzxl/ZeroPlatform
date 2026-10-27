using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern search input with placeholder text, clear button, and debounced text change events.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("DebouncedTextChanged")]
    [DefaultProperty("PlaceholderText")]
    [Description("Modern search box with debounced input and clear button")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSearchBox.bmp")]
    public class ZSearchControl : ControlBase
    {
        private readonly TextBox _textBox;
        private readonly Timer _debounceTimer;
        private string _placeholder = "🔍 Search...";
        private int _debounceMs = 200;

        private bool _isFocused = false;

        public event EventHandler<string>? DebouncedTextChanged;

        public ZSearchControl()
        {
            Size = new Size(240, 34);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                Location = new Point(12, (Height - 18) / 2),
                Width = Width - 36,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };

            _textBox.TextChanged += TextBox_TextChanged;
            _textBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _textBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _textBox.KeyDown += TextBox_KeyDown;

            Controls.Add(_textBox);

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _textBox.Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };

            _debounceTimer = new Timer { Interval = _debounceMs };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                DebouncedTextChanged?.Invoke(this, _textBox.Text);
            };

            UpdateTheme();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            if (_textBox == null) return;
            var palette = CurrentPalette;
            _textBox.BackColor = palette.Surface;
            _textBox.ForeColor = palette.TextPrimary;
            Invalidate();
        }

        [Category("Appearance")]
        public string PlaceholderText
        {
            get => _placeholder;
            set { _placeholder = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(200)]
        public int DebounceIntervalMs
        {
            get => _debounceMs;
            set { _debounceMs = Math.Max(50, value); if (_debounceTimer != null) _debounceTimer.Interval = _debounceMs; }
        }

        [Browsable(false)]
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.AllowNull]
#endif
        public override string Text
        {
            get => _textBox?.Text ?? string.Empty;
            set { if (_textBox != null) _textBox.Text = value ?? string.Empty; }
        }





        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
            Invalidate();
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && !string.IsNullOrEmpty(_textBox.Text))
            {
                _textBox.Clear();
                e.Handled = true;
            }
        }

        private int _baseHeight = 34;

        protected override void OnApplyDpiScaling(float scaleFactor, float factorRatio)
        {
            base.OnApplyDpiScaling(scaleFactor, factorRatio);
            Height = Math.Max(24, (int)Math.Round(_baseHeight * scaleFactor));
            UpdateTextBoxBounds();
        }

        private void UpdateTextBoxBounds()
        {
            if (_textBox != null)
            {
                int leftPad = (int)Math.Round(10 * DpiScale);
                int clearBtnW = (int)Math.Round(28 * DpiScale);
                _textBox.Location = new Point(leftPad, (Height - _textBox.PreferredHeight) / 2);
                _textBox.Width = Math.Max(10, Width - leftPad - clearBtnW);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateTextBoxBounds();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            int clearBtnW = (int)Math.Round(28 * DpiScale);
            // Click Clear Button
            if (!string.IsNullOrEmpty(_textBox.Text) && e.X >= Width - clearBtnW && e.X <= Width - (int)Math.Round(6 * DpiScale))
            {
                _textBox.Clear();
                _textBox.Focus();
            }
            else
            {
                _textBox.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            _textBox.BackColor = palette.Surface;
            _textBox.ForeColor = palette.TextPrimary;

            // 1. Fill parent background to eliminate black corner clipping artifacts
            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);
            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 2. Background and Border
            Color borderColor = _isFocused ? palette.Primary : palette.Border;
            float borderWidth = 1f;

            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var bgBrush = new SolidBrush(palette.Surface);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(borderColor, borderWidth);
                g.DrawPath(borderPen, path);
            }

            // 3. Placeholder Text (when empty and not typing)
            if (string.IsNullOrEmpty(_textBox.Text) && !_isFocused)
            {
                Rectangle phRect = new Rectangle(12, 0, Width - 36, Height);
                TextRenderer.DrawText(
                    g,
                    _placeholder,
                    Font,
                    phRect,
                    palette.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // 4. Clear Button (✕) when text exists
            if (!string.IsNullOrEmpty(_textBox.Text))
            {
                Rectangle clearRect = new Rectangle(Width - 28, (Height - 18) / 2, 18, 18);
                using var clearBg = new SolidBrush(palette.Hover);
                g.FillEllipse(clearBg, clearRect);

                TextRenderer.DrawText(
                    g,
                    "✕",
                    ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold),
                    clearRect,
                    palette.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounceTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZSearchControl"/>.
    /// </summary>
    [Obsolete("ZeroSearchBox is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchControl instead.")]
    [ToolboxItem(false)]
    public class ZeroSearchBox : ZSearchControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSearchControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SearchControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchControl instead.")]
    [ToolboxItem(false)]
    public class SearchControl : ZSearchControl
    {
    }

    #endregion
}
