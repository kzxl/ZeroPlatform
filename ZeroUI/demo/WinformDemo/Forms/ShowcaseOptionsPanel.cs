using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed class ShowcaseOptionsPanel : BaseUserControl, IZeroDpiScalable
    {
        private readonly Panel _container;
        private readonly Panel _header;
        private readonly Label _lblTitle;
        private ZeroGridControl? _grid;

        private float _currentDpiScale = 1.0f;
        private int _baseWidth = 270;
        private int _baseHeaderHeight = 44;

        // Behavior controls
        private CheckBox _chkFindPanel = null!;
        private CheckBox _chkHighlightMatches = null!;
        private CheckBox _chkAutoFilter = null!;
        private CheckBox _chkCheckBoxSelector = null!;
        private CheckBox _chkInPlaceEditing = null!;

        // Appearance controls
        private RadioButton _rbDensityCompact = null!;
        private RadioButton _rbDensityNormal = null!;
        private RadioButton _rbDensityTouch = null!;
        private CheckBox _chkAlternatingRows = null!;
        private CheckBox _chkGridlines = null!;
        private CheckBox _chkGroupPanel = null!;
        private CheckBox _chkSummaryFooter = null!;

        // Performance & Data controls
        private Button _btn100k = null!;
        private Button _btn500k = null!;
        private Button _btn1M = null!;
        private Button _btn10M = null!;
        private CheckBox _chkLiveSim = null!;
        private Button _btnStressTest = null!;

        // Action controls
        private Button _btnBestFit = null!;
        private Button _btnExportCsv = null!;
        private Button _btnSaveLayout = null!;
        private Button _btnRestoreLayout = null!;

        // Callbacks
        public event Action<int>? DatasetLoadRequested;
        public event Action? StressTestToggled;
        public event Action<bool>? LiveSimulationToggled;
        public event Action? ExportCsvRequested;
        public event Action<bool>? FindPanelToggled;

        private string? _savedGridLayout;

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            Width = (int)Math.Round(_baseWidth * scaleFactor);
            if (_header != null) _header.Height = (int)Math.Round(_baseHeaderHeight * scaleFactor);
            if (_lblTitle != null) _lblTitle.Font = new Font("Segoe UI", 10f * scaleFactor, FontStyle.Bold);
            BuildOptionsUI();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float factor = ZeroDpi.GetScaleFactor(this);
            if (Math.Abs(factor - _currentDpiScale) > 0.001f)
            {
                ApplyDpiScaling(factor);
            }
        }

        public ShowcaseOptionsPanel()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Right;
            Width = _baseWidth;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(0);

            // Header Banner
            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = _baseHeaderHeight,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(12, 10, 12, 10)
            };
            _lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "⚙️ Options & Properties",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _header.Controls.Add(_lblTitle);

            _container = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 251),
                Padding = new Padding(12, 8, 12, 16)
            };

            Controls.Add(_container);
            Controls.Add(_header);

            BuildOptionsUI();
        }

        public void BindGrid(ZeroGridControl grid)
        {
            _grid = grid;
            SyncFromGrid();
        }

        public void SyncFromGrid()
        {
            if (_grid == null) return;

            _chkAutoFilter.Checked = _grid.ShowAutoFilterRow;
            _chkCheckBoxSelector.Checked = _grid.ShowCheckBoxSelectorColumn;
            _chkGroupPanel.Checked = _grid.ShowGroupPanel;
            _chkSummaryFooter.Checked = _grid.ShowFooter;
            _chkAlternatingRows.Checked = _grid.ShowAlternatingRowColors;
            _chkGridlines.Checked = _grid.ShowGridLines;
            _chkInPlaceEditing.Checked = _grid.AllowCellEditing;
            _chkHighlightMatches.Checked = _grid.FindHighlightMatches;

            if (_grid.Density == GridDensity.Compact) _rbDensityCompact.Checked = true;
            else if (_grid.Density == GridDensity.Loose) _rbDensityTouch.Checked = true;
            else _rbDensityNormal.Checked = true;
        }

        public void ApplyTheme(ZeroSkin skin)
        {
            bool isDark = skin.IsDark;
            BackColor = isDark ? Color.FromArgb(28, 30, 42) : Color.FromArgb(248, 249, 251);
            _container.BackColor = BackColor;

            foreach (Control c in _container.Controls)
            {
                if (c is GroupBox gb)
                {
                    gb.ForeColor = isDark ? Color.FromArgb(200, 210, 230) : Color.FromArgb(70, 80, 95);
                    foreach (Control child in gb.Controls)
                    {
                        if (child is CheckBox cb)
                            cb.ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.FromArgb(40, 44, 52);
                        else if (child is RadioButton rb)
                            rb.ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.FromArgb(40, 44, 52);
                    }
                }
            }
        }

        private void BuildOptionsUI()
        {
            _container.SuspendLayout();
            _container.Controls.Clear();

            int currentY = (int)Math.Round(8 * _currentDpiScale);

            // 1. Group: Behavior
            var gbBehavior = CreateSectionGroup("BEHAVIOR", ref currentY, 175);
            _chkFindPanel = CreateCheckBox("Allow Find Panel", 12, 22, true, val =>
            {
                FindPanelToggled?.Invoke(val);
            });
            _chkHighlightMatches = CreateCheckBox("Highlight Find Results", 12, 50, true, val =>
            {
                if (_grid != null)
                {
                    _grid.FindHighlightMatches = val;
                    _grid.Invalidate();
                }
            });
            _chkAutoFilter = CreateCheckBox("Auto Filter Row", 12, 78, true, val =>
            {
                if (_grid != null) _grid.ShowAutoFilterRow = val;
            });
            _chkCheckBoxSelector = CreateCheckBox("Checkbox Selector Col", 12, 106, false, val =>
            {
                if (_grid != null) _grid.ShowCheckBoxSelectorColumn = val;
            });
            _chkInPlaceEditing = CreateCheckBox("In-Place Cell Editing", 12, 134, true, val =>
            {
                if (_grid != null) _grid.AllowCellEditing = val;
            });
            gbBehavior.Controls.AddRange(new Control[] { _chkFindPanel, _chkHighlightMatches, _chkAutoFilter, _chkCheckBoxSelector, _chkInPlaceEditing });
            _container.Controls.Add(gbBehavior);

            // 2. Group: Appearance & Density
            var gbAppearance = CreateSectionGroup("APPEARANCE & DENSITY", ref currentY, 195);
            _rbDensityCompact = CreateRadioButton("Compact (22px)", 12, 22, false, () =>
            {
                if (_grid != null) _grid.Density = GridDensity.Compact;
            });
            _rbDensityNormal = CreateRadioButton("Normal (26px)", 95, 22, true, () =>
            {
                if (_grid != null) _grid.Density = GridDensity.Middle;
            });
            _rbDensityTouch = CreateRadioButton("Touch (34px)", 175, 22, false, () =>
            {
                if (_grid != null) _grid.Density = GridDensity.Loose;
            });

            _chkAlternatingRows = CreateCheckBox("Alternating Row Colors", 12, 54, true, val =>
            {
                if (_grid != null)
                {
                    _grid.ShowAlternatingRowColors = val;
                    _grid.Invalidate();
                }
            });
            _chkGridlines = CreateCheckBox("Show Gridlines", 12, 82, true, val =>
            {
                if (_grid != null)
                {
                    _grid.ShowGridLines = val;
                    _grid.Invalidate();
                }
            });
            _chkGroupPanel = CreateCheckBox("Show Group Panel", 12, 110, false, val =>
            {
                if (_grid != null)
                {
                    _grid.ShowGroupPanel = val;
                    _grid.Invalidate();
                }
            });
            _chkSummaryFooter = CreateCheckBox("Show Summary Footer", 12, 138, true, val =>
            {
                if (_grid != null) _grid.ShowFooter = val;
            });

            gbAppearance.Controls.AddRange(new Control[] { _rbDensityCompact, _rbDensityNormal, _rbDensityTouch, _chkAlternatingRows, _chkGridlines, _chkGroupPanel, _chkSummaryFooter });
            _container.Controls.Add(gbAppearance);

            // 3. Group: Big Data & Benchmarks
            var gbData = CreateSectionGroup("BIG DATA & STRESS", ref currentY, 160);
            _btn100k = CreateButton("100K", 12, 24, 52, 28, () => DatasetLoadRequested?.Invoke(100_000));
            _btn500k = CreateButton("500K", 68, 24, 52, 28, () => DatasetLoadRequested?.Invoke(500_000));
            _btn1M = CreateButton("1M", 124, 24, 52, 28, () => DatasetLoadRequested?.Invoke(1_000_000));
            _btn10M = CreateButton("10M", 180, 24, 52, 28, () => DatasetLoadRequested?.Invoke(10_000_000));

            _chkLiveSim = CreateCheckBox("⚡ 60 FPS Realtime Ticks", 12, 60, false, val =>
            {
                LiveSimulationToggled?.Invoke(val);
            });

            _btnStressTest = CreateButton("🔥 Auto-Scroll Stress Test", 12, 92, 220, 32, () =>
            {
                StressTestToggled?.Invoke();
            });
            _btnStressTest.BackColor = Color.FromArgb(238, 242, 255);
            _btnStressTest.ForeColor = Color.FromArgb(79, 70, 229);

            gbData.Controls.AddRange(new Control[] { _btn100k, _btn500k, _btn1M, _btn10M, _chkLiveSim, _btnStressTest });
            _container.Controls.Add(gbData);

            // 4. Group: Actions & Layout
            var gbActions = CreateSectionGroup("ACTIONS & SERIALIZATION", ref currentY, 130);
            _btnBestFit = CreateButton("Best Fit Columns", 12, 24, 105, 28, () =>
            {
                _grid?.BestFitColumns();
            });

            _btnExportCsv = CreateButton("Export CSV", 127, 24, 105, 28, () =>
            {
                ExportCsvRequested?.Invoke();
            });

            _btnSaveLayout = CreateButton("Save Layout", 12, 62, 105, 28, () =>
            {
                if (_grid != null)
                {
                    _savedGridLayout = _grid.SaveLayoutToJson();
                    var pForm = FindForm();
                    if (pForm is BaseForm bf)
                    {
                        bf.ShowToast("Grid layout saved successfully to memory.", "Layout Saved", ToastType.Success);
                    }
                    else if (pForm != null)
                    {
                        ZeroToast.Success(pForm, "Grid layout saved successfully to memory.");
                    }
                    else
                    {
                        MessageBox.Show("Grid layout saved successfully to memory.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            });

            _btnRestoreLayout = CreateButton("Restore Layout", 127, 62, 105, 28, () =>
            {
                if (_grid != null && !string.IsNullOrEmpty(_savedGridLayout))
                {
                    _grid.RestoreLayoutFromJson(_savedGridLayout);
                    var pForm = FindForm();
                    if (pForm is BaseForm bf)
                    {
                        bf.ShowToast("Grid layout restored from memory.", "Layout Restored", ToastType.Info);
                    }
                    else if (pForm != null)
                    {
                        ZeroToast.Info(pForm, "Grid layout restored from memory.");
                    }
                    else
                    {
                        MessageBox.Show("Grid layout restored from memory.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    var pForm = FindForm();
                    if (pForm is BaseForm bf)
                    {
                        bf.ShowToast("No saved layout found. Please save a layout first.", "Layout Notice", ToastType.Warning);
                    }
                    else if (pForm != null)
                    {
                        ZeroToast.Warning(pForm, "No saved layout found. Please save a layout first.");
                    }
                    else
                    {
                        MessageBox.Show("No saved layout found. Please save a layout first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            });

            gbActions.Controls.AddRange(new Control[] { _btnBestFit, _btnExportCsv, _btnSaveLayout, _btnRestoreLayout });
            _container.Controls.Add(gbActions);

            _container.ResumeLayout(true);
        }

        private GroupBox CreateSectionGroup(string title, ref int y, int height)
        {
            var gb = new GroupBox
            {
                Text = title,
                Font = new Font("Segoe UI", 8.25f * _currentDpiScale, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 125),
                Location = new Point((int)Math.Round(4 * _currentDpiScale), y),
                Size = new Size((int)Math.Round(244 * _currentDpiScale), (int)Math.Round(height * _currentDpiScale))
            };
            y += (int)Math.Round((height + 10) * _currentDpiScale);
            return gb;
        }

        private CheckBox CreateCheckBox(string text, int x, int y, bool isChecked, Action<bool> onChanged)
        {
            var cb = new CheckBox
            {
                Text = text,
                Location = new Point((int)Math.Round(x * _currentDpiScale), (int)Math.Round(y * _currentDpiScale)),
                AutoSize = true,
                Checked = isChecked,
                Font = new Font("Segoe UI", 9f * _currentDpiScale, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 44, 52),
                Cursor = Cursors.Hand
            };
            cb.CheckedChanged += (s, e) => onChanged(cb.Checked);
            return cb;
        }

        private RadioButton CreateRadioButton(string text, int x, int y, bool isChecked, Action onChecked)
        {
            var rb = new RadioButton
            {
                Text = text,
                Location = new Point((int)Math.Round(x * _currentDpiScale), (int)Math.Round(y * _currentDpiScale)),
                AutoSize = true,
                Checked = isChecked,
                Font = new Font("Segoe UI", 8.5f * _currentDpiScale, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 44, 52),
                Cursor = Cursors.Hand
            };
            rb.CheckedChanged += (s, e) =>
            {
                if (rb.Checked) onChecked();
            };
            return rb;
        }

        private Button CreateButton(string text, int x, int y, int width, int height, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point((int)Math.Round(x * _currentDpiScale), (int)Math.Round(y * _currentDpiScale)),
                Size = new Size((int)Math.Round(width * _currentDpiScale), (int)Math.Round(height * _currentDpiScale)),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f * _currentDpiScale, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(33, 37, 41),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 225);
            btn.Click += (s, e) => onClick();
            return btn;
        }
    }
}
