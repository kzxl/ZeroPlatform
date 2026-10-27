using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern container control for grouping and arranging ZeroRadioButton options.
    /// Supports Horizontal, Vertical, or multi-column Grid layouts with two-way selection synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("SelectedIndex")]
    [DefaultEvent("SelectedIndexChanged")]
    [Description("Radio button group container with auto-layout and selection management")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroRadioGroup.bmp")]
    public class ZRadioGroup : ControlBase
    {
        private string[] _items = Array.Empty<string>();
        private readonly SelectionModel<string> _selection = new SelectionModel<string>();
        private Orientation _orientation = Orientation.Vertical;
        private int _columns = 1;
        private int _itemSpacing = 8;
        private int _itemHeight = 26;
        private int _itemWidth = 140;
        private bool _isUpdating = false;

        public event EventHandler? SelectedIndexChanged;

        public ZRadioGroup()
        {
            Size = new Size(240, 100);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _selection.SetSource(() => _items.Length, idx => _items[idx]);
            _selection.SelectionChanged += (s, e) =>
            {
                SyncChildCheckStates();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };

            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                UpdateChildFonts();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            foreach (Control c in Controls)
            {
                if (c is ZRadioButton rb)
                {
                    rb.UseDefaultSkin = UseDefaultSkin;
                    rb.CustomSkin = CustomSkin;
                }
            }
            Invalidate();
        }

        [Browsable(false)]
        public SelectionModel<string> Selection => _selection;

        [Category("Data")]
        [Description("Collection of item labels to display as radio buttons")]
        public string[] Items
        {
            get => _items;
            set
            {
                _items = value ?? Array.Empty<string>();
                RebuildChildButtons();
            }
        }

        [Category("Layout")]
        [DefaultValue(Orientation.Vertical)]
        [Description("Arrangement orientation of the radio buttons")]
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    ArrangeButtons();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(1)]
        [Description("Number of columns for grid layout. Ignored if Orientation is Horizontal.")]
        public int Columns
        {
            get => _columns;
            set
            {
                if (_columns != Math.Max(1, value))
                {
                    _columns = Math.Max(1, value);
                    ArrangeButtons();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(8)]
        public int ItemSpacing
        {
            get => _itemSpacing;
            set
            {
                if (_itemSpacing != value)
                {
                    _itemSpacing = value;
                    ArrangeButtons();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(26)]
        public int ItemHeight
        {
            get => _itemHeight;
            set
            {
                if (_itemHeight != value)
                {
                    _itemHeight = value;
                    ArrangeButtons();
                }
            }
        }

        [Category("Layout")]
        [DefaultValue(140)]
        public int ItemWidth
        {
            get => _itemWidth;
            set
            {
                if (_itemWidth != value)
                {
                    _itemWidth = value;
                    ArrangeButtons();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(-1)]
        [Description("Zero-based index of the currently selected radio option")]
        public int SelectedIndex
        {
            get => _selection.SelectedIndex;
            set => _selection.SelectIndex(value);
        }

        [Browsable(false)]
        public string? SelectedItem
        {
            get => _selection.SelectedItem;
            set => _selection.SelectItem(value);
        }

        private void RebuildChildButtons()
        {
            _isUpdating = true;
            try
            {
                Controls.Clear();
                string grpName = $"RadioGroup_{GetHashCode()}";

                for (int i = 0; i < _items.Length; i++)
                {
                    int index = i;
                    var rb = new ZRadioButton
                    {
                        Text = _items[i],
                        Font = Font,
                        GroupName = grpName,
                        Checked = (i == _selection.SelectedIndex),
                        Tag = index,
                        UseDefaultSkin = UseDefaultSkin,
                        CustomSkin = CustomSkin
                    };

                    rb.CheckedChanged += (s, e) =>
                    {
                        if (!_isUpdating && rb.Checked)
                        {
                            SelectedIndex = (int)rb.Tag!;
                        }
                    };

                    Controls.Add(rb);
                }
            }
            finally
            {
                _isUpdating = false;
            }

            ArrangeButtons();
        }

        private void SyncChildCheckStates()
        {
            _isUpdating = true;
            try
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    if (Controls[i] is ZRadioButton rb)
                    {
                        rb.Checked = (i == _selection.SelectedIndex);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateChildFonts()
        {
            foreach (Control c in Controls)
            {
                if (c is ZRadioButton rb)
                {
                    rb.Font = Font;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ArrangeButtons();
        }

        private void ArrangeButtons()
        {
            if (Controls.Count == 0) return;

            SuspendLayout();
            try
            {
                int count = Controls.Count;

                if (_orientation == Orientation.Horizontal)
                {
                    int curX = 0;
                    int curY = 0;

                    for (int i = 0; i < count; i++)
                    {
                        var rb = Controls[i];
                        int btnW = _itemWidth > 0 ? _itemWidth : 120;

                        if (curX + btnW > Width && curX > 0)
                        {
                            curX = 0;
                            curY += _itemHeight + _itemSpacing;
                        }

                        rb.SetBounds(curX, curY, btnW, _itemHeight);
                        curX += btnW + _itemSpacing;
                    }
                }
                else
                {
                    if (_columns <= 1)
                    {
                        int curY = 0;
                        int btnW = Math.Max(50, Width - 4);

                        for (int i = 0; i < count; i++)
                        {
                            Controls[i].SetBounds(0, curY, btnW, _itemHeight);
                            curY += _itemHeight + _itemSpacing;
                        }
                    }
                    else
                    {
                        int colWidth = Math.Max(40, (Width - (_columns - 1) * _itemSpacing) / _columns);

                        for (int i = 0; i < count; i++)
                        {
                            int col = i % _columns;
                            int row = i / _columns;

                            int x = col * (colWidth + _itemSpacing);
                            int y = row * (_itemHeight + _itemSpacing);

                            Controls[i].SetBounds(x, y, colWidth, _itemHeight);
                        }
                    }
                }
            }
            finally
            {
                ResumeLayout();
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZRadioGroup"/>.
    /// </summary>
    [Obsolete("ZeroRadioGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioGroup instead.")]
    [ToolboxItem(false)]
    public class ZeroRadioGroup : ZRadioGroup
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRadioGroup"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadioGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioGroup instead.")]
    [ToolboxItem(false)]
    public class RadioGroup : ZRadioGroup
    {
    }

    #endregion
}
