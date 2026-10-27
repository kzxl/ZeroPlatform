using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.Core.Localization;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Modern Enterprise Visual Filter / Query Builder control for ZeroUI WinForms.
    /// Provides an interactive hierarchical tree allowing users to construct complex
    /// multi-level logical filter expressions [AND/OR] with type-safe operators.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - DataGrid & Reporting")]
    [DefaultEvent("FilterChanged")]
    [Description("Interactive hierarchical visual filter tree builder for complex logical queries")]
    [ToolboxBitmap(typeof(ZeroIcons), "FilterControl.bmp")]
    public class ZFilterControl : Control
    {
        private readonly GroupFilterNode _rootGroup = new GroupFilterNode(FilterGroupOperator.And);
        private readonly List<string> _availableFields = new List<string>();
        private readonly Panel _treePanel;

        public event EventHandler? FilterChanged;

        [Browsable(false)]
        public GroupFilterNode RootGroup => _rootGroup;

        [Browsable(false)]
        public List<string> AvailableFields => _availableFields;

        public ZFilterControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Surface;
            Size = new Size(540, 260);

            _treePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12)
            };
            Controls.Add(_treePanel);

            ZeroTheme.ThemeChanged += OnThemeChanged;

            Localizer.CultureChanged += (s, e) =>
            {
                RebuildTreeUI();
            };

            // Add default starter condition
            _rootGroup.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
            RebuildTreeUI();
        }

        public void SetColumns(IEnumerable<ZeroColumn> columns)
        {
            _availableFields.Clear();
            if (columns != null)
            {
                foreach (var c in columns)
                {
                    string name = !string.IsNullOrEmpty(c.FieldName) ? c.FieldName : c.HeaderText;
                    if (!string.IsNullOrEmpty(name)) _availableFields.Add(name);
                }
            }
            RebuildTreeUI();
        }

        public void RebuildTreeUI()
        {
            _treePanel.Controls.Clear();
            int curY = 6;
            RenderGroupNode(_rootGroup, 0, ref curY);
        }

        private void RenderGroupNode(GroupFilterNode group, int indentLevel, ref int curY)
        {
            var colors = ZeroTheme.Colors;
            int x = 10 + indentLevel * 24;

            // Group Operator Header Row
            var grpRow = new Panel
            {
                Location = new Point(x, curY),
                Size = new Size(_treePanel.Width - x - 20, 32)
            };

            // [AND/OR] Toggle Button
            var btnOp = new Button
            {
                Text = group.Operator.GetLocalizedName(),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = group.Operator == FilterGroupOperator.Or ? colors.Warning : colors.Primary,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 26),
                Location = new Point(0, 3)
            };
            btnOp.FlatAppearance.BorderSize = 0;
            btnOp.Click += (s, e) =>
            {
                group.Operator = (group.Operator == FilterGroupOperator.And) ? FilterGroupOperator.Or : FilterGroupOperator.And;
                btnOp.Text = group.Operator.GetLocalizedName();
                btnOp.BackColor = group.Operator == FilterGroupOperator.Or ? colors.Warning : colors.Primary;
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
            grpRow.Controls.Add(btnOp);

            // [+] Add Condition Button
            var btnAddCond = new Button
            {
                Text = Localizer.GetString(StringId.FilterAddCondition),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = colors.TextPrimary,
                BackColor = colors.HeaderBackground,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 26),
                Location = new Point(80, 3)
            };
            btnAddCond.FlatAppearance.BorderColor = colors.Border;
            btnAddCond.Click += (s, e) =>
            {
                string field = _availableFields.Count > 0 ? _availableFields[0] : "Field";
                group.AddCondition(field, FilterComparisonOperator.Equals, "");
                RebuildTreeUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
            grpRow.Controls.Add(btnAddCond);

            // [+] Add Group Button
            var btnAddGroup = new Button
            {
                Text = Localizer.GetString(StringId.FilterAddGroup),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = colors.TextPrimary,
                BackColor = colors.HeaderBackground,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(84, 26),
                Location = new Point(188, 3)
            };
            btnAddGroup.FlatAppearance.BorderColor = colors.Border;
            btnAddGroup.Click += (s, e) =>
            {
                group.AddGroup(FilterGroupOperator.And);
                RebuildTreeUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
            grpRow.Controls.Add(btnAddGroup);

            _treePanel.Controls.Add(grpRow);
            curY += 34;

            // Render Children
            for (int i = 0; i < group.Children.Count; i++)
            {
                int childIndex = i;
                var child = group.Children[i];

                if (child is ConditionFilterNode cond)
                {
                    RenderConditionNode(group, cond, childIndex, indentLevel + 1, ref curY);
                }
                else if (child is GroupFilterNode subGroup)
                {
                    RenderGroupNode(subGroup, indentLevel + 1, ref curY);
                }
            }
        }

        private void RenderConditionNode(GroupFilterNode parentGroup, ConditionFilterNode cond, int index, int indentLevel, ref int curY)
        {
            var colors = ZeroTheme.Colors;
            int x = 10 + indentLevel * 24;

            var condRow = new Panel
            {
                Location = new Point(x, curY),
                Size = new Size(_treePanel.Width - x - 20, 32)
            };

            // Delete [X] Button
            var btnDel = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = colors.Danger,
                BackColor = colors.Surface,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(26, 26),
                Location = new Point(0, 3)
            };
            btnDel.FlatAppearance.BorderColor = colors.Border;
            btnDel.Click += (s, e) =>
            {
                parentGroup.Children.RemoveAt(index);
                RebuildTreeUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
            condRow.Controls.Add(btnDel);

            // Field Dropdown
            var cboField = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(130, 26),
                Location = new Point(32, 3)
            };
            if (_availableFields.Count > 0)
            {
                cboField.Items.AddRange(_availableFields.ToArray());
                cboField.SelectedItem = _availableFields.Contains(cond.FieldName) ? cond.FieldName : _availableFields[0];
            }
            else
            {
                cboField.Items.Add(cond.FieldName);
                cboField.SelectedIndex = 0;
            }
            cboField.SelectedIndexChanged += (s, e) =>
            {
                cond.FieldName = cboField.SelectedItem?.ToString() ?? "";
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
            condRow.Controls.Add(cboField);

            // Operator Dropdown
            var cboOp = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(130, 26),
                Location = new Point(168, 3)
            };
            FilterOperatorDisplayItem? selectedOpItem = null;
            foreach (FilterComparisonOperator op in Enum.GetValues(typeof(FilterComparisonOperator)))
            {
                var item = new FilterOperatorDisplayItem(op);
                cboOp.Items.Add(item);
                if (op == cond.Operator) selectedOpItem = item;
            }
            cboOp.SelectedItem = selectedOpItem;
            cboOp.SelectedIndexChanged += (s, e) =>
            {
                if (cboOp.SelectedItem is FilterOperatorDisplayItem item)
                {
                    bool needRebuild = (cond.Operator == FilterComparisonOperator.Between || item.Operator == FilterComparisonOperator.Between ||
                                        cond.Operator == FilterComparisonOperator.IsNull || item.Operator == FilterComparisonOperator.IsNull ||
                                        cond.Operator == FilterComparisonOperator.IsNotNull || item.Operator == FilterComparisonOperator.IsNotNull);
                    cond.Operator = item.Operator;
                    if (needRebuild)
                    {
                        RebuildTreeUI();
                    }
                    FilterChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            condRow.Controls.Add(cboOp);

            // Value Inputs based on Operator
            if (cond.Operator == FilterComparisonOperator.Between)
            {
                var txtVal1 = new TextBox
                {
                    Text = cond.Value,
                    Font = new Font("Segoe UI", 9f),
                    Size = new Size(80, 26),
                    Location = new Point(304, 3)
                };
                txtVal1.TextChanged += (s, e) =>
                {
                    cond.Value = txtVal1.Text;
                    FilterChanged?.Invoke(this, EventArgs.Empty);
                };
                condRow.Controls.Add(txtVal1);

                var lblAnd = new Label
                {
                    Text = "and",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = colors.TextSecondary,
                    AutoSize = true,
                    Location = new Point(388, 7)
                };
                condRow.Controls.Add(lblAnd);

                var txtVal2 = new TextBox
                {
                    Text = cond.Value2,
                    Font = new Font("Segoe UI", 9f),
                    Size = new Size(80, 26),
                    Location = new Point(416, 3)
                };
                txtVal2.TextChanged += (s, e) =>
                {
                    cond.Value2 = txtVal2.Text;
                    FilterChanged?.Invoke(this, EventArgs.Empty);
                };
                condRow.Controls.Add(txtVal2);
            }
            else if (cond.Operator != FilterComparisonOperator.IsNull && cond.Operator != FilterComparisonOperator.IsNotNull)
            {
                var txtValue = new TextBox
                {
                    Text = cond.Value,
                    Font = new Font("Segoe UI", 9f),
                    Size = new Size(140, 26),
                    Location = new Point(304, 3)
                };
                txtValue.TextChanged += (s, e) =>
                {
                    cond.Value = txtValue.Text;
                    FilterChanged?.Invoke(this, EventArgs.Empty);
                };
                condRow.Controls.Add(txtValue);
            }

            _treePanel.Controls.Add(condRow);
            curY += 32;
        }

        public string GetSqlWhere() => _rootGroup.ToSqlWhere();
        public string GetDisplayString() => _rootGroup.ToDisplayString();
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Surface;
                RebuildTreeUI();
            }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for FilterControl.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroFilterControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFilterControl instead.")]
    [ToolboxItem(false)]
    public class ZeroFilterControl : ZFilterControl
    {
    }

    internal sealed class FilterOperatorDisplayItem
    {
        public FilterComparisonOperator Operator { get; }
        public FilterOperatorDisplayItem(FilterComparisonOperator op) => Operator = op;
        public override string ToString() => Operator.GetLocalizedName();
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFilterControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FilterControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFilterControl instead.")]
    [ToolboxItem(false)]
    public class FilterControl : ZFilterControl
    {
    }

    #endregion
}
