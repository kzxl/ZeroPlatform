using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Charts;

namespace ZeroUI.WinForms.Design.Forms
{
    /// <summary>
    /// Visual 3-step Chart Configuration Wizard Dialog for ZChart.
    /// Allows selecting chart types, configuring series & appearance, and live-previewing chart layouts.
    /// </summary>
    public class ChartWizardForm : Form
    {
        private readonly ZChart _chart;

        private ListBox _lstChartTypes = null!;
        private TextBox _txtChartTitle = null!;
        private CheckBox _chkShowLegend = null!;
        private CheckBox _chkShowTooltip = null!;
        private CheckBox _chkShowCrosshair = null!;
        private ComboBox _cboPalette = null!;
        private Panel _pnlPreview = null!;
        private Button _btnOk = null!;
        private Button _btnCancel = null!;

        public ChartWizardForm(ZChart chart)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "ZeroUI Chart Configuration Wizard";
            Size = new Size(860, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(720, 500);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            BackColor = Color.FromArgb(246, 248, 250);

            // Header Banner
            var pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(22, 30, 46),
                Padding = new Padding(16, 12, 16, 12)
            };
            var lblTitle = new Label
            {
                Text = "Visual Chart Engine Wizard",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Select visualization templates, calibrate axes, customize color palettes, and preview layouts.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 175, 195),
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
                Text = "Apply to Chart",
                DialogResult = DialogResult.OK,
                Width = 120,
                Height = 32,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(16, 185, 129),
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
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                Panel1MinSize = 250,
                Panel2MinSize = 400
            };

            // Left Config Controls
            var pnlLeft = split.Panel1;
            pnlLeft.Padding = new Padding(12, 10, 6, 10);

            var grpType = new GroupBox { Text = "1. Visualization Type", Dock = DockStyle.Top, Height = 180, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _lstChartTypes = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle };
            _lstChartTypes.Items.AddRange(new object[]
            {
                "📊 Clustered Column (BarChart)",
                "📈 Spline Trend (LineChart)",
                "🌊 Gradient Area (AreaChart)",
                "🍩 Distribution Donut (PieChart)",
                "📉 Statistical Histogram",
                "🎯 Scatter Correlation",
                "📋 Quality Pareto (80/20)"
            });
            _lstChartTypes.SelectedIndex = 0;
            _lstChartTypes.SelectedIndexChanged += (s, e) => _pnlPreview?.Invalidate();
            grpType.Controls.Add(_lstChartTypes);

