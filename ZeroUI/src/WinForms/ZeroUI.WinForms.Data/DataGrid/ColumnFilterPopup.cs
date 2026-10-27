using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Excel-style column filter popup host for ZeroUI WinForms GridControl.
    /// Provides search box, (Select All) toggle, and distinct value multi-selection list.
    /// </summary>
    public class ColumnFilterPopup : IDisposable
    {
        private readonly int _columnIndex;
        private readonly string _columnName;
        private readonly Action<int, HashSet<string>?> _applyCallback;
        private readonly DropDownHost _host;
        private readonly Panel _container;

        private readonly TextBox _searchBox;
        private readonly CheckBox _selectAllCheck;
        private readonly CheckedListBox _valueList;
        private readonly Button _btnOk;
        private readonly Button _btnClear;
        private readonly Button _btnCancel;

        private readonly List<string> _allDistinctValues = new List<string>();
        private readonly HashSet<string> _selectedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ColumnFilterPopup(
            int columnIndex,
            string columnName,
            IEnumerable<string> distinctValues,
            HashSet<string>? currentSelectedValues,
            Action<int, HashSet<string>?> applyCallback)
        {
            _columnIndex = columnIndex;
            _columnName = columnName;
            _applyCallback = applyCallback;

            if (distinctValues != null)
            {
                _allDistinctValues.AddRange(distinctValues);
                _allDistinctValues.Sort(StringComparer.OrdinalIgnoreCase);
            }

            if (currentSelectedValues != null)
            {
                foreach (var val in currentSelectedValues)
                {
                    _selectedValues.Add(val);
                }
            }
            else
            {
                // Default: all checked
                foreach (var val in _allDistinctValues)
                {
                    _selectedValues.Add(val);
                }
            }

            var colors = ZeroTheme.Colors;

            _container = new Panel
            {
                Size = new Size(240, 320),
                BackColor = colors.Surface,
                Padding = new Padding(8)
            };

            // Header title
            var lblTitle = new Label
            {
                Text = $"Filter: {columnName}",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                Location = new Point(8, 8),
                Size = new Size(224, 20)
            };
            _container.Controls.Add(lblTitle);

            // Search box
            _searchBox = new TextBox
            {
                Location = new Point(8, 32),
                Size = new Size(224, 24),
                Font = new Font("Segoe UI", 9f),
                BackColor = colors.Background,
                ForeColor = colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            _searchBox.TextChanged += OnSearchTextChanged;
            _container.Controls.Add(_searchBox);

            // Select All check
            _selectAllCheck = new CheckBox
            {
                Text = "(Select All)",
                Location = new Point(8, 60),
                Size = new Size(224, 22),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = colors.TextSecondary,
                Checked = _selectedValues.Count == _allDistinctValues.Count
            };
            _selectAllCheck.CheckedChanged += OnSelectAllCheckedChanged;
            _container.Controls.Add(_selectAllCheck);

            // Values list
            _valueList = new CheckedListBox
            {
                Location = new Point(8, 86),
                Size = new Size(224, 186),
                Font = new Font("Segoe UI", 9f),
                BackColor = colors.Surface,
                ForeColor = colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };
            _valueList.ItemCheck += OnValueListItemCheck;
            _container.Controls.Add(_valueList);

            // Bottom Buttons
            _btnOk = new Button
            {
                Text = "OK",
                Location = new Point(8, 280),
                Size = new Size(68, 28),
                Font = new Font("Segoe UI", 8.5f),
                BackColor = colors.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) => ApplyAndClose();
            _container.Controls.Add(_btnOk);

            _btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(82, 280),
                Size = new Size(68, 28),
                Font = new Font("Segoe UI", 8.5f),
                BackColor = colors.Background,
                ForeColor = colors.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            _btnClear.FlatAppearance.BorderColor = colors.Border;
            _btnClear.Click += (s, e) => ClearAndClose();
            _container.Controls.Add(_btnClear);

            _host = new DropDownHost
            {
                Content = _container
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(156, 280),
                Size = new Size(76, 28),
                Font = new Font("Segoe UI", 8.5f),
                BackColor = colors.Background,
                ForeColor = colors.TextSecondary,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderColor = colors.Border;
            _btnCancel.Click += (s, e) => _host.Close();
            _container.Controls.Add(_btnCancel);

            PopulateValueList(string.Empty);
        }

        public void Show(Control owner, Point screenLocation)
        {
            _host.Show(screenLocation);
            _searchBox.Focus();
        }

        private void PopulateValueList(string query)
        {
            _valueList.BeginUpdate();
            _valueList.Items.Clear();

            bool filterActive = !string.IsNullOrEmpty(query);
            for (int i = 0; i < _allDistinctValues.Count; i++)
            {
                string val = _allDistinctValues[i];
                if (!filterActive || val.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bool isChecked = _selectedValues.Contains(val);
                    _valueList.Items.Add(val, isChecked);
                }
            }

            _valueList.EndUpdate();
        }

        private void OnSearchTextChanged(object? sender, EventArgs e)
        {
            PopulateValueList(_searchBox.Text.Trim());
        }

        private bool _isUpdatingSelectAll = false;
        private void OnSelectAllCheckedChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingSelectAll) return;
            _isUpdatingSelectAll = true;

            bool check = _selectAllCheck.Checked;
            for (int i = 0; i < _valueList.Items.Count; i++)
            {
                _valueList.SetItemChecked(i, check);
                string itemVal = _valueList.Items[i]?.ToString() ?? "";
                if (check)
                {
                    _selectedValues.Add(itemVal);
                }
                else
                {
                    _selectedValues.Remove(itemVal);
                }
            }

            _isUpdatingSelectAll = false;
        }

        private void OnValueListItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_isUpdatingSelectAll) return;

            string itemVal = _valueList.Items[e.Index]?.ToString() ?? "";
            if (e.NewValue == CheckState.Checked)
            {
                _selectedValues.Add(itemVal);
            }
            else
            {
                _selectedValues.Remove(itemVal);
            }
        }

        private void ApplyAndClose()
        {
            _host.Close();
            if (_selectedValues.Count == _allDistinctValues.Count)
            {
                // All selected = no filter needed
                _applyCallback(_columnIndex, null);
            }
            else
            {
                _applyCallback(_columnIndex, new HashSet<string>(_selectedValues, StringComparer.OrdinalIgnoreCase));
            }
        }

        private void ClearAndClose()
        {
            _host.Close();
            _applyCallback(_columnIndex, null);
        }

        public void Dispose()
        {
            _host?.Dispose();
            _container?.Dispose();
        }
    }
}
