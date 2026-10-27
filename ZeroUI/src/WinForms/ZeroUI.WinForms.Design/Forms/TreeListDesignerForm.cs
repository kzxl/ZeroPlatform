using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Data;

namespace ZeroUI.WinForms.Design.Forms
{
    /// <summary>
    /// Visual In-Place Schema & Column Designer Form for ZTreeList.
    /// </summary>
    public class TreeListDesignerForm : Form
    {
        private readonly ZTreeList _treeList;
        private readonly List<TreeColumnDefinition> _workingColumns = new List<TreeColumnDefinition>();

        private ListBox _lstColumns = null!;
        private PropertyGrid _propGrid = null!;
        private Panel _pnlPreview = null!;
        private Button _btnAdd = null!;
        private Button _btnRemove = null!;
        private Button _btnMoveUp = null!;
        private Button _btnMoveDown = null!;
        private Button _btnOk = null!;
        private Button _btnCancel = null!;

        public TreeListDesignerForm(ZTreeList treeList)
        {
            _treeList = treeList ?? throw new ArgumentNullException(nameof(treeList));
            InitializeWorkingModel();
            BuildUi();
            BindSelectedColumn();
        }

        private void InitializeWorkingModel()
        {
            if (_treeList.Columns != null && _treeList.Columns.Count > 0)
            {
                foreach (var col in _treeList.Columns)
                {
                    _workingColumns.Add(new TreeColumnDefinition
                    {
                        Caption = col.Caption ?? string.Empty,
                        FieldName = col.FieldName ?? string.Empty,
                        Width = col.Width > 0 ? col.Width : 120,
                        IsVisible = col.Visible
                    });
                }
            }
            // If empty, keep working columns completely empty by default
        }

