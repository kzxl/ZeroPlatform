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
    /// Precision numeric and financial editor integrated with a drop-down mini-calculator,
    /// dynamic keyboard shortcuts (F4/Alt+Down, Enter, Esc, operators), and decimal/currency formatting.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("Value")]
    [Description("Precision numeric and calculation editor with popup calculator and financial formatting.")]
    public class ZCalcEdit : EditorBase<decimal>
    {
        private decimal _minValue = decimal.MinValue / 100m;
        private decimal _maxValue = decimal.MaxValue / 100m;
        private int _precision = 2;
        private string _displayFormat = "";
        private bool _showCalculatorButton = true;

        private readonly TextBox _innerBox;
        private Rectangle _calcButtonRect;
        private bool _hoverCalcButton;
        private bool _pressCalcButton;

        private DropDownHost? _popupHost;
        private CalculatorPanel? _calcPanel;

        public ZCalcEdit()
        {
            _innerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Right,
                Multiline = false
            };

            Size = new Size(160, 32);
            Value = 0m;

            _innerBox.GotFocus += (s, e) =>
            {
                _innerBox.Text = Value.ToString(CultureInfo.CurrentCulture);
                _innerBox.SelectAll();
                Invalidate();
            };

            _innerBox.LostFocus += (s, e) =>
            {
                ParseInputText();
                FormatText();
                Invalidate();
            };

            _innerBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
                {
                    ToggleCalculatorPopup();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    ParseInputText();
                    FormatText();
                    _innerBox.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _innerBox.TextChanged += (s, e) =>
            {
                if (_innerBox.Focused)
                {
                    IsModified = true;
                }
            };

            Controls.Add(_innerBox);
            FormatText();
        }

        #region Properties

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

        [Category("Data")]
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
        public decimal MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                if (Value > _maxValue) Value = _maxValue;
            }
        }

        [Category("Behavior")]
        [DefaultValue(2)]
        [Description("Decimal precision places for rounding and display.")]
        public virtual int Precision
        {
            get => _precision;
            set
            {
                _precision = Math.Max(0, Math.Min(10, value));
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Custom format string such as 'N2', 'C0', or '#,##0.00'.")]
        public string DisplayFormat
        {
            get => _displayFormat;
            set
            {
                _displayFormat = value ?? string.Empty;
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Determines whether the calculator drop-down button is visible.")]
        public bool ShowCalculatorButton
        {
            get => _showCalculatorButton;
            set
            {
                _showCalculatorButton = value;
                PerformLayoutCustom();
                Invalidate();
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
                if (_innerBox != null) _innerBox.ReadOnly = value;
                Invalidate();
            }
        }

        #endregion

        #region Layout & Rendering

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformLayoutCustom();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            var pal = CurrentPalette;
            if (_innerBox != null)
            {
                _innerBox.BackColor = pal.Surface;
                _innerBox.ForeColor = pal.TextPrimary;
                _innerBox.Font = Font;
            }
            Invalidate();
        }

        private void PerformLayoutCustom()
        {
            if (_innerBox == null) return;

            int btnWidth = _showCalculatorButton ? 28 : 0;
            _calcButtonRect = new Rectangle(Width - btnWidth - 3, 3, btnWidth, Math.Max(0, Height - 6));

            int textLeft = 8;
            int textWidth = Math.Max(10, Width - btnWidth - 14);
            int textHeight = _innerBox.PreferredHeight;
            int textTop = Math.Max(2, (Height - textHeight) / 2);

            _innerBox.SetBounds(textLeft, textTop, textWidth, textHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pal = CurrentPalette;
            bool isFocused = Focused || (_innerBox != null && _innerBox.Focused);
            Color borderColor = HasError ? pal.Danger
                : isFocused ? pal.Primary
                : IsHovered ? pal.PrimaryHover
                : pal.Border;

            // Background & Border
            using (var brush = new SolidBrush(ReadOnly ? pal.Hover : pal.Surface))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            using (var pen = new Pen(borderColor, isFocused ? 1.5f : 1f))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            // Draw Calculator Button
            if (_showCalculatorButton)
            {
                Color btnBg = _pressCalcButton ? pal.PrimaryHover
                    : _hoverCalcButton ? pal.Hover
                    : Color.Transparent;

                if (btnBg != Color.Transparent)
                {
                    using (var b = new SolidBrush(btnBg))
                    {
                        g.FillRectangle(b, _calcButtonRect);
                    }
                }

                // Draw Mini-Calculator Vector Glyph
                DrawCalculatorGlyph(g, _calcButtonRect, _hoverCalcButton ? pal.Primary : pal.TextSecondary);
            }
        }

        private void DrawCalculatorGlyph(Graphics g, Rectangle bounds, Color color)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int w = 14;
            int h = 16;
            var calcBody = new Rectangle(cx - w / 2, cy - h / 2, w, h);

            using (var pen = new Pen(color, 1.2f))
            {
                // Calculator outline
                g.DrawRectangle(pen, calcBody);

                // Screen
                var screenRect = new Rectangle(calcBody.X + 2, calcBody.Y + 2, calcBody.Width - 4, 3);
                g.DrawRectangle(pen, screenRect);

                // 4 keys
                int keyW = 3;
                int keyH = 2;
                int startX = calcBody.X + 2;
                int startY = calcBody.Y + 7;

                // Row 1 keys
                using (var b = new SolidBrush(color))
                {
                    g.FillRectangle(b, startX, startY, keyW, keyH);
                    g.FillRectangle(b, startX + 5, startY, keyW, keyH);

                    // Row 2 keys
                    g.FillRectangle(b, startX, startY + 4, keyW, keyH);
                    g.FillRectangle(b, startX + 5, startY + 4, keyW, keyH);
                }
            }
        }

        #endregion

        #region Mouse & Popup Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_showCalculatorButton)
            {
                bool wasHover = _hoverCalcButton;
                _hoverCalcButton = _calcButtonRect.Contains(e.Location);
                if (wasHover != _hoverCalcButton) Invalidate(_calcButtonRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverCalcButton)
            {
                _hoverCalcButton = false;
                Invalidate(_calcButtonRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_showCalculatorButton && _calcButtonRect.Contains(e.Location) && !ReadOnly)
            {
                _pressCalcButton = true;
                Invalidate(_calcButtonRect);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressCalcButton)
            {
                _pressCalcButton = false;
                Invalidate(_calcButtonRect);
                if (_calcButtonRect.Contains(e.Location) && !ReadOnly)
                {
                    ToggleCalculatorPopup();
                }
            }
        }

        public void ShowCalculatorPopup()
        {
            if (ReadOnly) return;

            if (_popupHost == null)
            {
                _calcPanel = new CalculatorPanel();
                _calcPanel.ResultApplied += (s, res) =>
                {
                    Value = res;
                    _popupHost?.Close();
                    _innerBox.Focus();
                    _innerBox.SelectAll();
                };
                _calcPanel.Cancelled += (s, e) =>
                {
                    _popupHost?.Close();
                    _innerBox.Focus();
                };

                _popupHost = new DropDownHost
                {
                    Content = _calcPanel
                };
            }

            _calcPanel!.SetInitialValue(Value);
            _popupHost.Show(this, 0, Height + 2);
        }

        public void CloseCalculatorPopup()
        {
            _popupHost?.Close();
        }

        public void ToggleCalculatorPopup()
        {
            if (_popupHost != null && _popupHost.Visible)
            {
                CloseCalculatorPopup();
            }
            else
            {
                ShowCalculatorPopup();
            }
        }

        #endregion

        #region Text Parsing & Formatting

        private void ParseInputText()
        {
            string raw = _innerBox.Text.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                Value = 0m;
                return;
            }

            // Remove currency symbols & spaces
            raw = raw.Replace("₫", "").Replace("$", "").Replace("€", "").Replace("£", "").Trim();

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsed) ||
                decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                Value = Math.Round(parsed, _precision);
            }
        }

        protected virtual void FormatText()
        {
            if (_innerBox == null || _innerBox.Focused) return;

            if (!string.IsNullOrEmpty(_displayFormat))
            {
                _innerBox.Text = string.Format(CultureInfo.CurrentCulture, $"{{0:{_displayFormat}}}", Value);
            }
            else
            {
                string format = _precision > 0 ? $"#,##0.{new string('0', _precision)}" : "#,##0";
                _innerBox.Text = Value.ToString(format, CultureInfo.CurrentCulture);
            }
        }

        #endregion
    }

    /// <summary>
    /// Specialized Currency editor with pre-configured Vietnamese Dong (₫) / USD currency formatting.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [Description("Currency editor with quick currency symbols, thousands separator, and embedded mini-calculator.")]
    public class CurrencyEdit : ZCalcEdit
    {
        private string _currencySymbol = "₫";
        private bool _isSymbolSuffix = true;

        public CurrencyEdit()
        {
            Precision = 0; // Default integer for VND
            DisplayFormat = "#,##0";
        }

        [Category("Appearance")]
        [DefaultValue("₫")]
        public string CurrencySymbol
        {
            get => _currencySymbol;
            set
            {
                _currencySymbol = value ?? "";
                FormatText();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool IsSymbolSuffix
        {
            get => _isSymbolSuffix;
            set
            {
                _isSymbolSuffix = value;
                FormatText();
            }
        }

        protected override void FormatText()
        {
            if (Controls.Count > 0 && Controls[0] is TextBox tb && !tb.Focused)
            {
                string baseNum = Value.ToString(string.IsNullOrEmpty(DisplayFormat) ? "#,##0" : DisplayFormat, CultureInfo.CurrentCulture);
                if (string.IsNullOrEmpty(_currencySymbol))
                {
                    tb.Text = baseNum;
                }
                else if (_isSymbolSuffix)
                {
                    tb.Text = $"{baseNum} {_currencySymbol}";
                }
                else
                {
                    tb.Text = $"{_currencySymbol} {baseNum}";
                }
            }
        }
    }

    /// <summary>
    /// The standalone visual mini-calculator popup panel for CalcEdit.
    /// </summary>
    internal class CalculatorPanel : UserControl
    {
        public event EventHandler<decimal>? ResultApplied;
        public event EventHandler? Cancelled;

        private decimal _currentNumber = 0m;
        private decimal _accumulator = 0m;
        private string _pendingOp = "";
        private bool _isNewNumber = true;
        private string _displayText = "0";

        private readonly string[,] _buttons = new string[5, 4]
        {
            { "C", "CE", "⌫", "÷" },
            { "7", "8", "9", "×" },
            { "4", "5", "6", "-" },
            { "1", "2", "3", "+" },
            { "±", "0", ".", "=" }
        };

        private int _hoverR = -1;
        private int _hoverC = -1;

        public CalculatorPanel()
        {
            DoubleBuffered = true;
            Size = new Size(220, 260);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        }

        public void SetInitialValue(decimal val)
        {
            _currentNumber = val;
            _accumulator = 0m;
            _pendingOp = "";
            _isNewNumber = true;
            _displayText = val.ToString("G29", CultureInfo.InvariantCulture);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pal = ZeroTheme.Colors;

            // Panel Background
            using (var bgBrush = new SolidBrush(pal.Surface))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // LCD Display Screen
            var displayRect = new Rectangle(8, 8, Width - 16, 42);
            using (var lcdBg = new SolidBrush(Color.FromArgb(24, 28, 36)))
            using (var lcdBorder = new Pen(pal.Border, 1f))
            {
                g.FillRectangle(lcdBg, displayRect);
                g.DrawRectangle(lcdBorder, displayRect);
            }

            // Operation history tag
            if (!string.IsNullOrEmpty(_pendingOp))
            {
                string history = $"{_accumulator:G29} {_pendingOp}";
                using (var histFont = new Font(Font.FontFamily, 8f))
                using (var histBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
                {
                    g.DrawString(history, histFont, histBrush, new RectangleF(displayRect.X + 6, displayRect.Y + 2, displayRect.Width - 12, 14));
                }
            }

            // LCD Main Number Text
            using (var numFont = new Font("Consolas", 14f, FontStyle.Bold))
            using (var numBrush = new SolidBrush(Color.FromArgb(240, 245, 255)))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                };
                var textRect = new RectangleF(displayRect.X + 4, displayRect.Y + 12, displayRect.Width - 8, displayRect.Height - 14);
                g.DrawString(_displayText, numFont, numBrush, textRect, sf);
            }

            // Keypad Grid
            int gridTop = 56;
            int pad = 4;
            int btnW = (Width - 16 - pad * 3) / 4;
            int btnH = (Height - gridTop - 8 - pad * 4) / 5;

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int x = 8 + c * (btnW + pad);
                    int y = gridTop + r * (btnH + pad);
                    var rect = new Rectangle(x, y, btnW, btnH);

                    string text = _buttons[r, c];
                    bool isHover = (r == _hoverR && c == _hoverC);
                    bool isOperator = (c == 3 || text == "=" || text == "C" || text == "CE" || text == "⌫");
                    bool isEqual = (text == "=");

                    Color bg = isEqual
                        ? (isHover ? pal.PrimaryHover : pal.Primary)
                        : isOperator
                            ? (isHover ? pal.Hover : pal.HeaderBackground)
                            : (isHover ? pal.Hover : pal.Surface);

                    Color fg = isEqual
                        ? Color.White
                        : isOperator
                            ? pal.Primary
                            : pal.TextPrimary;

                    using (var btnBrush = new SolidBrush(bg))
                    using (var borderPen = new Pen(pal.Border, 1f))
                    {
                        g.FillRectangle(btnBrush, rect);
                        g.DrawRectangle(borderPen, rect);
                    }

                    using (var textBrush = new SolidBrush(fg))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString(text, Font, textBrush, rect, sf);
                    }
                }
            }

            // Outer border
            using (var borderPen = new Pen(pal.Border, 1.5f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int gridTop = 56;
            int pad = 4;
            int btnW = (Width - 16 - pad * 3) / 4;
            int btnH = (Height - gridTop - 8 - pad * 4) / 5;

            int oldR = _hoverR;
            int oldC = _hoverC;
            _hoverR = -1;
            _hoverC = -1;

            if (e.Y >= gridTop)
            {
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        int x = 8 + c * (btnW + pad);
                        int y = gridTop + r * (btnH + pad);
                        if (new Rectangle(x, y, btnW, btnH).Contains(e.Location))
                        {
                            _hoverR = r;
                            _hoverC = c;
                            break;
                        }
                    }
                    if (_hoverR != -1) break;
                }
            }

            if (oldR != _hoverR || oldC != _hoverC) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverR = -1;
            _hoverC = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_hoverR >= 0 && _hoverC >= 0)
            {
                ProcessKeyCommand(_buttons[_hoverR, _hoverC]);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
                return true;
            }
            if (keyData == Keys.Enter)
            {
                ProcessKeyCommand("=");
                return true;
            }
            if (keyData == Keys.Back)
            {
                ProcessKeyCommand("⌫");
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        public void ProcessKeyCommand(string cmd)
        {
            switch (cmd)
            {
                case "0":
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9":
                    if (_isNewNumber || _displayText == "0")
                    {
                        _displayText = cmd;
                        _isNewNumber = false;
                    }
                    else
                    {
                        if (_displayText.Length < 16) _displayText += cmd;
                    }
                    break;

                case ".":
                    if (_isNewNumber)
                    {
                        _displayText = "0.";
                        _isNewNumber = false;
                    }
                    else if (!_displayText.Contains("."))
                    {
                        _displayText += ".";
                    }
                    break;

                case "±":
                    if (_displayText != "0" && decimal.TryParse(_displayText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal neg))
                    {
                        _displayText = (-neg).ToString("G29", CultureInfo.InvariantCulture);
                    }
                    break;

                case "⌫":
                    if (!_isNewNumber && _displayText.Length > 0)
                    {
                        _displayText = _displayText.Substring(0, _displayText.Length - 1);
                        if (_displayText.Length == 0 || _displayText == "-") _displayText = "0";
                    }
                    break;

                case "CE":
                    _displayText = "0";
                    _isNewNumber = true;
                    break;

                case "C":
                    _displayText = "0";
                    _accumulator = 0m;
                    _pendingOp = "";
                    _isNewNumber = true;
                    break;

                case "+":
                case "-":
                case "×":
                case "÷":
                    ExecutePendingOperation();
                    _pendingOp = cmd;
                    _isNewNumber = true;
                    break;

                case "=":
                    ExecutePendingOperation();
                    _pendingOp = "";
                    _isNewNumber = true;
                    if (decimal.TryParse(_displayText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal finalVal))
                    {
                        ResultApplied?.Invoke(this, finalVal);
                    }
                    break;
            }

            Invalidate();
        }

        private void ExecutePendingOperation()
        {
            if (decimal.TryParse(_displayText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal currentVal))
            {
                if (string.IsNullOrEmpty(_pendingOp))
                {
                    _accumulator = currentVal;
                }
                else
                {
                    switch (_pendingOp)
                    {
                        case "+": _accumulator += currentVal; break;
                        case "-": _accumulator -= currentVal; break;
                        case "×": _accumulator *= currentVal; break;
                        case "÷":
                            if (currentVal != 0) _accumulator /= currentVal;
                            break;
                    }
                    _displayText = _accumulator.ToString("G29", CultureInfo.InvariantCulture);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCalcEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CalcEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZCalcEdit instead.")]
    [ToolboxItem(false)]
    public class CalcEdit : ZCalcEdit
    {
    }

    #endregion
}
