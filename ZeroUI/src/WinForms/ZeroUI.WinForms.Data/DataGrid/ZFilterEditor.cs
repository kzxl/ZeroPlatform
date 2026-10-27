using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Advanced visual filter query builder control and dialog for ZeroUI WinForms.
    /// Provides visual tree construction of multi-level [AND / OR] logical criteria,
    /// real-time SQL/RowFilter expression preview, and direct binding with GridControl.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - DataGrid & Reporting")]
    [DefaultEvent("FilterApplied")]
    [Description("Interactive hierarchical visual filter editor with expression preview and grid binding.")]
    public class ZFilterEditor : ControlBase
    {
        private readonly ZFilterControl _filterControl;
        private readonly TextBox _txtExpressionPreview;
        private readonly Panel _topPanel;
        private readonly Panel _bottomPanel;
        private readonly Button _btnVisualTab;
        private readonly Button _btnTextTab;
        private readonly Button _btnClearAll;
        private readonly Button _btnApply;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        private ZGrid? _attachedGrid;
        private bool _isTextMode;

        public event EventHandler? FilterApplied;
        public event EventHandler? FilterChanged;

        [Browsable(false)]
        public GroupFilterNode RootGroup => _filterControl.RootGroup;

        [Browsable(false)]
        public List<string> AvailableFields => _filterControl.AvailableFields;

        public ZFilterEditor()
        {
            Size = new Size(580, 360);
            DoubleBuffered = true;

            // Center Content (Initialize first to prevent lambda null capture warnings)
            _filterControl = new ZFilterControl
            {
                Dock = DockStyle.Fill
            };
            _filterControl.FilterChanged += (s, e) =>
            {
                UpdatePreview();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };

            _txtExpressionPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f),
                Visible = false
            };

            // Top Header Panel
            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(8, 5, 8, 5)
            };

            _btnVisualTab = new Button
            {
                Text = "Visual Tree",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(95, 28),
                Location = new Point(8, 5)
            };
            _btnVisualTab.FlatAppearance.BorderSize = 0;
            _btnVisualTab.Click += (s, e) => SwitchMode(false);

            _btnTextTab = new Button
            {
                Text = "Expression Preview",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(135, 28),
                Location = new Point(108, 5)
            };
            _btnTextTab.FlatAppearance.BorderSize = 0;
            _btnTextTab.Click += (s, e) => SwitchMode(true);

            _btnClearAll = new Button
            {
                Text = "Clear All",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 28),
                Dock = DockStyle.Right
            };
            _btnClearAll.Click += (s, e) =>
            {
                _filterControl.RootGroup.Children.Clear();
                _filterControl.RebuildTreeUI();
                UpdatePreview();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };

            _topPanel.Controls.Add(_btnVisualTab);
            _topPanel.Controls.Add(_btnTextTab);
            _topPanel.Controls.Add(_btnClearAll);

            // Bottom Actions Panel
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(8)
            };

            _btnApply = new Button
            {
                Text = "Apply",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(75, 28),
                Dock = DockStyle.Right
            };
            _btnApply.Click += (s, e) => ApplyFilter();

            _btnOk = new Button
            {
                Text = "OK",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(75, 28),
                Dock = DockStyle.Right
            };
            _btnOk.Click += (s, e) =>
            {
                ApplyFilter();
                var parentForm = FindForm();
                if (parentForm != null)
                {
                    parentForm.DialogResult = DialogResult.OK;
                    parentForm.Close();
                }
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(75, 28),
                Dock = DockStyle.Right
            };
            _btnCancel.Click += (s, e) =>
            {
                var parentForm = FindForm();
                if (parentForm != null)
                {
                    parentForm.DialogResult = DialogResult.Cancel;
                    parentForm.Close();
                }
            };

            // Spacing
            var spacer1 = new Panel { Width = 8, Dock = DockStyle.Right };
            var spacer2 = new Panel { Width = 8, Dock = DockStyle.Right };

            _bottomPanel.Controls.Add(_btnCancel);
            _bottomPanel.Controls.Add(spacer1);
            _bottomPanel.Controls.Add(_btnApply);
            _bottomPanel.Controls.Add(spacer2);
            _bottomPanel.Controls.Add(_btnOk);

            Controls.Add(_filterControl);
            Controls.Add(_txtExpressionPreview);
            Controls.Add(_bottomPanel);
            Controls.Add(_topPanel);

            SwitchMode(false);
            ApplyThemeColors();
        }

        public void SetColumns(IEnumerable<ZeroColumn> columns)
        {
            _filterControl.SetColumns(columns);
            UpdatePreview();
        }

        public void SetAvailableFields(IEnumerable<string> fields)
        {
            _filterControl.AvailableFields.Clear();
            if (fields != null)
            {
                _filterControl.AvailableFields.AddRange(fields);
            }
            _filterControl.RebuildTreeUI();
            UpdatePreview();
        }

        public void AttachTo(ZGrid grid)
        {
            _attachedGrid = grid ?? throw new ArgumentNullException(nameof(grid));
            SetColumns(grid.Columns);
        }

        public string GetSqlWhere() => _filterControl.GetSqlWhere();

        public string GetRowFilter() => _filterControl.RootGroup.ToRowFilter();

        public string GetDisplayString() => _filterControl.GetDisplayString();

        public void ApplyFilter()
        {
            if (_attachedGrid != null)
            {
                string rowFilter = GetRowFilter();
                if (_attachedGrid.DataSource is System.Data.DataTable dt)
                {
                    dt.DefaultView.RowFilter = rowFilter;
                }
                else if (_attachedGrid.DataSource is System.Data.DataView dv)
                {
                    dv.RowFilter = rowFilter;
                }
            }

            FilterApplied?.Invoke(this, EventArgs.Empty);
        }

        private void SwitchMode(bool textMode)
        {
            _isTextMode = textMode;
            _filterControl.Visible = !textMode;
            _txtExpressionPreview.Visible = textMode;

            var pal = CurrentPalette;
            if (_isTextMode)
            {
                _btnTextTab.BackColor = pal.Primary;
                _btnTextTab.ForeColor = Color.White;
                _btnVisualTab.BackColor = pal.Surface;
                _btnVisualTab.ForeColor = pal.TextSecondary;
                UpdatePreview();
            }
            else
            {
                _btnVisualTab.BackColor = pal.Primary;
                _btnVisualTab.ForeColor = Color.White;
                _btnTextTab.BackColor = pal.Surface;
                _btnTextTab.ForeColor = pal.TextSecondary;
            }
        }

        private void UpdatePreview()
        {
            string sql = GetSqlWhere();
            string rowFilter = GetRowFilter();
            string display = GetDisplayString();

            _txtExpressionPreview.Text =
                $"-- Display Criteria:{Environment.NewLine}{display}{Environment.NewLine}{Environment.NewLine}" +
                $"-- SQL Where Expression:{Environment.NewLine}{sql}{Environment.NewLine}{Environment.NewLine}" +
                $"-- DataView.RowFilter Expression:{Environment.NewLine}{rowFilter}";
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            ApplyThemeColors();
        }

        private void ApplyThemeColors()
        {
            var pal = CurrentPalette;
            BackColor = pal.Surface;
            _topPanel.BackColor = pal.HeaderBackground;
            _bottomPanel.BackColor = pal.HeaderBackground;

            _btnClearAll.BackColor = pal.Surface;
            _btnClearAll.ForeColor = pal.Danger;
            _btnClearAll.FlatAppearance.BorderColor = pal.Border;

            _btnOk.BackColor = pal.Primary;
            _btnOk.ForeColor = Color.White;
            _btnOk.FlatAppearance.BorderSize = 0;

            _btnApply.BackColor = pal.Surface;
            _btnApply.ForeColor = pal.TextPrimary;
            _btnApply.FlatAppearance.BorderColor = pal.Border;

            _btnCancel.BackColor = pal.Surface;
            _btnCancel.ForeColor = pal.TextSecondary;
            _btnCancel.FlatAppearance.BorderColor = pal.Border;

            _txtExpressionPreview.BackColor = pal.Surface;
            _txtExpressionPreview.ForeColor = pal.TextPrimary;

            SwitchMode(_isTextMode);
        }

        /// <summary>
        /// Displays the Filter Editor inside a modal dialog window.
        /// </summary>
        public static DialogResult ShowDialog(IWin32Window? owner, ZGrid grid)
        {
            using (var form = new Form())
            {
                form.Text = "Filter Editor";
                form.StartPosition = FormStartPosition.CenterParent;
                form.Size = new Size(640, 440);
                form.ShowIcon = false;
                form.ShowInTaskbar = false;
                form.MinimizeBox = false;
                form.MaximizeBox = true;

                var editor = new ZFilterEditor
                {
                    Dock = DockStyle.Fill
                };
                editor.AttachTo(grid);
                form.Controls.Add(editor);

                return form.ShowDialog(owner);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFilterEditor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FilterEditorControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFilterEditor instead.")]
    [ToolboxItem(false)]
    public class FilterEditorControl : ZFilterEditor
    {
    }

    #endregion
}
