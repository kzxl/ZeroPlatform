using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.DataGrid;

namespace ZeroUI.WinForms.Design.Forms
{
    /// <summary>
    /// Interactive In-place Visual Designer Dialog for ZGrid / GridControl.
    /// Allows adding, deleting, reordering columns, configuring in-place editors, and live-previewing grid schemas.
    /// </summary>
    public class GridDesignerForm : Form
    {
        private readonly ZGrid _grid;
        private readonly List<GridColumnDefinition> _workingColumns = new List<GridColumnDefinition>();

        private ListBox _lstColumns = null!;
        private Button _btnAdd = null!;
        private Button _btnRemove = null!;
        private Button _btnMoveUp = null!;
        private Button _btnMoveDown = null!;

        private TextBox _txtCaption = null!;
        private TextBox _txtFieldName = null!;
        private NumericUpDown _numWidth = null!;
        private ComboBox _cboAlignment = null!;
        private ComboBox _cboFrozen = null!;
        private ComboBox _cboEditor = null!;
        private ComboBox _cboSummary = null!;
        private CheckBox _chkVisible = null!;

        private Panel _pnlPreview = null!;
        private Button _btnOk = null!;
        private Button _btnCancel = null!;

        public GridDesignerForm(ZGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            InitializeWorkingModel();
            BuildUi();
            BindSelectedColumn();
        }

        private void InitializeWorkingModel()
        {
            // Clone current grid columns into working list
            if (_grid.Columns != null && _grid.Columns.Count > 0)
            {
                foreach (var col in _grid.Columns)
                {
                    _workingColumns.Add(new GridColumnDefinition
                    {
                        Caption = col.HeaderText,
                        FieldName = col.FieldName,
                        Width = col.Width > 0 ? col.Width : 100,
                        Alignment = (HorizontalAlignment)(int)col.Alignment,
                        IsVisible = col.IsVisible
                    });
                }
            }
            // If empty, keep working columns empty as requested
        }

        private void BuildUi()
        {
            Text = "ZeroUI GridControl Schema & Column Designer";
            Size = new Size(820, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(700, 480);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            BackColor = Color.FromArgb(246, 248, 250);
            ForeColor = Color.FromArgb(33, 37, 41);

            // Header Banner
            var pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(25, 30, 40),
                Padding = new Padding(16, 12, 16, 12)
            };
            var lblTitle = new Label
            {
                Text = "Grid Column & In-Place Editor Configuration",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Manage tabular column schemas, data field bindings, alignments, and repository editors.",
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
                Panel2MinSize = 400
            };

            // Left Pane: Column List
            var pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 6, 8, 0) };
            pnlLeftHeader.Controls.Add(new Label { Text = "Columns Palette", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Dock = DockStyle.Fill });

