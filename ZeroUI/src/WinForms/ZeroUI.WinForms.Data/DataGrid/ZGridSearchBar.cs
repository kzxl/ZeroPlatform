using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

using ZeroUI.Core.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Integrated search and action toolbar control for ZeroGridControl with debounced filtering and export shortcuts.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - DataGrid")]
    [DefaultEvent("ExportClicked")]
    [Description("Integrated search bar and toolbar for GridControl")]
    public class ZGridSearchBar : Panel, IZeroDpiScalable
    {
        private ZGrid? _grid;
        private readonly ZSearchControl _searchBox;
        private readonly Label _lblMatchCount;
        private readonly ZButton _btnDensity;
        private readonly ZButton _btnExport;

        private int _baseHeight = 48;
        private int _baseSearchWidth = 320;
        private float _currentDpiScale = 1.0f;

        /// <summary>
        /// Gets the active High-DPI Per-Monitor V2 scale factor applied to this SearchBar.
        /// </summary>
        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        /// <summary>
        /// Applies High-DPI Per-Monitor V2 scaling to GridSearchBar metrics (Height, SearchBox width, and button bounds).
        /// </summary>
        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            Height = Math.Max(32, (int)Math.Round(_baseHeight * scaleFactor));
            Padding = new Padding(
                (int)Math.Round(12 * scaleFactor),
                (int)Math.Round(7 * scaleFactor),
                (int)Math.Round(12 * scaleFactor),
                (int)Math.Round(7 * scaleFactor));

            if (_searchBox != null)
            {
                _searchBox.Location = new Point((int)Math.Round(12 * scaleFactor), (int)Math.Round(7 * scaleFactor));
                _searchBox.Width = (int)Math.Round(_baseSearchWidth * scaleFactor);
                _searchBox.ApplyDpiScaling(scaleFactor);
            }

            if (_lblMatchCount != null)
            {
                _lblMatchCount.Location = new Point((int)Math.Round(345 * scaleFactor), (int)Math.Round(14 * scaleFactor));
            }

            if (_btnDensity != null)
            {
                _btnDensity.Size = new Size((int)Math.Round(135 * scaleFactor), (int)Math.Round(32 * scaleFactor));
                _btnDensity.Location = new Point(Width - (int)Math.Round(270 * scaleFactor), (int)Math.Round(8 * scaleFactor));
                _btnDensity.ApplyDpiScaling(scaleFactor);
            }

            if (_btnExport != null)
            {
                _btnExport.Size = new Size((int)Math.Round(110 * scaleFactor), (int)Math.Round(32 * scaleFactor));
                _btnExport.Location = new Point(Width - (int)Math.Round(125 * scaleFactor), (int)Math.Round(8 * scaleFactor));
                _btnExport.ApplyDpiScaling(scaleFactor);
            }

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

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            float effectiveFactor = factor.Height > 0f ? factor.Height : factor.Width;
            if (effectiveFactor > 0f && Math.Abs(effectiveFactor - 1.0f) > 0.001f)
            {
                ApplyDpiScaling(_currentDpiScale * effectiveFactor);
            }
        }

        public event EventHandler? ExportClicked;

        public ZGridSearchBar()
        {
            Dock = DockStyle.Top;
            Height = 48;
            BackColor = ZeroTheme.Colors.Surface;
            Padding = new Padding(12, 7, 12, 7);

            _searchBox = new ZSearchControl
            {
                PlaceholderText = "🔍 Search all columns (live filter)...",
                Location = new Point(12, 7),
                Width = 320,
                DebounceIntervalMs = 150
            };
            _searchBox.DebouncedTextChanged += SearchBox_DebouncedTextChanged;

            _lblMatchCount = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.Primary,
                Location = new Point(345, 14)
            };

            _btnDensity = new ZButton
            {
                Text = "📏 Density: Normal",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Size = new Size(135, 32),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(Width - 270, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnDensity.Click += (s, e) =>
            {
                if (_grid == null) return;
                var next = _grid.Density switch
                {
                    ZeroUI.Core.Common.GridDensity.Compact => ZeroUI.Core.Common.GridDensity.Middle,
                    ZeroUI.Core.Common.GridDensity.Middle => ZeroUI.Core.Common.GridDensity.Loose,
                    ZeroUI.Core.Common.GridDensity.Loose => ZeroUI.Core.Common.GridDensity.Compact,
                    _ => ZeroUI.Core.Common.GridDensity.Middle
                };
                _grid.Density = next;
                _btnDensity.Text = next switch
                {
                    ZeroUI.Core.Common.GridDensity.Compact => "📏 Density: Compact",
                    ZeroUI.Core.Common.GridDensity.Middle => "📏 Density: Normal",
                    ZeroUI.Core.Common.GridDensity.Loose => "📏 Density: Comfortable",
                    _ => "📏 Density"
                };
            };

            _btnExport = new ZButton
            {
                Text = "📊 Export CSV",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(110, 32),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(Width - 125, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnExport.Click += (s, e) => ExportClicked?.Invoke(this, EventArgs.Empty);


            Controls.Add(_searchBox);
            Controls.Add(_lblMatchCount);
            Controls.Add(_btnDensity);
            Controls.Add(_btnExport);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var p = ZeroTheme.Colors;
            BackColor = p.Surface;
            _lblMatchCount.ForeColor = p.Primary;
            Invalidate();
        }


        public void TriggerExport()
        {
            ExportClicked?.Invoke(this, EventArgs.Empty);
        }

        public void AttachToGrid(ZGrid grid)
        {
            _grid = grid;
            UpdateCountBadge();
        }

        private void SearchBox_DebouncedTextChanged(object? sender, string query)
        {
            if (_grid == null || _grid.DataSource == null) return;

            string trimmed = query.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                _grid.ApplyFilter(null);
                _lblMatchCount.Text = "";
                return;
            }

            var src = _grid.DataSource;
            int colCount = _grid.Columns.Count;

            _grid.ApplyFilter(modelRow =>
            {
                CellValueBuffer buf = new CellValueBuffer();
                for (int c = 0; c < colCount; c++)
                {
                    if (!_grid.Columns[c].IsVisible) continue;
                    buf.Reset();
                    src.GetCellValue(modelRow, c, ref buf);
                    if (buf.Text.IndexOf(trimmed.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            });


            UpdateCountBadge();
        }

        public void UpdateCountBadge()
        {
            if (_grid == null || _grid.DataSource == null)
            {
                _lblMatchCount.Text = "";
                return;
            }

            if (_grid.RowCount < _grid.DataSource.TotalRowCount)
            {
                _lblMatchCount.Text = $"Matches: {_grid.RowCount:N0} / {_grid.DataSource.TotalRowCount:N0} rows";
            }

            else
            {
                _lblMatchCount.Text = "";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Bottom border line
            using var pen = new Pen(ZeroTheme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridSearchBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridSearchBar is deprecated and will be removed in 5 release cycles. Please migrate to ZGridSearchBar instead.")]
    [ToolboxItem(false)]
    public class GridSearchBar : ZGridSearchBar
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZGridSearchBar"/>.
    /// </summary>
    [Obsolete("ZeroGridSearchBar is deprecated. Please use ZGridSearchBar instead.")]
    [ToolboxItem(false)]
    public class ZeroGridSearchBar : ZGridSearchBar
    {
    }

    #endregion
}
