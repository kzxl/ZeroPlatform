using System;


using System.ComponentModel;using ZeroUI.WinForms.Icons;using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    public enum ModalIconType
    {
        None,
        Info,
        Success,
        Warning,
        Error,
        Confirm,
        Prompt
    }

    /// <summary>
    /// Modern enterprise modal dialog with rounded container, backdrop dimming,
    /// and built-in notification dialogs (Success, Warning, Error, Info, Confirm, Prompt).
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ModalDialog.bmp")]
    public class ZModalDialog : Form
    {
        private readonly string _title;
        private readonly Control _contentControl;
        private readonly Action? _onOk;
        private readonly Action? _onCancel;
        private readonly string _okText;
        private readonly string _cancelText;
        private readonly bool _showCancel;

        private Rectangle _cardRect;
        private Rectangle _closeRect;
        private bool _isCloseHovered = false;
        private readonly ZButton _btnOk;
        private readonly ZButton? _btnCancel;

        public ZModalDialog(
            string title,
            Control contentControl,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int cardWidth = 520,
            int cardHeight = 340)
        {
            _title = title;
            _contentControl = contentControl;
            _onOk = onOk;
            _onCancel = onCancel;
            _okText = okText;
            _cancelText = cancelText;
            _showCancel = showCancel;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            Size = new Size(cardWidth, cardHeight);
            BackColor = ZeroTheme.Colors.Surface;
            Opacity = 1.0;

            _cardRect = new Rectangle(0, 0, cardWidth - 1, cardHeight - 1);

            // Setup isolated body container inside card
            var bodyPanel = new Panel
            {
                Location = new Point(20, 56),
                Size = new Size(cardWidth - 40, cardHeight - 118),
                BackColor = Color.Transparent
            };
            _contentControl.Dock = DockStyle.Fill;
            bodyPanel.Controls.Add(_contentControl);
            Controls.Add(bodyPanel);

            // OK Button
            _btnOk = new ZButton
            {
                Text = _okText,
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(100, 36),
                Location = new Point(cardWidth - 120, cardHeight - 48)
            };
            _btnOk.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                _onOk?.Invoke();
                Close();
            };
            Controls.Add(_btnOk);

            // Cancel Button (if enabled)
            if (_showCancel)
            {
                _btnCancel = new ZButton
                {
                    Text = _cancelText,
                    ButtonStyle = ZeroButtonStyle.Secondary,
                    Size = new Size(100, 36),
                    Location = new Point(cardWidth - 230, cardHeight - 48)
                };
                _btnCancel.Click += (s, e) =>
                {
                    DialogResult = DialogResult.Cancel;
                    _onCancel?.Invoke();
                    Close();
                };
                Controls.Add(_btnCancel);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                _onCancel?.Invoke();
                Close();
            }
        }

        public static DialogResult Show(
            IWin32Window parent,
            string title,
            Control content,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int width = 520,
            int height = 340)
        {
            using var modal = new ZModalDialog(title, content, onOk, onCancel, okText, cancelText, showCancel, width, height);
            return modal.ShowDialog(parent);
        }

        /// <summary>
        /// Displays an enterprise Success modal dialog with a prominent green checkmark badge.
        /// </summary>
        public static DialogResult Success(
            IWin32Window parent,
            string title,
            string message,
            Action? onOk = null,
            string okText = "OK")
        {
            var pnl = new MessageDialogPanel(ModalIconType.Success, message);
            return Show(parent, title, pnl, onOk, null, okText, "", false, 460, 240);
        }

        /// <summary>
        /// Displays an enterprise Warning modal dialog with an amber alert badge.
        /// </summary>
        public static DialogResult Warning(
            IWin32Window parent,
            string title,
            string message,
            Action? onOk = null,
            string okText = "OK")
        {
            var pnl = new MessageDialogPanel(ModalIconType.Warning, message);
            return Show(parent, title, pnl, onOk, null, okText, "", false, 460, 240);
        }

        /// <summary>
        /// Displays an enterprise Error modal dialog with a crimson error badge.
        /// </summary>
        public static DialogResult Error(
            IWin32Window parent,
            string title,
            string message,
            Action? onOk = null,
            string okText = "Close")
        {
            var pnl = new MessageDialogPanel(ModalIconType.Error, message);
            return Show(parent, title, pnl, onOk, null, okText, "", false, 460, 240);
        }

        /// <summary>
        /// Displays an enterprise Information modal dialog with an indigo info badge.
        /// </summary>
        public static DialogResult Info(
            IWin32Window parent,
            string title,
            string message,
            Action? onOk = null,
            string okText = "OK")
        {
            var pnl = new MessageDialogPanel(ModalIconType.Info, message);
            return Show(parent, title, pnl, onOk, null, okText, "", false, 460, 240);
        }

        /// <summary>
        /// Displays an enterprise Confirm modal dialog with Question badge and Ok/Cancel buttons.
        /// </summary>
        public static DialogResult Confirm(
            IWin32Window parent,
            string title,
            string message,
            Action onConfirm,
            Action? onCancel = null,
            string confirmText = "Confirm",
            string cancelText = "Cancel")
        {
            var pnl = new MessageDialogPanel(ModalIconType.Confirm, message);
            return Show(parent, title, pnl, onConfirm, onCancel, confirmText, cancelText, true, 460, 240);
        }

        /// <summary>
        /// Displays an enterprise Prompt modal dialog allowing users to enter a text value or barcode.
        /// </summary>
        public static DialogResult Prompt(
            IWin32Window parent,
            string title,
            string message,
            string defaultValue,
            Action<string> onOk,
            Action? onCancel = null,
            string okText = "Save",
            string cancelText = "Cancel")
        {
            var pnl = new PromptDialogPanel(message, defaultValue);
            return Show(parent, title, pnl, () => onOk(pnl.InputText), onCancel, okText, cancelText, true, 480, 260);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var theme = ZeroTheme.Colors;

            // 1. Draw Card Background & Crisp Border
            int effModalRadius = ZeroUIConfig.GetEffectiveRadius(8);
            using (var path = CreateRoundedRectangle(_cardRect, effModalRadius))
            {
                using var cardBrush = new SolidBrush(theme.Surface);
                g.FillPath(cardBrush, path);

                using var borderPen = new Pen(theme.Border, 1.2f);
                g.DrawPath(borderPen, path);
            }

            // 2. Draw Header (Title & Divider)
            int headerY = 14;
            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            Rectangle titleRect = new Rectangle(20, headerY, Width - 65, 24);
            TextRenderer.DrawText(g, _title, titleFont, titleRect, theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using (var divPen = new Pen(theme.Border, 1f))
            {
                g.DrawLine(divPen, 0, 48, Width, 48);
                g.DrawLine(divPen, 0, Height - 58, Width, Height - 58);
            }

            // 3. Draw Close (✕) Button
            _closeRect = new Rectangle(Width - 36, 14, 24, 24);
            if (_isCloseHovered)
            {
                int effCloseRadius = ZeroUIConfig.GetEffectiveRadius(4);
                using var bPath = CreateRoundedRectangle(_closeRect, effCloseRadius);
                using var bBrush = new SolidBrush(theme.Hover);
                g.FillPath(bBrush, bPath);
            }

            TextRenderer.DrawText(
                g,
                "✕",
                ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Bold),
                _closeRect,
                _isCloseHovered ? theme.TextPrimary : theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_closeRect.IsEmpty && _closeRect.Contains(e.Location))
            {
                if (!_isCloseHovered)
                {
                    _isCloseHovered = true;
                    Cursor = Cursors.Hand;
                    Invalidate(_closeRect);
                }
            }
            else if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_closeRect);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                if (!_closeRect.IsEmpty && _closeRect.Contains(e.Location))
                {
                    DialogResult = DialogResult.Cancel;
                    _onCancel?.Invoke();
                    Close();
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        /// <summary>
        /// Inner panel rendering large semantic icon badge and wrapped message text.
        /// </summary>
        private class MessageDialogPanel : Control
        {
            private readonly ModalIconType _iconType;
            private readonly string _message;

            public MessageDialogPanel(ModalIconType iconType, string message)
            {
                _iconType = iconType;
                _message = message;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = ZeroTheme.Colors;

                // 1. Determine Badge Color & Glyph
                Color badgeColor = _iconType switch
                {
                    ModalIconType.Success => palette.Success,
                    ModalIconType.Warning => palette.Warning,
                    ModalIconType.Error => palette.Danger,
                    ModalIconType.Confirm => palette.Primary,
                    _ => palette.Info
                };

                string glyph = _iconType switch
                {
                    ModalIconType.Success => "✔",
                    ModalIconType.Warning => "⚠",
                    ModalIconType.Error => "✕",
                    ModalIconType.Confirm => "?",
                    _ => "ℹ"
                };

                // 2. Draw 52px Circular Icon Badge
                int iconSz = 52;
                var iconRect = new Rectangle(8, (Height - iconSz) / 2, iconSz, iconSz);

                using (var brushHalo = new SolidBrush(Color.FromArgb(30, badgeColor)))
                {
                    g.FillEllipse(brushHalo, iconRect);
                }

                using (var penBadge = new Pen(badgeColor, 1.8f))
                {
                    g.DrawEllipse(penBadge, iconRect);
                }

                using (var fontGlyph = new Font("Segoe UI", 18f, FontStyle.Bold))
                using (var brushGlyph = new SolidBrush(badgeColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(glyph, fontGlyph, brushGlyph, iconRect, sf);
                }

                // 3. Draw Message Text with Auto-Wrap
                int textX = iconRect.Right + 18;
                var textRect = new Rectangle(textX, 8, Width - textX - 8, Height - 16);

                using (var brushMsg = new SolidBrush(palette.TextPrimary))
                {
                    var sfMsg = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.LineLimit
                    };
                    g.DrawString(_message, Font, brushMsg, textRect, sfMsg);
                }
            }
        }

        /// <summary>
        /// Inner panel rendering prompt message and input text box.
        /// </summary>
        private class PromptDialogPanel : Control
        {
            private readonly string _message;
            private readonly TextBox _textBox;

            public string InputText => _textBox.Text;

            public PromptDialogPanel(string message, string defaultValue)
            {
                _message = message;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

                _textBox = new TextBox
                {
                    Text = defaultValue,
                    Font = new Font("Segoe UI", 10f),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = ZeroTheme.Colors.Surface,
                    ForeColor = ZeroTheme.Colors.TextPrimary
                };
                Controls.Add(_textBox);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                _textBox.Location = new Point(10, Height - 36);
                _textBox.Size = new Size(Width - 20, 28);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = ZeroTheme.Colors;

                // Prompt Icon (✏️)
                using (var iconFont = new Font("Segoe UI Emoji", 14f))
                using (var brushIcon = new SolidBrush(palette.Primary))
                {
                    g.DrawString("✏️", iconFont, brushIcon, 8, 8);
                }

                // Prompt Message
                var msgRect = new Rectangle(40, 8, Width - 50, Height - 48);
                using (var brushMsg = new SolidBrush(palette.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                    g.DrawString(_message, Font, brushMsg, msgRect, sf);
                }
            }
        }
    }

    #region Backward Compatibility Shims

    /// <summary>
    /// Legacy shim for ModalDialog.
    /// </summary>
    [Obsolete("ModalDialog is deprecated and will be removed in 5 release cycles. Please migrate to ZModalDialog instead.")]
    [ToolboxItem(false)]
    public class ModalDialog : ZModalDialog
    {
        public ModalDialog(
            string title,
            Control contentControl,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int cardWidth = 520,
            int cardHeight = 340)
            : base(title, contentControl, onOk, onCancel, okText, cancelText, showCancel, cardWidth, cardHeight)
        {
        }
    }

    /// <summary>
    /// Canonical Z-prefixed short alias for <see cref="ZModalDialog"/>.
    /// </summary>
    [ToolboxItem(false)]
    public class ZModal : ZModalDialog
    {
        public ZModal(
            string title,
            Control contentControl,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int cardWidth = 520,
            int cardHeight = 340)
            : base(title, contentControl, onOk, onCancel, okText, cancelText, showCancel, cardWidth, cardHeight)
        {
        }
    }

    /// <summary>
    /// Deprecated ZeroModal shim.
    /// </summary>
    [Obsolete("ZeroModal is deprecated and will be removed in 5 release cycles. Please migrate to ZModalDialog instead.")]
    [ToolboxItem(false)]
    public class ZeroModal : ZModalDialog
    {
        public ZeroModal(
            string title,
            Control contentControl,
            Action? onOk = null,
            Action? onCancel = null,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            int cardWidth = 520,
            int cardHeight = 340)
            : base(title, contentControl, onOk, onCancel, okText, cancelText, showCancel, cardWidth, cardHeight)
        {
        }
    }

    #endregion
}