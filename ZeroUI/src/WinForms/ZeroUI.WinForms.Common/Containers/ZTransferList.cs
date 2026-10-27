using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Dual-list transfer control for moving items between Available and Selected collections,
    /// complete with batch transfer buttons (&gt;, &lt;, &gt;&gt;, &lt;&lt;) and theme integration.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Containers")]
    [DefaultProperty("AvailableItems")]
    [DefaultEvent("ItemsTransferred")]
    [Description("Dual list transfer box for permissions, roles, and item selection")]
    public class ZTransferList : ControlBase
    {
        private readonly ListBox _leftList;
        private readonly ListBox _rightList;
        private readonly Panel _centerPanel;
        private readonly Button _btnRight;
        private readonly Button _btnAllRight;
        private readonly Button _btnLeft;
        private readonly Button _btnAllLeft;
        private readonly Label _leftHeader;
        private readonly Label _rightHeader;

        private readonly List<object> _availableItems = new List<object>();
        private readonly List<object> _selectedItems = new List<object>();

        public event EventHandler? ItemsTransferred;

        [Category("Data")]
        [Description("Collection of available source items.")]
        public object AvailableItems
        {
            get => _availableItems;
            set
            {
                _availableItems.Clear();
                if (value is IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null) _availableItems.Add(item);
                    }
                }
                BindLists();
            }
        }

        [Category("Data")]
        [Description("Collection of selected target items.")]
        public object SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems.Clear();
                if (value is IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null) _selectedItems.Add(item);
                    }
                }
                BindLists();
            }
        }

        [Category("Appearance")]
        [DefaultValue("Available")]
        [Description("Header title for the left available items list.")]
        public string AvailableTitle
        {
            get => _leftHeader.Text;
            set => _leftHeader.Text = value;
        }

        [Category("Appearance")]
        [DefaultValue("Selected")]
        [Description("Header title for the right selected items list.")]
        public string SelectedTitle
        {
            get => _rightHeader.Text;
            set => _rightHeader.Text = value;
        }

        public ZTransferList()
        {
            Size = new Size(420, 240);

            _leftHeader = new Label
            {
                Text = "Available",
                Height = 20,
                Font = ZeroFontCache.Get(8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _rightHeader = new Label
            {
                Text = "Selected",
                Height = 20,
                Font = ZeroFontCache.Get(8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _leftList = new ListBox
            {
                SelectionMode = SelectionMode.MultiExtended,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };

            _rightList = new ListBox
            {
                SelectionMode = SelectionMode.MultiExtended,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };

            _centerPanel = new Panel { Width = 48 };

            _btnRight = CreateButton(">");
            _btnAllRight = CreateButton(">>");
            _btnLeft = CreateButton("<");
            _btnAllLeft = CreateButton("<<");

            _btnRight.Click += (s, e) => TransferSelectedToRight();
            _btnAllRight.Click += (s, e) => TransferAllToRight();
            _btnLeft.Click += (s, e) => TransferSelectedToLeft();
            _btnAllLeft.Click += (s, e) => TransferAllToLeft();

            _centerPanel.Controls.Add(_btnRight);
            _centerPanel.Controls.Add(_btnAllRight);
            _centerPanel.Controls.Add(_btnLeft);
            _centerPanel.Controls.Add(_btnAllLeft);

            Controls.Add(_leftHeader);
            Controls.Add(_rightHeader);
            Controls.Add(_leftList);
            Controls.Add(_centerPanel);
            Controls.Add(_rightList);

            ApplyControlTheme();
        }

        private Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                Size = new Size(36, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = ZeroFontCache.Get(8f, FontStyle.Bold)
            };
        }

        private void BindLists()
        {
            _leftList.BeginUpdate();
            _rightList.BeginUpdate();

            _leftList.Items.Clear();
            foreach (var item in _availableItems)
                _leftList.Items.Add(item);

            _rightList.Items.Clear();
            foreach (var item in _selectedItems)
                _rightList.Items.Add(item);

            _leftList.EndUpdate();
            _rightList.EndUpdate();
        }

        public void TransferSelectedToRight()
        {
            var selected = _leftList.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                _availableItems.Remove(item);
                _selectedItems.Add(item);
            }
            BindLists();
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferAllToRight()
        {
            if (_availableItems.Count == 0) return;

            _selectedItems.AddRange(_availableItems);
            _availableItems.Clear();
            BindLists();
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferSelectedToLeft()
        {
            var selected = _rightList.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                _selectedItems.Remove(item);
                _availableItems.Add(item);
            }
            BindLists();
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferAllToLeft()
        {
            if (_selectedItems.Count == 0) return;

            _availableItems.AddRange(_selectedItems);
            _selectedItems.Clear();
            BindLists();
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutPositions();
        }

        private void UpdateLayoutPositions()
        {
            if (_leftList == null || _rightList == null || _centerPanel == null) return;

            int headerH = 22;
            int centerW = 48;
            int listW = Math.Max(50, (Width - centerW - 16) / 2);
            int listH = Math.Max(40, Height - headerH - 8);

            _leftHeader.SetBounds(4, 2, listW, headerH);
            _leftList.SetBounds(4, headerH + 2, listW, listH);

            _centerPanel.SetBounds(4 + listW + 4, headerH + 2, centerW, listH);

            int btnGap = 6;
            int btnH = 28;
            int totalBtnH = (btnH * 4) + (btnGap * 3);
            int startY = Math.Max(0, (listH - totalBtnH) / 2);

            _btnRight.SetBounds(6, startY, 36, btnH);
            _btnAllRight.SetBounds(6, startY + btnH + btnGap, 36, btnH);
            _btnLeft.SetBounds(6, startY + (btnH + btnGap) * 2, 36, btnH);
            _btnAllLeft.SetBounds(6, startY + (btnH + btnGap) * 3, 36, btnH);

            _rightHeader.SetBounds(4 + listW + centerW + 8, 2, listW, headerH);
            _rightList.SetBounds(4 + listW + centerW + 8, headerH + 2, listW, listH);
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            ApplyControlTheme();
            Invalidate();
        }

        private void ApplyControlTheme()
        {
            if (IsDisposed) return;
            if (_leftHeader == null || _rightHeader == null || _leftList == null || _rightList == null) return;

            var c = ZeroTheme.Colors;

            _leftHeader.ForeColor = c.TextPrimary;
            _rightHeader.ForeColor = c.TextPrimary;

            _leftList.BackColor = c.Surface;
            _leftList.ForeColor = c.TextPrimary;
            _rightList.BackColor = c.Surface;
            _rightList.ForeColor = c.TextPrimary;

            var buttons = new[] { _btnRight, _btnAllRight, _btnLeft, _btnAllLeft };
            foreach (var btn in buttons)
            {
                if (btn != null && !btn.IsDisposed)
                {
                    btn.BackColor = c.Background;
                    btn.ForeColor = c.Primary;
                    btn.FlatAppearance.BorderColor = c.BorderSubtle;
                }
            }
        }
    }

    [Obsolete("ZeroTransferList is deprecated and will be removed in 5 release cycles. Please migrate to ZTransferList instead.")]
    [ToolboxItem(false)]
    public class ZeroTransferList : ZTransferList { }
}