            _lstColumns = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f)
            };
            _lstColumns.SelectedIndexChanged += (s, e) => BindSelectedColumn();

            var pnlListButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Padding = new Padding(4, 4, 4, 4)
            };
            _btnAdd = new Button { Text = "Add", Width = 55, Height = 28 };
            _btnRemove = new Button { Text = "Remove", Width = 65, Height = 28 };
            _btnMoveUp = new Button { Text = "▲", Width = 36, Height = 28 };
            _btnMoveDown = new Button { Text = "▼", Width = 36, Height = 28 };

            _btnAdd.Click += (s, e) => AddColumn();
            _btnRemove.Click += (s, e) => RemoveColumn();
            _btnMoveUp.Click += (s, e) => MoveColumn(-1);
            _btnMoveDown.Click += (s, e) => MoveColumn(1);

            pnlListButtons.Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnMoveUp, _btnMoveDown });

            splitMain.Panel1.Controls.Add(_lstColumns);
            splitMain.Panel1.Controls.Add(pnlLeftHeader);
            splitMain.Panel1.Controls.Add(pnlListButtons);
            splitMain.Panel1.Padding = new Padding(12, 10, 6, 10);

            // Right Pane: Property Inputs & Live Preview
            var pnlRight = splitMain.Panel2;
            pnlRight.Padding = new Padding(6, 10, 12, 10);

            var grpProps = new GroupBox
            {
                Text = "Column Attributes & Editor",
                Dock = DockStyle.Top,
                Height = 220,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(12)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _txtCaption = new TextBox { Dock = DockStyle.Fill };
            _txtFieldName = new TextBox { Dock = DockStyle.Fill };
            _numWidth = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 20, Maximum = 1000, Value = 100 };
            _cboAlignment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboAlignment.Items.AddRange(new object[] { "Left", "Center", "Right" });
            _cboFrozen = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboFrozen.Items.AddRange(new object[] { "None", "Left", "Right" });
            _cboEditor = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboEditor.Items.AddRange(new object[] { "Default (Text)", "CheckEdit", "ProgressBar", "Rating", "ButtonEdit" });
            _cboSummary = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSummary.Items.AddRange(new object[] { "None", "Sum", "Count", "Average", "Min", "Max" });
            _chkVisible = new CheckBox { Text = "Is Visible", Checked = true, AutoSize = true };

            _txtCaption.TextChanged += (s, e) => SaveSelectedColumn();
            _txtFieldName.TextChanged += (s, e) => SaveSelectedColumn();
            _numWidth.ValueChanged += (s, e) => SaveSelectedColumn();
            _cboAlignment.SelectedIndexChanged += (s, e) => SaveSelectedColumn();
            _cboFrozen.SelectedIndexChanged += (s, e) => SaveSelectedColumn();
            _chkVisible.CheckedChanged += (s, e) => SaveSelectedColumn();

            table.Controls.Add(new Label { Text = "Caption:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            table.Controls.Add(_txtCaption, 1, 0);
            table.Controls.Add(new Label { Text = "Field Name:", TextAlign = ContentAlignment.MiddleRight }, 2, 0);
            table.Controls.Add(_txtFieldName, 3, 0);

            table.Controls.Add(new Label { Text = "Width (px):", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            table.Controls.Add(_numWidth, 1, 1);
            table.Controls.Add(new Label { Text = "Alignment:", TextAlign = ContentAlignment.MiddleRight }, 2, 1);
            table.Controls.Add(_cboAlignment, 3, 1);

            table.Controls.Add(new Label { Text = "In-Place Editor:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            table.Controls.Add(_cboEditor, 1, 2);
            table.Controls.Add(new Label { Text = "Summary:", TextAlign = ContentAlignment.MiddleRight }, 2, 2);
            table.Controls.Add(_cboSummary, 3, 2);

            table.Controls.Add(new Label { Text = "Frozen Pin:", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            table.Controls.Add(_cboFrozen, 1, 3);
            table.Controls.Add(_chkVisible, 3, 3);

            grpProps.Controls.Add(table);

            // Preview Box
            var grpPreview = new GroupBox
            {
                Text = "Live Schema Preview",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(8)
            };
            _pnlPreview = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlPreview.Paint += RenderPreview;
            grpPreview.Controls.Add(_pnlPreview);

            pnlRight.Controls.Add(grpPreview);
            pnlRight.Controls.Add(grpProps);

            Controls.Add(splitMain);
            Controls.Add(pnlBottom);
            Controls.Add(pnlBanner);

            RefreshList();
        }

        private void RefreshList()
        {
            int sel = _lstColumns.SelectedIndex;
            _lstColumns.Items.Clear();
            for (int i = 0; i < _workingColumns.Count; i++)
            {
                var col = _workingColumns[i];
                string cap = string.IsNullOrWhiteSpace(col.Caption) ? col.FieldName : col.Caption;
                _lstColumns.Items.Add($"[{i + 1}] {cap} ({col.Width}px)");
            }

            if (_lstColumns.Items.Count > 0)
            {
                _lstColumns.SelectedIndex = Math.Max(0, Math.Min(sel, _lstColumns.Items.Count - 1));
            }
            _pnlPreview?.Invalidate();
        }

        private bool _isBinding = false;

        private void BindSelectedColumn()
        {
            int idx = _lstColumns.SelectedIndex;
            if (idx < 0 || idx >= _workingColumns.Count)
            {
                _txtCaption.Enabled = false;
                _txtFieldName.Enabled = false;
                _numWidth.Enabled = false;
                _cboAlignment.Enabled = false;
                _cboFrozen.Enabled = false;
                _cboEditor.Enabled = false;
                _cboSummary.Enabled = false;
                _chkVisible.Enabled = false;
                return;
            }

            _isBinding = true;
            _txtCaption.Enabled = true;
            _txtFieldName.Enabled = true;
            _numWidth.Enabled = true;
            _cboAlignment.Enabled = true;
            _cboFrozen.Enabled = true;
            _cboEditor.Enabled = true;
            _cboSummary.Enabled = true;
            _chkVisible.Enabled = true;

            var col = _workingColumns[idx];
            _txtCaption.Text = col.Caption;
            _txtFieldName.Text = col.FieldName;
            _numWidth.Value = Math.Max(20, Math.Min(1000, col.Width));
            _cboAlignment.SelectedIndex = (int)col.Alignment;
            _cboFrozen.SelectedIndex = 0;
            _cboEditor.SelectedIndex = 0;
            _cboSummary.SelectedIndex = 0;
            _chkVisible.Checked = col.IsVisible;
            _isBinding = false;
        }

        private void SaveSelectedColumn()
        {
            if (_isBinding) return;
            int idx = _lstColumns.SelectedIndex;
            if (idx < 0 || idx >= _workingColumns.Count) return;

            var col = _workingColumns[idx];
            col.Caption = _txtCaption.Text;
            col.FieldName = _txtFieldName.Text;
            col.Width = (int)_numWidth.Value;
            col.Alignment = (HorizontalAlignment)Math.Max(0, _cboAlignment.SelectedIndex);
            col.IsVisible = _chkVisible.Checked;

            _pnlPreview?.Invalidate();
        }

        private void AddColumn()
        {
            int count = _workingColumns.Count + 1;
            _workingColumns.Add(new GridColumnDefinition
            {
                Caption = $"Column {count}",
                FieldName = $"Field{count}",
                Width = 100,
                Alignment = HorizontalAlignment.Left
            });
            RefreshList();
            _lstColumns.SelectedIndex = _workingColumns.Count - 1;
        }

        private void RemoveColumn()
        {
            int idx = _lstColumns.SelectedIndex;
            if (idx >= 0 && idx < _workingColumns.Count)
            {
                _workingColumns.RemoveAt(idx);
                RefreshList();
            }
        }

        private void MoveColumn(int delta)
        {
            int idx = _lstColumns.SelectedIndex;
            int target = idx + delta;
            if (idx >= 0 && target >= 0 && target < _workingColumns.Count)
            {
                var item = _workingColumns[idx];
                _workingColumns.RemoveAt(idx);
                _workingColumns.Insert(target, item);
                RefreshList();
                _lstColumns.SelectedIndex = target;
            }
        }

        private void RenderPreview(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(250, 252, 255));

            int x = 4;
            int headerH = 28;
            int rowH = 24;

            // Draw header bar
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

                int colW = Math.Max(40, col.Width / 2); // Scaled for preview canvas
                var hdrRect = new Rectangle(x, 4, colW, headerH);
                g.FillRectangle(hdrBrush, hdrRect);
                g.DrawRectangle(borderPen, hdrRect);

                string cap = string.IsNullOrWhiteSpace(col.Caption) ? col.FieldName : col.Caption;
                g.DrawString(cap, font, textBrush, new RectangleF(x + 4, 8, colW - 8, headerH - 8));

                // Draw 3 dummy rows
                for (int r = 0; r < 3; r++)
                {
                    var cellRect = new Rectangle(x, 4 + headerH + r * rowH, colW, rowH);
                    g.DrawRectangle(borderPen, cellRect);
                    g.DrawString($"Val {r + 1}", cellFont, cellBrush, new RectangleF(x + 4, cellRect.Y + 4, colW - 8, rowH - 8));
                }

                x += colW;
            }
        }

        private void ApplyChanges()
        {
            _grid.Columns.Clear();
            foreach (var col in _workingColumns)
            {
                _grid.Columns.Add(new ZeroColumn(col.FieldName, col.Caption, col.Width, (CellAlignment)(int)col.Alignment)
                {
                    IsVisible = col.IsVisible
                });
            }
            _grid.Invalidate();
        }

        public static string GenerateCSharpSetupCode(ZGrid grid)
        {
            if (grid == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI DataGrid Setup (C#)");
            sb.AppendLine("var grid = new ZeroUI.WinForms.DataGrid.ZGrid();");
            sb.AppendLine($"grid.ShowAutoFilterRow = {grid.ShowAutoFilterRow.ToString().ToLower()};");
            sb.AppendLine($"grid.ShowGroupPanel = {grid.ShowGroupPanel.ToString().ToLower()};");
            sb.AppendLine($"grid.ShowFooter = {grid.ShowFooter.ToString().ToLower()};");
            sb.AppendLine("grid.Columns.Clear();");
            foreach (var col in grid.Columns)
            {
                sb.AppendLine($"grid.Columns.Add(new ZeroColumn(\"{col.FieldName}\", \"{col.HeaderText}\", {col.Width}, CellAlignment.{col.Alignment}) {{ IsVisible = {col.IsVisible.ToString().ToLower()} }});");
            }
            return sb.ToString();
        }

        public static string GenerateXamlSetupCode(ZGrid grid)
        {
            if (grid == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI DataGrid (WPF XAML) -->");
            sb.AppendLine($"<z:ZeroGridControl ShowAutoFilterRow=\"{grid.ShowAutoFilterRow.ToString().ToLower()}\" ShowGroupPanel=\"{grid.ShowGroupPanel.ToString().ToLower()}\">");
            sb.AppendLine("    <z:ZeroGridControl.Columns>");
            foreach (var col in grid.Columns)
            {
                sb.AppendLine($"        <z:ZeroColumn FieldName=\"{col.FieldName}\" HeaderText=\"{col.HeaderText}\" Width=\"{col.Width}\" Alignment=\"{col.Alignment}\" IsVisible=\"{col.IsVisible.ToString().ToLower()}\" />");
            }
            sb.AppendLine("    </z:ZeroGridControl.Columns>");
            sb.AppendLine("</z:ZeroGridControl>");
            return sb.ToString();
        }

        private void ShowGeneratedCSharp()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI DataGrid Column Setup (C#)");
            sb.AppendLine("grid.Columns.Clear();");
            foreach (var col in _workingColumns)
            {
                string alignStr = col.Alignment switch
                {
                    HorizontalAlignment.Center => "CellAlignment.Center",
                    HorizontalAlignment.Right => "CellAlignment.Right",
                    _ => "CellAlignment.Left"
                };
                sb.AppendLine($"grid.Columns.Add(new ZeroColumn(\"{col.FieldName}\", \"{col.Caption}\", {col.Width}, {alignStr}) {{ IsVisible = {col.IsVisible.ToString().ToLower()} }});");
            }

            using var dlg = new CodeExportForm("ZeroUI Grid - Generated C# Setup Code", "C#", sb.ToString());
            dlg.ShowDialog(this);
        }

        private void ShowGeneratedXaml()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI DataGrid Columns (WPF XAML) -->");
            sb.AppendLine("<z:ZGrid.Columns>");
            foreach (var col in _workingColumns)
            {
                string alignStr = col.Alignment switch
                {
                    HorizontalAlignment.Center => "Center",
                    HorizontalAlignment.Right => "Right",
                    _ => "Left"
                };
                sb.AppendLine($"    <z:ZeroColumn FieldName=\"{col.FieldName}\" HeaderText=\"{col.Caption}\" Width=\"{col.Width}\" Alignment=\"{alignStr}\" IsVisible=\"{col.IsVisible.ToString().ToLower()}\" />");
            }
            sb.AppendLine("</z:ZGrid.Columns>");

            using var dlg = new CodeExportForm("ZeroUI Grid - Generated XAML Template", "XAML", sb.ToString());
            dlg.ShowDialog(this);
        }

        private class GridColumnDefinition
        {
            public string Caption { get; set; } = string.Empty;
            public string FieldName { get; set; } = string.Empty;
            public int Width { get; set; } = 100;
            public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
            public bool IsVisible { get; set; } = true;
        }
    }
}
