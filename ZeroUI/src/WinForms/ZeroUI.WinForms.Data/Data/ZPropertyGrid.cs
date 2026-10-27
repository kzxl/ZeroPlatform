using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Data
{
    /// <summary>
    /// Modern Enterprise PropertyGrid inspector control for ZeroUI WinForms.
    /// Inspects and edits properties of any object or manual property model with collapsible categories,
    /// live search filtering, in-place type-safe editors, and description card.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("PropertyValueChanged")]
    [Description("Enterprise property inspector with categories, search filter, and in-place editors")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPropertyGrid.bmp")]
    public class ZPropertyGrid : Control
    {
        private readonly PropertyModel _model = new PropertyModel();
        private readonly Panel _searchPanel;
        private readonly TextBox _searchBox;
        private readonly Panel _scrollContainer;
        private readonly Panel _descPanel;
        private readonly Label _lblDescTitle;
        private readonly Label _lblDescBody;

        private int _splitPosition = 160;
        private PropertyItem? _selectedItem;

        public event EventHandler<ZeroUI.Core.Data.PropertyValueChangedEventArgs>? PropertyValueChanged;

        [Browsable(false)]
        public PropertyModel Model => _model;

        [Category("Data")]
        public object? SelectedObject
        {
            get => _model.SelectedObject;
            set
            {
                _model.SetSelectedObject(value);
                RebuildPropertyTree();
            }
        }

        public ZPropertyGrid()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Surface;
            Size = new Size(320, 480);

            // 1. Search Panel Header
            _searchPanel = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6) };
            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _model.SearchFilter = _searchBox.Text.Trim();
                RebuildPropertyTree();
            };
            _searchPanel.Controls.Add(_searchBox);
            Controls.Add(_searchPanel);

            // 2. Bottom Description Panel
            _descPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = ZeroTheme.Colors.HeaderBackground,
                Padding = new Padding(10, 6, 10, 6)
            };
            _descPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(ZeroTheme.Colors.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _descPanel.Width, 0);
                }
            };
            _lblDescTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Text = "Property Description"
            };
            _lblDescBody = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Text = "Select a property to view its details."
            };
            _descPanel.Controls.Add(_lblDescBody);
            _descPanel.Controls.Add(_lblDescTitle);
            Controls.Add(_descPanel);

            // 3. Central Scroll Container
            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Surface
            };
            Controls.Add(_scrollContainer);
            _scrollContainer.BringToFront();

            _model.PropertyValueChanged += (s, e) => PropertyValueChanged?.Invoke(this, e);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void RebuildPropertyTree()
        {
            _scrollContainer.Controls.Clear();
            int curY = 4;

            foreach (var cat in _model.Categories)
            {
                // Category Header
                var catHeader = new Panel
                {
                    Location = new Point(0, curY),
                    Size = new Size(_scrollContainer.Width - 20, 26),
                    BackColor = ZeroTheme.Colors.HeaderBackground,
                    Cursor = Cursors.Hand
                };
                catHeader.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
                    {
                        string icon = cat.IsExpanded ? "▼ " : "▶ ";
                        g.DrawString(icon + cat.Name.ToUpperInvariant(), font, brush, 8, 5);
                    }
                };
                catHeader.Click += (s, e) =>
                {
                    cat.IsExpanded = !cat.IsExpanded;
                    RebuildPropertyTree();
                };
                _scrollContainer.Controls.Add(catHeader);
                curY += 28;

                if (!cat.IsExpanded) continue;

                // Items under this Category
                foreach (var item in cat.Items)
                {
                    var itemRow = new Panel
                    {
                        Location = new Point(0, curY),
                        Size = new Size(_scrollContainer.Width - 20, 30),
                        BackColor = (item == _selectedItem) ? ZeroTheme.Colors.Hover : ZeroTheme.Colors.Surface
                    };

                    itemRow.Paint += (s, e) =>
                    {
                        var g = e.Graphics;
                        using (var pen = new Pen(ZeroTheme.Colors.Border))
                        {
                            g.DrawLine(pen, 0, itemRow.Height - 1, itemRow.Width, itemRow.Height - 1);
                            g.DrawLine(pen, _splitPosition, 0, _splitPosition, itemRow.Height);
                        }

                        using (var font = new Font("Segoe UI", 9f))
                        using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
                        {
                            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                            g.DrawString(item.DisplayName, font, brush, new Rectangle(18, 0, _splitPosition - 22, itemRow.Height), sf);
                        }
                    };

                    itemRow.Click += (s, e) => SelectProperty(item);

                    // Create In-Place Editor on right side
                    int editorX = _splitPosition + 4;
                    int editorW = itemRow.Width - editorX - 4;

                    if (item.EditorType == PropertyEditorType.Boolean)
                    {
                        var chk = new CheckBox
                        {
                            Location = new Point(editorX + 4, (itemRow.Height - 20) / 2),
                            Checked = (bool)(item.Value ?? false),
                            Enabled = !item.IsReadOnly
                        };
                        chk.CheckedChanged += (s, e) => item.Value = chk.Checked;
                        itemRow.Controls.Add(chk);
                    }
                    else if (item.EditorType == PropertyEditorType.Dropdown && item.Choices != null)
                    {
                        var cbo = new ComboBox
                        {
                            Location = new Point(editorX, (itemRow.Height - 24) / 2),
                            Width = editorW,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Font = new Font("Segoe UI", 8.5f),
                            Enabled = !item.IsReadOnly
                        };
                        cbo.Items.AddRange(item.Choices);
                        cbo.SelectedItem = item.Value?.ToString();
                        cbo.SelectedIndexChanged += (s, e) => item.Value = cbo.SelectedItem;
                        itemRow.Controls.Add(cbo);
                    }
                    else
                    {
                        var txt = new TextBox
                        {
                            Location = new Point(editorX, (itemRow.Height - 22) / 2),
                            Width = editorW,
                            BorderStyle = BorderStyle.None,
                            Font = new Font("Segoe UI", 9f),
                            Text = item.Value?.ToString() ?? "",
                            ReadOnly = item.IsReadOnly,
                            BackColor = itemRow.BackColor,
                            ForeColor = ZeroTheme.Colors.TextPrimary
                        };
                        txt.LostFocus += (s, e) => item.Value = txt.Text;
                        txt.Enter += (s, e) => SelectProperty(item);
                        itemRow.Controls.Add(txt);
                    }

                    _scrollContainer.Controls.Add(itemRow);
                    curY += 31;
                }
            }
        }

        private void SelectProperty(PropertyItem item)
        {
            _selectedItem = item;
            _lblDescTitle.Text = item.DisplayName;
            _lblDescBody.Text = string.IsNullOrEmpty(item.Description)
                ? $"Type: {item.PropertyType.Name}  |  Category: {item.Category}"
                : item.Description;
            Invalidate(true);
        }
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Surface;
                _searchBox.BackColor = ZeroTheme.Colors.Surface;
                _searchBox.ForeColor = ZeroTheme.Colors.TextPrimary;
                _descPanel.BackColor = ZeroTheme.Colors.HeaderBackground;
                _lblDescTitle.ForeColor = ZeroTheme.Colors.TextPrimary;
                _lblDescBody.ForeColor = ZeroTheme.Colors.TextSecondary;
                RebuildPropertyTree();
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
    /// Legacy alias for <see cref="ZPropertyGrid"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPropertyGrid is deprecated. Please use PropertyGridControl instead.")]
    [ToolboxItem(false)]
    public class ZeroPropertyGrid : PropertyGridControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPropertyGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PropertyGridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPropertyGrid instead.")]
    [ToolboxItem(false)]
    public class PropertyGridControl : ZPropertyGrid
    {
    }

    #endregion
}
