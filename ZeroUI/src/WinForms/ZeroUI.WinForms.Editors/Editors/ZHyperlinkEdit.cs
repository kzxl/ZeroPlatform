using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern interactive hyperlink and URL editor for WinForms.
    /// Supports direct navigation, custom display text, editable mode with inline action launch button,
    /// and dark theme styling.
    /// </summary>
    [DefaultEvent(nameof(HyperlinkClick))]
    public class ZHyperlinkEdit : Control, IZeroEditor
    {
        private string _targetUrl = string.Empty;
        private string _displayText = string.Empty;
        private bool _isEditable;
        private bool _requireConfirmation;
        private bool _readOnly;
        private bool _isModified;
        private bool _isHovered;
        private bool _isLaunchHovered;
        private Rectangle _launchButtonRect;
        private TextBox? _innerBox;

        [Category("Data")]
        [DefaultValue("")]
        public string TargetUrl
        {
            get => _targetUrl;
            set
            {
                if (_targetUrl != value)
                {
                    _targetUrl = value ?? string.Empty;
                    if (_innerBox != null && _innerBox.Text != _targetUrl)
                        _innerBox.Text = _targetUrl;
                    _isModified = true;
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string DisplayText
        {
            get => _displayText;
            set
            {
                if (_displayText != value)
                {
                    _displayText = value ?? string.Empty;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsEditable
        {
            get => _isEditable;
            set
            {
                if (_isEditable != value)
                {
                    _isEditable = value;
                    UpdateEditableState();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool RequireConfirmation
        {
            get => _requireConfirmation;
            set => _requireConfirmation = value;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                if (_innerBox != null) _innerBox.ReadOnly = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => TargetUrl;
            set => TargetUrl = value?.ToString() ?? string.Empty;
        }

        public event EventHandler<HyperlinkNavigateEventArgs>? HyperlinkClick;
        public event EventHandler? EditValueChanged;

        public ZHyperlinkEdit()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            Height = 32;
            Width = 200;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        }

        private void UpdateEditableState()
        {
            if (_isEditable)
            {
                Cursor = Cursors.Default;
                if (_innerBox == null)
                {
                    _innerBox = new TextBox
                    {
                        BorderStyle = BorderStyle.None,
                        BackColor = ZeroTheme.Colors.BgInput,
                        ForeColor = ZeroTheme.Colors.TextPrimary,
                        Font = Font,
                        Location = new Point(8, (Height - 18) / 2),
                        Width = Width - 36,
                        Text = _targetUrl
                    };
                    _innerBox.TextChanged += (s, e) =>
                    {
                        _targetUrl = _innerBox.Text;
                        _isModified = true;
                        EditValueChanged?.Invoke(this, EventArgs.Empty);
                    };
                    _innerBox.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            Navigate();
                            e.Handled = true;
                        }
                    };
                    Controls.Add(_innerBox);
                }
                else
                {
                    _innerBox.Visible = true;
                }
            }
            else
            {
                Cursor = Cursors.Hand;
                if (_innerBox != null)
                {
                    _innerBox.Visible = false;
                }
            }
        }

        public void Reset()
        {
            TargetUrl = string.Empty;
            DisplayText = string.Empty;
            _isModified = false;
        }

        public void Clear() => Reset();

        public void Navigate()
        {
            if (string.IsNullOrWhiteSpace(TargetUrl)) return;

            HyperlinkHelper.TryCreateUri(TargetUrl, out var uri);
            var args = new HyperlinkNavigateEventArgs(TargetUrl, uri);
            HyperlinkClick?.Invoke(this, args);

            if (args.Cancel || args.Handled) return;

            if (_requireConfirmation)
            {
                var res = MessageBox.Show($"Open external link?\n\n{TargetUrl}", "Confirm Navigation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;
            }

            HyperlinkHelper.OpenTarget(TargetUrl);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_innerBox != null)
            {
                _innerBox.Location = new Point(8, (Height - 18) / 2);
                _innerBox.Width = Math.Max(20, Width - 36);
            }
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
            _isLaunchHovered = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool launchHover = _launchButtonRect.Contains(e.Location);
            if (launchHover != _isLaunchHovered)
            {
                _isLaunchHovered = launchHover;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (!_isEditable || _launchButtonRect.Contains(e.Location))
            {
                Navigate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            if (_isEditable)
            {
                // Editable Container Background & Border
                using (var bgBrush = new SolidBrush(ZeroTheme.Colors.BgInput))
                using (var path = CreateRoundedRectanglePath(bounds, 5))
                {
                    g.FillPath(bgBrush, path);
                }

                Color borderColor = Focused ? ZeroTheme.Colors.PrimaryAccent : (_isHovered ? ZeroTheme.Colors.PrimaryAccentDark : ZeroTheme.Colors.BorderDefault);
                using (var borderPen = new Pen(borderColor, 1f))
                using (var path = CreateRoundedRectanglePath(bounds, 5))
                {
                    g.DrawPath(borderPen, path);
                }

                // Action launch button on right
                int btnW = 24;
                _launchButtonRect = new Rectangle(Width - btnW - 3, 3, btnW, Height - 6);
                if (_isLaunchHovered)
                {
                    using var hBrush = new SolidBrush(Color.FromArgb(40, ZeroTheme.Colors.PrimaryAccent));
                    g.FillRectangle(hBrush, _launchButtonRect);
                }

                // Launch glyph ↗
                using var glyphFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                using var glyphBrush = new SolidBrush(ZeroTheme.Colors.PrimaryAccent);
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("↗", glyphFont, glyphBrush, _launchButtonRect, sfCenter);
            }
            else
            {
                _launchButtonRect = Rectangle.Empty;

                if (_isHovered)
                {
                    using var hBrush = new SolidBrush(Color.FromArgb(20, ZeroTheme.Colors.PrimaryAccent));
                    using var path = CreateRoundedRectanglePath(bounds, 4);
                    g.FillPath(hBrush, path);
                }

                string text = !string.IsNullOrEmpty(_displayText) ? _displayText : (!string.IsNullOrEmpty(_targetUrl) ? _targetUrl : "https://...");
                Color linkColor = _isHovered ? ZeroTheme.Colors.PrimaryAccentDark : ZeroTheme.Colors.PrimaryAccent;

                using var linkFont = new Font(Font.FontFamily, 9.5f, _isHovered ? FontStyle.Underline : FontStyle.Regular);
                using var linkBrush = new SolidBrush(linkColor);
                var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
                var textRect = new Rectangle(6, 0, Width - 12, Height);
                g.DrawString(text, linkFont, linkBrush, textRect, sf);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHyperlinkEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HyperlinkEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZHyperlinkEdit instead.")]
    [ToolboxItem(false)]
    public class HyperlinkEdit : ZHyperlinkEdit
    {
    }

    #endregion
}