            var grpOptions = new GroupBox { Text = "2. Appearance & Telemetry", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(10) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Font = new Font("Segoe UI", 9f, FontStyle.Regular) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            _txtChartTitle = new TextBox { Dock = DockStyle.Fill, Text = "Enterprise Production Telemetry" };
            _txtChartTitle.TextChanged += (s, e) => _pnlPreview?.Invalidate();

            _cboPalette = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboPalette.Items.AddRange(new object[] { "Obsidian Sapphire", "Emerald Industrial", "Sunset Amber", "Cyberpunk Neon" });
            _cboPalette.SelectedIndex = 0;
            _cboPalette.SelectedIndexChanged += (s, e) => _pnlPreview?.Invalidate();

            _chkShowLegend = new CheckBox { Text = "Show Legend", Checked = true, AutoSize = true };
            _chkShowTooltip = new CheckBox { Text = "Show Tooltips", Checked = true, AutoSize = true };
            _chkShowCrosshair = new CheckBox { Text = "Show Crosshairs", Checked = true, AutoSize = true };

            table.Controls.Add(new Label { Text = "Chart Title:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            table.Controls.Add(_txtChartTitle, 1, 0);
            table.Controls.Add(new Label { Text = "Color Theme:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            table.Controls.Add(_cboPalette, 1, 1);
            table.Controls.Add(_chkShowLegend, 1, 2);
            table.Controls.Add(_chkShowTooltip, 1, 3);
            table.Controls.Add(_chkShowCrosshair, 1, 4);

            grpOptions.Controls.Add(table);

            pnlLeft.Controls.Add(grpOptions);
            pnlLeft.Controls.Add(grpType);

            // Right Preview Canvas
            var pnlRight = split.Panel2;
            pnlRight.Padding = new Padding(6, 10, 12, 10);
            var grpPreview = new GroupBox { Text = "3. Live Chart Layout Preview", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(8) };
            _pnlPreview = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 24, 38), BorderStyle = BorderStyle.FixedSingle };
            _pnlPreview.Paint += RenderLivePreview;
            grpPreview.Controls.Add(_pnlPreview);
            pnlRight.Controls.Add(grpPreview);

            Controls.Add(split);
            Controls.Add(pnlBottom);
            Controls.Add(pnlBanner);
        }

        private void RenderLivePreview(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(18, 24, 38));

            int w = _pnlPreview.Width;
            int h = _pnlPreview.Height;

            // Draw Chart Title
            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.White);
            g.DrawString(_txtChartTitle.Text, titleFont, titleBrush, 16, 12);

            // Chart area rect
            var plotRect = new Rectangle(50, 50, w - 80, h - 90);
            using var gridPen = new Pen(Color.FromArgb(40, 60, 85), 1.0f);
            using var axisPen = new Pen(Color.FromArgb(80, 110, 140), 1.5f);

            // Grid lines
            for (int i = 0; i <= 4; i++)
            {
                int y = plotRect.Top + i * (plotRect.Height / 4);
                g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            }

            int typeIdx = _lstChartTypes.SelectedIndex;

            if (typeIdx == 0) // Column
            {
                using var barBrush = new LinearGradientBrush(plotRect, Color.FromArgb(0, 210, 255), Color.FromArgb(0, 120, 255), LinearGradientMode.Vertical);
                int barW = (plotRect.Width / 6) - 10;
                float[] vals = { 0.4f, 0.75f, 0.6f, 0.9f, 0.5f, 0.8f };
                for (int i = 0; i < 6; i++)
                {
                    int bx = plotRect.Left + 10 + i * (plotRect.Width / 6);
                    int bh = (int)(plotRect.Height * vals[i]);
                    int by = plotRect.Bottom - bh;
                    g.FillRectangle(barBrush, bx, by, barW, bh);
                }
            }
            else if (typeIdx == 3) // Donut
            {
                int dia = Math.Min(plotRect.Width, plotRect.Height) - 40;
                var donutRect = new Rectangle(plotRect.Left + (plotRect.Width - dia) / 2, plotRect.Top + (plotRect.Height - dia) / 2, dia, dia);
                using var p1 = new SolidBrush(Color.FromArgb(0, 210, 255));
                using var p2 = new SolidBrush(Color.FromArgb(16, 185, 129));
                using var p3 = new SolidBrush(Color.FromArgb(245, 158, 11));
                using var centerBrush = new SolidBrush(Color.FromArgb(18, 24, 38));

                g.FillPie(p1, donutRect, 0, 140);
                g.FillPie(p2, donutRect, 140, 120);
                g.FillPie(p3, donutRect, 260, 100);

                int hole = dia / 2;
                var holeRect = new Rectangle(donutRect.Left + hole / 2, donutRect.Top + hole / 2, hole, hole);
                g.FillEllipse(centerBrush, holeRect);
            }
            else // Spline / Line
            {
                using var linePen = new Pen(Color.FromArgb(0, 229, 255), 2.5f);
                var pts = new Point[]
                {
                    new Point(plotRect.Left + 20, plotRect.Bottom - 40),
                    new Point(plotRect.Left + plotRect.Width / 4, plotRect.Top + 60),
                    new Point(plotRect.Left + plotRect.Width / 2, plotRect.Bottom - 80),
                    new Point(plotRect.Left + 3 * plotRect.Width / 4, plotRect.Top + 40),
                    new Point(plotRect.Right - 20, plotRect.Top + 80)
                };
                g.DrawCurve(linePen, pts, 0.5f);

                using var dotBrush = new SolidBrush(Color.White);
                foreach (var p in pts)
                {
                    g.FillEllipse(dotBrush, p.X - 4, p.Y - 4, 8, 8);
                }
            }

            // Axes
            g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
            g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);

