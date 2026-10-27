using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public class BarcodeScannedEventArgs : EventArgs

    {
        public string Barcode { get; }
        public DateTime Timestamp { get; }

        public BarcodeScannedEventArgs(string barcode)
        {
            Barcode = barcode;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Specialized high-speed barcode and QR code scanner input control for factory workstations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("BarcodeScanned")]
    [Description("Hardware barcode and QR code scanner input control for factory workstations")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroBarcodeBox.bmp")]
    public class ZBarcodeScannerBox : Control
    {

        private readonly TextBox _textBox;
        private string _placeholder = "Scan or type barcode...";
        private bool _clearOnSubmit = true;
        private bool _selectAllOnFocus = true;
        private bool _isFocused = false;
        private int _flashTicks = 0;
        private readonly Timer _flashTimer;

        public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;

        public ZBarcodeScannerBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(280, 36);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                if (_textBox != null) _textBox.Font = Font;
                Invalidate();
            };

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                Location = new Point(34, 9),
                Width = Width - 44,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.Enter += (s, e) =>
            {
                _isFocused = true;
                if (_selectAllOnFocus) _textBox.SelectAll();
                Invalidate();
            };
            _textBox.Leave += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };

            _flashTimer = new Timer { Interval = 50 };
            _flashTimer.Tick += (s, e) =>
            {
                _flashTicks--;
                if (_flashTicks <= 0)
                {
                    _flashTimer.Stop();
                }
                Invalidate();
            };

            Controls.Add(_textBox);
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ClearOnSubmit
        {
            get => _clearOnSubmit;
            set => _clearOnSubmit = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool SelectAllOnFocus
        {
            get => _selectAllOnFocus;
            set => _selectAllOnFocus = value;
        }

        [Category("Appearance")]
        [DefaultValue("Scan or type barcode...")]
        public string PlaceholderText
        {
            get => _placeholder;
            set { _placeholder = value; Invalidate(); }
        }

        public string BarcodeText
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        public void FocusInput()
        {
            _textBox.Focus();
            _textBox.SelectAll();
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                string code = _textBox.Text.Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    // Trigger visual green pulse
                    _flashTicks = 6;
                    _flashTimer.Start();

                    BarcodeScanned?.Invoke(this, new BarcodeScannedEventArgs(code));

                    if (_clearOnSubmit)
                    {
                        _textBox.Clear();
                    }
                    else
                    {
                        _textBox.SelectAll();
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // 1. Fill parent background to eliminate black corner clipping artifacts
            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);
            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 2. Determine Border & Fill Color based on focus and flash
            Color borderColor;
            Color bgColor = palette.Surface;

            if (_flashTicks > 0)
            {
                borderColor = Color.FromArgb(16, 185, 129); // Flash Emerald
                bgColor = Color.FromArgb(246, 255, 237);
            }
            else if (_isFocused)
            {
                borderColor = palette.Primary;
            }
            else
            {
                borderColor = palette.Border;
            }

            _textBox.BackColor = bgColor;
            _textBox.ForeColor = palette.TextPrimary;

            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var bgBrush = new SolidBrush(bgColor);
                g.FillPath(bgBrush, path);

                using var pen = new Pen(borderColor, _isFocused || _flashTicks > 0 ? 1.8f : 1f);
                g.DrawPath(pen, path);
            }

            // 2. Draw Barcode Icon glyph (||||)
            Rectangle iconRect = new Rectangle(10, (Height - 16) / 2, 18, 16);
            TextRenderer.DrawText(g, "❚❙❘", ZeroFontCache.Get("Segoe UI", 10f, FontStyle.Bold), iconRect, _isFocused ? Color.FromArgb(79, 70, 229) : Color.FromArgb(156, 163, 175), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 3. Draw Placeholder when empty
            if (string.IsNullOrEmpty(_textBox.Text) && !_isFocused)
            {
                Rectangle phRect = new Rectangle(36, 0, Width - 44, Height);
                TextRenderer.DrawText(g, _placeholder, Font, phRect, Color.FromArgb(156, 163, 175), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _textBox.Focus();
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _flashTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeScannerBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BarcodeScannerBox is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeScannerBox instead.")]
    [ToolboxItem(false)]
    public class BarcodeScannerBox : ZBarcodeScannerBox
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeScannerBox"/>.
    /// </summary>
    [Obsolete("BarcodeBox in ZeroUI.WinForms.Industrial is deprecated. Use ZBarcodeScannerBox instead.")]
    [ToolboxItem(false)]
    public class BarcodeBox : ZBarcodeScannerBox
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeScannerBox"/>.
    /// </summary>
    [Obsolete("ZeroBarcodeBox is deprecated. Please use ZBarcodeScannerBox instead.")]
    [ToolboxItem(false)]
    public class ZeroBarcodeBox : ZBarcodeScannerBox
    {
    }

    #endregion



}
