using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Runtime Column Customization tool window for ZeroUI WinForms GridControl.
    /// Allows users to view hidden columns and restore their visibility via double-click or button.
    /// </summary>
    public class ColumnChooserDialog : Form
    {
        private readonly ZGrid _grid;
        private readonly ListBox _hiddenColumnsList;
        private readonly Button _btnShowColumn;
        private readonly Button _btnShowAll;
        private readonly Label _lblInfo;

        public ColumnChooserDialog(ZGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

            Text = "Customization";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(280, 380);
            MinimumSize = new Size(220, 260);
            TopMost = true;
            ShowInTaskbar = false;

            var colors = ZeroTheme.Colors;
            BackColor = colors.Surface;
            ForeColor = colors.TextPrimary;

            _lblInfo = new Label
            {
                Text = "Double-click column to restore in grid:",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = colors.TextSecondary,
                Padding = new Padding(8, 6, 8, 2)
            };
            Controls.Add(_lblInfo);

            _hiddenColumnsList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = colors.Background,
                ForeColor = colors.TextPrimary,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                DisplayMember = nameof(ZeroColumn.HeaderText),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30
            };

            _hiddenColumnsList.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _hiddenColumnsList.Items.Count) return;
                if (_hiddenColumnsList.Items[e.Index] is not ZeroColumn col) return;

                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                var g = e.Graphics;
                var bounds = e.Bounds;
                var themeColors = ZeroTheme.Colors;

                // 1. Background
                using (var brushBg = new SolidBrush(isSelected ? Color.FromArgb(228, 236, 252) : themeColors.Background))
                {
                    g.FillRectangle(brushBg, bounds);
                }

                if (isSelected)
                {
                    using var penBorder = new Pen(Color.FromArgb(180, 205, 245));
                    g.DrawRectangle(penBorder, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                }

                // 2. Icon (▦)
                using (var brushIcon = new SolidBrush(isSelected ? themeColors.Primary : themeColors.TextSecondary))
                {
                    using var fIcon = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                    g.DrawString("▦", fIcon, brushIcon, bounds.X + 8, bounds.Y + 6);
                }

                // 3. Column Title Text
                string title = !string.IsNullOrEmpty(col.HeaderText) ? col.HeaderText : col.FieldName;
                if (string.IsNullOrWhiteSpace(title)) title = $"Column #{e.Index + 1}";

                using (var brushText = new SolidBrush(isSelected ? Color.FromArgb(18, 86, 209) : themeColors.TextPrimary))
                {
                    using var fText = new Font("Segoe UI", 9.25f, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    var rectText = new Rectangle(bounds.X + 28, bounds.Y, bounds.Width - 36, bounds.Height);
                    using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(title, fText, brushText, rectText, sf);
                }
            };

            _hiddenColumnsList.DoubleClick += (s, e) => ShowSelectedColumn();
            _hiddenColumnsList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    ShowSelectedColumn();
                    e.Handled = true;
                }
            };
            Controls.Add(_hiddenColumnsList);

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = colors.Surface,
                Padding = new Padding(8)
            };

            _btnShowColumn = new Button
            {
                Text = "➕ Add Column",
                Dock = DockStyle.Left,
                Width = 110,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BackColor = colors.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnShowColumn.FlatAppearance.BorderSize = 0;
            _btnShowColumn.Click += (s, e) => ShowSelectedColumn();
            bottomPanel.Controls.Add(_btnShowColumn);

            _btnShowAll = new Button
            {
                Text = "Show All",
                Dock = DockStyle.Right,
                Width = 90,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = colors.Background,
                ForeColor = colors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnShowAll.FlatAppearance.BorderColor = colors.Border;
            _btnShowAll.Click += (s, e) => ShowAllColumns();
            bottomPanel.Controls.Add(_btnShowAll);

            Controls.Add(bottomPanel);
            _hiddenColumnsList.BringToFront();

            RefreshColumns();
        }

        public void RefreshColumns()
        {
            _hiddenColumnsList.BeginUpdate();
            _hiddenColumnsList.Items.Clear();

            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                var col = _grid.Columns[i];
                if (!col.IsVisible)
                {
                    _hiddenColumnsList.Items.Add(col);
                }
            }

            _hiddenColumnsList.EndUpdate();
            _btnShowColumn.Enabled = _hiddenColumnsList.Items.Count > 0;
            _btnShowAll.Enabled = _hiddenColumnsList.Items.Count > 0;
        }

        private void ShowSelectedColumn()
        {
            if (_hiddenColumnsList.SelectedItem is ZeroColumn col)
            {
                col.IsVisible = true;
                _grid.UpdateScrollBars();
                _grid.Invalidate();
                RefreshColumns();
            }
        }

        private void ShowAllColumns()
        {
            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                _grid.Columns[i].IsVisible = true;
            }
            _grid.UpdateScrollBars();
            _grid.Invalidate();
            RefreshColumns();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }
    }
}
