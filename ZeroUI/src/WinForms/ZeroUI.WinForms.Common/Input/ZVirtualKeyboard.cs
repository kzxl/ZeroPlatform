using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Input
{
    /// <summary>
    /// Industrial touch-screen virtual keyboard supporting QWERTY alphanumeric
    /// and 10-key numeric keypad (Numpad) layouts with large touch hit-targets.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty(nameof(LayoutMode))]
    public class ZVirtualKeyboard : ControlBase
    {
        private VirtualKeyboardLayout _layoutMode = VirtualKeyboardLayout.AlphaNumeric;
        private Control? _targetControl;
        private bool _shiftActive;
        private bool _capsActive;
        private bool _symbolActive;
        private int _pressedKeyIndex = -1;
        private int _hoveredKeyIndex = -1;

        private readonly List<KeyDefinition> _keys = new List<KeyDefinition>();

        [Category("Behavior")]
        [DefaultValue(VirtualKeyboardLayout.AlphaNumeric)]
        [Description("Selects either the full QWERTY alphanumeric layout or the 10-key numeric keypad.")]
        public VirtualKeyboardLayout LayoutMode
        {
            get => _layoutMode;
            set
            {
                if (_layoutMode != value)
                {
                    _layoutMode = value;
                    RebuildKeyLayout();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        [Description("The target input control receiving keystrokes from this virtual keyboard.")]
        public Control? TargetControl
        {
            get => _targetControl;
            set => _targetControl = value;
        }

        [Category("Appearance")]
        [DefaultValue(46)]
        [Description("Height of individual virtual keys in pixels.")]
        public int KeyHeight { get; set; } = 46;

        [Category("Appearance")]
        [DefaultValue(4)]
        [Description("Margin spacing between adjacent virtual keys in pixels.")]
        public int KeySpacing { get; set; } = 4;

        public event EventHandler<VirtualKeyEventArgs>? KeyPressed;
        public event EventHandler? EnterPressed;
        public event EventHandler? EscapePressed;

        public ZVirtualKeyboard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(680, 240);
            BackColor = CurrentPalette.Background;
            ForeColor = CurrentPalette.TextPrimary;
            RebuildKeyLayout();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = CurrentPalette.Background;
            ForeColor = CurrentPalette.TextPrimary;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RebuildKeyLayout();
        }

        #region Key Layout Engine

        private void RebuildKeyLayout()
        {
            _keys.Clear();

            if (_layoutMode == VirtualKeyboardLayout.Numpad)
            {
                BuildNumpadLayout();
            }
            else
            {
                BuildAlphaNumericLayout();
            }
        }

        private void BuildNumpadLayout()
        {
            int cols = 4;
            int rows = 4;
            int spacing = KeySpacing;

            int totalW = ClientSize.Width - spacing * (cols + 1);
            int totalH = ClientSize.Height - spacing * (rows + 1);
            int keyW = Math.Max(20, totalW / cols);
            int keyH = Math.Max(20, totalH / rows);

            string[,] matrix = new string[,]
            {
                { "7", "8", "9", "Bksp" },
                { "4", "5", "6", "Clear" },
                { "1", "2", "3", "+/-" },
                { "0", ".", "Esc", "Enter" }
            };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string label = matrix[r, c];
                    int x = spacing + c * (keyW + spacing);
                    int y = spacing + r * (keyH + spacing);

                    VirtualKeyType type = VirtualKeyType.Character;
                    if (label == "Bksp") type = VirtualKeyType.Backspace;
                    else if (label == "Clear") type = VirtualKeyType.Clear;
                    else if (label == "Enter") type = VirtualKeyType.Enter;
                    else if (label == "Esc") type = VirtualKeyType.Escape;

                    _keys.Add(new KeyDefinition(label, label, new Rectangle(x, y, keyW, keyH), type));
                }
            }
        }

        private void BuildAlphaNumericLayout()
        {
            int spacing = KeySpacing;
            int availH = ClientSize.Height - spacing * 6;
            int rowH = Math.Max(18, availH / 5);

            // Row 1: Numbers
            string[] r1 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Bksp" };
            BuildRow(r1, 0, spacing, rowH, new Dictionary<string, float> { ["Bksp"] = 1.6f });

            // Row 2: QWERTY
            string[] r2 = { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]" };
            BuildRow(r2, 1, spacing, rowH, new Dictionary<string, float> { ["Tab"] = 1.3f });

            // Row 3: ASDF
            string[] r3 = { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" };
            BuildRow(r3, 2, spacing, rowH, new Dictionary<string, float> { ["Caps"] = 1.4f, ["Enter"] = 1.8f });

            // Row 4: ZXCV
            string[] r4 = { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift" };
            BuildRow(r4, 3, spacing, rowH, new Dictionary<string, float> { ["Shift"] = 1.6f });

            // Row 5: Spacebar & control
            string[] r5 = { "?123", "Clear", "Space", "Esc" };
            BuildRow(r5, 4, spacing, rowH, new Dictionary<string, float> { ["?123"] = 1.5f, ["Clear"] = 1.5f, ["Space"] = 5.0f, ["Esc"] = 1.5f });
        }

        private void BuildRow(string[] items, int rowIndex, int spacing, int rowH, Dictionary<string, float>? weightOverrides = null)
        {
            float totalWeight = 0;
            foreach (var item in items)
            {
                if (weightOverrides != null && weightOverrides.TryGetValue(item, out float w))
                    totalWeight += w;
                else
                    totalWeight += 1.0f;
            }

            int availW = ClientSize.Width - spacing * (items.Length + 1);
            float unitW = availW / totalWeight;

            float curX = spacing;
            int y = spacing + rowIndex * (rowH + spacing);

            foreach (var item in items)
            {
                float weight = 1.0f;
                if (weightOverrides != null && weightOverrides.TryGetValue(item, out float w))
                    weight = w;

                int keyW = (int)(unitW * weight);
                var bounds = new Rectangle((int)curX, y, keyW, rowH);

                VirtualKeyType type = VirtualKeyType.Character;
                if (item == "Bksp") type = VirtualKeyType.Backspace;
                else if (item == "Enter") type = VirtualKeyType.Enter;
                else if (item == "Space") type = VirtualKeyType.Space;
                else if (item == "Shift" || item == "Caps") type = VirtualKeyType.Shift;
                else if (item == "Clear") type = VirtualKeyType.Clear;
                else if (item == "Esc") type = VirtualKeyType.Escape;
                else if (item == "Tab") type = VirtualKeyType.Tab;
                else if (item == "?123") type = VirtualKeyType.SymbolToggle;

                _keys.Add(new KeyDefinition(item, item, bounds, type));
                curX += keyW + spacing;
            }
        }

        #endregion

        #region Mouse & Touch Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevHover = _hoveredKeyIndex;
            _hoveredKeyIndex = HitTestKey(e.Location);
            if (prevHover != _hoveredKeyIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredKeyIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pressedKeyIndex = HitTestKey(e.Location);
            if (_pressedKeyIndex >= 0)
            {
                Invalidate();
                ProcessKeyActivation(_keys[_pressedKeyIndex]);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedKeyIndex >= 0)
            {
                _pressedKeyIndex = -1;
                Invalidate();
            }
        }

        private int HitTestKey(Point pt)
        {
            for (int i = 0; i < _keys.Count; i++)
            {
                if (_keys[i].Bounds.Contains(pt))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ProcessKeyActivation(KeyDefinition key)
        {
            string outText = key.PrimaryLabel;

            switch (key.KeyType)
            {
                case VirtualKeyType.Shift:
                    if (key.PrimaryLabel == "Caps")
                    {
                        _capsActive = !_capsActive;
                    }
                    else
                    {
                        _shiftActive = !_shiftActive;
                    }
                    Invalidate();
                    return;

                case VirtualKeyType.SymbolToggle:
                    _symbolActive = !_symbolActive;
                    Invalidate();
                    return;

                case VirtualKeyType.Backspace:
                    ApplyBackspace();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Backspace));
                    return;

                case VirtualKeyType.Clear:
                    ApplyClear();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Clear));
                    return;

                case VirtualKeyType.Enter:
                    ApplyEnter();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("\r\n", VirtualKeyType.Enter));
                    EnterPressed?.Invoke(this, EventArgs.Empty);
                    return;

                case VirtualKeyType.Escape:
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Escape));
                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    return;

                case VirtualKeyType.Space:
                    outText = " ";
                    break;

                case VirtualKeyType.Tab:
                    outText = "\t";
                    break;

                default:
                    if (outText.Length == 1 && char.IsLetter(outText[0]))
                    {
                        bool upper = _capsActive ^ _shiftActive;
                        outText = upper ? outText.ToUpperInvariant() : outText.ToLowerInvariant();
                        if (_shiftActive) _shiftActive = false; // Shift resets after 1 letter
                    }
                    break;
            }

            ApplyText(outText);
            KeyPressed?.Invoke(this, new VirtualKeyEventArgs(outText, key.KeyType));
        }

        private void ApplyText(string text)
        {
            if (_targetControl is TextBoxBase tb)
            {
                int start = tb.SelectionStart;
                int len = tb.SelectionLength;
                tb.Text = tb.Text.Remove(start, len).Insert(start, text);
                tb.SelectionStart = start + text.Length;
                tb.SelectionLength = 0;
            }
            else
            {
                try
                {
                    SendKeys.SendWait(text);
                }
                catch { }
            }
        }

        private void ApplyBackspace()
        {
            if (_targetControl is TextBoxBase tb)
            {
                int start = tb.SelectionStart;
                int len = tb.SelectionLength;
                if (len > 0)
                {
                    tb.Text = tb.Text.Remove(start, len);
                    tb.SelectionStart = start;
                }
                else if (start > 0)
                {
                    tb.Text = tb.Text.Remove(start - 1, 1);
                    tb.SelectionStart = start - 1;
                }
            }
            else
            {
                try { SendKeys.SendWait("{BACKSPACE}"); } catch { }
            }
        }

        private void ApplyClear()
        {
            if (_targetControl is TextBoxBase tb)
            {
                tb.Text = string.Empty;
            }
        }

        private void ApplyEnter()
        {
            if (_targetControl is TextBoxBase tb && tb.Multiline)
            {
                ApplyText("\r\n");
            }
            else
            {
                try { SendKeys.SendWait("{ENTER}"); } catch { }
            }
        }

        #endregion

        #region Rendering Engine

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var pal = CurrentPalette;

            Color backColor = pal.Background;
            Color keyBackNormal = pal.Surface;
            Color keyBackHover = pal.Hover;
            Color keyBackPressed = pal.Primary;
            Color keyBorder = pal.Border;
            Color textColor = pal.TextPrimary;
            Color specialKeyBack = pal.HeaderBackground;

            g.Clear(backColor);

            using var borderPen = new Pen(keyBorder, 1f);
            using var font = new Font(Font.FontFamily, Math.Max(9f, KeyHeight * 0.32f), FontStyle.Regular);
            using var specialFont = new Font(Font.FontFamily, Math.Max(8f, KeyHeight * 0.26f), FontStyle.Bold);

            for (int i = 0; i < _keys.Count; i++)
            {
                var k = _keys[i];
                bool isPressed = (i == _pressedKeyIndex);
                bool isHover = (i == _hoveredKeyIndex);

                Color currentKeyBack;
                if (isPressed) currentKeyBack = keyBackPressed;
                else if (isHover) currentKeyBack = keyBackHover;
                else if (k.KeyType != VirtualKeyType.Character && k.KeyType != VirtualKeyType.Space)
                    currentKeyBack = specialKeyBack;
                else
                    currentKeyBack = keyBackNormal;

                using var brush = new SolidBrush(currentKeyBack);
                using var path = CreateRoundedRectanglePath(k.Bounds, 4);
                g.FillPath(brush, path);
                g.DrawPath(borderPen, path);

                // Label
                string displayLabel = k.PrimaryLabel;
                if (displayLabel.Length == 1 && char.IsLetter(displayLabel[0]))
                {
                    bool upper = _capsActive ^ _shiftActive;
                    displayLabel = upper ? displayLabel.ToUpperInvariant() : displayLabel.ToLowerInvariant();
                }

                Color currentText = isPressed ? GetPressedTextColor(pal) : textColor;
                using var textBrush = new SolidBrush(currentText);

                var f = (k.KeyType != VirtualKeyType.Character) ? specialFont : font;
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString(displayLabel, f, textBrush, k.Bounds, sf);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color GetPressedTextColor(ZeroThemePalette pal)
        {
            double bgContrast = ZeroColorUtils.GetContrastRatio(ColorToHex(pal.Background), ColorToHex(pal.Primary));
            double textContrast = ZeroColorUtils.GetContrastRatio(ColorToHex(pal.TextPrimary), ColorToHex(pal.Primary));
            if (bgContrast >= 4.5 && bgContrast >= textContrast) return pal.Background;
            if (textContrast >= 4.5) return pal.TextPrimary;

            var skin = ZeroSkinManager.CurrentSkin;
            if (skin == null || !string.Equals(skin.Tokens.PrimaryAccent, ColorToHex(pal.Primary), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var s in ZeroSkinDefaults.GetAllDefaults())
                {
                    if (string.Equals(s.Tokens.PrimaryAccent, ColorToHex(pal.Primary), StringComparison.OrdinalIgnoreCase))
                    {
                        skin = s;
                        break;
                    }
                }
            }

            if (skin != null && !string.IsNullOrEmpty(skin.Tokens.SelectionForeground))
            {
                Color selColor = ColorTranslator.FromHtml(skin.Tokens.SelectionForeground);
                double selContrast = ZeroColorUtils.GetContrastRatio(ColorToHex(selColor), ColorToHex(pal.Primary));
                if (selContrast >= 4.5) return selColor;
            }

            return bgContrast >= textContrast ? pal.Background : pal.TextPrimary;
        }

        #endregion

        #region Helper Popover Launcher

        /// <summary>
        /// Displays a floating touch-screen virtual keyboard docked at the bottom of the screen or owner form.
        /// </summary>
        public static Form ShowFloatingPopup(Control target, VirtualKeyboardLayout layout = VirtualKeyboardLayout.AlphaNumeric, Form? parent = null)
        {
            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = ZeroTheme.Colors.Border,
                Padding = new Padding(1),
                Size = (layout == VirtualKeyboardLayout.Numpad) ? new Size(320, 300) : new Size(720, 260)
            };

            EventHandler themeHandler = (s, e) =>
            {
                if (form.IsDisposed) return;
                if (form.InvokeRequired)
                {
                    try { form.BeginInvoke(new Action(() => form.BackColor = ZeroTheme.Colors.Border)); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    form.BackColor = ZeroTheme.Colors.Border;
                }
            };

            int cleanedUp = 0;
            void CleanupHandler(object? s, EventArgs e)
            {
                if (System.Threading.Interlocked.Exchange(ref cleanedUp, 1) == 0)
                {
                    ZeroTheme.ThemeChanged -= themeHandler;
                }
            }

            ZeroTheme.ThemeChanged += themeHandler;
            form.FormClosed += CleanupHandler;
            form.Disposed += CleanupHandler;

            var kb = new ZVirtualKeyboard
            {
                Dock = DockStyle.Fill,
                LayoutMode = layout,
                TargetControl = target
            };

            kb.EscapePressed += (s, e) => form.Close();
            kb.EnterPressed += (s, e) => form.Close();

            form.Controls.Add(kb);

            Point targetScreen = target.PointToScreen(new Point(0, target.Height));
            Rectangle screenBounds = Screen.FromControl(target).WorkingArea;

            int x = targetScreen.X;
            int y = targetScreen.Y + 4;

            if (x + form.Width > screenBounds.Right)
                x = screenBounds.Right - form.Width - 8;
            if (y + form.Height > screenBounds.Bottom)
                y = target.PointToScreen(Point.Empty).Y - form.Height - 4;

            form.Location = new Point(Math.Max(screenBounds.Left + 8, x), Math.Max(screenBounds.Top + 8, y));
            form.Show();
            return form;
        }

        #endregion

        private class KeyDefinition
        {
            public string PrimaryLabel { get; }
            public string ShiftedLabel { get; }
            public Rectangle Bounds { get; }
            public VirtualKeyType KeyType { get; }

            public KeyDefinition(string primaryLabel, string shiftedLabel, Rectangle bounds, VirtualKeyType keyType)
            {
                PrimaryLabel = primaryLabel;
                ShiftedLabel = shiftedLabel;
                Bounds = bounds;
                KeyType = keyType;
            }
        }
    }
}
