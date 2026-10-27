using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Overlays
{
    public class ZCommandPalette : Form
    {
        private static readonly List<CommandItem> _registeredCommands = new List<CommandItem>();
        private readonly List<CommandItem> _commands;
        private List<CommandItem> _filteredCommands;
        private int _selectedIndex = 0;
        
        private TextBox _searchBox;
        private Panel _cardPanel;
        private CommandListControl _listControl;

        private ZCommandPalette(IEnumerable<CommandItem> commands)
        {
            _commands = commands.ToList();
            _filteredCommands = CommandFilterHelper.Filter(_commands, "");

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            
            // Transparency key trick for the form background
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            
            // Set size to something large enough to cover typical parent, but we will resize in OnLoad
            Size = new Size(800, 600);
            
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            int cardWidth = 500;
            int cardHeight = 400;

            _cardPanel = new Panel
            {
                Size = new Size(cardWidth, cardHeight),
                BackColor = ZeroTheme.Colors.Surface,
                Location = new Point((Width - cardWidth) / 2, (Height - cardHeight) / 2),
                Padding = new Padding(1)
            };

            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = ZeroFontCache.Get("Segoe UI", 14f, FontStyle.Regular),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Dock = DockStyle.Top,
                Margin = new Padding(10)
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            _searchBox.KeyDown += SearchBox_KeyDown;

            // Search box container for padding and bottom border
            var searchContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = Color.Transparent
            };
            searchContainer.Controls.Add(_searchBox);
            searchContainer.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border, 1f);
                e.Graphics.DrawLine(pen, 0, searchContainer.Height - 1, searchContainer.Width, searchContainer.Height - 1);
            };

            _listControl = new CommandListControl(this)
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Surface
            };

            _cardPanel.Controls.Add(_listControl);
            _cardPanel.Controls.Add(searchContainer);

            Controls.Add(_cardPanel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (Owner != null)
            {
                Bounds = Owner.Bounds;
                _cardPanel.Location = new Point((Width - _cardPanel.Width) / 2, (Height - _cardPanel.Height) / 2);
            }
            _searchBox.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            e.Graphics.FillRectangle(brush, ClientRectangle);
            
            // Draw card border
            using var borderPen = new Pen(ZeroTheme.Colors.Border, 1f);
            e.Graphics.DrawRectangle(borderPen, new Rectangle(_cardPanel.Left - 1, _cardPanel.Top - 1, _cardPanel.Width + 1, _cardPanel.Height + 1));
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            _filteredCommands = CommandFilterHelper.Filter(_commands, _searchBox.Text);
            _selectedIndex = 0;
            _listControl.Invalidate();
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (_selectedIndex < _filteredCommands.Count - 1)
                    _selectedIndex++;
                _listControl.Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_selectedIndex > 0)
                    _selectedIndex--;
                _listControl.Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ExecuteSelected()
        {
            if (_filteredCommands.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _filteredCommands.Count)
            {
                var cmd = _filteredCommands[_selectedIndex];
                if (cmd.IsEnabled)
                {
                    Close();
                    cmd.Execute?.Invoke();
                }
            }
        }

        public static void Show(IWin32Window owner, IEnumerable<CommandItem> commands)
        {
            var combined = _registeredCommands.Concat(commands).ToList();
            using var palette = new ZCommandPalette(combined);
            palette.ShowDialog(owner);
        }

        public static void Register(string id, string label, string? category, string? shortcut, Action execute)
        {
            Unregister(id);
            _registeredCommands.Add(new CommandItem
            {
                Id = id,
                Label = label,
                Category = category,
                ShortcutText = shortcut,
                Execute = execute
            });
        }

        public static void Unregister(string id)
        {
            _registeredCommands.RemoveAll(x => x.Id == id);
        }

        private class CommandListControl : Control
        {
            private readonly ZCommandPalette _parent;
            private const int ItemHeight = 40;

            public CommandListControl(ZCommandPalette parent)
            {
                _parent = parent;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var items = _parent._filteredCommands;
                var palette = ZeroTheme.Colors;
                
                int y = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (y > Height) break;

                    var item = items[i];
                    var rect = new Rectangle(0, y, Width, ItemHeight);

                    if (i == _parent._selectedIndex)
                    {
                        using var hoverBrush = new SolidBrush(palette.Hover);
                        g.FillRectangle(hoverBrush, rect);
                    }

                    int textX = 16;

                    // Glyph
                    if (!string.IsNullOrEmpty(item.Glyph))
                    {
                        using var glyphFont = ZeroFontCache.Get("Segoe UI Emoji", 10f);
                        TextRenderer.DrawText(g, item.Glyph, glyphFont, new Point(textX, y + 10), palette.TextSecondary);
                        textX += 24;
                    }

                    // Label
                    using var labelFont = ZeroFontCache.Get("Segoe UI", 10f);
                    var textColor = item.IsEnabled ? palette.TextPrimary : palette.TextSecondary;
                    TextRenderer.DrawText(g, item.Label, labelFont, new Point(textX, y + 10), textColor);

                    // Category badge
                    if (!string.IsNullOrEmpty(item.Category))
                    {
                        int labelWidth = TextRenderer.MeasureText(item.Label, labelFont).Width;
                        var catRect = new Rectangle(textX + labelWidth + 8, y + 10, 60, 20); // Simplified badge positioning
                        using var catFont = ZeroFontCache.Get("Segoe UI", 8f);
                        using var catBrush = new SolidBrush(palette.Border);
                        g.FillRectangle(catBrush, catRect);
                        TextRenderer.DrawText(g, item.Category, catFont, catRect, palette.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }

                    // Shortcut
                    if (!string.IsNullOrEmpty(item.ShortcutText))
                    {
                        using var scFont = ZeroFontCache.Get("Segoe UI", 9f);
                        var scSize = TextRenderer.MeasureText(item.ShortcutText, scFont);
                        TextRenderer.DrawText(g, item.ShortcutText, scFont, new Point(Width - scSize.Width - 16, y + 10), palette.TextSecondary);
                    }

                    y += ItemHeight;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int index = e.Y / ItemHeight;
                if (index >= 0 && index < _parent._filteredCommands.Count && index != _parent._selectedIndex)
                {
                    _parent._selectedIndex = index;
                    Invalidate();
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.Button == MouseButtons.Left)
                {
                    _parent.ExecuteSelected();
                }
            }
        }
    }
}