        private void BuildUi()
        {
            Text = "ZeroUI TreeList Schema & Column Designer";
            Size = new Size(820, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(700, 480);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            BackColor = Color.FromArgb(246, 248, 250);
            ForeColor = Color.FromArgb(33, 37, 41);

            // Banner
            var pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(25, 30, 40),
                Padding = new Padding(16, 12, 16, 12)
            };
            var lblTitle = new Label
            {
                Text = "TreeList Hierarchy Schema & Column Configuration",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Configure hierarchical multi-level columns, field names, and tree visual layouts.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(170, 180, 195),
                Dock = DockStyle.Bottom,
                AutoSize = true
            };
            pnlBanner.Controls.Add(lblTitle);
            pnlBanner.Controls.Add(lblSub);

            // Bottom Buttons Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(16, 10, 16, 10)
            };
            _btnOk = new Button
            {
                Text = "Apply & Close",
                DialogResult = DialogResult.OK,
                Width = 120,
                Height = 32,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(14, 116, 144),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) => ApplyChanges();

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 0, 10, 0)
            };

            var btnExportCs = new Button
            {
                Text = "⚡ Export C# Setup",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExportCs.FlatAppearance.BorderSize = 0;
            btnExportCs.Click += (s, e) => ShowGeneratedCSharp();

            var btnExportXaml = new Button
            {
                Text = "Export XAML",
                Width = 110,
                Height = 32,
                Dock = DockStyle.Left,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnExportXaml.Click += (s, e) => ShowGeneratedXaml();

            pnlBottom.Controls.Add(_btnCancel);
            pnlBottom.Controls.Add(new Label { Width = 10, Dock = DockStyle.Right });
            pnlBottom.Controls.Add(_btnOk);
            pnlBottom.Controls.Add(btnExportCs);
            pnlBottom.Controls.Add(new Label { Width = 10, Dock = DockStyle.Left });
            pnlBottom.Controls.Add(btnExportXaml);

            // Main Split
            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 260,
                Panel1MinSize = 220,
                Panel2MinSize = 350
            };

            // Left Panel (Column List & Management Toolbar)
            var pnlLeft = splitMain.Panel1;
            pnlLeft.Padding = new Padding(12, 12, 6, 12);

            var lblColumns = new Label { Text = "Defined Columns:", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _lstColumns = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle };
            _lstColumns.SelectedIndexChanged += (s, e) => BindSelectedColumn();

            var pnlListToolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 4, 0, 0) };
            _btnAdd = new Button { Text = "+ Add", Width = 55, Height = 28 };
            _btnAdd.Click += (s, e) => AddColumn();

            _btnRemove = new Button { Text = "- Remove", Width = 70, Height = 28 };
            _btnRemove.Click += (s, e) => RemoveColumn();

            _btnMoveUp = new Button { Text = "▲", Width = 32, Height = 28 };
            _btnMoveUp.Click += (s, e) => MoveColumn(-1);

            _btnMoveDown = new Button { Text = "▼", Width = 32, Height = 28 };
            _btnMoveDown.Click += (s, e) => MoveColumn(1);

            pnlListToolbar.Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnMoveUp, _btnMoveDown });

            pnlLeft.Controls.Add(_lstColumns);
            pnlLeft.Controls.Add(pnlListToolbar);
            pnlLeft.Controls.Add(lblColumns);

            // Right Split (Property Grid & Live Preview)
            var splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 240,
                Panel1MinSize = 160,
                Panel2MinSize = 140
            };

            var pnlProp = splitRight.Panel1;
            pnlProp.Padding = new Padding(6, 12, 12, 6);
            var lblProps = new Label { Text = "Column Properties:", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _propGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = true,
                PropertySort = PropertySort.CategorizedAlphabetical
            };
            _propGrid.PropertyValueChanged += (s, e) =>
            {
                RefreshColumnList();
                _pnlPreview.Invalidate();
            };
            pnlProp.Controls.Add(_propGrid);
            pnlProp.Controls.Add(lblProps);

            // Live Preview Canvas
            var pnlPreviewContainer = splitRight.Panel2;
            pnlPreviewContainer.Padding = new Padding(6, 6, 12, 12);
            var lblPreview = new Label { Text = "Live Hierarchy Preview Canvas:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            _pnlPreview = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlPreview.Paint += DrawPreviewCanvas;

            pnlPreviewContainer.Controls.Add(_pnlPreview);
            pnlPreviewContainer.Controls.Add(lblPreview);

            splitMain.Panel2.Controls.Add(splitRight);

            Controls.Add(splitMain);
            Controls.Add(pnlBottom);
            Controls.Add(pnlBanner);

            RefreshColumnList();
        }

        private void RefreshColumnList()
        {
            int sel = _lstColumns.SelectedIndex;
            _lstColumns.BeginUpdate();
            _lstColumns.Items.Clear();
            for (int i = 0; i < _workingColumns.Count; i++)
            {
                var col = _workingColumns[i];
                string cap = string.IsNullOrWhiteSpace(col.Caption) ? col.FieldName : col.Caption;
                _lstColumns.Items.Add($"[{i + 1}] {cap} ({col.FieldName}) - {col.Width}px");
            }
            _lstColumns.EndUpdate();

            if (sel >= 0 && sel < _lstColumns.Items.Count)
                _lstColumns.SelectedIndex = sel;
            else if (_lstColumns.Items.Count > 0)
                _lstColumns.SelectedIndex = 0;

            _pnlPreview?.Invalidate();
        }

        private void BindSelectedColumn()
        {
            int sel = _lstColumns.SelectedIndex;
            if (sel >= 0 && sel < _workingColumns.Count)
            {
                _propGrid.SelectedObject = _workingColumns[sel];
                _btnRemove.Enabled = true;
                _btnMoveUp.Enabled = sel > 0;
                _btnMoveDown.Enabled = sel < _workingColumns.Count - 1;
            }
            else
            {
                _propGrid.SelectedObject = null;
                _btnRemove.Enabled = false;
                _btnMoveUp.Enabled = false;
                _btnMoveDown.Enabled = false;
            }
        }

        private void AddColumn()
        {
            int nextId = _workingColumns.Count + 1;
            _workingColumns.Add(new TreeColumnDefinition
            {
                Caption = $"Column {nextId}",
                FieldName = $"Field{nextId}",
                Width = 120,
                IsVisible = true
            });
            RefreshColumnList();
            _lstColumns.SelectedIndex = _workingColumns.Count - 1;
        }

        private void RemoveColumn()
        {
            int sel = _lstColumns.SelectedIndex;
            if (sel >= 0 && sel < _workingColumns.Count)
            {
                _workingColumns.RemoveAt(sel);
                RefreshColumnList();
            }
        }

        private void MoveColumn(int delta)
        {
            int sel = _lstColumns.SelectedIndex;
            int newIdx = sel + delta;
            if (sel >= 0 && newIdx >= 0 && newIdx < _workingColumns.Count)
            {
                var item = _workingColumns[sel];
                _workingColumns.RemoveAt(sel);
                _workingColumns.Insert(newIdx, item);
                RefreshColumnList();
                _lstColumns.SelectedIndex = newIdx;
            }
        }

        private void DrawPreviewCanvas(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);

            int x = 4;
            int headerH = 26;
            int rowH = 24;

            if (_workingColumns.Count == 0)
            {
                using var msgFont = new Font("Segoe UI", 9f, FontStyle.Italic);
                using var msgBrush = new SolidBrush(Color.FromArgb(140, 150, 165));
                g.DrawString("No columns defined. Click '+ Add' to configure TreeList columns.", msgFont, msgBrush, 12, 12);
                return;
            }

            using var hdrBrush = new SolidBrush(Color.FromArgb(235, 240, 248));
            using var borderPen = new Pen(Color.FromArgb(210, 220, 235));
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var cellFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(40, 50, 70));
            using var cellBrush = new SolidBrush(Color.FromArgb(70, 80, 95));

            for (int i = 0; i < _workingColumns.Count; i++)
            {
                var col = _workingColumns[i];
                if (!col.IsVisible) continue;

                int colW = Math.Max(50, col.Width / 2);
                var hdrRect = new Rectangle(x, 4, colW, headerH);
                g.FillRectangle(hdrBrush, hdrRect);
                g.DrawRectangle(borderPen, hdrRect);

                string cap = string.IsNullOrWhiteSpace(col.Caption) ? col.FieldName : col.Caption;
                g.DrawString(cap, font, textBrush, new RectangleF(x + 4, 8, colW - 8, headerH - 8));

                // Draw tree hierarchy preview
                for (int r = 0; r < 3; r++)
                {
                    var cellRect = new Rectangle(x, 4 + headerH + r * rowH, colW, rowH);
                    g.DrawRectangle(borderPen, cellRect);
                    string prefix = (i == 0) ? (r == 0 ? "▾ Root Node" : (r == 1 ? "   ▸ Child 1" : "   ▸ Child 2")) : $"Item {r + 1}";
                    g.DrawString(prefix, cellFont, cellBrush, new RectangleF(x + 4, cellRect.Y + 4, colW - 8, rowH - 8));
                }

                x += colW;
            }
        }

        private void ApplyChanges()
        {
            _treeList.Columns.Clear();
            foreach (var col in _workingColumns)
            {
                _treeList.Columns.Add(new TreeListColumn(col.FieldName, col.Caption, col.Width)
                {
                    Visible = col.IsVisible
                });
            }
            _treeList.Invalidate();
        }

        public static string GenerateCSharpSetupCode(ZTreeList treeList)
        {
            if (treeList == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI TreeList Setup (C#)");
            sb.AppendLine("var treeList = new ZeroUI.WinForms.Data.ZTreeList();");
            sb.AppendLine($"treeList.ShowLines = {treeList.ShowLines.ToString().ToLower()};");
            sb.AppendLine($"treeList.ShowCheckBoxes = {treeList.ShowCheckBoxes.ToString().ToLower()};");
            sb.AppendLine("treeList.Columns.Clear();");
            foreach (var col in treeList.Columns)
            {
                sb.AppendLine($"treeList.Columns.Add(new TreeListColumn(\"{col.FieldName}\", \"{col.Caption}\", {col.Width}) {{ Visible = {col.Visible.ToString().ToLower()} }});");
            }
            return sb.ToString();
        }

        public static string GenerateXamlSetupCode(ZTreeList treeList)
        {
            if (treeList == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI TreeList (WPF XAML) -->");
            sb.AppendLine("<z:ZTreeList>");
            sb.AppendLine("    <z:ZTreeList.Columns>");
            foreach (var col in treeList.Columns)
            {
                sb.AppendLine($"        <z:TreeListColumn FieldName=\"{col.FieldName}\" Caption=\"{col.Caption}\" Width=\"{col.Width}\" Visible=\"{col.Visible.ToString().ToLower()}\" />");
            }
            sb.AppendLine("    </z:ZTreeList.Columns>");
            sb.AppendLine("</z:ZTreeList>");
            return sb.ToString();
        }

        private void ShowGeneratedCSharp()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI TreeList Column Setup (C#)");
            sb.AppendLine("treeList.Columns.Clear();");
            foreach (var col in _workingColumns)
            {
                sb.AppendLine($"treeList.Columns.Add(new TreeListColumn(\"{col.FieldName}\", \"{col.Caption}\", {col.Width}) {{ Visible = {col.IsVisible.ToString().ToLower()} }});");
            }

            using var dlg = new CodeExportForm("ZeroUI TreeList - Generated C# Setup Code", "C#", sb.ToString());
            dlg.ShowDialog(this);
        }

        private void ShowGeneratedXaml()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI TreeList Columns (WPF XAML) -->");
            sb.AppendLine("<z:ZTreeList.Columns>");
            foreach (var col in _workingColumns)
            {
                sb.AppendLine($"    <z:TreeListColumn FieldName=\"{col.FieldName}\" Caption=\"{col.Caption}\" Width=\"{col.Width}\" Visible=\"{col.IsVisible.ToString().ToLower()}\" />");
            }
            sb.AppendLine("</z:ZTreeList.Columns>");

            using var dlg = new CodeExportForm("ZeroUI TreeList - Generated XAML Template", "XAML", sb.ToString());
            dlg.ShowDialog(this);
        }

        private class TreeColumnDefinition
        {
            public string Caption { get; set; } = string.Empty;
            public string FieldName { get; set; } = string.Empty;
            public int Width { get; set; } = 120;
            public bool IsVisible { get; set; } = true;
        }
    }
}