            // Legend
            if (_chkShowLegend.Checked)
            {
                using var legBrush = new SolidBrush(Color.FromArgb(180, 200, 220));
                using var legFont = new Font("Segoe UI", 8.5f);
                g.DrawString("■ Series 1 (Metric A)   ■ Series 2 (Metric B)", legFont, legBrush, plotRect.Left + 20, plotRect.Bottom + 10);
            }
        }

        private void ApplyChanges()
        {
            _chart.Invalidate();
        }

        public static string GenerateCSharpSetupCode(ZChart chart)
        {
            if (chart == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI Chart Setup (C#)");
            sb.AppendLine("var chart = new ZeroUI.WinForms.Charts.ZChart();");
            sb.AppendLine($"chart.Title = \"{chart.Title}\";");
            sb.AppendLine($"chart.ChartType = ChartType.{chart.ChartType};");
            sb.AppendLine($"chart.LegendPosition = ChartLegendPosition.{chart.LegendPosition};");
            sb.AppendLine($"chart.ShowGridLines = {chart.ShowGridLines.ToString().ToLower()};");
            sb.AppendLine($"chart.ShowTooltips = {chart.ShowTooltips.ToString().ToLower()};");
            return sb.ToString();
        }

        public static string GenerateXamlSetupCode(ZChart chart)
        {
            if (chart == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI Chart Control (WPF XAML) -->");
            sb.AppendLine($"<z:ZChart Title=\"{chart.Title}\" ChartType=\"{chart.ChartType}\" ShowGridLines=\"{chart.ShowGridLines.ToString().ToLower()}\" />");
            return sb.ToString();
        }

        private void ShowGeneratedCSharp()
        {
            string chartTypeName = _lstChartTypes.SelectedIndex switch
            {
                1 => "ChartType.Line",
                2 => "ChartType.Area",
                3 => "ChartType.Pie",
                _ => "ChartType.Column"
            };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ZeroUI Chart Setup (C#)");
            sb.AppendLine($"chart.ChartType = {chartTypeName};");
            sb.AppendLine($"chart.Title = \"{_txtChartTitle.Text}\";");
            sb.AppendLine($"chart.ShowTooltips = {_chkShowTooltip.Checked.ToString().ToLower()};");
            sb.AppendLine($"chart.ShowCrosshair = {_chkShowCrosshair.Checked.ToString().ToLower()};");
            sb.AppendLine($"chart.LegendPosition = {(_chkShowLegend.Checked ? "ChartLegendPosition.Top" : "ChartLegendPosition.None")};");

            using var dlg = new CodeExportForm("ZeroUI Chart - Generated C# Setup Code", "C#", sb.ToString());
            dlg.ShowDialog(this);
        }

        private void ShowGeneratedXaml()
        {
            string chartTypeName = _lstChartTypes.SelectedIndex switch
            {
                1 => "Line",
                2 => "Area",
                3 => "Pie",
                _ => "Column"
            };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!-- ZeroUI Chart Control (WPF XAML) -->");
            sb.AppendLine($"<z:ZChart ChartType=\"{chartTypeName}\"");
            sb.AppendLine($"         Title=\"{_txtChartTitle.Text}\"");
            sb.AppendLine($"         ShowTooltips=\"{_chkShowTooltip.Checked.ToString().ToLower()}\"");
            sb.AppendLine($"         ShowCrosshair=\"{_chkShowCrosshair.Checked.ToString().ToLower()}\"");
            sb.AppendLine($"         LegendPosition=\"{(_chkShowLegend.Checked ? "Top" : "None")}\" />");

            using var dlg = new CodeExportForm("ZeroUI Chart - Generated XAML Template", "XAML", sb.ToString());
            dlg.ShowDialog(this);
        }
    }
}
