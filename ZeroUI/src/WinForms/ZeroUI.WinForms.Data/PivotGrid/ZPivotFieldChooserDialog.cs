using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Pivot;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.PivotGrid
{
    /// <summary>
    /// Interactive visual field chooser dialog for PivotGridControl.
    /// Allows users to view all available dimensions and measures, toggle visibility,
    /// and dynamically reassign fields between Filter, Column, Row, and Data Areas with live calculation updates.
    /// </summary>
    public class PivotFieldChooserDialog : Form
    {
        private readonly ZPivotGrid _pivotGrid;
        private readonly ListBox _allFieldsList;
        private readonly ListBox _filterList;
        private readonly ListBox _columnList;
        private readonly ListBox _rowList;
        private readonly ListBox _dataList;

        public PivotFieldChooserDialog(ZPivotGrid pivotGrid)
        {
            _pivotGrid = pivotGrid ?? throw new ArgumentNullException(nameof(pivotGrid));

            Text = "Pivot Grid Field List";
            Size = new Size(380, 520);
            MinimumSize = new Size(320, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ShowInTaskbar = false;
            TopMost = false;
            DoubleBuffered = true;

            var palette = ZeroTheme.Colors;
            BackColor = palette.Background;
            ForeColor = palette.TextPrimary;
            Font = new Font("Segoe UI", 9f);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(8)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Header Label
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  // Available Fields
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));  // 2x2 Target Areas
            Controls.Add(mainPanel);

            var lblAvailable = new Label
            {
                Text = "Drag fields between areas below:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = palette.TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            mainPanel.Controls.Add(lblAvailable, 0, 0);

            _allFieldsList = CreateFieldListBox();
            _allFieldsList.MouseDown += OnListMouseDown;
            mainPanel.Controls.Add(_allFieldsList, 0, 1);

            // 2x2 Areas Table
            var areasTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(0, 4, 0, 0)
            };
            areasTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            areasTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            areasTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            areasTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            mainPanel.Controls.Add(areasTable, 0, 2);

            _filterList = CreateAreaBucket(areasTable, "Filter Area", PivotArea.FilterArea, 0, 0);
            _columnList = CreateAreaBucket(areasTable, "Column Area", PivotArea.ColumnArea, 1, 0);
            _rowList = CreateAreaBucket(areasTable, "Row Area", PivotArea.RowArea, 0, 1);
            _dataList = CreateAreaBucket(areasTable, "Data Area", PivotArea.DataArea, 1, 1);

            PopulateFields();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private ListBox CreateFieldListBox()
        {
            var palette = ZeroTheme.Colors;
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Surface,
                ForeColor = palette.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                AllowDrop = true
            };
            list.ContextMenuStrip = CreateContextMenu();
            return list;
        }

        private ListBox CreateAreaBucket(TableLayoutPanel parent, string title, PivotArea area, int col, int row)
        {
            var palette = ZeroTheme.Colors;
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(3)
            };

            var lbl = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = palette.Primary,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold)
            };
            panel.Controls.Add(lbl);

            var list = CreateFieldListBox();
            list.Tag = area;
            list.Dock = DockStyle.Fill;
            list.DragEnter += OnAreaDragEnter;
            list.DragDrop += (s, e) => OnAreaDragDrop(list, area, e);
            list.MouseDown += OnListMouseDown;
            list.DoubleClick += (s, e) =>
            {
                if (list.SelectedItem is PivotGridField field)
                {
                    // Double click removes from area back to filter
                    field.Area = PivotArea.FilterArea;
                    _pivotGrid.RefreshData();
                    PopulateFields();
                }
            };

            panel.Controls.Add(list);
            list.BringToFront();
            parent.Controls.Add(panel, col, row);

            return list;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Move to Row Area", null, (s, e) => MoveSelectedField(PivotArea.RowArea));
            menu.Items.Add("Move to Column Area", null, (s, e) => MoveSelectedField(PivotArea.ColumnArea));
            menu.Items.Add("Move to Data Area", null, (s, e) => MoveSelectedField(PivotArea.DataArea));
            menu.Items.Add("Move to Filter Area", null, (s, e) => MoveSelectedField(PivotArea.FilterArea));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Toggle Visibility", null, (s, e) => ToggleSelectedFieldVisibility());
            return menu;
        }

        private void MoveSelectedField(PivotArea targetArea)
        {
            var field = GetActiveSelectedField();
            if (field != null)
            {
                field.Area = targetArea;
                field.Visible = true;
                _pivotGrid.RefreshData();
                PopulateFields();
            }
        }

        private void ToggleSelectedFieldVisibility()
        {
            var field = GetActiveSelectedField();
            if (field != null)
            {
                field.Visible = !field.Visible;
                _pivotGrid.RefreshData();
                PopulateFields();
            }
        }

        private PivotGridField? GetActiveSelectedField()
        {
            if (_allFieldsList.Focused && _allFieldsList.SelectedItem is PivotGridField f0) return f0;
            if (_filterList.Focused && _filterList.SelectedItem is PivotGridField f1) return f1;
            if (_columnList.Focused && _columnList.SelectedItem is PivotGridField f2) return f2;
            if (_rowList.Focused && _rowList.SelectedItem is PivotGridField f3) return f3;
            if (_dataList.Focused && _dataList.SelectedItem is PivotGridField f4) return f4;
            return _allFieldsList.SelectedItem as PivotGridField
                ?? _rowList.SelectedItem as PivotGridField
                ?? _columnList.SelectedItem as PivotGridField
                ?? _dataList.SelectedItem as PivotGridField
                ?? _filterList.SelectedItem as PivotGridField;
        }

        public void PopulateFields()
        {
            _allFieldsList.Items.Clear();
            _filterList.Items.Clear();
            _columnList.Items.Clear();
            _rowList.Items.Clear();
            _dataList.Items.Clear();

            foreach (var field in _pivotGrid.Fields)
            {
                string status = field.Visible ? "✓ " : "✗ ";
                _allFieldsList.Items.Add(field);

                if (field.Visible)
                {
                    switch (field.Area)
                    {
                        case PivotArea.FilterArea:
                            _filterList.Items.Add(field);
                            break;
                        case PivotArea.ColumnArea:
                            _columnList.Items.Add(field);
                            break;
                        case PivotArea.RowArea:
                            _rowList.Items.Add(field);
                            break;
                        case PivotArea.DataArea:
                            _dataList.Items.Add(field);
                            break;
                    }
                }
            }
        }

        private void OnListMouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is ListBox lb && e.Button == MouseButtons.Left)
            {
                int index = lb.IndexFromPoint(e.Location);
                if (index >= 0 && index < lb.Items.Count)
                {
                    var field = lb.Items[index] as PivotGridField;
                    if (field != null)
                    {
                        lb.DoDragDrop(field, DragDropEffects.Move);
                    }
                }
            }
        }

        private void OnAreaDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(PivotGridField)) == true)
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnAreaDragDrop(ListBox targetList, PivotArea targetArea, DragEventArgs e)
        {
            if (e.Data?.GetData(typeof(PivotGridField)) is PivotGridField field)
            {
                field.Area = targetArea;
                field.Visible = true;
                _pivotGrid.RefreshData();
                PopulateFields();
            }
        }

        private void ApplyTheme()
        {
            var palette = ZeroTheme.Colors;
            BackColor = palette.Background;
            ForeColor = palette.TextPrimary;

            _allFieldsList.BackColor = palette.Surface;
            _allFieldsList.ForeColor = palette.TextPrimary;
            _filterList.BackColor = palette.Surface;
            _filterList.ForeColor = palette.TextPrimary;
            _columnList.BackColor = palette.Surface;
            _columnList.ForeColor = palette.TextPrimary;
            _rowList.BackColor = palette.Surface;
            _rowList.ForeColor = palette.TextPrimary;
            _dataList.BackColor = palette.Surface;
            _dataList.ForeColor = palette.TextPrimary;

            Invalidate(true);
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => ApplyTheme();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}
}
