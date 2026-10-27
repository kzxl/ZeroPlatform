using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using ZeroUI.Core.Scada;

namespace ZeroUI.WinForms.Design.Editors
{
    /// <summary>
    /// Visual Property Grid UITypeEditor for SCADA Gauge Threshold Ranges.
    /// Opens an interactive modal dialog for adding and calibrating multi-stage warning and critical bands.
    /// </summary>
    public class GaugeThresholdEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.Modal;

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            if (provider?.GetService(typeof(IWindowsFormsEditorService)) is IWindowsFormsEditorService editorService)
            {
                var list = value as List<GaugeThresholdRange> ?? new List<GaugeThresholdRange>();
                using var dlg = new GaugeThresholdEditorDialog(list);
                if (editorService.ShowDialog(dlg) == DialogResult.OK)
                {
                    return dlg.GetRanges();
                }
            }
            return value;
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

        public override void PaintValue(PaintValueEventArgs e)
        {
            if (e.Value is List<GaugeThresholdRange> ranges && ranges.Count > 0)
            {
                int x = e.Bounds.X;
                int step = Math.Max(2, e.Bounds.Width / ranges.Count);
                for (int i = 0; i < ranges.Count; i++)
                {
                    var r = ranges[i];
                    Color c = Color.FromArgb((int)r.ArgbColor);
                    using var brush = new SolidBrush(c);
                    e.Graphics.FillRectangle(brush, x, e.Bounds.Y, step, e.Bounds.Height);
                    x += step;
                }
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(16, 185, 129));
                e.Graphics.FillRectangle(b, e.Bounds);
            }
        }

        public class GaugeThresholdEditorDialog : Form
        {
            private readonly List<GaugeThresholdRange> _ranges = new List<GaugeThresholdRange>();
            private readonly DataGridView _grid;
            private readonly Button _btnOk;
            private readonly Button _btnCancel;
            private readonly Button _btnAdd;
            private readonly Button _btnRemove;

            public GaugeThresholdEditorDialog(List<GaugeThresholdRange> initial)
            {
                _ranges.AddRange(initial);

                Text = "SCADA Gauge Threshold Range Editor";
                Size = new Size(580, 400);
                StartPosition = FormStartPosition.CenterParent;
                Font = new Font("Segoe UI", 9f);
                BackColor = Color.FromArgb(246, 248, 250);

                var pnlBanner = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(25, 30, 45), Padding = new Padding(12, 8, 12, 8) };
                pnlBanner.Controls.Add(new Label { Text = "Configure Operational Limit Bands (Normal / Warning / Critical)", ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Dock = DockStyle.Fill });

                var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10) };
                _btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, Dock = DockStyle.Right };
                _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Dock = DockStyle.Right };
                pnlBottom.Controls.Add(_btnCancel);
                pnlBottom.Controls.Add(new Label { Width = 8, Dock = DockStyle.Right });
                pnlBottom.Controls.Add(_btnOk);

                var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 4, 8, 4) };
                _btnAdd = new Button { Text = "Add Range", Width = 90, Height = 28 };
                _btnRemove = new Button { Text = "Remove", Width = 75, Height = 28 };
                _btnAdd.Click += (s, e) => AddRange();
                _btnRemove.Click += (s, e) => RemoveRange();
                pnlButtons.Controls.AddRange(new Control[] { _btnAdd, _btnRemove });

                _grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    BackgroundColor = Color.White,
                    AllowUserToAddRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    RowHeadersVisible = false
                };

                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "From", Name = "From" });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To", Name = "To" });
                _grid.Columns.Add(new DataGridViewComboBoxColumn
                {
                    HeaderText = "Severity",
                    Name = "Severity",
                    DataSource = Enum.GetValues(typeof(GaugeSeverity))
                });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Label", Name = "Label" });

                PopulateGrid();

                Controls.Add(_grid);
                Controls.Add(pnlButtons);
                Controls.Add(pnlBottom);
                Controls.Add(pnlBanner);
            }

            private void PopulateGrid()
            {
                _grid.Rows.Clear();
                foreach (var r in _ranges)
                {
                    _grid.Rows.Add(r.From, r.To, r.Severity, r.Label);
                }
            }

            private void AddRange()
            {
                double start = _ranges.Count > 0 ? _ranges[_ranges.Count - 1].To : 0.0;
                double end = start + 25.0;
                _ranges.Add(new GaugeThresholdRange(start, end, GaugeSeverity.Normal, 0xFF10B981u, "Normal"));
                PopulateGrid();
            }

            private void RemoveRange()
            {
                if (_grid.CurrentRow != null && _grid.CurrentRow.Index >= 0 && _grid.CurrentRow.Index < _ranges.Count)
                {
                    _ranges.RemoveAt(_grid.CurrentRow.Index);
                    PopulateGrid();
                }
            }

            public List<GaugeThresholdRange> GetRanges()
            {
                var result = new List<GaugeThresholdRange>();
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    var row = _grid.Rows[i];
                    double from = Convert.ToDouble(row.Cells["From"].Value);
                    double to = Convert.ToDouble(row.Cells["To"].Value);
                    var sev = (GaugeSeverity)(row.Cells["Severity"].Value ?? GaugeSeverity.Normal);
                    string label = row.Cells["Label"].Value?.ToString() ?? "";
                    uint col = sev switch
                    {
                        GaugeSeverity.Warning => 0xFFF59E0Bu,
                        GaugeSeverity.Critical => 0xFFEF4444u,
                        _ => 0xFF10B981u
                    };
                    result.Add(new GaugeThresholdRange(from, to, sev, col, label));
                }
                return result;
            }
        }
    }
}
