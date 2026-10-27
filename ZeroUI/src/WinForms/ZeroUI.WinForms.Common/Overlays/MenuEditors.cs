using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Icons;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    #region 1. MenuItemSearch

    /// <summary>
    /// Interactive search input panel designed for context menus and toolbars.
    /// Supports debounced search, vector icons, watermark placeholder, and quick-clear action.
    /// </summary>
    public class SearchBoxPanel : Panel
    {
        private readonly TextBox _textBox;
        private readonly Timer _debounceTimer;
        private string _placeholder = "Search...";
        private bool _isHovered;
        private Rectangle _clearButtonRect;

        public event EventHandler<string>? SearchSubmitted;

        public string SearchText
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        public string Placeholder
        {
            get => _placeholder;
            set { _placeholder = value; Invalidate(); }
        }

        public int DebounceInterval
        {
            get => _debounceTimer.Interval;
            set => _debounceTimer.Interval = Math.Max(50, value);
        }

        public TextBox InnerTextBox => _textBox;

        public SearchBoxPanel(string placeholder = "Search...", int width = 220, int debounceMs = 250)
        {
            _placeholder = placeholder;
            Size = new Size(width, 32);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Location = new Point(30, 8),
                Size = new Size(width - 56, 16),
                Font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular)
            };

            _debounceTimer = new Timer { Interval = Math.Max(50, debounceMs) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                SearchSubmitted?.Invoke(this, _textBox.Text.Trim());
            };

            _textBox.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
                Invalidate();
            };

            _textBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _debounceTimer.Stop();
                    SearchSubmitted?.Invoke(this, _textBox.Text.Trim());
                }
                else if (e.KeyCode == Keys.Escape && !string.IsNullOrEmpty(_textBox.Text))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _textBox.Clear();
                }
            };

            Controls.Add(_textBox);
            UpdateColors();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void UpdateColors()
        {
            var palette = ZeroTheme.Colors;
            BackColor = palette.CardBackground;
            _textBox.BackColor = palette.Surface;
            _textBox.ForeColor = palette.TextPrimary;
            Invalidate();
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
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_clearButtonRect.Contains(e.Location) && !string.IsNullOrEmpty(_textBox.Text))
            {
                _textBox.Clear();
                _textBox.Focus();
                _debounceTimer.Stop();
                SearchSubmitted?.Invoke(this, string.Empty);
            }
            else
            {
                _textBox.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            var boxRect = new Rectangle(2, 2, Width - 5, Height - 5);

            // 1. Box background & border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(boxRect, 6))
            {
                using var brushBg = new SolidBrush(palette.Surface);
                g.FillPath(brushBg, path);

                Color borderColor = _textBox.Focused ? palette.Primary : (_isHovered ? palette.TextSecondary : palette.Border);
                using var penBorder = new Pen(borderColor, _textBox.Focused ? 1.5f : 1f);
                g.DrawPath(penBorder, path);
            }

            // 2. Search vector icon
            var iconRect = new Rectangle(8, (Height - 16) / 2, 16, 16);
            ZeroIcon.Draw(g, IconKey.Search, iconRect, _textBox.Focused ? palette.Primary : palette.TextSecondary);

            // 3. Clear button or placeholder
            if (string.IsNullOrEmpty(_textBox.Text) && !_textBox.Focused)
            {
                var fontPlace = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Italic);
                using var brushPlace = new SolidBrush(Color.FromArgb(130, palette.TextSecondary));
                g.DrawString(_placeholder, fontPlace, brushPlace, 32, (Height - 16) / 2);
            }
            else if (!string.IsNullOrEmpty(_textBox.Text))
            {
                _clearButtonRect = new Rectangle(Width - 24, (Height - 16) / 2, 16, 16);
                ZeroIcon.Draw(g, IconKey.Close, _clearButtonRect, palette.TextSecondary);
            }
            else
            {
                _clearButtonRect = Rectangle.Empty;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => UpdateColors();

}

    /// <summary>
    /// Search input menu item embeddable into ContextMenuStrip or ToolStrip.
    /// </summary>
    public class MenuItemSearch : MenuItemControlHost<SearchBoxPanel>
    {
        public string SearchText
        {
            get => TypedControl.SearchText;
            set => TypedControl.SearchText = value;
        }

        public string Placeholder
        {
            get => TypedControl.Placeholder;
            set => TypedControl.Placeholder = value;
        }

        public event EventHandler<string>? SearchSubmitted
        {
            add => TypedControl.SearchSubmitted += value;
            remove => TypedControl.SearchSubmitted -= value;
        }

        public MenuItemSearch(string placeholder = "Search...", Action<string>? onSearch = null, int debounceMs = 250, int width = 220)
            : base(new SearchBoxPanel(placeholder, width, debounceMs))
        {
            if (onSearch != null)
            {
                TypedControl.SearchSubmitted += (s, text) => onSearch(text);
            }
        }
    }

    #endregion

    #region 2. MenuItemToggle

    /// <summary>
    /// Interactive panel with Title, optional Subtitle, and a modern iOS/Fluent style ToggleSwitch pill.
    /// </summary>
    public class ToggleItemPanel : Panel
    {
        private string _title;
        private string? _subtitle;
        private bool _checked;
        private bool _isHovered;
        private Rectangle _pillRect;

        public event EventHandler<bool>? CheckedChanged;

        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        public string? Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; Invalidate(); }
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    CheckedChanged?.Invoke(this, _checked);
                }
            }
        }

        public ToggleItemPanel(string title, bool isChecked = false, string? subtitle = null, int width = 220)
        {
            _title = title;
            _checked = isChecked;
            _subtitle = subtitle;
            int height = string.IsNullOrEmpty(subtitle) ? 34 : 44;
            Size = new Size(width, height);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
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
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // Hover background pill
            if (_isHovered)
            {
                using var brushHov = new SolidBrush(Color.FromArgb(25, palette.Primary));
                using var pathHov = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(2, 2, Width - 4, Height - 4), 4);
                g.FillPath(brushHov, pathHov);
            }

            // Title & Subtitle text
            int textX = 10;
            if (string.IsNullOrEmpty(_subtitle))
            {
                var fontTitle = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular);
                using var brushTitle = new SolidBrush(palette.TextPrimary);
                g.DrawString(_title, fontTitle, brushTitle, textX, (Height - 16) / 2);
            }
            else
            {
                var fontTitle = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
                using var brushTitle = new SolidBrush(palette.TextPrimary);
                g.DrawString(_title, fontTitle, brushTitle, textX, 5);

                var fontSub = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Regular);
                using var brushSub = new SolidBrush(palette.TextSecondary);
                g.DrawString(_subtitle, fontSub, brushSub, textX, 22);
            }

            // Toggle switch pill
            int pillW = 34;
            int pillH = 18;
            int pillX = Width - pillW - 10;
            int pillY = (Height - pillH) / 2;
            _pillRect = new Rectangle(pillX, pillY, pillW, pillH);

            Color pillBg = _checked ? palette.Primary : Color.FromArgb(140, palette.Border);
            using (var pathPill = ZeroUIConfig.CreateRoundedRectangle(_pillRect, pillH / 2))
            using (var brushPill = new SolidBrush(pillBg))
            {
                g.FillPath(brushPill, pathPill);
            }

            // Toggle knob
            int knobD = pillH - 4;
            int knobX = _checked ? (pillX + pillW - knobD - 2) : (pillX + 2);
            int knobY = pillY + 2;
            using (var brushKnob = new SolidBrush(Color.White))
            {
                g.FillEllipse(brushKnob, knobX, knobY, knobD, knobD);
            }
        }
    }

    /// <summary>
    /// Toggle switch menu item embeddable into ContextMenuStrip or ToolStrip.
    /// </summary>
    public class MenuItemToggle : MenuItemControlHost<ToggleItemPanel>
    {
        public bool Checked
        {
            get => TypedControl.Checked;
            set => TypedControl.Checked = value;
        }

        public string Title
        {
            get => TypedControl.Title;
            set => TypedControl.Title = value;
        }

        public string? Subtitle
        {
            get => TypedControl.Subtitle;
            set => TypedControl.Subtitle = value;
        }

        public event EventHandler<bool>? CheckedChanged
        {
            add => TypedControl.CheckedChanged += value;
            remove => TypedControl.CheckedChanged -= value;
        }

        public MenuItemToggle(string title, bool isChecked = false, Action<bool>? onToggle = null, string? subtitle = null, int width = 220)
            : base(new ToggleItemPanel(title, isChecked, subtitle, width))
        {
            if (onToggle != null)
            {
                TypedControl.CheckedChanged += (s, c) => onToggle(c);
            }
        }
    }

    #endregion

    #region 3. MenuItemSlider

    /// <summary>
    /// Interactive panel with Title, live value readout, and a continuous horizontal slider track.
    /// </summary>
    public class SliderItemPanel : Panel
    {
        private string _title;
        private string _unit;
        private int _min;
        private int _max;
        private int _value;
        private bool _isDragging;
        private Rectangle _trackRect;

        public event EventHandler<int>? ValueChanged;

        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; Invalidate(); }
        }

        public int Minimum
        {
            get => _min;
            set { _min = value; Invalidate(); }
        }

        public int Maximum
        {
            get => _max;
            set { _max = value; Invalidate(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(_min, Math.Min(_max, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    ValueChanged?.Invoke(this, _value);
                }
            }
        }

        public SliderItemPanel(string title, int min = 0, int max = 100, int current = 50, string unit = "", int width = 220)
        {
            _title = title;
            _min = min;
            _max = Math.Max(min + 1, max);
            _value = Math.Max(_min, Math.Min(_max, current));
            _unit = unit;

            Size = new Size(width, 46);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                UpdateValueFromPoint(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateValueFromPoint(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
        }

        private void UpdateValueFromPoint(int mouseX)
        {
            if (_trackRect.Width <= 0) return;
            float ratio = (float)(mouseX - _trackRect.Left) / _trackRect.Width;
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            Value = _min + (int)Math.Round(ratio * (_max - _min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // 1. Header: Title on left, Value readout on right
            var fontTitle = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var brushTitle = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(_title, fontTitle, brushTitle, 10, 5);
            }

            string valText = $"{_value}{_unit}";
            var fontVal = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var brushVal = new SolidBrush(palette.Primary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(valText, fontVal, brushVal, Width - 10, 5, sf);
            }

            // 2. Track bar
            int trackH = 4;
            int trackX = 10;
            int trackY = 28;
            int trackW = Width - 20;
            _trackRect = new Rectangle(trackX, trackY, trackW, trackH);

            // Empty track groove
            using (var brushEmpty = new SolidBrush(palette.Border))
            using (var pathEmpty = ZeroUIConfig.CreateRoundedRectangle(_trackRect, 2))
            {
                g.FillPath(brushEmpty, pathEmpty);
            }

            // Active track fill
            float progress = (_max > _min) ? (float)(_value - _min) / (_max - _min) : 0f;
            int filledW = (int)Math.Round(progress * trackW);
            if (filledW > 0)
            {
                var filledRect = new Rectangle(trackX, trackY, filledW, trackH);
                using var brushFill = new SolidBrush(palette.Primary);
                using var pathFill = ZeroUIConfig.CreateRoundedRectangle(filledRect, 2);
                g.FillPath(brushFill, pathFill);
            }

            // 3. Thumb knob
            int thumbD = 14;
            int thumbX = trackX + filledW - (thumbD / 2);
            int thumbY = trackY + (trackH / 2) - (thumbD / 2);

            // Thumb halo shadow
            using (var brushShadow = new SolidBrush(Color.FromArgb(40, palette.Primary)))
            {
                g.FillEllipse(brushShadow, thumbX - 2, thumbY - 2, thumbD + 4, thumbD + 4);
            }

            // Thumb body
            using (var brushThumb = new SolidBrush(palette.Primary))
            {
                g.FillEllipse(brushThumb, thumbX, thumbY, thumbD, thumbD);
            }
            using (var brushCenter = new SolidBrush(Color.White))
            {
                g.FillEllipse(brushCenter, thumbX + 4, thumbY + 4, thumbD - 8, thumbD - 8);
            }
        }
    }

    /// <summary>
    /// Continuous slider menu item embeddable into ContextMenuStrip or ToolStrip.
    /// </summary>
    public class MenuItemSlider : MenuItemControlHost<SliderItemPanel>
    {
        public int Value
        {
            get => TypedControl.Value;
            set => TypedControl.Value = value;
        }

        public string Title
        {
            get => TypedControl.Title;
            set => TypedControl.Title = value;
        }

        public event EventHandler<int>? ValueChanged
        {
            add => TypedControl.ValueChanged += value;
            remove => TypedControl.ValueChanged -= value;
        }

        public MenuItemSlider(string title, int min = 0, int max = 100, int current = 50, Action<int>? onValueChanged = null, string unit = "", int width = 220)
            : base(new SliderItemPanel(title, min, max, current, unit, width))
        {
            if (onValueChanged != null)
            {
                TypedControl.ValueChanged += (s, v) => onValueChanged(v);
            }
        }
    }

    #endregion

    #region 4. MenuItemComboBox

    /// <summary>
    /// Interactive panel hosting a ComboBox selector inside menus with theme reactivity.
    /// </summary>
    public class ComboBoxItemPanel : Panel
    {
        private readonly Label _label;
        private readonly ComboBox _comboBox;

        public event EventHandler? SelectionChanged;

        public ComboBox InnerComboBox => _comboBox;

        public int SelectedIndex
        {
            get => _comboBox.SelectedIndex;
            set => _comboBox.SelectedIndex = value;
        }

        public object? SelectedItem
        {
            get => _comboBox.SelectedItem;
            set => _comboBox.SelectedItem = value;
        }

        public ComboBoxItemPanel(string label, object[]? items = null, int selectedIndex = -1, int width = 220)
        {
            Size = new Size(width, 36);
            DoubleBuffered = true;

            _label = new Label
            {
                Text = label,
                Location = new Point(8, 9),
                AutoSize = true,
                Font = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold)
            };

            int comboX = 85;
            _comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(comboX, 6),
                Size = new Size(width - comboX - 10, 24),
                Font = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular)
            };

            if (items != null && items.Length > 0)
            {
                _comboBox.Items.AddRange(items);
                if (selectedIndex >= 0 && selectedIndex < items.Length)
                {
                    _comboBox.SelectedIndex = selectedIndex;
                }
            }

            _comboBox.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);

            Controls.Add(_label);
            Controls.Add(_comboBox);

            UpdateColors();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void UpdateColors()
        {
            var palette = ZeroTheme.Colors;
            BackColor = palette.CardBackground;
            _label.ForeColor = palette.TextPrimary;
            _comboBox.BackColor = palette.Surface;
            _comboBox.ForeColor = palette.TextPrimary;
            Invalidate();
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => UpdateColors();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// ComboBox selector menu item embeddable into ContextMenuStrip or ToolStrip.
    /// </summary>
    public class MenuItemComboBox : MenuItemControlHost<ComboBoxItemPanel>
    {
        public int SelectedIndex
        {
            get => TypedControl.SelectedIndex;
            set => TypedControl.SelectedIndex = value;
        }

        public object? SelectedItem
        {
            get => TypedControl.SelectedItem;
            set => TypedControl.SelectedItem = value;
        }

        public event EventHandler? SelectionChanged
        {
            add => TypedControl.SelectionChanged += value;
            remove => TypedControl.SelectionChanged -= value;
        }

        public MenuItemComboBox(string label, object[]? items, int selectedIndex = -1, Action<object?>? onSelectionChanged = null, int width = 220)
            : base(new ComboBoxItemPanel(label, items, selectedIndex, width))
        {
            if (onSelectionChanged != null)
            {
                TypedControl.SelectionChanged += (s, e) => onSelectionChanged(TypedControl.SelectedItem);
            }
        }
    }

    #endregion

    #region 5. MenuItemNumeric

    /// <summary>
    /// Interactive precision stepper panel hosting numeric increment/decrement buttons and value readout.
    /// </summary>
    public class NumericItemPanel : Panel
    {
        private string _label;
        private decimal _min;
        private decimal _max;
        private decimal _value;
        private decimal _step;
        private Rectangle _minusRect;
        private Rectangle _plusRect;
        private bool _minusHover;
        private bool _plusHover;

        public event EventHandler<decimal>? ValueChanged;

        public decimal Minimum
        {
            get => _min;
            set { _min = value; Invalidate(); }
        }

        public decimal Maximum
        {
            get => _max;
            set { _max = value; Invalidate(); }
        }

        public decimal Step
        {
            get => _step;
            set => _step = Math.Max(0.001m, value);
        }

        public decimal Value
        {
            get => _value;
            set
            {
                decimal clamped = Math.Max(_min, Math.Min(_max, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    ValueChanged?.Invoke(this, _value);
                }
            }
        }

        public NumericItemPanel(string label, decimal min = 0, decimal max = 100, decimal current = 1, decimal step = 1, int width = 220)
        {
            _label = label;
            _min = min;
            _max = Math.Max(min, max);
            _step = step;
            _value = Math.Max(_min, Math.Min(_max, current));

            Size = new Size(width, 36);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool mHov = _minusRect.Contains(e.Location);
            bool pHov = _plusRect.Contains(e.Location);
            if (mHov != _minusHover || pHov != _plusHover)
            {
                _minusHover = mHov;
                _plusHover = pHov;
                Cursor = (mHov || pHov) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _minusHover = false;
            _plusHover = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_minusRect.Contains(e.Location))
                {
                    Value -= _step;
                }
                else if (_plusRect.Contains(e.Location))
                {
                    Value += _step;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // 1. Label on left
            var fontLabel = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var brushLabel = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(_label, fontLabel, brushLabel, 10, (Height - 16) / 2);
            }

            // 2. Stepper container on right
            int btnW = 24;
            int boxH = 24;
            int valW = 44;
            int totalW = btnW * 2 + valW;
            int startX = Width - totalW - 10;
            int startY = (Height - boxH) / 2;

            _minusRect = new Rectangle(startX, startY, btnW, boxH);
            var valRect = new Rectangle(startX + btnW, startY, valW, boxH);
            _plusRect = new Rectangle(startX + btnW + valW, startY, btnW, boxH);

            var outerRect = new Rectangle(startX, startY, totalW, boxH);
            using (var pathOuter = ZeroUIConfig.CreateRoundedRectangle(outerRect, 4))
            {
                using var brushBg = new SolidBrush(palette.Surface);
                g.FillPath(brushBg, pathOuter);

                using var penBorder = new Pen(palette.Border, 1f);
                g.DrawPath(penBorder, pathOuter);
            }

            // Minus button hover & glyph
            if (_minusHover)
            {
                using var brushHov = new SolidBrush(Color.FromArgb(30, palette.Primary));
                g.FillRectangle(brushHov, _minusRect);
            }
            using (var penGlyph = new Pen(palette.TextPrimary, 1.5f))
            {
                int cy = _minusRect.Y + _minusRect.Height / 2;
                g.DrawLine(penGlyph, _minusRect.X + 7, cy, _minusRect.Right - 7, cy);
            }

            // Value text
            var fontVal = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var brushVal = new SolidBrush(palette.TextPrimary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_value.ToString(), fontVal, brushVal, valRect, sf);
            }

            // Plus button hover & glyph
            if (_plusHover)
            {
                using var brushHov = new SolidBrush(Color.FromArgb(30, palette.Primary));
                g.FillRectangle(brushHov, _plusRect);
            }
            using (var penGlyph = new Pen(palette.TextPrimary, 1.5f))
            {
                int cx = _plusRect.X + _plusRect.Width / 2;
                int cy = _plusRect.Y + _plusRect.Height / 2;
                g.DrawLine(penGlyph, _plusRect.X + 7, cy, _plusRect.Right - 7, cy);
                g.DrawLine(penGlyph, cx, _plusRect.Y + 7, cx, _plusRect.Bottom - 7);
            }

            // Dividers
            using (var penDiv = new Pen(palette.Border, 1f))
            {
                g.DrawLine(penDiv, valRect.Left, valRect.Top, valRect.Left, valRect.Bottom);
                g.DrawLine(penDiv, valRect.Right, valRect.Top, valRect.Right, valRect.Bottom);
            }
        }
    }

    /// <summary>
    /// Numeric stepper menu item embeddable into ContextMenuStrip or ToolStrip.
    /// </summary>
    public class MenuItemNumeric : MenuItemControlHost<NumericItemPanel>
    {
        public decimal Value
        {
            get => TypedControl.Value;
            set => TypedControl.Value = value;
        }

        public decimal Minimum
        {
            get => TypedControl.Minimum;
            set => TypedControl.Minimum = value;
        }

        public decimal Maximum
        {
            get => TypedControl.Maximum;
            set => TypedControl.Maximum = value;
        }

        public decimal Step
        {
            get => TypedControl.Step;
            set => TypedControl.Step = value;
        }

        public event EventHandler<decimal>? ValueChanged
        {
            add => TypedControl.ValueChanged += value;
            remove => TypedControl.ValueChanged -= value;
        }

        public MenuItemNumeric(string label, decimal min = 0, decimal max = 100, decimal current = 1, decimal step = 1, Action<decimal>? onValueChanged = null, int width = 220)
            : base(new NumericItemPanel(label, min, max, current, step, width))
        {
            if (onValueChanged != null)
            {
                TypedControl.ValueChanged += (s, v) => onValueChanged(v);
            }
        }
    }

    #endregion
}
