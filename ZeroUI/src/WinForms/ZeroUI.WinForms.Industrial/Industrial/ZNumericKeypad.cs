using System;


using System.ComponentModel;using ZeroUI.WinForms.Icons;using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial touch-screen on-screen numeric keypad popup dialog.
    /// Provides large touch targets, Min/Max boundary checks, and engineering unit display.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroNumericKeypad.bmp")]
    public class ZNumericKeypad : Form
    {
        private readonly Label _lblTitle;
        private readonly Label _lblDisplay;
        private readonly Label _lblLimits;
        private string _currentBuffer = "0";
        private bool _isNewEntry = true;

        public double ResultValue { get; private set; }
        public double MinLimit { get; set; } = double.MinValue;
        public double MaxLimit { get; set; } = double.MaxValue;
        public string EngineeringUnit { get; set; } = "";

        public ZNumericKeypad(string title, double initialValue, double minLimit = 0, double maxLimit = 100, string unit = "")
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Size = new Size(280, 420);
            Text = title;

            MinLimit = minLimit;
            MaxLimit = maxLimit;
            EngineeringUnit = unit;
            ResultValue = initialValue;
            _currentBuffer = initialValue.ToString("0.##");

            bool isDark = ZeroTheme.IsDark;
            BackColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
            ForeColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(12, 10),
                Size = new Size(240, 20),
                ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139)
            };
            Controls.Add(_lblTitle);

            _lblDisplay = new Label
            {
                Text = $"{_currentBuffer} {EngineeringUnit}".Trim(),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(12, 34),
                Size = new Size(240, 44),
                BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.White,
                ForeColor = isDark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_lblDisplay);

            _lblLimits = new Label
            {
                Text = $"Range: [{MinLimit:0.##} - {MaxLimit:0.##}] {EngineeringUnit}",
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                Location = new Point(12, 82),
                Size = new Size(240, 16),
                ForeColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184)
            };
            Controls.Add(_lblLimits);

            BuildKeypadButtons(isDark);
        }

        private void BuildKeypadButtons(bool isDark)
        {
            string[,] layout = {
                { "7", "8", "9", "⌫" },
                { "4", "5", "6", "CLR" },
                { "1", "2", "3", "±" },
                { "0", ".", "ESC", "OK" }
            };

            int startX = 12;
            int startY = 104;
            int btnW = 56;
            int btnH = 65;
            int gap = 5;

            Color btnBg = isDark ? Color.FromArgb(30, 41, 59) : Color.White;
            Color actionBg = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Color enterBg = Color.FromArgb(22, 163, 74); // Green

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    string key = layout[r, c];
                    var btn = new Button
                    {
                        Text = key,
                        Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                        Location = new Point(startX + c * (btnW + gap), startY + r * (btnH + gap)),
                        Size = new Size(btnW, btnH),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = key == "OK" ? enterBg : (key == "⌫" || key == "CLR" || key == "ESC" || key == "±" ? actionBg : btnBg),
                        ForeColor = key == "OK" ? Color.White : (isDark ? Color.White : Color.FromArgb(15, 23, 42))
                    };
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);

                    string capturedKey = key;
                    btn.Click += (s, e) => OnKeyClicked(capturedKey);
                    Controls.Add(btn);
                }
            }
        }

        private void OnKeyClicked(string key)
        {
            switch (key)
            {
                case "OK":
                    if (double.TryParse(_currentBuffer, out var val))
                    {
                        if (val < MinLimit || val > MaxLimit)
                        {
                            MessageBox.Show($"Value {val} is outside allowed range [{MinLimit} - {MaxLimit}].", "Range Limit Violation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        ResultValue = val;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    break;
                case "ESC":
                    DialogResult = DialogResult.Cancel;
                    Close();
                    break;
                case "CLR":
                    _currentBuffer = "0";
                    _isNewEntry = true;
                    break;
                case "⌫":
                    if (_currentBuffer.Length > 1)
                        _currentBuffer = _currentBuffer.Substring(0, _currentBuffer.Length - 1);
                    else
                        _currentBuffer = "0";
                    break;
                case "±":
                    if (_currentBuffer.StartsWith("-"))
                        _currentBuffer = _currentBuffer.Substring(1);
                    else if (_currentBuffer != "0")
                        _currentBuffer = "-" + _currentBuffer;
                    break;
                case ".":
                    if (_isNewEntry)
                    {
                        _currentBuffer = "0.";
                        _isNewEntry = false;
                    }
                    else if (!_currentBuffer.Contains("."))
                    {
                        _currentBuffer += ".";
                    }
                    break;
                default: // Digits 0-9
                    if (_isNewEntry || _currentBuffer == "0")
                    {
                        _currentBuffer = key;
                        _isNewEntry = false;
                    }
                    else
                    {
                        _currentBuffer += key;
                    }
                    break;
            }

            _lblDisplay.Text = $"{_currentBuffer} {EngineeringUnit}".Trim();
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZNumericKeypad"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroNumericKeypad is deprecated and will be removed in 5 release cycles. Please migrate to ZNumericKeypad instead.")]
    [ToolboxItem(false)]
    public class ZeroNumericKeypad : ZNumericKeypad
    {
        public ZeroNumericKeypad(string title, double initialValue, double minLimit = 0, double maxLimit = 100, string unit = "")
            : base(title, initialValue, minLimit, maxLimit, unit)
        {
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZNumericKeypad"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("NumericKeypad is deprecated and will be removed in 5 release cycles. Please migrate to ZNumericKeypad instead.")]
    [ToolboxItem(false)]
    public class NumericKeypad : ZNumericKeypad
    {
        public NumericKeypad(string title, double initialValue, double minLimit = 0, double maxLimit = 100, string unit = "")
            : base(title, initialValue, minLimit, maxLimit, unit) { }
    }

    #endregion
}
