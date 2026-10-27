using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Visual Cron expression builder with preset templates, 5-field manual editors,
    /// and real-time human-readable schedule preview.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [Description("Visual Cron expression builder with presets, field inputs, and natural language translation")]
    public class ZCronEditor : Control
    {
        private string _cronExpression = "0 8 * * 1-5";
        private bool _showPreview = true;

        private ComboBox _presetCombo;
        private TextBox _minuteBox;
        private TextBox _hourBox;
        private TextBox _dayBox;
        private TextBox _monthBox;
        private TextBox _dowBox;
        private TextBox _expressionBox;

        public event EventHandler? CronChanged;
        public event EventHandler? ExpressionChanged { add => CronChanged += value; remove => CronChanged -= value; }

        [Category("Data")]
        [DefaultValue("0 8 * * 1-5")]
        [Description("The standard 5-part cron expression (minute hour day month day-of-week).")]
        public string CronExpression
        {
            get => _cronExpression;
            set
            {
                if (_cronExpression != value)
                {
                    _cronExpression = value ?? "* * * * *";
                    ParseToFields(_cronExpression);
                    CronChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowPreview
        {
            get => _showPreview;
            set { _showPreview = value; Invalidate(); }
        }

        public ZCronEditor()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(460, 150);
            BackColor = Color.Transparent;

            // 1. Preset Selector
            _presetCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular)
            };
            _presetCombo.Items.AddRange(new object[]
            {
                "Custom Schedule",
                "Every Minute (* * * * *)",
                "Every 5 Minutes (*/5 * * * *)",
                "Every 15 Minutes (*/15 * * * *)",
                "Hourly at :00 (0 * * * *)",
                "Daily at 08:00 AM (0 8 * * *)",
                "Daily at Midnight (0 0 * * *)",
                "Weekdays at 08:00 AM (0 8 * * 1-5)",
                "Weekly on Sunday (0 0 * * 0)",
                "Monthly on 1st (0 0 1 * *)"
            });
            _presetCombo.SelectedIndex = 7; // Weekdays at 08:00 AM
            _presetCombo.SelectedIndexChanged += OnPresetChanged;
            Controls.Add(_presetCombo);

            // 2. Field Boxes
            _minuteBox = CreateFieldBox("0");
            _hourBox = CreateFieldBox("8");
            _dayBox = CreateFieldBox("*");
            _monthBox = CreateFieldBox("*");
            _dowBox = CreateFieldBox("1-5");

            Controls.Add(_minuteBox);
            Controls.Add(_hourBox);
            Controls.Add(_dayBox);
            Controls.Add(_monthBox);
            Controls.Add(_dowBox);

            // 3. Expression Preview Box
            _expressionBox = new TextBox
            {
                ReadOnly = true,
                Text = _cronExpression,
                Font = ZeroFontCache.Get("Consolas", 9.5f, FontStyle.Bold)
            };
            Controls.Add(_expressionBox);

            UpdateLayout();
            ZeroTheme.ThemeChanged += (s, e) => Invalidate();
        }

        private TextBox CreateFieldBox(string defaultValue)
        {
            var tb = new TextBox
            {
                Text = defaultValue,
                TextAlign = HorizontalAlignment.Center,
                Font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular)
            };
            tb.TextChanged += (s, e) => RebuildExpressionFromFields();
            return tb;
        }

        private void OnPresetChanged(object? sender, EventArgs e)
        {
            string preset = _presetCombo.SelectedItem?.ToString() ?? string.Empty;
            if (preset.Contains("(* * * * *)")) CronExpression = "* * * * *";
            else if (preset.Contains("(*/5 * * * *)")) CronExpression = "*/5 * * * *";
            else if (preset.Contains("(*/15 * * * *)")) CronExpression = "*/15 * * * *";
            else if (preset.Contains("(0 * * * *)")) CronExpression = "0 * * * *";
            else if (preset.Contains("(0 8 * * *)")) CronExpression = "0 8 * * *";
            else if (preset.Contains("(0 0 * * *)")) CronExpression = "0 0 * * *";
            else if (preset.Contains("(0 8 * * 1-5)")) CronExpression = "0 8 * * 1-5";
            else if (preset.Contains("(0 0 * * 0)")) CronExpression = "0 0 * * 0";
            else if (preset.Contains("(0 0 1 * *)")) CronExpression = "0 0 1 * *";
        }

        private void ParseToFields(string expr)
        {
            var parts = expr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                _minuteBox.Text = parts[0];
                _hourBox.Text = parts[1];
                _dayBox.Text = parts[2];
                _monthBox.Text = parts[3];
                _dowBox.Text = parts[4];
            }
            if (_expressionBox != null) _expressionBox.Text = expr;
        }

        private void RebuildExpressionFromFields()
        {
            string expr = $"{_minuteBox.Text} {_hourBox.Text} {_dayBox.Text} {_monthBox.Text} {_dowBox.Text}";
            if (_cronExpression != expr)
            {
                _cronExpression = expr;
                if (_expressionBox != null) _expressionBox.Text = expr;
                CronChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (_presetCombo == null || _expressionBox == null || _dowBox == null) return;
            int margin = 10;
            int y = 8;

            // Row 1: Preset dropdown
            _presetCombo.SetBounds(margin + 60, y, Width - margin * 2 - 60, 24);
            y += 34;

            // Row 2: 5 fields (Minute, Hour, Day, Month, Day of week)
            int fieldW = (Width - margin * 2 - 20) / 5;
            _minuteBox.SetBounds(margin + 0 * (fieldW + 5), y + 16, fieldW, 22);
            _hourBox.SetBounds(margin + 1 * (fieldW + 5), y + 16, fieldW, 22);
            _dayBox.SetBounds(margin + 2 * (fieldW + 5), y + 16, fieldW, 22);
            _monthBox.SetBounds(margin + 3 * (fieldW + 5), y + 16, fieldW, 22);
            _dowBox.SetBounds(margin + 4 * (fieldW + 5), y + 16, fieldW, 22);
            y += 48;

            // Row 3: Preview Box
            _expressionBox.SetBounds(margin + 60, y, 160, 22);
        }

        public string ToHumanReadable() => DescribeCron(_cronExpression);

        public static string DescribeCron(string cron)
        {
            string c = (cron ?? string.Empty).Trim();
            if (c == "* * * * *") return "Every minute";
            if (c == "*/5 * * * *") return "Every 5 minutes";
            if (c == "*/15 * * * *") return "Every 15 minutes";
            if (c == "0 * * * *") return "Every hour on the hour";
            if (c == "0 0 * * *") return "Every day at midnight (00:00)";
            if (c == "0 8 * * *") return "Every day at 08:00 AM";
            if (c == "0 8 * * 1-5") return "Monday through Friday at 08:00 AM";
            if (c == "0 0 * * 0") return "Every Sunday at midnight";
            if (c == "0 0 1 * *") return "First day of every month at midnight";

            return $"Custom schedule ({c})";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var colors = ZeroTheme.Colors;
            var font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular);
            var boldFont = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold);
            using var textBrush = new SolidBrush(colors.TextPrimary);
            using var labelBrush = new SolidBrush(colors.TextSecondary);
            using var borderPen = new Pen(colors.BorderDefault, 1f);

            // Outer border
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            // Row 1 Label: Preset
            g.DrawString("Preset:", boldFont, labelBrush, 10, 10);

            // Row 2 Labels: Min, Hour, Day, Month, DOW
            int margin = 10;
            int fieldW = (Width - margin * 2 - 20) / 5;
            int labelY = 44;

            g.DrawString("Minute", font, labelBrush, margin + 0 * (fieldW + 5), labelY);
            g.DrawString("Hour", font, labelBrush, margin + 1 * (fieldW + 5), labelY);
            g.DrawString("Day", font, labelBrush, margin + 2 * (fieldW + 5), labelY);
            g.DrawString("Month", font, labelBrush, margin + 3 * (fieldW + 5), labelY);
            g.DrawString("D.O.W.", font, labelBrush, margin + 4 * (fieldW + 5), labelY);

            // Row 3 Label: Cron & Human Text
            if (_showPreview)
            {
                int prevY = 92;
                g.DrawString("Result:", boldFont, labelBrush, 10, prevY);

                string human = ToHumanReadable();
                var humanFont = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Italic);
                using var humanBrush = new SolidBrush(colors.PrimaryAccent);
                g.DrawString($"\"{human}\"", humanFont, humanBrush, 235, prevY + 2);
            }
        }
    }

    [Obsolete("CronEditor is deprecated. Use ZCronEditor instead.")]
    [ToolboxItem(false)]
    public class CronEditor : ZCronEditor { }

    [Obsolete("ZeroCronEditor is deprecated. Use ZCronEditor instead.")]
    [ToolboxItem(false)]
    public class ZeroCronEditor : ZCronEditor { }
}
