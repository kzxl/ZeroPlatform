using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern WinForms action slot text editor with configurable embedded action buttons.
    /// Provides built-in support for File/Folder pickers, clipboard copy, and 1-click clear.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Text")]
    [Description("Text editor with embedded action buttons for file/folder pickers, copy, and search")]
    [ToolboxBitmap(typeof(ZeroIcons), "TextEdit.bmp")]
    public class ZButtonEdit : ZTextBox
    {
        private readonly List<EditorButtonModel> _buttons = new List<EditorButtonModel>();
        private readonly List<Rectangle> _buttonRects = new List<Rectangle>();
        private int _hoveredButtonIndex = -1;
        private int _pressedButtonIndex = -1;

        [Category("Behavior")]
        [DefaultValue("All Files (*.*)|*.*")]
        public string FileFilter { get; set; } = "All Files (*.*)|*.*";

        [Category("Behavior")]
        [DefaultValue("Select File")]
        public string DialogTitle { get; set; } = "Select File";

        [Category("Behavior")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<EditorButtonModel> Buttons => _buttons;

        public event EventHandler<EditorButtonClickEventArgs>? ButtonClick;

        public ZButtonEdit()
        {
            // Default with BrowseFile button
            _buttons.Add(new EditorButtonModel(EditorButtonKind.BrowseFile));
            MouseMove += OnButtonMouseMove;
            MouseLeave += OnButtonMouseLeave;
            MouseDown += OnButtonMouseDown;
            MouseUp += OnButtonMouseUp;
        }

        public void AddButton(EditorButtonKind kind, string? toolTip = null)
        {
            _buttons.Add(new EditorButtonModel(kind, toolTip));
            Invalidate();
        }

        private void OnButtonMouseMove(object? sender, MouseEventArgs e)
        {
            int oldHover = _hoveredButtonIndex;
            _hoveredButtonIndex = -1;

            for (int i = 0; i < _buttonRects.Count; i++)
            {
                if (_buttonRects[i].Contains(e.Location))
                {
                    _hoveredButtonIndex = i;
                    Cursor = Cursors.Hand;
                    break;
                }
            }

            if (_hoveredButtonIndex == -1)
            {
                Cursor = Cursors.Default;
            }

            if (oldHover != _hoveredButtonIndex)
            {
                Invalidate();
            }
        }

        private void OnButtonMouseLeave(object? sender, EventArgs e)
        {
            if (_hoveredButtonIndex != -1)
            {
                _hoveredButtonIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        private void OnButtonMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _hoveredButtonIndex >= 0)
            {
                _pressedButtonIndex = _hoveredButtonIndex;
                Invalidate();
            }
        }

        private void OnButtonMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _pressedButtonIndex >= 0)
            {
                int clickedIndex = _pressedButtonIndex;
                _pressedButtonIndex = -1;
                Invalidate();

                if (clickedIndex < _buttons.Count && _buttonRects.Count > clickedIndex && _buttonRects[clickedIndex].Contains(e.Location))
                {
                    var btn = _buttons[clickedIndex];
                    var args = new EditorButtonClickEventArgs(btn.Kind, btn.Tag);
                    ButtonClick?.Invoke(this, args);

                    if (!args.Handled)
                    {
                        ExecuteDefaultAction(btn);
                    }
                }
            }
        }

        protected virtual void ExecuteDefaultAction(EditorButtonModel btn)
        {
            switch (btn.Kind)
            {
                case EditorButtonKind.BrowseFile:
                    using (var dlg = new OpenFileDialog())
                    {
                        dlg.Filter = FileFilter;
                        dlg.Title = DialogTitle;
                        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                        {
                            Text = dlg.FileName;
                        }
                    }
                    break;

                case EditorButtonKind.BrowseFolder:
                    using (var dlg = new FolderBrowserDialog())
                    {
                        dlg.Description = DialogTitle;
                        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                        {
                            Text = dlg.SelectedPath;
                        }
                    }
                    break;

                case EditorButtonKind.Clear:
                    Clear();
                    break;

                case EditorButtonKind.Copy:
                    if (!string.IsNullOrEmpty(Text))
                    {
                        Clipboard.SetText(Text);
                    }
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            _buttonRects.Clear();
            if (_buttons.Count == 0) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int btnWidth = 24;
            int btnHeight = Height - 8;
            int currentRight = Width - 6;

            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                var btn = _buttons[i];
                if (!btn.Visible) continue;

                var rect = new Rectangle(currentRight - btnWidth, 4, btnWidth, btnHeight);
                _buttonRects.Insert(0, rect);
                currentRight -= (btnWidth + 2);

                // Background highlight
                if (i == _pressedButtonIndex)
                {
                    using var brush = new SolidBrush(Color.FromArgb(40, ZeroTheme.Colors.Primary));
                    g.FillRectangle(brush, rect);
                }
                else if (i == _hoveredButtonIndex)
                {
                    using var brush = new SolidBrush(Color.FromArgb(25, ZeroTheme.Colors.TextPrimary));
                    g.FillRectangle(brush, rect);
                }

                // Glyph Icon
                Color iconColor = (i == _hoveredButtonIndex) ? ZeroTheme.Colors.Primary : ZeroTheme.Colors.TextSecondary;
                using var pen = new Pen(iconColor, 1.5f);

                DrawButtonGlyph(g, btn.Kind, rect, pen, iconColor);
            }
        }

        private static void DrawButtonGlyph(Graphics g, EditorButtonKind kind, Rectangle rect, Pen pen, Color color)
        {
            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;

            switch (kind)
            {
                case EditorButtonKind.BrowseFile:
                case EditorButtonKind.BrowseFolder:
                    // Folder glyph
                    g.DrawRectangle(pen, cx - 6, cy - 4, 12, 9);
                    g.DrawLine(pen, cx - 6, cy - 4, cx - 3, cy - 7);
                    g.DrawLine(pen, cx - 3, cy - 7, cx + 1, cy - 7);
                    g.DrawLine(pen, cx + 1, cy - 7, cx + 3, cy - 4);
                    break;

                case EditorButtonKind.Clear:
                    // 'X' glyph
                    g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
                    break;

                case EditorButtonKind.Copy:
                    // Double sheet glyph
                    g.DrawRectangle(pen, cx - 5, cy - 3, 7, 8);
                    g.DrawRectangle(pen, cx - 2, cy - 6, 7, 8);
                    break;

                case EditorButtonKind.Search:
                    // Magnifying glass
                    g.DrawEllipse(pen, cx - 5, cy - 5, 7, 7);
                    g.DrawLine(pen, cx + 1, cy + 1, cx + 5, cy + 5);
                    break;

                case EditorButtonKind.DropDown:
                    // Chevron down
                    Point[] pts = { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) };
                    using (var br = new SolidBrush(color)) g.FillPolygon(br, pts);
                    break;

                default:
                    // Generic ellipsis
                    using (var br = new SolidBrush(color))
                    {
                        g.FillEllipse(br, cx - 5, cy - 1, 2, 2);
                        g.FillEllipse(br, cx - 1, cy - 1, 2, 2);
                        g.FillEllipse(br, cx + 3, cy - 1, 2, 2);
                    }
                    break;
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZButtonEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ButtonEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonEdit instead.")]
    [ToolboxItem(false)]
    public class ButtonEdit : ZButtonEdit
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZButtonEdit"/>.
    /// </summary>
    [Obsolete("ZeroButtonEdit is deprecated. Please use ZButtonEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroButtonEdit : ZButtonEdit
    {
    }

    #endregion
}
