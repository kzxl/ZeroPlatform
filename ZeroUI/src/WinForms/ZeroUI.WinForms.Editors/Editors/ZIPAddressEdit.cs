using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern WinForms 4-octet segmented IPv4 address editor for ZeroUI.
    /// Provides auto-advancement on period (.) or space, numeric clamping (0-255),
    /// backspace navigation, and bidirectional IPAddress/string binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Text")]
    [Description("Industrial 4-octet segmented IPv4 address editor")]
    [ToolboxBitmap(typeof(ZeroIcons), "TextEdit.bmp")]
    public class ZIPAddressEdit : ControlBase, IZeroEditor
    {
        private readonly IPAddressModel _model = new IPAddressModel();
        private readonly TextBox[] _octets = new TextBox[4];
        private bool _isUpdating;

        public IPAddressModel Model => _model;

        [Category("Data")]
        [Description("The typed IPAddress value of the control.")]
        public IPAddress? Address
        {
            get => _model.Address;
            set
            {
                if (!_isUpdating && value != null)
                {
                    SetIPAddress(value);
                }
            }
        }

        [Category("Data")]
        [Description("The string representation of the IP address.")]
#pragma warning disable CS8765, CS8764
        public override string Text
        {
            get => _model.Text;
            set
            {
                if (!_isUpdating)
                {
                    UpdateOctetsFromText(value ?? "0.0.0.0");
                }
            }
        }
#pragma warning restore CS8765, CS8764

        [Category("Appearance")]
        [Description("The string representation of the IP address.")]
        public string Value
        {
            get => Text;
            set => Text = value;
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => _model.Address;
            set
            {
                if (value is IPAddress ip) Address = ip;
                else if (value is string s) Text = s;
                else Address = null;
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _octets[0].ReadOnly;
            set
            {
                for (int i = 0; i < 4; i++) _octets[i].ReadOnly = value;
            }
        }

        [Browsable(false)]
        public bool IsModified { get; set; }

        public event EventHandler? EditValueChanged;

        public ZIPAddressEdit()
        {
            Size = new Size(160, 32);
            BackColor = ZeroTheme.Colors.Surface;
            DoubleBuffered = true;

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var box = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    TextAlign = HorizontalAlignment.Center,
                    MaxLength = 3,
                    Font = Font,
                    BackColor = ZeroTheme.Colors.Surface,
                    ForeColor = ZeroTheme.Colors.TextPrimary,
                    Text = "0"
                };

                box.KeyPress += (s, e) =>
                {
                    if (ReadOnly) { e.Handled = true; return; }

                    if (e.KeyChar == '.' || e.KeyChar == ' ')
                    {
                        e.Handled = true;
                        if (index < 3)
                        {
                            _octets[index + 1].Focus();
                            _octets[index + 1].SelectAll();
                        }
                        return;
                    }

                    if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
                    {
                        e.Handled = true;
                    }
                };

                box.KeyDown += (s, e) =>
                {
                    if (ReadOnly) return;

                    if (e.KeyCode == Keys.Back && box.SelectionStart == 0 && box.SelectionLength == 0)
                    {
                        if (index > 0)
                        {
                            _octets[index - 1].Focus();
                            _octets[index - 1].SelectionStart = _octets[index - 1].Text.Length;
                            e.Handled = true;
                        }
                    }
                    else if (e.KeyCode == Keys.Right && box.SelectionStart == box.Text.Length && index < 3)
                    {
                        _octets[index + 1].Focus();
                        _octets[index + 1].SelectionStart = 0;
                        e.Handled = true;
                    }
                    else if (e.KeyCode == Keys.Left && box.SelectionStart == 0 && index > 0)
                    {
                        _octets[index - 1].Focus();
                        _octets[index - 1].SelectionStart = _octets[index - 1].Text.Length;
                        e.Handled = true;
                    }
                };

                box.TextChanged += (s, e) =>
                {
                    if (_isUpdating) return;

                    if (int.TryParse(box.Text, out int val))
                    {
                        _model.SetOctet(index, val);
                        if (val > 255)
                        {
                            box.Text = "255";
                            box.SelectionStart = box.Text.Length;
                        }
                        else if (val < 0)
                        {
                            box.Text = "0";
                        }

                        if (box.Text.Length == 3 || (val >= 26 && box.Text.Length >= 2))
                        {
                            if (index < 3 && box.Focused)
                            {
                                _octets[index + 1].Focus();
                                _octets[index + 1].SelectAll();
                            }
                        }
                    }

                    SyncValues();
                };

                _octets[i] = box;
                Controls.Add(box);
            }

            LayoutOctetBoxes();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutOctetBoxes();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = ZeroTheme.Colors.Surface;
            if (_octets != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_octets[i] != null)
                    {
                        _octets[i].BackColor = ZeroTheme.Colors.Surface;
                        _octets[i].ForeColor = ZeroTheme.Colors.TextPrimary;
                    }
                }
            }
            Invalidate();
        }

        private void LayoutOctetBoxes()
        {
            int totalW = Width - 8;
            int boxW = (totalW - 24) / 4;
            int boxH = _octets[0]?.PreferredHeight ?? 18;
            int top = (Height - boxH) / 2;

            for (int i = 0; i < 4; i++)
            {
                if (_octets[i] != null)
                {
                    int x = 4 + i * (boxW + 8);
                    _octets[i].SetBounds(x, top, boxW, boxH);
                }
            }
        }

        private void SyncValues()
        {
            if (_isUpdating) return;
            IsModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateBoxesFromModel()
        {
            _isUpdating = true;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    _octets[i].Text = _model[i].ToString();
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateOctetsFromText(string text)
        {
            _model.TrySetFromText(text);
            UpdateBoxesFromModel();
            SyncValues();
        }

        public void SetIPAddress(IPAddress address)
        {
            _model.Address = address;
            UpdateBoxesFromModel();
            SyncValues();
        }

        public void Clear()
        {
            UpdateOctetsFromText("0.0.0.0");
            IsModified = false;
        }

        public void Reset()
        {
            Clear();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw Border
            using (var pen = new Pen(ZeroTheme.Colors.Border, 1f))
            {
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                g.DrawRectangle(pen, r);
            }

            // Draw dot separators
            using var brush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
            using var font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);

            int totalW = Width - 8;
            int boxW = (totalW - 24) / 4;
            int cy = Height / 2 - 3;

            for (int i = 0; i < 3; i++)
            {
                int dotX = 4 + (i + 1) * boxW + i * 8 + 2;
                g.DrawString(".", font, brush, dotX, cy);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIPAddressEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IPAddressEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZIPAddressEdit instead.")]
    [ToolboxItem(false)]
    public class IPAddressEdit : ZIPAddressEdit
    {
    }

    #endregion
}
