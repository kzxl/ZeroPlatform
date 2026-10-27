using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern anti-aliased Color Picker editor for ZeroUI WinForms.
    /// Provides live swatch preview, hex text input, standard enterprise palette swatches,
    /// and RGB adjustment controls.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ColorChanged")]
    [Description("Modern color picker editor with swatch preview, palette, and hex input")]
    [ToolboxBitmap(typeof(ZeroIcons), "ColorPickEdit.bmp")]
    public class ZColorPicker : ControlBase, IZeroEditor
    {
        private Color _selectedColor = Color.FromArgb(79, 70, 229); // Default ZeroUI Primary
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isDroppedDown = false;

        private readonly DropDownHost _dropdown;
        private readonly ColorPickerPopupControl _popupControl;

        public event EventHandler? ColorChanged;
        public event EventHandler? EditValueChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => SelectedColor;
            set
            {
                if (value == null || value == DBNull.Value)
                {
                    SelectedColor = Color.Empty;
                }
                else if (value is Color c)
                {
                    SelectedColor = c;
                }
                else if (value is string s)
                {
                    try
                    {
                        SelectedColor = ColorTranslator.FromHtml(s);
                    }
                    catch
                    {
                        SelectedColor = Color.Empty;
                    }
                }
                else if (value is int argb)
                {
                    SelectedColor = Color.FromArgb(argb);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; } = false;

        public void Reset()
        {
            SelectedColor = Color.FromArgb(79, 70, 229);
            IsModified = false;
        }

        public void Clear() => Reset();

        [Category("Appearance")]
        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    IsModified = true;
                    ColorChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("#4F46E5")]
        public string HexCode
        {
            get => $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            set
            {
                if (TryParseHex(value, out var c))
                {
                    SelectedColor = c;
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables the desktop screen eyedropper button in the color picker popup.")]
        public bool ShowEyedropper { get; set; } = true;

        /// <summary>
        /// Attempts to parse a color from hex string representation (#RGB, #RRGGBB, #AARRGGBB, or without '#').
        /// </summary>
        public static bool TryParseHex(string? hex, out Color color)
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                string h = hex!.Trim();
                if (!h.StartsWith("#")) h = "#" + h;
                if (ZeroColor.TryParseHex(h, out var zc))
                {
                    color = Color.FromArgb(zc.A, zc.R, zc.G, zc.B);
                    return true;
                }
                try
                {
                    color = ColorTranslator.FromHtml(h);
                    return true;
                }
                catch { }
            }
            color = Color.Empty;
            return false;
        }

        /// <summary>
        /// Displays a modern modal ZeroUI Color Picker dialog using ColorPickEdit.
        /// </summary>
        /// <param name="owner">Optional parent window.</param>
        /// <param name="initialColor">Initial color to select.</param>
        /// <param name="title">Optional dialog title.</param>
        /// <returns>The chosen <see cref="Color"/>, or <c>null</c> if canceled.</returns>
        public static Color? PickColor(IWin32Window? owner, Color initialColor, string title = "Select Color")
        {
            using (var dlg = new Form())
            {
                dlg.Text = title;
                dlg.Font = new Font("Segoe UI", 9f);
                dlg.Size = new Size(340, 180);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.FromArgb(248, 250, 252);

                var lbl = new Label
                {
                    Text = "Choose Color or Enter Hex / Eyedropper:",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Top = 16,
                    Left = 20,
                    AutoSize = true
                };

                var editor = new ZColorPicker
                {
                    SelectedColor = initialColor,
                    Top = 42,
                    Left = 20,
                    Width = 284,
                    Height = 36
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Top = 96,
                    Left = 138,
                    Width = 80,
                    Height = 28,
                    FlatStyle = FlatStyle.System
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Top = 96,
                    Left = 224,
                    Width = 80,
                    Height = 28,
                    FlatStyle = FlatStyle.System
                };

                dlg.Controls.AddRange(new Control[] { lbl, editor, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return editor.SelectedColor;
                }
                return null;
            }
        }

        public ZColorPicker()
        {
            Size = new Size(160, 36);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            _popupControl = new ColorPickerPopupControl(this);
            _dropdown = new DropDownHost
            {
                Content = _popupControl
            };
            _dropdown.Closed += (s, e) =>
            {
                _isDroppedDown = false;
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            _popupControl?.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = CurrentPalette;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw Background & Border
            using (var path = CreateRoundedRectanglePath(bounds, 4))
            {
                using (var brush = new SolidBrush(colors.Surface))
                {
                    g.FillPath(brush, path);
                }

                Color borderColor = _isDroppedDown || _isFocused
                    ? colors.Primary
                    : (_isHovered ? colors.PrimaryHover : colors.Border);

                using (var pen = new Pen(borderColor, _isDroppedDown || _isFocused ? 1.5f : 1.0f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw Color Swatch Box
            int swatchSize = Height - 14;
            int swatchX = 7;
            int swatchY = 7;
            var swatchRect = new Rectangle(swatchX, swatchY, swatchSize, swatchSize);

            using (var sPath = CreateRoundedRectanglePath(swatchRect, 3))
            {
                using (var sBrush = new SolidBrush(_selectedColor))
                {
                    g.FillPath(sBrush, sPath);
                }
                using (var sPen = new Pen(colors.Border, 1f))
                {
                    g.DrawPath(sPen, sPath);
                }
            }

            // Draw Hex String
            string hexText = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            using (var brush = new SolidBrush(colors.TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };
                var textRect = new Rectangle(swatchX + swatchSize + 10, 0, Width - swatchSize - 36, Height);
                g.DrawString(hexText, Font, brush, textRect, sf);
            }

            // Draw Dropdown Arrow
            int arrowX = Width - 18;
            int arrowY = Height / 2;
            using (var pen = new Pen(colors.TextSecondary, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, arrowX, arrowY - 2, arrowX + 4, arrowY + 2);
                g.DrawLine(pen, arrowX + 4, arrowY + 2, arrowX + 8, arrowY - 2);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (ReadOnly || !Enabled) return;

            if (_isDroppedDown)
            {
                _dropdown.Close();
            }
            else
            {
                _isDroppedDown = true;
                _popupControl.SyncColor(_selectedColor);
                _dropdown.ShowDropDown(this, 220, 202);
                Invalidate();
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

        // Popup with Palette Swatches, Eyedropper, and RGB/Hex Controls
        private class ColorPickerPopupControl : Control
        {
            private readonly ZColorPicker _owner;
            private Color _currentColor;
            private readonly TextBox _hexBox;
            private Rectangle _eyedropperRect;
            private bool _hoverEyedropper = false;

            private static readonly Color[] Palette = new[]
            {
                Color.FromArgb(79, 70, 229),   // Indigo (Primary)
                Color.FromArgb(37, 99, 235),   // Blue
                Color.FromArgb(14, 165, 233),  // Sky
                Color.FromArgb(20, 184, 166),  // Teal
                Color.FromArgb(34, 197, 94),   // Green (Success)
                Color.FromArgb(234, 179, 8),   // Yellow (Warning)
                Color.FromArgb(249, 115, 22),  // Orange
                Color.FromArgb(239, 68, 68),   // Red (Danger)
                Color.FromArgb(236, 72, 153),  // Pink
                Color.FromArgb(168, 85, 247),  // Purple
                Color.FromArgb(15, 23, 42),    // Slate Dark
                Color.FromArgb(100, 116, 139), // Slate Muted
                Color.FromArgb(148, 163, 184), // Slate Light
                Color.FromArgb(226, 232, 240), // Slate Border
                Color.FromArgb(255, 255, 255), // Pure White
                Color.FromArgb(248, 250, 252), // Slate 50 (Neutral background)
                Color.FromArgb(238, 242, 255), // Indigo 50 (Blue tint background)
                Color.FromArgb(236, 253, 245), // Emerald 50 (Green tint background)
                Color.FromArgb(255, 251, 235), // Amber 50 (Warm tint background)
                Color.FromArgb(255, 241, 242)  // Rose 50 (Red tint background)
            };

            public ColorPickerPopupControl(ZColorPicker owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;

                _hexBox = new TextBox
                {
                    Location = new Point(12, 160),
                    Width = 70,
                    Font = new Font("Segoe UI", 9f)
                };
                _hexBox.TextChanged += (s, e) =>
                {
                    string text = _hexBox.Text.Trim();
                    if (TryParseHex(text, out var parsed))
                    {
                        _currentColor = parsed;
                        Invalidate();
                    }
                };
                _hexBox.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        TryApplyHex(_hexBox.Text);
                        e.SuppressKeyPress = true;
                    }
                };
                Controls.Add(_hexBox);
                _eyedropperRect = new Rectangle(88, 158, 28, 26);
            }

            public void SyncColor(Color color)
            {
                _currentColor = color;
                _hexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                Invalidate();
            }

            private void TryApplyHex(string hex)
            {
                if (TryParseHex(hex, out var parsed))
                {
                    _currentColor = parsed;
                    _owner.SelectedColor = _currentColor;
                    _owner._dropdown.Close();
                }
            }

            private void StartEyedropper()
            {
                _owner._dropdown.Close();
                var overlay = new EyedropperOverlay(
                    color =>
                    {
                        _owner.SelectedColor = color;
                    },
                    () => { });
                overlay.Show();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_owner.ShowEyedropper)
                {
                    bool hoverEye = _eyedropperRect.Contains(e.Location);
                    if (hoverEye != _hoverEyedropper)
                    {
                        _hoverEyedropper = hoverEye;
                        Cursor = _hoverEyedropper ? Cursors.Hand : Cursors.Default;
                        Invalidate();
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var colors = _owner.CurrentPalette;
                g.Clear(colors.Surface);

                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }

                // Draw Swatch Matrix (4 rows x 5 columns)
                int swatchSize = 28;
                int spacing = 8;
                int startX = 12;
                int startY = 12;

                for (int i = 0; i < Palette.Length; i++)
                {
                    int col = i % 5;
                    int row = i / 5;
                    int x = startX + col * (swatchSize + spacing);
                    int y = startY + row * (swatchSize + spacing);

                    var rect = new Rectangle(x, y, swatchSize, swatchSize);
                    using (var path = CreateRoundedRectanglePath(rect, 4))
                    {
                        using (var brush = new SolidBrush(Palette[i]))
                        {
                            g.FillPath(brush, path);
                        }
                        using (var pen = new Pen(colors.Border, 1f))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }

                // Draw Eyedropper Button
                if (_owner.ShowEyedropper)
                {
                    using (var eyePath = CreateRoundedRectanglePath(_eyedropperRect, 3))
                    {
                        if (_hoverEyedropper)
                        {
                            using (var bgBrush = new SolidBrush(colors.HeaderBackground))
                            {
                                g.FillPath(bgBrush, eyePath);
                            }
                        }
                        using (var borderPen = new Pen(_hoverEyedropper ? colors.Primary : colors.Border, 1f))
                        {
                            g.DrawPath(borderPen, eyePath);
                        }
                    }

                    // Vector Eyedropper Pipette Icon
                    Color iconColor = _hoverEyedropper ? colors.Primary : colors.TextSecondary;
                    using (var pen = new Pen(iconColor, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        int cx = _eyedropperRect.X + _eyedropperRect.Width / 2;
                        int cy = _eyedropperRect.Y + _eyedropperRect.Height / 2;
                        // Pipette angled from top-right to bottom-left
                        g.DrawLine(pen, cx - 4, cy + 4, cx + 2, cy - 2);
                        // Bulb at top right
                        g.DrawLine(pen, cx + 1, cy - 4, cx + 4, cy - 1);
                        // Nozzle tip
                        g.DrawLine(pen, cx - 5, cy + 5, cx - 6, cy + 6);
                    }
                }

                // Draw Color Preview Swatch
                int previewX = _owner.ShowEyedropper ? 122 : 90;
                int previewY = 158;
                int previewW = Width - previewX - 12;
                int previewH = 26;
                var pRect = new Rectangle(previewX, previewY, previewW, previewH);
                using (var path = CreateRoundedRectanglePath(pRect, 4))
                {
                    using (var brush = new SolidBrush(_currentColor))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(colors.Border, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                if (_owner.ShowEyedropper && _eyedropperRect.Contains(e.Location))
                {
                    StartEyedropper();
                    return;
                }

                // Click Preview Swatch to confirm and close
                int previewX = _owner.ShowEyedropper ? 122 : 90;
                int previewY = 158;
                int previewW = Width - previewX - 12;
                int previewH = 26;
                if (new Rectangle(previewX, previewY, previewW, previewH).Contains(e.Location))
                {
                    _owner.SelectedColor = _currentColor;
                    _owner._dropdown.Close();
                    return;
                }

                int swatchSize = 28;
                int spacing = 8;
                int startX = 12;
                int startY = 12;

                for (int i = 0; i < Palette.Length; i++)
                {
                    int col = i % 5;
                    int row = i / 5;
                    int x = startX + col * (swatchSize + spacing);
                    int y = startY + row * (swatchSize + spacing);

                    if (new Rectangle(x, y, swatchSize, swatchSize).Contains(e.Location))
                    {
                        _currentColor = Palette[i];
                        _owner.SelectedColor = _currentColor;
                        _owner._dropdown.Close();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for ColorPickEdit.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroColorPicker is deprecated and will be removed in 5 release cycles. Please migrate to ZColorPicker instead.")]
    [ToolboxItem(false)]
    public class ZeroColorPicker : ZColorPicker
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZColorPicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColorPickEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZColorPicker instead.")]
    [ToolboxItem(false)]
    public class ColorPickEdit : ZColorPicker
    {
    }

    #endregion
}
