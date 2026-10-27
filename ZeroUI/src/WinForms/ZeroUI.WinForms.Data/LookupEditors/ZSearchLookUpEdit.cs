using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Enterprise high-capacity paginated and debounced search dropdown editor.
    /// Optimized for massive datasets (50,000+ items) such as Bill of Materials (BOMs),
    /// item master catalogs, customer directories, and warehouse pallets.
    /// Features persistent search header with Find/Clear commands, embedded virtual grid,
    /// and dynamic item count status footer.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("SelectionChanged")]
    [DefaultProperty("Placeholder")]
    [Description("High-capacity enterprise search lookup with persistent header search and virtual grid.")]
    [ToolboxBitmap(typeof(ZeroIcons), "SearchLookUpEdit.bmp")]
    public class ZSearchLookUpEdit : ControlBase, IZeroEditor
    {
        private readonly DropDownHost _dropdown;
        private readonly Panel _popupContainer;
        private readonly TextBox _searchBox;
        private readonly Button _btnFind;
        private readonly Button _btnClear;
        private readonly Label _lblStatus;
        private readonly Button _btnAddNew;
        private readonly Button _btnApply;
        private readonly ZGrid _grid;
        private readonly Timer _debounceTimer;
        private readonly SearchFilterEngine _filterEngine = new SearchFilterEngine();

        private string _placeholder = "Click or press Alt+Down to search...";
        private string _displayMember = "Name";
        private string _valueMember = "Id";
        private string _selectedText = string.Empty;
        private object? _selectedValue = null;
        private object? _selectedItem = null;

        private bool _multiSelect = false;
        private bool _showTokens = false;
        private readonly List<object> _selectedValues = new List<object>();
        private readonly List<string> _selectedTexts = new List<string>();

        private int _tokenHeight = 22;
        private int _tokenSpacing = 4;
        private readonly List<Rectangle> _tokenBounds = new List<Rectangle>();
        private readonly List<Rectangle> _closeBounds = new List<Rectangle>();
        private Rectangle _overflowBounds = Rectangle.Empty;
        private int _overflowCount = 0;
        private int _hoveredCloseIndex = -1;
        private bool _isHoveredOverflow = false;

        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isDroppedDown = false;
        private int _debounceDelayMs = 250;
        private bool _isModified = false;
        private bool _isReadOnly = false;
        private bool _showAddNewButton = false;
        private string _addNewButtonText = "+ Add New Record";

        public event EventHandler? SelectionChanged;
        public event EventHandler? DropDownOpened;
        public event EventHandler? DropDownClosed;
        public event EventHandler? EditValueChanged;

        /// <summary>
        /// Occurs when the user requests to add a new record or enters an unlisted search value.
        /// </summary>
        public event EventHandler<ProcessNewValueEventArgs>? ProcessNewValue;

        #region Properties

        [Category("ZeroUI - Behavior")]
        [Description("Enables multi-item selection with checkbox column and persistent selected items.")]
        [DefaultValue(false)]
        public bool MultiSelect
        {
            get => _multiSelect;
            set
            {
                if (_multiSelect != value)
                {
                    _multiSelect = value;
                    _grid.SelectionMode = _multiSelect ? ZeroGridSelectionMode.MultiRow : ZeroGridSelectionMode.SingleRow;
                    _grid.ShowCheckBoxSelectorColumn = _multiSelect;
                    if (_btnApply != null) _btnApply.Visible = _multiSelect;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Displays selected items as interactive vector chips/tokens with dismiss icons when MultiSelect is true.")]
        [DefaultValue(false)]
        public bool ShowTokens
        {
            get => _showTokens;
            set
            {
                if (_showTokens != value)
                {
                    _showTokens = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public IReadOnlyList<object> SelectedValues => _selectedValues;

        [Browsable(false)]
        public IReadOnlyList<string> SelectedTexts => _selectedTexts;

        [Category("ZeroUI - Behavior")]
        [Description("Determines whether the '+ Add New Record' footer action button is visible.")]
        [DefaultValue(false)]
        public bool ShowAddNewButton
        {
            get => _showAddNewButton;
            set
            {
                if (_showAddNewButton != value)
                {
                    _showAddNewButton = value;
                    if (_btnAddNew != null)
                    {
                        _btnAddNew.Visible = _showAddNewButton;
                    }
                }
            }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Text caption displayed on the add new record footer action button.")]
        [DefaultValue("+ Add New Record")]
        public string AddNewButtonText
        {
            get => _addNewButtonText;
            set
            {
                _addNewButtonText = value ?? "+ Add New Record";
                if (_btnAddNew != null)
                {
                    _btnAddNew.Text = _addNewButtonText;
                }
            }
        }

        [Category("ZeroUI - Behavior")]
        [Description("Debounce delay in milliseconds before executing background filter.")]
        [DefaultValue(250)]
        public int DebounceDelayMs
        {
            get => _debounceDelayMs;
            set
            {
                _debounceDelayMs = Math.Max(50, value);
                _debounceTimer.Interval = _debounceDelayMs;
            }
        }

        [Category("ZeroUI - Behavior")]
        [Description("Matching mode for multi-term query tokens (All / AND or Any / OR).")]
        [DefaultValue(SearchTokenMatchMode.All)]
        public SearchTokenMatchMode MatchMode
        {
            get => _filterEngine.MatchMode;
            set => _filterEngine.MatchMode = value;
        }

        [Category("ZeroUI - Appearance")]
        [Description("Placeholder text displayed when no item is selected.")]
        [DefaultValue("Click or press Alt+Down to search...")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? string.Empty;
                Invalidate();
            }
        }

        [Category("ZeroUI - Data")]
        [Description("Property name displayed in the editor after selection.")]
        [DefaultValue("Name")]
        public string DisplayMember
        {
            get => _displayMember;
            set => _displayMember = value ?? "Name";
        }

        [Category("ZeroUI - Data")]
        [Description("Property name providing the key or value of the selected item.")]
        [DefaultValue("Id")]
        public string ValueMember
        {
            get => _valueMember;
            set => _valueMember = value ?? "Id";
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? SelectedValue
        {
            get => _selectedValue;
            set
            {
                if (!Equals(_selectedValue, value))
                {
                    _selectedValue = value;
                    _isModified = true;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? SelectedItem
        {
            get => _selectedItem;
            set => _selectedItem = value;
        }

        [Browsable(false)]
        public string SelectedText => _selectedText;

        [Browsable(false)]
        public ZGrid Grid => _grid;

        [Browsable(false)]
        public int TotalRecords => _grid.DataSource?.TotalRowCount ?? 0;

        [Browsable(false)]
        public int FilteredRecords => _grid.VisualRowCount;

        #region IZeroEditor Implementation

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => _multiSelect ? (object)_selectedValues.ToArray() : SelectedValue;
            set
            {
                if (_multiSelect)
                {
                    _selectedValues.Clear();
                    _selectedTexts.Clear();
                    if (value is System.Collections.IEnumerable enumerable && !(value is string))
                    {
                        foreach (var item in enumerable)
                        {
                            if (item != null)
                            {
                                _selectedValues.Add(item);
                                _selectedTexts.Add(item.ToString() ?? string.Empty);
                            }
                        }
                    }
                    else if (value != null)
                    {
                        _selectedValues.Add(value);
                        _selectedTexts.Add(value.ToString() ?? string.Empty);
                    }
                    _isModified = true;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    SelectedValue = value;
                }
            }
        }

        [Category("ZeroUI - Behavior")]
        [DefaultValue(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Category("ZeroUI - Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _isReadOnly;
            set => _isReadOnly = value;
        }

        public void Reset()
        {
            _selectedValue = null;
            _selectedItem = null;
            _selectedText = string.Empty;
            _selectedValues.Clear();
            _selectedTexts.Clear();
            _isModified = false;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear() => Reset();

        #endregion

        #endregion

        public ZSearchLookUpEdit()
        {
            Size = new Size(280, 36);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            // 1. Instantiate Controls & Timers
            _debounceTimer = new Timer
            {
                Interval = _debounceDelayMs
            };

            _grid = new ZGrid
            {
                Dock = DockStyle.Fill
            };

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };

            _btnFind = new Button
            {
                Dock = DockStyle.Right,
                Width = 60,
                Text = "Find",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = ZeroTheme.Colors.Primary,
                ForeColor = Color.White
            };
            if (_btnFind.FlatAppearance != null)
            {
                _btnFind.FlatAppearance.BorderSize = 0;
            }

            _btnClear = new Button
            {
                Dock = DockStyle.Right,
                Width = 56,
                Text = "Clear",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = ZeroTheme.Colors.TextSecondary
            };
            if (_btnClear.FlatAppearance != null)
            {
                _btnClear.FlatAppearance.BorderSize = 0;
            }

            _btnAddNew = new Button
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                Text = _addNewButtonText,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = ZeroTheme.Colors.Primary,
                Visible = _showAddNewButton
            };
            if (_btnAddNew.FlatAppearance != null)
            {
                _btnAddNew.FlatAppearance.BorderSize = 0;
            }
            _btnAddNew.Click += (s, e) => HandleAddNewRecord();

            _btnApply = new Button
            {
                Dock = DockStyle.Right,
                Width = 60,
                Text = "Done",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = ZeroTheme.Colors.Primary,
                ForeColor = Color.White,
                Visible = _multiSelect
            };
            if (_btnApply.FlatAppearance != null)
            {
                _btnApply.FlatAppearance.BorderSize = 0;
            }
            _btnApply.Click += (s, e) =>
            {
                SyncMultiSelection();
                _dropdown?.Close();
            };

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ready"
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 7, 8, 7),
                BackColor = ZeroTheme.Colors.Surface
            };
            headerPanel.Controls.Add(_searchBox);
            headerPanel.Controls.Add(_btnClear);
            headerPanel.Controls.Add(_btnFind);

            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Padding = new Padding(10, 5, 10, 5),
                BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 30, 35) : Color.FromArgb(245, 246, 248)
            };
            footerPanel.Controls.Add(_lblStatus);
            footerPanel.Controls.Add(_btnAddNew);
            footerPanel.Controls.Add(_btnApply);

            _popupContainer = new Panel
            {
                Size = new Size(560, 360),
                BackColor = ZeroTheme.Colors.Surface,
                Padding = new Padding(1)
            };
            _popupContainer.Controls.Add(_grid);
            _popupContainer.Controls.Add(headerPanel);
            _popupContainer.Controls.Add(footerPanel);
            _grid.BringToFront();

            _dropdown = new DropDownHost
            {
                Content = _popupContainer
            };

            // 2. Wire Events
            _dropdown.Closed += (s, e) =>
            {
                _isDroppedDown = false;
                DropDownClosed?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };
            _dropdown.Opened += (s, e) =>
            {
                _isDroppedDown = true;
                DropDownOpened?.Invoke(this, EventArgs.Empty);
                _searchBox.Focus();
                _searchBox.SelectAll();
                UpdateStatusLabel();
                Invalidate();
            };

            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ExecuteSearch();
            };

            _grid.SelectionChanged += (s, e) =>
            {
                if (_multiSelect)
                {
                    SyncMultiSelection();
                }
            };

            _grid.DoubleClick += (s, e) => CommitSelection();
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitSelection();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _dropdown.Close();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Up && _grid.SelectedVisualRow <= 0)
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                    e.Handled = true;
                }
            };

            _btnFind.Click += (s, e) =>
            {
                _debounceTimer.Stop();
                ExecuteSearch();
            };

            _btnClear.Click += (s, e) =>
            {
                _debounceTimer.Stop();
                _searchBox.Text = string.Empty;
                ExecuteSearch();
                _searchBox.Focus();
            };

            _searchBox.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    _debounceTimer.Stop();
                    ExecuteSearch();
                    if (_grid.VisualRowCount > 0)
                    {
                        CommitSelection();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (_grid.VisualRowCount > 0)
                    {
                        _grid.Focus();
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _dropdown.Close();
                    e.Handled = true;
                }
            };
        }

        public void SetDataSource<T>(IList<T> items)
        {
            _grid.SetDataSource(items);
            UpdateStatusLabel();
        }

        public void ShowDropDown()
        {
            if (_isReadOnly || _isDroppedDown) return;

            int width = Math.Max(Width, 560);
            int height = 360;
            _dropdown.ShowDropDown(this, width, height);
        }

        public void CloseDropDown()
        {
            if (_isDroppedDown)
            {
                _dropdown.Close();
            }
        }

        private void ExecuteSearch()
        {
            string query = _searchBox.Text.Trim();
            _filterEngine.SetQuery(query);

            if (!_filterEngine.HasFilter)
            {
                _grid.ApplyFilter(null);
                UpdateStatusLabel();
                return;
            }

            var src = _grid.DataSource;
            if (src == null)
            {
                UpdateStatusLabel();
                return;
            }

            int colCount = _grid.Columns.Count;
            var cellValues = new List<string>(colCount);

            _grid.ApplyFilter(modelRow =>
            {
                cellValues.Clear();
                CellValueBuffer buf = new CellValueBuffer();
                for (int c = 0; c < colCount; c++)
                {
                    if (!_grid.Columns[c].IsVisible) continue;
                    buf.Reset();
                    src.GetCellValue(modelRow, c, ref buf);
                    cellValues.Add(buf.Text.ToString());
                }
                return _filterEngine.MatchesAnyColumn(cellValues);
            });

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            int total = TotalRecords;
            int filtered = FilteredRecords;
            _lblStatus.Text = _filterEngine.FormatStatusText(filtered, filtered, total);
        }

        private void CommitSelection()
        {
            if (_multiSelect)
            {
                SyncMultiSelection();
                _dropdown.Close();
                return;
            }

            int visualRow = _grid.SelectedVisualRow;
            if (visualRow < 0) return;

            int modelRow = _grid.GetModelRowIndex(visualRow);
            var src = _grid.DataSource;
            if (modelRow < 0 || src == null) return;

            // Extract display value from column matching DisplayMember or first visible column
            CellValueBuffer buf = new CellValueBuffer();
            int displayCol = GetDisplayColumnIndex();

            src.GetCellValue(modelRow, displayCol, ref buf);
            _selectedText = buf.Text.ToString();
            _selectedValue = modelRow;
            _isModified = true;

            _dropdown.Close();
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private int GetDisplayColumnIndex()
        {
            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                if (string.Equals(_grid.Columns[i].FieldName, _displayMember, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return 0;
        }

        private void SyncMultiSelection()
        {
            if (!_multiSelect) return;
            var src = _grid.DataSource;
            if (src == null) return;

            _selectedValues.Clear();
            _selectedTexts.Clear();

            int displayCol = GetDisplayColumnIndex();
            CellValueBuffer buf = new CellValueBuffer();

            foreach (int vRow in _grid.SelectedVisualRows)
            {
                int mRow = _grid.GetModelRowIndex(vRow);
                if (mRow >= 0)
                {
                    buf.Reset();
                    src.GetCellValue(mRow, displayCol, ref buf);
                    _selectedValues.Add(mRow);
                    _selectedTexts.Add(buf.Text.ToString());
                }
            }

            _isModified = true;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Paint & UI Interaction

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowDropDown();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                ShowDropDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.F4)
            {
                ShowDropDown();
                e.Handled = true;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _hoveredCloseIndex = -1;
            _isHoveredOverflow = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_multiSelect && _showTokens)
            {
                int prevHover = _hoveredCloseIndex;
                bool prevPillHover = _isHoveredOverflow;

                _hoveredCloseIndex = -1;
                _isHoveredOverflow = false;

                for (int i = 0; i < _closeBounds.Count; i++)
                {
                    if (_closeBounds[i].Contains(e.Location))
                    {
                        _hoveredCloseIndex = i;
                        break;
                    }
                }

                if (_hoveredCloseIndex == -1 && !_overflowBounds.IsEmpty && _overflowBounds.Contains(e.Location))
                {
                    _isHoveredOverflow = true;
                }

                if (prevHover != _hoveredCloseIndex || prevPillHover != _isHoveredOverflow)
                {
                    Invalidate();
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _multiSelect && _showTokens)
            {
                for (int i = 0; i < _closeBounds.Count; i++)
                {
                    if (_closeBounds[i].Contains(e.Location))
                    {
                        _selectedValues.RemoveAt(i);
                        _selectedTexts.RemoveAt(i);
                        _isModified = true;
                        Invalidate();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                        EditValueChanged?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = CurrentPalette;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw Background & Border
            using (var path = CreateRoundedRectanglePath(bounds, 4))
            {
                using (var brush = new SolidBrush(colors.Surface))
                {
                    g.FillPath(brush, path);
                }

                Color borderColor = _isDroppedDown || _isFocused
                    ? colors.Primary
                    : (_isHovered ? colors.PrimaryHover : colors.Border);

                using (var pen = new Pen(borderColor, _isDroppedDown || _isFocused ? 1.5f : 1.0f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw Search Magnifier Icon (Vector)
            int iconX = 10;
            int iconY = (Height - 14) / 2;
            using (var iconPen = new Pen(_isHovered || _isDroppedDown ? colors.Primary : colors.TextSecondary, 1.6f))
            {
                g.DrawEllipse(iconPen, iconX, iconY, 9, 9);
                g.DrawLine(iconPen, iconX + 7, iconY + 7, iconX + 13, iconY + 13);
            }

            // Draw Dropdown Chevron Arrow
            int arrowX = Width - 24;
            int arrowY = Height / 2;
            using (var pen = new Pen(_isHovered || _isDroppedDown ? colors.Primary : colors.TextSecondary, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (_isDroppedDown)
                {
                    g.DrawLine(pen, arrowX, arrowY + 2, arrowX + 4, arrowY - 2);
                    g.DrawLine(pen, arrowX + 4, arrowY - 2, arrowX + 8, arrowY + 2);
                }
                else
                {
                    g.DrawLine(pen, arrowX, arrowY - 2, arrowX + 4, arrowY + 2);
                    g.DrawLine(pen, arrowX + 4, arrowY + 2, arrowX + 8, arrowY - 2);
                }
            }

            // Draw Display Text, Summary, or Tokens
            if (_multiSelect && _showTokens)
            {
                DrawTokens(g, colors);
            }
            else if (_multiSelect)
            {
                DrawMultiTextSummary(g, colors);
            }
            else
            {
                DrawSingleText(g, colors);
            }
        }

        private void DrawSingleText(Graphics g, ZeroThemePalette colors)
        {
            int textX = 30;
            int textW = Width - textX - 30;
            var textRect = new Rectangle(textX, 0, textW, Height);

            string textToDraw = !string.IsNullOrEmpty(_selectedText) ? _selectedText : _placeholder;
            Color textColor = !string.IsNullOrEmpty(_selectedText) ? colors.TextPrimary : colors.TextSecondary;

            TextRenderer.DrawText(
                g,
                textToDraw,
                Font,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawMultiTextSummary(Graphics g, ZeroThemePalette colors)
        {
            int textX = 30;
            int textW = Width - textX - 30;
            var textRect = new Rectangle(textX, 0, textW, Height);

            string textToDraw;
            Color textColor;

            if (_selectedTexts.Count == 0)
            {
                textToDraw = _placeholder;
                textColor = colors.TextSecondary;
            }
            else if (_selectedTexts.Count <= 2)
            {
                textToDraw = string.Join(", ", _selectedTexts);
                textColor = colors.TextPrimary;
            }
            else
            {
                textToDraw = string.Format("{0} items selected", _selectedTexts.Count);
                textColor = colors.TextPrimary;
            }

            TextRenderer.DrawText(
                g,
                textToDraw,
                Font,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawTokens(Graphics g, ZeroThemePalette colors)
        {
            _tokenBounds.Clear();
            _closeBounds.Clear();
            _overflowBounds = Rectangle.Empty;
            _overflowCount = 0;

            if (_selectedTexts.Count == 0)
            {
                DrawSingleText(g, colors);
                return;
            }

            int curX = 30; // After magnifier icon
            int curY = (Height - _tokenHeight) / 2;
            int maxAvailableWidth = Width - 32; // Leave room for chevron

            for (int k = 0; k < _selectedTexts.Count; k++)
            {
                string itemText = _selectedTexts[k];
                var size = g.MeasureString(itemText, Font);
                int tokenW = (int)Math.Ceiling(size.Width) + 24;
                int remainingItems = _selectedTexts.Count - k;

                if (curX + tokenW > maxAvailableWidth)
                {
                    _overflowCount = remainingItems;
                    string overflowText = $"+{_overflowCount} more";
                    var pillSize = g.MeasureString(overflowText, Font);
                    int pillW = (int)Math.Ceiling(pillSize.Width) + 12;

                    if (curX + pillW > maxAvailableWidth && _tokenBounds.Count > 0)
                    {
                        pillW = Math.Max(24, maxAvailableWidth - curX);
                    }

                    var pillRect = new Rectangle(curX, curY, pillW, _tokenHeight);
                    _overflowBounds = pillRect;

                    using (var pillPath = CreateRoundedRectanglePath(pillRect, 4))
                    {
                        Color pillBg = _isHoveredOverflow ? Color.FromArgb(60, colors.Primary) : Color.FromArgb(30, colors.Primary);
                        using (var pillBrush = new SolidBrush(pillBg))
                        {
                            g.FillPath(pillBrush, pillPath);
                        }
                        using (var pillPen = new Pen(colors.Primary, 1f))
                        {
                            g.DrawPath(pillPen, pillPath);
                        }
                    }

                    using (var pillTextBrush = new SolidBrush(colors.Primary))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        };
                        g.DrawString(overflowText, Font, pillTextBrush, pillRect, sf);
                    }

                    break;
                }

                var tokenRect = new Rectangle(curX, curY, tokenW, _tokenHeight);
                var closeRect = new Rectangle(curX + tokenW - 16, curY + (_tokenHeight - 10) / 2, 10, 10);

                _tokenBounds.Add(tokenRect);
                _closeBounds.Add(closeRect);

                // Chip background & border
                using (var tPath = CreateRoundedRectanglePath(tokenRect, 4))
                {
                    using (var tBrush = new SolidBrush(Color.FromArgb(28, colors.Primary)))
                    {
                        g.FillPath(tBrush, tPath);
                    }
                    using (var tPen = new Pen(Color.FromArgb(90, colors.Primary), 1f))
                    {
                        g.DrawPath(tPen, tPath);
                    }
                }

                // Chip Text
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var textRect = new Rectangle(tokenRect.X + 6, tokenRect.Y, tokenRect.Width - 22, tokenRect.Height);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(itemText, Font, brush, textRect, sf);
                }

                // Close '✕'
                bool isCloseHovered = (_tokenBounds.Count - 1) == _hoveredCloseIndex;
                Color xColor = isCloseHovered ? colors.Danger : colors.TextSecondary;
                using (var pen = new Pen(xColor, 1.4f))
                {
                    g.DrawLine(pen, closeRect.X + 1, closeRect.Y + 1, closeRect.Right - 1, closeRect.Bottom - 1);
                    g.DrawLine(pen, closeRect.Right - 1, closeRect.Y + 1, closeRect.X + 1, closeRect.Bottom - 1);
                }

                curX += tokenW + _tokenSpacing;
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void HandleAddNewRecord()
        {
            var args = new ProcessNewValueEventArgs(_searchBox.Text);
            ProcessNewValue?.Invoke(this, args);
            if (args.Handled)
            {
                if (args.NewValue != null)
                {
                    SelectedValue = args.NewValue;
                }
                _dropdown.Close();
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            var palette = CurrentPalette;
            if (_popupContainer != null) _popupContainer.BackColor = palette.Surface;
            if (_searchBox != null)
            {
                _searchBox.BackColor = palette.Surface;
                _searchBox.ForeColor = palette.TextPrimary;
            }
            if (_btnFind != null) _btnFind.BackColor = palette.Primary;
            if (_btnClear != null) _btnClear.ForeColor = palette.TextSecondary;
            if (_lblStatus != null) _lblStatus.ForeColor = palette.TextSecondary;
            if (_btnAddNew != null) _btnAddNew.ForeColor = palette.Primary;
            if (_btnApply != null) _btnApply.BackColor = palette.Primary;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounceTimer.Dispose();
                _dropdown.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSearchLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SearchLookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchLookUpEdit instead.")]
    [ToolboxItem(false)]
    public class SearchLookUpEdit : ZSearchLookUpEdit
    {
    }

    #endregion
}
