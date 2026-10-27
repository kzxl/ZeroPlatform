using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    public enum DropDownAlignment
    {
        Below,
        Above,
        Auto
    }

    /// <summary>
    /// Lightweight non-activating drop-down popup host for ZeroUI editors.
    /// Provides screen boundary clamping, automatic DropUp/DropDown flipping,
    /// seamless focus management, and unified theme styling.
    /// </summary>
    public class DropDownHost : ToolStripDropDown
    {
        private Control? _content;
        private ToolStripControlHost? _host;

        public Control? Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    Items.Clear();
                    _content = value;
                    if (_content != null)
                    {
                        _host = new ToolStripControlHost(_content)
                        {
                            Margin = Padding.Empty,
                            Padding = Padding.Empty,
                            AutoSize = false
                        };
                        Items.Add(_host);
                    }
                }
            }
        }

        public DropDownHost()
        {
            AutoClose = true;
            DropShadowEnabled = true;
            DoubleBuffered = true;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            TabStop = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // Add WS_CLIPSIBLINGS and WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                cp.ExStyle |= unchecked((int)(0x00000080 | 0x08000000)); // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                return cp;
            }
        }

        /// <summary>
        /// Displays the dropdown positioned relative to the owner editor with automatic boundary detection.
        /// </summary>
        public void ShowDropDown(Control owner, int width, int height, DropDownAlignment alignment = DropDownAlignment.Auto)
        {
            if (owner == null || !owner.IsHandleCreated || _content == null || _host == null)
            {
                return;
            }

            _content.Size = new Size(width, height);
            _host.Size = new Size(width, height);
            Size = new Size(width, height);

            Point screenPt = owner.PointToScreen(Point.Empty);
            Rectangle workArea = Screen.FromControl(owner).WorkingArea;

            int popupX = screenPt.X;
            int popupY = screenPt.Y + owner.Height;

            // Horizontal Clamping
            if (popupX + width > workArea.Right)
            {
                popupX = workArea.Right - width;
            }
            if (popupX < workArea.Left)
            {
                popupX = workArea.Left;
            }

            // Vertical Alignment and Auto-Flip
            if (alignment == DropDownAlignment.Above ||
                (alignment == DropDownAlignment.Auto && (popupY + height > workArea.Bottom) && (screenPt.Y - height >= workArea.Top)))
            {
                // Flip upward (DropUp mode)
                popupY = screenPt.Y - height;
            }
            else
            {
                if (popupY + height > workArea.Bottom)
                {
                    popupY = workArea.Bottom - height;
                }
            }

            Show(popupX, popupY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw 1px clean theme border
            var palette = ZeroTheme.Colors;
            using (var pen = new Pen(palette.Border, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="DropDownHost"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use DropDownHost instead.")]
    public class ZeroDropDownHost : DropDownHost
    {
    }
}

