using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Comprehensive multi-pen industrial trend and telemetry analytics studio control.
    /// Combines a high-throughput multi-Y axes waveform plot, dual-cursor delta inspection HUD,
    /// time-range presets (1m to 24h), live/pause playback, and real-time channel statistics.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Industrial Multi-Pen Trend and Telemetry Analytics Studio")]
    public class ZTrendStudio : BaseUserControl, IZeroDpiScalable
    {
        private readonly Panel _toolbarPanel;
        private readonly ZTrendPlot _plot;
        private readonly ZTrendPenTable _penTable;
        private readonly SplitContainer _splitContainer;

        // Toolbar buttons
        private readonly Button _btnLive;
        private readonly Button _btnDualCursor;
        private readonly Button _btnResetZoom;
        private readonly Button _btnToggleTable;
        private readonly Button _btnExportCsv;
        private readonly Button _btnSnapshot;

        // Preset buttons
        private readonly List<Button> _presetButtons = new List<Button>();

        private float _currentDpiScale = 1.0f;

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public ZTrendPlot Plot => _plot;
        public ZTrendPenTable PenTable => _penTable;
        public List<TrendPen> Pens => _plot.Pens;
        public List<TrendAnnotation> Annotations => _plot.Annotations;
        public TrendDualCursor DualCursor => _plot.DualCursor;

        [Category("ZeroUI - Behavior")]
        public bool IsLiveFollow
        {
            get => _plot.IsLiveFollow;
            set
            {
                _plot.IsLiveFollow = value;
                UpdateLiveButtonState();
            }
        }

        [Category("ZeroUI - Behavior")]
        public TimeSpan TimeWindow
        {
            get => _plot.TimeWindow;
            set => _plot.TimeWindow = value;
        }

        public ZTrendStudio()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Size = new Size(800, 520);
            BackColor = Color.FromArgb(15, 23, 42);

            // 1. Central Canvas & Pen Table
            _plot = new ZTrendPlot { Dock = DockStyle.Fill };
            _penTable = new ZTrendPenTable { Dock = DockStyle.Fill, Plot = _plot };

            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 370,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };

            _splitContainer.Panel1.Controls.Add(_plot);
            _splitContainer.Panel2.Controls.Add(_penTable);

            // 2. Top Toolbar
            _toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(8, 6, 8, 6)
            };

            _btnLive = CreateToolbarButton("🟢 LIVE FOLLOW", Color.FromArgb(16, 185, 129), 115);
            _btnLive.Click += (s, e) =>
            {
                _plot.IsLiveFollow = !_plot.IsLiveFollow;
                UpdateLiveButtonState();
            };

            _btnDualCursor = CreateToolbarButton("🎯 Dual Cursor", Color.FromArgb(71, 85, 105), 100);
            _btnDualCursor.Click += (s, e) =>
            {
                _plot.DualCursor.Enabled = !_plot.DualCursor.Enabled;
                UpdateDualCursorButtonState();
                _plot.Invalidate();
            };

            _btnResetZoom = CreateToolbarButton("↺ Reset (100%)", Color.FromArgb(71, 85, 105), 100);
            _btnResetZoom.Click += (s, e) =>
            {
                _plot.ResetZoom();
                _penTable.Invalidate();
            };

            _btnExportCsv = CreateToolbarButton("📊 CSV", Color.FromArgb(71, 85, 105), 65);
            _btnExportCsv.Click += (s, e) => PromptExportCsv();

            _btnSnapshot = CreateToolbarButton("📷 Image", Color.FromArgb(71, 85, 105), 75);
            _btnSnapshot.Click += (s, e) => PromptSnapshot();

            _btnToggleTable = CreateToolbarButton("📋 Metrics", Color.FromArgb(71, 85, 105), 80);
            _btnToggleTable.Click += (s, e) =>
            {
                _splitContainer.Panel2Collapsed = !_splitContainer.Panel2Collapsed;
                _btnToggleTable.BackColor = _splitContainer.Panel2Collapsed ? Color.FromArgb(51, 65, 85) : Color.FromArgb(79, 70, 229);
            };

            // Preset Buttons: 1m, 5m, 15m, 1h, 8h, 24h
            var presetDef = new (string text, TimeSpan span)[]
            {
                ("1m", TimeSpan.FromMinutes(1)),
                ("5m", TimeSpan.FromMinutes(5)),
                ("15m", TimeSpan.FromMinutes(15)),
                ("1h", TimeSpan.FromHours(1)),
                ("8h", TimeSpan.FromHours(8)),
                ("24h", TimeSpan.FromHours(24))
            };

            var presetsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            foreach (var (text, span) in presetDef)
            {
                var btn = CreatePresetButton(text, span);
                _presetButtons.Add(btn);
                presetsPanel.Controls.Add(btn);
            }

            // Left actions layout
            _toolbarPanel.Controls.Add(presetsPanel);

            var sep1 = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };
            _toolbarPanel.Controls.Add(sep1);

            _toolbarPanel.Controls.Add(_btnLive);

            var sep2 = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = Color.Transparent };
            _toolbarPanel.Controls.Add(sep2);

            _toolbarPanel.Controls.Add(_btnDualCursor);

            var sep3 = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = Color.Transparent };
            _toolbarPanel.Controls.Add(sep3);

            _toolbarPanel.Controls.Add(_btnResetZoom);

            // Right actions layout
            _toolbarPanel.Controls.Add(_btnSnapshot);
            _toolbarPanel.Controls.Add(_btnExportCsv);
            _toolbarPanel.Controls.Add(_btnToggleTable);

            // Reverse order for DockStyle.Right if needed
            _btnSnapshot.Dock = DockStyle.Right;
            _btnExportCsv.Dock = DockStyle.Right;
            _btnToggleTable.Dock = DockStyle.Right;
            _btnResetZoom.Dock = DockStyle.Left;
            _btnDualCursor.Dock = DockStyle.Left;
            _btnLive.Dock = DockStyle.Left;

            Controls.Add(_splitContainer);
            Controls.Add(_toolbarPanel);

            _plot.TimeRangeChanged += (s, e) =>
            {
                UpdateLiveButtonState();
                _penTable.Invalidate();
            };

            _plot.CursorsChanged += (s, e) => _penTable.Invalidate();

            UpdateLiveButtonState();
            UpdateDualCursorButtonState();
        }

        private Button CreateToolbarButton(string text, Color backColor, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreatePresetButton(string text, TimeSpan span)
        {
            var btn = new Button
            {
                Text = text,
                Width = 42,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = span == _plot.TimeWindow ? Color.FromArgb(79, 70, 229) : Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 3, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                _plot.TimeWindow = span;
                foreach (var b in _presetButtons)
                {
                    b.BackColor = Color.FromArgb(51, 65, 85);
                }
                btn.BackColor = Color.FromArgb(79, 70, 229);
                _penTable.Invalidate();
            };
            return btn;
        }

        private void UpdateLiveButtonState()
        {
            if (_plot.IsLiveFollow)
            {
                _btnLive.Text = "🟢 LIVE FOLLOW";
                _btnLive.BackColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                _btnLive.Text = "⏸ PAUSED";
                _btnLive.BackColor = Color.FromArgb(245, 158, 11);
            }
        }

        private void UpdateDualCursorButtonState()
        {
            if (_plot.DualCursor.Enabled)
            {
                _btnDualCursor.BackColor = Color.FromArgb(234, 179, 8);
                _btnDualCursor.ForeColor = Color.Black;
            }
            else
            {
                _btnDualCursor.BackColor = Color.FromArgb(71, 85, 105);
                _btnDualCursor.ForeColor = Color.White;
            }
        }

        public TrendPen AddPen(string name, string unit, Color color, TrendYAxisPosition position = TrendYAxisPosition.Left1)
        {
            var pen = new TrendPen(name, unit, color, position);
            _plot.Pens.Add(pen);
            _plot.Invalidate();
            _penTable.Invalidate();
            return pen;
        }

        public void AddAnnotation(DateTime timestamp, string label, string description = "", Color? color = null, string icon = "🚩")
        {
            _plot.Annotations.Add(new TrendAnnotation(timestamp, label, description, color, icon));
            _plot.Invalidate();
        }

        public void UpdateLiveStream()
        {
            _plot.UpdateLiveStream();
            _penTable.Invalidate();
        }

        public void ResetZoom()
        {
            _plot.ResetZoom();
            _penTable.Invalidate();
        }

        public void ExportCsv(string filePath)
        {
            if (_plot.Pens.Count == 0) return;

            var sb = new StringBuilder();

            // Header line: Timestamp, Pen1, Pen2, ...
            sb.Append("Timestamp");
            foreach (var pen in _plot.Pens)
            {
                sb.Append($",{pen.Name} ({pen.Unit})");
            }
            sb.AppendLine();

            DateTime start = _plot.StartTime;
            DateTime end = _plot.EndTime;

            // Collect all unique timestamps from visible pens
            var timestamps = new SortedSet<DateTime>();
            foreach (var pen in _plot.Pens)
            {
                var pts = pen.GetPointsInRange(start, end);
                foreach (var pt in pts)
                {
                    timestamps.Add(pt.Timestamp);
                }
            }

            foreach (var ts in timestamps)
            {
                sb.Append(ts.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                foreach (var pen in _plot.Pens)
                {
                    double? v = TrendDualCursor.GetValueAt(pen, ts);
                    sb.Append(v.HasValue ? $",{v.Value.ToString("F3", CultureInfo.InvariantCulture)}" : ",");
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private void PromptExportCsv()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"TrendExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportCsv(sfd.FileName);
                    MessageBox.Show(this, $"Successfully exported trend data to:\n{sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PromptSnapshot()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
                FileName = $"TrendSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var bmp = new Bitmap(_plot.Width, _plot.Height);
                    _plot.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(sfd.FileName, ImageFormat.Png);
                    MessageBox.Show(this, $"Successfully saved trend snapshot to:\n{sfd.FileName}", "Snapshot Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to save snapshot: {ex.Message}", "Snapshot Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;
            _toolbarPanel.Height = (int)Math.Round(42 * scaleFactor);
            Invalidate();
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTrendStudio"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TrendStudio is deprecated and will be removed in 5 release cycles. Please migrate to ZTrendStudio instead.")]
    [ToolboxItem(false)]
    public class TrendStudio : ZTrendStudio
    {
    }

    #endregion
}
